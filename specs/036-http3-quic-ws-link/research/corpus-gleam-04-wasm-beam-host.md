# Corpus — Gleam/AtomVM stack — Cluster 04: WASM host networking + native side-process options

2026-06-27

Feature 036 (HTTP/3 QUIC + WebSocket channel-link prototype). This cluster investigates the
networking reality of running AtomVM (the BEAM the Gleam stack targets) under a WASM host
(browser or Node), what UDP/QUIC such a host can or cannot reach, browser WebTransport as a
genuine HTTP/3 path, and the native side-process bridging architecture that lets Gleam/AtomVM
logic drive a *real* QUIC endpoint. Informs FR-009/FR-010 (Gleam stack interchangeability), FR-013
(LAN demo), constitution II (honest reporting).

---

### [1] Networking — Emscripten documentation
- **URL**: https://emscripten.org/docs/porting/networking.html
- **Type / version / date**: Official Emscripten docs (dev 6.0.x), living document, fetched 2026-06-27
- **Architectural concern**: failure-modes
- **Close-read findings**:
  - By default Emscripten emulates POSIX socket calls **over the WebSocket protocol** ("by default Emscripten attempts to emulate such connections to take place over the WebSocket protocol instead"); this needs a server-side bridge (e.g. WebSockify) to translate WebSocket frames into real TCP.
  - **Direct UDP is not available in browsers**: "Direct UDP communication is not available in browsers, but as a close alternative, the WebRTC specification provides a mechanism to perform UDP-like communication with WebRTC Data Channels." So raw UDP — the substrate QUIC requires — is categorically off-limits to browser WASM.
  - UDP "emulated" over WebSocket inherits TCP semantics (no packet drop, reliable, ordered) — it is not real datagram UDP, so it cannot carry a QUIC handshake.
  - A `tools/websocket_to_posix_proxy/` server exists for fuller POSIX socket support: it proxies `socket/bind/connect/listen/accept` over WebSockets and performs the **native** TCP/UDP calls on the proxy host's behalf. This is itself a native side-process bridge — the WASM side never holds a real socket.
- **Informs**: FR-010 (Gleam genuine-QUIC feasibility), constitution II
- **Confidence**: high

### [2] Building to WebAssembly — Emscripten documentation
- **URL**: https://emscripten.org/docs/compiling/WebAssembly.html
- **Type / version / date**: Official Emscripten docs (dev 6.0.x), fetched 2026-06-27
- **Architectural concern**: packaging
- **Close-read findings**:
  - `emcc` emits a `.wasm` (compiled code) plus a `.js`/`.mjs` glue file that loads and runs it; you execute the JS, which instantiates the WASM. This is the build shape AtomVM's emscripten platform uses.
  - The same output can run on the Web, under Node.js, and in standalone wasm runtimes — the *runtime host* (browser vs Node) determines which host APIs (and therefore which networking) are reachable.
  - Browsers cannot `file://`-XHR the `.wasm`; it must be served over HTTP(S), which interacts with the COOP/COEP requirement AtomVM adds (source [3]).
- **Informs**: FR-009 (stack packaging parity), FR-013
- **Confidence**: high

### [3] Getting Started Guide (WebAssembly / NodeJS / browser) — AtomVM documentation
- **URL**: https://doc.atomvm.org/main/getting-started-guide.html
- **Type / version / date**: Official AtomVM docs, main / 0.8.0-dev, fetched 2026-06-27
- **Architectural concern**: packaging
- **Close-read findings**:
  - AtomVM has a real **emscripten/WASM build** producing `AtomVM.wasm` + a JS/`.mjs` glue file; **two distinct link configurations** exist — one for **NodeJS**, one for the **browser** (the browser build adds `AtomVM.worker.js`).
  - NodeJS invocation is a plain CLI: `node /path/to/Atomvm-node-<version>.mjs /path/to/myapp.avm`. The browser build runs the same `.avm` bytecode but inside the page.
  - Browser execution **requires SharedArrayBuffer**, which forces serving over localhost/HTTPS with `Cross-Origin-Opener-Policy` and `Cross-Origin-Embedder-Policy` (COOP/COEP) headers; a service-worker trick is offered when headers can't be set.
  - The guide documents console/timer/run semantics but **says nothing about sockets or TCP/UDP** on the emscripten platform — a telling silence, corroborated by [4].
- **Informs**: FR-009 (Gleam-on-AtomVM packaging), FR-013
- **Confidence**: high

### [4] Network Programming Guide — AtomVM documentation
- **URL**: https://doc.atomvm.org/main/network-programming-guide.html
- **Type / version / date**: Official AtomVM docs, main / 0.8.0-dev, fetched 2026-06-27
- **Architectural concern**: failure-modes
- **Close-read findings**:
  - AtomVM exposes Erlang/OTP-style `gen_tcp`, `gen_udp`, and a subset of the OTP `socket` API (inet domain, stream+dgram types, tcp+udp protocols) — but only a **strict subset** of OTP.
  - The entire network guide is framed around **ESP32 and Pico W/Pico 2 W WiFi**; it gives **no networking story for the emscripten/WASM platform** (nor for generic_unix in this section). AtomVM networking is platform-driver-specific, and the emscripten port does not ship a TCP/UDP driver wired to host sockets.
  - Implication: a Gleam program calling `gen_udp`/`socket` under AtomVM-on-WASM has no platform driver behind it — the call is unsupported on that platform, not merely sandbox-blocked.
- **Informs**: FR-010 (genuine-QUIC feasibility — the decisive negative), constitution II
- **Confidence**: med (negative inferred from documented platform scope + silence; warrants a build-time probe before final claim)

### [5] UDP/datagram sockets (`dgram`) — Node.js documentation
- **URL**: https://nodejs.org/api/dgram.html
- **Type / version / date**: Official Node.js API docs (v26.x), fetched 2026-06-27
- **Architectural concern**: concurrency
- **Close-read findings**:
  - Node's `dgram` is a **genuine UDP datagram** implementation (`dgram.createSocket('udp4'|'udp6')`, `.bind()`, `.send()`); a bound socket keeps the event loop alive to receive datagrams. This is real UDP — the substrate QUIC needs.
  - Crucially, `dgram` is a **Node host API available to JavaScript**, not something Emscripten's socket emulation maps to automatically: by default Emscripten routes sockets through WebSocket emulation (source [1]) even under Node, so AtomVM-on-WASM does **not** transparently get `dgram` UDP.
  - To reach real UDP from a WASM-hosted AtomVM under Node you would have to bridge out to the JS host explicitly (custom glue / a proxy), i.e. the host JS opens `dgram` and shuttles bytes to/from WASM — again a side-process/host-bridge architecture, not in-VM QUIC.
- **Informs**: FR-010, FR-013 (a Node-side bridge is the only "all-JS-host" UDP path)
- **Confidence**: high (dgram capability); med (the explicit-bridge requirement is inferred from [1]+[4])

### [6] WebTransport API — MDN Web Docs
- **URL**: https://developer.mozilla.org/en-US/docs/Web/API/WebTransport_API
- **Type / version / date**: MDN, Baseline 2026 (cross-browser since March 2026), fetched 2026-06-27
- **Architectural concern**: handshake
- **Close-read findings**:
  - WebTransport transmits over **HTTP/3 Transport, i.e. real QUIC/UDP** — "a modern update to WebSockets, transmitting data between client and server using HTTP/3 Transport." This is a *genuine* browser-side real-QUIC path (the browser's own QUIC stack does the handshake), unlike Emscripten socket emulation.
  - Offers both **reliable ordered streams** (uni/bidirectional, via `createBidirectionalStream()`) and **unreliable datagrams** (`WebTransport.datagrams`) — covering the FR's stream-multiplexing and would-be datagram needs.
  - **Secure-context only (HTTPS), explicit port required**, and available in Web Workers (matching AtomVM's worker-based browser build, [3]).
  - It is a browser **client** API: the *server* is still a native QUIC/HTTP/3 endpoint (C#/Rust/Go). So WebTransport gives the browser real QUIC but does not make AtomVM itself a QUIC endpoint.
- **Informs**: FR-009/FR-010 (legit browser real-QUIC path worth noting), FR-013
- **Confidence**: high

### [7] WebTransport (self-signed certs / serverCertificateHashes) — quic-go docs
- **URL**: https://quic-go.net/docs/webtransport/
- **Type / version / date**: quic-go project docs, living, fetched 2026-06-27
- **Architectural concern**: cert-trust
- **Close-read findings**:
  - WebTransport's `serverCertificateHashes` option "makes it possible to use certificates **not signed by a Certificate Authority (CA)**" — the browser pins a SHA-256 hash of the server cert instead of doing CA-chain validation. This is the exact shape of FR-003 (shared self-signed cert, out-of-band trust pinning, no public CA).
  - (Spec detail from the W3C/IETF side, to confirm before relying on it: hash-pinned WebTransport certs are constrained — short validity window, ECDSA P-256 — but the *mechanism* directly matches the feature's manual-pinning trust model.)
  - Confirms a browser can connect to a self-signed QUIC/HTTP3 endpoint without a domain name or CA, which is the trust posture 036 wants.
- **Informs**: FR-003 / SC-005 (trust anchor), FR-010
- **Confidence**: med (mechanism high; exact cert constraints need a W3C-spec confirm)

### [8] QUIC support in .NET — Microsoft Learn
- **URL**: https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-overview
- **Type / version / date**: Microsoft Learn, .NET 9 (stable as of .NET 9; doc dated 2023-03, updated 2024-11), fetched 2026-06-27
- **Architectural concern**: handshake
- **Close-read findings**:
  - `System.Net.Quic` (public+stable from .NET 9) wraps native **MsQuic**: `QuicListener` (server accept), `QuicConnection` (both roles, ConnectAsync), `QuicStream` (uni/bidirectional, built-in multiplexing). `IsSupported` must be checked (libmsquic + TLS 1.3 presence).
  - QUIC **mandates TLS 1.3** and requires **ALPN** (`ApplicationProtocols` / `SslApplicationProtocol`) on both listener and client — the client's protocol list must be a subset of the listener's; mismatch fails the handshake (maps to FR-019 ALPN/version failure reporting).
  - Server needs a TLS certificate (`ServerCertificate` on `SslServerAuthenticationOptions`); client validates via `SslClientAuthenticationOptions` with a `TargetHost` — both customizable to accept a pinned self-signed cert (FR-003).
  - Platform: msquic.dll ships with .NET on Windows 11/Server 2022+; Linux needs `libmsquic`; macOS partial via Homebrew. This is the **native side-process** that would be the genuine QUIC endpoint driven by Gleam logic.
- **Informs**: FR-018 (reuse spec 025 C# link seam), FR-010 (native endpoint the side-process pattern drives)
- **Confidence**: high

### [9] Ports (open_port / external-program IPC) — Erlang System Documentation
- **URL**: https://www.erlang.org/doc/system/c_port.html
- **Type / version / date**: Official Erlang/OTP docs (v28.x), fetched 2026-06-27
- **Architectural concern**: WS-framing (length-prefixed IPC framing)
- **Close-read findings**:
  - Erlang's canonical external-program bridge is `open_port/2` with `{spawn, ExtPrg}`: byte-oriented, asynchronous message passing between a BEAM process and a native program over the program's fd 0/1 (stdin/stdout).
  - `{packet, N}` (N ∈ {1,2,4}) frames each message with an N-byte length prefix — a ready-made, well-defined framing for shuttling QUIC-stream payloads between Gleam/AtomVM and a native QUIC side-process without inventing a wire format.
  - All traffic flows through the owning (connected) process; messages are delivered asynchronously — fits a Gleam actor owning the link and a native QUIC endpoint as the port program. (Caveat: confirm AtomVM's emscripten/Node port supports `open_port`-spawn of OS processes; on bare WASM this likely degrades to a host-JS shim rather than true `{spawn,...}`.)
- **Informs**: FR-010 (how Gleam logic drives a native endpoint), FR-018
- **Confidence**: med (mechanism high for OTP; AtomVM-WASM support for process-spawn ports unverified)

### [10] Orbital — build & flash Gleam projects to AtomVM (+ Welcome to AtomVM)
- **URL**: https://github.com/giacomocavalieri/orbital/  (and https://doc.atomvm.org/main/welcome-to-atomvm.html)
- **Type / version / date**: Community tool (Orbital) + official AtomVM intro, fetched 2026-06-27
- **Architectural concern**: packaging
- **Close-read findings**:
  - AtomVM reuses the existing Erlang/Elixir/**Gleam** toolchains: Gleam source compiles to **BEAM bytecode**, packed into an `.avm` AtomVM runs. So "the Gleam stack" = Gleam→BEAM→AtomVM, and AtomVM's platform (ESP32 / generic_unix / **emscripten**) decides what host facilities (incl. networking) exist.
  - Orbital targets *device* flashing (ESP32-class), where `gen_tcp`/`gen_udp` drivers exist — i.e. genuine networking lives on the native/device AtomVM ports, **not** the WASM port.
  - Reinforces the split: the WASM host is a viable *execution* environment for Gleam logic, but the real-network capability is a property of a non-WASM AtomVM platform or an out-of-VM native endpoint.
- **Informs**: FR-009 (Gleam stack definition/packaging)
- **Confidence**: med (Orbital is community tooling; AtomVM/Gleam toolchain claim high)

### [11] Specific Browser Limitations / async networking — Emscripten documentation
- **URL**: https://emscripten.org/docs/porting/guidelines/browser_limitations.html
- **Type / version / date**: Official Emscripten docs (dev 6.0.x), fetched 2026-06-27
- **Architectural concern**: backpressure/flow-control
- **Close-read findings**:
  - Emscripten supports libc networking functions **only in asynchronous (non-blocking) form**, because the underlying JS networking primitives are async — blocking socket reads/connects don't translate to the browser event loop.
  - This means even the WebSocket-emulated socket path imposes an async, event-loop-bound flow-control model; a synchronous QUIC/UDP read loop is not portable to browser WASM without a worker + async restructuring.
  - Confirms that browser WASM networking is fundamentally constrained to what the host JS exposes asynchronously (WebSocket, WebRTC, WebTransport) — never a raw OS socket.
- **Informs**: FR-010, constitution II (honest limitation statement)
- **Confidence**: high

### [12] websocket_to_posix_proxy / WebRTC alternative — Emscripten networking (proxy section)
- **URL**: https://emscripten.org/docs/porting/networking.html (proxy + WebRTC subsections)
- **Type / version / date**: Official Emscripten docs (dev 6.0.x), fetched 2026-06-27
- **Architectural concern**: stream-multiplexing
- **Close-read findings**:
  - The `websocket_to_posix_proxy` server is the supported way to get "real" POSIX sockets from WASM: WASM↔WebSocket↔**proxy host performs native TCP/UDP**. It proxies socket/bind/connect/listen/accept but **not** `poll`/`select`/`close` (use `shutdown`). This is architecturally identical to the "native side-process" pattern — the genuine socket lives in a native helper, never in the WASM module.
  - For UDP-like behaviour with real packet-loss semantics, WebRTC Data Channels are the only browser option, but Emscripten exposes **no C/C++ API** for WebRTC (or WebTransport) — access is via hand-written JS interop only.
  - Net: any "real QUIC" from WASM-hosted AtomVM must terminate in a native helper process (proxy or purpose-built endpoint) reached over WebSocket/host-IPC — there is no in-WASM QUIC stack.
- **Informs**: FR-010 (architecture), FR-013, constitution II
- **Confidence**: high

---

## Cluster feasibility verdict

- **A WASM-hosted AtomVM cannot open a genuine QUIC/UDP socket.** In the **browser**, Emscripten flatly states direct UDP is unavailable and emulates sockets over WebSocket (TCP semantics) — no datagram substrate, so no QUIC handshake ([1],[11]). Under **Node**, real UDP exists as the JS `dgram` API ([5]), but Emscripten does not map WASM sockets onto `dgram` (it uses WebSocket emulation even there), and — decisively — **AtomVM's emscripten platform ships no `gen_tcp`/`gen_udp` driver at all** ([3],[4]): AtomVM networking is documented only for ESP32/Pico WiFi ports. So the limit is *both* sandbox (browser) and *missing platform driver* (AtomVM-WASM), not a tuning knob.

- **The honest architecture for a "Gleam/AtomVM stack" that still reaches real QUIC is a native side-process.** Gleam→BEAM logic runs on AtomVM; a separate **native QUIC endpoint** (the C#/.NET `System.Net.Quic`/MsQuic endpoint from FR-018/spec 025, or a Rust/Python equivalent) holds the actual QUIC connection, and the two are joined by a local IPC channel ([8]). Under native AtomVM this is Erlang `open_port`/`{spawn,…}` with `{packet,N}` length-prefixed framing ([9]); under a WASM host it is the Emscripten WebSocket-proxy shape — WASM↔WebSocket↔native helper that performs the real socket calls ([1],[12]). Either way the WASM/BEAM side is the *driver*, the native helper is the *QUIC endpoint*.

- **This must be reported under constitution II as exactly that — not as "AtomVM speaks QUIC."** The truthful claim is: *"The Gleam/AtomVM logic drives a real on-wire QUIC handshake via a native side-process endpoint; the BEAM/WASM runtime itself does not and cannot terminate QUIC."* Any demo wording that implies in-VM QUIC from WASM would be a misrepresentation. FR-010's genuine-QUIC feasibility for the Gleam stack should be recorded as **conditionally yes (via native side-process)**, with the in-WASM-QUIC option marked **infeasible** and evidenced by [1]/[4]/[11].

- **WebTransport is a legitimate, distinct browser-side real-QUIC path worth noting.** Unlike Emscripten socket emulation, WebTransport uses the *browser's own* QUIC/HTTP-3 stack — real streams and real unreliable datagrams ([6]) — and `serverCertificateHashes` supports exactly the self-signed, CA-less, hash-pinned trust model of FR-003/SC-005 ([7]). But it is a *client* API: the peer is still a native QUIC server, and it is reachable from page JS, not from inside the AtomVM WASM module without hand-written interop ([12]). It is a credible alternative front-end for a browser-hosted Gleam UI, not a way to make AtomVM itself a QUIC endpoint.

- **Recommended posture for 036:** keep the C#/.NET `System.Net.Quic` endpoint as the single genuine QUIC implementation; model the "Gleam/AtomVM stack" as **Gleam logic + native QUIC side-process over a length-prefixed local IPC**, and (optionally) note WebTransport as a future browser-native real-QUIC client. This satisfies FR-009/FR-010 honestly and keeps FR-013's LAN demo backed by a real handshake rather than WebSocket-emulated pseudo-UDP.

- **Open items to confirm before final claims (flagging, per constitution II):** (a) whether AtomVM's emscripten port supports `open_port`-style OS-process spawn at all (likely degrades to a host-JS shim) ([9]); (b) exact WebTransport self-signed cert constraints (validity window, ECDSA P-256) from the W3C spec ([7]); (c) a build-time probe that AtomVM-WASM genuinely lacks any socket driver, to upgrade [4]'s confidence from med to high.

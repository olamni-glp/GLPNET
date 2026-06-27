# Corpus — Gleam/AtomVM stack — Cluster 02: genuine QUIC + TLS feasibility (HIGHEST RISK)

2026-06-27

Scope: decide, with evidence, whether the second transport stack (Gleam on AtomVM)
can perform a **genuine** QUIC handshake (real UDP + TLS 1.3 + QUIC transport, RFC
9000/9001/9114) — on any AtomVM target (WASM/Emscripten via Node, or generic_unix) —
or whether genuine QUIC is infeasible and the spec's honest-feasibility edge case
(FR-010, constitution II) must be triggered. Sources: AtomVM GitHub/docs, Emscripten
WASM networking docs, BEAM QUIC libraries (quicer/msquic), Gleam ecosystem.

---

### [1] AtomVM — Network Programming Guide
- **URL**: https://doc.atomvm.org/main/network-programming-guide.html
- **Type / version / date**: Official AtomVM docs, `main` (0.8.0-dev), accessed 2026-06-27
- **Architectural concern**: handshake (transport substrate availability)
- **Close-read findings**:
  - The guide covers only `gen_udp` and `gen_tcp` socket APIs (plus WiFi/AP setup for ESP32/Pico W). UDP/TCP datagram and stream sockets are the *entire* networking surface documented.
  - **No mention of TLS, SSL, DTLS, QUIC, or HTTP/3 anywhere** in the networking guide. The stated extension path is "HTTP, MQTT, and other protocols" built by third parties over the low-level sockets — nothing QUIC-class.
  - Explicitly notes "IPv6 addresses are not yet supported" — a marker of how early the network stack is; QUIC-grade transport features are far beyond current scope.
  - Implication: even on a target where UDP exists, there is no AtomVM-native QUIC/TLS layer to sit on top of it. A real QUIC endpoint would have to be implemented from scratch in BEAM bytecode or supplied as a NIF (see [3], [5], [7]).
- **Informs**: FR-009/FR-010 (honest feasibility of the 2nd stack), constitution II (no simulation passed off as real), SC-007
- **Confidence**: high

### [2] AtomVM — Changelog (ssl interface, socket, crypto, WASM)
- **URL**: https://doc.atomvm.org/main/CHANGELOG.html
- **Type / version / date**: Official changelog, spans v0.6.0-alpha.2 → 0.7.x/0.8.0-dev, accessed 2026-06-27
- **Architectural concern**: handshake / cert-trust (TLS readiness)
- **Close-read findings**:
  - "Minimal implementation of the OTP `ssl` interface" added (v0.6.0-alpha.2). It is **client-oriented and severely constrained**: "active mode is not supported right now, ssl must be explicitly set to `{active, false}` … otherwise it will crash." A TLS stack that crashes in active mode is nowhere near able to drive a QUIC handshake state machine.
  - **No TLS server / listener** is documented — the ssl support is `ssl:connect`-shaped only. QUIC needs full TLS 1.3 handshake exposure (key schedule, transport parameters, 0-RTT/1-RTT keys) via the RFC 9001 binding — not a black-box `ssl:connect`.
  - Crypto via **Mbed-TLS** (OpenSSL removed): `crypto:strong_rand_bytes/1`, `crypto:hash/2`, `crypto:one_time/4,5`, AEAD, Ed25519 — primitives exist, but only on generic_unix/ESP32/RP2040, and there is **no QUIC packet-protection / header-protection plumbing** that would consume them.
  - WASM: "WASM32 JIT backend for Emscripten platform" (v0.7.0-beta.0), ES6 `.mjs` modules. The Emscripten port is real and improving — but inherits browser/Node socket limits ([5], [6]).
  - **"No mention of QUIC" and "no TLS distribution"** in the entire changelog history.
- **Informs**: FR-009/FR-010, constitution II, SC-007
- **Confidence**: high

### [3] AtomVM — Programmers Guide (NIFs / Ports / native code)
- **URL**: https://doc.atomvm.org/main/programmers-guide.html
- **Type / version / date**: Official docs, `main` (0.8.0-dev), accessed 2026-06-27
- **Architectural concern**: packaging (can a real-QUIC NIF be loaded at all)
- **Close-read findings**:
  - "The Espressif SDK and tool chains do not support dynamic loading of shared libraries and dynamic symbol lookup … dynamic libraries are not supported at all on the ESP32 … any code needed at runtime must be statically linked." AtomVM has **no runtime NIF/driver loading** (`erlang:load_nif/2`-by-dynamic-`.so` is not the model) — native extensions are *compiled into* the VM via `REGISTER_NIF_COLLECTION` / components at build time.
  - Consequence: the standard Erlang escape hatch — load `quicer` (an msquic NIF) at runtime — **does not exist on AtomVM**. You would have to fork AtomVM's C and statically link an entire QUIC engine into the VM, then expose hand-written NIFs. That is building a new C QUIC port, not "using AtomVM".
  - AtomVM is a *subset* BEAM: it runs unmodified `.beam` for the supported subset, but the OTP libraries a QUIC stack assumes (full `ssl`, `inet`, dirty schedulers for NIF blocking) are partly or wholly absent.
- **Informs**: FR-009/FR-010, constitution II
- **Confidence**: high

### [4] AtomVM — Extensions catalog
- **URL**: https://atomvm.org/extensions/
- **Type / version / date**: Official extensions index, accessed 2026-06-27
- **Architectural concern**: packaging (ecosystem availability)
- **Close-read findings**:
  - Extensions are grouped Tools / Drivers (NIF-or-port and BEAM) / Libraries. The catalog is dominated by hardware peripherals (displays AtomGL/SSD1306, DHT/BME280/GPS sensors, addressable LEDs).
  - **The only networking extension is `atomvm_mqtt_client` (ESP32 MQTT over TCP).** There is **no QUIC, no TLS server, no HTTP/3, no DTLS, no WebTransport** extension — and no community work toward one.
  - Confirms there is no off-the-shelf path: nothing to depend on, nothing in progress.
- **Informs**: FR-009/FR-010, constitution II, SC-007
- **Confidence**: high

### [5] Emscripten — Networking documentation
- **URL**: https://emscripten.org/docs/porting/networking.html
- **Type / version / date**: Official Emscripten docs (6.0.0-git dev), accessed 2026-06-27
- **Architectural concern**: handshake (UDP substrate on the WASM target)
- **Close-read findings**:
  - **"Direct UDP communication is not available in browsers."** Raw UDP — the literal substrate QUIC requires — cannot be opened from WASM in a browser. WebRTC DataChannels are offered only as a "UDP-like" alternative (and Emscripten provides no C/C++ WebRTC API anyway).
  - TCP is likewise unavailable directly; Emscripten emulates POSIX sockets either as **TCP-over-WebSockets** (needs a Websockify-style server proxy) or via the **`websocket_to_posix_proxy`** server that tunnels all POSIX socket calls to a native side-process which performs the real TCP/UDP.
  - Therefore, on the WASM/Emscripten AtomVM target, *any* "UDP socket" is necessarily proxied through a **native side-process over WebSockets** — the wire-level QUIC datagrams would be produced by that proxy, not by AtomVM. That is not AtomVM doing genuine QUIC; it is AtomVM driving an external QUIC speaker.
  - WebSockets are the only near-native transport ("the closest to TCP on the web") — relevant to the WS half of the feature, not the QUIC half.
- **Informs**: FR-009/FR-010, constitution II (no simulation), SC-007
- **Confidence**: high

### [6] WebAssembly/design — "does support for socket(udp and tcp)?" (#1251) + corroborating sources
- **URL**: https://github.com/WebAssembly/design/issues/1251 ; corroborated by https://hacks.mozilla.org/2017/06/introducing-humblenet-a-cross-platform-networking-library-that-works-in-the-browser/ and https://github.com/paullouisageneau/datachannel-wasm
- **Type / version / date**: WASM design issue (2018, long-standing) + Mozilla Hacks (2017) + active WASM datachannel lib; accessed 2026-06-27
- **Architectural concern**: handshake (fundamental WASM transport constraint)
- **Close-read findings**:
  - Browser WASM has **no raw socket primitive by design** (sandbox); the issue remains the canonical "no, and use the web APIs" reference. You "aren't able to send UDP packets directly in the browser, and you will never be able to ship your own."
  - The only UDP-flavoured browser transports are **WebRTC DataChannels** and **WebTransport** (HTTP/3-over-QUIC exposed *as a browser API* — the browser performs the QUIC, the app never sees packets). WASM libraries "don't actually implement UDP — they wrap the browser's native APIs."
  - This is a *platform* limit, not an AtomVM bug: no BEAM-on-WASM can transcend it. Genuine, app-controlled QUIC bytes on the wire from in-browser WASM are impossible.
  - Caveat for completeness: this is browser-specific. A standalone WASI runtime *can* get sockets via `wasi-sockets`, but AtomVM's WASM port targets the Emscripten/Node-and-browser model ([2],[5]), not WASI raw UDP — and even WASI raw UDP would still need a QUIC/TLS implementation on top ([1]–[4]).
- **Informs**: FR-009/FR-010, constitution II, SC-007
- **Confidence**: high

### [7] quicer — QUIC for Erlang & Elixir (README)
- **URL**: https://github.com/emqx/quic/blob/main/README.md
- **Type / version / date**: EMQX quicer, `main`, "Project Status: Preview", accessed 2026-06-27
- **Architectural concern**: packaging (the only real BEAM QUIC option)
- **Close-read findings**:
  - quicer is "an **msquic NIF binding**" — it is a **C NIF wrapping Microsoft's msquic** C library; QUIC + TLS 1.3 is done in native code, not BEAM.
  - Build requires **`cmake3.16+`** (to build the msquic native lib) and `rebar3`, OTP25+. OS support: Linux/macOS supported, Windows "help needed".
  - This is the realistic way "BEAM does genuine QUIC" — and it is fundamentally **incompatible with AtomVM**: it needs runtime NIF loading + full OTP, which AtomVM does not provide ([3]). It would run under standard Erlang/OTP — i.e. **not AtomVM, and not Gleam-on-AtomVM**.
  - It does confirm the side-process option's plausibility: a *standard-OTP* (or any native) process running quicer/msquic could be the real-QUIC speaker that an AtomVM/Gleam control plane orchestrates.
- **Informs**: FR-009/FR-010 (honest fallback shape), constitution II
- **Confidence**: high

### [8] quicer — HexDocs (runtime/status)
- **URL**: https://quicer.hexdocs.pm/readme.html
- **Type / version / date**: quicer v0.2.x HexDocs, "Project Status: Preview", accessed 2026-06-27
- **Architectural concern**: stream-multiplexing / concurrency (maturity of the only BEAM QUIC)
- **Close-read findings**:
  - Confirms OTP25+, msquic C-library dependency via NIF, cmake build, Linux/macOS only, **Preview** maturity — i.e. even on *standard* OTP, BEAM QUIC is pre-stable.
  - msquic NIFs run on the BEAM scheduler (dirty-scheduler territory for blocking native calls) — machinery AtomVM lacks. Reinforces that quicer presupposes a full OTP runtime AtomVM is a subset of.
  - Establishes the honest baseline: "genuine QUIC on BEAM" already means "native msquic behind a NIF on full OTP." AtomVM removes both prerequisites (full OTP, runtime NIF loading).
- **Informs**: FR-009/FR-010, constitution II
- **Confidence**: high

### [9] Gleam — package index & awesome-gleam
- **URL**: https://packages.gleam.run/ ; https://github.com/gleam-lang/awesome-gleam ; https://hexdocs.pm/gleam_http/
- **Type / version / date**: Official Gleam package index + curated list, accessed 2026-06-27
- **Architectural concern**: packaging (Gleam-side library availability)
- **Close-read findings**:
  - Gleam's HTTP story is `gleam_http` (Request/Response types) + servers like `wisp`/`mist` — **HTTP/1.x over TCP**. **No QUIC, no HTTP/3, no DTLS** package exists in the Gleam ecosystem.
  - Gleam has no transport of its own; it FFIs into Erlang/Elixir. So "Gleam QUIC" reduces to "call a BEAM QUIC lib" = quicer/msquic ([7],[8]) — which only runs on full OTP, not AtomVM.
  - Confirms there is nothing to reuse at the Gleam layer either; the second stack would be greenfield against a substrate (AtomVM) that itself lacks QUIC.
- **Informs**: FR-009/FR-010, SC-007
- **Confidence**: high

### [10] ElixirForum — "QUICER: Next Generation Transport Protocol Library for BEAM"
- **URL**: https://elixirforum.com/t/quicer-next-generation-transport-protocol-library-for-beam/51414
- **Type / version / date**: Community talk/discussion thread (EMQX author), 2022→, accessed 2026-06-27
- **Architectural concern**: failure-modes / concurrency (how BEAM actually gets QUIC)
- **Close-read findings**:
  - Frames the *only* mature BEAM QUIC path as the msquic-NIF approach — the community consensus is that QUIC on BEAM = bind native msquic, because reimplementing QUIC+TLS1.3 in pure BEAM is impractical.
  - Reinforces that there is **no pure-BEAM QUIC implementation** to load onto AtomVM as plain `.beam` (which would be the only AtomVM-compatible route, since AtomVM can't load the NIF).
  - Strengthens the verdict: the realistic genuine-QUIC artifact is native (msquic), and AtomVM cannot host native NIFs of that class at runtime.
- **Informs**: FR-009/FR-010, constitution II
- **Confidence**: med (forum/talk, but consistent with [7][8])

### [11] AtomVM — ssl/TLS 1.3 fix + Mbed-TLS migration (release notes)
- **URL**: https://www.atomvm.net/doc/v0.6.5/CHANGELOG.html (and Releases: https://github.com/atomvm/AtomVM/releases)
- **Type / version / date**: AtomVM 0.6.5+ changelog/releases, accessed 2026-06-27
- **Architectural concern**: cert-trust / handshake (TLS substrate state)
- **Close-read findings**:
  - A bug fix "crash on macOS due to missing call to `psa_crypto_init` for TLS 1.3" shows AtomVM's TLS is wired to **Mbed-TLS PSA crypto** and TLS 1.3 *handshakes can run* in the minimal `ssl` client — but only as the constrained `ssl:connect` client from [2] (no server, no active mode).
  - There is **no API surface to extract TLS 1.3 secrets/transport parameters** the way RFC 9001 QUIC requires; the `ssl` layer is opaque TLS-over-TCP, not a QUIC crypto provider. So even the existing TLS code cannot be repurposed to feed a QUIC handshake.
  - Mbed-TLS *does* ship experimental QUIC/TLS hooks upstream, but AtomVM exposes none of that to BEAM.
- **Informs**: FR-009/FR-010, constitution II, SC-007
- **Confidence**: med

### [12] AtomVM — Emscripten/WASM port (news + getting-started)
- **URL**: https://www.atomvm.net/news/ ; https://deepwiki.com/atomvm/AtomVM/1.1-getting-started
- **Type / version / date**: AtomVM project news + overview, 2024–2025, accessed 2026-06-27
- **Architectural concern**: packaging / concurrency (what the WASM target actually is)
- **Close-read findings**:
  - The Emscripten port runs AtomVM "in the browser" and under Node via Wasm — confirming the realistic Gleam/AtomVM "Node WASM host" deployment named in plan.md is the **browser/Node Emscripten model**, which is exactly the model with **no raw UDP** ([5],[6]).
  - On this target the VM's networking is whatever Emscripten/Node bridges expose — WebSockets natively, sockets only via proxy — so the WASM AtomVM cannot itself originate QUIC datagrams.
  - generic_unix AtomVM (CLI) has real sockets (`gen_udp`) but still no TLS-server/QUIC layer ([1]–[4]); it could *carry* UDP but has nothing to make those packets a valid QUIC handshake.
- **Informs**: FR-009/FR-010, constitution II
- **Confidence**: med

---

## Cluster feasibility verdict

- **Genuine, AtomVM-native QUIC is NOT feasible on any AtomVM target.** No AtomVM-native QUIC or HTTP/3 exists ([1],[2],[4]); the `ssl` layer is a minimal, crash-in-active-mode, client-only `ssl:connect` with no server and no RFC-9001 secret/transport-parameter export ([2],[11]) — it cannot drive a QUIC handshake even where UDP is present.

- **The WASM/Emscripten target (the plan's "Node WASM host") cannot originate QUIC at all.** Browsers/Emscripten have **no raw UDP** by platform design; UDP/TCP only exist via a native WebSocket-proxied side-process or browser WebRTC/WebTransport APIs ([5],[6]). Any "QUIC" there would be spoken by an external process, not by AtomVM — passing that off as AtomVM QUIC would violate constitution II.

- **The generic_unix AtomVM target has real UDP (`gen_udp`) but no QUIC/TLS stack on top of it** ([1],[3]). It could carry datagrams but has nothing that makes them a conformant QUIC+TLS1.3 handshake; building one would mean reimplementing QUIC+TLS in BEAM bytecode (impractical — the whole BEAM ecosystem instead binds native msquic, [7][8][10]).

- **The standard BEAM answer — `quicer` (msquic C NIF) — is incompatible with AtomVM.** AtomVM has no runtime dynamic NIF/driver loading (native code must be statically compiled into a forked VM) and is a BEAM subset lacking full OTP/dirty schedulers ([3]); quicer needs OTP25+, cmake-built msquic, runtime NIF loading ([7],[8]). "Gleam QUIC" reduces to "call quicer," which only runs on full OTP — i.e. **not** Gleam-on-AtomVM ([9],[10]).

- **Honest options that remain (in order of integrity):** (a) **Native side-process** — an AtomVM/Gleam control plane orchestrates an external genuine-QUIC speaker (standard-OTP `quicer`/msquic, or any real QUIC binary) and reports the handshake honestly as *external*, not AtomVM-native; (b) **report `real_quic=false` for the Gleam/AtomVM stack** and document the platform limitation per FR-010's edge case; (c) **defer/descope** the second stack, keeping C#/.NET (System.Net.Quic/MsQuic) as the sole genuine-QUIC stack.

- **Recommendation:** Treat FR-009/FR-010's "two interchangeable stacks" as **not fully satisfiable in the genuine-QUIC sense by Gleam/AtomVM.** Per constitution II (no simulation passed off as real) and the spec's honest-feasibility edge case, the Gleam/AtomVM stack should either (a) be implemented as a thin Gleam control layer over a native msquic side-process with `real_quic` truthfully attributed to that side-process, or (b) be reported as a feasibility limitation (`real_quic=false`) and deferred — never faked. The WS half over plain WebSockets is feasible on AtomVM; the QUIC half is not.

- **Confidence: HIGH.** Multiple independent, dated, official sources (AtomVM docs/changelog/extensions, Emscripten docs, WASM design, quicer README/HexDocs) converge with no contradicting evidence. The only residual uncertainty is upstream churn (AtomVM `ssl`/socket layers are pre-1.0 and evolving), which could *narrow* but not erase the gap within this feature's horizon.

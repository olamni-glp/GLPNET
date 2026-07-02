# Corpus — Gleam/AtomVM stack — Cluster 01: AtomVM platform overview + networking

2026-06-27

Feature 036 (HTTP/3 QUIC + WebSocket channel-link prototype) names two interchangeable
transport stacks. This cluster close-reads the SECOND stack's foundation: **AtomVM**, the
tiny Erlang/BEAM VM that runs Gleam-compiled bytecode. The goal is to establish what AtomVM
*is*, what BEAM/OTP it supports vs omits, what raw networking and TLS it actually has today,
and on which platform target a 036 Gleam stack could realistically run. The deeper
"can it do QUIC/HTTP3?" question is deferred to cluster 02 — this cluster establishes the
substrate and its hard limits.

Sources: official AtomVM docs (atomvm.org, doc.atomvm.org, github.com/atomvm/AtomVM),
the AtomVM CHANGELOG, a maintainer year-in-review, and the Gleam-on-AtomVM toolchain
projects (atomvm_packgleam, orbital).

---

### [1] Welcome to AtomVM! (official docs, main branch)
- **URL**: https://doc.atomvm.org/main/welcome-to-atomvm.html
- **Type / version / date**: Official documentation, 0.8.0-dev (main branch), 2026
- **Architectural concern**: packaging
- **Close-read findings**:
  - AtomVM is a from-scratch (ground-up) implementation of the BEAM ("Bogdan/Björn's Erlang
    Abstract Machine") for constrained devices; runs in as little as 32 KiB of RAM.
  - Feature set advertised: lightweight-process concurrency (spawn/monitor/message-passing),
    GC with shared data, pre-emptive scheduling, **SMP**, "a rich set of networking APIs for
    IoT applications that communicate over IP networks", and device protocols (GPIO/I2C/SPI/UART).
  - Primary targets are ESP32 and STM32 micro-controllers; Linux/FreeBSD/macOS are supported
    but framed as "for development and testing purposes only" — i.e. the UNIX port is a
    first-class dev/host target, not the intended production deployment.
- **Informs**: FR-009/FR-010 (AtomVM is the runtime of the 2nd stack), FR-014/FR-015 (corpus),
  Gleam-feasibility edge case (constrained-runtime framing matters for QUIC viability).
- **Confidence**: high

### [2] atomvm/AtomVM — repository README
- **URL**: https://github.com/atomvm/AtomVM
- **Type / version / date**: Official source repo README, main branch, 2026
- **Architectural concern**: packaging
- **Close-read findings**:
  - Authoritative platform list: **Linux, macOS, FreeBSD, DragonFly (generic_unix); ESP32 SoC
    (IDF/FreeRTOS); STM32 MCUs (ST HAL/LL SDK); Raspberry Pi Pico and Pico 2; Browsers and
    NodeJS with WebAssembly (emscripten).** This confirms the WASM/Emscripten target is real
    and officially supported, alongside generic_unix.
  - "Minimal Erlang VM that supports a subset of ErlangVM features and is able to run
    **unmodified BEAM binaries** on really small systems like MCUs." Any language that emits
    standard BEAM bytecode (Erlang, Elixir, Gleam) can in principle run.
  - Build deps include "**Mbed TLS** (portable TLS library, optionally required to support SSL)"
    — TLS is an optional compile-time feature, not always present.
  - Tested with BEAM compiled by OTP 21–27 (OTP 29 only on main).
- **Informs**: FR-009/FR-010 (platform-target selection: UNIX host vs WASM), FR-014/FR-015,
  Gleam-feasibility edge case.
- **Confidence**: high

### [3] Network Programming Guide (official docs, main)
- **URL**: https://doc.atomvm.org/main/network-programming-guide.html
- **Type / version / date**: Official documentation, 0.8.0-dev (main), 2026
- **Architectural concern**: backpressure/flow-control (networking primitives)
- **Close-read findings**:
  - "AtomVM supports the `gen_udp` and `gen_tcp` APIs" obeying Erlang/OTP syntax/semantics,
    but **"not all of the Erlang/OTP gen_tcp/gen_udp functionality is implemented."**
  - The networking guide is dominated by the ESP32/Pico-W `network` module (WiFi STA/AP/STA+AP
    modes), callback-driven connectivity, and resilience to network changes — i.e. the doc's
    networking story is micro-controller-centric.
  - Stated unimplemented items: **IPv6 is not yet supported**; WiFi credentials stored
    unencrypted. Scan-result counts capped per target.
  - Extensions may layer HTTP/MQTT over the low-level interfaces (no native HTTP/2 or HTTP/3).
- **Informs**: FR-009/FR-010 (the 2nd stack's transport primitives), FR-014/FR-015,
  Gleam-feasibility edge case (no IPv6, partial gen_udp = QUIC substrate concerns).
- **Confidence**: high

### [4] Socket / OTP socket interface (network guide + apidocs)
- **URL**: https://doc.atomvm.org/main/network-programming-guide.html (socket section) and
  https://www.atomvm.net/doc/v0.6.0-alpha.2/apidocs/erlang/estdlib/inet.html
- **Type / version / date**: Official docs / apidocs, 0.6.x–0.8.0-dev, 2025–2026
- **Architectural concern**: stream-multiplexing / backpressure (low-level sockets)
- **Close-read findings**:
  - AtomVM implements a **strict subset of the OTP `socket` interface** (BSD-sockets-style),
    giving finer-grained control than gen_tcp/gen_udp.
  - `socket:open/3` currently supports **domain = `inet` only; types = `stream` and `dgram`;
    protocols = `tcp` and `udp`.** Critically: `dgram`/`udp` IS present — a UDP datagram socket
    primitive exists, which is the prerequisite a QUIC substrate would need.
  - `inet` module present but minimal. No `inet6`/IPv6 domain.
- **Informs**: FR-009/FR-010, FR-014/FR-015, Gleam-feasibility edge case (UDP dgram socket is
  the foundation cluster 02 must evaluate for QUIC).
- **Confidence**: high

### [5] Differences between AtomVM and BEAM (official docs)
- **URL**: https://doc.atomvm.org/main/differences-with-beam.html
- **Type / version / date**: Official documentation, 0.7.0-dev, 2025–2026
- **Architectural concern**: concurrency / failure-modes
- **Close-read findings**:
  - **OTP support is "currently very limited":** `gen_server`, `gen_statem`, `supervisor`,
    `proc_lib`, `sys` "only implement a subset of what OTP provides." Standard library is
    "extremely limited"; OTP programs "very unlikely runnable without a lot of changes."
  - Integers capped at **256-bit** (raises `overflow`), no arbitrary-precision bignums.
  - **No bitstrings** with non-byte-aligned sizes (binaries OK) — relevant for protocol bit
    packing. **No code reloading.** NIFs/ports must be statically linked (no dynamic linking);
    no dirty schedulers.
  - **Distribution is "a work in progress"** — node monitoring and key features unimplemented,
    though BEAM↔AtomVM node connection is possible.
  - Detect runtime via `erlang:system_info(machine)` → `"ATOM"` (vs `"BEAM"`).
  - "Much slower than BEAM, even with the JIT enabled" but far less RAM.
- **Informs**: FR-009/FR-010 (capability parity between the two stacks), FR-014/FR-015,
  Gleam-feasibility edge case (constrained OTP means hand-rolled protocol logic).
- **Confidence**: high

### [6] AtomVM CHANGELOG (official docs, main)
- **URL**: https://doc.atomvm.org/main/CHANGELOG.html
- **Type / version / date**: Official changelog, spans v0.5–v0.7.x-alpha, 2024–2026
- **Architectural concern**: cert-trust / handshake (TLS history)
- **Close-read findings**:
  - **ssl: "Added minimal support for the OTP `ssl` interface"** in v0.6.0-alpha.2 — TLS exists
    but is explicitly *minimal*.
  - v0.6.0-alpha.2: **crypto on generic_unix moved from OpenSSL to Mbed-TLS** (OpenSSL removed).
  - v0.6.0-alpha.2: "Added support for the OTP `socket` interface". v0.7.0-alpha.1: async API
    for `recv`/`recvfrom`/`accept` + UDP multicast.
  - v0.6.2: ssl default flipped `{active,false}`→`{active,true}` to match OTP, **but active mode
    is not actually supported** — callers must force `{active,false}` or it crashes; also fixed a
    use-after-free in ssl code and `ssl:recv(Socket,0)` semantics. (TLS is young/fragile.)
  - v0.6.3 added a simple HTTP client (for OTA). OTP support: v0.7.0-alpha.1 dropped OTP < 26,
    added OTP-28; main added OTP-29.
- **Informs**: FR-009/FR-010 (TLS = cert-trust/handshake building block for both QUIC and WSS),
  FR-014/FR-015, Gleam-feasibility edge case.
- **Confidence**: high

### [7] AtomVM 2025 Year in Review (Davide Bettio, maintainer)
- **URL**: https://medium.com/@Bettio/atomvm-2025-year-in-review-c669597d396c
- **Type / version / date**: Maintainer blog (credible primary), Dec 2025
- **Architectural concern**: packaging / concurrency
- **Close-read findings**:
  - Four execution modes shipped in 2025: **Emulated (default), JIT (compile native at runtime),
    Native/AoT (precompile on desktop/CI), and Hybrid.** Relevant to whether a heavy protocol
    stack could be made fast enough.
  - **Distribution: "cookie auth is there, TLS distribution isn't yet"** — confirms TLS coverage
    is partial even in the latest cycle.
  - WASM front-end story is maturing via **"Popcorn"**: "it runs AtomVM compiled to WebAssembly,
    and wraps it with the tooling and APIs needed to build real front-end experiences" — a
    browser-side runtime layer above the raw emscripten port.
  - Main branch carries OTP-28 support (stable 0.6.x does not).
- **Informs**: FR-009/FR-010 (WASM target maturity), FR-014/FR-015, Gleam-feasibility edge case.
- **Confidence**: high

### [8] Gleam on AtomVM — atomvm_packgleam
- **URL**: https://github.com/karlsson/atomvm_packgleam
- **Type / version / date**: Community tool (BEAM-ecosystem), 2024–2025
- **Architectural concern**: packaging
- **Close-read findings**:
  - Gleam compiles to **BEAM bytecode via the standard Gleam compiler**; `atomvm_packgleam`
    wraps AtomVM's PackBEAM tool to bundle the `.beam` outputs into an AtomVM `.avm` archive.
  - Requires **Gleam 1.9.1+** (needs GitHub-repo dependency support). Build with
    `gleam run -m atomvm_packgleam`; `.avm` name taken from `gleam.toml`.
  - Entry point convention: the main module needs a **`pub fn start()`** (AtomVM uses the first
    module exporting `start/0` as the app entry); a `pub fn main()` is kept for `gleam run`.
  - Caveat: "**AtomVM implements a constrained subset of Erlang's standard library** — if your
    Gleam code or deps use unsupported functions you'll see a runtime error on device."
- **Informs**: FR-009/FR-010 (how Gleam actually reaches AtomVM), FR-014/FR-015,
  Gleam-feasibility edge case (stdlib-subset risk for any Gleam HTTP/QUIC library).
- **Confidence**: high

### [9] orbital — build & flash Gleam to AtomVM
- **URL**: https://github.com/giacomocavalieri/orbital/blob/main/README.md
- **Type / version / date**: Community tool (Gleam ecosystem, Giacomo Cavalieri), 2025
- **Architectural concern**: packaging
- **Close-read findings**:
  - Higher-level alternative to packgleam: `gleam add --dev orbital`, define a module with a
    `start` function, then `gleam run -m orbital flash esp32 --port /dev/...` to compile + flash.
  - Demonstrated target is **ESP32**; the workflow is device-flash-oriented (MCU), not UNIX-host.
  - Repeats the key caveat verbatim: AtomVM implements a **constrained subset of the Erlang
    stdlib**; unsupported calls surface as `undef` errors in stack traces at runtime.
- **Informs**: FR-009/FR-010, FR-014/FR-015, Gleam-feasibility edge case.
- **Confidence**: high

### [10] WASM/Emscripten browser + NodeJS port (getting-started + build docs)
- **URL**: https://doc.atomvm.org/main/getting-started-guide.html
- **Type / version / date**: Official documentation, 0.8.0-dev / 0.6.7, 2025–2026
- **Architectural concern**: failure-modes / packaging (browser sandbox)
- **Close-read findings**:
  - AtomVM-for-WebAssembly runs under **NodeJS and in browsers (Safari, Chrome/Chromium, Firefox)**.
  - The browser port **requires `SharedArrayBuffer`**, which browsers gate behind
    **cross-origin isolation HTTP headers** (COOP/COEP). Without header control (e.g. GitHub
    Pages) you need a JS **service-worker trick** to inject headers.
  - There is an AtomVM "toy webserver" (`wasm_webserver.avm`) to serve WASM examples — implies
    the browser sandbox networking is served *to*, not raw sockets *from*, the WASM VM.
  - Implication (not stated but structural): a browser WASM VM has **no raw UDP/TCP sockets** —
    only what the JS/Web platform exposes (fetch, WebSocket, WebRTC). This bears directly on the
    QUIC-substrate question for the WASM target.
- **Informs**: FR-009/FR-010 (UNIX host vs WASM target choice), FR-014/FR-015,
  Gleam-feasibility edge case (browser sandbox cannot host a raw QUIC/UDP stack).
- **Confidence**: med (the no-raw-socket conclusion for browser WASM is structural inference,
  high-confidence in general but not a single quoted AtomVM sentence)

### [11] Programmers Guide — PackBEAM / .avm packaging model
- **URL**: https://doc.atomvm.org/main/programmers-guide.html
- **Type / version / date**: Official documentation, 0.8.0-dev, 2026
- **Architectural concern**: packaging
- **Close-read findings**:
  - AtomVM uses a custom archive format **`.avm`** packaging BEAM files; the **PackBEAM** tool
    creates these archives. AtomVM uses the **first module exporting `start/0`** as the entry point.
  - Workflow is uniform across source languages: Erlang/Elixir/**Gleam** → standard compiler →
    `.beam` → PackBEAM → `.avm` → flash (MCU) or load (UNIX/WASM). Confirms Gleam is a
    first-class consumer at the bytecode/packaging level, not a special case.
  - Runtime APIs are "a carefully selected subset of Erlang/OTP" plus AtomVM-original
    IoT/MCU APIs.
- **Informs**: FR-009/FR-010 (uniform packaging for the 2nd stack), FR-014/FR-015.
- **Confidence**: high

### [12] Add AtomVM support to gleam CLI — discussion #4222
- **URL**: https://github.com/gleam-lang/gleam/discussions/4222
- **Type / version / date**: Gleam-lang GitHub discussion, 2025
- **Architectural concern**: packaging
- **Close-read findings**:
  - Community/upstream interest in first-class `gleam` CLI integration for AtomVM targets;
    today the path is via external tools (packgleam/orbital), not built into the Gleam CLI.
  - Reinforces that **Gleam→AtomVM is a real but still community-driven toolchain** (2025-era),
    not a polished single-command experience — a maturity signal for 036 feasibility.
  - The FOSDEM 2026 talk "AtomVM: Elixir, Erlang, and Gleam on Microcontrollers" confirms
    Gleam is an officially-acknowledged target language for AtomVM.
- **Informs**: FR-009/FR-010, FR-014/FR-015, Gleam-feasibility edge case (toolchain maturity).
- **Confidence**: med

---

## Cluster feasibility verdict

- **Raw networking AtomVM has today**: `gen_tcp`, `gen_udp`, and a strict subset of the OTP
  `socket` API. `socket:open/3` supports `domain=inet`, `type=stream|dgram`, `protocol=tcp|udp`.
  Crucially a **UDP datagram socket primitive exists** (`dgram`/`udp`), which is the necessary
  (not sufficient) substrate for QUIC. Limits: IPv6 unsupported, only a subset of gen_tcp/gen_udp
  functions implemented, and the networking docs are ESP32/Pico-centric.

- **TLS/ssl status**: only **"minimal" OTP `ssl` support** (since v0.6.0-alpha.2), backed by
  **Mbed-TLS** on generic_unix (OpenSSL removed). It is young and fragile: **active mode is not
  supported** (must force `{active,false}`), TLS-for-distribution is absent, and recent fixes
  (use-after-free, `recv` semantics) show low maturity. There is **no DTLS and no QUIC TLS 1.3
  integration** evident — a serious gap for any HTTP/3 ambition (deferred to cluster 02).

- **BEAM/OTP it omits**: OTP behaviours (`gen_server`/`gen_statem`/`supervisor`/`proc_lib`/`sys`)
  are partial subsets; the stdlib is "extremely limited"; integers cap at 256-bit; no non-byte
  bitstrings; no code reloading; distribution is WIP. Any Gleam HTTP/WS/QUIC library that pulls
  in unsupported stdlib functions fails at runtime (`undef`) — a real corpus risk.

- **Gleam path is real**: Gleam compiles to standard BEAM bytecode, packaged via PackBEAM into
  `.avm` (tools: `atomvm_packgleam`, `orbital`; needs Gleam 1.9.1+; entry point `start/0`).
  It is community-driven, not yet built into the `gleam` CLI — workable but rough.

- **Realistic platform target for the 036 Gleam stack = generic_unix (Linux/macOS host).** This
  is the only AtomVM target with a full POSIX socket layer (TCP + UDP dgram) AND Mbed-TLS ssl,
  making it the sole candidate able to host a UDP-based QUIC substrate. ESP32/Pico add WiFi but
  same socket/ssl limits and far less headroom. The **WASM/browser target cannot host a raw
  QUIC/UDP stack** at all — it is sandboxed to Web APIs (fetch/WebSocket/WebRTC), needs
  SharedArrayBuffer + COOP/COEP, and is best read as the *client/SPA* tier (cf. Popcorn), not a
  QUIC server.

- **Set-up for cluster 02**: AtomVM has the raw ingredients (UDP dgram sockets, an Mbed-TLS-backed
  but minimal ssl) but **no native QUIC, no DTLS, no HTTP/3, no TLS 1.3-over-QUIC binding** in
  evidence. Cluster 02 must answer: can a QUIC/HTTP-3 implementation run on AtomVM at all — via a
  pure-Gleam/Erlang QUIC library on top of `gen_udp`, via an Mbed-TLS-based native NIF (statically
  linked, since no dynamic linking), or is WebSocket-over-TLS the only realistically achievable
  channel-link on this stack today?

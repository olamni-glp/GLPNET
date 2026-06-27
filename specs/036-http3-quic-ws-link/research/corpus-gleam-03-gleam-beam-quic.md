# Corpus — Gleam/AtomVM stack — Cluster 03: Gleam language + BEAM/Erlang QUIC ecosystem

2026-06-27

Scope: what the Gleam language is, and what a Gleam program could reach for QUIC/HTTP3
and WebSockets **if it ran on the full BEAM** (Erlang/OTP VM) rather than on AtomVM.
The AtomVM constraint itself is covered in detail by a sibling cluster; here it is noted
only to mark the tension explicitly. Sources are the Gleam site, Hex/HexDocs package docs,
the emqx `quicer` NIF, Cowboy/erlang_quic HTTP/3 efforts, and AtomVM docs for the
boundary condition.

---

### [1] Gleam programming language — overview
- **URL**: https://gleam.run/  (and FAQ https://gleam.run/frequently-asked-questions/)
- **Type / version / date**: Language home page; Gleam is post-1.0 (stable since 2024); accessed 2026-06-27
- **Architectural concern**: packaging
- **Close-read findings**:
  - Gleam is a statically-typed, concurrent functional language that **compiles to Erlang source (→ `.beam` bytecode on the BEAM VM)** and, alternatively, to **JavaScript**. Two distinct backends; the BEAM backend is what matters for QUIC/OTP.
  - Runs on "the battle-tested Erlang virtual machine that powers planet-scale systems"; explicitly markets interop: "easy to use code written in other BEAM languages such as Erlang and Elixir, so there's a rich ecosystem of thousands of open source libraries." This is the hook by which Gleam reaches `quicer`/`cowboy`.
  - Ships its own build tool, formatter, package manager (`gleam new`, Hex-based). Packaging story is mature and conventional for a 036 prototype build.
  - Critical for 036: "BEAM" here means the **full** Erlang VM. Gleam-on-AtomVM is a separate, much narrower target — Gleam still compiles to Erlang/`.beam`, but AtomVM only runs a subset (see [10],[11]).
- **Informs**: FR-009/FR-010 (Gleam stack identity), FR-014/FR-015 (packaging/build)
- **Confidence**: high

### [2] Gleam externals / FFI guide (`@external`)
- **URL**: https://gleam.run/documentation/externals/
- **Type / version / date**: Official language docs; accessed 2026-06-27
- **Architectural concern**: packaging (interop boundary)
- **Close-read findings**:
  - Gleam calls Erlang/Elixir/JS via the `@external(erlang, "module", "function")` attribute — this is the ONLY mechanism by which Gleam would invoke a BEAM QUIC library (`quicer:listen/...`) since none of these libs are written in Gleam.
  - **Type annotations are mandatory and unchecked** at the boundary: the compiler cannot verify the external Erlang matches declared types; runtime errors are possible. A Gleam→quicer binding is hand-written, unverified glue.
  - Target-specific: Erlang externals do not compile to the JS backend. A QUIC-bound Gleam program is BEAM-only by construction.
  - Guidance: "Externals should be used sparingly." A real QUIC stack would be mostly external glue, which is the unusual case, not the norm.
- **Informs**: FR-009/FR-010 (how Gleam reaches QUIC), FR-014/FR-015
- **Confidence**: high

### [3] mist — Gleam web server (HTTP + WebSocket)
- **URL**: https://hexdocs.pm/mist/ (current v6.0.3; also v5.0.4 widely deployed)
- **Type / version / date**: HexDocs package; v6.0.3 / 2026; accessed 2026-06-27
- **Architectural concern**: WS-framing
- **Close-read findings**:
  - mist is a **pure-Gleam web server** ("a glistening Gleam web server") built atop `glisten` (TCP/TLS, [4]) and using `gramps` ([6]) for HTTP/WS frame helpers. Real download volume (100k+ total) — it is the de-facto Gleam HTTP server.
  - **WebSocket support is first-class**: text + binary frames, connect/disconnect/close lifecycle handlers, custom message routing, endpoint-selective upgrade. This directly satisfies the WS half of 036 on full BEAM.
  - Also supports chunked responses, streaming request bodies, body size limits, file serving.
  - **HTTP version**: mist serves HTTP/1.1 (and the Gleam ecosystem's HTTP/2 story is limited); **no HTTP/3** — mist is built on `glisten` (TCP), not on a QUIC/UDP transport. So mist gives WebSockets-over-TCP, NOT WebSockets-over-HTTP/3.
- **Informs**: FR-002 (WebSocket), FR-009/FR-010 (Gleam server stack)
- **Confidence**: high

### [4] glisten — pure-Gleam TCP/TLS server
- **URL**: https://hexdocs.pm/glisten/ (v9.0.0; also v8.0.1) — repo https://github.com/rawhat/glisten
- **Type / version / date**: HexDocs/GitHub; v9.0.0 / 2026; accessed 2026-06-27
- **Architectural concern**: concurrency
- **Close-read findings**:
  - "A pure Gleam TCP library" with **TLS** support; the transport substrate beneath mist.
  - Uses `gleam_otp` ([7]): a **supervisor manages a pool of acceptors**; each acceptor blocks on `accept`, then spawns a per-connection handler process. Classic BEAM acceptor-pool concurrency, fully type-safe.
  - It is **TCP/TLS only** — it does NOT speak UDP/QUIC. Confirms the Gleam-native server stack tops out at TLS-over-TCP; HTTP/3 cannot ride glisten.
  - This is the relevant model for the prototype's WS link: per-connection actor, supervised, backpressure via BEAM process mailboxes.
- **Informs**: FR-002 (WS transport), FR-009/FR-010, concurrency/backpressure
- **Confidence**: high

### [5] stratus — Gleam WebSocket client
- **URL**: https://hex.pm/packages/stratus — repo https://github.com/rawhat/stratus
- **Type / version / date**: Hex package; active 2026; accessed 2026-06-27
- **Architectural concern**: WS-framing
- **Close-read findings**:
  - The Gleam **WebSocket client** counterpart to mist's server; connects to WS servers, sends/receives, manages connection lifecycle. Built on `gramps` framing helpers.
  - Gives 036 a symmetric Gleam-native client+server WS story (mist server + stratus client) — useful if the link end-to-end is Gleam↔Gleam or Gleam↔other.
  - Same transport ceiling: WS over TCP/TLS, no QUIC/HTTP3.
- **Informs**: FR-002 (WebSocket client side), FR-009/FR-010
- **Confidence**: high

### [6] gramps — Gleam HTTP/WebSocket framing helpers
- **URL**: https://hexdocs.pm/gramps/ (v6.0.0) — repo https://github.com/rawhat/gramps
- **Type / version / date**: HexDocs/GitHub; v6.0.0; last update 2026-04-18; accessed 2026-06-27
- **Architectural concern**: WS-framing
- **Close-read findings**:
  - Shared library of **WebSocket frame data types + functions** (and HTTP helpers); the common framing core used by BOTH mist (server) and stratus (client). Single source of WS-framing truth in Gleam.
  - Means RFC 6455 frame parsing/serialization in the Gleam stack is one audited, reused module — relevant to WS-framing correctness in 036.
- **Informs**: FR-002 (WS-framing), FR-009/FR-010
- **Confidence**: high

### [7] gleam_otp + gleam_erlang — actors / OTP for Gleam
- **URL**: https://hexdocs.pm/gleam_otp/ (v1.2.0) — repo https://github.com/gleam-lang/otp
- **Type / version / date**: HexDocs/GitHub; gleam_otp v1.2.0; accessed 2026-06-27
- **Architectural concern**: concurrency
- **Close-read findings**:
  - Type-safe actor model over Erlang OTP: `actor` (gen_server-equivalent, handles OTP system/debug/trace messages), supervisors and supervision trees for fault tolerance/self-healing. Processes themselves are in the `gleam_erlang` library.
  - Goals: full type safety of actors/messages, OTP compatibility, supervisor fault tolerance, "equivalent performance to Erlang's OTP."
  - **Limitation noted**: not all OTP is exposed (some can't be typed safely; some supervision strategies still in development). On full BEAM this is the concurrency/backpressure substrate for the QUIC/WS handlers.
  - Important for the AtomVM tension: this assumes full-BEAM OTP; AtomVM only partially implements gen_server/supervisor/etc. ([11]).
- **Informs**: FR-009/FR-010 (concurrency model), backpressure/failure-modes
- **Confidence**: high

### [8] emqx `quicer` — Erlang QUIC NIF wrapping MsQuic
- **URL**: https://github.com/emqx/quic (README), https://hexdocs.pm/quicer/readme.html
- **Type / version / date**: GitHub/HexDocs; v0.2.x ("Preview"); accessed 2026-06-27
- **Architectural concern**: handshake / ALPN / cert-trust / stream-multiplexing
- **Close-read findings**:
  - `quicer` is "QUIC erlang library" providing a **NIF binding to Microsoft's MsQuic** (C library; uses OpenSSL for TLS). This is THE production-grade QUIC path on the BEAM — it is what EMQX uses to serve MQTT-over-QUIC.
  - **ALPN**: supported for protocol selection during connection establishment. **Certs/TLS**: `certfile`/`keyfile` options; peer verification via `verify`; `sslkeylogfile` for Wireshark decryption. **Stream multiplexing**: bidirectional + unidirectional streams, tunable via `peer_bidi_stream_count`/`peer_unidi_stream_count`. **Handshake** + listen/connect/accept/send API (`quicer:listen`, `quicer:connect`, `quicer:send`).
  - Event-driven: MsQuic events are translated into Erlang messages — fits BEAM actor handling and a Gleam `@external` binding.
  - **Status: Preview, not production-ready**; Linux + macOS supported, **Windows support is explicitly incomplete/"help needed."** Relevant since glpnet is a Windows repo (036 dev host is Windows 11).
  - CRITICAL for 036: it is a **C NIF** — it requires dynamic loading of compiled native code into the VM. This is exactly what AtomVM cannot do ([10]).
- **Informs**: FR-009/FR-010 (genuine QUIC path), FR-014/FR-015 (cert distribution, ALPN), handshake
- **Confidence**: high

### [9] Cowboy 2.14 — experimental HTTP/3 over quicer
- **URL**: https://github.com/ninenines/cowboy/issues/1544 ; https://ninenines.eu/articles/ ; https://dev.to/niamtokik/http3-and-quic-in-erlang-with-cowboy-549o
- **Type / version / date**: GitHub issue + Nine Nines articles + dev.to writeup; Cowboy ≥2.14.0 (experimental); accessed 2026-06-27
- **Architectural concern**: handshake / failure-modes
- **Close-read findings**:
  - Cowboy (the standard Erlang HTTP server) gained **experimental HTTP/3 (RFC 9114) since 2.14.0**, but it **does NOT implement QUIC itself — it depends on `quicer`** ([8]) and is gated behind the `COWBOY_QUICER=1` compile macro; `quicer` is deliberately NOT a default dependency.
  - Practitioner verdict on the quicer-backed path: "a BIG NO-NO for a deployment in production" — the dependency chain "is insane, it fetch[es] many python-like modules and recompile[s] OpenSSL from scratch." Build/packaging cost is high.
  - Means: on full BEAM, HTTP/3-as-a-server is reachable (Cowboy+quicer) but experimental and heavy. A Gleam program would typically front Cowboy/quicer via Elixir/Erlang interop or `@external`.
- **Informs**: FR-009/FR-010 (HTTP/3 server feasibility), FR-014/FR-015 (build/packaging burden), failure-modes
- **Confidence**: high

### [10] erlang_quic (benoitc) — pure-Erlang QUIC + HTTP/3
- **URL**: https://github.com/benoitc/erlang_quic ; https://hexdocs.pm/quic/readme.html ; .../docs/HTTP3.md
- **Type / version / date**: GitHub/HexDocs; `quic` v1.2.0; accessed 2026-06-27
- **Architectural concern**: stream-multiplexing / ALPN
- **Close-read findings**:
  - A **pure-Erlang** QUIC implementation — "no NIFs or native code dependencies." Covers **RFC 9000/9001 (QUIC/TLS), RFC 9114 (HTTP/3), RFC 9204 (QPACK)**, plus RFC 9297/9298 (HTTP Datagrams, CONNECT-UDP). Full client AND server, incl. server push.
  - QPACK encoder/decoder on dedicated unidirectional streams; full request/response/push stream multiplexing; Extended CONNECT (`:protocol` pseudo-header) toward WebTransport/CONNECT-UDP. ALPN-aware.
  - Maturity signals: EUnit/PropEr/Common Test suites + benchmarks; appears actively maintained. Being pure Erlang, it is far lighter to build than quicer (no MsQuic/OpenSSL recompile).
  - 036 significance: this is the ONLY QUIC route that is conceptually AtomVM-compatible in principle (pure BEAM bytecode, no NIF) — BUT it relies on substantial OTP/crypto + UDP socket support that AtomVM may not provide, and it still targets full BEAM. Worth flagging as the "no-NIF QUIC" option.
- **Informs**: FR-009/FR-010 (HTTP/3 without a NIF), FR-014/FR-015 (lighter build), stream-multiplexing
- **Confidence**: med (maturity/perf vs quicer unverified beyond docs)

### [11] AtomVM — differences with BEAM (the boundary condition)
- **URL**: https://doc.atomvm.org/main/differences-with-beam.html
- **Type / version / date**: Official AtomVM docs; v0.7/0.8-dev; accessed 2026-06-27
- **Architectural concern**: packaging / failure-modes
- **Close-read findings**:
  - **AtomVM cannot dynamically load compiled C NIFs at runtime**: "NIFs and Ports need to be linked with the VM as most embedded environments do not support dynamic linking." Custom NIFs must be **statically compiled into the AtomVM binary**. It also does **not implement the `onload` opcode / `-onload` attribute** that BEAM NIF loading relies on.
  - Therefore **`quicer` ([8]) — a runtime-loaded MsQuic NIF — cannot run on AtomVM.** The production QUIC path is structurally unavailable on AtomVM.
  - "Support for OTP applications is currently very limited" — only subsets of `gen_server`, `gen_statem`, `supervisor`, `proc_lib`, `sys`. The `gleam_otp` ([7]) and glisten/mist supervisor models would be partially unsupported.
  - NIFs/Ports must "return quickly" (no dirty-scheduler analog) — long native QUIC handshakes ill-suited even if statically linked.
- **Informs**: FR-009/FR-010 (AtomVM stack limits), FR-014/FR-015 (static-link packaging), failure-modes
- **Confidence**: high

### [12] AtomVM — Programmers Guide (capabilities/networking)
- **URL**: https://doc.atomvm.org/main/programmers-guide.html
- **Type / version / date**: Official AtomVM docs; v0.8.0-dev; accessed 2026-06-27
- **Architectural concern**: concurrency / packaging
- **Close-read findings**:
  - AtomVM "implements a strict subset of the BEAM instruction set" + "a small subset of the Erlang/OTP standard libraries," optimized for micro-controllers (ESP32 etc.).
  - Networking is geared to embedded: **WiFi** on supported chips, plus peripheral protocols (GPIO/I2C/SPI/UART). It is NOT a general TCP/TLS+UDP server platform like full BEAM; the rich socket/TLS stack glisten/mist/quicer assume is not the AtomVM target environment.
  - Reinforces that "Gleam on AtomVM" ≠ "Gleam on BEAM": same compiler output, radically different runtime surface. The QUIC/WS libraries in this cluster are full-BEAM assets, not AtomVM assets.
- **Informs**: FR-009/FR-010, FR-014/FR-015, concurrency
- **Confidence**: high

---

## Cluster feasibility verdict

- **On FULL BEAM, genuine QUIC is reachable today.** `quicer` ([8]) wraps Microsoft MsQuic via a C NIF and is production-used by EMQX; it gives real handshake, ALPN, TLS cert files, and bidi/unidi stream multiplexing. A Gleam program reaches it through `@external` Erlang bindings ([2]) — unverified glue, but viable. There is also a **NIF-free** alternative, `erlang_quic` ([10]), pure-Erlang HTTP/3+QPACK (RFC 9114/9204).

- **On FULL BEAM, WebSockets are a solved, native problem.** mist (server, [3]) + stratus (client, [5]) over glisten TCP/TLS ([4]), sharing gramps framing ([6]), give a clean, type-safe, supervised (gleam_otp, [7]) RFC 6455 stack — entirely pure Gleam, no NIF. This is the strongest, lowest-risk part of any Gleam 036 stack.

- **Hard tension with the spec's AtomVM mandate.** AtomVM ≠ full BEAM. AtomVM **cannot load `quicer`** at all: it forbids runtime-loaded C NIFs (NIFs/Ports must be statically linked into the VM; no `onload` opcode) ([11]). So the one production QUIC path is structurally impossible on AtomVM. It also implements only OTP subsets and an embedded networking surface (WiFi/peripherals, not a general TCP/TLS+UDP server) ([11],[12]) — glisten/mist/gleam_otp would be only partially supported.

- **A no-NIF QUIC (erlang_quic) is the only conceptual AtomVM-direction option**, since it is pure BEAM bytecode — but it depends on substantial OTP, crypto, and UDP-socket support that AtomVM does not currently provide, and is documented/maturity-validated only against full BEAM. Treat as research-grade, not a near-term AtomVM target.

- **Windows caveat for 036's dev host**: `quicer` lists Windows support as incomplete ("help needed"), and the Cowboy+quicer HTTP/3 build is explicitly judged unfit for production (recompiles OpenSSL, heavy Python-like deps) ([9]). Even the full-BEAM QUIC path is build-heavy on this repo's platform.

- **Realistic Gleam stack shape for 036**: (a) the *demonstrable* slice is Gleam-native **WebSockets over TLS/TCP** (mist/stratus/glisten/gramps + gleam_otp) on **full BEAM** — genuine, type-safe, low-risk; (b) genuine **QUIC/HTTP3** is only honest on **full BEAM via quicer (NIF) or erlang_quic (pure)**, NOT on AtomVM. If the spec insists on AtomVM, the Gleam transport cannot offer real QUIC — the AtomVM node would have to terminate QUIC elsewhere (a full-BEAM or C# peer) and speak a lighter link, OR 036 must relax AtomVM to full BEAM for the Gleam stack. This conflict should be surfaced to the spec before implementation.

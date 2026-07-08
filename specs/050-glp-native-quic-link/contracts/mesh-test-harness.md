# Contract: Cross-Host GLP Mesh Test (FR-012–FR-018)

## The GLP program is the harness
Every link in the test is opened by a **GLP goal** in the REPL — never by an external Python/C# harness (FR-003, FR-012, SC-004). The program lives under `programs/tests/quic/` (SRSW-clean, `procedure`-declared), loaded by `out/csharp/glp_repl` on each host. It reuses the unchanged wrappers `server_listener` / `client_connector` / `link_send` / `link_close` / `link_monitor` from `programs/self.glp`.

## Topology
- 5 C# endpoints ⇒ **C(5,2) = 10 full-duplex links** (10 QUIC bidi streams; one `ch(In, Out?)` per peer-pair, no doubling).
- Endpoints: 2 delivered glpnet C# REPL instances + 3 pre-built MAUI C# apps (external participants — FR-013a interop-ready, not built here).
- Hosts: Olamnit **192.168.0.136** + gavri **192.168.0.108**.
- Server role uses `QuicTransport.CreateListenerAsync` → `QuicListenerHandle.AcceptAsync` (many isolated links per UDP port).

## Test dimensions (all as GLP goals)
- **Mesh (FR-013, SC-004)**: every peer-pair link established by a GLP goal; peer-to-peer duplex delivery holds at the concurrency floor; delivered endpoints accept the 3 pre-built apps.
- **Performance (FR-014, SC-005)**: median round-trip < 50 ms LAN; ≥ 1000 messages sustained, zero loss. *(Provisional — research D-3.)*
- **Security/cyber (FR-015, SC-008)**: capability refusal (absent/tampered/expired macaroon); signed-content tamper detection (whole + sub-content, via `sig/Seals.cs`); cert-pin enforcement (rogue/non-pinned peer rejected). Each a recorded outcome, zero false accepts.
- **Reliability (FR-016, SC-006)**: duplicate suppression (`msg_id` + per-link `seq`); exactly-once remote reader reactivation; fault reporting via the 025 monitor stream (never swallowed).
- **Graceful termination (FR-017/FR-018, SC-007)**: drain in-flight → `link_close` on every link → orderly teardown of listeners/connectors/streams/QUIC connections → zero crashes → immediate re-run needs no manual cleanup.

## Interop-readiness surface (what the pre-built MAUI apps rely on)
Shared cert + SPKI pin from `glpquick-cert/`; static macaroon root out-of-band; crdtmsg envelope wire format; ALPN `h3`; one WS per QUIC bidi stream. An app that cannot terminate QUIC in-process reaches the mesh via the 036 Profile-A WS-to-QUIC side-process — FR-002 genuine-QUIC still holds on every link.

## Acceptance
- Two-host manual run (`quickstart.md`) — the headline SC-004/SC-005/SC-008 cross-host demonstration.
- Single-host multi-NIC / loopback-QUIC xUnit + REPL regressions gate the CI-checkable properties (registration, crdtmsg-on-wire, macaroon gate, graceful close, multi-accept).
- REPL regression added to `test/run_all_tests.sh` for the GLP-level link program (load + one-bind-crosses assertion where a hermetic quic endpoint is available).

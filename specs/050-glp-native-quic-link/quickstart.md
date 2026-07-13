# Quickstart — GLP-Native True-QUIC Link (two-host acceptance run)

Demonstrates: a GLP program in the C# REPL stands up genuine QUIC links across two physical hosts, speaks crdtmsg under macaroon control, runs the full test, and shuts down cleanly.

## Prerequisites
- Two Windows-11 hosts on the same LAN: **Olamnit 192.168.0.136** + **gavri 192.168.0.108**.
- .NET 10 runtime with MsQuic (`QuicTransport.IsSupported == true` on both).
- The built C# REPL (`out/csharp/glp_repl`) with the 050 quic-transport registration.
- The **same** shared trust material on both hosts: copy `glpquick-cert/{glpquick.pfx,glpquick.fingerprint}` + the out-of-band static-macaroon root to each.

## 0. Verify QUIC is available (both hosts)
Run the REPL; confirm the quic transport registers without a `PlatformNotSupportedException`. If QUIC is unavailable the link fails loud — it never downgrades to TCP/loopback (FR-002).

## 1. One genuine link + one bind crosses the wire (US1 / SC-001)
- On Olamnit (listener), a GLP goal:
  `server_listener(link_id("quic", ep("192.168.0.136", 4599), 1), Link, Faults).`
- On gavri (connector):
  `client_connector(link_id("quic", ep("192.168.0.136", 4599), 1), Link, Faults).`
- Producer binds one writer on Olamnit; the consumer reader on gavri suspends until the value crosses the real QUIC wire, then reactivates **exactly once**.
- Assert on the wire (packet capture or endpoint attestation): a genuine QUIC+WS handshake, not loopback/TCP.

## 2. crdtmsg on the wire (US2 / SC-002)
- Send a crdtmsg message (incl. one carrying a rich-text edit op) over the link.
- Assert: on-wire L5 payload is a well-formed crdtmsg envelope; peer decodes losslessly incl. unknown-ignorable sections; malformed inputs are rejected loud-fail.

## 3. Macaroon gating (US3 / SC-003)
- Establish with a valid macaroon → succeeds.
- Retry with absent/tampered/expired → fails closed, refusal recorded, no crash.

## 4. Full mesh + perf + security + reliability (US4 / SC-004..SC-006, SC-008)
- Launch the role-parameterized GLP mesh program on both hosts; bring the 3 pre-built MAUI apps online.
- Assert: all 10 full-duplex links opened by GLP goals; perf targets met (provisional: median RT < 50 ms, ≥ 1000 msgs, zero loss); rogue peer + tampered signed block rejected (zero false accepts); duplicates suppressed, exactly-once reactivation, faults reported.

## 5. Graceful termination (US5 / SC-007)
- Request stop: in-flight messages drain, every link `link_close`s, listeners/connectors/streams/QUIC connections tear down in order, process exits with no error.
- Re-run step 1 immediately — links re-establish with no manual cleanup.

## CI-checkable subset (single host)
`csharp/glp_link.tests/` (xUnit, real quic loopback handshake) + `bash test/run_all_tests.sh` (REPL suite; baseline 524/525) cover registration, crdtmsg-on-wire, macaroon gate, pin-mismatch reject, graceful close, and multi-accept — everything except the genuine two-physical-host cross-LAN demonstration, which is the manual acceptance run above.

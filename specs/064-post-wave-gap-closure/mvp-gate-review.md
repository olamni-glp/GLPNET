# MVP Gate Review (T020) — US1 (as re-scoped) + US2 — 2026-08-03

**Gate definition** (clarify Q4 + Option-B ruling): US1 = Gleam link real-gap closure (multi-accept, QUIC-WS bridge access, cross-runtime coverage); US2 = C# multi-client serve path. Both must be delivered with zero regression.

## US1 evidence

| Item | Evidence |
|---|---|
| Gleam multi-accept (T010/T011) | multi_accept.gleam (344L, per-accept nonce, exit_on_close inherited, broker/pump design) + 3-test suite incl. half-close-at-establishment; commit f88ff5e1 |
| QUIC-WS bridge (T012) | BridgeAcceptor on glp_quick_host reusing AcceptLoopAsync + gleam bridge_client dial helper; relay both directions byte-identical, over-capacity refusal, disconnect-survival; commit c5644c28 |
| Cross-runtime coverage (T013 as re-scoped) | gleeunit suites + T029 two-direction FE/BE smoke (t029-cross-febe-smoke.md); full harness extension deferred to the OTP-25 environment (host 12/18 deviation, baseline.md) |
| Transferred scope | dist-unify/quiescence → distributed-unification-quiescence-protocol feature (roadmap, captured); contracts bannered; audit trail in tasks.md T003–T009/T013 |

## US2 evidence

| Item | Evidence |
|---|---|
| Continuous multi-accept transport (T015) | TcpTransport.AcceptLoopAsync, additive, single-accept path preserved; commit 521fafcf |
| ClientSession/RoutedReply (T016) | lifecycle + loud mis-route + non-wedging discard; 6 tests |
| Multi-client EngineServer (T017/T018) | opt-in EngineServeMode.MultiClient, per-session forwarders → merged channel → serial dispatcher, reply isolation, disconnect-survival; 061 single-client mode byte-preserved; 4 tests; commit b3c6d48d |
| Recorded partial | A31 GLP-level merge wiring needs live-stream injection = §1.14-gated new surface; C#-side merge tree delivers the serve semantics (EngineServer.cs header + tasks note) |

## Suite state at gate (serial runs, zero regression vs T001)

link 171 · il_codec 64 · engine_host 73 · wire_registry 6 · split_protocol 46 · gleam 618 · REPL 381 · corpus 206/206.

## Verdict

**GATE PASS.** US1 (re-scoped) and US2 delivered with evidence; two recorded partials (A31 GLP-merge, cross-runtime harness extension) are explicit, §1.14- or environment-gated, and carried to DEFERRALS (T038). Proceed to polish (T037–T041).

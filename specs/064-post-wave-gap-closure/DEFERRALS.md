# 064 Deferrals register (T038) — 2026-08-03

Explicit, durable deferrals from this feature. Each names its gate; none is a silent drop.

| # | Deferral | Gate | Where recorded |
|---|---|---|---|
| D064-1 | Native BEAM QUIC-WS transport leaf (quicer NIF, 036 Profile-C lineage) — capability meanwhile served by the T012 C# bridge | engineer decision + toolchain risk | clarify Q2 ruling; 059 close-out-064.md (T084/T085/T086/T098) |
| D064-2 | Distributed unification + quiescence protocol — TRANSFERRED to `distributed-unification-quiescence-protocol-two-runtime-spec-first` (roadmap, captured) | spec-first pipeline on the new feature; ack-path substrate prerequisite | Option-B ruling in spec.md Clarifications; bannered contracts; parity-checklist.md |
| D064-3 | A31 GLP-level merge wiring to real client channels (live-stream injection + incremental readout = new engine/GLP surface) | §1.14 proposal + engineer approval | EngineServer.cs header; T017 report; us3-review-note precedent |
| D064-4 | Project-level compiler split of glp_runtime_net (assembly-level absence assertion for FR-006) — type-level boundary enforced meanwhile (CompilerAbsenceTests) | own future feature | us3-review-note.md deviation 1 |
| D064-5 | IL-session loaded state under the 061 snapshot/quiescence machinery | contract silent; needs a snapshot-semantics ruling | us3-review-note.md deviation 3 |
| D064-6 | Cross-runtime harness extension (multi-link + bridge scenarios in test/parity/cross_runtime, ×10 loops) + full Section I 18/18 re-verification on this host | OTP-25 environment (Windows OTP 25 install — engineer/admin) | baseline.md host deviation; T013 re-scope note |
| D064-7 | RESULT-binding rendering convention unification (C# pre-rendered strings vs Gleam structured terms — both legal 038 envelopes; each side's renderer mis-displays the other) | small follow-up fix, either convention; decide with D064-2's wire work | t029-cross-febe-smoke.md |
| D064-8 | Gleam BE: SNAPSHOT support + mid-serve second-client loud refusal parity + :trace/:limit wire kinds | follow-up on the FE/BE line | T026-T028 report deviations |
| D064-10 | SC-003 corpus-equivalence gate is narrower than "the full regression corpus": 12 representative typed programs, all nullary-driven (bindings empty on both sides by construction), and the inline one-shot RUN_GOAL_IL envelope is never compared against the text path | widen the cases (bindings-bearing goals + inline one-shot comparison) in a follow-up; needs the IL-path binding shape settled first | T040 cycle-2 review (`equivalence-claim-overscoped`); scope note in csharp/glp_split_protocol.tests/CorpusEquivalenceTests.cs header |
| D064-9 | 059 residual tail (23 tasks incl. T091 full FE/BE acceptance, T079 multiagent plays, T061/T063/T069/T071 engine residuals) + 5 ambiguity flags awaiting engineer rulings | per-item; see the flag list | specs/059-full-scope-gleam-glp-implementation/close-out-064.md |

Fleet norms carried (not deferrals): D-9 + `{exit_on_close, false}` on every BEAM socket path; serial C#/Gleam suite runs on this host (WSL localhost port sharing).

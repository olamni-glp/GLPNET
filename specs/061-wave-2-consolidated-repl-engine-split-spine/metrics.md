<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 7c1d2e9a-4b6f-4a3d-9e21-061e5c0a8f36
-->

# Metric Tables — 061 Wave 2 (T036, FR-041 / R8)

One table per consolidated seed, per the shared R8 template
(`docs/research/repl-engine-separation/reconciliation/METRIC-COMBINATION-TEMPLATE.md`:
`name | kind | tool | threshold`), with the R14 protocol-verification row
mandatory in each. Thresholds are recorded against the wave's ACHIEVED,
executed evidence — every tool cell names a runnable harness.

Machine-check scan results (T037, Constitution III/V) are recorded at the end.

## Seed 1 — `repl-engine-process-split-mvp` (US1)

| name | kind | tool | threshold |
|---|---|---|---|
| Split-vs-single-process result parity (SC-001) | pragmatic | `ParityCorpusTests` (`dotnet test csharp/glp_engine_host.tests --filter ParityCorpus`): C#-runtime-compatible Section-A corpus through the split client, diffed vs the single-process REPL | identical rendered results on 100% of the corpus — **achieved** |
| Request/response frame round-trip + loud-fail | pragmatic | `RequestResponseCodecTests` (kind bytes, request_id echo, unknown-kind + trailing-bytes loud-fail) | 100% of parametrized cases — **achieved** |
| Engine survives client lifecycle (US1 AS-3/AS-4) | pragmatic | `EngineServerTests` (single-accept, second-client loud refusal, compile-error keeps serving, exit + reconnect keeps loaded program) | all scenarios pass — **achieved (62/62 suite)** |
| Front↔back protocol validation (**R14, mandatory**) | pragmatic | real SPIN 6.5.1 on the full wire protocol, `docs/research/repl-engine-separation/models/spin/run.sh` (all six request kinds + restore window + deferral + shutdown) | deadlock-freedom + no unspecified receptions + `request_eventually_answered` + `deferred_snapshot_eventually_completes`, errors: 0, full statespace — **achieved (PASS, RESULT.md)** |
| Result-envelope byte contract (038 reuse) | formal | shipped 038 `glp_result_codec` golden suite (`dotnet test csharp/glp_result_codec.tests` where present; envelope reused unchanged per FR-001) | byte-level `decode(encode(x)) = x` on every field variant — **inherited from shipped 038, unchanged** |
| SRSW preservation | formal | in-repo SRSW validator (REPL suite §D) before/after the wave | 0 new SRSW violations; REPL suite green — **achieved (532/532)** |

## Seed 2 — `engine-state-snapshot-and-persistence-api` (US2)

| name | kind | tool | threshold |
|---|---|---|---|
| Snapshot blob byte-parity | formal | `SnapshotTests.Blob_EncodeDecode_RoundTripsByteIdentically` + `Restore_ThenRecapture_ReproducesEverySectionByteForByte` | `decode(encode(state)) == state` AND restore→recapture reproduces every section byte-for-byte — **achieved** |
| Restore equivalence probes (SC-004) | pragmatic | `RestoreEquivalenceTests`: second engine `--from-snapshot`, state-revealing probe set against both | byte-identical RESULT envelope bodies on 100% of probes — **achieved** |
| Quiescence gating + deferral (FR-014) | pragmatic | `SnapshotTests` non-quiescent deferral + coalescing facts | busy ⇒ DEFERRED, parked fires at next quiescence, seq monotonic, never an inconsistent snapshot — **achieved** |
| Torn-write safety (FR-013) | pragmatic | `SnapshotStoreTests`: kill during `Write` at every step | a crash at ANY point leaves `Latest()` at the previous seq; torn write never listed — **achieved** |
| Timer re-arm with remaining duration (FR-015) | pragmatic | `RestoreEquivalenceTests.ArmedTimer_CapturedAsRemainingDuration_RearmsAndFiresAfterRestore` | captured remaining ∈ (0, armed]; fires post-restore; suspended goal reactivates — **achieved** |
| Protocol validation of the snapshot window (**R14, mandatory**) | pragmatic | SPIN full-protocol model (wire rules 4/5: ENGINE_BUSY restore window, SNAPSHOT deferral) — `models/spin/run.sh` | errors: 0 incl. `deferred_snapshot_eventually_completes` — **achieved (PASS)** |
| SRSW preservation | formal | REPL suite §D before/after | 0 new violations — **achieved (532/532)** |

## Seed 3 — `liveness-crash-restart-host` (US3, host/infra — R9)

| name | kind | tool | threshold |
|---|---|---|---|
| Kill → detect → restart within budget | pragmatic | `SupervisorTests.KillEngine_SupervisorDetects_RestartsFromLatestSnapshot` (real engine binary) | detection within ping budget; replacement restored from latest seq + healthy; crash record complete (FR-024) — **achieved** |
| Backoff progression + taxonomy stop (FR-023/DEF-F2) | pragmatic | `SupervisorTests.InstantlyDyingChild_BackoffProgresses_ThenTaxonomyStopsTheLoop` | geometric backoff observed (50→100 ms); `repeated_immediate_crash` stops the loop loudly — **achieved** |
| Corrupt-latest previous-seq fallback (once) | pragmatic | `SupervisorTests.CorruptLatestSnapshot_FallsBackToPreviousSeq_Once` | serves from previous seq; no restart loop — **achieved** |
| Timed liveness/supervision bound (**R14 row; SC-003**) | pragmatic | UPPAAL `verifyta` on `models/uppaal/supervision.xml` (ping interval/timeout/backoff automata; `run.ps1`) | detect→restart within one ping interval + restore time — **model authored; verdict NOT RUN: verifyta 5.x key-gated (license), recorded honestly in `models/uppaal/RESULT.md`; T030 stays open pending the academic key** |
| Shapiro criteria (R9 host/infra rule) | — | five-criterion N/A block below | every criterion addressed — **recorded** |

R9 five-criterion justification (host/infra seed — the supervisor lives above the
engine library and touches no GLP semantics):

| criterion | applies? | justification |
|---|---|---|
| Committed-choice concurrency | **N/A** | The supervisor is a separate process holding the wire client slot; it pings and restarts. No reduction path is introduced or altered. |
| SRSW | **N/A** | No GLP variable, heap cell, or clause is read or written; supervision state is CrashLog JSONL + process handles. (REPL §D still green as a regression guard.) |
| Suspension correctness | **N/A** | Suspension state crosses only inside the opaque snapshot blob (seed 2's obligation); the supervisor never decodes it. |
| Monotone variable binding | **N/A** | The supervisor moves no bindings; restart delegates restore to the engine's own path. |
| Three-valued unification | **N/A** | PING/ACK and exit codes are the only observed outcomes; no unification verdict is produced or projected. |

## Seed 4 — `restore-and-resume-with-link-reestablish` (US4)

| name | kind | tool | threshold |
|---|---|---|---|
| Kill-and-restart correctness (FR-033/SC-002) | pragmatic | `KillAndRestartTests` (`dotnet test csharp/glp_engine_host.tests --filter KillAndRestart`): real binary, tcp peer, snapshot mid-stream, kill, supervised restart, resume | peer-observable committed stream ≡ uninterrupted run (`[1,2,3,99,4,5,6]`, incl. the post-snapshot transport-committed 99 exactly once); deterministic — **achieved (4/4 consecutive runs)** |
| Re-wire adoption of pre-bound cells (DEF-E1) | pragmatic | `RewireTests`: adopt pre-bound cells, idempotent re-adoption, normal-path guards intact, egress resumes at the first unshipped tail | no re-ship of committed work; first post-restore bind ships; `WireEstablishedLink` guards unchanged — **achieved (6/6)** |
| Link-definition (0x09) round-trip incl. role | formal | `RewireTests.CaptureThenRestore_RoundTripsLinkDefinition_WithRole` + `SnapshotTests` byte-identity | LinkId + role + cursor positions round-trip exactly; capture loud-fails on a role-less handle — **achieved** |
| Crash/restore/resume consistency (**R14 row; FR-040**) | formal | real TLC 2.19 on `models/tla/CrashRestore.tla` (`models/tla/run.sh`): all crash points, complete statespace, + two negative controls | `NoDup` + `Ordered` + `NoCommittedLoss` + `EventuallyAllObserved` hold (0 errors); rearm-at-zero and async-ship negations each yield their counterexample — **achieved (PASS, RESULT.md)** |
| SRSW preservation | formal | REPL suite §D before/after | 0 new violations — **achieved (532/532)** |

## Machine-check scan (T037, Constitution III/V)

Commands (run from repo root against all 061 artifacts + the wave's new/changed code):

```
grep -rn "skipSRSW" specs/061-wave-2-consolidated-repl-engine-split-spine/ csharp/glp_split_protocol/ csharp/glp_engine_host/ csharp/glp_repl_client/ csharp/glp_supervisor/ csharp/glp_engine_host.tests/ csharp/glp_link/primitives/RewireHandle.cs docs/research/repl-engine-separation/models/
grep -rniE "OPENAI_API_KEY|litellm|openai" <same paths>
```

Executed 2026-07-30 (excluding `obj/`/`bin/` build outputs and this file):

| check | result |
|---|---|
| `skipSRSW` tokens | **0 violations** (sole occurrence: plan.md's Constitution-III check sentence naming the token being scanned for) |
| `OPENAI_API_KEY` / `litellm` / `openai` tokens | **0 violations** (sole occurrence: plan.md's Constitution-V check sentence naming the tokens) |

No LM anywhere on the runtime or verification path — SPIN/TLC/verifyta are
deterministic checkers (Constitution V).

## Suite diff vs the T005 baseline (T038, SC-005 / Constitution VII)

Executed 2026-07-30 after US4 landed (commit 9e79fc61):

| suite | T005 baseline | wave close | diff |
|---|---|---|---|
| REPL (`bash test/run_all_tests.sh`) | 532/532 | 532/532 | zero regression |
| glp_engine_host.tests | 23/23 (skeleton) | 62/62 | +39 new (US1–US4), 0 failures |
| glp_link.tests | 152/152 | 152/152 | zero regression (Role property + stamps additive) |
| glp_crdtmsg.tests | 184/184 | 184/184 | zero regression |
| glp_il_codec.tests | 45/45 | 45/45 | zero regression |
| glp_schema_lang.tests | 269/269 | 269/269 | zero regression |
| glp_wire_registry.tests | 6/6 | 6/6 | zero regression |

**SC-005: zero regression across every suite.**

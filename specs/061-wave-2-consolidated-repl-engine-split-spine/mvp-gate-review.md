# Anchor-A MVP-Gate Review — 061 Wave 2 (T040, FR-043)

**Trigger**: US1 (the ratified MVP cut, seed #6 `repl-engine-process-split-mvp`) shipped
at commit `7948a552` on branch `061-wave-2-consolidated-repl-engine-split-spine`, 2026-07-29.
**Protocol**: Deferral Register pickup protocol step 2 — re-read Anchor A + full register
rescan for anything newly unblocked (`docs/research/repl-engine-separation/reconciliation/DEFERRALS.md`).

## MVP evidence

- Two-process split live: `csharp/glp_engine_host` (TCP loopback, one client — FR-002) +
  `csharp/glp_repl_client` (thin, no language context — R7/FR-003), wire = FrameCodec TLV over
  the shared `csharp/glp_split_protocol` payload types (D1).
- Results ride the shipped 038 `ResultEnvelope` — ground-only subset, engine-pre-rendered
  bindings (R6), captured output blob (R3) — FR-004.
- Parity: `ParityCorpusTests` (4 corpus cases through BOTH the single-process REPL and the
  split path — identical binding/status lines, SC-001 subset). Engine survives client exit +
  reconnect (US1 AS-3); compile errors keep it serving (AS-4); suspension renders as the REPL
  does (AS-2).
- Suites: engine-host 32/32; all other C# suites green; REPL suite 532/532 at baseline
  (specs/…/baseline.md) — no regression.

## Anchor A row-by-row

| ID | Verdict at this gate |
|---|---|
| DEF-A1 (Dart-mirror byte-parity for the result codec) | **stays open** — explicitly out of this wave's scope (spec Assumptions: "Dart-mirror byte-parity … remains deferred (DEF-A1)"). No new unblock: the C#-only envelope path shipped unchanged; the Dart mirror + golden-file test remains a future feature. |
| DEF-A2 (multi-client / multi-accept, seeds #10→#13) | **stays open** — FR-002 pins one-engine/one-client for this wave; the second-accept loud-refusal path (wire rule 1, tested) is the MVP boundary marker, not the multi-accept loop. Wave-4 (olamnit, feature 062) carries the #13 GLP multi-client control program seed; #10 remains roadmap-owned. |
| DEF-A3 (full SPIN model of the complete wire protocol) | **DISCHARGED by this wave (T015)** — `docs/research/repl-engine-separation/models/spin/wire_protocol.pml` models all six request kinds + DEFERRED/ENGINE_BUSY + restore window + shutdown; real SPIN 6.5.1 verdicts: deadlock-freedom, no unspecified receptions, `request_eventually_answered`, `deferred_snapshot_eventually_completes` — all errors: 0, full statespace, 0 unreached states (RESULT.md). Register status update lands at T039. |

## Full-register rescan (newly unblocked / in-flight this wave)

- **DEF-D1, DEF-D2** (snapshot scope + U-P forks): consumed by this wave's spec — DEF-D1's
  field set is binding in FR-010; U-P1/2/5/6/7 were resolved in the 2026-07-29 clarify session
  (quiescence definition, remaining-time timer re-arm, at-most-once crash boundary); U-P3/U-P4
  resolved by FR-011 / the snapshot-store contract. Implementation lands in US2 (T016–T024a);
  register statuses update at T039.
- **DEF-E1, DEF-E2** (RewireHandle + verbatim addresses): specified (FR-031, FR-011); land in
  US4 (T031–T033) / US2 capture (T018). Update at T039.
- **DEF-F1** (self-prove liveness goal — §1.14 language-authority gate): STAYS proposal-only
  (FR-021); the proposal memo is T028's deliverable; **no implementation this wave** — approval
  is Gabi's alone. Register row stays open past wave close by design.
- **DEF-F2** (unrecoverable taxonomy, Windows-first): specified (FR-023/025); lands in US3
  (T027). Update at T039.
- **Anchors B, C (DEF-C1/C2/C3 beyond the ground-only subset), G, H, X1, X2**: no new unblocks
  from US1; C-rows' MVP halves (output layout, ground subset) were consumed by shipped 038 and
  ride unchanged here. **DEF-X3**: the §10.9 in-flight-replay fork was owner-decided at this
  wave's clarify (at-most-once, NO replay — FR-032); replay itself remains a recorded deferral.

## Gate verdict

**PASS — proceed to US2 (snapshot) on this branch.** No deferral was found silently dropped;
no scope creep past the ratified MVP cut detected (multi-client, full envelope set, Dart parity
and the self-prove goal all remain explicitly out). One deviation pair is recorded in
baseline.md (net10.0 target; client + glp_result_codec reference) — neither touches an FR.

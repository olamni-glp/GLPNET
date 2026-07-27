# Analyze findings — 060 wave-3 full Gleam chain

**Produced by**: `/bk-analyze`, 2026-07-27 | **Verdict**: 0 CRITICAL. Cleared to proceed to `/bk-implement`.

**Status**: **C1, B1, A1, U1 APPLIED to `tasks.md` on 2026-07-27** (owner-directed, after the read-only
analyze pass). C2, G1, C3, D1, I1 remain open by decision — see the judgement-calls table.

| Finding | Disposition | Change |
|---|---|---|
| C1 | ✅ applied | new task **T018a** — writer-MGU verified across module linking + engine seam |
| B1 | ✅ applied | new gate task **T028a** — Bug-Protocol triage strictly precedes T029; T029 restricted to class-(a) cases |
| A1 | ✅ applied | **T031** promoted to a gate — fails the phase below 95% |
| U1 | ✅ applied | **T042** tightened from *selectable* to *reachable* (instantiate + assert construction) |
| C2, G1, C3, D1, I1 | open | accepted as-is |

Task count moved 52 → 54. Suffixed IDs were used so the original numbering — and every reference in
this file — stays valid.

Persisted here so a restarted session does not lose the reasoning.

## Applied

### C1 — HIGH — FR-007 writer-MGU has zero task coverage

`spec.md` FR-007 requires binding writers only, never readers, never writer-to-writer. 059 recorded this DELIVERED, but wave 3 actively changes the loader (T009–T011) and introduces the engine transport seam (T007) — the code paths that could break it. No task re-verifies it.

**Fix**: add a `gleeunit` task under Phase 3 asserting writer-MGU polarity survives module linking.

### B1 — MEDIUM — Bug-Protocol obligation on golden regeneration is prose, not a gate

`tasks.md` Phase 5 carries a note: *"if T029 surfaces a behavioural divergence rather than a missing file, STOP and report — do not regenerate a golden from the runtime under test."* That obligation lives **under** the phase, not **in** T029. An executor working the checklist can run `record_dart_goldens.sh` and bake a drifted reference into a golden without reading it. Constitution II is the principle at stake.

**Fix**: promote the note into T029's task text, or split it into a preceding verification task that must pass first.

### U1 — MEDIUM — FR-025 "reachable" vs T042 "selectable" — APPLIED

FR-025 requires unproven transports stay *reachable without link-layer code changes*; T042 originally asserted only that the scheme variant was *selectable* — strictly weaker. A selectable-but-broken transport would have passed T042 while violating FR-025.

**Applied**: T042 now instantiates each of `zmq`/`quic`/`ws` through `link_scheme` and asserts construction succeeds.

### A1 — MEDIUM — SC-001's 95% target was unenforced — APPLIED

SC-001 sets a ≥95% in-scope pass rate, but T031 only *recorded* the rate. Nothing failed the wave at 60%.

**Applied**: T031 is now a gate — it computes, records with exceptions named, and fails the phase below 95%.

## Judgement calls — left open by decision

| ID | Sev | Issue | Suggested fix |
|---|---|---|---|
| C2 | MEDIUM | FR-004 (three-phase HEAD/GUARD/BODY preserved) has no task; assumed baseline-covered. | Add an assertion task, or state explicitly that T001 covers it. |
| G1 | LOW | SC-004's "no manual intervention between start-up and first message" is untested. | Fold an assertion into T040, or accept as advisory. |
| C3 | LOW | FR-031 (BEAM acceptance target) has no task — implicitly covered by everything running on BEAM. | No action. |
| D1 | LOW | FR-018 subsumed by FR-018a for the only known population. | No action — FR-018 is the general rule. |
| I1 | LOW | "reference runtime" (spec, plan) vs "Dart" (research, quickstart, `record_dart_goldens.sh`). | No action — the script name is fixed on disk. |

## Coverage

- 34 functional requirements, 10 success criteria, **54 tasks** (52 at analyze time, +2 from C1/B1).
- At analyze time: **31/34 FR covered = 91%.** Uncovered: FR-004 (C2), FR-007 (C1), FR-031 (C3).
- After applying C1: **32/34 = 94%.** Remaining uncovered: FR-004 (C2, open), FR-031 (C3, implicit).
- All 52 tasks map to at least one FR or SC — no unmapped tasks.
- Constitution: no violations. B1 sits *under* Principle II, not against it.

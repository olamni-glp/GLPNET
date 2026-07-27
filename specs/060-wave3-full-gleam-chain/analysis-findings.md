# Analyze findings — 060 wave-3 full Gleam chain

**Produced by**: `/bk-analyze`, 2026-07-27 | **Status**: reported, **not applied** (analyze is read-only)
**Verdict**: 0 CRITICAL. Cleared to proceed to `/bk-implement`.

Persisted here so a restarted session does not lose them. Two are worth fixing before implementation starts; the rest are judgement calls.

## Worth fixing first

### C1 — HIGH — FR-007 writer-MGU has zero task coverage

`spec.md` FR-007 requires binding writers only, never readers, never writer-to-writer. 059 recorded this DELIVERED, but wave 3 actively changes the loader (T009–T011) and introduces the engine transport seam (T007) — the code paths that could break it. No task re-verifies it.

**Fix**: add a `gleeunit` task under Phase 3 asserting writer-MGU polarity survives module linking.

### B1 — MEDIUM — Bug-Protocol obligation on golden regeneration is prose, not a gate

`tasks.md` Phase 5 carries a note: *"if T029 surfaces a behavioural divergence rather than a missing file, STOP and report — do not regenerate a golden from the runtime under test."* That obligation lives **under** the phase, not **in** T029. An executor working the checklist can run `record_dart_goldens.sh` and bake a drifted reference into a golden without reading it. Constitution II is the principle at stake.

**Fix**: promote the note into T029's task text, or split it into a preceding verification task that must pass first.

## Judgement calls

| ID | Sev | Issue | Suggested fix |
|---|---|---|---|
| U1 | MEDIUM | FR-025 requires unproven transports stay "reachable without link-layer code changes"; T042 only asserts *selectable* — weaker. Selectable-but-broken passes T042 and violates FR-025. | Tighten T042 to instantiate each scheme through the seam and assert construction succeeds; or weaken FR-025 to "selectable". |
| A1 | MEDIUM | SC-001 sets ≥95% in-scope pass rate; T031 only *records* it. Nothing fails the wave at 60%. | Make T031 a gate: record **and** compare against 95%, escalating below. |
| C2 | MEDIUM | FR-004 (three-phase HEAD/GUARD/BODY preserved) has no task; assumed baseline-covered. | Add an assertion task, or state explicitly that T001 covers it. |
| G1 | LOW | SC-004's "no manual intervention between start-up and first message" is untested. | Fold an assertion into T040, or accept as advisory. |
| C3 | LOW | FR-031 (BEAM acceptance target) has no task — implicitly covered by everything running on BEAM. | No action. |
| D1 | LOW | FR-018 subsumed by FR-018a for the only known population. | No action — FR-018 is the general rule. |
| I1 | LOW | "reference runtime" (spec, plan) vs "Dart" (research, quickstart, `record_dart_goldens.sh`). | No action — the script name is fixed on disk. |

## Coverage

- 34 functional requirements, 10 success criteria, 52 tasks.
- **31/34 FR covered = 91%.** Uncovered: FR-004 (C2), FR-007 (C1), FR-031 (C3).
- All 52 tasks map to at least one FR or SC — no unmapped tasks.
- Constitution: no violations. B1 sits *under* Principle II, not against it.

# Three-role orchestration — closure evidence (063 US3)

Roadmap item: **`three-role-agent-team-orchestration`** (captured,
buildkit-migration-bound). This wave's US3 deliverable is
adopt-and-operationalize (research R9): the capability already migrated into
the installed toolchain (`/bk-3rtask`, spec-051); wave-5 formalized the
protocol and ran real engagements through it.

## Deliverables (contract three-role-engagement.md)

1. **Protocol** — [PROTOCOL.md](PROTOCOL.md): the operator runbook (role
   charters, blind-then-cross-verify, false-consensus guard, authority order,
   convergence caps, evidence/attribution, engineer gates), distilled from the
   recorded method doc + the installed capability contract; references both,
   duplicates neither.
2. **Two engagements on real wave-5 gates**:
   - [E1 — plan review](engagements/E1-plan-review.md) of this wave's plan
     artifacts (run `20260730T005639Z-bf19`): 50 attributed claims, codex
     critic 32C/2R/16E; found + fixed the one real cross-artifact conflict
     (0011/0012 migration staleness); 6 spec-improvement escalates left open.
   - [E2 — code review](engagements/E2-us1-code-review.md) of the US1
     completion diff (run `20260730T012529Z-bf52`): 51 attributed claims,
     codex critic 28C/22E/1R; **found 6 real concurrency/robustness defects in
     the US1 diff, all fixed this wave** (silent-stall on write failure,
     cross-goal block misattribution, timeout/block dup-reply race, unlocked
     child stdin, mesh-before-spawn race, link_status injection +
     trailing-garbage parse), plus 2 control-plane fixes and a new
     child-death fault test.
3. **Roadmap advance** — DEFERRED to wave close (T030), executed with the
   GitFlow ship on the engineer's keystroke; this record is the evidence the
   advance links to.

## What the dogfood proved (feeds the buildkit migration)

Both engagements demonstrated the pattern's core value on real glpnet work:
the blind cross-provider (codex) critic materially strengthened both runs — it
**refuted the entire E2 method first draft**, forcing the honest
`singleton-by-design` correction (implementation facts under disjoint slices
cannot be independently corroborated and must not be over-claimed), and it
surfaced concrete code defects a single reviewer's confirmation bias would
likely have passed. Constitution V held throughout: all LM work ran through
the installed capability's Claude agents + the local codex CLI — no external
LM API on any path.

# Engagement E2 — code-review triad over the US1 completion diff

**Date**: 2026-07-30. **Run**: `20260730T012529Z-bf52` (buildkit-3rtask, code
adapter, review-only, feature 063; artifacts under
`.specify/3rtask/runs/20260730T012529Z-bf52/`, gitignored).
**Subject**: the US1 completion diff (T009–T015): the C# host `--repl` live
bridge + `Tmsg` codec half + `Mesh` self-hook (slice-host), the Python
control-plane `--repl` plumbing (slice-controlplane), the contract C1–C4 +
regression/e2e tests + `SKILL.md` (slice-spec-tests).

## Participants & roles

| Role | Runtime | Notes |
|---|---|---|
| Curator/conductor | Claude (this session) | wrote shared artifacts only via the subcommands |
| Planner | Claude sub-agent | drafted the code-review method |
| Planning Critic | **codex** (cross-provider) | blind red-team: 11 REFUTE / 4 ESCALATE / 0 CONFIRM on the first draft |
| Builders ×3 | Claude, pairwise-BLIND | host ‖ control-plane ‖ contract+tests+doc (disjoint by path) |
| Execution Critic | codex (cross-provider, `independence_warning: false`) | mechanical-merge adjudication |

## Planning phase

The blind red-team **refuted the entire first draft** — its load-bearing
objection: under pairwise-disjoint slices an implementation fact can never get
an independent second reading, so a method that promises "corroboration" of
code facts over-claims. The revision (engineer-directed) introduced the honest
correction now folded into PROTOCOL.md thinking: **`singleton-by-design`** for
implementation facts (never promoted, own rubric bucket), cross-slice
corroboration reserved for INTERFACE facts; a single **symbol-authority**
(slice-host) emitting canonical ids + aliases the merge resolves mechanically;
STRONG test links only when a graded assertion at path:line restates the
clause; a **typed-enum-field conflict predicate** (framing/on_malformed/
validation/…) so conflicts are mechanical, never prose judgment. Frozen:
`method-20260730T012529Z-bf52`, 15 elements, 4 escalates accepted at freeze;
independence audit 0 violations (×2).

## Execution phase & critic verdicts

3 blind builders → **51 attributed claims** + structured inventories (20
host symbol/hazard rows, 16 control-plane entry-point rows, C1–C4 clause table
+ test table + doc-divergence rows). Mechanical merge: 51 combined, 0 raw
conflicts, all singleton (expected — disjoint slices). Codex adjudication:
**28 CONFIRM / 22 ESCALATE / 1 REFUTE**, 4 corroborated interface pairs, 5
coverage gaps, ranked 22-item fix-candidate list. (One REFUTE: an over-broad
"malformed input cannot crash either path" claim, cited only for the mesh drop
path.) No silent merge; conflicts/singletons visible in the run's claim files
and `escalations.md` (FR-013 / acceptance-2 satisfied). All LM work ran
through the installed capability + local codex CLI (constitution V /
acceptance-3; this run is its own evidence).

## Engineer fix pass — 6 real US1-diff defects FIXED + doc/coverage tail

Applied to `csharp/glp_quick_host/Program.cs`, `glp_quick/src/glp_quick/cli.py`,
`.claude/skills/glp-quick/SKILL.md`, `glp_quick/tests/test_repl_bridge.py`:

1. **host-01** — `ReplChild.Feed` write-failure now answers the requester with
   an explicit `[repl error: repl_down]` (was a silent stall; C1 violation).
2. **host-04** — blocks are attributed to the goal captured at emit time
   (`Goal(requester,page,gen)` record); the answered mark raises only to that
   goal's generation (no cross-goal misattribution).
3. **host-02/03** — the answered transition is a single-winner CAS
   (`RaiseTo`): the `(no output)` timeout never double-replies against a real
   block; a late real block is still delivered.
4. **host-05** — child stdin writes are `lock`-guarded (concurrent mesh
   clients can no longer interleave goal bytes).
5. **host-06** — the `Mesh` is constructed BEFORE the child spawns; the death
   callback can never observe an unassigned mesh.
6. **host-08** — `Tmsg.LinkStatus` constrains `state` to atom-safe chars
   (injection-proof by construction). **host-09** — `TryParseReplGoal` now
   rejects trailing garbage after `)`.
7. **cp-02** — `--repl <path>` requires an existing regular `.dll/.exe` file
   (was exists-check only, admitting directories). **cp-07** — the gleam
   `--repl` refusal is caught in `main()` as a clean CLI error (was an
   unhandled traceback).
8. **st-08** — new test `test_repl_child_death_surfaces_as_fault_not_stall`
   covers C1's previously-untested child-death fault clause.
9. Doc: `SKILL.md` `--repl` now shows the path form, status marked LIVE, the
   `repl_down` signal added to the failure contract.

**Verification**: glp_quick_host builds clean; `glp_link.tests` 156/156;
`glp_quick` suite **188 passed** (the +1 is the new child-death test), 2
pre-existing profile-C `quicer` failures unchanged, 0 new regressions.

## Reported, NOT fixed (Bug Protocol — pre-existing code outside the US1 diff)

host-11 (Mesh.Register non-atomic, 036), host-12 (dup-id receive-blackout
undocumented, 036), host-18 (cert_load exit-code alias, 036 FR-019), host-19
(discarded stdin task swallows send errors, 036), 0abb05ae2e1b (raw-newline
JSON frame vs stdout framing, 036 relay), cp-08/cp-09 (env-derived identity +
inbox/outbox paths trusted verbatim, 037/040 console — engineer policy
decision), cp-11 (`ReplKind` 'dart' validated but never forwarded).
Recommended intake: /bk-backlog against link-completion + the 037/040 features.

## Outcome

Rubric (frozen M11): correctness-hazards 2/5 → all six fixed this pass;
trust-boundary 3/5; contract-coverage 4/5 → C1 child-death now covered;
test-strength 4/5. Verdict record: `review_only`, critic codex,
`independence_warning: false`. Budget: hit warn_confirm at ~565k vs the 500k
declaration after all agent work completed (recorded, not silent). Token
ledger (spec-020): planner 162k; builders 145k/138k/120k; codex critic rows
`unavailable`.

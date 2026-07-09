# 049 full-wave ship — handoff to the PRIMARY session

**From**: gavri (delegated US2+US3 sub-task) · **To**: primary/Olamnit session · **Date**: 2026-07-08
**Directed by**: Gabi ("hand this exact ship plan to the primary session")

## Why the ship must run from the primary session (not gavri)

1. **Branch/host**: gavri worked on the delegated sub-branch `049a-gavri-us2-us3`. `buildkit ship`
   acts on the *current* branch — run from gavri it would PR the sub-branch → develop and cut a
   release from the wrong branch, colliding with the primary session's ship (duplicate release
   branches / PRs / tags). The canonical feature branch `049-wave1-guard-link-acceptance` and the
   DBOS pipeline state live with the primary session.
2. **CLI**: the `buildkit ship` installed on gavri is the minimal build — accepts only `--help`
   (no `--dry-run`, no flags). No safe preview is possible there.
3. **Gate**: 049's ship gate is HARD — ALL FOUR user stories must pass (spec Clarifications,
   Option B). See go-conditions below.

## What gavri already delivered (integrated via PR #96, merged → feature branch)

- **US2 Profile C (SC-005): PASS** — in-process BEAM QUIC via quicer NIF; demo equals the Profile A
  baseline; `gleam_quic/src/glpq_quic.erl` + provisioning on the feature branch. MSVC-native
  attempt: toolchain works, blocker is upstream quicer's unix-only C source (escalated, FR-010).
- **US3 Two-host LAN (SC-006): PASS** — genuine cross-host handshake + mutual SPKI pin + full-duplex
  + ≥4-client mesh + kill-one resilience (Olamnit ↔ gavri).
- Evidence: `specs/049-wave1-guard-link-acceptance/evidence/gavri/{00-environment,10-profile-c,20-two-host,90-summary}.md`.
- FR-015 findings #3/#5/#6/#7 verified fixed on the branch.
- Opaque-payload transport soak was ruled the WRONG layer → superseded by promoted roadmap feature
  `glp-native-true-quic-link`; footnoted only, not part of any verdict.

## GO-CONDITIONS before running `buildkit ship` (must ALL be true)

- [ ] **US1** GLP policy-guard is a GENUINE realization (NOT the flagged shadow evaluator) — or the
      wave is expressly re-scoped by Gabi's recorded ruling.
- [ ] **US4** marathon durability verified on a real persisted run.
- [ ] US2 + US3 present on the feature branch (DONE — PR #96).
- [ ] `implement` stage = complete in the pipeline state (ship's FR-008 gate).
- [ ] Working tree clean on `049-wave1-guard-link-acceptance`.

## Exact sequence (run from the primary session, on the canonical branch)

```
# on 049-wave1-guard-link-acceptance, only after ALL go-conditions above are true:
buildkit ship            # full-wave 049: commit → push → PR(feature→develop) → merge → release → tag(main) → back-merge
/bk-close 049            # retrospective + reconcile follow-ups; advances roadmap features glp-policy-guard
                         #   + http3-quic-ws-link-full-acceptance to shipped/closed (FR-014)
/bk-specify "050 ..."    # next feature
```

Notes:
- `buildkit ship` writes `main` via the release PR — never hand-merge (CLAUDE.md).
- If the `[Unreleased]` CHANGELOG is empty it auto-seeds; different-day pyproject bump only.
- On a mid-flow stop, re-run `buildkit ship` — it is idempotent/resumable (state observed from git/gh).

**Do NOT ship on the unmet gate.** If US1 is still the shadow layer at ship time, either realize it
genuinely first or record an express re-scope ruling — shipping fabricated work tags a permanent
release on `main` and cannot be undone.

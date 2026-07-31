# Contract — US3: formal 3-role orchestration, operationalized

Authoritative sources: the roadmap record of
`three-role-agent-team-orchestration` (formalize the proven method; migration
to the toolchain already landed) + the recorded method-and-dogfood document
(`docs/research/three-role-agent-teams/METHOD-AND-DOGFOOD.md`).

## Deliverables

1. **PROTOCOL.md** (`docs/three-role-orchestration/`): the written, reusable
   protocol — planning triad (generator/validator/curator) + execution triad
   (scanner/evaluator/curator); role charters, hand-offs, blind-then-
   cross-verify rule, false-consensus guard (same-family scanners cannot
   corroborate), authority order, convergence loop + cycle caps, evidence &
   attribution rules, engineer decision gates, token-budget etiquette.
   References the installed capability's contract; duplicates nothing.
2. **Engagement records** (2 minimum, on real wave-5 gates):
   - E1: plan-review engagement over this wave's plan artifacts.
   - E2: code-review engagement over the US1 completion diff.
   Each record: participants/roles, inputs, attributed claims, critic
   verdicts (CONFIRM/REFUTE/ESCALATE), escalations raised, engineer
   decisions, outcome.
3. **Closure evidence**: the roadmap item advanced with links to 1–2.

## Acceptance

- Running an engagement requires no ad-hoc invention: every step the operator
  takes is named in PROTOCOL.md.
- Conflicting builder claims in E1/E2 escalate to the engineer visibly; no
  silent merge (FR-013).
- All LM work runs through the installed Claude-side capability (+ its local
  codex CLI degradation rules); no external LM API (constitution V).

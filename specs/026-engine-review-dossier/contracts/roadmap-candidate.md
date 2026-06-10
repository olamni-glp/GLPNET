# Contract: Roadmap Candidate Seeding (post-approval, FR-019)

**Feature**: `026-engine-review-dossier`

Governs the **only mutation outside the dossier** this feature performs: seeding successor features 2–16 into `buildkit-roadmap` as candidates. **Strictly gated** on owner approval of the dossier at the marathon gate.

## Preconditions (ALL must hold before any seeding)

1. The dossier `design-dossier.md` exists and satisfies `contracts/dossier-outline.md`.
2. The feature breakdown (dossier section 11) is topologically valid (SC-004).
3. **The owner has explicitly approved the dossier** at the marathon approval gate. (No approval → no seeding. This is an autonomous-action boundary per the safety protocol: the seeding mutation waits for the owner.)

## Action

For each successor-feature entry **2–16** (entry 1 *is* this feature — not re-seeded), create exactly one `buildkit-roadmap` candidate.

## Candidate payload (per entry)

| Field | Source | Constraint |
|---|---|---|
| `kind` | entry.kind | one of prep / experiment / mvp / follow-up |
| `scope` | entry.scope | one-line |
| `why` | entry.why | rationale |
| `depends_on[]` | entry.depends_on | references other candidates; no forward dependency |
| `state` | — | `candidate` ONLY |

## Postconditions (acceptance — SC-010)

1. Every successor feature 2–16 exists as a roadmap candidate carrying kind/scope/why/depends-on.
2. **Zero** successor features are specified, planned, or implemented (`state == candidate` for all).
3. Drawing the first successor into the pipeline is a **separate later step**, not part of this feature.

## Prohibited

- Seeding before approval.
- Running `/buildkit-specify` (or any later pipeline stage) on any successor.
- Editing engine/runtime/REPL code (INV-1).

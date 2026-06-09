# Quickstart: Engine Review + Refactoring Design Dossier

**Feature**: `026-engine-review-dossier` | **Date**: 2026-06-09

This feature delivers a document, not a runnable artifact. "Quickstart" here means: how the dossier is **authored**, how it is **verified**, and how successor authors **use** it.

## For the author (producing the dossier)

1. Read the read-only inputs: `docs/research/repl-engine-separation/{investigation,requirements,feature-definition,llvm-feasibility,research-programme}.md`.
2. For each of the 13 sections (§0–§12) in `contracts/dossier-outline.md`, write the section by **re-reading the cited code** (`out/csharp`, `csharp/glp_link`, `codeconv/.../marathon`, `glp_runtime`, `programs/self.glp`) and recording current reality with `file:line` (FR-016).
3. Render every genuine fork as an **option set** (2–5 options, consequences, trade-off, evidence, optional advisory recommendation) — never settle it (FR-011, FR-018).
4. Author the **feature breakdown** (section 11) as a topologically-valid ordered list; ensure each entry cites a dossier section (FR-013).
5. Write to `docs/research/repl-engine-separation/design-dossier.md`. Change no engine/runtime/REPL code (FR-015).

## Verification (the acceptance gate — no tests to run)

Check the dossier against the spec's measurable Success Criteria:

| Check | Criterion | How |
|---|---|---|
| 7/7 design areas covered | SC-001 | Each of sections 1–8 present; each forced-design or options |
| 2/2 premises reconciled w/ `file:line` | SC-002 | Section 9 |
| 100% open questions as options | SC-003 | Section 10 vs `investigation.md` §8.3 list |
| Options evidence-grounded + concise | SC-009 | Every option has `file:line`/prior-art + ≤ few lines |
| Breakdown well-formed, no forward deps | SC-004 | Section 11 topological check |
| MVP enumerates net-new deps + defers | SC-007 | Section 8 |
| Every area tagged + cited | SC-008 | INV-2 across sections |
| Wire-crossing design locatable from dossier alone | SC-005 | Read section 2 without opening engine source |
| Zero code lines changed | SC-006 | `git diff` touches only `docs/` + `specs/` |

## Post-approval (FR-019 seeding — owner-gated)

After the owner approves the dossier at the marathon gate, seed successor features 2–16 per `contracts/roadmap-candidate.md`:

- One `buildkit-roadmap` candidate per successor, carrying kind/scope/why/depends-on.
- `state == candidate` for all (SC-010); specify/plan/implement **nothing**.
- Verify: `buildkit-roadmap` lists 15 new candidates (features 2–16); no successor is specified.

## For a successor-feature author (consuming the dossier)

1. Open `design-dossier.md`; find your design area by anchor.
2. Read the forced design or the option set + the owner's recorded decision (made at the approval gate).
3. Use the `file:line` citations to jump straight to the code you will touch — no need to re-derive from source (US1).
4. Your roadmap candidate already records kind/scope/why/depends-on; draw it into the pipeline with `/buildkit-specify` when it is its turn.

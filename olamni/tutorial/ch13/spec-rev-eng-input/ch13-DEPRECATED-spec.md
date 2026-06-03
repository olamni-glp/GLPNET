# Feature Specification: Chapter 13 (Bonus) — Python Actors

**Feature Branch**: `014-tutorial-ch13`
**Created**: 2026-04-28
**Status**: BLOCKED — scenario not yet specified.
**Input**: `olamni/tutorial/ch13/ch13-sources.md` + `olamni/tutorial/charter.md` §3 (Implementation principle 3 — bonus chapter).
**Constitution**: `.specify/memory/constitution.md` — Principle VI; Principle I (extraction grounded in `GLP_ART.pdf`) DOES NOT APPLY HERE because Ch 13 has no PDF source — it is an Olamni-defined bonus.
**Tutorial Mode**: multi-actor-distillation (Python-actor flavour)

## Status

Per `charter.md` §3 (Implementation principle 3):
> "Bonus ch 13: Python actors instead of Dart/Flutter; bridge over JSON-line stdin/stdout subprocess. Scenario TBD with Udi."

**No scenario, no protocol selection, no bridge schema have been decided yet.**

## Required clarifications before this spec can advance

1. **Choose target protocol** — which book chapter's protocol does the bonus chapter exercise?
   - Ch 8 social-graph cold-call (befriending)?
   - Ch 9 CSSN (any of the 5 use cases)?
   - Ch 11 Grassroots Flash (cryptocurrency)?
   - Ch 12 Constitutional Consensus?
   - `programs/Bonds/` plays (fplay1–fplay12)?

2. **Define the JSON-line bridge schema:**
   - Commands FROM the GLP REPL TO Python (e.g., `befriend(alice, bob)`).
   - Events FROM Python TO the GLP REPL (e.g., `accepted(...)`, `rejected(...)`).
   - Format: one JSON object per line, on `stdin`/`stdout` of the Python subprocess.

3. **Decide harness shape:**
   - Python actors connect to a long-running GLP REPL via `subprocess.Popen` from the GLP side, OR
   - Python actors run as standalone processes, with GLP spawning them per agent.

## User Scenarios & Testing
N/A until scenario is fixed.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001** No tutorial files MAY be generated for `olamni/tutorial/ch13/` while the scenario is undecided.
- **FR-002** This spec MUST be revisited and rewritten when (a) the target protocol is selected and (b) the bridge schema is defined.
- **FR-003** Until then, `olamni/tutorial/ch13/` SHOULD contain only `ch13-sources.md` documenting the open clarifications.
- **FR-004** When unblocked, the deliverable SHOULD include: a project subdir mirroring chs 7–12 shape (modulo Python instead of Dart/Flutter), Python-actor scripts, JSON-line bridge code, and a `boot.glp` driving the play.

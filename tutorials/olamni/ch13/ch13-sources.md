# Ch 13 Sources — Bonus: Python Actors

**NOT IN GLP_ART.pdf** — this chapter is the user-defined bonus per `charter.md` §3:

> "Bonus ch 13: Python actors instead of Dart/Flutter; bridge over JSON-line stdin/stdout subprocess. Scenario TBD with Udi."

## Tutorial mode
multi-actor-distillation (use-case-driven, Python-actor flavour).

## Programs / scenario
TBD — to be specified by Udi. Scenario, choice of protocol (GC, CSSN, consensus), and target use case are not yet decided.

## Companion repo references
- Charter §3 (Python bonus chapter principle).
- The chosen base protocol's repo subtree (e.g., `programs/typed_book/cssn/` or `cryptocurrencies/`) once scenario is fixed.
- `../charter.md`

## Required clarifications before /tutorial-specify can run
- Pick the target protocol (CSSN play? Bonds? GC? Consensus?).
- Define the JSON-line bridge schema (commands in, events out).
- Decide whether actors connect to an existing Dart REPL run or a Python harness.

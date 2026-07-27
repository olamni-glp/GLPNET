# Contract: Gleam REPL command surface

**Feature**: `060-wave3-full-gleam-chain` | Serves **User Story 2** (FR-011 … FR-015)

The interactive instance's command surface. Delivered commands are frozen — wave 3 adds the two ABSENT ones (gap G4) without changing existing behaviour.

## Commands

| Command | Arguments | Result | Errors | State |
|---|---|---|---|---|
| `load <path>` | file path | `Loaded(module, procedure_count)` | `LoadError{file, clause, reason}` — instance stays usable (FR-003) | delivered |
| `<goal>.` | goal term | `Success(bindings)` \| `Failure` \| `Suspended(readers)` \| `Bounded(steps)` | `GoalError{reason}` | delivered |
| `:trace` | none (toggle) | `TraceOn` \| `TraceOff` | — | delivered |
| `:limit <n>` | positive integer | `LimitSet(n)` | `BadArgument` on n ≤ 0 | delivered |
| `:bytecode <name>/<arity>` | procedure reference | `Disassembly(instructions)` | `UnknownProcedure{name, arity}` | **wave 3** |
| `:boot <module>` | module name | `Booted(module)` \| `Failure` \| `Suspended` | `UnknownModule{name}` | **wave 3** |
| `:quit` | none | exits | — | delivered |

## Invariants

1. **A failed command never ends the session.** Every error above leaves the instance ready for the next command (FR-003).
2. **Re-load replaces.** `load` on an already-loaded module replaces its procedures; stale definitions are unreachable afterwards (FR-015).
3. **Bounded ≠ failed.** A run stopped by `:limit` returns `Bounded(steps)`, never `Failure` (FR-013).
4. **Suspension is a result.** `Suspended(readers)` is a normal outcome and names the readers being waited on (FR-006).
5. **Trace ordering is comparable.** With `:trace` on, emitted steps must be in an order comparable to the reference runtime's trace for the same goal (FR-012) — the format may differ, the sequence may not.
6. **`:bytecode` is read-only.** Disassembly must not mutate the loaded program or the heap.

## Out of scope

Command history, tab completion, multi-line editing, scripting/pipe mode. Not required by any FR.

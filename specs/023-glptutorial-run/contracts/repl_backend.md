# Contract — REPL backend invocation & outcome-only capture

**Feature**: `023-glptutorial-run` | **Date**: 2026-06-04
**Depends on**: [research.md](../research.md) (D6, D7), [data-model.md](../data-model.md)

Defines how the run layer drives a GLP REPL backend non-interactively and how it
parses the outcome-only result. The contract is **backend-agnostic**: C# (default)
and Dart print the identical outcome grammar, so one driver + one parser serve both.

---

## Backends

| Backend | Default? | Launch (argv) | Source of truth |
|---|---|---|---|
| `CSHARP` | **yes (mandated, FR-007/018)** | `dotnet run --project out/csharp/glp_repl` (or built exe when present) | `out/csharp/glp_repl/`, runner `out/csharp/lib/bytecode/runner.cs` (implemented, not a stub) |
| `DART` | on demand | `dart run bin/glp_repl.dart` or sibling `glp_runtime/glp_repl.exe` | sibling `glp_runtime/bin/glp_repl.dart` |

Both are line-oriented REPL loops reading stdin, printing to stdout, terminated by
`:quit`.

---

## stdin script grammar (driver → backend)

The driver writes, in order, one line each:

```
<load-target>          # SINGLE_FILE: a .glp path | PROJECT_DIR: a directory path
[:limit <N>]           # only when the goal needs it (Goal.needs_limit, e.g. plays)
<goal-1>.              # one or more goals, each ending in '.'
[<goal-2>. ...]
:quit
```

- A **directory** load-target is recognized by the REPL as a project load and
  prints `✓ Loaded project: <dir>` (loads all modules, resolves imports, one pass).
- A **`.glp`** load-target prints `✓ Loaded: <file>`.
- A failed load prints an error naming the file/module and why (FR-017) — the driver
  surfaces it; it does not proceed to run goals against a failed load.

The driver MUST bound execution with `--timeout` so a non-terminating goal becomes a
**reported** P1/limitation, never a frozen process (D6).

---

## stdout outcome grammar (backend → parser)

Per goal, after the `GLP> <goal>` echo (or directly, in piped mode), the backend
prints zero or more binding lines then exactly one status line:

```
<Name> = <value>          # zero or more; value may be the literal <unbound>
→ succeeds | → suspended | → failed
```

Confirmed identical in both backends:
- C#: `PrintStatus` → `"→ succeeds" | "→ failed" | "→ suspended"`; bindings as
  `"{name} = {value}"` / `"{name} = <unbound>"`.
- Dart: `_printStatus` + the same binding format.

**Parser rules (D7):**
1. Capture only **binding lines** (`^\s*\w+ = .*$`) and the **single status line**
   (`^→ (succeeds|suspended|failed)$`) for each goal. Ignore banners, prompts,
   `✓ Loaded…`, and any trace output.
2. **Outcome-only**: do not capture or diff step traces, bytecode, or
   suspension/reactivation events (FR-008).
3. **Fresh-variable normalization**: replace internal fresh-var tokens matching
   `X\d+` (and equivalents) with a canonical placeholder before comparison, because
   the goldens state these numbers vary per run. Ground bindings and the `→ status`
   compare verbatim.

---

## Golden parsing (`ex-MM-repl-trace.md`)

The golden file interleaves `GLP> <goal>` blocks with the same binding+status
lines inside fenced code. The parser splits on `GLP> <goal>` prompts and applies the
identical outcome grammar above. Goals in the golden are matched to executed goals by
goal text (normalized). A goal with no golden block → `Verdict.NO_GOLDEN`.

---

## C# failure → P1 (FR-007/018)

The backend resolver classifies these as a **critical P1 defect**, never tolerated
silently:

- `dotnet`/build absent or the C# REPL fails to start → exit 8, P1 message + cause.
- The C# REPL starts but errors / crashes / times out on a goal → exit 8, P1 message
  + captured stderr.
- The C# outcome **differs** from a backend-independent expectation (wrong result) →
  reported as a P1 difference (the `explain` verdict carries `p1_notice`).

Optional Dart fallback (`run`/`explain` may retry on Dart) is allowed **only** with a
prominent `p1_notice` stating the C# failure; the C# failure is never masked or
downgraded to a warning.

---

## Bridge-free guarantee

The backend driver uses `subprocess` + file reads only. It MUST NOT import `dbos`,
`sqlalchemy`, `psycopg`, or `codeconv.{bridge_client,runner,db}`. The extended
`test_tutorials_no_bridge.py` covers the new `backends`/`outcome` modules (D11).

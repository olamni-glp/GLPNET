# MLIR/GLP-dialect round-trip spike — RESULT

**Status**: ✅ **PASS** — recorded against **real MLIR** (compiled-LLVM `mlir.ir` bindings) in
**T020**, 2026-06-10. This is the US4 acceptance artifact (R13/R14, FR-043/071, SC-007/009). Desk
research does **not** satisfy this; an executed real-tool round-trip does.

## What was verified

The minimal GLP IL fragment **ILFRAG-1** — one clause `p(X, Y?) :- ground(X?) | q(Y).` touching each
of the four GLP/FCP dialect primitives exactly once — was **encoded** into a real MLIR module (the
four primitives realized as ops in an unregistered `glp` dialect), **printed**, **re-parsed in a
fresh `Context`**, and **decoded** back into the original `ILFragment` structure. The deterministic
oracle then checked identity.

| Primitive (FR-040) | Realized as | In ILFRAG-1 |
|---|---|---|
| HEAD-unify | `glp.head_unify` | head `p(X, Y)` |
| GUARD-test | `glp.guard_test` | `ground(X)` |
| BODY-spawn | `glp.body_spawn` | `q(Y)` |
| suspend-reactivate | `glp.suspend_reactivate` | reader `Y` |

## Result (deterministic oracle — FR-041)

```
structural  decode(encode(p)) == p : True
textual     str round-trip idempotent : True
RESULT: PASS   (oracle = deterministic; no LM in path)
EXIT=0
```

Two independent checks, both green:
- **STRUCTURAL** `decode(encode(p)) == p` — the primary metric (FR-041): the fragment survives
  GLP-IL → MLIR → text → MLIR → GLP-IL and the rebuilt dataclass equals the original.
- **TEXTUAL** — MLIR's own print is idempotent under re-parse (`str(m1) == str(m2)`).

Encoded MLIR (the `glp` dialect form that round-trips):

```mlir
module {
  "glp.clause"() ({
    "glp.head_unify"() {args = "X,Y", functor = "p"} : () -> ()
    "glp.guard_test"() {arg = "X", test = "ground"} : () -> ()
    "glp.body_spawn"() {args = "Y", goal = "q"} : () -> ()
    "glp.suspend_reactivate"() {reader = "Y"} : () -> ()
  }) {arity = "2", functor = "p"} : () -> ()
}
```

## Claude restricted to structural generation (U4 mitigation, FR-041/073)

No language model participates at run time. Claude's role was limited to **authoring** the fixed
structural encoder (`harness.py`); the MLIR parser/printer is the deterministic oracle and decides
pass/fail. There is no external-LM call on the verification path (no-API rule holds trivially — the
grep gate at T010/T025 covers the artifacts).

## Reproduction

- Canonical (WSL2): `spikes/mlir/run.sh` → runs `harness.py` on the real bindings.
- Windows wrapper: `spikes/mlir/run.ps1` → forwards to `run.sh` via `wsl.exe -d Ubuntu`.
- Subject: `spikes/mlir/ilfrag1.py` (ILFRAG-1, pure structural data — no MLIR, no LM).
- Tool versions: `spikes/mlir/tool-versions.txt` — `mlir-python-bindings 22.0.0.2025112901` (real
  compiled-LLVM, makslevental find-links), Python 3.12.3, WSL2 Ubuntu.

## Scope (minimal feasibility spike — FR-074)

ONE clause, four primitives once each. The full opcode set, the progressive-lowering pass, and the
production GLP/FCP dialect stay at **#4 / #11** (DEF-B1/H1). The mis-attributed `2502.06854` citation
is recorded open (DEF-B2; candidate LingoDB, VLDB 2022) and did not block this spike (FR-042).

**Conclusion**: the "MLIR is a viable substrate for the GLP/FCP IL" claim is backed by a recorded
real-MLIR round-trip on a non-trivial fragment — not desk research.

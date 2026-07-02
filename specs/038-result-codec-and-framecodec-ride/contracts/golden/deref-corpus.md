# Deref + var→writer fidelity corpus (US3, T035)

Reference vectors for the **server-side deep-resolve** (contract §6, R5) exercised by the
US3 fidelity tests (T033 nested-bound, T034 var→writer, T037 depth-32 truncation) in all
three runtimes. Dart is the reference (R9); C# and Gleam reproduce the **resolved
outcome**, not necessarily the same heap addresses.

Deep-resolve rule (identical in all three): `deepResolve(term, depth)` returns the
explicit `StructTerm("$truncated", [])` marker when `depth > 32`; only **struct nesting**
increments depth (bound-var indirection and constants do not). The top binding is
resolved at depth 0.

## Vectors

| name | heap input (built per runtime) | recorded Dart outcome (the reference) |
|---|---|---|
| `nested_bound` | writer S ← `point(1, 2)` over bound writers | `StructTerm("point", [ConstInt 1, ConstInt 2])` — args in order, fully resolved |
| `depth32_full` | a chain of **32** nested single-arg `s(·)` structs over a `ConstInt 0` leaf; leaf sits at depth 32 | 32 nested `s(·)` with `ConstInt 0` innermost — **no** `$truncated` (depth 32 is the last resolved level) |
| `depth33_truncated` | a chain of **33** nested `s(·)` structs | 33 nested `s(·)` then `StructTerm("$truncated", [])` at depth 33 — the marker appears at exactly the bound (no over/under-resolve) |
| `multi_var_to_writer` | 3 **unbound** query writers X, Y, Z | ordered `varToWriter = [X→(inst,addrX), Y→(inst,addrY), Z→(inst,addrZ)]`; identity is `GlobalVarId(agentId, localId)`, `localId` = the producing engine's writer addr (per-runtime), preserved across the codec round-trip |
| `nested_unbound_arg` | writer S ← `pair(a, U)` with `U` unbound | `StructTerm("pair", [ConstAtom "a", VarRef(GlobalVarId inst, addrU)])` — the remaining variable is a global id, never a raw heap addr |

### Address-independence

`nested_bound`, `depth32_full`, `depth33_truncated` contain no unbound variables, so their
resolved-term **bytes are byte-identical across runtimes** (they encode with no address
leak). `multi_var_to_writer` / `nested_unbound_arg` carry a `localId` that is the local
writer address — **per-runtime**, so these are asserted as identity-preservation (round-trip
by `GlobalVarId`), NOT cross-runtime byte-parity (R7, data-model §3).

Cyclic terms are a separate gated vector — see T041 (`$truncated` via the depth bound;
codec-local cycle policy is owner-gated D5/FORK-1, FR-008).

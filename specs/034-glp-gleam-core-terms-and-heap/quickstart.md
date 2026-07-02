# Quickstart: glp_gleam core terms + heap + unification (F4)

**Branch**: `034-glp-gleam-core-terms-and-heap` | **Date**: 2026-06-25
How to build, test, and use the F4 `runtime` kernel inside the `glp_gleam/` subtree. Environment:
**WSL Ubuntu** with the F1/F3-pinned toolchain (Gleam 1.17.0 · Erlang/OTP 25.3.2.8 · rebar3 3.19.0).
F4 is **additive** — it does not change `test/run_all_tests.sh` (the Dart REPL suite) or any other
subtree.

## Build & test (WSL)

```bash
cd glp_gleam
gleam deps download        # uses the committed manifest.toml (stdlib 1.0.3 / erlang 1.3.0 / gleeunit 1.11.0)
gleam build --target erlang # zero errors expected (SC-006)
gleam test                  # gleeunit on BEAM — all green, incl. the new runtime tests (SC-006)
./smoke.sh                  # the F3 WSL gate (gleam build + gleam test), still exit 0
```

`gleam_otp` must remain absent from `manifest.toml` (0 occurrences — SC-006). No build artifacts
(`build/`, `*.beam`) are committed (SC-007).

## Using the kernel (illustrative Gleam)

```gleam
import glp/runtime as rt        // umbrella re-exports; or import glp/runtime/heap, .../terms, .../unify

pub fn demo() {
  let h0 = rt.new_heap()                                   // empty store
  let #(h1, w, r) = rt.allocate_variable(h0)               // fresh logic var: writer w, reader r

  // US1: a fresh variable derefs to Unbound
  let assert Ok(#(h2, rt.Unbound(_))) = rt.deref(h1, r)

  // US1: bind the writer to a ground value, deref the reader back to it
  let value = rt.const_atom("bound_atom")
  let assert Ok(#(h3, _activations)) = rt.bind_writer(h2, w, value)
  let assert Ok(#(_h4, rt.Bound(out))) = rt.deref(h3, r)   // out == ConstTerm(ConstAtom("bound_atom"))

  // US2: three-valued unification
  let #(h5, w2, _r2) = rt.allocate_variable(h3)
  let assert Ok(rt.Success(_)) = rt.unify(h5, rt.VarRef(w2), rt.const_int(42))   // writer bound → Success
  let assert Ok(rt.Fail) = rt.unify(h5, rt.const_int(1), rt.const_int(2))         // mismatch → Fail
}
```

## What F4 delivers (and what it does not)

| In scope (F4) | Out of scope (later features) |
|---|---|
| Term model: const (atom/int/real/string), struct, list (cons/nil), var ref | `MutualRefTerm`, `ModuleTerm` (F6) |
| Heap: allocate, tag-based roles, path-compressing deref, bind-to-value, bind-to-var | Goal scheduler / reduction loop (F5) |
| Writer-MGU three-valued unification (success / suspend / fail) | Compiler, loader, REPL (F6/F7) |
| Suspension storage + activation-list **production** | Imported readers / cross-agent / link (F9+) |
| Dart-derived observable-outcome parity corpus | Internal-heap-layout parity (explicitly excluded) |

## Acceptance walkthrough (maps to the spec's Independent Tests)

1. **US1 (SC-001/SC-002)** — `gleam test` runs `terms_test` + `heap_test`: build/inspect/compare all 9
   term kinds; allocate → deref `Unbound` → bind → deref the value → re-deref O(1) (compressed).
2. **US2 (SC-003/SC-004)** — `unify_test`: the full truth table returns the correct
   success/suspend/fail; every WxW attempt returns `Error(WriterToWriter)` (0 silent).
3. **US3 (SC-005)** — `suspension_test` + `parity_test`: suspend → bind → exact activation list;
   var-bind forwards suspensions; the micro-scenario corpus matches the Dart source-of-truth on every
   observable outcome.

## Verifying additivity (SC-007)

```bash
# from repo root, after the F4 change:
git status --porcelain glp_gleam/            # only NEW src/glp/runtime/** + test/glp/runtime/** (+ filled runtime.gleam)
git diff --stat -- glp_runtime/ glp_runtime_net/ out/csharp/ codeconv/   # MUST be empty (no other-subtree change)
git check-ignore glp_gleam/build || true     # build/ ignored — never committed
```

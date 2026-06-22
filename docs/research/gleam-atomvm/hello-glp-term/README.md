# hello-glp-term smoke

Throwaway-grade Gleam smoke for **031-gleam-port-spike** (epic `gleam-atomvm`, F1). It proves
the Gleam→BEAM toolchain end-to-end, gives the architectural-fit risk *running* evidence, and —
critically — **runs on AtomVM as well as Erlang**.

It constructs a representative GLP term (`pair(label, _G0)` — one compound/structure `pair/2`
and one unbound-variable analogue `_G0`) and demonstrates **exactly one** unbound→bound
transition, observed by a reader, two ways:

- **PRIMARY — process/state-holder** ("logic variable = BEAM process"): a cell process holds the
  binding; a separate **writer** process binds it; a separate **reader** process observes the
  bound value. The cell is spawned via a **raw `erlang:spawn`** external, and uses
  `gleam_erlang` Subjects (`self()`+`make_ref()`, `!`, selective `receive`) for typed messaging.
- **FUNCTIONAL SIBLING** — the same single bind via immutable threaded state (the
  mutable-heap-vs-immutability contrast: the old unbound value is never mutated).

**Why raw `erlang:spawn`, not `gleam_otp`?** AtomVM's BEAM/OTP subset omits **`proc_lib`**, and
both `gleam_otp`'s actor and `gleam_erlang`'s own `process.spawn`/`spawn_unlinked` route through
`proc_lib`. Spawning the raw way keeps the whole smoke inside AtomVM's subset, so it runs on
**both** Erlang and AtomVM. (A `gleam_otp` actor version crashes on AtomVM with
`Unable to open proc_lib.beam … module proc_lib cannot be resolved` — that's the recorded
subset boundary.)

**Out of scope (NOT implemented):** full unification of two terms, suspension/reactivation
*scheduling*, bytecode execution, performance measurement. The single bind is the *bounded*
mutable-variable demonstration.

Pinned toolchain (exact versions + setup): see `../toolchain-inventory.md`.
Environment used: **WSL Ubuntu 24.04.3 (x86_64)** — Gleam 1.17.0 · Erlang/OTP 25.3.2.8 ·
AtomVM v0.6.6 (host, static-mbedtls). Deps: `gleam_stdlib` 1.0.3 · `gleam_erlang` 1.3.0.

---

## Build & run on Erlang/BEAM (FR-004, US2)

```bash
cd docs/research/gleam-atomvm/hello-glp-term
gleam build --target erlang
gleam run   --target erlang
```

**Observed output** (`gleam run --target erlang`, verbatim):

```text
    Running hello_glp_term.main
== hello-glp-term : Gleam smoke on Erlang/BEAM + AtomVM ==
representative term       : pair(label, _G0)
  compound/structure      : pair/2
  unbound-variable        : _G0

[process/state-holder model: logic variable = BEAM process (raw spawn)]
  cell before bind (read by main)     : unbound
  writer process binds _G0            : _G0 := bound_atom
  cell after bind (read by reader)    : bound_atom
  resolved term                       : pair(label, bound_atom)

[functional sibling model: immutable threaded state]
  heap0 (unbound)                     : unbound
  heap1 = write(heap0, bound_atom)    : bound_atom
  heap0 re-read (immutable, unchanged): unbound
```

**Tests** (`gleam test --target erlang`): `4 passed, no failures`.

## Reproducibility (SC-002)

`rm -rf build && gleam run --target erlang` re-resolves `gleam_stdlib 1.0.3` / `gleam_erlang
1.3.0` / `gleeunit 1.11.0`, recompiles, and prints the SAME block above.

---

## Run the full smoke on AtomVM (FR-005, US3) — host build v0.6.6

A prebuilt AtomVM host release was used (no source build needed). AtomVM accepts `.beam` files
directly and calls the first module's `start/0`; the Gleam module exports `pub fn start()`.

```bash
cd docs/research/gleam-atomvm/hello-glp-term
gleam build --target erlang
MAIN=build/dev/erlang/hello_glp_term/ebin/hello_glp_term.beam
DEPS=$(find build/dev/erlang -path '*/ebin/*.beam' ! -name 'hello_glp_term.beam')
/opt/atomvm/AtomVM-static "$MAIN" $DEPS /opt/atomvm/atomvmlib-v0.6.6.avm
```

**Observed output on AtomVM** (verbatim — the SAME term + bind + functional output as Erlang):

```text
== hello-glp-term : Gleam smoke on Erlang/BEAM + AtomVM ==
representative term       : pair(label, _G0)
  compound/structure      : pair/2
  unbound-variable        : _G0

[process/state-holder model: logic variable = BEAM process (raw spawn)]
  cell before bind (read by main)     : unbound
  writer process binds _G0            : _G0 := bound_atom
  cell after bind (read by reader)    : bound_atom
  resolved term                       : pair(label, bound_atom)

[functional sibling model: immutable threaded state]
  heap0 (unbound)                     : unbound
  heap1 = write(heap0, bound_atom)    : bound_atom
  heap0 re-read (immutable, unchanged): unbound
Return value: nil
```

So the **full** Gleam smoke — term construction AND the process/state-holder unbound→bound bind
over real BEAM processes — runs on AtomVM, not just on Erlang. Sanity: the AtomVM host build
also runs its own `hello_world.avm` → `Return value: ok`.

> **Packaging note (reproducibility).** AtomVM's CLI accepts one-or-more files —
> `<path-to-avm-or-beam-file>+` (`AtomVM-static -h`) — so passing the app beam **plus all
> dependency beams** is supported, and the run above was re-verified reproducible from a clean
> `gleam build`. AtomVM's *documented production packaging* instead bundles the same beams into a
> single `.avm` via `packbeam` (e.g. the `atomvm_rebar3_plugin` / `atomvm_packbeam`); F2/F3 should
> adopt `.avm` packing for shippable artifacts. Either way the host-build verdict stands — this is
> a packaging-form note, not a viability caveat.

---

## JavaScript backend (US4)

The full smoke does **not** compile to JS (`gleam_erlang` processes are Erlang/BEAM-only —
`gleam build --target javascript` → `error: Unsupported target … no implementation for the
JavaScript target` at `process.send` / `process.receive`). The **pure functional subset**
(term construction + immutable bind, `gleam_stdlib` only) DOES compile + run on JS — see the
committed sibling project `../js-probe/` (`cd ../js-probe && gleam run --target javascript`).

---

## Project layout (the assumed `glp_gleam/` convention for F2/F3)

```text
hello-glp-term/
├── gleam.toml            # name, deps (gleam_stdlib, gleam_erlang; gleeunit dev) — NO gleam_otp
├── manifest.toml         # locked dep versions (committed)
├── src/hello_glp_term.gleam   # term + raw-spawn process bind + functional sibling; start/0 for AtomVM
├── test/hello_glp_term_test.gleam
└── README.md             # this file (recorded commands + observed output, Erlang + AtomVM)
```

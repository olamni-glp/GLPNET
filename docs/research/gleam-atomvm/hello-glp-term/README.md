# hello-glp-term smoke

Throwaway-grade Gleam smoke for **031-gleam-port-spike** (epic `gleam-atomvm`, F1). It proves
the Gleam→BEAM toolchain end-to-end and gives the architectural-fit risk *running* evidence.

It constructs a representative GLP term (`pair(label, _G0)` — one compound/structure `pair/2`
and one unbound-variable analogue `_G0`) and demonstrates **exactly one** unbound→bound
transition, observed by a reader, two ways:

- **PRIMARY — process/state-holder** ("logic variable = BEAM process"): a Gleam `gleam_otp`
  actor holds the cell; a separate **writer** process binds it; a separate **reader** process
  observes the bound value. Core BEAM message passing.
- **FUNCTIONAL SIBLING** — the same single bind via immutable threaded state, making the
  mutable-heap-vs-immutability contrast explicit (the old unbound value is never mutated).

**Out of scope (NOT implemented):** full unification of two terms, suspension/reactivation
*scheduling*, bytecode execution, performance measurement. The single bind is the *bounded*
mutable-variable demonstration.

Pinned toolchain (exact versions + setup): see `../toolchain-inventory.md`.
Environment used: **WSL Ubuntu 24.04.3 (x86_64)** — Gleam 1.17.0 · Erlang/OTP 25.3.2.8.

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
== hello-glp-term : Gleam smoke on Erlang/BEAM ==
representative term       : pair(label, _G0)
  compound/structure      : pair/2
  unbound-variable        : _G0

[process/state-holder model: logic variable = BEAM process]
  cell before bind (read by main)     : unbound
  writer process binds _G0            : _G0 := bound_atom
  cell after bind (read by reader)    : bound_atom
  resolved term                       : pair(label, bound_atom)

[functional sibling model: immutable threaded state]
  heap0 (unbound)                     : unbound
  heap1 = write(heap0, bound_atom)    : bound_atom
  heap0 re-read (immutable, unchanged): unbound
```

**Tests** (`gleam test --target erlang`):

```text
    Running hello_glp_term_test.main
....
4 passed, no failures
```

## Reproducibility (SC-002)

Clean rebuild reproduces byte-identical output:

```bash
rm -rf build && gleam run --target erlang
# → re-downloads gleam_stdlib 1.0.3 / gleam_erlang 1.3.0 / gleam_otp 1.2.0 / gleeunit 1.11.0,
#   recompiles, prints the SAME block shown above.
```

---

## AtomVM attempt (FR-005, US3) — host build v0.6.6, effort-bounded

R3 ladder: a **prebuilt host release was found** (`AtomVM-linux-x86_64-static-mbedtls-v0.6.6`),
so no source build was needed. AtomVM accepts `.beam`/`.avm` files directly; Gleam's entry is
`main/0`, so a one-line `start/0` shim is used.

**1. AtomVM host build runs** (sanity, the release's own `hello_world.avm`):

```bash
/opt/atomvm/AtomVM-static hello_world-v0.6.6.avm
# → Return value: ok
#   hello_world
```

**2. The smoke on AtomVM — partial:**

```bash
printf '%s\n' '-module(glp_start).' '-export([start/0]).' 'start() -> hello_glp_term:main().' > /tmp/glp_start.erl
erlc -o /tmp /tmp/glp_start.erl
BEAMS=$(find build/dev/erlang -path '*/ebin/*.beam' | tr '\n' ' ')
/opt/atomvm/AtomVM-static /tmp/glp_start.beam $BEAMS /opt/atomvm/atomvmlib-v0.6.6.avm
```

Observed (abridged) — the **term-construction path runs on AtomVM**, then the `gleam_otp`
actor crashes:

```text
== hello-glp-term : Gleam smoke on Erlang/BEAM ==
representative term       : pair(label, _G0)
  compound/structure      : pair/2
  unbound-variable        : _G0
Unable to open proc_lib.beam
Warning: module proc_lib cannot be resolved.
CRASH
======
Stacktrace:
[{gleam@otp@actor,start,1,...},{hello_glp_term,process_bind_demo,0,...},{hello_glp_term,main,0,...}]
x[1]: undef
**End Of Crash Report**
Return value: error
```

**Named BEAM/OTP-subset limitation**: AtomVM 0.6.6's OTP subset does **not** include
**`proc_lib`**, which `gleam_otp`'s actor uses to spawn/init the actor process → `undef`.

**3. The concurrency *substrate* works on AtomVM** (raw BEAM primitives — `spawn`/`!`/
`receive`/`make_ref`, no `gleam_otp`). This pinpoints the boundary: the variable-as-process
pattern itself is AtomVM-portable; only the OTP-library abstraction is not.

```bash
# /tmp/atomvm_probe.erl : a cell process bound via raw spawn/!/receive/make_ref
erlc -o /tmp /tmp/atomvm_probe.erl
/opt/atomvm/AtomVM-static /tmp/atomvm_probe.beam atomvmlib-v0.6.6.avm
# → Return value: true
#   {cell_after_bind,{bound,bound_atom}}
```

---

## JavaScript backend (US4) — node v18.19.1

**Full smoke does NOT compile to JS** — `gleam_erlang`/`gleam_otp` are Erlang/BEAM-only
(processes have no JS implementation):

```bash
gleam build --target javascript
# → error: Unsupported target
#     ┌─ src/hello_glp_term.gleam:144:41
#     │   actor.new(None) |> actor.on_message(handle_cell) |> actor.start
#   This value is not available as it is defined using externals, and there is
#   no implementation for the JavaScript target.
#   (same for process.send / process.receive / actor.start / actor.call)
```

**The pure functional subset DOES compile + run on JS** (term construction + immutable bind,
using only `gleam_stdlib`), verified in a throwaway `js_probe` project:

```text
    Running js_probe.main
== js functional subset (no BEAM processes) ==
representative term : pair(label, _G0)
heap0 (unbound)     : unbound
heap1 (bound)       : bound_atom
heap0 re-read       : unbound
```

So JS carries the **pure** parts of a GLP port but not the BEAM-process concurrency model.

---

## Project layout (the assumed `glp_gleam/` convention for F2/F3)

```text
hello-glp-term/
├── gleam.toml            # name, deps (gleam_stdlib, gleam_erlang, gleam_otp; gleeunit dev)
├── manifest.toml         # locked dep versions (committed)
├── src/hello_glp_term.gleam
├── test/hello_glp_term_test.gleam
└── README.md             # this file (recorded commands + observed output)
```

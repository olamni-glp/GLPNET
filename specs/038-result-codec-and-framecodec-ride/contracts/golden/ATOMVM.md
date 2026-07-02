# Running the codec on AtomVM (T039 float / T040 int64-edge)

AtomVM is an **independent BEAM-alternative VM** (it executes `.beam` module files but is
not Erlang/OTP's BEAM runtime). The gated entries — float `0x03` (ED-6 `/float` spike) and
the 64-bit-int edges — must be decode-verified **on AtomVM**, because a plain `gleam test`
runs on full Erlang/OTP and is therefore NOT an AtomVM-faithfulness signal (R11, FR-011).

We run AtomVM **outside a browser, under Node.js, with no Linux/Unix distro** — the VM is
WebAssembly, so a JS/WASM host is all that's needed.

## One-time: build AtomVM.mjs for the node target

The AtomVM emscripten platform supports two link targets (`AVM_EMSCRIPTEN_ENV`): `web`
(browser, `-sENVIRONMENT=web,worker`, fetches the wasm) and the default **node**
(`-sNODERAWFS -sENVIRONMENT=node` — real filesystem, runnable from Node). Build the node
variant once (emsdk on PATH):

```bash
cd <AtomVM>/src/platforms/emscripten
emcmake cmake -B build -DAVM_EMSCRIPTEN_ENV=node .
ninja -C build AtomVM        # relinks build/src/AtomVM.mjs (+ AtomVM.wasm) for node
```

Then AtomVM runs a module directly, no browser and no separate JS shim:

```bash
node <AtomVM>/src/platforms/emscripten/build/src/AtomVM.mjs <first.beam> <deps...>
```

AtomVM selects the module exporting `start/0` as the entry point (here
`atomvm_gated_probe`), and loads every other `.beam` passed as an argument.

## Run the gated conformance probe

```bash
bash specs/038-result-codec-and-framecodec-ride/contracts/golden/run_atomvm_gated.sh
```

The runner builds the Gleam erlang target, gathers the real codec beams
(`glp@codec@term_codec` + its `gleam_stdlib` deps) plus `atomvm_gated_probe.beam`
(`glp_gleam/src/atomvm_gated_probe.gleam`), runs them on AtomVM, and asserts:

| entry | term bytes on AtomVM | round-trip |
|---|---|---|
| int64 **max** `9223372036854775807` | `<<2, 255,255,255,255,255,255,255,127>>` | `true` |
| int64 **min** `-9223372036854775808` | `<<2, 0,0,0,0,0,0,0,128>>` (two's-complement LE) | `true` |
| **float** Pi `3.141592653589793` | `<<3, 24,45,68,84,251,33,9,64>>` (IEEE-754 LE) | `true` |

## Result (verified 2026-07-02, AtomVM 0.7.999)

**PASS** — the real `glp/codec/term_codec` `encode_term`/`decode_term` running on AtomVM
produce **byte-identical** output to the Dart source of truth (and C#/Gleam-on-BEAM) for
both the float `0x03` entry and the 64-bit-int edges, and all three **round-trip**
(`decode_term(bytes) == Ok(#(term, <<>>))`). AtomVM 0.7.999 supports 64-bit float
bitstrings and masks bignums to the low 64 bits (two's-complement) exactly as the codec
requires. (The spec text names AtomVM 0.6.6; the installed toolchain is 0.7.999 — the run
is on the genuine AtomVM present on the host.)

Gated entries remain **quarantined / NOT byte-final** in SC-002 regardless (R11/R6); this
run records the AtomVM decode-verification the gate calls for.

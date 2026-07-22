# AtomVM gated-probe runbook — WP `guard-atomvm-gated-probe`

**Feature**: 059 full-scope Gleam GLP · **Wave**: 1 (guard) · **Created**: 2026-07-20 · **Baseline commit**: `49b523420d745875c67207417adf56c8a5537331` · **Register entry**: `phase2-plan/frozen-interface-register.md` → ✅ RESOLVED 2026-07-21 (measured; see below)

## Why this guard is manual, and what that costs

`gleam test` runs on **full Erlang/OTP, not AtomVM**, and is therefore explicitly **not** an AtomVM-faithfulness signal (spec R11 / FR-011, tasks T039/T040). The probe at `glp_gleam/src/atomvm_gated_probe.gleam` is the only artifact that exercises the real feature-038 term codec *on AtomVM*. It cannot ride the `gleam test` suite guard, so this guard is **human-in-the-loop at feature checkpoints** — by the capability's own design, the weakest drift control in wave 1. That is recorded here rather than hidden.

## What the probe covers

The **gated** codec entries — the ones deliberately quarantined from the byte-final goldens (R11/R6) because their AtomVM representation was not pinned:

| Entry | Term | Source |
|---|---|---|
| 64-bit int max | `ConstTerm(ConstInt(9_223_372_036_854_775_807))` | T040 |
| 64-bit int min | `ConstTerm(ConstInt(-9_223_372_036_854_775_808))` | T040 |
| IEEE-754 double | `ConstTerm(ConstReal(3.141592653589793))` | T039 (ED-6 AtomVM float spike) |

For each, the probe displays (a) the encoded bytes from `encode_term`, and (b) the boolean result of the round-trip check `decode_term(bytes) == Ok(#(term, <<>>))` — the original term plus **no residue**.

## Procedure

1. Build the Gleam project to Erlang beams (WSL):
   ```
   cd /mnt/d/bstdev/research/glp/glpnet/glp_gleam && gleam build --target erlang
   ```
2. Run the probe under the **Node AtomVM wrapper** (no browser, no distro). AtomVM selects the module exporting `start/0` — this probe — as the entry point:
   ```
   node <AtomVM.mjs> atomvm_gated_probe.beam glp@codec@term_codec.beam \
        gleam@int.beam gleam@bit_array.beam gleam@result.beam gleam_stdlib.beam ...
   ```
   The beams live under `glp_gleam/build/dev/erlang/*/ebin/`. The wrapper path is environment-local and is **not** pinned by this repo.

## Expected verdicts (the guard's assertion)

1. **All three round-trip checks display `true`** — encode∘decode is identity with no residue on AtomVM.
2. **The three entries remain EXCLUDED from the byte-final golden corpus.** This guard asserts the exclusion still holds; it does **not** promote these entries to byte-final. Promotion would be a change to the frozen `codec-envelope` contract and requires a rule-request ruling.
3. `git diff --exit-code glp_gleam/src/atomvm_gated_probe.gleam <baseline commit>` is **empty** — the probe source itself is pinned.

## Measured verdicts — 2026-07-21 (items 1–2 EXECUTED on AtomVM: expected → **measured**)

**Executed 2026-07-21.** The probe was run on a genuine Node AtomVM wrapper and the displayed byte output is recorded below as the reference verdict. This converts items 1–2 above from "expected" to **measured** and closes the wave-1 gap.

**Environment (this run):**
- **Node AtomVM wrapper**: `AtomVM-node-v0.7.0-alpha.1.js` + `AtomVM.wasm`, from upstream release `atomvm/AtomVM@v0.7.0-alpha.1` (the Node/WASM target — the released **0.7.x** line that the commit-`99a80ba7` `0.7.999` main snapshot belongs to). Installed to `C:\Users\gavri\tools\atomvm\`, sha256-verified: `.js` = `5df0b0ce39e8f50518be34c8c50286bdeca435083252699f90ab3e3de3145d20`, `.wasm` = `966d6121f1f32cbcc306ce0e4cd0918763bf4e224e886f6a731f6e0887ec8075`. (The emscripten loader hard-references `AtomVM.wasm`, so the release `.wasm` is placed under that name alongside the `.js`.)
- **Host**: Windows Node `v22.22.2` — no browser, no distro, the WASM VM under Node, exactly as the procedure prescribes.
- **Beams**: `glp_gleam` built `gleam build --target erlang` under **OTP-27** (`tools/otp27`) — the real feature-038 codec (`glp@codec@term_codec.beam`) + `atomvm_gated_probe.beam` + gleam_stdlib deps.
- **Command**: `node AtomVM-node-v0.7.0-alpha.1.js atomvm_gated_probe.beam glp@codec@term_codec.beam gleam@int.beam gleam@bit_array.beam gleam@result.beam gleam@order.beam gleam@list.beam gleam@bool.beam gleam@option.beam gleam_stdlib.beam` (the committed `specs/038-result-codec-and-framecodec-ride/contracts/golden/run_atomvm_gated.sh` drives the same, with `AVM_MJS=<installed wrapper>`, `ERLANG_BIN=tools/otp27/bin`).

**Measured output** — for each term the probe displays the `encode_term` bytes then the `decode_term(bytes) == Ok(#(term, <<>>))` round-trip bool:

| Entry | Term | Measured bytes on AtomVM | Round-trip |
|---|---|---|---|
| 64-bit int max | `ConstTerm(ConstInt(9_223_372_036_854_775_807))` | `<<2,255,255,255,255,255,255,255,127>>` | `true` |
| 64-bit int min | `ConstTerm(ConstInt(-9_223_372_036_854_775_808))` | `<<2,0,0,0,0,0,0,0,128>>` (two's-complement LE) | `true` |
| IEEE-754 double | `ConstTerm(ConstReal(3.141592653589793))` | `<<3,24,45,68,84,251,33,9,64>>` (IEEE-754 LE) | `true` |

**Result: PASS.** All three round-trip checks display `true`, and the encoded bytes are **byte-identical** to (a) the pinned expected table above, (b) the prior AtomVM 0.7.999 measurement recorded in `specs/038-result-codec-and-framecodec-ride/contracts/golden/ATOMVM.md` (commit `99a80ba7`), and (c) the Dart/C#/Gleam-on-BEAM source of truth. Item 2 (exclusion of the gated entries from the byte-final goldens) is unchanged — this run **records the AtomVM verdict, it does not promote** the entries to byte-final.

**Refutation discipline honored (environment, not codec absence).** The AtomVM present by default in this session's toolchain was the native **0.6.6** Linux build in WSL (`/opt/atomvm/AtomVM-static`). It **cannot run this codec** — it aborts with `Warning: function erlang:list_to_bitstring/1 cannot be resolved` then a CRASH inside `gleam_stdlib:bit_array_concat/1` on the very first encode (a 0.6.6 BIF gap; 64-bit-float bitstrings likewise landed after 0.6.6). Per this runbook's refutation condition that is classified **environment**, not a codec `false`, and is precisely why the newer 0.7.x Node wrapper had to be installed. No round-trip `false` was ever observed on a VM that can run the codec.

**Corroboration — source-built main-snapshot wrapper (2026-07-22).** To close the version gap between the released 0.7.x wrapper used above and the `0.7.999` main snapshot that commit `99a80ba7` originally used, the Node AtomVM wrapper was **also built from AtomVM main source** via emsdk — the exact `emcmake cmake -B build -DAVM_EMSCRIPTEN_ENV=node . && ninja -C build AtomVM` procedure `specs/038-result-codec-and-framecodec-ride/contracts/golden/ATOMVM.md` documents. Built at AtomVM main commit `0220c78` (2026-07-13), `ATOMVM_BASE_VERSION = 0.8.0-dev` — main has since rolled past 0.7 to the 0.8 line, so the literal string `0.7.999` is a *historical* main-snapshot version, not reproducible from current main; this is the same-class artifact. Running the identical probe on that `AtomVM.mjs` (`C:\Users\gavri\tools\atomvm-src\src\platforms\emscripten\build\src\`) prints the **same three byte-lines + three `true`s**, byte-identical to the table above. The gated-codec byte-parity verdict is therefore corroborated on **two independent, genuinely-built AtomVM VMs** — released `v0.7.0-alpha.1` and source-built `0.8.0-dev` main — bracketing the original `0.7.999`.

## Baseline history — freeze → closure

- **At the freeze baseline (2026-07-20):** items 1–2 were **not executed** — the Node-AtomVM wrapper was not present in the session toolchain, so under the register's **degrade-loudly** rule this was carried as an open gap, never a silent skip. What was pinned even then: the probe source (item 3, git-diff checkable) and the exclusion of the gated entries from the goldens (the `codec-envelope` freeze + its golden tests under `gleam test`).
- **Closed 2026-07-21:** the wrapper was located/installed (see above) and this runbook executed; items 1–2 are now measured, so wave-4 claims depending on AtomVM faithfulness cite this measured verdict rather than the prior gap.

## Refutation conditions

- A round-trip check displays `false` on AtomVM ⇒ the gated entry is a genuine cross-VM codec divergence — escalate under the `codec-envelope` freeze; do not adjust the probe to pass.
- The wrapper cannot load the beams ⇒ classify as **environment**, not absence, before drawing any conformance conclusion (the same discipline the plan requires for Profile-C QUIC).

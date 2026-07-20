# AtomVM gated-probe runbook — WP `guard-atomvm-gated-probe`

**Feature**: 059 full-scope Gleam GLP · **Wave**: 1 (guard) · **Created**: 2026-07-20 · **Baseline commit**: `49b523420d745875c67207417adf56c8a5537331` · **Register entry**: `phase2-plan/frozen-interface-register.md` → open items

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

## Baseline status at wave 1 — HONEST GAP

**Not executed at the freeze baseline.** The recorded procedure above is reconstructed from the probe's own module documentation; the Node-AtomVM wrapper is an environment-local dependency that is not present/verified in this session's toolchain, and the plan's own acceptance language calls this "its recorded manual procedure."

Per the register's **degrade-loudly** rule this is recorded as a gap, never a silent skip:

- What IS pinned right now: the probe source (item 3 above, git-diff checkable) and the exclusion of the gated entries from the goldens (enforced by the `codec-envelope` freeze and its golden tests, which run under `gleam test`).
- What is NOT pinned right now: the AtomVM **execution** verdicts (items 1–2), because the wrapper has not been run at this baseline.
- Closing action: a first execution of this runbook, with its displayed byte output pasted into this file as the reference verdict, converts items 1–2 from "expected" to "measured". Until then, any wave-4 claim that depends on AtomVM faithfulness must cite this gap rather than assume it.

## Refutation conditions

- A round-trip check displays `false` on AtomVM ⇒ the gated entry is a genuine cross-VM codec divergence — escalate under the `codec-envelope` freeze; do not adjust the probe to pass.
- The wrapper cannot load the beams ⇒ classify as **environment**, not absence, before drawing any conformance conclusion (the same discipline the plan requires for Profile-C QUIC).

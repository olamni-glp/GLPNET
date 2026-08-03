# Quickstart: Wave 3 — Full Gleam chain

**Feature**: `060-wave3-full-gleam-chain` | **Date**: 2026-07-27

This is the path SC-003 measures: **first contact → an answer to a goal in under 5 minutes.**

---

## 1. Prerequisites

```
gleam --version          # Gleam toolchain, Erlang/BEAM target
erl -version             # full BEAM (AtomVM is NOT required — FR-031)
```

Wave-3 acceptance runs on the full BEAM. No AtomVM build, no MSVC, no WSL `quicer` — those belong to deferred work (research.md D2, D3).

## 2. Build

```
cd glp_gleam
gleam build
gleam test               # baseline: 465 green
```

If `gleam test` is not at 465, stop — that is the non-regression floor (SC-009), and a change made against a red baseline cannot be attributed.

## 3. Run a goal (User Story 1)

```
gleam run -- load programs/tests/typed/<file>.glp
gleam run -- goal '<goal>.'
```

Expected outcomes are the same three the reference runtime gives: **succeeds**, **fails**, or **suspended**. Suspension is a first-class result, not an error (FR-006).

## 4. Interactive session (User Story 2)

```
gleam run -- repl
```

| Command | Effect | State |
|---|---|---|
| `load <path>` | parse → SRSW → type-check → compile → load | delivered |
| `<goal>.` | pose a goal | delivered |
| `:trace` | toggle execution tracing | delivered |
| `:limit <n>` | bound execution steps | delivered |
| `:bytecode <name>/<arity>` | show compiled form | **wave-3 work (G4)** |
| `:boot <module>` | boot a module's entry point | **wave-3 work (G4)** |
| `:quit` | leave | delivered |

Re-loading a file replaces its definitions; stale definitions must not survive (FR-015).

## 5. Conformance corpus (User Story 3)

```
bash test/parity/run_gleam_corpus.sh          # Gleam verdicts
bash test/parity/run_differential.sh          # Gleam vs reference, case by case
bash test/parity/record_dart_goldens.sh       # regenerate missing goldens (FR-018b)
```

Read the counts, not just the exit code:

```
pass + fail + out_of_scope  ==  total          # SC-002 — nothing silently skipped
out_of_scope("golden missing — 059 T051 drift") == 44 at wave start, 0 at wave end  # SC-010
```

A case with no golden is **out-of-scope with a reason**, never a pass (FR-018a).

## 6. Link two Gleam instances (User Story 4)

Start two instances, join over loopback first, then TCP:

```
# instance A
gleam run -- serve --scheme loopback --address a
# instance B
gleam run -- link --scheme loopback --peer a
```

Check in this order:
1. Both sides report the peer connected (FR-020).
2. A message round-trips intact and in order (FR-021).
3. Kill one — the survivor reports the loss **within 30 s** rather than blocking (FR-024, SC-007).
4. Point a peer with a bad capability set at it — refusal carries a stated reason (FR-022).

Repeat over `--scheme tcp`. Those two schemes are the whole acceptance surface; `zmq`/`quic`/`ws` stay behind the seam unproven (FR-025).

## 7. Cross-runtime C# ↔ Gleam (User Story 5)

Requires a runnable C# GLP instance. Every scenario runs **in both directions** — C# initiating and Gleam initiating (FR-028) — and a term echoed back must come home identical, nested structures and unbound variables included (FR-027).

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `gleam test` below 465 | baseline broken before your change — fix or revert first |
| Goal returns `fails` where reference says `suspended` | three-valued unification defect — this is a Bug-Protocol STOP, not a tweak |
| Corpus run exits non-zero with rc=44 | the known missing-goldens drift; regenerate via `record_dart_goldens.sh` |
| Link hangs instead of reporting peer loss | inbound pump / fault propagation gap (G7) — expected until US4 lands |
| `Unimplemented distribute` | module linking gap (G1) — expected until US1 lands |

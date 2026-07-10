# Quickstart: 050-full-gleam-combined

## Toolchain (Windows host)

- OTP 29 (`C:\Program Files\Erlang OTP\bin`), Gleam 1.17.0, rebar3 3.27 (`D:\tools\rebar3`) — all on user PATH (fresh shells resolve them).
- Lean 4 + Lake for the proof projects.
- WSL for: `gleam test` (gleeunit Windows path bug), QUIC/Profile-C runtime (049 ruling).

## Build

```bash
cd glp_gleam && gleam build --target erlang        # native Windows OK
```

## Unit tests (WSL)

```bash
cd /mnt/d/bstdev/research/glp/glpnet/glp_gleam && gleam test
```

## Run the Gleam REPL (once built, M1)

```bash
cd glp_gleam && gleam run                          # REPL: load programs/..., goal., :trace, :limit, :quit
echo -e 'load ../programs/tests/typed/append_dl.glp\nappend_dl_test.' | gleam run
```

## Corpus parity (M1 LOCK)

```bash
bash test/parity/record_dart_goldens.sh            # refresh Dart goldens + reference timings (explicit only)
bash test/parity/run_gleam_corpus.sh               # Gleam run + diff vs goldens + 10x wall-clock check
bash test/parity/run_differential.sh programs/tests/typed/append_dl.glp 'append_dl_test.'   # 3-runtime diff
```

## Reference-suite regression guard (before checkpoints touching shared files)

```bash
bash test/run_all_tests.sh                         # Dart REPL suite (repo root; delete glp_runtime/.dart_tool/repl.dill if stale)
bash test/link/run_link_tests_cross.sh             # Dart<->C# 16/16 rig (needs built C# REPL under out/csharp/)
```

## Cross-runtime capstone (M2)

```bash
bash test/link/run_link_tests_cross_gleam.sh       # C#<->Gleam 8 scenarios x 2 directions = 16/16 (TCP native; QUIC-WS under WSL)
```

## Proofs

```bash
cd glp_gleam/lean/WriterMguBindsOnlyWriters && lake build   # PI:14 (gates M1)
cd glp_gleam/lean/DistDerefConvergence && lake build        # PI:17 (gates M2)
```

## Gotchas

- `gleam test` on native Windows crashes in gleeunit discovery — always WSL.
- Stale REPL kernel snapshot `glp_runtime/.dart_tool/repl.dill` breaks the Dart suite — delete and re-run.
- QUIC (quicer/MsQuic) runtime is WSL-only; builds may use `gleam_quic/profile_c/windows-msvc-cmake.patch` notes.
- Do not copy corpus programs — they live only in `programs/tests/` (single source of truth).

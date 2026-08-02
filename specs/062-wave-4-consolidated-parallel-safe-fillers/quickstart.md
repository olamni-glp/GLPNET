# Quickstart — validating Wave 4 slices

Each slice is independently verifiable. Baseline before, re-test after (DISCIPLINE §2.2).

## US1 — Depgraph tooling (Python / codeconv)
```
# baseline
cd codeconv && .venv/Scripts/python.exe -m pytest -q
# mark-and-recompute on a fixture; assert only the marked subgraph recomputed
# trends across two recorded runs; assert byte-identical on re-run of unchanged inputs
```

## US2 — Feasibility studies (documents)
Read each study under `specs/062-.../research/`; confirm it states a go/no-go recommendation
with named risks. Sign-off is per-study.

## US3 — Engine & transport (C#/.NET line, per R-3)
```
# multi-accept: connect >=2 clients to one endpoint; assert none dropped
# compiled-IL: compile IL on side A, send, execute on side B; assert result == local
# hardening: feed malformed IL / version-mismatch / mid-transfer failure; assert safe reject
# zmq base: sender->receiver round-trip test
# run the C#/engine suite; assert no regression vs baseline
```

## US4 — GLP multi-client control program
```
echo -e 'load programs/tests/typed/<control_program>.glp\n<goal>.' | dart run bin/glp_repl.dart
# assert: type-checks, compiles, runs to documented succeeded/suspended outcome
bash test/run_all_tests.sh   # regression case added; assert green vs baseline
```

## US5 — §1.14 language items (proposal-gated)
```
# 1. Confirm the written §1.14 proposal exists under specs/062-.../proposals/ and cites its
#    authoritative source (FCP file/section or sibling-GLP spec) and the 2026-07-29 approval.
# 2. Only then: implement in glp_runtime (extend _TentativeStruct/_ClauseVar, never remove).
# 3. Positive + negative regression:
bash test/run_all_tests.sh          # Sections A (runtime) / C (negative) updated
cd glp_runtime && dart test         # Dart unit coverage; assert no regression
```

## Wave close
Every item terminal (delivered / delivered-as-study); no item silently dropped (SC-008).
Ship via GitFlow; announce release cut to the fleet lead (ariellas) before `buildkit release`.

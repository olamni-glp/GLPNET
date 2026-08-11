<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Quickstart: SC-002 IL-parity bridge

Prerequisites (from REPORT §5): dotnet 10.0.301, `Antlr4.Runtime.Standard` 4.13.1, Java 17
(`~/java/jdk-17.0.19+10`) + vendored ANTLR 4.13.2 jar (only needed to regenerate the parser).

## Regenerate the parser (only if `Glp.g4` changes, e.g. the D5 `mod`-functor fix)

```
cd spike/antlr4-glp-grammar
java -jar antlr-4.13.2-complete.jar -Dlanguage=CSharp -visitor -o gen Glp.g4
```

## Build the bridge + parity harness

```
cd spike/antlr4-glp-grammar
dotnet build bridge/Bridge.csproj
dotnet build parity/Parity.csproj
```

## Run corpus IL parity (SC-001 / SC-002)

The corpus is referenced IN PLACE from `programs/` (single source of truth — `corpus/` holds only
`MANIFEST.md`/`CONSTRUCTS.md`, no `.glp`). SC-001 is the built-in 7-file set (no `--corpus`); SC-002
sweeps a `programs/` subtree with `--corpus <dir>` (recurses `*.glp`). Run from the spike root:

```
cd spike/antlr4-glp-grammar
dotnet run --project harness/Harness.csproj -- --parity                              # SC-001: 7-file corpus
dotnet run --project harness/Harness.csproj -- --parity --corpus programs/tests/typed   # SC-002 (also: programs/lib, programs/typed_book, programs/tests/dynamic_dispatch)
```

Each writes/updates a section in `../RESULTS.md` (upsert by title). Expected: a per-file
`MATCH`/`DIVERGE` table. Green = 100% MATCH (7/7 for SC-001) or every DIVERGE carries a documented
`cause` and un-caused divergences = 0 (SC-002). See RESULTS.md "Summary & bounded conditions" for the
BC-1 one-sided-reject class (prelude/import/native-dependent files).

## Run the bounded fuzz (SC-003)

```
cd spike/antlr4-glp-grammar
dotnet run --project harness/Harness.csproj -- --fuzz --budget 10000   # halts on first un-caused divergence
```

Expected: budget completes with zero un-caused divergences; any divergence prints the exact
reproducing input (captured under `fuzz-repro/`). The generator emits only NON-cyclic `=` guards
(DEC F3) — cyclic `=` overflows the production engine (BC-2 / F-069-1), a separate filed engine bug.

## Regression guard (FR-010 held ⇒ production baseline stays green)

```
export DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart
bash test/run_all_tests.sh        # expect the 546–547 baseline, unchanged
```

## Read the deliverables

- `spike/antlr4-glp-grammar/RESULTS.md` — parity result table + fuzz summary (SC-006).
- `spike/antlr4-glp-grammar/DECISION.md` — adopt / adopt-with-conditions / do-not-adopt, with cited
  evidence and bounded conditions (SC-004, FR-011).

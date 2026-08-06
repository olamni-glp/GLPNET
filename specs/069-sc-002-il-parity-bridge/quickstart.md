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

```
cd spike/antlr4-glp-grammar/harness
dotnet run -- --parity --corpus ../corpus     # writes/updates ../RESULTS.md
```

Expected: a per-file `MATCH`/`DIVERGE` table in `RESULTS.md`. Green = 100% MATCH (7/7 for SC-001;
full expanded corpus for SC-002) or every DIVERGE carries a documented `cause`.

## Run the bounded fuzz (SC-003)

```
cd spike/antlr4-glp-grammar/harness
dotnet run -- --fuzz --budget 10000           # halts on first un-caused divergence
```

Expected: budget completes with zero un-caused divergences; any divergence prints the exact
reproducing input.

## Regression guard (FR-010 held ⇒ production baseline stays green)

```
export DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart
bash test/run_all_tests.sh        # expect the 546–547 baseline, unchanged
```

## Read the deliverables

- `spike/antlr4-glp-grammar/RESULTS.md` — parity result table + fuzz summary (SC-006).
- `spike/antlr4-glp-grammar/DECISION.md` — adopt / adopt-with-conditions / do-not-adopt, with cited
  evidence and bounded conditions (SC-004, FR-011).

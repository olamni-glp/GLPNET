<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Quickstart: glp-runtime-consol

## Prerequisites

- Java 17: `/c/Users/smbuser/java/jdk-17.0.19+10/bin/java`
- `dotnet` 10.0.301 on PATH
- ANTLR4 complete jar (acquire into `spike/antlr4-glp-grammar/` — see research R1)
- Tests: `export DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart` before `bash test/run_all_tests.sh`

## Scope B — abandon dead-stub cleanup (do first, no gate)

```bash
# 1. Confirm the stub is dead (expect zero production callers)
grep -rn "AbandonOps\|AbandonWriter" out/csharp/ csharp/ | grep -v "runtime/abandon.cs"
# 2. Remove the dead stub
git rm out/csharp/lib/runtime/abandon.cs
# 3. Rebuild the C# engine solution to zero errors and re-run baselines
dotnet build <engine.sln>
export DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart; bash test/run_all_tests.sh
```

If step 1 finds a caller → STOP and report (Bug-Protocol); do not remove.

## Scope A — ANTLR4 grammar spike (STOP at the §1.14 gate)

```bash
cd spike/antlr4-glp-grammar
# 1. Author Glp.g4 from the token vocabulary (out/csharp/lib/compiler/token.cs) — faithful only.
# 2. Generate the C# parser front-end
java -jar antlr-4.13.2-complete.jar -Dlanguage=CSharp -o gen Glp.g4
# 3. Build the harness, parse the corpus, compute IL parity vs the hand-written parser
dotnet run --project harness -- --corpus corpus/ --report REPORT.md
# 4. Review REPORT.md — verdict + coverage (SC-001) + IL parity (SC-002)
```

**§1.14 STOP**: If authoring `Glp.g4` reveals that faithfully accepting the language requires a
change to the accepted syntax, STOP. Write an owner proposal (Gabi + Udi) per DISCIPLINE §1.14 /
Constitution IV-a. Do not change what the language accepts before approval.

## Done-when

- `REPORT.md` states a go/no-go verdict; coverage + IL-parity recorded (SC-001/002/003).
- `abandon.cs` removed; C# builds green; all pre-existing baselines green (SC-005/006).
- No accepted-syntax change landed without a recorded §1.14 approval (SC-004).

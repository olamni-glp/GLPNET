# Contract: Corpus parity + differential harness (M1 LOCK)

## Golden recording protocol (FR-011, SC-001, SC-009)

- ONE recording location: `test/parity/goldens/` (single source of truth for recorded outputs; the inline assertions in `test/run_all_tests.sh` remain that suite's own expectations — they are not duplicated).
- `test/parity/record_dart_goldens.sh`: for each corpus case, run the Dart REPL (piped commands, `PYTHONUTF8`-safe, stdout-normalized: strip timing/prompt noise, normalize variable numbering if unstable) and record: outcome text + wall-clock. Re-recording is explicit (script rerun), never implicit.
- Normalization rules live in one place (a shared awk/sed lib sourced by both recorder and comparator) so Dart and Gleam outputs are normalized identically.

## Parity measurement

- The Gleam corpus runner executes the same case list through the Gleam REPL surface and diffs normalized output against the golden. Agreement = byte-equal after normalization.
- Declaration rule (FR-011): parity may not be declared until GAP-G1, GAP-G2, GAP-G3, GAP-G8, FORK-1 exist as named cases in `programs/tests/` (shared corpus home) and pass.
- Divergence protocol: STOP; report three-way (golden, Gleam output, spec anchor); classify per Bug Protocol — Gleam port bug (fix port) / Dart bug (report, do NOT mirror the bug) / spec gap (escalate). Never adjust a golden to make Gleam pass.

## Performance bound (SC-009)

- The recording run captures Dart wall-clock per case; the Gleam run captures its own; the comparator asserts `sum(gleam) <= 10 × sum(dart)` (suite-level, not per-case, to tolerate per-case noise). Report both sums in the runner summary.

## Differential harness (FR-012, closes MISS-04)

- `test/parity/run_differential.sh <program.glp> <goal>`: runs the same program+goal on Dart, C#, and Gleam REPLs, normalizes, and prints agree/diverge with a three-column diff on divergence. Exit code = number of divergent pairs.
- Used ad hoc during the port and wired into the acceptance run for the corpus subset tagged cross-runtime.

## Regression guard (SC-007)

- Existing suites remain green: `bash test/run_all_tests.sh` (Dart), C# suites (`csharp/*/tests`, link rig), before each marathon checkpoint that touches shared files (`programs/tests/`, `test/`).

# 076 — Test baseline (T001)

Per CLAUDE.md Test Protocol / DISCIPLINE §2.2: known-good baseline recorded BEFORE any
change to the type checker.

## Pre-change baseline — 2026-08-12

**Command** (from repo root):

```
DART=/d/BSTDEV/tools/dart-sdk/bin/dart.exe bash test/run_all_tests.sh
```

**Result**: `Total: 547 | Passed: 547 | Failed: 0` — ALL TESTS PASSED (exit 0).

Recorded at commit `5c22ac7c` (the §1.14 approval + authoritative-spec amendment; no
checker code changed yet).

Notes:
- Section S (`ms_message` durable mesh messaging) SKIPPED — venv absent; it is a
  standalone gate (`ms_message/tests/drill_disconnect.py`), not a regression.
- Section I (cross-runtime Gleam × C#) passed: US5 round_trip 12/12, mismatch 2/2.
- `glp_gleam/build/` is single-OTP (CLAUDE.md): the WSJF/Windows Section I harness owns it
  after this run. Re-running the WSL `gleam test` suite requires `rm -rf glp_gleam/build`
  first — a beam-load error there is NOT a code regression.

## Pre-change baseline re-confirmed — 2026-08-14 (this session, before any edit)

Re-run at `daabe346` in the new session the marathon required, before touching the
checker:

- REPL suite: `Total: 547 | Passed: 547 | Failed: 0` — ALL TESTS PASSED (exit 0).
  Section A 221, Section B 110, Section C 50. **Section I link_both_ways: PASS=4 FAIL=0.**
- Dart unit tests (`cd glp_runtime && dart test`): **441 passed / 5 skipped / 5 failed**.
  The 5 failures are pre-existing and unrelated to the type checker:
  - `test/compiler/partial_evaluator_test.dart` — guard validation ×2
  - `test/module/module_hierarchy_test.dart` — self.glp chain discovery ×3

## Post-change verification (T014)

Run at the same host, same commands.

- REPL suite: `Total: 550 | Passed: 549 | Failed: 1`.
  - **Section A 221/221** (= baseline)
  - **Section B 112/112** (= baseline 110 + `issue4_bind_later` + `head_flip_general`)
  - **Section C 51/51** (= baseline 50 + `head_flip_negative`)
  - Sections D–H, J–S: unchanged.
  - **Section I cross-runtime Gleam × C#**: `link_both_ways` PASS=2 FAIL=2
    (`pc_integers [C→G]`, `bidirectional [C→G]`). `round_trip` 12/12 and `mismatch` 2/2
    still pass.
- New unit tests: `test/analysis/type_checker/body_atom_licensing_test.dart` — **19/19 pass.**

### The one failure is NOT attributable to this feature

Stated as fact with the evidence, not as an assumption:

1. **The failing harness contains no Dart.** `test/parity/cross_runtime/link_both_ways.sh`
   drives exactly two processes — `gleam run` from `glp_gleam/` and the C# REPL at
   `out/csharp/glp_repl/bin/Debug/net10.0/glp_repl.exe` (`lib.sh:24-25,44-46`). The Dart
   type checker this feature changes is never invoked on that path.
2. **Neither runtime artifact changed.** `glp_repl.exe` and `glp_gleam/build/` both carry
   mtime 2026-08-06, i.e. unchanged since long before this session.
3. **It reproduces in isolation**, so it is not a one-off flake:
   `bash test/parity/cross_runtime/link_both_ways.sh` → PASS=2 FAIL=2, exit 2.
4. The failure surface is transport, not semantics: the C# consumer reports
   `System.IO.IOException: ... An established connection was aborted by the software in
   your host machine` and `Got = []`. Only the C→G direction fails; G→C passes.

What changed between the green baseline and the failure was **host state, not code**: two
unified-suite runs went concurrent (a killed background run orphaned its process tree),
Git-Bash hit `fork: Resource temporarily unavailable` / `0xC0000142`, and the orphaned
trees were then force-killed. No stray `glp_repl.exe`, `beam`, `erl`, `gleam` or `dotnet`
process survives, so the residue is not a live listener.

**Per CLAUDE.md Bug Protocol this is reported, not worked around.** SC-002 ("zero
regressions relative to the pre-change baseline") is therefore **not signed off** here:
every type-checker-relevant section is green and at the expected counts, but the suite as
a whole is 549/550 and the cross-runtime link defect is open and needs a decision.

## ✅ SC-002 SIGNED — 2026-08-16T20:25Z (T014 complete)

**The C→G failure was host-state contamination. It does not reproduce on a clean host.**

Isolated re-run first, on a host verified free of `glp_repl` / `beam` / `erl` / `gleam` /
`dotnet` processes and with no concurrent suite:

```
bash test/parity/cross_runtime/link_both_ways.sh
  PASS: pc_integers   [G→C]      PASS: pc_integers   [C→G]
  PASS: bidirectional [G→C]      PASS: bidirectional [C→G]
  US5 link_both_ways: PASS=4 FAIL=0        EXIT=0
```

Then the **full** suite (the isolated script proves attribution; only the full suite signs
SC-002):

```
DART=/d/BSTDEV/tools/dart-sdk/bin/dart.exe bash test/run_all_tests.sh
  Section A: 221 passed, 0 failed        (= baseline 221)
  Section B: 112 passed, 0 failed        (= baseline 110 + issue4_bind_later + head_flip_general)
  Section C:  51 passed, 0 failed        (= baseline  50 + head_flip_negative)
  Section I: link_both_ways 4/4 · round_trip 12/12 · mismatch 2/2
  Total: 550 | Passed: 550 | Failed: 0   ALL TESTS PASSED!   (0 FAIL lines)
```

Dart unit tests (T014 requires **both** suites):

```
glp_runtime> dart test   →  460 passed / 5 skipped / 5 failed
  baseline was            →  441 passed / 5 skipped / 5 failed
  441 + 19 (new body_atom_licensing tests) = 460  ✓
```

The 5 failures are **identical to baseline, verified by name** — not merely equal in count:

- `test/compiler/partial_evaluator_test.dart` — guard validation ×2
- `test/module/module_hierarchy_test.dart` — self.glp chain discovery ×3

**⇒ Zero regressions on both suites. SC-002 SIGNED.**

### Why the earlier run was red — root cause, for the record

Two unified-suite runs went concurrent (a killed background run orphaned its process tree);
Git-Bash hit `fork: Resource temporarily unavailable` / `0xC0000142`; the orphaned trees were
force-killed. The residue broke the C→G socket only. **Nothing in the code changed between the
red and green runs** — `glp_repl.exe` and `glp_gleam/build/` still carry mtime 2026-08-06.

**Standing rule confirmed by this episode: never start a second suite run while one is live,
and never kill a running suite** — an orphaned tree is exactly what produces this failure.

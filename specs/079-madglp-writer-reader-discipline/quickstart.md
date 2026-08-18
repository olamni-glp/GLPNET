# Quickstart — 079 audit + verify

Env: `export DART=C:/src/flutter/bin/cache/dart-sdk/bin/dart.exe`

## Baseline (before any change — FR-003)
cd glp_runtime && "$DART" test test/multiagent/    # record pass count
bash test/run_all_tests.sh                          # REPL suite (Section T abort = known 064, orthogonal)

## Audit R-1 (read-only first)
grep -n "pairedReaderAddr" glp_runtime/lib/bytecode/runner.dart   # classify each call site bound/unbound
# heap_fcp.dart: readerForWriter :199 (null on bound, Case 3), pairedReaderAddr :236 (+1 at :242)

## Verify (after)
cd glp_runtime && "$DART" test test/multiagent/    # == baseline (SC-002)
# fault-injection: break a cross-pointer -> pairedReaderAddr must fail loud, not return +1 (SC-001)

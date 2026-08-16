# Quickstart: Type-checker body-atom moding (076)

## Reproduce Issue 4 (pre-fix)

From repo root, one-liner (scripted REPL use — `echo -e`, never heredoc):

```
echo -e 'load programs/tests/typed/issue4_bind_later.glp\n:quit' | dart run glp_runtime/bin/glp_repl.dart
```


> **Host note (ariellas, verified 2026-08-16):** `dart` is not on PATH here. Prefix the
> repro and unit-test commands with the SDK path, e.g.
> `export PATH="/d/BSTDEV/tools/dart-sdk/bin:$PATH"` — see CLAUDE.md. The suite command
> already carries `DART=...` for this reason.

(The test program is created by the implement stage; until then, any file containing
`procedure bind_later(_).` and `bind_later(Done?) :- wait(1000) | Done = done.`
reproduces: "Variable mode mismatch: writer requires ↑ (produce), got ↓ (consume)".)

## Baseline (MANDATORY before any change)

```
DART=/d/BSTDEV/tools/dart-sdk/bin/dart.exe bash test/run_all_tests.sh
```

Confirm green, then checkpoint commit (stage by name, single-line message). If the
suite fails unexpectedly first check: stale `glp_runtime/.dart_tool/repl.dill` (delete
it), wrong working directory (must be repo root).

Dart unit tests, separately:

```
cd glp_runtime && dart test
```

## Gate check (before implementing)

The §1.14 approval must be recorded (spec.md Clarifications entry + marathon trace row
on `mrun-d086da8a860f`). Verify the marathon item:

```
buildkit-marathon status --feature type-checker-body-atom-moding-accept-head-flipped-readers-unblock-2
```

The `1.14-ruling` discharge item must be satisfied before `/bk-implement` proceeds.

## Verify (after the change)

1. Re-run both suites (commands above) — zero regressions vs baseline.
2. P1/P2 load and run; N1 fails type-check with the row-4 diagnostic
   (contracts/body-atom-moding-rule.md).
3. `docs/known-issues.md` Issue 4 closed with the stale prelude claim corrected
   (declarations live in `programs/self.glp`; built-in type prelude empty).
4. Commit (files by name), push; ship later via `buildkit ship --skip-preflight`.

# Quickstart: 014-package-self-import-resolution

End-to-end smoke for the feature. Assumes feature 012's prerequisites are satisfied (PGLite bridge installed, codeconv installed, `--data-dir` override understood; see `specs/012-codeconv-runner/quickstart.md` Flow C).

This document adds **Flow G** to the existing flows (A–F) of feature 012. Flows A–F are NOT re-described here.

## Prerequisites (delta over feature 012)

- `glp_runtime_net/pubspec.yaml` exists and contains `name: glp_runtime` (verified live 2026-05-11).
- This feature's code change is merged into the working tree (Phase 4–5 of `tasks.md` complete).
- `pytest codeconv/tests/ -k 'parse or pubspec or self_package or outside_subtree' --test-concurrency=1` is green.

## Flow G — self-package resolution end-to-end (US1, US2, SC-001 through SC-007)

```powershell
# 1. Baseline — record the isolated count BEFORE this feature lands (read existing tombstones).
#    Skip this step if the feature is already merged; this is for the merge-day verification only.
codeconv discover --data-dir .pgdb-tmp-pre --root glp_runtime_net --json `
  | Tee-Object pre.json
$pre = Get-Content pre.json | ConvertFrom-Json
"BEFORE: edges=$($pre.imports), isolated=70 (per /docs/future/codeconv-discover-package-self-import-resolution.md)"

# 2. Refresh after the feature: TRUNCATE inventory then re-discover (workflow_contract.md § Idempotence).
#    This is the one-time SC-007 refresh.
codeconv doctor --truncate-codeconv          # see note below if the flag does not exist; manual psql works
codeconv discover --data-dir .pgdb --root glp_runtime_net --json `
  | Tee-Object post.json
$post = Get-Content post.json | ConvertFrom-Json
"AFTER: files_processed=$($post.files_processed), imports=$($post.imports)"

# 3. SC-001 — verify isolated count drops below 20.
$tombs = Get-ChildItem .codeconv\tombstones -Recurse -Filter *.dart.md
$isolated = 0
foreach ($t in $tombs) {
    $text = Get-Content $t.FullName -Raw
    $parts = $text -split '---', 3
    if ($parts.Length -lt 3) { continue }
    $fm = $parts[1] | ConvertFrom-Yaml   # Use ConvertFrom-Yaml from powershell-yaml or fallback to Python one-liner
    if (($fm.dependencies.Count -eq 0) -and ($fm.callers.Count -eq 0)) {
        $isolated += 1
    }
}
"isolated=$isolated  (PASS if < 20)"

# 4. SC-002 — verify heap_fcp.dart tombstone has the four expected dependencies.
$tomb = Get-Content '.codeconv\tombstones\lib\runtime\heap_fcp.dart.md' -Raw
$tomb -match '(?s)^---\n(.+?)\n---' | Out-Null
$fm = $matches[1] | ConvertFrom-Yaml
$expected = @(
    'lib/multiagent/variable_table.dart',
    'lib/runtime/machine_state.dart',
    'lib/runtime/suspension.dart',
    'lib/runtime/terms.dart'
)
$actual = ($fm.dependencies | Sort-Object)
"deps match: $((Compare-Object $expected $actual).Count -eq 0)"

# 5. SC-004 — idempotence: a second discover produces zero diff.
git diff --quiet .codeconv/tombstones; "tomb diff after step 2: $LASTEXITCODE (0 = clean, 1 = dirty — expect dirty here, this is the refresh)"
codeconv discover --data-dir .pgdb --root glp_runtime_net --json | Out-Null
git diff --quiet .codeconv/tombstones; "tomb diff after second run: $LASTEXITCODE (expect 0 — idempotent)"

# 6. SC-005 — missing-pubspec fallback. Rename pubspec, run, verify warning + isolated graph.
Move-Item glp_runtime_net\pubspec.yaml glp_runtime_net\pubspec.yaml.bak
codeconv doctor --truncate-codeconv
$summary = codeconv discover --data-dir .pgdb --root glp_runtime_net --json | ConvertFrom-Json
$pmw = $summary.warnings | Where-Object { $_.kind -eq 'pubspec_missing' }
"pubspec_missing count: $($pmw.Count)  (PASS if == 1)"
"reason: $($pmw[0].reason)            (PASS if == 'absent')"
"path:   $($pmw[0].path)              (PASS if 'glp_runtime_net/pubspec.yaml')"
Move-Item glp_runtime_net\pubspec.yaml.bak glp_runtime_net\pubspec.yaml
codeconv doctor --truncate-codeconv
codeconv discover --data-dir .pgdb --root glp_runtime_net | Out-Null   # restore good state

# 7. SC-006 — performance: discover stays within the 60 s / 5 s budgets.
Measure-Command { codeconv doctor --truncate-codeconv; codeconv discover --data-dir .pgdb --root glp_runtime_net } | Format-List TotalSeconds
"PASS if TotalSeconds < 60"
Measure-Command { codeconv discover --data-dir .pgdb --root glp_runtime_net } | Format-List TotalSeconds
"PASS if TotalSeconds < 5"

# 8. SC-007 — single tombstone-refresh commit. Stage the .codeconv/tombstones/ diff alongside the code change.
git status .codeconv/tombstones | Select-Object -First 20
git add .codeconv/tombstones
git commit -m "Refresh tombstones after feature 014 self-package rewrite (SC-007)"
```

## Notes on the helper invocations

- `codeconv doctor --truncate-codeconv` is referenced as a convenience for the SC-007 refresh recipe. If the flag is not implemented (verify with `codeconv doctor --help`), use the manual psql equivalent:
  ```powershell
  $port = (Get-Content .pgdb\bridge.json | ConvertFrom-Json).port
  & psql "host=127.0.0.1 port=$port dbname=postgres user=postgres password=postgres" `
    -c "TRUNCATE codeconv.dart_files, codeconv.dart_imports, codeconv.dart_callers, codeconv.dart_files_orphaned;"
  ```
  Adding the flag to `codeconv doctor` is OUT OF SCOPE for this feature. If we choose to add it as a convenience, that's a small extra task; do not block the feature on it.
- `ConvertFrom-Yaml` requires `Install-Module powershell-yaml -Scope CurrentUser`. If unavailable, replace with a one-line Python invocation:
  ```powershell
  $deps = python -c "import yaml,sys; print('\n'.join(yaml.safe_load(open(sys.argv[1])).get('dependencies',[])))" .codeconv\tombstones\lib\runtime\heap_fcp.dart.md
  ```

## Negative-control verification (US1 acceptance scenario 2)

```powershell
# Verify external package imports continue to be silently skipped (no warning, no edge).
$tomb = Get-Content '.codeconv\tombstones\lib\runtime\heap_fcp.dart.md' -Raw
$tomb -match '(?s)^---\n(.+?)\n---' | Out-Null
$fm = $matches[1] | ConvertFrom-Yaml
foreach ($dep in $fm.dependencies) {
    if ($dep -like 'package:*' -or $dep -like 'dart:*') {
        Write-Error "FAIL: external import '$dep' surfaced as in-subtree edge"
    }
}
"PASS — all dependencies are POSIX in-subtree paths (no package:/dart: leakage)"
```

## What this quickstart does NOT exercise

- Pub workspace cross-package resolution (out of scope; spec line 92).
- The `--from-tombstones` mode (unaffected by this feature; covered by feature 012's Flow D).
- Bridge lifecycle (unaffected; covered by feature 012's Flow A).
- D2NET migration (unaffected; covered by feature 012's Flow B).

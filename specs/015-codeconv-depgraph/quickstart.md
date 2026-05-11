# Quickstart: 015-codeconv-depgraph

End-to-end smoke for the feature. Assumes features 012 and 014 are merged (PGLite bridge installed, codeconv installed, `--data-dir` override understood, `/codeconv-discover` produces 128 files / 443 in-subtree edges / 6 isolated). See `specs/012-codeconv-runner/quickstart.md` Flow C and `specs/014-package-self-import-resolution/quickstart.md` Flow G if those preconditions are not yet met.

This document adds **Flow H** (depgraph compute + mark + stamp + rebuild end-to-end) to the existing flows A–G of features 012/014. Flows A–G are NOT re-described here.

## Prerequisites (delta over features 012 + 014)

- `/codeconv-discover` has run at least once and `codeconv.dart_files` is non-empty (otherwise SC FR-010 fires — see step 1.5 below).
- This feature's code change is merged into the working tree (Phases 1–4 of `tasks.md` complete).
- Alembic revision `0002_dart_depgraph` has been applied (`codeconv migrate` exits 0).
- `pytest codeconv/tests/test_depgraph_algorithm.py codeconv/tests/test_depgraph_compute.py codeconv/tests/test_depgraph_mark.py codeconv/tests/test_depgraph_stamp.py codeconv/tests/test_depgraph_rebuild_conversions.py codeconv/tests/test_depgraph_cycle_fixture.py codeconv/tests/test_depgraph_idempotence.py codeconv/tests/test_depgraph_schema_isolation.py --test-concurrency=1` is green.

## Flow H — depgraph end-to-end (US1, US2, US3, SC-001 through SC-008)

```powershell
# 0. Baseline — confirm discover state.
$tot = & psql "host=127.0.0.1 port=$((Get-Content .pgdb\bridge.json | ConvertFrom-Json).port) dbname=postgres user=postgres password=postgres" `
  -t -A -c "SELECT count(*) FROM codeconv.dart_files"
"Inventory files: $tot  (expect 128 on current baseline)"

# 1. SC-001 — compute the depgraph, warm bridge, < 5 s.
Measure-Command { codeconv depgraph compute --data-dir .pgdb } | Format-List TotalSeconds
"PASS if TotalSeconds < 5"

# 1.5 SC-FR010 — empty-inventory error path.
codeconv doctor --truncate-codeconv   # see feature 014 quickstart for the fallback recipe
$exit = & codeconv depgraph compute --data-dir .pgdb; "exit=$LASTEXITCODE"
"PASS if exit != 0 and stderr mentions /codeconv-discover"
codeconv discover --data-dir .pgdb --root glp_runtime_net | Out-Null   # restore state

# 2. SC-005 — read the ready array from the JSON artefact (top-level, lexically sorted).
Get-Content .codeconv\depgraph.json | ConvertFrom-Json | Select-Object -ExpandProperty ready | Select-Object -First 6
"PASS if at least 6 paths appear (the 6 isolated files from feature 014 baseline)"

# 3. SC-FR001 (cross-check) — JSON `ready` set equals SQL-derived ready set.
$jsonReady = (Get-Content .codeconv\depgraph.json | ConvertFrom-Json).ready
$sqlReady = & psql "host=127.0.0.1 port=$((Get-Content .pgdb\bridge.json | ConvertFrom-Json).port) dbname=postgres user=postgres password=postgres" `
  -t -A -c "SELECT path FROM codeconv.dart_depgraph WHERE ready ORDER BY path"
"PASS if (Compare-Object \$jsonReady (\$sqlReady -split \"`n\")).Count -eq 0"

# 4. SC-003 — edge invariant: for every (A→B) in dart_imports,
#           topo_level(A) > topo_level(B) OR cycle_group_id(A) = cycle_group_id(B).
$bad = & psql "host=127.0.0.1 port=$((Get-Content .pgdb\bridge.json | ConvertFrom-Json).port) dbname=postgres user=postgres password=postgres" `
  -t -A -c "SELECT count(*) FROM codeconv.dart_imports i JOIN codeconv.dart_depgraph a ON a.path=i.from_path JOIN codeconv.dart_depgraph b ON b.path=i.to_path WHERE NOT (a.topo_level > b.topo_level OR a.cycle_group_id = b.cycle_group_id)"
"PASS if bad == 0  (got $bad)"

# 5. US1 acceptance #1 — isolated files appear at topo_level=0 with ready=true.
$lvl0 = & psql "host=127.0.0.1 port=$((Get-Content .pgdb\bridge.json | ConvertFrom-Json).port) dbname=postgres user=postgres password=postgres" `
  -t -A -c "SELECT count(*) FROM codeconv.dart_depgraph WHERE topo_level=0 AND ready"
"PASS if lvl0 >= 6  (got $lvl0)"

# 6. US2 — advance the frontier. Pick any topo_level=0 file and walk it through the lifecycle.
$target = ($jsonReady | Select-Object -First 1)
"Marking started: $target"
codeconv depgraph mark-started $target
codeconv depgraph compute --data-dir .pgdb | Out-Null
$statusAfterStart = (Get-Content .codeconv\depgraph.json | ConvertFrom-Json).files | Where-Object { $_.path -eq $target } | Select-Object -ExpandProperty status
"PASS if status='in_progress'  (got $statusAfterStart)"

"Marking completed: $target"
codeconv depgraph mark-completed $target --target "csharp/$target.cs"
codeconv depgraph compute --data-dir .pgdb | Out-Null
$statusAfterComplete = (Get-Content .codeconv\depgraph.json | ConvertFrom-Json).files | Where-Object { $_.path -eq $target } | Select-Object -ExpandProperty status
"PASS if status='converted'  (got $statusAfterComplete)"

# 7. SC-002 — idempotence: a second compute on unchanged state produces zero diff.
$json1 = Get-Content .codeconv\depgraph.json -Raw
codeconv depgraph compute --data-dir .pgdb | Out-Null
$json2 = Get-Content .codeconv\depgraph.json -Raw
# Strip the `generated_at` line before comparing.
$strip = { $args[0] -replace '"generated_at":\s*"[^"]+"', '"generated_at":""' }
"PASS if (&\$strip \$json1) == (&\$strip \$json2)  ($(((&\$strip \$json1) -eq (&\$strip \$json2))))"

# 8. SC-008 — dry-run produces no changes.
$pre = (Get-Item .codeconv\depgraph.json).LastWriteTime
$preRows = & psql "host=127.0.0.1 port=$((Get-Content .pgdb\bridge.json | ConvertFrom-Json).port) dbname=postgres user=postgres password=postgres" `
  -t -A -c "SELECT count(*) FROM codeconv.dart_depgraph"
codeconv depgraph compute --data-dir .pgdb --dry-run | Out-Null
$post = (Get-Item .codeconv\depgraph.json).LastWriteTime
$postRows = & psql "host=127.0.0.1 port=$((Get-Content .pgdb\bridge.json | ConvertFrom-Json).port) dbname=postgres user=postgres password=postgres" `
  -t -A -c "SELECT count(*) FROM codeconv.dart_depgraph"
"PASS if (\$pre -eq \$post) and (\$preRows -eq \$postRows)"

# 9. FR-014 — stamp tombstones with depgraph + conversion state.
codeconv depgraph stamp-tombstones --data-dir .pgdb
$tomb = Get-Content ".codeconv\tombstones\$target.md" -Raw
"PASS if tombstone contains 'status: converted'  ($($tomb -match 'status:\s*converted'))"
"PASS if tombstone contains conversion_completed_at  ($($tomb -match 'conversion_completed_at:'))"

# 10. FR-006a round-trip — wipe dart_conversions, rebuild from tombstones, recompute, status should be 'converted' again.
& psql "host=127.0.0.1 port=$((Get-Content .pgdb\bridge.json | ConvertFrom-Json).port) dbname=postgres user=postgres password=postgres" `
  -c "TRUNCATE codeconv.dart_conversions" | Out-Null
codeconv depgraph rebuild-conversions-from-tombstones --data-dir .pgdb
codeconv depgraph compute --data-dir .pgdb | Out-Null
$rebuiltStatus = (Get-Content .codeconv\depgraph.json | ConvertFrom-Json).files | Where-Object { $_.path -eq $target } | Select-Object -ExpandProperty status
"PASS if status='converted'  (got $rebuiltStatus)"

# 11. SC-007 — schema isolation: no new objects in public or dbos.
$publicTables = & psql "host=127.0.0.1 port=$((Get-Content .pgdb\bridge.json | ConvertFrom-Json).port) dbname=postgres user=postgres password=postgres" `
  -t -A -c "SELECT table_name FROM information_schema.tables WHERE table_schema='public' ORDER BY table_name"
"public tables: $publicTables  (PASS if unchanged from pre-feature snapshot — likely D2NET's 5 tables)"
```

## US3 — 3-file cycle fixture (SC-006)

```powershell
# Construct a synthetic fixture inventory in an isolated data dir.
$tmp = ".pgdb-cycle-fixture"
codeconv discover --data-dir $tmp --root specs/015-codeconv-depgraph/scripts/cycle_fixture
codeconv depgraph compute --data-dir $tmp
$json = Get-Content "$tmp\..\..\.codeconv\depgraph.json" | ConvertFrom-Json
"cycle_count: $($json.cycle_count)  (PASS if == 1)"
$cycleMembers = $json.files | Where-Object { $_.cycle_group_id -eq ($json.files | Where-Object path -EQ 'A.dart').cycle_group_id }
"cycle size: $($cycleMembers.Count)  (PASS if == 3)"
"shared topo_level: $($cycleMembers | Select-Object -ExpandProperty topo_level -Unique).Count  (PASS if == 1)"
```

(The cycle fixture under `specs/015-codeconv-depgraph/scripts/cycle_fixture/` is created as part of the tasks.md implementation; it contains three minimal `.dart` files that import each other in a 3-cycle.)

## Negative controls

```powershell
# mark-completed on a never-started row → error.
$exit = & codeconv depgraph mark-completed nonexistent/path.dart; "exit=$LASTEXITCODE"
"PASS if exit != 0"

# mark-started on already-started → warning + no-op.
codeconv depgraph mark-started $target 2>&1 | Tee-Object -Variable out
"PASS if out contains 'already started'  ($($out -match 'already started'))"

# rebuild-conversions-from-tombstones when no tombstones contain conversion keys → empty result, no error.
git stash push -m "tmp" -- .codeconv/tombstones
codeconv depgraph rebuild-conversions-from-tombstones --data-dir .pgdb
$rows = & psql "host=127.0.0.1 port=$((Get-Content .pgdb\bridge.json | ConvertFrom-Json).port) dbname=postgres user=postgres password=postgres" `
  -t -A -c "SELECT count(*) FROM codeconv.dart_conversions"
"PASS if rows == 0  (got $rows)"
git stash pop
```

## What this quickstart does NOT exercise

- Bridge lifecycle (unaffected; covered by feature 012's Flow A).
- D2NET migration (unaffected; covered by feature 012's Flow B).
- Self-package edge resolution (unaffected; covered by feature 014's Flow G).
- Multi-file SCCs in the production inventory — if the live `glp_runtime_net/` happens to contain a real cycle, step 7 of the cycle fixture above is exercised against the real data instead of the synthetic. Either is acceptable evidence for SC-006.

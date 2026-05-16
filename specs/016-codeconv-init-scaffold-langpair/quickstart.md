# Quickstart: codeconv init + scaffold (Dart→C#)

Prereqs: codeconv venv (`codeconv/.venv`), Node ≥20 (bridge), feature-015 merged/available (tombstone `target_path`). On this exFAT checkout **always pass `--data-dir C:/pglite/research/glpnet`** (D: is exFAT; the guard hard-fails otherwise).

`PY=codeconv/.venv/Scripts/python.exe` ; `DD=C:/pglite/research/glpnet`.

## Flow I — full Dart→C# pipeline

```
0. $PY -m codeconv.cli --data-dir $DD migrate          # applies 0001+0002+0003
1. $PY -m codeconv.cli --data-dir $DD init --source glp_runtime_net --target out/csharp \
       --source-lang dart --target-lang csharp --accept-suggested-exclusions --non-interactive
   # → workspace_settings pair=dart/csharp; excluded_directories seeded; phase_* seeded;
   #   discover delegated → codeconv.dart_files populated (~128). exit 0.
2. $PY -m codeconv.cli --data-dir $DD depgraph compute  # ordering + readiness (feature 015)
3. $PY -m codeconv.cli --data-dir $DD scaffold --json    # mirrors source→out/csharp/*.cs + __<base>/ workdirs
   # → target tree produced via staging+atomic move; each tombstone gains target_path;
   #   phase_status['scaffold']=COMPLETE. exit 0.
4. $PY -m codeconv.cli --data-dir $DD depgraph stamp-tombstones   # optional: embed full state
```

### Verify (success criteria)

- **SC-001** clean→scaffolded with only init+scaffold (+discover/depgraph) — no manual DB/FS steps.
- **SC-002** re-run step 1 and step 3 ⇒ zero diff (workspace tables, inventory, tombstones, target tree).
- **SC-007** every in-scope source file's tombstone `target_path` == its `out/csharp/...cs` path:
  `read_tombstone(.codeconv/tombstones/<rel>.dart.md)['target_path']`.
- **SC-008** pair mismatch refuses: `scaffold --source-lang dart --target-lang rust` ⇒ non-zero, no output.
- **schema isolation** `\dt codeconv.*` gains exactly `workspace_settings, excluded_directories, phase_sequence, phase_status`; `\dn`, `public`, `dbos` unchanged vs pre-016.
- **SC-005** existing discover/depgraph suites stay green.

## Flow II — exclusion management (US4)

```
$PY -m codeconv.cli --data-dir $DD init add-exclude glp_runtime_net/lib/generated
# → files under it leave codeconv.dart_files (discover re-synced); exclusion persists.
$PY -m codeconv.cli --data-dir $DD init remove-exclude glp_runtime_net/lib/generated
# → those files return.
```

## Flow III — extensibility proof (US3 / SC-003)

Register a test-only pair in `test_langpair_registry.py`; assert `list_pairs()` shows it, `init --source-lang X --target-lang Y` binds it, and `git diff` touches **no** file under `codeconv/src/codeconv/tools/{init,scaffold,discover,depgraph}/`.

## Negative checks

- `init --source ../outside` → exit 2, no workspace state.
- `init --source-lang dart --target-lang rust` → exit 5, lists registered pairs, no state.
- `scaffold` before `init` → exit 2 ("run codeconv init first").
- `scaffold` into a non-empty target without `--force-delete-target` → exit 2 (refuse); with it → confirmation gate then atomic replace.
- Failure mid-scaffold ⇒ `<target>` untouched (staging dir discarded).

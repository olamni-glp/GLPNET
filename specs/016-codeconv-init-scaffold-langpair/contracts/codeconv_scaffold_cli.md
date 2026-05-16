# Contract: `codeconv scaffold` (and `/codeconv-scaffold`)

Source: spec FR-013..FR-019, FR-021, D4/D6. Conforms to the feature-012 tool contract. Tool subpackage: `codeconv/src/codeconv/tools/scaffold/`.

## Invocation

Slash `/codeconv-scaffold [args]` → CLI `codeconv scaffold [args]`. De-brand of `D2NET-scaffold`. Top-level flags propagate; `--data-dir C:/pglite/research/glpnet` on this checkout.

## Command tree

```text
codeconv scaffold [run]                   # default — produce the target tree
```

## Flags

| Flag | Default | Effect |
|---|---|---|
| `--force-delete-target` | off | destructive: overwrite a non-empty target — requires explicit confirmation |
| `--quiet` / `--json` | off | per top-level convention |
| `--no-tombstone-update` | off | skip writing `target_path` into tombstones (testing only) |

## Behaviour (FR-013..FR-018)

1. Acquire-or-discover the bridge (shared, FR-019).
2. `resolve_workspace_pair(engine)` — REQUIRES an initialised workspace. Unset/unknown/mismatch with any override ⇒ exit `2`/`5` actionable, **no output produced** (FR-018/SC-008).
3. Prerequisite check: `codeconv.dart_files` non-empty (inventory exists). Empty ⇒ exit `2` ("run codeconv init first").
4. Read the in-scope file set from `codeconv.dart_files`, minus `codeconv.excluded_directories` (FR-014). NOT from any `public.dart_files`.
5. Plan the target tree via the pair: for each in-scope source file, `target_for(source_rel)` (extension swap, mirrored dirs) and `workdir_name(source_rel)` (per-file working dir; `dart_csharp` → `__<base>`).
6. Stage: write the planned tree under `<target>.codeconv-scaffold-tmp/` (incl. an empty `.codeconv-scaffold-manifest` sentinel at the tree root). **"Non-empty target" here means non-empty AND not produced by a prior scaffold of this workspace** (no `.codeconv-scaffold-manifest` sentinel at its root — i.e. it holds foreign/operator content). Such an unmanaged non-empty target without `--force-delete-target` ⇒ exit `2` (refuse, live tree untouched); with `--force-delete-target` ⇒ require explicit confirmation (skill gate) then replace. A **managed** target (sentinel present, produced by a prior scaffold) is refreshed idempotently **without** `--force-delete-target` — this reconciliation is REQUIRED so the "idempotent no-op / identical target tree" guarantee (Exit codes row 0 / SC-002) holds (a re-scaffold's target is otherwise always non-empty and would be permanently un-rescaffoldable). D2Net.Scaffold `TargetTreePlanner.TargetIsManaged` parity. Atomically move staging → `<target>` (no half-written tree on failure — FR-017).
7. For each scaffolded source file, write the produced target rel-path into the **existing feature-015 tombstone `target_path`** via `codeconv.tools.depgraph.tombstone_writer` (canonical YAML writer; idempotent). Missing tombstone for a file ⇒ **warning**, continue (FR-015).
8. Upsert `codeconv.phase_status` (`phase='scaffold'`, `status` IN_PROGRESS→COMPLETE) and ensure `phase_sequence` has `scaffold` (FR-016). One transaction for the DB mutations.
9. Emit summary (human/`--json`), exit `0`.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | success (incl. idempotent no-op) |
| 1 | generic error |
| 2 | missing prerequisite (no workspace / empty inventory) or refused non-empty-target overwrite |
| 5 | unregistered / mismatched language pair (no output) |

## Idempotence (SC-002)

Re-`scaffold` on an unchanged inventory ⇒ identical target tree (no spurious churn), zero new `phase_*` duplication, zero tombstone diff (canonical writer + same `target_path` values). `--dry-run`-style safety is achieved via the staging dir (a failed run leaves `<target>` untouched).

## Slash wrapper (`/codeconv-scaffold` SKILL.md)

Mirrors `/codeconv-discover` PLUS `/D2NET-scaffold`'s destructive gate: prompt before `--force-delete-target`, drive any confirmation, cache by target@timestamp. CLI authoritative.

## Does NOT

Does NOT translate Dart→C# (produces the *skeleton/target tree*, not converted code — conversion is a future stage); does NOT create a `scaffold_tracker` table or any `public.*`; does NOT modify feature-012/014/015 tables or tombstone key set (only fills the existing `target_path`).

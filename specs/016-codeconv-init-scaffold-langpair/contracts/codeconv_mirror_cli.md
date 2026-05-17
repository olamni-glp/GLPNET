# Contract: `codeconv mirror` (and `/codeconv-mirror`)

Source: spec Amendment 1 (FR-027..FR-041, D7), reproducing spec `001-d2net-scaffold` FR-002..FR-014 generically via the language-pair registry. Conforms to the feature-012 tool contract. Tool subpackage: `codeconv/src/codeconv/tools/mirror/`.

## Invocation

Slash `/codeconv-mirror [args]` → CLI `codeconv mirror [args]`. Generic re-expression of the removed `D2NET-scaffold` (NOT a revival of it). Top-level flags propagate; `--data-dir C:/pglite/research/glpnet-016` on the GLPNET-016 worktree.

## Command tree

```text
codeconv mirror [run]                     # default — produce the inventory subtree
```

## Flags

| Flag | Default | Effect |
|---|---|---|
| `--refresh` | off | re-run against an existing output tree with spec-`001` FR-011 semantics (rewrite `.src`/non-source; preserve every companion + tracker; stub newly-found source files) — destructive-adjacent, skill-gated |
| `--quiet` / `--json` | off | per top-level convention |

No `--source-lang`/`--target-lang` overrides: the pair is resolved **solely** from the workspace (FR-028); pair selection is owned by `codeconv init` (D6/FR-004).

## Behaviour (FR-027..FR-037)

1. Acquire-or-discover the bridge (shared, FR-019) — one read-only lookup only.
2. Resolve the pair from `codeconv.workspace_settings` (`source_lang`/`target_lang`). Unset ⇒ exit `2` "run `codeconv init` first" (no output). Set-but-unregistered ⇒ exit `5` actionable (lists registered pairs), no output (FR-028 / D6 / FR-004 / FR-005).
3. Read workspace `mirror_source_root` (input — source-language tree root, e.g. `glp_runtime`) and workspace `source_path` (output root — the inventory subtree, e.g. `glp_runtime_net`). Output root == or nested-in input root ⇒ exit `2` refuse (FR-029 / spec-`001` FR-014). Missing `mirror_source_root` ⇒ exit `2` "run `codeconv init` first".
4. If the output root already exists and `--refresh` is NOT given ⇒ exit `2` refuse, leave it untouched (FR-035 / spec-`001` FR-011 default).
5. Pre-flight pass over the (non-pruned) source tree: detect every companion-name collision with a pre-existing non-source file in the same folder. Any collision ⇒ report the full list, exit `1`, write nothing (FR-036 / spec-`001` FR-012).
6. Stage under `<output>.codeconv-mirror-tmp/`: deterministic recursive walk pruning the pair's `mirror_prune_segments()`; non-source files copied byte-identical; each source file preserved as `<name><preserved_source_suffix()>` (for `dart_csharp` the suffix is `""` per FR-032 Option 1 — the source is mirrored verbatim as `.dart` so `discover` inventories it); per source file one stub per `companion_extensions()` containing `companion_stub_comment(ext, base)`; write the pair's `tracker_filename()` at the staged root with one record per source file (preserved-rel path + companions, status `todo`, enum `{todo,in-progress,done,blocked}`). `--refresh`: rewrite preserved-source/non-source from current source, add stubs for newly-found source files only, copy forward every pre-existing companion + the existing tracker byte-identical. Atomically move staging → output (no half-written tree on failure — FR-037).
7. Emit summary (human/`--json`): dirs created, non-source copied, source preserved, companions generated, tracker records, (refresh) newly-found source files. Exit `0`.

`mirror` makes **no** `codeconv`-schema writes and does **not** advance phase tracking (it precedes the workspace-state stages; phase tracking is owned by init/discover/scaffold).

## Exit codes

| Code | Meaning |
|---|---|
| 0 | success (incl. `--refresh`) |
| 1 | generic error (incl. companion collision pre-flight abort — nothing written) |
| 2 | missing prerequisite (no initialised workspace) / refused (output exists w/o `--refresh`, or output nested in source) |
| 5 | workspace pair is not a registered pair (no output) |

## Idempotence

Re-`mirror` without `--refresh` ⇒ zero-change refusal (exit 2). `--refresh` on an unchanged source ⇒ every companion file and the tracker byte-identical; `.src`/non-source files byte-identical to current source (spec-`001` SC-008/SC-009). A failed run leaves `<output>` untouched (staging dir).

## Slash wrapper (`/codeconv-mirror` SKILL.md)

Mirrors `/codeconv-scaffold`'s structure + destructive gate: confirm before `--refresh` (it rewrites the regenerable subtree). CLI authoritative; args forwarded verbatim.

## Does NOT

Does NOT translate Dart→C# (produces the inventory subtree skeleton — conversion is a later stage); does NOT create any DB table, `public.*`, tombstone, or phase row; does NOT modify feature-012/014/015 state or the US2 `scaffold` behaviour; does NOT revive the removed D2NET `d2net-scaffold` binary/skill.

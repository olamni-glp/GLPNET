# Contract: `codeconv init` (and `/codeconv-init`)

Source: spec FR-006..FR-012, FR-019, FR-021, D3/D6. Conforms to `specs/012-codeconv-runner/contracts/codeconv_tool_contract.md` (auto-discovered `app: typer.Typer` + optional `register_workflows`). Tool subpackage: `codeconv/src/codeconv/tools/init/`.

## Invocation

Slash `/codeconv-init [args]` → CLI `codeconv init [args]`. De-brand of `D2NET-init`. Top-level `codeconv` flags (`--repo-root`, `--data-dir`, `--quiet`, `--json`) propagate via `typer.Context`. On this exFAT checkout pass `--data-dir C:/pglite/research/glpnet`.

## Command tree

```text
codeconv init [run]                       # default — configure + delegate inventory to discover
codeconv init add-exclude <path>          # add a manual exclusion, re-sync inventory
codeconv init remove-exclude <path>       # remove an exclusion, re-sync inventory
codeconv init list                        # list in-scope inventoried files
codeconv init inspect [--exclusions|--current-phase]   # workspace introspection
```

## `run` flags

| Flag | Default | Effect |
|---|---|---|
| `--source <path>` | `glp_runtime_net` | source subtree (repo-relative); validated (exists, in-repo, not reserved) |
| `--target <path>` | (required for a usable workspace) | target tree root |
| `--source-lang <id>` | `dart` | source language id |
| `--target-lang <id>` | `csharp` | target language id |
| `--exclude <path>` (repeatable) | — | manual exclusions |
| `--accept-suggested-exclusions` | off | accept the pair's `tool_exclusion_globs()` recs non-interactively |
| `--non-interactive` | off | no prompts; requires the above or explicit excludes |
| `--rebuild` | off | destructive re-init (discards existing workspace state) — requires confirmation |
| `--quiet` / `--json` | off | per top-level convention |

## `run` behaviour (FR-006..FR-010, FR-012)

1. Resolve `(source_lang,target_lang)` against the registry. Unregistered → exit `5` actionable (names known pairs), **no writes** (FR-005).
2. Validate `--source`/`--target`: existence, inside repo, not a reserved name. Invalid → exit `2`, **no partial state** (FR-012).
3. Acquire-or-discover the bridge (`codeconv.bridge_client.acquire_or_discover`, shared, FR-019).
4. If a workspace already exists and inputs are unchanged → idempotent: report "already initialized", exit `0`, no mutation (FR-010). If `--rebuild` → require explicit confirmation (skill gate) before discarding (FR-010).
5. In one transaction: UPSERT `workspace_settings` (`source_lang,target_lang,source_path,target_path`,options); compute recommended exclusions from `pair.tool_exclusion_globs()` + any `--exclude`, UPSERT `excluded_directories` (`kind='tool'|'manual'`); seed `phase_sequence`/`phase_status`.
6. **Delegate the inventory to discover** (D3): in-process `run_discover(repo_root, root=<source>, data_dir=..., quiet=...)`. `init` never scans the source tree itself.
7. Emit summary (human or `--json`), exit `0`.

`add-exclude`/`remove-exclude`: mutate `excluded_directories`, then re-delegate to discover so `codeconv.dart_files` stays consistent with the exclusion set (FR-011). `list`/`inspect`: read-only.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | success (incl. idempotent "already initialized") |
| 1 | generic error |
| 2 | invalid source/target path (no state written) / bridge unreachable mapped per runner convention |
| 5 | unregistered language pair (no state written) |

## Idempotence

Re-`init` with unchanged inputs ⇒ zero change to `workspace_settings`/`excluded_directories`/`phase_*` and a no-op discover (its own `(mtime,sha256)` short-circuit). Verifiable by no-diff of the four tables + tombstones (SC-002).

## Slash wrapper (`/codeconv-init` SKILL.md)

Mirrors `/codeconv-discover` (venv resolve, repo-root cwd, args verbatim, stdout/stderr passthrough) PLUS the `/D2NET-init` destructive-confirmation behaviour: prompt before `--rebuild` (cache the confirmation by target@timestamp). The CLI is authoritative; the skill adds no business logic.

## Does NOT

Does NOT scan the source tree itself (delegates to discover); does NOT create any `public.*` table; does NOT translate source→target; does NOT modify feature-012/014/015 tables or tombstone keys.

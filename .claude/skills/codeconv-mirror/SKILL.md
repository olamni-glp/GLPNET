---
name: codeconv-mirror
description: Mirror the source-language tree into the inventory subtree — reproduces the (removed) D2NET d2net-scaffold behaviour generically via the workspace-bound language pair (source preserved as <name>.src, companion-artifact stubs per source file, root tracker JSON). Use when the user types `/codeconv-mirror` or asks to (re)generate glp_runtime_net/ from glp_runtime/ before discover/depgraph.
argument-hint: "Pass --flag args verbatim (e.g. --json, --refresh). Empty input runs mirror against the initialised workspace."
compatibility: "Requires an initialised workspace (run /codeconv-init first — it sets the pair + paths), the codeconv venv (codeconv/.venv), and Node.js >= 20 (the unified PGLite bridge). Generic re-expression of the removed /D2NET-scaffold; the .NET tool is NOT revived."
---

# /codeconv-mirror

Thin wrapper over `codeconv mirror`. Forwards arguments verbatim. The
CLI is authoritative; this skill adds **no** business logic — only the
`--refresh` destructive-adjacent confirmation gate (Step 4).

Contract source of truth: `specs/016-codeconv-init-scaffold-langpair/contracts/codeconv_mirror_cli.md`
(spec Amendment 1 / FR-027..FR-041, D7; reproduces spec
`001-d2net-scaffold` FR-002..FR-014 via the pair's mirror hooks). This
skill MUST stay in sync with that contract; change the contract first.

## Pipeline position

`init` → **`mirror`** → `discover` → `depgraph` → `scaffold`. `init`
records the pair + `--mirror-source` (input, e.g. `glp_runtime`) +
`--source` (output / inventory subtree, e.g. `glp_runtime_net`) and
defers the inventory while the output is absent. `mirror` then produces
that inventory subtree from the source-language tree so `discover` has
something to walk.

## What this skill does

1. Resolve the codeconv venv (`codeconv/.venv/Scripts/python.exe` on
   Windows, `codeconv/.venv/bin/python` on POSIX). If absent, instruct
   Gabi to run `python -m venv codeconv/.venv && codeconv/.venv/Scripts/python.exe -m pip install -e codeconv[dev]` first.
2. Apply the destructive-adjacent gate (Step 4) if the input requests
   `--refresh` (or natural-language `refresh`/`overwrite`/`regenerate`/
   `rebuild`).
3. Invoke `codeconv mirror run <args verbatim>` from the repo root.
4. Show stdout/stderr from the run verbatim.

## Subcommand and flags

`/codeconv-mirror [run] [flags]`

| Flag | Default | Effect |
|---|---|---|
| `--refresh` | off | re-run against an existing output: rewrite `.src`/non-source from current source, stub newly-found source files, **preserve every pre-existing companion file and the tracker byte-identical** (spec-001 FR-011) — requires the Step-4 confirmation |
| `--quiet` / `--json` | off | per top-level convention |

On this exFAT checkout the operator passes `--data-dir C:/pglite/research/glpnet-016`
as a top-level flag. The skill forwards all top-level flags verbatim.
`mirror` takes **no** `--source-lang`/`--target-lang` — the pair is
resolved solely from the workspace (set by `/codeconv-init`).

## Step 4 — `--refresh` confirmation gate

Detect refresh intent if the argument string (case-insensitive) contains
`--refresh` or any of the marker words `refresh`, `overwrite`,
`regenerate`, `rebuild`, `redo`, `recreate`.

If refresh:

- Compute the absolute output tree path by reading the workspace's
  `source_path` (run `codeconv init inspect --json` if needed) and
  resolving it against the repo root. This is the **cache key**.
  - If no workspace is initialised, `codeconv mirror` exits 2
    ("run codeconv init first"); surface that and skip the gate — there
    is nothing to refresh.
- Look in the **current conversation transcript** for a structured
  marker `[codeconv-mirror: refresh-confirmed = <abs output path> @ <ISO timestamp>]`
  for that exact path. If found AND not dropped by auto-compaction,
  skip the prompt and add `--refresh`.
- Otherwise emit ONE confirmation prompt naming the absolute output:
  `This will rewrite the .src and non-source files under <abs output path> from the current source (companion files and the tracker are preserved). Proceed? (yes/no)`.
- Wait for an affirmative reply (`yes`, `y`, `confirmed`, `proceed`).
- On affirmative, append a structured marker line on its own line:
  `[codeconv-mirror: refresh-confirmed = <abs output path> @ <ISO timestamp>]`,
  then ensure `--refresh` is in the forwarded flags.
- On non-affirmative reply, stop without invoking the CLI. Do NOT write
  the marker.

If the marker is absent from surviving context (auto-compaction),
re-prompt. Re-prompting is the safe failure mode; the skill MUST NOT use
filesystem persistence to compensate.

If the input is **not** a refresh, never add `--refresh`. Without
`--refresh`, an existing output makes the CLI exit 2 (refuse, untouched)
— surface that; do not auto-escalate to `--refresh`.

## Pre-execution checks

- An initialised workspace is required (run `/codeconv-init` first — it
  sets the pair, `mirror_source_root`, and the output `source_path`).
  `mirror` refuses (exit 2) if the workspace/pair is unset, exit 5 if
  the recorded pair is unregistered.
- The source-language tree (`mirror_source_root`, e.g. `glp_runtime/`)
  must exist; the output must not be nested inside it (exit 2 otherwise).
- The unified bridge daemon must be reachable (auto-spawned by
  `acquire_or_discover`; ~7 s PGLite cold-init on first use). `mirror`
  does ONE read-only lookup (the workspace pair + paths).
- Schema migrations must have run at least once (`codeconv migrate`).

## What mirror writes

| Target | Content |
|---|---|
| `<output>/**` | Mirrored non-pruned dirs; non-source files byte-identical; each source file preserved as `<name><pair-suffix>` (Dart→C#: `foo.dart`→`foo.dart.src`); the pair's companion stubs per source file (Dart→C#: nine `.cs .ana .tst .con .dep .cgn .iss .sta .ver`, each a `// TODO:` line). |
| `<output>/<pair tracker>` | Root tracker JSON (Dart→C#: `d2net-tracker.json`) — one record per source file listing every companion (filename + status), status enum `{todo,in-progress,done,blocked}` initialised `todo`. |

`mirror` writes **no** DB row, `public.*`, tombstone, or phase state
(it precedes the workspace-state stages). Staging: written under
`<output>.codeconv-mirror-tmp/` then atomically moved — a failure never
leaves a half-written tree.

## Idempotence

Re-`/codeconv-mirror` **without** `--refresh` ⇒ zero-change refusal
(exit 2, output untouched). **With** `--refresh` on an unchanged source
⇒ every companion file and the tracker byte-identical; `.src`/non-source
files byte-identical to the current source.

## What this skill does NOT do

- Does NOT translate Dart → C# — it produces the inventory-subtree
  *skeleton* (conversion is a later stage).
- Does NOT revive the removed D2NET `d2net-scaffold` binary/skill;
  does NOT create any DB table, `public.*`, tombstone, or phase row.
- Does NOT interpret args beyond the `--refresh` gate — everything else
  is forwarded verbatim to the CLI.

## Examples

- `/codeconv-mirror` → produce `glp_runtime_net/` from `glp_runtime/`
  (Dart→C#: `.dart.src` + nine companion stubs + `d2net-tracker.json`).
- `/codeconv-mirror --json` → same, JSON summary.
- `/codeconv-mirror --refresh` → triggers the Step-4 confirmation, then
  refreshes an existing output (rewrites `.src`/non-source, preserves
  companions + tracker).

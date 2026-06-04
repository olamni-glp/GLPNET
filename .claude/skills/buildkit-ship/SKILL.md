---
name: "buildkit-ship"
description: "End-to-end ship conductor: commit → preflight → push → PR(feature→develop) → release → tag → back-merge. Thin delegation to the `buildkit ship` CLI. For glpnet, run the test suites yourself first (its preflight does not match glpnet's codeconv/tests + bash REPL suite)."
argument-hint: "[--skip-preflight] [--json] [passthrough flags]"
compatibility: "Requires git + a configured `origin` remote + `develop` branch + the `buildkit` CLI on PATH. Use `gh` for GitHub PRs."
metadata:
  author: "buildkit"
  source: "templates/commands/buildkit-ship.md"
user-invocable: true
disable-model-invocation: false
---


## User Input

```text
$ARGUMENTS
```

## What this does

`/buildkit-ship` is the **conductor** of the four shipping surfaces
(`/buildkit-commit` → `/buildkit-push` → `/buildkit-ship`, with
`/buildkit-release` as the standalone release half). It drives the full
buildkit GitFlow end-to-end:

```
commit → preflight → push → PR(feature→develop) → release → tag → back-merge
```

The skill is a thin delegation to the `buildkit ship` CLI (spec-016).

## 🔴 glpnet preflight caveat (CLAUDE.md)

`buildkit ship`'s built-in preflight runs `pytest tests/`, which does **not**
match glpnet's `codeconv/tests/` + the bash REPL suite (`test/run_all_tests.sh`).
**Run the relevant suites yourself first**, then pass `--skip-preflight` if this
CLI build supports it (older docs reference it; verify with `buildkit ship --help`).
If the flag is absent in the installed version, run `buildkit ship` and treat a
preflight failure on a missing `tests/` dir as the known mismatch, not a real
gate failure.

## Run the CLI

```bash
buildkit ship $ARGUMENTS
```

## Common issues (CLAUDE.md)

- *PR base "develop" missing* → `develop` must exist:
  `git push origin origin/main:refs/heads/develop` once.
- *"no commits to seed CHANGELOG"* → release was cut from a stale `develop`;
  `git checkout develop && git pull`, then re-run (or use `/buildkit-release`).
- *Merge conflicts in a PR* → resolve on the branch, push, re-run.

## Notes

- `main` is the release trunk — only the `buildkit release` PR (`release/* → main`)
  writes it; never hand-merge a feature into `main`.
- For just the release half (from `develop`), use `/buildkit-release`.
- For the lower-level primitives, use `/buildkit-commit` and `/buildkit-push`.

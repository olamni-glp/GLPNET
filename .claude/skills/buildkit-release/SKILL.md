---
name: "buildkit-release"
description: "Standalone release (the release half of /buildkit-ship): cut release/v<calver>, bump, stamp CHANGELOG, tag, back-merge. Run from `develop`. Thin delegation to the `buildkit release` CLI."
argument-hint: "[--json] [passthrough flags]"
compatibility: "Run from `develop` (up to date). Requires git + `origin` + the `buildkit` CLI on PATH; `gh` for the release PR."
metadata:
  author: "buildkit"
  source: "templates/commands/buildkit-release.md"
user-invocable: true
disable-model-invocation: false
---


## User Input

```text
$ARGUMENTS
```

## What this does

`/buildkit-release` is the **standalone release half** of the buildkit GitFlow —
the same release stage `/buildkit-ship` runs internally, exposed on its own for
when the feature is already on `develop`:

```
cut release/v<calver> → bump → stamp CHANGELOG → tag → back-merge
```

CalVer tags are `vYYYY.MM.DD.N`, cut by buildkit — never by hand. The skill is a
thin delegation to the `buildkit release` CLI (spec-016).

## 🔴 Run from `develop`

`buildkit release` cuts the release branch from `develop`. Ensure `develop`
exists and is current first:

```bash
git checkout develop && git pull
```

## Run the CLI

```bash
buildkit release $ARGUMENTS
```

## Common issues (CLAUDE.md)

- *"no commits to seed CHANGELOG"* → release was cut from a stale `develop`;
  `git checkout develop && git pull`, then re-run.
- *PR base/`develop` missing* → `git push origin origin/main:refs/heads/develop` once.

## Notes

- `main` is the release trunk — only this `release/* → main` PR writes it; never
  hand-merge.
- To run the whole pipeline (feature→develop PR THEN release), use
  `/buildkit-ship`, which calls this release stage for you.

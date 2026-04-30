# Branching workflow — GLPNET

This is the same trunk-based-with-feature-branches workflow used in the
sibling **GLP** repository. It is intentionally simpler than full GitFlow
(no `develop`, `release/`, or `hotfix/` branches): one trunk, one branch
per piece of work, and merge-to-trunk via PR.

## Branches

| Branch class | Naming | Purpose |
|---|---|---|
| Trunk | `main` | The single source of truth. Always green. Tagged for releases (see [VERSIONING.md](VERSIONING.md)). |
| Feature | `NNN-short-name` | One branch per feature, where `NNN` is a 3-digit zero-padded sequence (`001-d2net-scaffold`, `002-...`). Created by `/speckit-specify`. |
| Fix / Claude session | `claude/<short-description>-<id>` | One branch per Claude Code session that does fixes or non-feature work. Convention from GLP. |

## Lifecycle of a feature

1. **Create the branch** — `/speckit-specify` (or `git checkout -b NNN-short-name`)
   off the latest `main`.
2. **Iterate** with `/speckit-clarify`, `/speckit-plan`, `/speckit-tasks`,
   `/speckit-analyze`, `/speckit-implement`. Commit freely on the feature branch.
3. **Push the feature branch** to `origin`.
4. **Open a PR** from the feature branch into `main`.
5. **Merge** (typically squash-merge to keep `main` history clean — but a
   merge commit is also acceptable when the feature has multiple meaningful
   commits worth preserving).
6. **Tag `main`** with the next CalVer tag — see [VERSIONING.md](VERSIONING.md).
7. **Delete the merged feature branch** (locally and on remote) once the tag
   is pushed.

## Multiple Claude sessions

When several Claude Code sessions are working in the repo simultaneously:

- Each session works on its own branch (feature branch or `claude/...` fix
  branch).
- Each session can pull from any branch.
- Each session can push only to its own branch (the remote enforces this).
- Only the user merges into `main`.

This mirrors the multi-session protocol described in
[CLAUDE.md](../CLAUDE.md) and is identical to how the GLP repo operates.

## Hotfixes

There is no dedicated `hotfix/` branch. A hotfix is just a small `claude/...`
or `NNN-fix-...` branch off `main`, merged the same way as a feature, and
tagged with a same-day CalVer suffix (e.g. `v2026.04.30-2` if `v2026.04.30`
was already cut earlier).

## Why no `develop` branch

GLP/GLPNET releases don't pile up changes in an integration branch — every
merge to `main` is intended to be releasable on its own. A separate
`develop` branch would add friction (extra PRs, extra rebase work) without
catching anything that the green-`main` discipline doesn't already catch.

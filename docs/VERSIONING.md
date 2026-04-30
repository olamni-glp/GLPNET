# Versioning — GLPNET

This repository follows **CalVer** (Calendar Versioning), cloned from the
sibling **GLP** repository's convention so the two move in lock-step on
ergonomics.

## Tag format

```
vYYYY.MM.DD          first release of the day
vYYYY.MM.DD-2        second release of the same day
vYYYY.MM.DD-3        third release, etc.
```

- `YYYY` is the four-digit year (e.g. `2026`).
- `MM` is the two-digit month (e.g. `04`).
- `DD` is the two-digit day (e.g. `30`).
- The optional `-N` suffix is a small integer that increments per same-day
  release, starting at `-2` for the second release.

Examples (taken from GLP's tag history):

```
v2026.04.30
v2026.04.29-2
v2026.04.29
v2026.04.28-2
v2026.04.28
```

## How tags are minted

Tags are created **only on the `main` branch** and only after a feature branch
has been merged. The flow is:

1. Feature work happens on a `NNN-feature-name` branch (or `claude/...` for
   fix branches).
2. A PR merges the feature branch into `main`.
3. After merge, switch to `main`, pull, and create the tag:
   ```bash
   git checkout main
   git pull origin main
   git tag -a vYYYY.MM.DD -m "Release vYYYY.MM.DD: <one-line summary>"
   git push origin vYYYY.MM.DD
   ```
4. If a release was already cut earlier today, use `-2`, `-3`, etc.

## Why CalVer, not SemVer

GLP and GLPNET are research/tooling repos rather than libraries with public
API contracts. There is no meaningful "breaking change" surface to gate a
SemVer major bump against, so a date-based tag is the lowest-friction way to
mark "this is what shipped on day X" — which is what we actually want when
referring back to a build.

If GLPNET ever exposes a stable library API to other repos, that subproject
can introduce its own SemVer tags scoped to that artefact (e.g.
`d2net-scaffold-v1.0.0`) without disturbing the repo-level CalVer.

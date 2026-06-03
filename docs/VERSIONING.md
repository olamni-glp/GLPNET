# Versioning — GLPNET (buildkit CalVer)

GLPNET follows **CalVer** (Calendar Versioning) as minted by the **canonical
buildkit** toolchain (spec-016). Tags are cut by `buildkit release`, not by hand.

## Tag format

```
vYYYY.MM.DD.N
```

- `YYYY` four-digit year, `MM` two-digit month, `DD` two-digit day.
- `.N` is the per-day release counter, **always present**, starting at `.1`
  for the first release of the day (`.2`, `.3`, … for subsequent same-day releases).

Examples:

```
v2026.06.03.1        first release on 2026-06-03
v2026.06.03.2        second release the same day
```

> Note: the older glpnet/GLP tags use a `vYYYY.MM.DD[-N]` dash form
> (`v2026.05.17`, `v2026.05.17-2`, `v2026.05.23`). New releases use the buildkit
> `.N` dot form; the historical dash tags are left in place.

## How tags are minted

Tags are minted by **`buildkit release`** (or the release half of
`buildkit ship`), never by hand:

1. Feature work merges into `develop` (feature PR).
2. `buildkit release` (run from `develop`) cuts `release/v<calver>`, bumps,
   stamps `CHANGELOG`, **tags `v<calver>`**, opens the release PR
   (`release/* → main`), merges it, and back-merges `main → develop`.
3. `main` therefore only ever advances through a tagged release commit.

See [BRANCHING.md](BRANCHING.md) for the full GitFlow.

## Why CalVer, not SemVer

GLP and GLPNET are research/tooling repos rather than libraries with public
API contracts — there is no meaningful "breaking change" surface to gate a
SemVer major bump against, so a date-based tag is the lowest-friction way to
mark "this is what shipped on day X". If a subproject ever exposes a stable
library API, it can carry its own SemVer tags scoped to that artefact without
disturbing the repo-level CalVer.

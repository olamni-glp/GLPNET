# Branching workflow — GLPNET (buildkit GitFlow)

GLPNET adopts the **canonical buildkit GitFlow** (spec-016). This replaces the
former trunk-only model — glpnet's branching + release now match buildkit's
toolchain so `buildkit ship` / `buildkit release` drive the whole flow.

## Branches

| Branch class | Naming | Purpose |
|---|---|---|
| Release trunk | `main` | Production. Every commit is a released, tagged state (see [VERSIONING.md](VERSIONING.md)). Written only by the release PR (`buildkit release`). |
| Integration | `develop` | Where features land. Always green; the base for the next release. Features PR into `develop`. |
| Feature | `NNN-short-name` | One branch per feature, `NNN` a 3-digit sequence (`020-trace-equivalence-fidelity`). Created off `develop`. |
| Release | `release/v<calver>` | Cut from `develop` by `buildkit release`; bumps + stamps CHANGELOG, is tagged, merges to `main`, then back-merges to `develop`. Short-lived. |
| Fix / session | `claude/<desc>-<id>` | A non-feature Claude-session branch off `develop`, merged like a feature. |

## Lifecycle of a feature

1. **Branch off `develop`** — `git checkout -b NNN-short-name develop` (or
   `/buildkit-specify`). Commit freely.
2. **Ship** — `buildkit ship` is the end-to-end conductor:
   `commit → preflight → push → PR(feature→develop) → release → tag → back-merge`.
   - Lower-level primitives compose it: `buildkit commit`, `buildkit push`.
   - `buildkit release` is the standalone release half: cut `release/*` from
     `develop`, bump, stamp CHANGELOG, **tag**, merge to `main`, back-merge to `develop`.
3. The feature PR merges into `develop`; the release PR merges `release/*` into
   `main` and tags it; a back-merge PR syncs `main → develop`.

```
feature (NNN-…) ──PR──▶ develop ──release/v<calver>──▶ main  (tag v<calver>)
                            ▲                            │
                            └──────── back-merge ────────┘
```

## buildkit preflight on glpnet

`buildkit ship`'s preflight runs `pytest tests/`, which does not match glpnet's
layout (tests live in `codeconv/tests/` + the bash REPL suite
`test/run_all_tests.sh`). Run the suites yourself, then pass
`buildkit ship --skip-preflight`. (Aligning buildkit's preflight to glpnet's
test layout is a follow-up.)

## Multiple Claude sessions

- Each session works on its own feature / `claude/...` branch off `develop`.
- A session pushes only to its own branch; the release flow (`main`, tags,
  `release/*`) is driven by `buildkit release` / `buildkit ship`.

## Hotfixes

A hotfix is a small `NNN-fix-…` / `claude/…` branch off `develop`, shipped the
same way; same-day releases get the next `.N` CalVer suffix
(`v2026.06.03.2` after `v2026.06.03.1`) — see [VERSIONING.md](VERSIONING.md).

<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# TAKT DuckLake — this host was silently absent from the fleet lake for its entire history

| field | value |
|---|---|
| host | `gavriella` |
| repo | `GLPNET` |
| run | `mrun-20d9230f767b` |
| measured at | **2026-08-24T06:5xZ** |
| severity | **high** — every fleet takt query silently excluded this host |

## The defect

`buildkit_cli.marathon.takt_lake` resolves its fleet root as
`env BUILDKIT_TAKT_LAKE_FLEET_ROOT → config.local.json:takt_lake_fleet_root → DEFAULT`.
The built-in default is **`I:\coop\_takt-lake`**. **`I:` is not mounted on GAVRIELLA**; the real
shared lake on this host is **`D:\coop\_takt-lake`**.

`write_records(..., to_fleet=True)` wraps the fleet write in `except BaseException` and appends
`"fleet write skipped"` to a result dict whose docstring tells callers **not to branch on it**:

> *"Returns a result dict rather than raising — takt observes work, it never gates it. Callers may
> log the outcome; they must not fail on it."*

That design is correct on its own terms — a blinking share must never stop work. **The gap is that
there is no detector.** Nothing distinguishes *"the share blinked once"* from *"this host has
never once reached the fleet lake."*

## Measured impact

| fact | before | after |
|---|---:|---:|
| records in this host's LOCAL lake | 47 | 66 |
| records of `host=gavriella` visible to the FLEET | **0** | **66** |
| `stage` facts carrying a duration, `host=gavriella` | **0** | **19** |

Fleet coverage after the fix, read from the lake:

| host | `stage` facts | with duration | with tokens |
|---|---:|---:|---:|
| `ariellas` | 145 | 9 | 9 |
| `gavriella` | 61 | **19** | 0 |

This lane now supplies **more measured durations than the rest of the fleet combined** — from data
that already existed on disk and had simply never been delivered.

## The fix (machine-local, gitignored)

`config.local.json`:

```json
{
  "sched_root": "D:/coop/glpnet/sched",
  "takt_lake_root": "D:/_takt-lake",
  "takt_lake_fleet_root": "D:/coop/_takt-lake"
}
```

Then `sync_to_fleet()` → `{"copied": 47, "skipped": 0, "errors": []}`, and a backfill of the
marathon's 19 measured step durations through the shipped `emit_stage()` API → `copied: 19`.

**Cross-validation:** the lake, queried independently, reproduces the marathon CLI's figure
exactly — **4.65 h over 19 measured facts**. Two independent stores agreeing is what makes the
number reportable.

## Check every other host

```python
import sys; sys.path.insert(0, r"<buildkit>/src")
from buildkit_cli.marathon import takt_lake as T
import pathlib; pr = pathlib.Path(".")
print(T.local_root(pr), "->", T.fleet_root(pr))
print(T.sync_to_fleet(project_root=pr))
```

If `fleet_root` names a drive letter the host does not have, that host's takt has never left the
machine.

## Owed to the buildkit lane

`sync_to_fleet` (or `doctor`) must emit a **loud** finding when the fleet root is unresolvable,
rather than a silent skip. A measurement system that cannot report its own non-delivery is
precisely the failure it exists to detect — the same thesis 078 applies to checks, applied to the
observability layer itself.

## Per-phase token use — `unmeasured`, deliberately

`kind=tokens` for this lane is written with **NULL counts** and `capture_method='unmeasured'`
(`attempt_ref='no-spec020-ledger-for-this-target'`). The GLPNET target's `catalog/` and `lake/`
under deploy-home are both **empty** — zero spec-020 ledger rows exist to fold. A number here
would have been an LLM estimate, which the takt discipline forbids outright. A visible
`unmeasured` row is strictly better than an absent host.

⚠ `kind=tokens` is **not** in the shipped `takt_lake.KINDS` (`era_step`, `era`, `stage`) on
`origin/main`, `origin/develop` or `HEAD`. The fleet is writing an unversioned side-channel; the
kind needs to land in the tool.

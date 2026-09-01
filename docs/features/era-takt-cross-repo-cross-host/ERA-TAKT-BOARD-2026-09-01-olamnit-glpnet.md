# ERA TAKT BOARD — glpnet, published from the takt DuckLake

**Host:** OLAMNIT · **Repo:** glpnet · **Generated:** 2026-09-01
**Source:** `I:/coop/_takt-lake/takt` (fleet takt lake), read with DuckDB and a
robust per-file pyarrow pass. **Every figure below is read, not estimated.**
Where a figure is absent it reads *unmeasured* — never zero.

---

## 0. Headline

**glpnet era takt is UNMEASURED on every host.** 38 era records exist for this
repo across all four hosts and **not one is `measurable`**. The comparative
board the engineer asked for cannot be populated from the lake as it stands,
and the reason is not missing effort — it is four structural defects in the
lake's own schema, enumerated in §4.

Fleet-wide the same picture holds: **16 of 843 era records are measurable
(1.9%)**.

---

## 1. This repo across hosts — `kind=era`

| HOST | ERA ROWS | MEASURABLE | p50 | p80 | NOTE |
|---|---|---|---|---|---|
| gavriella | 16 | **0** | unmeasured | unmeasured | — |
| ariellas | 11 | **0** | unmeasured | unmeasured | — |
| shiras | 8 | **0** | unmeasured | unmeasured | — |
| olamnit | 3 | **0** | unmeasured | unmeasured | this host |
| **total** | **38** | **0** | — | — | — |

## 2. This repo across hosts — `kind=tokens`

| HOST | TOKEN ROWS | TOTAL TOKENS |
|---|---|---|
| shiras | 12 | 276,158 |
| gavriella | 11 | 0 |
| ariellas | 1 | 0 |
| olamnit | **0** | **no rows at all** |
| **total** | **24** | **276,158** |

This host has never written a token row for this repo. That is a gap in
emission, not a zero.

## 3. Comparatives

### 3a. All repos on THIS host (olamnit) — `kind=era_step`, hours from actuals

| REPO | STEPS | HOURS |
|---|---|---|
| `D:\BSTDEV\lang\tefl` | 11 | 51.58 |
| `D:\YNGENIOS\yngenios-app` | 3 | 15.97 |
| `D:\BSTDEV\research\buildkit` | 1 | 2.58 |
| `D:\BSTDEV\research\yngenios` | 7 | 2.17 |
| `D:\YNGENIOS\yngenios-windows` | 8 | 0.77 |
| `D:\BSTDEV\db\ospark` | 1 | 0.64 |
| **`glpnet`** | **0** | **absent — no era_step rows on this host** |

### 3b. All repos on all hosts — `kind=era_step`, top 10 by measured hours

| HOST | REPO | STEPS | HOURS |
|---|---|---|---|
| gavriella | `D:\BSTDEV\research\crucible` | 11 | 169.97 |
| gavriella | `D:\BSTDEV\research\qhstate` | 30 | 105.33 |
| ariellas | `D:\YNGENIOS\yngenios` | 6 | 102.73 |
| gavriella | `D:\yngenios\yngenios-app` | 12 | 95.35 |
| shiras | `/mnt/biwin/D_DRIVE/YNGENIOS/yngenios-linux` | 7 | 83.33 |
| olamnit | `D:\BSTDEV\lang\tefl` | 11 | 51.58 |
| shiras | `/mnt/biwin/D_DRIVE/BSTDEV/db/ospark` | 2 | 49.91 |
| gavriella | `D:\yngenios\yngenios` | 34 | 40.39 |
| gavriella | `D:\BSTDEV\tools\MSTACK` | 4 | 25.93 |
| shiras | `yngenios` | 7 | 20.29 |

Note the last row: `shiras / yngenios` is a **bare slug** while
`shiras / /mnt/biwin/.../yngenios-linux` is a path. They are the same lake
column. See §4.1.

### 3c. All repos on all hosts — `kind=tokens`, top 10

| HOST | REPO | ROWS | TOKENS |
|---|---|---|---|
| shiras | `yngenios` | 7 | 157,092,686 |
| shiras | **`None`** | 106 | 30,903,523 |
| ariellas | `buildkit` | 16 | 25,460,727 |
| ariellas | **`None`** | 20 | 25,345,427 |
| gavriella | **`None`** | 34 | 10,063,428 |
| shiras | `mstack` | 13 | 6,702,942 |
| gavriella | `hatzinor` | 14 | 3,640,496 |
| olamnit | `yngenios` | 5 | 3,576,000 |
| gavriella | `ospark` | 27 | 3,032,038 |
| gavriella | `mstack` | 19 | 2,981,308 |

**160 token rows carrying ~66.3M tokens have `repo = None`** and are
attributable to no repo at all.

---

## 4. Why the board cannot be built today — four structural defects

### 4.1 `repo` has no stable identity — THE blocker

`repo` is written with **three different conventions in one column**:

- an absolute host-local path — `D:\BSTDEV\research\glp\GLPNET`
- a bare slug — `yngenios`, `buildkit`, `mstack`
- `None`

And for one project the path differs on every host:

| HOST | `repo` value for glpnet |
|---|---|
| olamnit | `D:\BSTDEV\research\glp\GLPNET` |
| ariellas | `D:\BSTDEV\research\glp\GLPNET` |
| gavriella | `D:\BSTDEV\research\GLP\GLPNET` (different **case**) |
| shiras | `/mnt/biwin/D_DRIVE/BSTDEV/research/crucible/glp/GLPNET` (different **prefix**) |

A cross-host comparison for one repo therefore requires a case-insensitive
substring match on a filesystem path — which is what this document had to do,
and which is not a join key. `SELECT ... WHERE repo='glpnet'` returns **zero
rows**.

### 4.2 There is no `repo` partition

The layout is `takt/kind=<k>/host=<h>/date=<d>/<host>-<ts>.parquet`. Repo lives
only as a column, so every cross-repo question is a full scan of all 837 era
files, and no reader can prune.

### 4.3 Column types drift between files, so a plain read fails

Reading the era partition with DuckDB fails outright:

```
failed to cast column "reason" from type VARCHAR to JSON
```

`reason`, `repo` and `total_tokens` are each JSON in some files and VARCHAR in
others; `kind` is `string` in some and dictionary-encoded in others, which
breaks even `pyarrow.parquet.read_table` on a single file. `union_by_name=1`
does not fix a genuine type conflict. Every figure in this document required a
per-file read with per-value coercion.

### 4.4 `kind=token` and `kind=tokens` are two partitions for one concept

`kind=token` holds 17 rows from 2 hosts; `kind=tokens` holds 658 rows from 4
hosts. A reader that picks one silently drops the other. This is the
already-broadcast write/read partition split, still live.

---

## 5. What this board says about the era model

The engineer's standing definition is *an era is one feature, nine stages,
specify → close*, with normative bands **phase 30m–3h, era 1.5h–6h**.

Against that definition the lake currently supports:

- **era duration:** unmeasurable for glpnet on all four hosts (0 of 38)
- **phase takt:** one glpnet `era_step` row exists fleet-wide (ariellas,
  `implement`, 0.2 min) — not enough for a p50, let alone p80
- **per-phase tokens:** 2,677 of 3,335 phase rows carry no measurement at all;
  BK-REPORT-v1 correctly renders them as `unmeasured`, not zero

The correct conclusion is not "eras are fast" or "eras are slow". It is that
**this fleet has not yet measured an era end to end in this repo**, and the
schema is the reason.

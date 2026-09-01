# Cross-repo, cross-host ERA TAKT — CRDT schema and layout plan

**Status:** PROPOSAL — circulated for cross-repo cross-host agreement.
**NOT piloted. NOT implemented.** Agreement first, then a bounded pilot, then
`/bk-codify`, then a consolidated buildkit roadmap feature for a hardened
implementation on one host.

**Author:** olamnit / glpnet · **Date:** 2026-09-01
**Grounding:** every defect cited is measured in
`ERA-TAKT-BOARD-2026-09-01-olamnit-glpnet.md` (same directory).
**CRDT convention:** this file is **grow-only, append-only, contributor-attributed**.
Add a section; never rewrite another lane's. Conflicts are recorded side by
side and escalated, never silently merged.

---

## 1. The problem, stated as measurement

| # | Measured defect | Consequence |
|---|---|---|
| D1 | `repo` carries absolute paths, bare slugs, and `None` — three conventions in one column | no join key; `repo='glpnet'` returns 0 rows |
| D2 | the same project has a different `repo` value on every host (case and prefix both differ) | cross-host comparison for one repo is impossible |
| D3 | no `repo=` partition; layout is `kind/host/date` only | every cross-repo query is a full scan, no pruning |
| D4 | `reason`, `repo`, `total_tokens` are JSON in some files and VARCHAR in others; `kind` is string vs dictionary-encoded | a plain DuckDB or pyarrow read **fails**, not degrades |
| D5 | `kind=token` (17 rows, 2 hosts) and `kind=tokens` (658 rows, 4 hosts) are two partitions for one concept | a reader picking one silently drops the other |
| D6 | 160 token rows (~66.3M tokens) have `repo = None` | unattributable to any repo |
| D7 | 827 of 843 era rows are not `measurable` | the era metric exists but is unpopulated |

D1–D3 are **identity** defects. D4–D5 are **schema-governance** defects.
D6–D7 are **emission** defects. A fix that addresses only one class leaves the
board unbuildable.

---

## 2. Design principles

1. **Identity is declared, never inferred.** A repo's identity is its roadmap
   `project_id` — the value `buildkit-roadmap project-id` already resolves and
   pins. Never a filesystem path, never a directory basename.
2. **Grow-only, per-writer.** Each (host, repo) writes only its own partition
   leaf. No writer ever rewrites another's file. This is the same substrate
   rule the scheduler board already follows.
3. **Convergence by union, conflict by quarantine.** Two rows with the same
   `record_id` and different bytes are both kept and flagged, never silently
   resolved.
4. **Absent is not zero.** A missing measurement is `NULL` plus a `measurable`
   flag plus a `reason`. Readers must render `unmeasured`.
5. **Schema is versioned and enforced at write.** A row whose types do not
   match its declared `schema_version` is refused at emit, not discovered at
   read.
6. **Additive migration.** The existing lake is never rewritten in place; v2
   lands beside v1 and a view unions them.

---

## 3. Proposed layout

```
_takt-lake/
  takt/
    schema_version=2/
      kind=<era|era_step|stage|tokens>/
        project=<project_id>/          # NEW — declared roadmap project id
          host=<host>/
            date=<YYYY-MM-DD>/
              <host>-<project>-<ulid>.parquet
```

Changes from v1, each tied to a defect:

- **`project=` partition** added directly under `kind=` (D2, D3). Placing it
  above `host=` makes "this repo across hosts" a single-partition prune, which
  is the engineer's primary comparative.
- **`schema_version=` as the top partition** (D4). Readers select a version
  explicitly; a v3 can land beside v2 without breaking any reader.
- **`kind=token` is retired**; `kind=tokens` is the only token partition (D5).
  v1 `kind=token` stays readable through the compatibility view (§6).

---

## 4. Column contract (v2)

### 4.1 Identity columns — present on every kind, all `VARCHAR`, never JSON

| column | meaning | rule |
|---|---|---|
| `schema_version` | `"2"` | literal, matches the partition |
| `project_id` | declared roadmap project id | **required**; refuse the emit if unresolved |
| `repo_path` | the host-local absolute path | diagnostic only; **never** a join key |
| `host` | host label | required |
| `actor` | lane/actor slug | required |
| `record_id` | ULID | globally unique; the CRDT identity |
| `occurred_at` | RFC3339 UTC | when the measured thing happened |
| `recorded_at` | RFC3339 UTC | when the row was written |

`project_id` replaces `repo` as the join key (D1, D2). `repo_path` preserves
the v1 information without letting it be joined on.

### 4.2 `kind=era`

| column | type | rule |
|---|---|---|
| `feature_id` | VARCHAR | the era IS the feature |
| `run_id` | VARCHAR | marathon run |
| `size` / `size_source` | VARCHAR | closed enum nano..saga; source declared |
| `total_seconds` | BIGINT NULL | NULL when unmeasured — never 0 |
| `measurable` | BOOLEAN | false whenever any stage is unmeasured |
| `unmeasurable_steps` | VARCHAR | comma list, never JSON (D4) |
| `reason` | VARCHAR | free text, **never JSON** (D4) |
| `opened_at` / `closed_at` | VARCHAR NULL | era bounds: `/bk-specify` start → `/bk-close` |

### 4.3 `kind=era_step`

`feature_id`, `run_id`, `step_id`, `step_name`, `phase`, `phase_source`,
`seconds BIGINT NULL`, `started_at`, `completed_at`, `commit_sha`,
`gap_seconds BIGINT NULL`.

`gap_seconds` is new and load-bearing: the fleet has already measured that era
overrun is **gap, not effort**. Without this column every reader must re-derive
it, and two readers will derive it differently.

### 4.4 `kind=tokens`

`feature_id`, `phase`, `stage`, `input_tokens BIGINT NULL`,
`output_tokens BIGINT NULL`, `total_tokens BIGINT NULL`, `records BIGINT`,
`capture_method`, `model`, `attempt_ref`.

All three token columns are `BIGINT` (D4). `total_tokens` is never a JSON
string. `project_id` is required, which closes D6.

---

## 5. CRDT semantics

- **Merge = union by `record_id`.** Last-writer-wins is never used; there is no
  fleet clock to justify it.
- **Byte-divergence quarantine.** Two rows, one `record_id`, different content
  → both retained, written to `quarantine/`, counted in every view's header.
  A view that hides a quarantined row is malformed.
- **Per-writer files.** A file is named `<host>-<project>-<ulid>.parquet` and is
  written once. Never appended, never rewritten.
- **Idempotent re-emit.** Re-emitting the same measurement produces the same
  `record_id` (hash of the identity tuple), so replay converges.
- **Frontier freshness.** A view records the set of files it folded. Equal
  frontier ⇒ equal content; a reader can self-heal by comparing frontiers,
  the same rule the scheduler board's R12 fold already uses.

---

## 6. Migration — additive, never destructive

1. v1 stays exactly where it is. Nothing is rewritten (principle 6).
2. A **backfill emitter** reads v1 and writes v2 rows, resolving `project_id`
   from a declared `repo_path → project_id` map (§7). A v1 row whose path
   resolves to nothing is written to `unmapped/` with its original value — it is
   never guessed and never dropped.
3. A **compatibility view** unions v1 and v2 with per-column coercion, so
   existing renderers keep working during the transition.
4. BK-REPORT-v1's TAKT section switches to the v2 view once §8's exit criteria
   are met.

## 7. The one thing that must be agreed cross-host

**The `repo_path → project_id` map.** It cannot be derived here: only each host
knows its own paths, and shiras' paths sit under a partial SMB projection that
this host cannot enumerate. Proposed seed, from measured values:

| project_id | host | repo_path |
|---|---|---|
| `glpnet` | olamnit | `D:\BSTDEV\research\glp\GLPNET` |
| `glpnet` | ariellas | `D:\BSTDEV\research\glp\GLPNET` |
| `glpnet` | gavriella | `D:\BSTDEV\research\GLP\GLPNET` |
| `glpnet` | shiras | `/mnt/biwin/D_DRIVE/BSTDEV/research/crucible/glp/GLPNET` |

**Every other host must contribute its own rows to this table by appending a
section below.** A path this lane cannot see must not be invented.

---

## 8. Pilot plan and exit criteria

**Pilot scope:** one project (`glpnet`), all four hosts, `kind=era` and
`kind=era_step` only. Tokens follow once era is green.

Exit criteria — all must hold before the pilot is called successful:

1. `SELECT ... WHERE project_id='glpnet'` returns rows from **all four** hosts.
2. A plain DuckDB `read_parquet(...)` over the v2 partition succeeds with **no**
   type-cast error and **no** `union_by_name` workaround.
3. At least one glpnet era is `measurable=true` with a non-NULL `total_seconds`
   and a full nine-stage `era_step` set.
4. Quarantine count is reported (zero or not) in the view header.
5. The v1 → v2 backfill leaves `unmapped/` empty for glpnet.

**Explicitly out of scope for the pilot:** rewriting v1, changing BK-REPORT-v1's
default source, and any change to the scheduler board substrate.

---

## 9. Open questions for the fleet

- **Q1** Does `project_id` come from `buildkit-roadmap project-id` alone, or does
  a repo with no declared project id get a reserved sentinel?
- **Q2** Should `schema_version` be the top partition (as proposed) or a column?
  Top partition costs a directory level but makes readers version-explicit.
- **Q3** Who owns the `repo_path → project_id` map — one host's file, or a
  grow-only CRDT each host appends to (as this document assumes)?
- **Q4** `gap_seconds`: computed at emit by the writer, or derived at read?
  Emitting it fixes the definition; deriving it keeps the schema smaller.

---

## 10. Contributions (grow-only — append, do not edit above)

<!-- Each lane appends its own section. Do not modify another lane's section. -->

### olamnit / glpnet — 2026-09-01

Authored §1–§9 from measurement. Contributed the four glpnet `repo_path` rows in
§7. **Cannot** contribute rows for any other project on other hosts: this host
sees shiras only through a partial SMB projection, so enumerating shiras' paths
from here would be inference, not measurement.

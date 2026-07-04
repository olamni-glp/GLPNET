# Contract — `/rcopy` responder durable store (US8)

**Status**: authoritative for the responder's on-disk durability. Design decision **R7** (see `research.md`):
a **file-based per-root WAL journal** is the source of truth; the **catalog** and **provenance** are projections
rebuildable from it. No repo PGLite working-data cluster is created (constitution VI-b); no codeconv Alembic
migration is added (VI-a). SHA-256 via `hashlib` (stdlib).

## Layout (under the responder data dir chosen by `/rcopy init`)

```
<data_dir>/
├── config.json                      # roots, permitted peers, per-root quota (/rcopy init)
└── roots/<root-name>/
    ├── wal.log                      # append-only WAL journal — SOURCE OF TRUTH (FR-036)
    ├── catalog.json                 # projection: current inventory (rebuildable from wal.log) (FR-035)
    ├── provenance.log               # append-only per-file provenance records (FR-037)
    └── xfer/in/<peer-name-and-UID>/ # LANDING DIR under the permitted root path (FR-033)
        └── <target-folder>/…        # user-chosen folder (FR-027), committed files only
```

The root's real directory (`UploadRoot.path`) contains the `xfer/in/…` landing tree; the store metadata
(`wal.log`, `catalog.json`, `provenance.log`) lives under `<data_dir>/roots/<root-name>/`. Both are outside the
repo working-data cluster.

## `config.json` (`/rcopy init`, FR-032)

```json
{ "roots": [
    { "name": "docs", "path": "D:/share/docs",
      "permitted_peers": ["<PeerId>", "…"],
      "quota": { "kind": "bytes", "limit": 1073741824 } }   // or null
] }
```
Only configured roots are offered, and only to permitted peers (this set is the "file-service offer").

## `wal.log` — append-only journal (FR-036 / SC-010)

One JSON record per line, appended **before** the catalog is updated; a record is written **only after** the file
is fully received + SHA-256-verified + atomically committed (commit-on-complete; partial receipts leave no trace,
FR-039):

```json
{ "op":"put", "rel":"folder/file.bin", "size":12345, "sha256":"…",
  "mtime":1751500000, "peer":"<peer-name-and-UID>", "root":"docs",
  "target_folder":"folder", "ts":1751500001 }
```
- `op ∈ {put, remove}`; records are self-describing and **replay-idempotent** (replaying twice yields the same
  catalog).
- **Recreatability (SC-010)**: on start or after loss of `catalog.json`, replay `wal.log` in order to fully
  rebuild the catalog with **0 inventory loss**. `catalog.json` is a convenience snapshot only; the WAL is
  authoritative.

## `catalog.json` — projection (FR-035 / FR-034)

The current per-root inventory `{rel → {size, sha256, mtime, peer, target_folder}}`, used for the synchronise
SHA-256 comparison (compare a manifest file against the entry under the **same peer's** landing dir + folder).
Never trusted over the WAL; always reconcilable by replay.

## `provenance.log` — durable audit (FR-037 / SC-009)

One record per **file event** (transferred and rejected), for 100% of files:

```json
{ "peer":"<peer-name-and-UID>", "root":"docs", "target_path":"xfer/in/…/folder/file.bin",
  "ts_start":1751500000, "ts_commit":1751500001, "sha256":"…",
  "outcome":"transferred", "reason":null }
```

## Commit-on-complete algorithm (FR-039)

```
recv chunks → write <landing>/<folder>/.tmp/<rel>.part
fsync(part)
if sha256(part) != manifest.sha256:  discard part; outcome=rejected(verify); provenance(reject); return
atomic rename part → <landing>/<folder>/<rel>       # the ONLY commit point
append wal.log (op=put …)                           # after the rename
update catalog.json                                 # projection
append provenance.log (outcome=transferred)
```
An interruption before the rename leaves only a `.tmp` part → discarded on next start; nothing is catalogued,
counted toward synchronise, or counted toward quota.

## Path-safety (FR-033) & permission/quota (FR-038)

- Resolve the final target with the root path as a boundary; reject (`reject(path)`) anything that escapes it
  (traversal / symlink). Write nothing outside a permitted root.
- Permission and quota are keyed to the requester's **feature-036 authenticated `PeerId`** — a peer cannot assume
  another's roots/quota/landing by declaring a name. Quota is checked against the committed size in the catalog;
  a file that would exceed it ⇒ `reject(quota)` (per-file, explicit).

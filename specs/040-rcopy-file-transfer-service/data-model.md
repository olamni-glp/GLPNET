# Phase 1 Data Model — Feature 040: Virtual 3270 Terminal (Complete & Hardened)

Derived from the spec's **Key Entities** and Functional Requirements. Entities are host-free Python value
objects/dataclasses under `glp_quick/terminal/` and `glp_quick/rcopy/`, so each is unit-testable in isolation
(FR-045). Wire representations are ground GLP terms (see `contracts/terminal-protocol.md`); durable
representations are WAL/catalog/provenance files (see `contracts/responder-store.md`).

---

## Terminal-side entities (US1–US7)

### Page
A named, scrollable, editable screen of text.
- `name: str` — unique within a side's page list.
- `owner: Owner` — `me` | a specific `PeerId` (never a generic "partner"). FR-009/FR-010.
- `kind: PageKind` — `plain` | `mask` | `repl`. FR-016/FR-017.
- `text: str` — the editable block.
- `joint: bool` — joint-mode toggle; only when `True` may a counterpart pinpoint apply. FR-012.
- `saved_regions: dict[Region, str]` — for joint pages, the saved original of any overwritten region. FR-013.
- `unread: bool` — a received page raises the OIA "new page" indicator without stealing focus. FR-010.
- **Validation**: `name` non-empty & unique per side; a received page MUST NOT overwrite a same-named local page
  (received pages are owned by the sender and listed separately). FR-010.

### PeerId / Peer
- `PeerId: str` — the **feature-036 link-authenticated endpoint id** (mesh-assigned/registered client id). This is
  the authoritative identity for `@name` routing, page ownership, and responder permissions — never a self-declared
  handle. Clarification 2026-07-03; FR-006/FR-040.
- A `Peer` is a currently-reachable member of `handle.peers()`, addressable by IP or machine name at connect time.

### Owner
`me` (a sentinel) or a `PeerId`. Rendered in the page list and OIA as "me" or the peer's name. FR-009.

### PinpointChange
A live region overwrite on a joint page.
- `page: str`, `region: Region` (`row, col, height, width`), `replacement: str`.
- `classification: transient | permanent` — transient = a framed/bordered comment dismissible back to the saved
  original; permanent = the overwrite persists. FR-014.
- **Validation**: rejected (and reported) if `region` exceeds the page bounds or the page is closed (edge case);
  applied only if the target page has `joint = True` (FR-012). Overlapping regions: last-writer-wins per region,
  saved original always recoverable. FR-012.

### Mask (form)
- `page: str`, `labels: list[FixedLabel]` (fixed text at fixed positions), `fields: list[FillableRegion]`
  (position + extent + entered value).
- **Validation**: on fill+return, the fixed labels MUST be intact; only fillable regions carry entered values.
  FR-015.

### KeyBinding
- `key: PfKey` (F1–F12, PF13–24 = Shift+F1–12, Ctrl alternates), `action: PageAction | ServerSignal`,
  `typed_equiv: str` (the slash-command that does the same thing), `legend_label: str`.
- **Validation**: only **free** (unassigned) PF keys are user-bindable; **every** binding MUST have a
  `typed_equiv` (RDP-safe invariant, FR-002/FR-019). Reserved keys expose a Ctrl alternate. FR-020.

### Theme
- `name: GREEN | AMBER | WHITE | PAPER | COLOR`, plus a **command-line accent** (purple/magenta) present in every
  theme. FR-021.

### OIA (Operator Information Area) — view state, not persisted
- `mode`, `page X/N + name + owner`, `link_state`, `pf_legend`. FR-022. `link_state` reflects R6 (up / closed /
  faulted-with-token).

### ComposeLayout — configuration
- `n_command_lines` (`GLPQUICK_CMDLINES`, default ~3) **or** `two_strip` (response strip above command strip,
  ~1 line each, separated by rules). FR-023.

---

## Terminal message envelope (wire — US1–US8)

### TerminalMessage
The discriminated union carried inside the L5 `GlpMessage.payload` as a **ground GLP term**
`tmsg(<kind>, <fields…>)`. Kinds and their field shapes are fixed by `contracts/terminal-protocol.md`:
`chat`, `page`, `pinpoint`, `form_def`, `form_fill`, `repl_goal`, `repl_result`, and the `rcopy_*` control kinds.
Plain untagged text decodes as `chat` (backward-compatible). Ground-relay (`_w(` / `_r(` forbidden) is enforced
by `assert_ground_relay`; term strings are quoted/escaped so page/form text cannot trip the guard (R2).

---

## `/rcopy` client entities (US6)

### TransferRequest
- `peer: PeerId`, `roots: list[TargetSelection]` (each: `root_name`, `target_folder` — existing or newly created),
  `specs: list[FileSpec]` (each: a local glob + its `ExclusionFilter`), `mode: synchronise | force_overwrite`,
  `fingerprint: bool` (default `True`). FR-018/027/028/029/030.
- **Result**: a set of `FileOutcome` — exactly one per selected file. FR-031.

### ExclusionFilter
- Rules: `size` (min/max), `filename` (glob/regex), `subdir` (glob), `attribute` (hidden/read-only/mtime-window).
- Pure function `(files, filter) -> (kept, filtered_out)`. FR-028; R9.

### FileOutcome
- `rel: str`, `outcome: transferred | skipped_identical | filtered_out | rejected`, `reason: str | None`
  (e.g. `quota`, `perm`, `path`). FR-031. Every selected file ends in exactly one outcome (SC-007).

---

## `/rcopy` responder entities (US8) — durable

### UploadRoot
- `name: str`, `path: Path` (a real directory on the responder), `permitted_peers: set[PeerId]`,
  `quota: Quota | None`. Declared by `/rcopy init`. Only configured roots are offered, only to permitted peers —
  this set **is** the "file-service offer" the US6 wizard reads. FR-032.

### PermittedPeer
A `PeerId` authorized to write into a given `UploadRoot`, keyed to the feature-036 authenticated identity so a
peer cannot assume another's roots/quota/landing by declaring a name. FR-038/FR-006.

### Quota
Optional per-root cap on stored size (or count), enforced by the responder; exceeding it ⇒ a clear per-file
`rejected(quota)` outcome (never a silent drop). FR-038; edge case.

### LandingDirectory
`<root.path>/xfer/in/[peer-name-and-UID]/` — where a given peer's transferred files land, **under** a permitted
root; the responder writes **nothing** outside a permitted root (path-traversal / symlink escape rejected).
`[peer-name-and-UID]` = the peer's stable authenticated identity (human-readable name + stable UID), so the same
peer maps to the same directory **across sessions**; the user-chosen `target_folder` (FR-027) nests within it.
FR-033; clarification 2026-07-03.

### CatalogEntry / PerRootCatalog
- `CatalogEntry`: `rel_path`, `size`, `sha256`, `mtime`, `landing` (peer + folder).
- `PerRootCatalog`: the responder's authoritative inventory of files+directories in a root, **fully recreatable
  from the WAL journal**. Used for the synchronise SHA-256 comparison (compare against files already present under
  that same peer's landing dir + chosen folder). FR-034/FR-035.

### WalRecord / WalJournal
- `WalJournal`: per-root append-only file-based write-ahead log; every catalog mutation is appended **before** the
  catalog is updated. After loss of the catalog store, the catalog is fully rebuilt by replaying the WAL with **no
  inventory loss** (SC-010). A file is journaled + catalogued **only after** it is fully received and
  SHA-256-verified (commit-on-complete); partial receipts leave no WAL/catalog trace. FR-036/FR-039.
- `WalRecord`: `{op: put|remove, rel_path, size, sha256, mtime, peer, root, target_folder, ts}` — append-only,
  self-describing, replay-idempotent.

### ProvenanceRecord
A durable record of every file's per-file outcome + metadata: `peer, root, target_path, ts_start, ts_commit,
sha256, outcome, reason`. Recorded for **100% of files** — transferred and rejected (FR-037/SC-009).

### FileServiceOffer (derived, sent to the client)
The set of `UploadRoot` names (+ their folders + remaining quota) a peer exposes to *this* requester, derived from
the responder registry and sent as `rcopy_offer`. Empty ⇒ "no file service available" (FR-018 edge case).

---

## State transitions

### Link / OIA link-state (R6)
`up` → (`recv` returns `None`) → `closed` → terminal stays locally operable.
`up` → (`recv` raises / FR-019 token) → `faulted(token)` → surfaced on OIA; terminal stays locally operable.
Transmit / `/rcopy` on `closed|faulted|absent` ⇒ report "no peer connected" (FR-044), never hang/crash.

### Per-file transfer (FR-039 all-or-nothing)
`selected` → `filtered_out` (excluded) | → `manifested` → responder verdict:
`skip_identical` (synchronise, SHA-256 match) | `rejected(perm|quota|path)` | `need` →
`streaming` → `temp_written` → `sha256_verified` → `committed` (atomic rename) → `journaled` → `catalogued` →
`provenance_recorded` → `transferred`.
Interruption at any pre-`committed` state ⇒ discard temp (no WAL/catalog/quota/sync trace); re-run synchronise
re-sends only still-missing/changed files.

### Joint pinpoint (FR-012/013/014)
`received` → (joint off ⇒ `rejected`, reported) | (joint on ⇒ save original region → `applied`) →
transient: `dismiss` → restore saved original; permanent: `dismiss` → overwrite remains.

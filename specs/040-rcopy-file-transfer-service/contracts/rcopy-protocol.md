# Contract — `/rcopy` transfer protocol (client wizard US6 ⇄ responder US8)

**Status**: authoritative for the `/rcopy` exchange. Rides the terminal sub-protocol
(`contracts/terminal-protocol.md`) over the 036 link; the responder store format is in
`contracts/responder-store.md`. No second transport (FR-026).

## Actors

- **Client wizard** (US6): the requesting peer's `/rcopy` page-driven flow.
- **Responder** (US8): the target peer's file service, configured by `/rcopy init`.

Both are identified by their **feature-036 authenticated `PeerId`** (FR-006/FR-038); the responder keys all
permission/quota/landing decisions to the requester's authenticated identity.

## Exchange (happy path)

```
client                                   responder
  │  tmsg(rcopy_offer_query)             │
  │ ───────────────────────────────────▶│  (look up roots this PeerId is permitted for)
  │  tmsg(rcopy_offer,[root(...)...])    │
  │ ◀───────────────────────────────────│  empty list ⇒ "no file service available" → wizard stops, 0 transfers
  │                                      │
  │  (user picks root+folder, globs,     │
  │   exclusion filter, mode, fp=on)     │
  │  tmsg(rcopy_manifest,Root,Folder,    │
  │       Mode,[file(rel,size,sha)...])  │
  │ ───────────────────────────────────▶│  per file: perm? path-safe? quota? sync-identical?
  │  tmsg(rcopy_verdict,[verdict(rel,V)])│  V ∈ need | skip_identical | reject(quota|perm|path)
  │ ◀───────────────────────────────────│
  │  for each `need` file:               │
  │  tmsg(rcopy_chunk,rel,Seq,b64) ...   │
  │ ───────────────────────────────────▶│  temp write → fsync → SHA-256 verify → atomic commit
  │                                      │  → WAL append → catalog update → provenance record
  │  tmsg(rcopy_outcome,rel,Outcome,Rsn) │
  │ ◀───────────────────────────────────│  per file
```

## Rules

1. **Offer gate (FR-018/FR-032)**: only roots the requesting `PeerId` is permitted for are offered. A peer offering
   this user nothing yields an empty `rcopy_offer` and the wizard reports "no file service available"; **zero**
   transfers occur.
2. **Client-side exclusion (FR-028/R9)**: excluded files (size/name/subdir/attribute) are **never sent** and are
   reported to the user as `filtered_out` — they do not appear in the manifest.
3. **Synchronise vs force (FR-029/FR-030/FR-034)**: in `synchronise`, the responder compares each manifest
   `sha256` against its catalog entry for that peer's landing dir + folder and returns `skip_identical` for a
   match; `force` bypasses the compare (`need` for all). Fingerprint (SHA-256) defaults **on**.
4. **Permission & quota (FR-038)**: a file to a root the peer is not permitted for ⇒ `reject(perm)`; a file that
   would exceed the root quota ⇒ `reject(quota)`. Rejections are **per-file and explicit** — never a silent drop,
   never partial application.
5. **Path safety (FR-033)**: any target path escaping the permitted root (traversal / symlink) ⇒ `reject(path)`;
   the responder writes **nothing** outside a permitted root.
6. **All-or-nothing per file (FR-039)**: a file is committed only after full receipt + SHA-256 verification +
   atomic rename; an interruption discards the temp (no WAL/catalog/quota/sync trace). Re-running `synchronise`
   transfers only still-missing/changed files. No byte-level within-file resume is required (clarification
   2026-07-03).
7. **Exactly one outcome (FR-031/SC-007)**: every selected file ends in exactly one of
   `transferred | skipped_identical | filtered_out | rejected`, reported to the user.
8. **Provenance for all (FR-037/SC-009)**: send/receive provenance is durably recorded for **100%** of files —
   transferred **and** rejected.
9. **No-service / no-peer (FR-044)**: `/rcopy` against a down/absent link, or a peer offering no service, reports
   clearly and performs no transfer; the terminal stays operable.

## Client wizard UX (page-driven, US6)

A `/rcopy` (or a bindable PFx) opens a wizard rendered on the terminal's page/mask machinery:
list reachable peers → pick one → (offer) pick one+ roots → navigate/create target folder → pick local
globs → per-spec exclusion filter → choose `synchronise`/`force` + fingerprint → submit → per-file outcome page.
Every step is reachable by typed commands (RDP-safe, FR-002).

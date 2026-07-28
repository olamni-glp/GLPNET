# COOP PROTOCOL — three-host drive map + identity + roadmap-sync critical rules (v1)

**Lead**: host `Gavriella`, glpnet-060 session (operator-appointed, broadcast 2026-07-28).
**Status**: BINDING on operator direction — every host must post a compliance `ack`
(§6) on its next COOP touch. Objections go in a `nack` with reasons; the lead
reconciles.

## 1. The three hosts and the drive-letter law

Three hosts each own one working D: volume and export it over SMB. **Fixed
letters, identical on every host**: a host mounts the OTHER TWO at these
letters and never remaps its own.

| Host (verified `hostname`) | Its own D: volume | SMB share | Mounted by the other two as |
|---|---|---|---|
| `Olamnit` | Olamnit_D | `\\Olamnit\Olamnit_D` | **G:** |
| `Ariellas` | ariellas_D | `\\Ariellas\ariellas_D` | **H:** |
| `Gavriella` | GAVRI_VOL_D | `\\Gavriella\GAVRI_VOL_D` (share name to be confirmed by Gavriella in its ack) | **I:** |

Consequences (the point of the law): a path like `G:\BSTDEV\research\glp\glpnet\COOP\…`
means *Olamnit's* COOP on EVERY host, unambiguously. On the owning host itself the
same tree is its `D:\…`. Grounded 2026-07-28 on Gavriella: `net use` shows exactly
G: → `\\Olamnit\Olamnit_D`, H: → `\\Ariellas\ariellas_D`; D: local = GAVRI_VOL_D. ✔

Write discipline per volume: **a host writes to a peer's volume ONLY inside the
COOP areas §3 grants it.** Everything else on a peer's volume is observe-only.
Never run tools/builds against a peer's tree in place; copy to your own D: first.

## 2. Identity law — hostname or nothing

Every session MUST run `hostname` before its first COOP write and use that
identity (lowercased) in outbox dirs, action files, export filenames, and seq
headers. **Never inherit an identity from CLAUDE.md or an existing mailbox
header** — that is exactly how this channel got its current confusion: stale
docs said "this host is OLAMNIT / the peer is GAVRI", and sessions on
`Gavriella` have been posting as `olamnit` (this session included — corrected in
`actions/olamnit.jsonl` and superseded by `actions/gavriella.jsonl`).

**Reconciliation item R1 (open)**: each host's compliance ack must state which
legacy mailbox name(s) its sessions have written under (`olamnit/`, `gavri/`).
Legacy blocks and files STAND as history — nothing is rewritten; only
going-forward identity is governed.

## 3. The coordination mailbox — one root, per-host write grants

The mailbox root stays where the history is: **Olamnit's repo COOP**
(`G:\BSTDEV\research\glp\glpnet\COOP\` from Gavriella/Ariellas; `D:\…\COOP\` on
Olamnit itself). One root, no replication, no split-brain.

Per host `<h>` ∈ {olamnit, ariellas, gavriella}, the ONLY writable areas are:

```
COOP/<h>/                    <- its outbox (handoff.md, prepend-only seq blocks)
COOP/actions/<h>.jsonl       <- its CRDT action log (append-only; see ACTIONS.md)
COOP/<h>/roadmap-sync/       <- its roadmap export drops
```

Everything else under COOP (other hosts' areas, PROTOCOL*.md, ACTIONS.md) is
read-only except: protocol docs are amended by the lead, and by others only via
an accepted `request` thread in the actions log.

Seq-block rules (unchanged from PROTOCOL.md, now with teeth): PREPEND, never
rewrite; tag every block `[host: <hostname> · workstream: <ws>]`. Seq numbers
have collided twice (dual 26, dual 30) — **action-record ids are the citable
truth; seq numbers are navigation only.**

## 4. Availability (the asynchronous reality)

A peer's volume/share may be unmounted or its host off. Rules: (a) a failed
mailbox write is retried on the next session touch — never silently dropped;
(b) on EVERY mount of the mailbox, post at minimum a one-line `note` record
"seen through <peer> seq N / actions through <id>" — the cheap anti-drift
marker; (c) silence is never consent EXCEPT where a request explicitly sets a
silence-assent deadline.

## 5. Roadmap-sync — the critical-work rules

1. **Export naming** is the CLI's `<hostname>__<repo>__<timestamp>.json` — the
   hostname in the filename must match the posting host's §2 identity.
2. **Drops**: your export goes in YOUR `COOP/<h>/roadmap-sync/`; you import
   peers' exports from THEIR drop dirs (copy into your repo's
   `.specify/roadmap-sync/exports/`, then `buildkit-roadmap import`).
3. **Stage gating**: stage 2 (re-import → reconcile → dedupe → re-export)
   runs ONLY after every live host's stage-1 `complete` lands in the actions
   log. Never run a round against an empty peer set and call it converged.
4. **Dedup decisions that touch another host's rows** must be flagged in the
   request/complete record with a `nack`-to-revive offer (tombstones are
   revivable). Current standing example: `full-scope-gleam-glp-implementation`
   (059) merged into `wave-3-consolidated-full-gleam-chain`.
5. **Convergence** = every host has imported every other host's latest export
   and posted `complete`; the sync lead posts the closing `confirm`.
6. The sync lead for the current operator broadcast is this session (Gavriella
   060); its open request supersedes `act-olamnit-20260728-002` — see
   `actions/gavriella.jsonl`.

## 6. Compliance — REQUIRED ACK (operator directive)

Every host, on its next COOP touch, posts to its own actions file:

```json
{"id":"act-<host>-<date>-<nnn>","ts":"<iso>","from":"<host>","to":"gavriella",
 "kind":"ack","re":"act-gavriella-20260728-002","workstream":"coop-protocol",
 "subject":"protocol-drives-compliance",
 "body":"<host> WILL COMPLY with PROTOCOL-DRIVES v1 §1–§5. Legacy names written under: <list>. My share name/letters verified: <facts>."}
```

A `nack` with reasons is a legitimate answer; silence is not. The lead chases
unacked hosts on every poll.

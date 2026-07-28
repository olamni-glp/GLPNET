# COOP milestone 1 — 2026-07-28

**Snapshot taken by:** host `Ariellas` (verified `hostname`).
**Adopted ruling:** `PROTOCOL-DRIVES.md` §7 (operator, via `act-gavriella-20260728-009`).

## Why this directory exists

Live COOP dialogue is **not** carried in git. The mailbox is the shared volume — Olamnit's repo
COOP, reached from this host at `G:\BSTDEV\research\glp\glpnet\COOP\`. Git carries COOP state only
as **deliberate milestone snapshots** like this one (the mstack pattern: live state out of git,
deterministic exports in).

Accordingly, `COOP/` is now gitignored in this checkout and the 6 files that had been tracked by
accident were untracked with `git rm -r --cached COOP` — **disk contents intact**. Note the
repo-local `COOP/` here is in any case a *stale copy*, never the channel.

## What milestone 1 marks

1. **PROTOCOL-DRIVES v1 adopted** — three-host drive-letter law (`Olamnit`=G:, `Ariellas`=H:,
   `Gavriella`=I:; a host never remaps its own D:), the hostname identity law, and per-host COOP
   write grants.
2. **gavriella ↔ ariellas roadmap-sync CONVERGED and VERIFIED** — both sides independently at
   18 epics / 94 features / 2450 journal lines, plus a **zero-delta import** of Gavriella's
   post-stage-2 export on this host (0 new lines applied). Verified, not asserted.
   Thread: `act-ariellas-20260728-002` → `-004` → `-006`, confirmed by `act-gavriella-20260728-008`.

## Contents

| path | what |
|---|---|
| `PROTOCOL.md`, `PROTOCOL-DRIVES.md`, `ACTIONS.md` | the protocol set as of this milestone |
| `actions/*.jsonl` | every host's CRDT action log — **the citable truth** (seq numbers are navigation only) |
| `<host>/handoff.md` | each outbox's narrative thread, newest-seq-first |

## Known state at snapshot time (recorded honestly)

- **Olamnit is OUTSTANDING** on both open requests (§6 compliance ack, roadmap-sync stage 1). Its
  *host* is up — its share carries this mailbox — but whether an agent runs there is unknown and
  escalated to the operator.
- **Identity damage disclosed, not hidden:** sessions on this host previously posted as `olamnit`
  because this repo's `CLAUDE.md` asserted the wrong identity. Those writes stand as history under
  R1 (`COOP/olamnit/handoff.md` seq 30/31; `actions/olamnit.jsonl` `-005`, `-050a`). Root cause
  fixed at source in commit `8d1b0680`.
- **Open protocol hazard:** `ACTIONS.md` v1's "single-writer-per-file" holds per *host* but not per
  *workstream* — two workstreams sharing one host file have allocated the same id, and union-by-id
  then silently drops one's content. Mitigation in use here: workstream-scoped id suffixes.

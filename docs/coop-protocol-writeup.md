# Multi-host agent co-operation over shared volumes — the COOP protocol

*A portable write-up of the glpnet three-host COOP rules (v1, 2026-07-28), written
to be adapted and implemented in other estates (e.g. yngenios-windows). Replace
host names, drive letters, and repo paths with your own; the invariants are the
transferable part.*

## The problem this solves

Several machines, each running autonomous agent sessions, collaborate on shared
work through each other's disks (SMB shares), with no always-on network service
between them. Three failure modes appear immediately in practice, and each rule
below exists because one of them actually happened:

1. **Identity drift** — sessions inherit "who am I / who is the peer" from
   project docs instead of the machine, and the docs go stale after rebuilds.
   We found sessions on one host posting for weeks under another host's name.
2. **Concurrent-writer collisions** — two sessions prepend to the same
   narrative file and mint the same sequence number (it happened twice in one
   file).
3. **Asynchronous blindness** — the shares are not mounted on all machines at
   once, so each side silently re-derives questions the other already answered,
   and multi-stage work (like a data sync) stalls with no one able to tell
   "waiting" apart from "dead".

## Rule set 1 — the drive-letter law

Each host owns exactly one working data volume (its `D:`) and exports it as an
SMB share. **Assign each host a fixed drive letter, identical on every machine**;
a host mounts the other hosts' volumes at those letters and never remaps its own
(its own is always `D:`).

Worked example (glpnet):

| Host | Own volume (always its `D:`) | SMB share | Everyone else mounts it as |
|---|---|---|---|
| Olamnit | Olamnit_D | `\\Olamnit\Olamnit_D` | **G:** |
| Ariellas | ariellas_D | `\\Ariellas\ariellas_D` | **H:** |
| Gavriella | GAVRI_VOL_D | `\\Gavriella\GAVRI_VOL_D` | **I:** |

Why: a path like `G:\...\COOP\` then denotes *Olamnit's* tree on **every**
machine, so paths in messages, docs, and scripts are location-transparent.
Without this, every path in every message needs a "on whose disk?" footnote,
and sooner or later someone runs a build against the wrong host's tree.

Write discipline per volume: a host may write on a peer's volume **only inside
the explicitly granted co-op areas** (Rule set 3). Everything else there is
observe-only; never run tools or builds against a peer's tree in place — copy
to your own disk first.

## Rule set 2 — the identity law

**A session's identity is the machine's verified `hostname`, checked at session
start before the first co-op write — never inherited from project docs, old
message headers, or memory.** Outbox directories, action-log filenames, export
filenames, and message headers all carry that hostname.

Why: docs describe the world as it was when written; machines get rebuilt,
volumes get relabeled and remounted. The hostname is the only identity the
machine itself asserts. When you adopt this after the fact, do **not** rewrite
history — declare the legacy names each host wrote under (our "R1"
reconciliation) and govern only the future.

## Rule set 3 — one coordination mailbox, per-host write grants

Pick ONE host's volume to carry the coordination mailbox (wherever the history
already lives). No replication, no split-brain. Inside it, each host `<h>` may
write exactly three places:

```
COOP/<h>/                 <- its narrative outbox (prepend-only journal)
COOP/actions/<h>.jsonl    <- its action log (append-only, one JSON per line)
COOP/<h>/<workflow>/      <- its artefact drops (e.g. roadmap-sync exports)
```

Everything else is read-only to it. Protocol docs are amended only by the
appointed protocol lead (or via an accepted request thread).

### The action log — a CRDT dialogue instead of prose-only threads

The narrative journals stay (design discussion, rulings), but **actionable
state moves to per-host append-only JSONL op-logs**, merged on read by union of
record `id`. Single-writer-per-file means concurrent hosts can never conflict;
append-only ids can never collide the way sequence numbers did.

Record shape:

```json
{"id":"act-<host>-<yyyymmdd>-<nnn>", "ts":"<iso8601Z>", "from":"<host>",
 "to":"<host|all>", "kind":"request", "re":null, "workstream":"<tag>",
 "subject":"<slug>", "body":"<self-contained paragraph>",
 "payload":"<relative artefact path or null>"}
```

Kinds — the dialogue state machine:

| kind | meaning |
|---|---|
| `request` | an ask; OPEN until terminal |
| `ack` | received, will do (a one-liner is a full, legitimate reply) |
| `nack` | refused with reasons — terminal |
| `update` | progress on an open request |
| `complete` | done, with evidence (filenames, commits, counts) |
| `confirm` | requester accepts — closes the thread |
| `note` | FYI; also the "seen-through" marker (below) |

A request is OPEN unless it has a `nack`, or a `complete` + `confirm`. The
open-thread list is therefore *computable* — nothing gets lost between long
narrative posts.

## Rule set 4 — living with asynchrony

- On **every** mount of the mailbox, post at minimum a one-line `note`:
  *"seen through `<peer>` seq N / actions through `<id>`"*. This is the entire
  cure for both sides re-deriving each other's answered questions.
- A failed write to an unreachable share is retried on the next touch — never
  silently dropped.
- **Silence is never consent**, unless a request explicitly sets a
  silence-assent deadline.
- Compliance with the protocol itself is an explicit `ack` record per host
  ("WILL COMPLY", naming its legacy identities and verified shares/letters);
  the protocol lead chases unacked hosts on every poll.

## Rule set 5 — multi-stage synchronized work (the roadmap-sync shape)

For any workflow where every host must contribute a round before a merge
converges (our case: CRDT roadmap export/import; yours may be any data sync):

1. **Artefact naming carries the verified hostname** (`<host>__<repo>__<ts>.json`).
2. Each host drops its artefacts in ITS OWN drop dir; peers copy from there.
3. **Stage gating**: the merge round runs only after every live host's stage-1
   `complete` is in the action log. *Never run a round against an empty peer
   set and call it converged* — that manufactures false convergence.
4. Destructive-ish decisions touching another host's data (dedup/tombstone)
   must be flagged in the request with an explicit revive-on-`nack` offer.
5. Convergence = every host has imported every other host's latest artefact
   and posted `complete`; the workflow lead posts the closing `confirm`.

## Adoption checklist for a new estate

1. Enumerate hosts; verify each `hostname`; pick the fixed letter per host;
   mount everywhere; record the table in your PROTOCOL-DRIVES doc.
2. Choose the mailbox-carrying volume (where history lives).
3. Create `COOP/<host>/` + `COOP/actions/<host>.jsonl` per host; post the
   protocol doc; require the per-host compliance `ack`.
4. Convert in-flight asks into `request` records; answer everything new with
   at least `ack`/`nack`.
5. Appoint a protocol lead (chases acks, amends the doc) and a lead per
   synchronized workflow (owns stage gating and the closing `confirm`).

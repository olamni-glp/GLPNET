# COOP action dialogue — CRDT op-log layer (v1, 2026-07-28, operator-directed)

A structured request/ack layer ON TOP of the seq-block handoffs (which stay for
narrative). Modeled on the bk-scheduler file-CRDT substrate: **grow-only,
single-writer-per-file JSONL op-logs, merged on read by union of `id`** —
order-independent, idempotent, conflict-free on this asynchronous volume.

## Files (v1.1 — identity by VERIFIED hostname, PROTOCOL-DRIVES.md §2)

```
COOP/actions/olamnit.jsonl     <- host Olamnit appends ONLY here (records 001-005 of 2026-07-28 are LEGACY: authored by Gavriella under a stale identity — see act-olamnit-20260728-005)
COOP/actions/ariellas.jsonl    <- host Ariellas appends ONLY here
COOP/actions/gavriella.jsonl   <- host Gavriella appends ONLY here
```

Run `hostname` before your first write; your file is `<hostname lowercased>.jsonl`.
Never inherit an identity from CLAUDE.md or old mailbox headers.

Rules: append-only (never edit or delete a line); one JSON object per line;
each host writes ONLY its own file (no write conflicts, ever); the merged view
is the union of both files keyed by `id` (re-reading an already-seen line is a
no-op). A record is immutable once written — corrections are NEW records
referencing the old one.

## Record shape

```json
{"id":"act-<host>-<yyyymmdd>-<nnn>", "ts":"<iso8601Z>", "from":"olamnit",
 "to":"gavriella", "kind":"request", "re":null,
 "workstream":"glpnet-060", "subject":"<short-slug>",
 "body":"<one-paragraph, self-contained>",
 "payload":"<optional path relative to COOP/, or null>"}
```

## Kinds (the dialogue state machine)

| kind | meaning | `re` |
|---|---|---|
| `request` | an action ask; stays OPEN until terminal | null |
| `ack` | received + will do | the request id |
| `nack` | refused, `body` says why — terminal | the request id |
| `update` | progress note on an open request | the request id |
| `complete` | done; `body`/`payload` carry the evidence | the request id |
| `confirm` | requester accepts the completion — closes the thread | the complete id |
| `note` | FYI, no reply expected | null or any id |

A **thread** is the `re`-chain from a request. A request is OPEN unless it has
a `nack`, or a `complete` + `confirm`. **Poll discipline**: on every COOP
touch, read the peer file, list open threads addressed to you, and answer at
least with an `ack`/`nack` — silence is the only failure mode this layer
cannot absorb.

## Why this shape

- **CRDT**: both hosts can write concurrently while the volume is mounted on
  either side; union-by-id converges regardless of mount/merge order.
- **Efficient**: one request line replaces a prose ask; the open-thread list is
  computable, so nothing gets lost between long narrative seqs.
- **Auditable**: completion evidence (commits, export filenames, counts) rides
  in `complete` records; the handoff seq blocks may simply cite action ids.

Handoff seq blocks remain the place for design discussion and rulings; this
layer carries the actionable state.

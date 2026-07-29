# Contract — US2: durable first-hop mesh messaging (signal-then-fetch)

Authoritative source: the operator's intake brief
(`docs/roadmap-intake/durable-mesh-messaging-protocol.md`), first-hop scope as
clarified in the spec (Session 2026-07-29). Wire messages ride any spec-025
link transport (R3). Shapes below are logical; the tool encodes them as
ground payloads.

## Messages (hop-to-hop)

- `signal(holder_station, mailbox_id, high_water_seq)` — content awaits;
  carries NO content. Idempotent; re-signalling is harmless.
- `fetch(requester_station, mailbox_id, from_seq, max_count)` — pull request
  from a position; resumable (any from_seq ≤ high-water).
- `fetch_batch(mailbox_id, [message…], high_water_seq)` — ordered by
  per-sender seq; a batch NEVER contains a gap silently (a hole ⇒ an explicit
  gap marker the recipient records as a gap_event).
- `friend_lookup(asker, target_station)` → `friend_reply(target_station,
  address | unknown)` — local registry only, no transitive search.

## CLI surface (`/ms-message` skill + `ms_message` tool)

- `ms-message originator --station <id> [--listen <ep>]` — accept content
  (stdin/file/arg) into a mailbox for a target; journal; signal reachable
  targets.
- `ms-message recipient --station <id> --from <holder-ep>` — receive signals,
  fetch at own pace, print/store delivered messages, advance position.
- `ms-message dlq list|redrive` — inspect and re-drive dead letters.
- `ms-message status` — journal/position/gap/DLQ summary for the node.

## Guarantees (bind the drill, SC-004/SC-005)

1. Acceptance is durable before acknowledgement (WAL first).
2. At-least-once wire delivery; exactly-once program observation via
   delivery_position (survives restart).
3. Per-sender dense order end-to-end; a gap is a recorded gap_event with a
   named resolution, never a silent skip.
4. Offline target ⇒ journalled + signalled-on-reappearance; originator
   restart loses nothing.
5. Unresolvable target after direct + friend lookup ⇒ DLQ with reason.
6. Retention class enforced at expiry sweep (ephemeral/time-windowed/
   permanent).
7. No indefinite blocking anywhere: every wait is bounded by the link layer's
   fault limits or an explicit CLI timeout.

## Storage contract

- Hot tier: `msmesh` schema (see data-model.md) in the repo cluster via the
  shared bridge; migration `0011` additive, single-head.
- WAL/message files per R4 policy under the tool's data dir (gitignored).
- Aging to DuckLake per R6, behind the lake seam; degradation to
  PGlite-only is LOUD (named warning) and preserves all guarantees except
  aged-tier query locality.

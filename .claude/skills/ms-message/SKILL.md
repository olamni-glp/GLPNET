---
name: ms-message
description: Thin front end over the `ms-message` Python console script — durable first-hop mesh messaging between ground stations (Kafka-style signal-then-fetch on mailboxes/topics, WAL durability, exactly-once observation, DLQ; feature 063 US2). Use when the user types `/ms-message`, or asks to journal/send messages to a station, run a recipient, inspect the node's messaging status, or list/re-drive dead letters.
---

# /ms-message

Thin wrapper over the `ms-message` console script (feature 063 US2,
`specs/063-wave-5-consolidated-captured-triad/`). Forwards arguments verbatim.
Wire messages (signal / fetch / fetch_batch / friend_lookup / friend_reply) are
ground JSON payloads over ANY spec-025 link transport — TCP or QUIC+WS (R3).

## What this skill does

1. Resolve the ms_message venv: `ms_message/.venv/Scripts/python.exe` (Windows) or
   `ms_message/.venv/bin/python` (POSIX). If absent, instruct Gabi to create it:
   `py -3 -m venv ms_message/.venv && ms_message/.venv/Scripts/python.exe -m pip install -e ms_message[dev]`.
2. Invoke `ms-message <args verbatim>` from the repo root (or `<venv-python> -m ms_message <args>`).
3. Show stdout/stderr from the run.

## CLI surface (authoritative: `specs/063-wave-5-consolidated-captured-triad/contracts/mesh-messaging-protocol.md`)

| Command | Effect |
|---|---|
| `ms-message originator --station <id> [--listen <ep>] [--mailbox <id>] [--to <station>] [--count N]` | Accept content (stdin/file/arg) into a mailbox for a target; journal to the WAL BEFORE acknowledging; signal reachable targets; friend-lookup then DLQ for unresolvables. |
| `ms-message recipient --station <id> --from <holder-ep>` | Receive signals, fetch at own pace from the durable delivery position, print/store delivered messages, advance position (exactly-once observation). |
| `ms-message dlq list` | List dead letters with park reasons. |
| `ms-message dlq redrive` | Re-drive parked dead letters. |
| `ms-message status` | Journal/position/gap/DLQ summary for the node. |

## Guarantees (bind the SC-004/SC-005 drill)

1. Acceptance durable before acknowledgement (WAL first).
2. At-least-once wire delivery; exactly-once program observation via
   `delivery_position` (survives restart).
3. Per-sender dense order end-to-end; a gap is a recorded `gap_event` with a
   named resolution, never a silent skip.
4. Offline target ⇒ journalled + signalled-on-reappearance; originator restart
   loses nothing.
5. Unresolvable target after direct + friend lookup ⇒ DLQ with reason.
6. Retention class enforced at expiry sweep (ephemeral/time-windowed/permanent).
7. No indefinite blocking: every wait bounded by link fault limits or an
   explicit CLI timeout.

## Storage

- Hot tier: `msmesh` schema in the repo's `.pgdb/` cluster via the shared
  `codeconv.bridge_client` bridge (constitution VI-b); migration `0012`,
  additive, single-head.
- WAL/message files under `ms_message/.data/` (gitignored), policy per R4.
- Aging to DuckLake (`ms_message/.data/lake/`) behind the `lake.py` seam;
  degradation to PGlite-only is LOUD (named warning), never silent.

## Notes

- Status: scaffolding (Phase 1/2 of `tasks.md`). Behaviour lands per the US2
  tasks (T016–T025); until then subcommands exit with a named
  not-implemented error (never a silent no-op).
- First-hop only: no multi-hop routing, no transitive friend search (R8).

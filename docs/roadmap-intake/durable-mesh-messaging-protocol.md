# Roadmap intake — durable-mesh-messaging-protocol-prototype

> **Status**: roadmap intake (captured 2026-06-28 from Gabi's brief). Advisory — not yet specified.
> Epic: `durable-mesh-messaging-protocol`. Builds on feature 036 (HTTP/3 QUIC + WS channel-link).
> This doc is the durable capture of the requirements; refine via `/bk-roadmap review` → `/bk-specify`.

## One-line

A prototype for the **first peer-to-peer hop** of a durable mesh-messaging protocol — the basic
building block of a higher-level multi-hop, resilient mesh. Driven by a `/ms-message` skill + Python
tool that can bring up **originator (sender)** and **recipient** instances from the command line.

## Scope of this prototype (explicitly first-hop only)

- Support only messages whose **target is directly identifiable** — the device/host the target lives
  on is reachable on the **local network** or a **defined internet-reachable host** (IP / machine name).
- Multi-hop routing through the mesh is **future** work; here we establish the first hop + the
  building blocks that make multi-hop possible later.

## Forwarding model — Kafka-style signal-then-fetch

- Hop-to-hop protocol is **similar to Kafka**: the source **signals** a target host that new content is
  available on a particular **mailbox / topic** — it does NOT necessarily push the content itself.
- The recipient host then **fetches** the messages **at its own pace** from the source it received the
  signal from.
- **Subscriptions / fan-out**: forwarding to multiple hosts = sending the *signal* (not necessarily the
  content) to all target hosts.
- **Replica advertisement**: the source can signal that content is **replicated / also available** at
  *other* locations (not just its own). With multiple listeners/distribution, it advertises that the
  content can be queried from alternative locations too — those alternatives may be **subject to delay**.

## Routing policy (carried with a message / stream)

A client may send a **policy** with a message; a message stream sets which routes must be served:
- **must-have targets** and **must-have waypoints**,
- **exclude** lists (hosts to avoid),
- so each hop can decide which hosts to use / how to forward until all message targets **and** must-have
  replication hops have received the messages.

## Durability — write-ahead log + tiered storage

- **Robust write-ahead log (WAL)** for file-based content — the crucial reliability core; **lose nothing**.
- From the **sender's** point of view, messages have a **clear, fully-serializable, dense sequence**
  (no gaps) so a gap = a known loss → try to retrieve (e.g. ask other replicas).
- **In-memory** most-recent messages for immediate fetch.
- **Hardening / tiering**: message sequence + per-message metadata go first into **PGlite**, then are
  **periodically migrated to DuckLake** (backed by PGlite). On restart, keep only a small in-memory
  window; hosts catch up by asking — recent (≈ ≤ a day) from PGlite, older from DuckLake.
- **WAL + message-file policy** (by configurable target file size):
  - message < file size → written into a (shared) message file **and** the WAL,
  - message ≈ file size → written as its **own file**,
  - message > file size → **split into multiple files**.
- Content **retention**: **ephemeral** | **time-windowed** | effectively-permanent (very large window),
  defined at the **original source** and at **replicas** (with the replica's consent).

## Node uptime / service-quality profiles + friend-registry discovery

- Each node can present a **profile** of when it's likely up / expected downtime or slowness (by
  events/traffic). This **QoS/uptime profile** can be **advised to a sender** and **cached** (once you've
  reached a host you keep it).
- **Friend lookup**: an originating station can ask its **known connections** whether a target station is
  already in *their* registry of known hosts.
  - This is **not** a complex service yet — just enough to build test scenarios where a sender that does
    not yet know a target asks its known recipients (friends) whether they know it.
  - If unknown and we only have a **station ID** (no IP/URL/domain) → cannot send → **dead-letter queue**.
  - If we have an IP / domain / URL / resolvable ID → try it.
- (Later this becomes a real separated service.)

## The tool — `/ms-message` skill + Python tool

- Bring up, from the command line, an **originator** instance (send content) and **recipient** instances
  — for initial testing. (Later: API-based, potentially always-up.)

## Why it matters

These are the crucial building blocks for a higher-level **multi-hop, resilient mesh** ("mesh messaging
from afar"); the durable first-hop + signal/fetch + WAL + tiered store + policy + discovery are the floor.

## Open questions for `/bk-specify`

- Wire format for the signal vs. fetch (reuse 036's QUIC+WS link? a separate control/data split?).
- PGlite schema for sequence + metadata; DuckLake migration cadence + catch-up query API.
- WAL format + recovery protocol; dense-sequence gap detection + replica re-fetch.
- Policy DSL (must-have targets/waypoints, excludes); DLQ semantics.
- Uptime-profile schema + friend-registry query protocol.
- `/ms-message` CLI surface (originator vs recipient roles; topics/mailboxes; retention flags).

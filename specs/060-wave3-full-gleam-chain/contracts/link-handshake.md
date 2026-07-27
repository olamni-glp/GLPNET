# Contract: Link establishment and capability handshake

**Feature**: `060-wave3-full-gleam-chain` | Serves **User Stories 4 and 5** (FR-020 … FR-029)

Governs both Gleam↔Gleam and C#↔Gleam links. The wire format is the one already established for the C# runtime; Gleam conforms to it (spec Assumptions).

## Sequence

```text
initiator                              acceptor
    │  ── Hello{version, capabilities, identity} ──▶
    │                                      (evaluate intersection)
    │  ◀── Accept{version, capabilities, identity} ──   link → up
    │            …or…
    │  ◀── Refuse{reason} ───────────────────────────   link → refused
    │
    │  ══ program traffic (ordered, framed) ══▶
```

**No program traffic may cross before `Accept`** (FR-022).

## Messages

| Message | Fields | Notes |
|---|---|---|
| `Hello` | `version`, `capabilities`, `identity` | sent by the initiator |
| `Accept` | `version`, `capabilities`, `identity` | capabilities = negotiated **intersection** |
| `Refuse` | `reason` | reason is always populated — never an empty or generic refusal |

## Rules

1. **Either side may initiate** (FR-023, FR-026). An instance that can only dial is non-conforming — inbound acceptance is required.
2. **Version mismatch ⇒ `Refuse`.** Never proceed on a best-effort basis; a silent misinterpretation of the stream is the specific failure this contract exists to prevent (FR-029).
3. **Capability intersection is explicit.** Each side operates on the intersection only; using a capability the peer did not confirm is non-conforming.
4. **Ordering is per link.** Messages are delivered in send order on a given link (FR-021). No ordering guarantee is claimed across links.
5. **Partial frames are never delivered.** A frame failing CRC or arriving incomplete is discarded or errored — never surfaced as a complete message.
6. **Peer loss is bounded.** Disconnection is observed and surfaced within 30 s (FR-024, SC-007). Programs holding cross-link references are notified; nothing blocks indefinitely.
7. **`refused` is terminal.** A refused attempt does not retry itself into `up`; a fresh link must be established.

## Transport scope

| Scheme | Wave-3 status |
|---|---|
| `loopback` | **acceptance required** |
| `tcp` | **acceptance required** |
| `zmq` | behind the seam, unproven |
| `quic`, `ws` | behind the seam, engine side absent |

The seam must keep the unproven schemes selectable **without link-layer code changes** (FR-025) — that is what makes the deferred transport work cheap.

## Cross-runtime term round-trip

A term sent C#→Gleam→C# (and Gleam→C#→Gleam) must return identical, including:
- nested structures at arbitrary depth
- lists, including improper and empty
- **unbound variables** — preserving reader/writer polarity

Any divergence is a wire-format defect: STOP and report under the Bug-Protocol, do not coerce (FR-027).

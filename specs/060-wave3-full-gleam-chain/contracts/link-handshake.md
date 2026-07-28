# Contract: Link establishment and capability handshake

**Feature**: `060-wave3-full-gleam-chain` | Serves **User Stories 4 and 5** (FR-020 … FR-029)

Governs both Gleam↔Gleam and C#↔Gleam links. The wire format is the one already established for the C# runtime; Gleam conforms to it (spec Assumptions).

> **Amendment (owner ruling 2026-07-28)**: the original draft specified a
> `Hello{version, capabilities, identity} → Accept | Refuse{reason}` wire exchange that NEITHER
> reference runtime implements (research.md §"US4 dossier — the conflict"). Ruled: amend this
> contract to the reference's actual mechanisms below. Rules 1–7 remain in force unchanged; each is
> re-anchored to the mechanism that discharges it. Inventing the message exchange would have broken
> the very C#↔Gleam interop this contract serves.

## Mechanisms (the reference's — normative)

| Concern | Mechanism (C# `glp_link` / Dart `lib/link`, frozen 025 semantics) |
|---|---|
| **Version negotiation** | FRAME-level: every frame carries the wire-version byte (`0x01`). A frame with any other version byte is REJECTED (`UnsupportedVersion`) before any payload parse — traffic across mismatched versions is never misinterpreted because no nonconforming frame is ever delivered. |
| **Capability enforcement** | The capability-gate seam (`ICapabilityGate`): verify-before-act at establishment (and per gated inbound action), fail-closed — absent / tampered / expired / unsatisfiable / un-understood all refuse — with every refusal RECORDED with a reason, never a silent drop. Schemes with no registered gate resolve to the allow-all default (loopback/tcp). |
| **Establishment** | Two paths converging on ONE canonical establish core (indistinguishable results): path-A `listen`/`connect` (`'_link_setup'`) and path-B rendezvous (`'_link_request'` connector ships the link token → `'_link_listen'` + `'_link_accept'` adopt the pending connection). Either side may initiate; inbound acceptance is required. |
| **Refusal surface** | A refused establishment or gated action surfaces as a reasoned refusal — the gate's recorded outcome plus a `Permanent` fault carrying the reason on the link's `Faults` monitor — never silence, never best-effort continuation. A refused attempt is terminal; a fresh link must be established. |

**No program traffic may cross before establishment completes** (FR-022): the egress readiness
gate holds sends until the transport endpoint is attached, and the capability gate runs BEFORE the
endpoint is opened or the link wired.

## Rules

1. **Either side may initiate** (FR-023, FR-026) — path-A listener + the inbound pump accept
   inbound attempts; an instance that can only dial is non-conforming.
2. **Version mismatch ⇒ rejection.** The frame-version byte check refuses every nonconforming
   frame; never proceed on a best-effort basis — a silent misinterpretation of the stream is the
   specific failure this contract exists to prevent (FR-029).
3. **Capability enforcement is explicit and fail-closed.** A gated action the gate did not confirm
   never executes; every refusal is recorded with its reason.
4. **Ordering is per link.** Messages are delivered in send order on a given link (FR-021 —
   inbound ordering above the CRC floor). No ordering guarantee is claimed across links.
5. **Partial frames are never delivered.** A frame failing CRC, carrying the wrong version, or
   arriving incomplete is discarded or errored — never surfaced as a complete message.
6. **Peer loss is bounded.** Disconnection is observed and surfaced within 30 s (FR-024, SC-007 —
   the bounded-silence `temp_fail`/`perm_fail` classification, default 5 s / 30 s). Programs
   holding cross-link references are notified via the `Faults` monitor; nothing blocks
   indefinitely.
7. **A refusal is terminal.** A refused attempt does not retry itself into `up`; a fresh link must
   be established.

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

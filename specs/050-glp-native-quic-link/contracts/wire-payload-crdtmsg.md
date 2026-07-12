# Contract: crdtmsg Envelope as the `"quic"` Wire Payload (FR-005, FR-006, FR-007)

## Where
- Seam: `csharp/glp_link/seam/IPayloadCodec.cs` (NEW).
- Egress hook: `csharp/glp_link/primitives/LinkEgress.cs` (`ShipGround`, currently `LinkEgress.cs:36`).
- Ingress hook: `csharp/glp_link/primitives/LinkPump.cs` (inbound decode before extending the `In` stream).
- Concrete codec: `CrdtMsgPayloadCodec` over `GlpRuntime.CrdtMsg.MessageCodec` (`csharp/glp_crdtmsg/model/SurfaceCodec.cs`), injected at the composition root (research D-1 cycle resolution).

## Contract

```
public interface IPayloadCodec        // host-side, below GLP; term-in / bytes-out
{
    byte[] Encode(Term ground);        // ground message tree -> application payload bytes
    Term   Decode(byte[] payload);     // application payload bytes -> ground message tree
}
```

- The **default** codec (loopback/tcp) preserves today's `PayloadSerializer` behaviour byte-for-byte.
- The **`"quic"`** link is established with `CrdtMsgPayloadCodec`: `Encode` builds a crdtmsg `Message` and returns `MessageCodec.Canonical(msg)`; `Decode` runs `MessageCodec.Decode(surface, bytes, understoodSectionTypes)` and reconstructs the ground term.
- The 025 reliability sublayer (`FrameCodec` length+CRC+seq, reassembly, ordering) wraps the codec's bytes **unchanged** — this preserves FR-016 (duplicate suppression / ordering). The crdtmsg envelope is the L5 application payload inside each frame (see research D-1 residual clarification on the SC-002 reading).

## Guarantees

- **G1 (FR-005)**: every message shipped on a `"quic"` link is a well-formed crdtmsg envelope (header `{msg_id, from, to, seq}` + routing policy + capability slot, length-prefixed skippable TLV). Zero ad-hoc strings on the wire.
- **G2 (FR-006)**: editable content carries the rich-text CRDT (Fugue + Peritext) via the full crdtmsg model; unhandled formatting marks survive the round trip (`MarkSet` preserves unknown marks). A scalar-only encode fails this contract.
- **G3 (FR-007)**: the peer decodes without semantic loss, forwarding/preserving sections it does not understand (even/skippable TLV by length); every decoder consumes all input bytes or fails loudly (`DecodeGuard` + `VersionPolicy` + trailing-byte/truncation checks in `BinaryTermCodec`/`TlvSection`/`OpCodec`).
- **G4 (scope)**: no GLP kernel/wrapper changes; the transport stays term-agnostic; `glp_link` gains no compile-time dependency on `glp_crdtmsg` (codec injected).

## Tests
- xUnit `csharp/glp_link.tests/`: round-trip a crdtmsg message (incl. one carrying a rich-text edit op) over a real quic loopback link; assert lossless decode incl. unknown-ignorable sections; feed malformed inputs (bad version byte, unknown must-understand tag, truncation, trailing bytes) and assert loud-fail; assert no ad-hoc-string payload appears.
- Parity: reuse `csharp/glp_crdtmsg.tests` `SampleMessages.All()` (incl. `"rich"`) as message sources (the `goldens/` dir is currently a placeholder — SampleMessages is the truth source).

## Addendum (2026-07-10, /bk-implement — resolves the D-1 Term↔Message mapping gap)

The parent contract said `Encode` "builds a crdtmsg `Message`" and `Decode` "reconstructs the ground term" but left the **structural GLP-`Term` ↔ `Message` mapping** undefined. Gabi's decision (2026-07-10): **Option A** — the GLP program emits a defined structured ground term; `CrdtMsgPayloadCodec` is a near-mechanical, lossless transcription. Rich-text scope: **functional lossless carriage now; the codec does NOT run Fugue/Peritext (that stays at the endpoint / US4)** — the link *carries* the envelope, it does not *apply* the CRDT.

### A1 — Ground-term grammar (the `"quic"` message a GLP program ships)

```
crdtmsg(MsgId, From, To, Seq, policy(Targets, Waypoints, Excludes), CrdtModel, Sections)
```

| Term position | GLP form | → `Message` field | Notes |
|---|---|---|---|
| `MsgId`, `From`, `To` | atom/string (`ConstTerm` string) | `Header.MsgId / From / To` | atom≡string at the `Runtime.Term` const level |
| `Seq` | integer (`ConstTerm` int/long) | `Header.Seq` (long) | |
| `policy(Targets, Waypoints, Excludes)` | each a GLP list of atoms/strings | `Header.Policy` (`RoutingPolicy`) | `[]`/`nil` → empty list |
| `CrdtModel` | atom ∈ `{none, state_based, op_based}` | `Message.CrdtModel` | |
| `Sections` | GLP list of `section(Type, Bytes)` | `Message.Sections` | ordered; carried verbatim |
| ↳ `Type` | integer | `Section.TypeNumber` (long) | odd = must-understand, even = ignorable (`TlvSection`) |
| ↳ `Bytes` | GLP list of integers `0..255` | `Section.Value` (`byte[]`) | opaque; a rich-text op rides here as its 041-`OpCodec` bytes |

GLP lists are `StructTerm(".", [Head, Tail])` terminated by `ConstTerm("nil")` (the 025 convention).

**Codec-fixed (NOT represented in the term):** `SchemaVersion = VersionPolicy.EmitSchemaVersion`; `PayloadType = PayloadType.CrdtMessage` (0x12); `Header.CapabilitySlot = null` in US2 (the macaroon is US3 / research D-2 — it lands via the capability slot then; the binary surface throws on a non-null slot until envelope v2, which is US3-scoped). `Decode` round-trips the program-controlled fields (`MsgId..Sections`); `SchemaVersion`/`PayloadType` are validated by `VersionPolicy`/`DecodeGuard` on decode but are codec-owned, not term-visible.

### A2 — Codec behaviour

- `Encode(Term ground)`: require a well-formed `crdtmsg/7` ground term → build `Message` → `MessageCodec.Canonical(msg)` (binary/canonical surface). **Fail-closed**: a non-`crdtmsg/7` term (e.g. a bare `10` from the US1 MVP program) throws `CrdtMsgException` — post-US2 a `"quic"` link speaks crdtmsg (FR-005), so a bare term on that link is a program error, not something to wrap silently. (The US1 `quic_one_bind.glp` stays a historical MVP proof; it only *loads* in the REPL suite and its xUnit one-bind tests use the default codec.)
- `Decode(byte[] payload)`: `MessageCodec.Decode(Binary, payload, understoodSectionTypes)` → rebuild the `crdtmsg/7` ground term. Loud-fail on bad codec-format-version byte, unknown `PayloadType`, unknown **must-understand** (odd) section, truncation, or trailing bytes — the `VersionPolicy` + `DecodeGuard` + `BinaryTermCodec`/`TlvSection` checks already enforce this (FR-007). `understoodSectionTypes` is empty for US2 (the codec understands no must-understand section), so an unknown even section is carried verbatim and an unknown odd section fails loud — exactly the T013/T014 split.

### A3 — Rich-text carriage (G2 / FR-006, "carry not apply")

A rich-text edit op is produced by the 041 model (`RichTextDoc`/`OpCodec`) as opaque bytes and ridden in a `section(EvenType, Bytes)`. Because the section value is opaque and carried verbatim, formatting marks the receiver does not understand survive the round trip for free (`MarkSet` never sees them at the link layer). The codec is therefore **not** scalar-only (it carries any section, including a rich-text-op section), satisfying G2 without the link running Fugue/Peritext. Applying the CRDT is the endpoint's job (US4).

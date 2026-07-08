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

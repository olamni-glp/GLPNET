# Handoff to link-layer #36 — framing / transport (T043, FR-006)

Framing and transport are **out of scope** for this result-envelope codec (FR-006). This
note records the one load-bearing fact #36 needs, **verified against the shipped code**.

## The fact (verified 2026-07-02)

`csharp/glp_link/reliability/FrameCodec.cs`:

- line 6: `public enum FrameKind : byte { Whole = 0, Fragment = 1 }`
- line 64: `private const int OffKind = 1;` — the **byte offset** (1) within the frame
  header at which the `FrameKind` byte is written (`h[OffKind] = (byte)kind`, line 116;
  read back + validated at lines 141–142).

So the `FrameCodec` header byte at `OffKind` is a **fragmentation discriminant**
(`Whole` vs `Fragment`) — it is **NOT** a payload-type discriminator. The frame header
carries no payload-type field at all.

## Consequence for #36

A shared wire that multiplexes payload kinds (029 IL programs, 038 result envelopes,
future kinds) needs its **own payload-type prefix byte** — the `FrameCodec` fragmentation
kind cannot serve that role. This codec already **reserves `0x11`** for `RESULT_ENVELOPE`
(029 IL program = `0x10`), so envelopes and IL programs are never confusable once #36 adds
a payload-type prefix (contract §4, §9; data-model §3).

Nothing in feature 038 depends on #36 landing; the reservation is forward-compatible.

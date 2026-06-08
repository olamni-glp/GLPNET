# `csharp/glp_link` — hand-authored C# link-layer overlay (feature 025)

The **clobber-safe** home (FR-057) for the multi-protocol peer-to-peer link layer's
C# reference. Authored **C#-first** (RULED B3); the Dart mirror under
`glp_runtime/lib/link/` follows once this reference passes.

## Why this lives outside `out/csharp`

codeconv's `dart_csharp` mirror regenerates one `.cs` per source `.dart` under
`out/csharp/lib/**` and `glp_runtime_net/`. Anything hand-authored at a path with a
Dart preimage is **overwritten** by the next mirror/scaffold/codegen. Nothing here
has a Dart preimage, so the regen oracle never names it as an output. See
`specs/025-multi-protocol-link-layer/contracts/architecture-context.md` §2.

It references the build-gated `out/csharp` product (`glp_runtime_net`) so it reuses
the byte-parity `PayloadSerializer`, `GlobalWritersTable`, and `MadContext` rather
than forking them.

## Layering (bottom → top)

```
ILinkTransport / ILinkEndpoint   seam/      raw opaque frames, per-scheme leaves   (T020)
        ▲
reliability sublayer             reliability/  framing+CRC+fragment (T021),
                                              seq/dedup/FIFO/reorder (T022),
                                              epoch/fence (T023), distributed GC (T024),
                                              backpressure N=8 (T025)
        ▲
MadContext seams                 (out/csharp) onMessageReady (out) / handleMadAssignment (in)
        ▲
base link primitives             primitives/  '_link_setup' '_link_send' '_link_recv'
                                              '_link_request' '_link_accept'
                                              '_link_monitor' '_link_close'           (Phase 3)
        ▲
GLP wrappers                      programs/lib/link.glp                                (T036)
```

`transports/` holds the per-scheme leaves (loopback T026, file, ws/wss, http2,
mqtt, coap, ble-l2cap — Phase 6); they are native/per-platform and **not**
auto-converted (FR-058).

## Build

```
dotnet build csharp/glp_link/GlpLink.csproj
```

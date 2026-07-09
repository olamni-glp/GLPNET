# Phase 0 Research — Design Decisions

Feature 050 is an integration of three shipped subsystems (025 link layer, 036 QUIC transport, 041 crdtmsg). Phase 0 records the decisions that the code-level integration turns on. Each is stated as Decision / Rationale / Alternatives, with the on-disk anchors verified during planning.

## Verified starting state (what already exists on disk)

- `csharp/glp_link/transports/QuicTransport.cs` — a **complete, genuine** `ILinkTransport` (real `System.Net.Quic`, ALPN `h3`, mutual shared-cert SPKI-SHA256 pin via `PinValidationCallback`, WS-over-one-bidi-stream, `IsSupported` no-fallback gate, multi-accept `CreateListenerAsync`/`QuicListenerHandle`). Built by `GlpLink.csproj`.
- The 025 kernels + wrappers are transport-agnostic: `LinkListenKernel`/`LinkSetupKernel` etc. call `link.Transports.Select(scheme)`; the GLP wrappers `server_listener`/`client_connector`/`link_close` live in `programs/self.glp` (lines ~469–571).
- The egress path is `LinkEgress.ShipGround` → `new PayloadSerializer("").SerializeAgentMessage(ground)` → `FrameCodec.Encode(payload, seq, …)` (`LinkEgress.cs:36,43`). Ingress is `LinkPump.RecvLoopAsync` → decode ground payload → extend the `In` stream on the runner thread.
- The composition root that registers transports is `out/csharp/glp_repl/Program.cs:30-35` — today it registers `TcpTransport` + `LoopbackTransport` only. It is documented as the ONE place allowed to reference both `glp_runtime_net` and `GlpLink`.
- `csharp/glp_crdtmsg/` — envelope model (`model/AbstractModel.cs`), TLV codec (`envelope/TlvSection.cs`), surface codecs (`model/SurfaceCodec.cs`, `BinaryTermCodec` canonical), capability slot (`header/CapabilitySlot.cs`, section type `0x20`, additive-optional v2), `cap/Macaroon.cs` with `Verify()`, rich text (`crdt/richtext/Fugue.cs`, `Peritext.cs`, `RichTextDoc.cs`). `GlpCrdtMsg.csproj` **references** `GlpLink.csproj` (direction: crdtmsg → link).
- `glpquick-cert/` holds the four shared-trust artifacts (`glpquick.pem`, `glpquick.key`, `glpquick.pfx`, `glpquick.fingerprint`).

## D-1 — How crdtmsg envelopes get onto the 025 QUIC link (the payload bridge)

**Decision**: introduce a host-side, per-link **`IPayloadCodec`** seam in `csharp/glp_link/seam/`. The default codec preserves today's behaviour (`PayloadSerializer` ground-term blob) for loopback/tcp. The `"quic"` link is established with a **`CrdtMsgPayloadCodec`** that encodes the ground message tree as a **041 crdtmsg canonical envelope** (`GlpRuntime.CrdtMsg.MessageCodec.Canonical`) and decodes the reverse. `LinkEgress.ShipGround` and `LinkPump`'s inbound decode call the link's codec instead of hard-coding `PayloadSerializer`. The kernels and GLP wrappers are unchanged; the codec is selected at establishment time from the link's scheme/options.

**Reference-cycle resolution**: `glp_crdtmsg → glp_link` already exists, so `glp_link` cannot compile-time-reference `glp_crdtmsg` (cycle). Two acceptable shapes, decided at `/bk-tasks`/implement:
- (preferred) `IPayloadCodec` lives in `glp_link`; the concrete `CrdtMsgPayloadCodec` lives in `glp_crdtmsg` (which already references `glp_link`) and is **injected at the composition root** — the same seam the transport registration uses. `glp_link` stays codec-agnostic; no cycle.
- (fallback) move the small crdtmsg envelope codec surface into a shared leaf project both reference. Heavier; only if injection proves insufficient.

**Rationale**: the 025 `ILinkTransport` doc explicitly says the seam "carries the already-byte-parity blob as an opaque frame and knows nothing about terms" — a per-link payload codec is the natural, minimal extension and keeps the transport truly agnostic. It satisfies FR-005/FR-006/FR-007 (crdtmsg envelope + rich-text CRDT + lossless/loud-fail decode) without touching the transport or any GLP kernel (FR-019 / IV-a respected). The rich-text case (FR-006) is carried because `CrdtMsgPayloadCodec` uses the full crdtmsg model, not a scalar shortcut.

**Alternatives considered**:
- *GLP program emits crdtmsg envelope bytes as a ground blob, link ships bytes verbatim* — keeps `glp_link` untouched, but pushes wire-format construction into GLP and makes "is this a valid crdtmsg envelope on the wire?" depend on program discipline rather than the transport. Rejected as fragile and harder to assert (SC-002).
- *Reuse `glp_crdtmsg/route/QuicLinkTransport.cs`* — that is the side-process `glp_quick_host` path (see D-4); conflicts with the in-process C# REPL ruling and the no-shadow-layer mandate. Rejected.

**Residual clarification** (for `/bk-analyze` to surface): the 025 `FrameCodec` wraps every payload with its own reliability header (length prefix + CRC + sequencing) for reassembly/ordering. SC-002 says "messages observed on the wire are well-formed crdtmsg envelopes." Confirm the intended reading: the **L5 application payload** inside each frame is a crdtmsg envelope (recommended, and what this design delivers), vs. a literal requirement that raw QUIC-stream bytes are a bare crdtmsg envelope with no 025 framing (would mean bypassing the 025 reliability sublayer — not recommended, loses FR-016 duplicate-suppression/ordering).

## D-2 — Which crdtmsg surface carries the capability slot on the wire

**Decision**: the `"quic"` wire payload uses a crdtmsg surface that **carries the capability slot**. Today `BinaryTermCodec.WriteHeader` **throws** when `Header.CapabilitySlot` is non-null (capability rides only on the JSON/DTO base64 surface, section `0x20` v2). Since FR-008 requires the macaroon to ride in the envelope's capability slot on the QUIC link, the implementation MUST either (a) send on a surface that already carries the slot, or (b) extend the binary canonical surface to encode the additive-optional `0x20` capability section at schema v2.

**Rationale**: FR-008 + the beacon static-macaroon model put the macaroon *in the envelope*, not in a side channel. The capability slot (`CapabilitySlot.Attach`, section `0x20`) is the designed home. Option (b) is preferred for a genuine binary wire, but it is a **change inside feature 041's codec** — so it is **propose-first / 041-scoped**: coordinate with the 041 owner, keep it additive-optional (v1 readers skip `0x20` by length, per `VersionPolicy` accepting `[1,2]`), and do not silently fork the format.

**Alternatives considered**:
- *Macaroon as a 025 link option / handshake side-channel outside the envelope* — simpler wiring but violates FR-008's "rides in the envelope's capability slot." Rejected.
- *Ship on JSON surface* — carries the slot today with zero codec change, but JSON is not the canonical/signing form and inflates the wire. Acceptable as an MVP stopgap if (b) is deferred; flag the trade-off.

**Residual clarification**: confirm binary-v2-capability (option b, coordinate with 041) vs. JSON-surface stopgap (option a). This drives whether 050 has an 041-coordination task.

## D-3 — Performance / reliability targets (SC-005)

**Decision**: adopt the spec's default working targets — **median round-trip < 50 ms on the LAN wire; ≥ 1000 messages sustained with zero loss** — as the plan's targets, explicitly marked provisional.

**Rationale**: `/bk-clarify` was skipped in this pipeline run (the user chose plan→tasks→analyze directly). The spec (Assumptions + SC-005) states these are placeholders to confirm at clarify. Proceeding on the documented defaults keeps the pipeline moving; the numbers are easy to re-tune in the test program.

**Residual clarification** (NEEDS CLARIFICATION, carried into plan Technical Context): confirm the SC-005 latency/throughput numbers before the cross-host acceptance run is treated as a firm pass/fail gate. All other success criteria (SC-001..SC-004, SC-006..SC-008) are firm.

## D-4 — Which QUIC transport does 050 drive (reconciling the two)

**Decision**: 050 drives **`csharp/glp_link/transports/QuicTransport.cs`** — the 036 genuine **in-process** QUIC leaf — registered into the REPL `LinkRuntime`. It does **not** use `csharp/glp_crdtmsg/route/QuicLinkTransport.cs`.

**Rationale**: the spec's settled clarification (Q1) is that the **C# reference REPL terminates genuine QUIC in-process**; and FR-019 / the Assumptions bar bespoke evaluators or shadow layers. `glp_crdtmsg/route/QuicLinkTransport.cs` (048 work) reaches QUIC by spawning the `glp_quick_host` **side-process** — a different topology (out-of-process) and the crdtmsg-router's own path, not the 025 `LinkRuntime` kernel path. Using it would make the REPL not terminate QUIC in-process and would route around the genuine kernels.

**Implementation note / task**: a `/bk-tasks` task MUST read `glp_crdtmsg/route/QuicLinkTransport.cs` + `route/LinkTransport.cs` and record explicitly how the crdtmsg router relates to the 025 link path, so the two are not accidentally double-wired. If the router already encodes crdtmsg envelopes usefully, reuse its codec surface for `CrdtMsgPayloadCodec` (D-1) rather than duplicating.

**Alternatives considered**: side-process via `glp_quick_host` (rejected per above; it remains the Profile-A path for endpoints that cannot terminate QUIC in-process, e.g. some MAUI apps — that is FR-013a interop, not the delivered REPL endpoints).

## D-5 — Mesh topology, endpoints, and MAUI interop readiness

**Decision**: the all-pairs full mesh is **5 C# endpoints** ⇒ **C(5,2) = 10 full-duplex links** (10 QUIC bidi streams, one per peer-pair — no link doubling, per the spec clarification). This feature **delivers 2 glpnet C# REPL endpoints** + the role-parameterized GLP test program; the **3 pre-built MAUI C# apps** (Android tablet, Android phone, Windows app) are **external participants** — 050 must be *ready to accept* them (stand up listeners/acceptors that honor the mutual-pin QUIC + macaroon + crdtmsg contract) but does not build or modify them (FR-013a).

**Rationale**: directly from the spec clarifications (2026-07-08) and FR-013/FR-013a. Each endpoint is a genuine GLP-program-hosting C# REPL/app (no bespoke CLI harness); every link is opened by a GLP goal (FR-003/FR-012). The mesh server role reuses `QuicTransport.CreateListenerAsync`/`QuicListenerHandle` (already built for the multi-accept case) so one bound UDP port accepts many isolated client links.

**Interop-readiness contract** (the surface the pre-built apps rely on) is pinned in `contracts/mesh-test-harness.md`: shared cert + SPKI pin from `glpquick-cert/`, static macaroon root distributed out-of-band, crdtmsg envelope wire format, ALPN `h3`, one WS per bidi stream. An app that cannot terminate QUIC in-process reaches the mesh via the 036 Profile-A WS-to-QUIC side-process — the genuine-QUIC requirement of FR-002 still holds on every link (in-process-vs-side-process reach per endpoint is a plan-level detail, not a relaxation).

**Alternatives considered**: doubling to 20 unidirectional links (rejected — a GLP link is inherently full-duplex `ch(In, Out?)` over one bidi stream, spec clarification); building the MAUI apps here (rejected — explicitly out of scope, FR-013a).

## Summary of residual clarifications carried forward

1. **D-1**: the SC-002 reading — L5 payload is a crdtmsg envelope (recommended) vs. bare-stream crdtmsg with no 025 framing.
2. **D-2**: capability-on-wire surface — extend binary to v2 (041-coordinated) vs. JSON-surface stopgap.
3. **D-3**: SC-005 latency/throughput numbers (provisional defaults in use).

These are intentionally left as explicit open items for `/bk-analyze` to flag and for the user's "apply top remediations" pass to resolve, rather than silently guessed.

## Implementation-time findings (US1 MVP — /bk-implement, 2026-07-09)

### T002 — QUIC support + cert probe (D-5 note)
Probed on the Olamnit dev host: `QuicListener.IsSupported == QuicConnection.IsSupported == true` (genuine QUIC available). `glpquick-cert/glpquick.pfx` loads via `X509CertificateLoader.LoadPkcs12` **with private key**; the computed `base64(SHA-256(SPKI))` pin equals `glpquick-cert/glpquick.fingerprint` **exactly** (`0LOmLNM0HYv79Rkoasuu6L4MKGRyg7axgJufbZBcyTo=`). The shared cert is a **1-year** cert (2026-07-08 → 2027-07-08); trust is the SPKI pin, not expiry (the 036 `PinValidationCallback` waives chain/name errors) — consistent with FR-010 "permanent credential, pin-anchored." ⇒ US1's loopback-UDP xUnit tests drive a **real MsQuic handshake** (analyze note A2), not a shim.

### T004 — crdtmsg-router vs 025-leaf reconciliation (D-4 confirmed)
`csharp/glp_crdtmsg/route/QuicLinkTransport.cs` implements a **different** `ILinkTransport` — namespace `GlpRuntime.CrdtMsg.Route`, **peer-name-keyed** mailbox (`LocalPeer`/`Members`/`SendAsync(peer,bytes)`/`Inbound`), ALPN **`glp-colab`**, a **per-peer** pin dictionary, and its own private control/box/presence stream protocol (per-box multiplexing). It is NOT the 025 `GlpRuntime.Link.Seam.ILinkTransport` (scheme-keyed, `ListenAsync`/`ConnectAsync`→`ILinkEndpoint`, ALPN **`h3`**, single shared cert+pin, WS-over-one-bidi-stream). **No double-wiring risk**: 050 registers ONLY into the 025 `TransportRegistry` at the REPL composition root; the two seams never meet. Note for US2 (T017): the crdtmsg router forwards **opaque** frame bytes verbatim (`route/LinkTransport.cs`: "Router forwards header+payload bytes verbatim") — there is **no reusable envelope-codec surface** in the router to lift for `CrdtMsgPayloadCodec`; that codec will wrap `GlpRuntime.CrdtMsg` `MessageCodec` directly (D-1).

### T010 — kernels reach the quic leaf UNCHANGED (FR-001/FR-019)
Traced: `LinkSetupKernel.LinkSetup` → `LinkTerms.ParseLinkId` (`LinkScheme.Of("quic")` → `LinkScheme.Quic`) → `LinkEstablish.WireEstablishedLink` → `Establish` → `link.Transports.Select(id.Scheme)` → the registered `QuicTransport` (`LinkSetupKernel.cs:51`). No kernel or GLP-wrapper edit was needed — the only US1 production changes are the additive `SharedCertMaterial` loader and the composition-root registration in `out/csharp/glp_repl/Program.cs`. `_link_setup` blocks the runner thread on the real handshake via `ConnectAsync().GetAwaiter().GetResult()` (bounded by `ConnectTimeout`); the parked listener accepts on the thread pool — no self-deadlock (verified green by `QuicLinkOneBindTests`).

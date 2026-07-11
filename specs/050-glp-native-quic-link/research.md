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

### T013–T021 — crdtmsg on the wire (US2, /bk-implement 2026-07-10)
The D-1 Term↔Message mapping gap (flagged at the earlier `/bk-implement` STOP) was resolved by Gabi's **Option A** decision and pinned in `contracts/wire-payload-crdtmsg.md` **Addendum** (grammar `crdtmsg(MsgId, From, To, Seq, policy(T,W,E), CrdtModel, Sections)`; rich-text = **carriage not apply**). `CrdtMsgPayloadCodec` (`csharp/glp_crdtmsg/bridge/CrdtMsgPayloadCodec.cs`) transcribes that ground term ↔ `MessageCodec.Canonical` (binary/canonical surface) losslessly and fail-closed; injected for `LinkScheme.Quic` at the REPL composition root (`out/csharp/glp_repl/Program.cs`), loopback/tcp keep the default blob. xUnit `CrdtMsgOnLinkTests` green: round-trip incl. a rich-text edit op over a **real MsQuic loopback link** (235 ms — genuine handshake, not a vacuous skip), malformed loud-fail (bad version byte / unknown must-understand tag / truncation / trailing / non-`crdtmsg` term), and the on-wire L5 payload decodes as a `PayloadType.CrdtMessage` envelope with the op section crossing verbatim (195 ms). glp_link.tests 121/121, glp_crdtmsg.tests 114/114.

### T021 — FrameCodec wraps the crdtmsg bytes UNCHANGED; SC-002 reading confirmed (L5-payload-is-envelope)
`LinkEgress.ShipGround` (`LinkEgress.cs:36→43`) computes `payload = handle.Codec.Encode(ground)` then `FrameCodec.Encode(payload, seq, MaxFrameBytes)` — the 025 reliability sublayer (length + CRC + seq, reassembly, ordering) frames the codec's bytes **verbatim**; the codec never touches the frame header and the frame layer never touches the payload (FR-016 duplicate-suppression/ordering preserved). **Empirical confirmation** (T015): the peer captured the framed wire bytes, `FrameCodec.ParseFrame` + `FrameReassembler.Accept` stripped the framing, and the recovered payload decoded cleanly as a well-formed crdtmsg `Message` — so the crdtmsg envelope is exactly the **L5 application payload inside each frame**. This settles the D-1 residual clarification in favour of the recommended reading (crdtmsg envelope at L5, NOT bare QUIC-stream bytes bypassing 025 framing); the alternative would have discarded FR-016. No `FrameCodec`/reliability change was needed.

### T022–T028 — macaroon gate (US3, /bk-implement 2026-07-11); D-2 RESOLVED

**D-2 resolution (T025): the dichotomy was false — no 041 codec change and no JSON stopgap.** D-2
framed the choice as "extend the binary surface to carry `Header.CapabilitySlot` at v2
(041-coordinated, propose-first) vs. ship on the JSON surface (stopgap)". Verified on disk: 041's own
shipped capability-slot design (`header/CapabilitySlot.cs`, 041 T048) does **not** use the
`Header.CapabilitySlot` field on the wire — `CapabilitySlot.Attach` rides the capability as a
**reserved even/ignorable TLV *section*** (`SectionType = 0x20`) and stamps `SchemaVersion = 2`. The
binary canonical surface (`BinaryTermCodec`) carries TLV sections verbatim and `VersionPolicy`
accepts schema `[1,2]`, so the binary wire **already** carries the v2 additive-optional capability
slot with zero 041 codec changes; a v1 reader skips `0x20` by length (BB-VER-2). The
`Header.CapabilitySlot` **field** (whose non-null case `BinaryTermCodec.WriteHeader` loud-fails)
remains the JSON/DTO+CBOR-only representation and stays `null` on the binary wire. Confirmed
empirically by `MacaroonGateTests.CapabilitySlot_RidesBinaryCanonicalSurface_AsSection0x20`
(decodes the gated wire bytes with the UNTOUCHED `MessageCodec.Binary`: slot present, v2, header
field null). The propose-first / 041-coordination concern therefore does not arise — nothing in
041's codec changed. (One **additive** edit inside `cap/Macaroon.cs`: a `FromWire` rehydration
factory so `MacaroonCodec` can reconstruct a received macaroon with its wire-claimed signature;
`Verify` still detects tampering via the HMAC chain. Minting/verification semantics untouched.)

**Gate architecture (T026/T027)**: mirrors the D-1 codec seam. `ICapabilityGate` +
`CapabilityRefusedException` + allow-all `DefaultCapabilityGate` live in `glp_link/seam/`;
`CapabilityGateRegistry` (scheme→gate, default allow-all — loopback/tcp unchanged) on
`LinkRuntime`; the one concrete `MacaroonLinkGate` lives in `glp_crdtmsg/bridge/` and is injected
for `LinkScheme.Quic` at the REPL composition root. **Establishment (FR-008)**:
`LinkEstablish.WireEstablishedLink` consults the gate BEFORE any transport endpoint is opened
(verify-before-act; a refused establishment opens nothing) and fails closed through the existing
graceful Abort path. **Maintenance (FR-009, T027)**: every outbound envelope's slot is attached
codec-side (capability is codec-fixed carriage, never term-visible — extends the Addendum A1
"codec-fixed" list); every inbound delivery is a gated action — the gated `CrdtMsgPayloadCodec`
extracts + `Macaroon.Verify()`s the slot, records the outcome, strips the slot, and on failure
(absent/malformed/tampered/expired/unsatisfiable/un-understood) records
`ProvenanceOutcome.Refused` and throws `CapabilityRefusedException`, which `LinkPump` catches to
refuse JUST that action — the link, pump, and run stay graceful (proven by
`GatedActionMidSession_…_RunStaysGraceful`: two refused actions, then a valid one still delivers).
100% of gated actions record a provenance row (041 C19), refusals as the distinct `Refused`.

**Root material (T028)**: `glpquick-cert/glpquick.macaroon.key` (base64, 32-byte minimum) loaded
fail-closed by `StaticMacaroonMaterial.LoadFromRepo()` (reuses the `SharedCertMaterial` walk-up);
the presented static macaroon is minted from it at boot (`location "glpquick"`, identifier
`"glpnet-mesh"` — the beacon static-macaroon model, not per-session). Caveat vocabulary understood
by the gate (fail-closed on any other key): `action` ∈ {establish, deliver}, `peer`, `expires`
(the 041 `CapabilityTests` numeric-clock idiom; the gate's clock is injectable for deterministic
tests). xUnit: glp_link.tests 129/129 (8 new), glp_crdtmsg.tests 114/114.

### T029–T041 — full mesh + graceful termination (US4/US5, /bk-implement 2026-07-11)

**US4 host capability (T029–T031)**, all over a genuine MsQuic handshake (skip-guarded):
- **T029 mesh** (`QuicMeshTests`): `QuicTransport.CreateListenerAsync` → `QuicListenerHandle.AcceptAsync`
  brings up N isolated client links from ONE UDP port; each is its own `QuicConnection` + bidi stream,
  so killing client 0's link leaves every sibling exchanging both directions untouched (FR-013 isolation).
- **T030 reliability** (`QuicReliabilityTests`): a redelivered framed message (same `msg_id`/seq) is an
  idempotent no-op at `InboundOrdering`'s high-water — the reader extends `In` EXACTLY ONCE
  (exactly-once reactivation, FR-016); and a send into a vanished peer surfaces
  `tempFail(LinkId, Reason)` on the establishment `Faults` stream (reported, never swallowed).
- **T031 cyber** (`QuicCyberTests`): a rogue peer presenting its own (non-pinned) cert is rejected at the
  mutual SPKI-pin handshake and the listener stays healthy for a genuine member (zero false accepts,
  two-sided); a tampered SIGNED block that crossed the real wire fails `SealSet.Verify` (041 Ed25519
  whole + Biscuit-chained sub-seals) while its untampered counterpart verifies — the refusal recorded as
  `ProvenanceOutcome.Refused` (zero false accepts, zero false rejects).

**US4 GLP program (T032–T036)** — `programs/tests/quic/quic_mesh.glp`: role-parameterized
(`main(node_a|node_b, Report)`), opens each peer-pair link as a GLP goal via the UNCHANGED 025 wrappers
(`server_listener`/`client_connector`), and — because post-US2 the `"quic"` wire is a crdtmsg envelope —
ships `crdtmsg/7` GROUND terms (a `mk_ping` head-constructor over the addendum-A1 grammar), NOT bare
values. Full-duplex per node (`gen_pings` onto `Out`, `collect` off `In`); `monitor_link` collects the
per-link fault stream (T034/T035 GLP-level observation of security/reliability, with the crypto proven
host-side in T031/T030). Loads clean through the full REPL pipeline (SRSW + type-check + compile) — added
to `test/run_all_tests.sh` Section B. Perf targets (T033/SC-005) are structural + provisional (research
D-3), confirmed at the T043 two-host run. Interop-readiness (T036/FR-013a): the delivered node_a/node_b
stand up listeners honoring the mutual-pin QUIC + macaroon + crdtmsg contract so the 3 pre-built MAUI apps
can join; this program neither builds nor drives them. FR-019 respected — no new kernel/primitive; `"quic"`
is data, the mesh reuses `link_close`/`link_monitor` unchanged.

**US5 graceful termination (T037–T041)** — `QuicTeardownTests` over genuine QUIC:
- **T037**: in-flight envelopes drain, then `Out = []` (the canonical graceful stream-end) runs an ordered
  teardown — the peer reads `null`, distributed GC reclaims (`LinkReclaimer.IsReclaimed`), registry back to
  baseline, zero crashes.
- **T038**: an abrupt `link_close` kernel teardown releases the UDP port so an immediate re-run
  re-establishes on the same port with no leftover listener/connection (FR-018).
- **T039**: a peer that vanishes mid-drain surfaces the fault on the monitor stream (walk past the `ok`
  baseline to the `closed`/`tempFail`/`permFail` term) and teardown still completes (idempotent GC, no
  crash). Note: `DisposeAsync` is a GRACEFUL WS close (no fault) — the fault is driven by the failed
  egress SEND into the dead connection, mirroring T030. The program's `close_mesh_link/1` wraps the
  existing `link_close` for a stop-requested run (no new kernel). Gotcha recorded: the registry/reclaimer
  key is the GROUND establishment LinkId (nonce from the term), NOT the transport endpoint's internal
  per-connection nonce.

### T010 — kernels reach the quic leaf UNCHANGED (FR-001/FR-019)
Traced: `LinkSetupKernel.LinkSetup` → `LinkTerms.ParseLinkId` (`LinkScheme.Of("quic")` → `LinkScheme.Quic`) → `LinkEstablish.WireEstablishedLink` → `Establish` → `link.Transports.Select(id.Scheme)` → the registered `QuicTransport` (`LinkSetupKernel.cs:51`). No kernel or GLP-wrapper edit was needed — the only US1 production changes are the additive `SharedCertMaterial` loader and the composition-root registration in `out/csharp/glp_repl/Program.cs`. `_link_setup` blocks the runner thread on the real handshake via `ConnectAsync().GetAwaiter().GetResult()` (bounded by `ConnectTimeout`); the parked listener accepts on the thread pool — no self-deadlock (verified green by `QuicLinkOneBindTests`).

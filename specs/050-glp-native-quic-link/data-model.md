# Phase 1 Data Model — Entities Mapped to Real C# Types

Feature 050 adds almost no new data types; it composes existing ones. This maps each spec **Key Entity** to the concrete on-disk type (or the one new host-side type) and its validation rules. "REUSE" = shipped and unchanged; "NEW" = introduced by 050; "EXTEND (coordinated)" = a change in another feature's code, propose-first.

## Entities

### QUIC link — `link_id("quic", Endpoint, Nonce)`
- **GLP surface**: `LinkId ::= link_id(Scheme, Endpoint, Nonce)`, `Endpoint ::= String ; ep(String, Integer)`, `Nonce ::= Integer ; String` (025 `contracts/link-primitives.md`).
- **C# (REUSE)**: `csharp/glp_link/seam/LinkId.cs` — `sealed record LinkId(LinkScheme Scheme, LinkAddress Endpoint, LinkNonce Nonce)` (value equality = identity/idempotency). `LinkScheme.Quic` already exists (`seam/LinkScheme.cs`). Term↔value in `primitives/LinkTerms.cs` (`ParseLinkId`/`ToTerm`).
- **Decomposition**: a link is `ch(In, Out?)` in GLP — inherently full-duplex, one WS over one QUIC bidi stream.
- **Validation**: scheme must be registered (`TransportRegistry.Select` throws `KeyNotFoundException` if not — FR-001); `ep(Host, Port)` required for quic (`QuicTransport.RequirePort` throws otherwise); no TCP/loopback fallback (FR-002).

### LinkRuntime `"quic"` transport registration
- **C# (REUSE + WIRE)**: `csharp/glp_link/primitives/TransportRegistry.cs` `Register(ILinkTransport)`; the leaf is `transports/QuicTransport.cs` (`SupportedSchemes = { LinkScheme.Quic }`). **New wiring** at `out/csharp/glp_repl/Program.cs:30-35`: `link.Transports.Register(new QuicTransport(sharedCert, spkiPin))`.
- **Validation**: double-registration of a scheme throws (ambiguous config — not papered over); `QuicTransport.IsSupported` gates real-QUIC availability.

### crdtmsg envelope (ground-relay)
- **C# (REUSE)**: `csharp/glp_crdtmsg/model/AbstractModel.cs` — `record Message(int SchemaVersion, byte PayloadType, Header Header, IReadOnlyList<Section> Sections, CrdtModel CrdtModel)`; `record Header(string MsgId, string From, string To, long Seq, RoutingPolicy Policy, byte[]? CapabilitySlot)`; `record Section(long TypeNumber, byte[] Value)`. TLV codec `envelope/TlvSection.cs` (odd type_number = must-understand; even = skippable-by-length). Surface codecs `model/SurfaceCodec.cs` (`MessageCodec.Canonical` = binary).
- **Validation (FR-007)**: `envelope/DecodeGuard.cs` (`CheckPayloadTypeKnown`, `CheckMustUnderstand`), `envelope/VersionPolicy.cs` (codec-format-version hard gate; schema-version `[1,2]` additive skip), consume-all-or-throw (`BinaryTermCodec.Decode` trailing-byte check; truncation throws `CrdtMsgException`).

### Rich-text CRDT payload (Fugue + Peritext)
- **C# (REUSE)**: `csharp/glp_crdtmsg/crdt/richtext/Fugue.cs` (`FugueTree.Integrate/Delete/Visible/Text`), `Peritext.cs` (`MarkSet.Add/Remove/Active`, **`UnknownActive(knownTypes)`** — unknown marks preserved), `RichTextDoc.cs` (`Apply(Op)` on `seq_insert`/`seq_delete`/`mark_add`/`mark_remove`). Op = `crdt/Op.cs` `record Op(Dot Id, …, byte[] Payload, string Box)`.
- **Validation (FR-006)**: a scalar-only path fails the requirement; the payload MUST carry the rich-text model so unhandled formatting marks survive the round trip (`MarkSet` drops nothing for being unrecognised).

### Macaroon capability
- **C# (REUSE)**: `csharp/glp_crdtmsg/cap/Macaroon.cs` — `Create(rootKey, location, identifier)`, `AddCaveat(Caveat)`, `record Caveat(Key, Op, Value)`, and the verify-before-act `bool Verify(byte[] rootKey, IReadOnlyDictionary<string,string> context, IReadOnlySet<string> understoodKeys)` (fail-closed: un-understood ⇒ false, unsatisfiable ⇒ false, `FixedTimeEquals` integrity). Refusal recording: `cap/Provenance.cs` (`ProvenanceLog`, `ProvenanceOutcome.Refused`).
- **Carriage**: rides in the envelope capability slot (`header/CapabilitySlot.cs`, section `0x20`, additive-optional v2). See research D-2 for the on-wire-surface decision.
- **Validation (FR-008/FR-009)**: absent/tampered/expired/unsatisfiable/un-understood ⇒ fail closed, recorded refusal, no crash.

### Trunk / shared certificate
- **On disk (REUSE)**: `glpquick-cert/{glpquick.pem,glpquick.key,glpquick.pfx,glpquick.fingerprint}`. Loaded as `X509Certificate2` (via `X509CertificateLoader.LoadPkcs12`), pin = `QuicTransport.SpkiPin(cert)` = `base64(SHA256(SPKI))`.
- **Validation (FR-010/FR-011)**: permanent credential — **no time-boxed carve-out**; mutual pin via `QuicTransport.PinValidationCallback` (waives only no-CA-chain + hostname-mismatch; never blanket-accepts; `FixedTimeEquals` on the pin). Rogue/non-pinned peer ⇒ handshake rejected.

### Fault stream
- **GLP surface (REUSE)**: `server_listener(LinkId, Link?, Faults?)` binds a `Faults` stream; `link_monitor(LinkId, Faults?)`. Fault lattice terms via `primitives/LinkTerms.cs` (`Ok/Closed/TempFail/PermFail`, `FromSignal`); delivery via `primitives/LinkFaults.cs` / `LinkPump`.
- **Validation (FR-016)**: faults reported, never swallowed.

### Mesh endpoint
- **Concept**: one C# glpnet REPL/app in the mesh. Five total = 2 delivered glpnet C# REPL instances (`out/csharp/glp_repl`) + 3 pre-built MAUI C# apps (external participants).
- **Server role (REUSE)**: `QuicTransport.CreateListenerAsync` → `QuicListenerHandle.AcceptAsync` (many isolated per-client links from one UDP port).

### Host pair
- Olamnit **192.168.0.136** and gavri **192.168.0.108** — the two physical Windows-11 demo hosts across which the 5 endpoints are distributed.

## New type introduced by 050

### `IPayloadCodec` (host-side seam) + `CrdtMsgPayloadCodec`
- **NEW**: `csharp/glp_link/seam/IPayloadCodec.cs` — `byte[] Encode(Term ground)` / `Term Decode(byte[] payload)` (below GLP; term-in/bytes-out). Default impl = current `PayloadSerializer` behaviour (loopback/tcp).
- **NEW**: `CrdtMsgPayloadCodec` (in `glp_crdtmsg`, injected at composition root — see research D-1 cycle resolution) — bridges the GLP ground message tree ↔ `MessageCodec` crdtmsg envelope, carrying the capability slot (D-2) and the rich-text model (FR-006).
- **Not a GLP kernel or primitive** — no language-authority approval needed (FR-019 / Constitution IV-a).

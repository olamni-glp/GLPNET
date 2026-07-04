# Prior-Art Sibling Scan — CRDT Multi-Format Messaging Epic

> **Run date:** 2026-07-03
> **Recency cutoff:** 2026-06-05 (findings after this date are "actively worked on"; earlier ones are historical/tangential and flagged as such)
> **Role:** Curating (three-role prior-art scan team) — consolidates raw findings from 8 per-repo readers
> **Units scanned:** `glpnet` · `buildkit-beacon` · `mstack` (MSTACK + mstack-coop) · `olamnit` (olamnit + olamnit-assistant + coop) · `buildkit` · `crucible` · `qhstate` (qhstate + qhstate-Yngenios + qhstate-coop) · `research-loose` (_marathon-synthesis + syst-lit-rev-agentic-protocols)
> **Status:** Advisory consolidation. Confidence + recency are carried on every claim; keyword-collision traps are called out explicitly.

---

## 1. Executive summary — is the target language already (partly) defined?

**Yes — substantially, and in more than one place — but no single unit holds the whole thing, and the pieces do not yet agree on a wire.** The eight directive signals (multi-format encodings, routing/capability header, multiple signatures, version tolerance, QUIC/HTTP-3 transport, durable/CRDT store, schema architecture, roadmap/interview provenance) are each satisfied by *at least one* actively-developed unit since the cutoff. Two findings are near-complete definitions of the entire epic:

- **`qhstate` spec-036 "Extensible Multi-Format CRDT Message Architecture"** (committed 2026-06-27/28) is an almost 1:1 match: head-tagged versioned blocks, lossless JSON↔binary(CBOR-TLV)↔YAML surfaces, three selectable CRDT models, per-block macaroon+ed25519 sealing, skip-unknown-by-length, an informal plaintext schema-definition language, and a ratified 50-pattern research corpus. **This is the strongest single prior-art artifact and should anchor F3 synthesis.**
- **`buildkit` spec-047 "yngenious daemon"** (committed 2026-07-03) issues **Decision D2 — unify** the three divergent envelopes (beacon v1/v2, olamnit 48-byte header, GLP L5 `GlpMessage`) into ONE canonical header+payload carrier. **This is the reconciliation mandate the epic exists to fulfill** — it names the exact fragmentation to be resolved.

The **critical caveat**: these live in *different repos with different wires*. beacon/crucible/mstack/qmedit use a **JSON envelope** (`{v,type,target,payload,macaroon}` family); olamnit uses a **48-byte fixed binary header**; glpnet uses a **ground-GLP-term L5 envelope** over real QUIC. They converge on *concepts* (verify-before-act macaroon, append-only version-tolerant header, msg_id/seq dedup, canonical round-trip) but not on *bytes*. The epic's net-new work is **the unification** (buildkit D2) plus the parts nobody has built: an interchange across *more than two* encodings, semantic tombstones / repair segments, and CRDT-of-the-message-itself (vs CRDT-of-the-roadmap-store).

**Provenance is solid and recent:** the durable-mesh-messaging brief (`glpnet`, captured 2026-06-28), the verbatim Marcelle+Gabi principal interview (`mstack`, 2026-06-18), and the yngenios SOURCE-MATERIAL brief (`buildkit`, 2026-07-03) all name the principals and scope the epic.

---

## 2. The epic decoded — eight signals restated as acceptance criteria

| # | Signal | Restated as an acceptance criterion |
|---|---|---|
| S1 | Multi-format / interchangeable encodings | One logical message losslessly round-trips across ≥2 (ideally JSON / binary / YAML / Gleam-term) surfaces; a promotion between surfaces changes no consumer. |
| S2 | Routing + capability header (macaroons/amulets) | A fixed/canonical header carries routing (from/to/seq/waypoints/excludes) + a capability token verified **before** any side effect. |
| S3 | Multiple signatures (whole + sub-content) | Whole-message attestation AND per-block/per-hop attestation coexist and both verify after round-trips; transparent payload passthrough. |
| S4 | Forward/backward version tolerance | A newer-schema message is processed by an older reader (skip-unknown by length/tag); additive-only evolution; repair/tombstone for removed content. |
| S5 | QUIC/HTTP-3 optimized binary transmission | Real QUIC/HTTP-3 + WS transport with binary framing (CRC/length/seq), transport-agnostic body. |
| S6 | CRDT substrate (messages/stores/documents) | Conflict-free, order-independent, idempotent convergence for the message/content/document store. |
| S7 | Schema / format architecture | A schema-definition language + message-type registry declaring payload shapes. |
| S8 | Roadmap + Marcel/Gabi interview provenance | Verbatim principal capture + roadmap-intake brief grounding the epic. |

---

## 3. Epicenter: GLPNET existing work

`glpnet` is the epicenter — it holds the epic-intake brief **and** the built codec/link/capability machinery, with commits through 2026-07-03.

- **Link layer (spec 025, multi-protocol-link-layer)** — ground-relay discipline (no `_w`/`_r` placeholders cross the wire), per-link sequence numbers. Substrate the L5 envelope rides. CRDT/OT explicitly scoped *above the seam, out of scope* in 025 tutorials. *(mtime 2026-06-08, pre-cutoff foundation; still load-bearing.)*
- **IL/result/frame codecs (029/038)** —
  - **029 IL-codec-spike**: `BytecodeProgram ↔ bytes` round-trip with a **Lean 4 `decode∘encode = id` sorry-free proof** — machine-checked codec-soundness prior art. *(pre-cutoff spec; cited by 038 as oracle; confidence med.)*
  - **038 result-codec + FrameCodec (2026-07-02)**: **cross-runtime byte-parity** (Dart/C#/Gleam byte-identical), frozen versioned ISA (FR-010), FrameCodec frame-envelope (`0x01` ver byte, 22-byte BE header, per-chunk CRC-32, MTU frag, 64 MiB guard), PayloadSerializer tag scheme (Constant=1/Variable=2/Struct=3/List=4). **FR-006 records the open handoff: `FrameCodec.cs:64 OffKind` is fragmentation, not a payload-type discriminator → a payload-type prefix byte is still needed.** *(confidence high.)*
- **Gleam port (031/032)** — `glp_gleam/src/glp/codec/{term_codec,result_envelope}.gleam` implement the 038 codec on the Gleam/BEAM term+heap layer (034). A Gleam-term surface exists for S1.
- **HTTP/3-QUIC-WS (spec 036, 2026-07-02)** — genuine QUIC handshake (System.Net.Quic/MsQuic), RFC 6455 WS over one bidi QUIC stream, SPKI-SHA-256 cert pinning. **L5 envelope schema** `{msg_id, from, to, seq, payload}`, `to ∈ endpoint_id | "broadcast"`, payload = ground GLP term. Failure contract enumerates terminal faults (cert_mismatch / alpn_version_mismatch / udp_blocked / link_dropped / over_capacity). *(confidence high — this is the real transport the whole family points at.)*
- **rcopy mesh + tmsg codec (spec 040, 2026-07-03)** — **`tmsg(Kind,Field…)` single-codec / single encode-decode point** shared by all kinds (chat/page/pinpoint/form_def/repl_goal/rcopy_*); **additive kinds, unrecognized `tmsg` surfaced as an informational line not a crash, bare legacy chat text decodes as `chat`** (S1 + S4). Capability = **feature-036 authenticated `PeerId`** keying all permission/quota/landing decisions (`rcopy_verdict reject(quota|perm|path)`) — the glpnet analogue of macaroon/amulet (S2).
- **gleam_quic transport** — `glpq_ffi.erl` reassembles >1 MiB frames (misroute fixed 2026-07-03).

**glpnet net:** the transport (S5), the cross-runtime binary codec + framing (S1/S5), the additive message-type registry (S1/S4/S7), and identity-keyed capability (S2) are BUILT here. Missing here: macaroon/amulet token type (referenced only, lives in beacon/mstack/olamnit), CRDT-of-messages (deferred), multi-signature, repair segment.

---

## 4. Roadmap + interview provenance (S8)

| Artifact | Unit | Date | What it is |
|---|---|---|---|
| `docs/roadmap-intake/durable-mesh-messaging-protocol.md` | glpnet | captured **2026-06-28** (Gabi brief), mtime 2026-07-02 | **THE epic intake brief.** Multi-hop resilient mesh, Kafka-style signal-then-fetch, mailbox/topic, replica advertisement, WAL + tiered PGlite→DuckLake store, retention tiers (ephemeral/time-windowed/permanent), friend-registry discovery, dead-letter queue. Names a "Policy DSL" (line 84) as an open question. |
| `.../durable-mesh-messaging-protocol.md:33` (routing policy) | glpnet | 2026-06-28 | Exact routing-policy field set: **must-have targets, must-have waypoints, exclude lists** — carried with the message/stream. |
| `PRINCIPAL-INTERVIEW-2026-06-18.md` | mstack (dianna) | **2026-06-18** | **The verbatim Marcelle+Gabi principal interview.** Mesh (≤12 heterogeneous devices, ≤3 routes, reconverge <1s), amulets+macaroons (amulet = Amoeba-style static 16 B: Port 48b / ObjNum 24b / Rights 8b / Check 48b), WAL, guardians/actors, negotiated signatures (HMAC-SHA256/512 vs Ed25519/ECDSA/RSA-PSS). Richest single design-context doc. |
| `docs/yngenios/SOURCE-MATERIAL.md` | buildkit | **2026-07-03** | Verbatim yngenios product brief; names **Gabi + Marcel/Marcelle**; §4 enumerates the BSTDEV source corpus (beacon macaroon+mailbox+QHSM, olamnit CapabilityKernel/Link/RouterEngine, GLP-Net QUIC/3270). Roadmap hub. |
| `docs/proposals/beacon-pilot.md` | buildkit-beacon | 2026-06-22 | Beacon epic proposal (WSJF 1.78/RICE 300); names Marcelle + Gabi; origin of the beacon messaging line. |
| `mstack-traffic.md` | buildkit-beacon (037) | 2026-06-22 | Design-research doc: capability-in-the-envelope **amulets**, GLPNET friends-of-friends mesh (B1 signaling / B2 bulk split-mix-erasure / B3 backbone), bearer-agnostic. "Gabi and Marcelle are the authority." |
| `docs/research/three-role-agent-teams/METHOD-AND-DOGFOOD.md` | glpnet | 2026-07-02 | The scan method itself (this run's methodology). |

**Committed vs advisory:** the glpnet brief is *advisory — not yet specified*. The mstack interview and buildkit SOURCE-MATERIAL are captured inputs feeding specified features (mstack 007, buildkit 047). No separate "Marcel" (distinct from Marcelle) transcript was found — "Marcel" and "Marcelle" refer to the same colleague across units; principals are **Gabi (GAVRI)** + **Marcelle (OLAMNIT)**.

---

## 5. Encoding interchange (S1) — evidence + gaps

**Strong, but capped at two tiers per unit — no single unit spans JSON+binary+YAML+Gleam-term.**

| Unit | Encodings interchanged | Round-trip discipline | Recency |
|---|---|---|---|
| **qhstate** `qmedit/surfaces/` | **JSON + binary(CBOR-TLV) + YAML** | lossless JSON↔binary (FR-004..007); `QMD1` magic + `payload_len(uint32 BE)` lets a reader **skip an unknown-head block without decoding** | 2026-06-28 **(best coverage)** |
| **olamnit** `EnvelopeCodec.cs` | **JSON ↔ binary** (one format byte flip re-encodes, no consumer change; `BytesMarker $bytes` keeps closed set faithful) | AOT-safe hand-rolled Utf8JsonWriter/JsonDocument, no reflection | active kernel, through 2026-07-03 |
| **glpnet** 038 + `glp_gleam` | **Dart/C#/Gleam** binary byte-parity; ground-GLP-term surface | cross-runtime **byte-identical**; 029 Lean `decode∘encode=id` proof | 2026-07-02 |
| **buildkit-beacon** `envelope.py` | **JSON only** (canonical field order, v2 additive fields emitted only when present) | byte-stable round trip asserted (`test_envelope_v2.py`); C#/Python lock-step | 2026-06-26 |
| **mstack** `wire.py` | **single binary frame** (DATA/ACK/INTRO, CRC32 + HMAC) | transport-agnostic "identical bytes"; round-trip vectors 0/1/1KiB/1MiB | 2026-06-28 |

**Gap:** the epic's full multi-encoding language (JSON *type-encoding* ↔ binary ↔ YAML ↔ markdown ↔ Gleam-term, all interchangeable) exists **nowhere as one artifact**. qmedit is closest (3 surfaces) but has no Gleam-term surface; glpnet has the Gleam-term surface + cross-runtime parity but no YAML/markdown. **The interchange matrix is the net-new integration work.**

---

## 6. Header, routing & capability tokens (S2)

**The most redundantly-covered signal — five independent macaroon implementations exist.** This is a keyword-collision-rich area; ratings below separate real token machinery from mere references.

**Capability tokens — macaroon (implemented):**
- **mstack** `security/macaroon.py` + `contracts/macaroon.md` (2026-06-28) — HMAC-chained caveats (`sig_n = HMAC-SHA256(sig_{n-1}, caveat_{n-1})`), attenuation, **verify-before-act**, human-rooted `introduce` capability. Contract tests. **STRONG.**
- **buildkit-beacon** `macaroon.py` (2026-06-29) — `base64url(payload) + '.' + hex(HMAC)`, `caps[]`, cross-language byte-stable verify (Python-minted verifies in C#), `_TYPE_CAP` maps message-type→required cap. Names the **amulet** scheme as round-two target. **STRONG.**
- **qhstate** `qmedit/seal/macaroon.py` (2026-06-28) — HMAC-chained caveats, **fail-closed** (unsatisfiable OR un-understood caveat → fail). **STRONG.**
- **olamnit** `EnvelopeHeader.cs` (active) — 48-byte fixed header; `CapabilityId` (interned ring-0 `<cap>.<action>`) + 128-bit `CapRef` handle; "both are requests never grants — forging resolves to refusal." **STRONG (binary header variant).**
- **crucible** `beacon_feed.py` + dossier (2026-06-28) — consumes beacon's `{v,type,target,payload,macaroon}`; cites `beacon/host/Macaroon/Macaroon.cs:145`. **STRONG (consumer).**
- **buildkit** spec-047 (2026-07-03) — macaroon HMAC (beacon) minted/attenuated/**revoked** via olamnit CapabilityKernel; FR-017 verify-before-act on every routed action. **STRONG (unification).**

**Amulet (the distinct token type):**
- **mstack** `PRINCIPAL-INTERVIEW-2026-06-18.md` — the canonical **amulet** definition: Amoeba-style static **16 B** (Port 48b / ObjNum 24b / Rights 8b / Check 48b). **STRONG — this is where "amulet ≠ macaroon" is defined.**
- **buildkit-beacon** mstack-traffic.md + beacon macaroon.py name amulet as the round-two target.
- glpnet finding `P7-qhsm-yngenios/DOSSIER.md:107` (PAT-03) — only macaroon hit *inside* glpnet, cited FROM the beacon unit; confirms the token lives outside glpnet.

**Curated capability-security corpora (reusable prior-art reservoirs):**
- **mstack** `04-capability-security-authz.md` (2026-06-17) — 18-entry corpus: Google Macaroons NDSS'14, Biscuit, UCAN, WAVE, SPIFFE, OCapN, CHERI, lnd, libmacaroons/HyperDex.
- **olamnit** `02-corpus.md:858` — cites Google Macaroons as the spec for the repo's existing seam; rtos-kernel converges macaroon/Biscuit/UCAN.

**Routing policy fields:**
- **glpnet** brief (S8) — must-have targets / must-have waypoints / exclude lists.
- **olamnit** spec-016 distance-vector-routing (2026-06-27) — next-hop selection, split-horizon + poison-reverse, withdraw/poison stale routes, reconvergence quiescence. Closest *implemented* routing behaviour (uses route-poisoning vocabulary, not waypoint/exclude).
- **buildkit-beacon** mstack-traffic B2 — bearer-agnostic split/mix/erasure over multiple routes.

**Header schemas that carry routing+capability as segments:**
- beacon/crucible: `{v,type,target,payload,macaroon}`
- olamnit: 48-byte `{NameId(route), CapabilityId, CapRefHi/Lo, Version}`
- glpnet L5: `{msg_id, from, to, seq, payload}`
- buildkit D2 unified: `{version,type,from,to,seq,msg_id,macaroon}+payload`

---

## 7. Signatures & transparent payload transport (S3)

**Partial. Two distinct signature *layers* coexist across units; only qhstate has explicit per-block (sub-content) signing.**

- **qhstate** spec-036 FR-018 + `seal/` (2026-06-27/28) — **STRONGEST for S3.** Per-block/sub-content sealing: **ed25519 signature + macaroon per appended block**, verifiable after round-trips; `_union_seals` merges multiple independent seals in the footer (whole-doc carries many sub-seals). SC-005: tampered sealed block fails 100%. Opaque unknown blocks carried verbatim + skipped by length = transparent passthrough.
- **glpnet** archive `main_GLP_to_Dart.tex:619` (pre-cutoff) — **nested attestation**: `forward(p,P)` wraps an attested post; recipients verify BOTH q's forwarding attestation AND p's original creation attestation → verifiable forwarding chain. Academic, nearest in-repo definition of whole-vs-sub-content signing.
- **mstack** `wire.py` (2026-06-28) — two independent layers: whole-message **HMAC e2e integrity** (survives relays) + per-link macaroon caveat-chain HMAC. Payload opaque. Principal interview adds **negotiated** symmetric(HMAC)/asymmetric(Ed25519/ECDSA/RSA-PSS) signatures verified by the gateway.
- **buildkit-beacon** `sign.py` (2026-07-02) — **Ed25519** whole-artifact signing of CRDT exports (vs per-message macaroon HMAC) = two signature classes. `render_display` = opaque JSON hydrated unmodified (transparent formatting payload).
- **olamnit** — CRC tamper-detects header auth field; secure link = AES-256-GCM AEAD + ECDH pinning (transport encryption, **not** nested content signatures).
- **buildkit** spec-047 — sender-authenticated page owner (per-sub-content auth) distinct from whole-envelope macaroon; "signed CRDT export" in CHANGELOG. No explicit detached multi-sig scheme — inferred from auth layering.
- **crucible** — `render_display` transparent passthrough; no signing beyond envelope macaroon.

**Gap:** a first-class scheme where whole-message AND arbitrary sub-content signatures both travel and both verify is only prototyped in qmedit. Everywhere else it is two *incidental* layers (transport HMAC + capability HMAC), not a designed multi-signature format.

---

## 8. Version tolerance & repair (S4)

**Skip-unknown + additive-schema is well covered. Semantic tombstone / repair-segment is essentially MISSING.**

- **qhstate** spec-036 FR-023 + `binary_surface.py` — **STRONGEST.** Skip blocks you don't understand by `payload_len`, no corruption of understood blocks; per-block version tag; immutable append/prepend extension; FR-024 self-upgrade-path recognition. **No repair-segment/tombstone here.**
- **buildkit-beacon** `envelope.py` — explicit emit-low/accept-range (`ENVELOPE_VERSION=1`, `MAX_SUPPORTED_VERSION=2`), v2 = additive optional superset (v1 reader ignores v2 fields; v2 reader accepts v1); out-of-range rejected. No repair segment.
- **glpnet** 040 tmsg — additive kinds, unrecognized `tmsg` surfaced not crashed (the real forward-tolerance mechanism in glpnet).
- **olamnit** `EnvelopeHeader.cs` — append-only header, bump Version; `TryDecode` fails on unknown version, degrades unknown NameId to `unknown:<id>` (skip/degrade not crash); MemoryPack VersionTolerant mode.
- **buildkit** spec-046 — idempotent forward/reverse schema evolution with **data "parking"** (reversal moves post-evo journal rows to `roadmap_crdt_parked` instead of dropping) — **the nearest thing to a repair/tombstone-preservation mechanism**, but it is a *migration* concept, not an in-message repair segment.
- **mstack** — only a wire version byte + forward-compat `dst`; no additive/skip/tombstone mechanism.

**⚠️ Keyword-collision trap:** glpnet spec-035 "semantic-tombstone-enrichment" is a **codeconv dependency-inventory tombstone** (per-Dart-file metadata `.md`), **NOT** a CRDT/message semantic tombstone. It is **not** prior art for version-skip/repair-segment. Do not cite it for S4.

**Gap:** a message-level **repair segment** (re-transmit/patch a specific removed/damaged element) and a message-level **semantic tombstone** (mark content deleted while preserving convergence) are defined **nowhere**. CRDT soft-tombstones exist in the *stores* (beacon crdt, buildkit roadmap_crdt) but not in the *message format*.

---

## 9. QUIC/HTTP-3 wire framing (S5)

**Mature — but concentrated in glpnet only.**

- **glpnet** spec-036 — **the only real QUIC/HTTP-3 implementation.** Genuine handshake (System.Net.Quic/MsQuic, GA .NET 9+), RFC 6455 WS over one bidi QUIC stream, SPKI-SHA-256 pinning; `gleam_quic` relay reassembles >1 MiB frames. Gleam Profile A = Gleam/BEAM channel-link + C# genuine-QUIC side-process.
- **buildkit** spec-047 research/contract C5 — points AT glpnet's QUIC/WS: `LinkId(scheme ∈ {quic,tcp,ws,loopback,ble})`, `ILinkEndpoint`, reliability sublayer (version+CRC32+length framing, per-link FIFO seq/dedup, credit-window backpressure). The reuse plan.
- **mstack** `duplex-link-port.md` — `GlpLinkAdapter` binds the mesh to GLPNET QUIC/HTTP3/WS behind a pluggable `DuplexLink` port (self-delimiting frames, reliable-ordered + unreliable-datagram modes). The seam that consumes glpnet transport.
- **olamnit** — framing vocabulary (`[u32 len][u32 crc][u64 seq][header++body]`) reused for GLPNET glp_il_codec, but transport is **WAL/WebSocket/WSS, NOT QUIC**.
- **buildkit-beacon**, **crucible**, **qhstate** — **NO QUIC** (WebSocket + MCP only; qhstate transport out of scope). `research-loose` SIENA is only a content-routing analogue.

**Reuse path:** glpnet is the transport of record; mstack's `DuplexLink` port + buildkit's `LinkId` abstraction are the ready-made seams to plug it behind. Framing needs the 038 FR-006 payload-type prefix byte to become a general carrier.

---

## 10. CRDT substrate (S6)

**Two production-grade CRDT engines exist — both for the ROADMAP store, not for messages.** qhstate has the only *message-level* CRDT.

- **buildkit** `deploy/evolutions/roadmap_crdt/evolution.py` (2026-07-03) — **STRONGEST engine.** OR-set elements, per-cell HLC clock, causal watermark, **HLC + DVV + slot** columns, field-grained journal with fold/project/replay. Migration 0022 collision-free store.
- **buildkit-beacon** `roadmap/crdt/{replay,journal,project,writeback,clock,identity,slot}.py` (2026-06-30/07-02) — append-only journal → deterministic **order-independent** projected HEAD (property-tested), LWW registers keyed `(hlc, origin_host_id)`, add-wins OR-Set, soft tombstones, idempotent `op_id` apply (`ON CONFLICT DO NOTHING`), Ed25519-signed exports, `min_reader_version` forward-compat. Plus a **curated 42-paper CRDT corpus** (`corpus-index.md`): Shapiro CvRDT/CmRDT, RGA/Logoot/Fugue/Yjs, Riak DT/OR-Set/Automerge JSON CRDT, Lamport/DVV/HLC, cr-sqlite/ElectricSQL/PGlite, **Cambria edit lenses + expand/contract schema evolution**.
- **qhstate** `qmedit/crdt/{base,state_based,op_based}.py` (2026-06-28) — **the only message-level CRDT:** three selectable models (state-based default / op-based / minimal append-prepend log) on an immutable substrate; `doc.crdt_model` recorded on the doc; absent/unknown → state-based. Curated CRDT corpus at `specs/036/corpora/crdts/`.
- **olamnit** rtos-kernel.md:527 — CRDT-by-mutability: immutable→content-addressed CID, concurrent-mutable→**Automerge**, single-writer→LWW; local-first WAL+PGlite authoritative. `PgliteContentStore.cs`. K2 durable mesh relay survives relay-node kill (journal/DLQ, exactly-once).
- **glpnet** — CRDT/CALM surveyed in `multi-protocol-link-layer/corpus/17` as prior art but **deliberately deferred** (out of scope in 025). `main_GLP_to_Dart.tex` blocklace = "eventual consistency similar to CRDTs" (cites Shapiro 2011).
- **mstack** — **NEGATIVE:** no CRDT; PGlite dual-store + delivery ledger (dedup/exactly-once); anti-entropy explicitly out of scope. Contrasts with, rather than provides, the substrate.
- **research-loose** — academic definitions: Shapiro 2011 (commutative/associative/idempotent merge), Lasp/Meiklejohn (CRDT-over-actors, flags the "restricts expressible message types" tradeoff — directly bears on S1+S6 tension).

**Fit:** the roadmap CRDT engines (buildkit/beacon) are the reusable convergence machinery; qmedit is the only design that puts CRDT *in the message*. The **message-vs-store CRDT distinction** is a key synthesis decision. *[Superseded 2026-07-04 by F3 §6 E1: store engine = delta-CRDT + Merkle anti-entropy; roadmap_crdt demoted to a concept reference (OR-Set/HLC machinery), not the store engine.]*

---

## 11. Schema & format architecture (S7)

- **qhstate** spec-036 FR-013..016 — **informal plaintext schema-definition language** (simpler than JSON-Schema, plaintext-XSD-like) with `name@version` imports + in-document anonymous-but-reusable types; head-tagged versioned blocks (FR-001..003). `synthesis/architecture-outline.md` = 50 traced patterns across 7 corpus groups (at-protocol, crdts, cross-referencing, json-binary-encodings, lisp-clojure, mumps-globals, token-thesaurus). **The most complete schema-architecture artifact.**
- **buildkit-beacon** `message_types.py` + `message-and-mailbox.md` — runtime **message-type registry** with declarative per-type payload shapes (required/optional field → scalar kind, NOT full JSON-Schema); extra fields tolerated (additive); 8 enumerated pilot types with direction/payload/effect table; "new feed adds only a payload shape, never changes the envelope." C# mirror `MessageTypeRegistry.cs`.
- **glpnet** 040 — `tmsg(Kind,Field…)` registry (chat/page/pinpoint/form_def/repl_goal/rcopy_*).
- **buildkit** spec-047 D2 — **the unification contract**: reconcile 3 envelopes into one carrier + `tmsg(...)` codec in data-model.
- **olamnit** rtos-kernel.md:480 — single `IMessageSerializer<T>` seam, PayloadFormat byte, MemoryPack VersionTolerant; header always binary fixed-layout regardless of tier.
- **mstack** spec-007 FR-005 — versioned envelope + entity model (Message/Envelope, Custody Record, Delivery Ledger, Forwarding Seam) — scoped to durable P2P, not a general schema language.

**Where a schema-definition language exists:** qmedit (plaintext DSL) + beacon (declarative registry). Where it must be *created*: a **unified** cross-runtime registry that all four wires agree on (buildkit D2 mandate) with the glpnet brief's "Policy DSL" for routing.

---

## 12. Prior-art coverage matrix (signal × repo)

Legend: **S** strong (built/defined here since cutoff) · **P** partial · **W** weak/reference-only · **·** absent · **X** keyword-collision trap (looks relevant, isn't)

| Signal | glpnet | bk-beacon | mstack | olamnit | buildkit | crucible | qhstate | research-loose |
|---|---|---|---|---|---|---|---|---|
| S1 multi-format encodings | S | P (JSON) | P (1 bin) | S (JSON↔bin) | S (unify) | W (ptr) | **S (3 surf)** | · |
| S2 header + macaroon/amulet | S (PeerId) | S | **S (amulet def)** | S (48B) | S (unify) | S | S | W (gap) |
| S3 multi-signature | P (nested) | P (Ed25519) | P (2 layers) | W (AEAD) | P | W | **S (per-block)** | · |
| S4 version tolerance / repair | S (skip) | S (add-super) | W | P (degrade) | P (parking) | W | **S (skip-len)** | · |
| S5 QUIC/HTTP-3 framing | **S** | W (WS) | P (seam) | P (WAL/WS) | S (reuse) | W (WS) | · (oos) | W |
| S6 CRDT substrate | W (deferred) | **S (roadmap)** | · (neg) | S (Automerge) | **S (roadmap)** | · | **S (message)** | W (academic) |
| S7 schema architecture | S (tmsg) | S (registry) | P | S (seam) | S (D2 unify) | P | **S (DSL)** | · |
| S8 roadmap/interview | **S (brief)** | S | **S (interview)** | P (colab) | **S (SOURCE)** | · | · | W |

---

## 13. Gaps & net-new work

**What the epic must define from scratch (no prior art anywhere, or only academic):**

1. **The unification itself (highest-value net-new).** buildkit D2 *mandates* one carrier reconciling beacon-JSON / olamnit-48B / glpnet-GLP-term, but the reconciled wire is not yet built. Bytes do not agree across units today.
2. **≥3-way encoding interchange as one artifact.** No unit spans JSON + binary + YAML + markdown + Gleam-term. qmedit (3) and glpnet-Gleam (parity) must be fused; markdown surface exists nowhere.
3. **Message-level semantic tombstone.** CRDT soft-tombstones exist only in the *stores*; a tombstone in the *message format* is undefined.
4. **Repair segment.** Re-transmit/patch a specific removed/damaged element within a message — defined nowhere (buildkit "parking" is a migration analogue only).
5. **First-class multi-signature format** (whole + arbitrary sub-content both travelling + verifying) — only prototyped per-block in qmedit; elsewhere it is incidental HMAC layering.
6. **CRDT-of-the-message vs CRDT-of-the-store decision.** Two mature store engines (buildkit/beacon) + one message-CRDT (qmedit) exist but were never reconciled into a single model.
7. **Routing "Policy DSL"** (must-have targets/waypoints/exclude lists as an executable policy) — named as an open question in the glpnet brief; olamnit distance-vector routing is the nearest *behaviour* but not a policy language.
8. **Payload-type discriminator in the frame** — glpnet 038 FR-006 explicitly leaves `FrameCodec` needing a payload-type prefix byte to become a general multi-format carrier.
9. **Amulet as a live token type** — defined (mstack 16 B Amoeba-style) and named as beacon's round-two target, but **not implemented** anywhere; only macaroons are built.

**Total gap count: 9.**

---

## 14. Reuse recommendations & risks

**Canonical artifacts to build on (ranked):**
1. **`buildkit` spec-047 D2 unification contract** — adopt as the reconciliation spec; it already enumerates the three envelopes and the merge rules.
2. **`qhstate` spec-036 + qmedit** — adopt as the multi-format + per-block-seal + message-CRDT reference implementation; adopt its schema-definition DSL and 50-pattern synthesis.
3. **`glpnet` spec-036 QUIC/WS transport + 038 FrameCodec (fix FR-006)** — the transport + cross-runtime binary layer of record.
4. **`buildkit` / `buildkit-beacon` roadmap_crdt engine** — the convergence machinery (HLC/DVV/OR-Set/journal-fold) for any durable store. *[Superseded 2026-07-04 by F3 §6 E1: store engine = delta-CRDT + Merkle anti-entropy; roadmap_crdt demoted to a concept reference, not the store engine.]*
5. **`mstack` + `buildkit-beacon` macaroon** + **mstack amulet definition** — the capability layer; `DuglexLink` port + `LinkId` as the transport seams.
6. **Corpora to hand F2/F3 directly:** beacon 42-paper CRDT index, mstack 18-entry capability-security corpus, qmedit 50-pattern synthesis + 7 corpus groups, olamnit macaroon corpus.

**Risks & caveats:**
- **Keyword-collision trap #1:** glpnet spec-035 "semantic-tombstone" = codeconv inventory metadata, NOT a CRDT/message tombstone. Exclude from S4.
- **Keyword-collision trap #2:** "CRDT" in glpnet/mstack is *surveyed-and-deferred* or *out-of-scope*, not built — do not over-credit those units for S6.
- **Keyword-collision trap #3:** olamnit/beacon "QUIC" hits are **zero** — their "same-envelope frames" is WS/MCP, not QUIC. Only glpnet has real QUIC.
- **Wire divergence:** the concept-convergence hides byte-divergence — do not assume the units interoperate today; they don't.
- **"Marcel" = "Marcelle":** one colleague; no separate Marcel transcript exists.
- **Recency:** glpnet 029 codec, `main_GLP_to_Dart.tex`, and the multi-protocol-link corpus are **pre-cutoff**; reported for definitional value but not "actively worked" — weight accordingly.
- **research-loose** contributes only academic framing (Shapiro/Lasp/SIENA) + principal-naming; a *different* epic (Marathon harness). Low weight.

**Recommended inputs to F2 (web-research corpus):**
- CRDT: Shapiro 2011 CvRDT/CmRDT, Automerge JSON CRDT, Yjs, RGA/Fugue, **Cambria edit lenses / expand-contract schema evolution** (the multi-format-version-tolerance angle), cr-sqlite/ElectricSQL.
- Capability: Google Macaroons (NDSS'14), Biscuit, UCAN, WAVE, SPIFFE, OCapN, **Amoeba amulets** (the static-token lineage).
- Encoding: CBOR/MessagePack TLV skip-tolerance, AT Protocol/Bluesky message model, MUMPS tree globals, Lisp/Clojure head-tagged structure.
- Transport: QUIC/HTTP-3 framing, RFC 6455 WS-over-QUIC, content-based routing (SIENA), split/mix/erasure multipath.
- Signatures: Ed25519/EdDSA detached + nested attestation chains, macaroon caveat HMAC chains.

**Recommended inputs to F3 (synthesis):**
- Anchor on **qmedit (036) as the message-format skeleton** + **buildkit-047-D2 as the unification frame** + **glpnet-036/038 as the transport/binary layer** + **buildkit/beacon roadmap_crdt as the convergence engine**.
- Resolve the **9 gaps** in §13, prioritising: (a) the unified wire, (b) 3+-way interchange, (c) message-level tombstone + repair segment, (d) the CRDT-of-message-vs-store decision, (e) the routing Policy DSL.
- Carry the **coverage matrix (§12)** as the synthesis scaffold; every net-new item must trace to a matrix cell that is P/W/·/X, not S.

---

## 15. Source appendix — ranked file list

Rank by (recency since cutoff × directness × confidence). All paths absolute.

| # | File | Date | Signals | Conf |
|---|---|---|---|---|
| 1 | `D:/bstdev/research/qhstate/specs/036-extensible-multi-format-schema-prototype-implementation/spec.md` | 2026-06-27 | S1,S3,S4,S6,S7,epic | high |
| 2 | `D:/bstdev/research/buildkit/specs/047-build-yngenious-daemon-service-mvp/contracts/README.md` | 2026-07-03 | S1,S2,S5,S7 (D2 unify) | high |
| 3 | `D:/bstdev/research/glp/glpnet/docs/roadmap-intake/durable-mesh-messaging-protocol.md` | 2026-06-28 | S2,S5,S6,S8 (the brief) | high |
| 4 | `D:/bstdev/tools/MSTACK/dianna/application/box-inputs/P3-DRAFTING/inputs/PRINCIPAL-INTERVIEW-2026-06-18.md` | 2026-06-18 | S2 (amulet def),S3,S8 | high |
| 5 | `D:/bstdev/research/buildkit/docs/yngenios/SOURCE-MATERIAL.md` | 2026-07-03 | S8 (roadmap hub) | high |
| 6 | `D:/bstdev/research/glp/glpnet/specs/040-rcopy-file-transfer-service/contracts/terminal-protocol.md` | 2026-07-03 | S1,S4,S7 (tmsg codec) | high |
| 7 | `D:/bstdev/research/glp/glpnet/specs/036-http3-quic-ws-link/contracts/wire-contract.md` | 2026-07-02 | S2,S5,S7 (L5 envelope) | high |
| 8 | `D:/bstdev/research/glp/glpnet/specs/038-result-codec-and-framecodec-ride/spec.md` | 2026-07-02 | S1,S5 (byte-parity+FrameCodec FR-006) | high |
| 9 | `D:/bstdev/research/qhstate/qmedit/src/qmedit/surfaces/binary_surface.py` | 2026-06-28 | S1,S4 (CBOR-TLV skip-len) | high |
| 10 | `D:/bstdev/research/buildkit/src/buildkit_cli/deploy/evolutions/roadmap_crdt/evolution.py` | 2026-07-03 | S6 (HLC/DVV/OR-Set engine) | high |
| 11 | `D:/bstdev/research/buildkit-beacon/src/buildkit_cli/roadmap/crdt/replay.py` | 2026-06-30 | S6 (order-independent fold) | high |
| 12 | `D:/bstdev/research/olamnit/Olamnit/Olamnit.Kernel/Envelope/EnvelopeCodec.cs` | active→2026-07-03 | S1 (JSON↔bin one-byte flip) | high |
| 13 | `D:/bstdev/research/olamnit/Olamnit/Olamnit.Kernel/Envelope/EnvelopeHeader.cs` | active→2026-07-03 | S2,S4 (48B header) | high |
| 14 | `D:/bstdev/tools/MSTACK/specs/007-durable-mesh-messaging/contracts/macaroon.md` | 2026-06-28 | S2 (HMAC-chain caveats) | high |
| 15 | `D:/bstdev/research/buildkit-beacon/src/buildkit_cli/beacon/macaroon.py` | 2026-06-29 | S2 (caps[], amulet target) | high |
| 16 | `D:/bstdev/research/qhstate/qmedit/src/qmedit/seal/macaroon.py` + `seal/` | 2026-06-28 | S3 (per-block ed25519+macaroon) | high |
| 17 | `D:/bstdev/research/buildkit-beacon/specs/043-export-roadmap/research/corpus/corpus-index.md` | 2026-06 | S6 corpus (42 papers, Cambria) | high |
| 18 | `D:/bstdev/research/buildkit-beacon/src/buildkit_cli/beacon/message_types.py` | 2026-06-26 | S7 (declarative registry) | high |
| 19 | `D:/bstdev/tools/MSTACK/specs/007-durable-mesh-messaging/contracts/duplex-link-port.md` | 2026-06-28 | S5 (DuplexLink seam) | high |
| 20 | `D:/bstdev/tools/MSTACK/dianna/application/system-descriptions/04-capability-security-authz.md` | 2026-06-17 | S2 corpus (18 entries) | high |
| 21 | `D:/bstdev/research/olamnit/specs/013-olamnit-rtos-kernel/rtos-kernel.md` (:480/:484/:527) | active→2026-06-27 | S1,S5,S6,S7 | high/med |
| 22 | `D:/bstdev/research/buildkit/specs/046-roadmap-export-evolution/spec.md` | 2026-07-02 | S4 (parking = repair analogue) | high |
| 23 | `D:/bstdev/research/crucible/src/crucible_present/beacon_feed.py` | 2026-06-28 | S2 (envelope consumer) | high |
| 24 | `D:/bstdev/research/qhstate/.../synthesis/architecture-outline.md` | 2026-06-27 | S7 (50 patterns/7 groups) | high |
| 25 | `D:/bstdev/research/buildkit-beacon/specs/037-bk-beacon-pilot/research/mstack-traffic.md` | 2026-06-22 | S2,S5,S8 (amulets, GLPNET B1/B2/B3) | high |
| 26 | `D:/bstdev/research/glp/glpnet/specs/029-il-codec-spike/spec.md` | pre-cutoff | S1 (Lean decode∘encode=id) | med |
| 27 | `D:/bstdev/research/olamnit/specs/016-distance-vector-routing/spec.md` | 2026-06-27 | S2 (routing behaviour) | med |
| 28 | `D:/bstdev/research/glp/glpnet/docs/archive/main_GLP_to_Dart (3).tex:619` | pre-cutoff | S3,S6 (nested attestation, blocklace) | med |
| 29 | `D:/bstdev/tools/MSTACK/mesh/src/durable_mesh/wire.py` | 2026-06-28 | S1,S3 (frame codec + e2e HMAC) | med |
| 30 | `D:/bstdev/research/olamnit/CHANGELOG.md:81` (K2 durable mesh relay) | 2026-07-03 | S6 (relay journal/DLQ) | med |
| 31 | `D:/bstdev/research/glp/glpnet/docs/research/multi-protocol-link-layer/corpus/17-efficient-logic-variables-distributed-computing.md` | pre-cutoff (2026-06-08) | S6 (CRDT/CALM survey) | low |
| 32 | `D:/bstdev/research/syst-lit-rev-agentic-protocols/wip/stage-07.../corpus_pubsub-routing-coordination_analysis.md` | pre-cutoff | S6 (Shapiro), S5 (SIENA) | low/med |
| 33 | `D:/bstdev/research/_marathon-synthesis/RESTART-STATE.md` | 2026-06-13 | S8 (principal naming; wrong epic) | low |

---

*End of consolidated prior-art scan. 33 ranked sources · 9 identified gaps · 8 units. Advisory, non-blocking.*

---

## Change log — 042 verification pass (2026-07-04)

> All amendments below were made by feature 042-crdtmsg-verify-harden; the finding ids
> resolve in docs/research/crdt-multiformat-messaging/verification-report-042.md.

| # | Section touched | Change | Why (finding id) | Baseline |
|---|---|---|---|---|
| 1 | (new terminal section) | Added this change-log section skeleton (contract rule 4) | SETUP-042-F1 | HEAD(6ff3a8c9) |
| 2 | §12 matrix, S8 × qhstate | `P (eng interview)` → `·` — annotation has zero body/§15 support (overclaimed) | LR-042-F1-1 | DELIVERY(c20317ce) |
| 3 | §12 matrix, S5 × crucible | `·` → `W (WS)` — body L163 attributes WS/MCP transport to crucible identically to beacon's W (missed-coverage) | LR-042-F1-2 | DELIVERY(c20317ce) |
| 4 | §10 "Fit" line | E1 supersession note appended (store engine = delta-CRDT + Merkle; roadmap_crdt demoted to concept reference) | RP-042-13 | HEAD(6ff3a8c9) |
| 5 | §14 reuse item 4 | Same E1 supersession note appended to the roadmap_crdt reuse recommendation | RP-042-13 | HEAD(6ff3a8c9) |

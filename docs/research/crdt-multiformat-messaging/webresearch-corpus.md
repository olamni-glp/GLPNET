# Web-Research Corpus — CRDT Multi-Format Messaging Epic

> **Run date:** 2026-07-03
> **Role:** Curating (F2 web-research corpus team) — consolidates 184 candidate papers from 10 theme search agents into a final curated index.
> **Total papers (curated):** 148
> **Scope:** External literature (peer-reviewed papers, standards, seminal works, authoritative reference specs). This corpus **EXTENDS the internal corpora** already cited in the F1 prior-art scan — **beacon-42** (42-paper CRDT index), **mstack-18** (18-entry capability-security corpus), **qmedit-50** (50-pattern synthesis / 7 corpus groups). Papers already present internally are tagged `[INTERNAL-OVERLAP <corpus>]`; they are kept here (not dropped) so F3 has one unified bibliography, but the tag flags that no NEW acquisition is needed.
> **Mapping key:** `S1–S8` = the eight epic signals (§2 of the F1 scan); `gap1–gap9` = the nine net-new gaps (§13). The theme agents' own `maps_to` guesses were **re-mapped** to these canonical IDs during curation.
> **Verification:** audited & hardened by feature 042 (incl. full bibliographic re-verification) — see [verification-report-042.md](verification-report-042.md) and the terminal change log.

## Canonical signal / gap legend

| ID | Signal (acceptance criterion) | ID | Gap (net-new work) |
|----|-------------------------------|----|--------------------|
| S1 | Multi-format / interchangeable encodings | gap1 | The unified wire (reconcile beacon-JSON / olamnit-48B / glpnet-GLP-term) |
| S2 | Routing + capability header (macaroon/amulet) | gap2 | ≥3-way encoding interchange as ONE artifact |
| S3 | Multiple signatures (whole + sub-content) | gap3 | Message-level semantic tombstone |
| S4 | Forward/backward version tolerance + repair | gap4 | Repair segment (patch a removed/damaged element) |
| S5 | QUIC/HTTP-3 optimized binary framing | gap5 | First-class multi-signature format |
| S6 | CRDT substrate (messages/stores/documents) | gap6 | CRDT-of-the-message vs CRDT-of-the-store decision |
| S7 | Schema / format architecture | gap7 | Routing "Policy DSL" (targets/waypoints/excludes) |
| S8 | Roadmap / interview provenance | gap8 | Payload-type discriminator byte in the frame |
| | | gap9 | Amulet as a live token type (Amoeba 16 B) |

---

## Per-theme counts

| Theme | Count |
|-------|-------|
| schema-languages | 12 |
| encodings-spectrum | 19 |
| schema-evolution | 19 |
| skip-unknown-extensibility | 10 |
| crdt-foundations | 20 |
| crdt-systems | 8 |
| crdt-messages-stores | 14 |
| capability-tokens | 16 |
| signatures-attestation | 15 |
| transport-routing | 15 |
| **Total** | **148** |

---

## 1. Schema languages (IDLs / format-description languages) — 12

Bears on **S7** (schema architecture), **S1/gap2** (multi-encoding interchange), **gap1** (unified carrier).

1. **Thrift: Scalable Cross-Language Services Implementation** — Slee, Agarwal, Kwiatkowski (2007) — Facebook whitepaper — https://thrift.apache.org/static/files/thrift-20070401.pdf
   - Founding IDL-plus-codegen model (language-neutral type + service defs → RPC clients across langs) that Protobuf/gRPC mirrored. Canonical prior art for schema-defined cross-language messaging.
   - *concepts:* IDL, cross-language codegen, field identifiers, versioning via optional fields · *maps_to:* S7, gap1, gap2 · **seminal**
2. **Foundations of JSON Schema** — Pezoa, Reutter, Suárez, Ugarte, Vrgoč (2016) — WWW 2016 (ACM) — https://dl.acm.org/doi/10.1145/2872427.2883029
   - First formal syntax + semantics for JSON Schema; rigorous account of what a structural schema over JSON means. Theoretical backbone for validating multi-format messages.
   - *concepts:* formal semantics, recursive references, structural validation, decidability · *maps_to:* S7 · **seminal**
3. **Validation of Modern JSON Schema: Formalization and Complexity** — Attouche, Baazizi, Colazzo, Ghelli, Sartiani, Scherzinger (2024) — PACMPL (POPL) — https://dl.acm.org/doi/full/10.1145/3632891
   - Extends the formal treatment to Modern JSON Schema (dynamic refs, annotation-dependent validation) with complexity results + a verified Scala validator. State of the art on schema expressiveness/checkability.
   - *concepts:* dynamic references, annotation-dependent validation, complexity, verified validator · *maps_to:* S7 · **strong**
4. **EverParse: Verified Secure Zero-Copy Parsers for Authenticated Message Formats** — Ramananandro, Delignat-Lavaud, Fournet, Swamy, Chajed, Kobeissi, Protzenko (2019) — USENIX Security 2019 — https://www.usenix.org/conference/usenixsecurity19/presentation/delignat-lavaud
   - Compiles a DSL of TLV message formats to **formally verified** parsers/serializers proven safe, correct (parse = inverse of serialize) and non-malleable. The rigorous ideal for deriving multi-runtime codecs from one schema (validated on TLS 1.0–1.3, ASN.1 DER).
   - *concepts:* verified parsing, non-malleability, parser combinators, TLV, schema→parser codegen (F*/LowParse) · *maps_to:* S1, S7, gap2 · **seminal**
5. **RFC 8610: Concise Data Definition Language (CDDL)** — Birkholz, Vigano, Bormann (2019) — IETF RFC 8610 (Proposed Standard) — https://datatracker.ietf.org/doc/html/rfc8610
   - IETF schema notation covering **both CBOR and JSON with one grammar** — directly on-point for a layer that must describe the same message across binary and textual encodings.
   - *concepts:* dual CBOR/JSON schema, sockets/plugs extensibility, control operators · *maps_to:* S1, S7, gap2 · **seminal**
6. **RFC 9682: Updates to the CDDL Grammar** — Bormann (2024) — IETF RFC 9682 — https://www.rfc-editor.org/rfc/rfc9682.html
   - Errata/grammar consolidation needed to implement a correct current-generation CDDL parser rather than the stale RFC 8610 ABNF.
   - *concepts:* CDDL grammar corrections, ABNF consolidation · *maps_to:* S7 · **ok**
7. **RFC 1014: XDR — External Data Representation Standard** — Sun Microsystems / R. Srinivasan (1987) — IETF RFC 1014 — https://www.rfc-editor.org/rfc/rfc1014
   - The original platform-independent data-description + marshaling language behind Sun RPC/NFS — historical root of IDL-for-wire-format (canonical byte order, describe-data-not-code).
   - *concepts:* external data representation, canonical byte order, structs/unions/enums DSL, rpcgen · *maps_to:* S7, gap1 · **seminal**
8. **ITU-T X.680–X.693: ASN.1 — Basic Notation and Encoding Rules** — ITU-T / ISO-IEC JTC1 (2021) — ITU-T Rec. X.680–X.693 (ISO/IEC 8824/8825) — https://www.itu.int/rec/T-REC-X.680-X.693-201508-S/en
   - The most mature **encoding-agnostic** abstract-syntax schema language: ONE abstract type definition, MANY encoding rules (BER/DER/PER). The archetype for separating message structure from wire encoding — the exact model gap2 needs.
   - *concepts:* abstract vs transfer syntax, multiple encoding rules, information object classes, constraints, tagging · *maps_to:* S1, S7, gap1, gap2 · **seminal**
9. **DFDL v1.0 (GFD.240; ISO/IEC 23415:2024)** — Beckerle, Hanson (2021) — Open Grid Forum GFD.240 (orig. GFD.207) — https://ogf.org/documents/GFD.207.pdf
   - An XSD-extension DSL that declaratively describes text, dense-binary AND legacy formats — the leading example of augmenting a schema language with physical-layout annotations to parse/unparse arbitrary wire formats from one model.
   - *concepts:* declarative format description, XSD + physical properties, parse+unparse from one model, logical/representation split · *maps_to:* S1, S7, gap2 · **strong**
10. **Kaitai Struct: Declarative Language to Generate Binary Data Parsers** — Yakshin et al. (2016) — project reference docs — https://kaitai.io/
    - A YAML `.ksy` language describing binary structure once, compiling parsers to 12+ target languages — a working blueprint for one-schema/many-runtime codec generation (the multi-format codegen model).
    - *concepts:* declarative `.ksy`, multi-target parser codegen, type/seq/instances · *maps_to:* S1, S7, gap2 · **strong**
11. **Cap'n Proto, FlatBuffers, and SBE (design comparison)** — Kenton Varda (2014) — capnproto.org design writeup — https://capnproto.org/news/2014-06-17-capnproto-flatbuffers-sbe.html
    - First-party design rationale contrasting three zero-copy schema-driven wire formats (arena layout, in-memory==wire, RPC integration) by the author of Protobuf v2. Primary-source insight into schema-language tradeoffs for low-copy messaging.
    - *concepts:* zero-copy, arena allocation, in-memory==wire, fixed field offsets, pointer/segment framing · *maps_to:* S1, gap2, gap8 · **strong**
12. **Expressiveness and Complexity of XML Schema** — Martens, Neven, Schwentick, Bex (2006) — ACM TODS — https://www.theoinf.uni-bayreuth.de/pool/documents/Paper2006-10/Paper2006/Expressiveness_and_Complexity_of_XML_Schema_paper.pdf
    - Rigorous characterization of what XSD structural typing can/cannot express and at what cost, with the empirical finding that most real schemas use only local/parent-context typing — a caution on how much structural expressiveness a message schema language actually needs.
    - *concepts:* structural typing, single-type tree automata, expressiveness classes, validation complexity · *maps_to:* S7 · **strong**

---

## 2. Encodings spectrum (binary ↔ human-readable) — 19

Bears on **S1** (interchange), **gap2** (≥3-way interchange), **gap8** (payload-type/TLV discriminator), **S4** (self-describing skip), **gap3** (canonical/deterministic encoding groundwork — the tombstone artifact itself is §5/§7 territory), determinism for signing (S3).

1. **A Survey of JSON-compatible Binary Serialization Specifications** — Viotti, Kinderkhedia (2022) — arXiv 2201.02089 (Oxford) — https://arxiv.org/abs/2201.02089
   - The single most complete comparative survey of schema-driven IDLs (ASN.1/Avro/Bond/Cap'n Proto/FlatBuffers/Protobuf/Thrift) vs schema-less formats (BSON/CBOR/MessagePack/…). The reference map for choosing a message schema language.
   - *concepts:* schema-driven vs schema-less, wire-format taxonomy, field tagging, type descriptors · *maps_to:* S1, gap2, gap8 · **seminal**
2. **A Benchmark of JSON-compatible Binary Serialization Specifications** — Viotti, Kinderkhedia (2022) — arXiv 2201.03051 (Oxford) — https://arxiv.org/abs/2201.03051
   - Companion empirical benchmark (400+ SchemaStore docs, 36-category doc taxonomy) quantifying size/space tradeoffs — grounds any "which encoding" decision in measured wire cost.
   - *concepts:* serialization benchmark, space efficiency, document taxonomy · *maps_to:* S1, gap2 · **strong**
3. **RFC 8949: Concise Binary Object Representation (CBOR)** — Bormann, Hoffman (2020) — IETF STD 94 — https://www.rfc-editor.org/rfc/rfc8949.html `[INTERNAL-OVERLAP qmedit-50]`
   - The self-describing, extensible binary encoding of choice for constrained/messaging systems; §4.2 Deterministic Encoding is needed to hash/sign CRDT ops reproducibly; unknown tags pass through (extensibility without version negotiation).
   - *concepts:* major types, self-describing, tags/extensibility, deterministic encoding (§4.2), map-key sort · *maps_to:* S1, S4, gap8, gap3 · **seminal**
4. **CBOR: On Deterministic Encoding and Representation** (draft-bormann-cbor-det) — Bormann (2024) — IETF I-D — https://www.ietf.org/archive/id/draft-bormann-cbor-det-04.html
   - Deep treatment of what deterministic CBOR must guarantee across encoder/decoder pairs — directly relevant to byte-identical CRDT-op hashes across peers.
   - *concepts:* deterministic encoding, canonical form, numeric reduction · *maps_to:* S3, gap3 · **strong**
5. **dCBOR: A Deterministic CBOR Application Profile** (draft-mcnally) — McNally, Allen (2024) — IETF I-D — https://blockchaincommons.github.io/WIPs-IETF-draft-deterministic-cbor/draft-mcnally-deterministic-cbor.html
   - A stricter deterministic profile aimed at content-addressed / signable data — a concrete recipe if CRDT ops are hashed for identity or dedup.
   - *concepts:* deterministic profile, content-addressing, signing-safe encoding · *maps_to:* S3, gap3 · **ok**
6. **RFC 8785: JSON Canonicalization Scheme (JCS)** — Rundgren, Jordan, Erdtman (2020) — IETF (Informational) — https://www.rfc-editor.org/rfc/rfc8785.html
   - Deterministic serialization of the readable end of the spectrum: byte-identical JSON via key sort + ECMAScript number formatting — the canonicalization model for signing/hashing readable payloads.
   - *concepts:* canonicalization, I-JSON subset, lexicographic key sort, byte-identical output · *maps_to:* S3, gap3 · **strong**
7. **ITU-T X.690: ASN.1 Encoding Rules — BER/CER/DER** — ITU-T (2021) — ISO/IEC 8825-1 — https://www.itu.int/rec/T-REC-X.690-202102-I/en
   - The canonical-vs-permissive lineage CBOR/DER determinism descends from: BER leaves choices, DER/CER remove them for unique cryptographic encodings — foundational to the tolerant-reader vs canonical debate.
   - *concepts:* TLV, BER permissiveness, DER definite-length uniqueness, canonical encoding · *maps_to:* S1, S3, gap8 · **seminal**
8. **ITU-T X.691: ASN.1 Packed Encoding Rules (PER)** — ITU-T (2021) — ISO/IEC 8825-2 — https://www.itu.int/rec/T-REC-X.691/en
   - The schema-driven, no-tag-on-the-wire extreme — maximal compactness by relying entirely on shared schema, contrasting self-describing TLV formats.
   - *concepts:* packed encoding, schema-driven no-tags, bit-aligned compactness · *maps_to:* S1, gap2 · **strong**
9. **MessagePack Specification** — Furuhashi (2013) — msgpack.org — https://github.com/msgpack/msgpack/blob/master/spec.md `[INTERNAL-OVERLAP qmedit-50]`
   - Compact wire-efficient self-describing binary JSON superset (non-string map keys, binary blobs) — a leading candidate for compact CRDT-op framing on the network end.
   - *concepts:* self-describing binary, wire compactness, non-string keys, JSON superset · *maps_to:* S1, gap8 · **strong**
10. **BSON (Binary JSON) Specification** — MongoDB (2009) — bsonspec.org — https://bsonspec.org/spec.html `[INTERNAL-OVERLAP qmedit-50]`
    - Length-prefixed, traversal-optimized binary JSON with extra types — the in-memory-manipulation-optimized point, contrasting MessagePack's wire optimization; embedded lengths enable skip.
    - *concepts:* length-prefixed docs, in-place traversal, extended types, skip via embedded length · *maps_to:* S1, gap8 · **ok**
11. **Protocol Buffers: Encoding (wire format) + proto3 guide** — Google (2024) — protobuf.dev — https://protobuf.dev/programming-guides/encoding/ `[INTERNAL-OVERLAP qmedit-50]`
    - Normative reference for the de-facto baseline: `tag=(field<<3)|wire_type`, varint, unknown-field skipping — the concrete mechanism enabling length-prefixed skip-unknown parsing and the discriminator model gap8 needs.
    - *concepts:* field tag numbers, wire types, varint/zigzag, packed repeated, unknown-field skip · *maps_to:* S1, S4, gap8 · **seminal**
12. **LEB128 (Little Endian Base 128) variable-length integers** — DWARF Committee (2017) — DWARF standard — https://en.wikipedia.org/wiki/LEB128
    - The continuation-bit varint scheme underlying Protobuf/WASM/DWARF/Automerge — the primitive for length-prefixes and compact small-integer fields in any op codec.
    - *concepts:* continuation bit, 7-bit groups, signed/unsigned LEB128 · *maps_to:* gap8 · **ok**
13. **SFVInt: Fast Generic Variable-Length Integer Decoding using Bit-Manipulation Instructions** — Liao et al. (2024) — arXiv 2403.06898 — https://arxiv.org/abs/2403.06898
    - State-of-the-art branchless/SIMD varint decoding — evidence that length-prefix/varint framing need not bottleneck high-volume CRDT streams.
    - *concepts:* varint decoding, bit-manipulation, SIMD, throughput · *maps_to:* gap8 · **ok**
14. **SPKI/SDSI S-Expressions** (draft-rivest-sexp) — Rivest (1997) — IETF I-D — https://datatracker.ietf.org/doc/draft-rivest-sexp/ *(042: original MIT URL people.csail.mit.edu/rivest/Sexp.txt now 404 — link-rot, EP-F2-030)* `[INTERNAL-OVERLAP qmedit-50]`
    - Head-tagged, self-describing octet-string/list structure with a length-prefixed **canonical** form for signing — a minimal, spectrum-spanning model where the same tree has readable and canonical binary encodings.
    - *concepts:* head-tagged lists, canonical S-expression, length-prefixed strings, transport vs canonical form · *maps_to:* S1, S3, gap2, gap3 · **seminal**
15. **A Type-Theoretic Model on NDN-TLV Encoding** — Ma, Afanasyev, Zhang (2022) — ACM ICN 2022 — https://dl.acm.org/doi/10.1145/3517212.3558093
    - Peer-reviewed formal (type-theoretic) model of TLV packet encoding — rigorous grounding for a tolerant-reader/skip-unknown wire format with provable structure.
    - *concepts:* TLV formal model, type theory, packet-format verification, forward compat · *maps_to:* S4, gap8 · **strong**
16. **NDN Packet Format Specification v0.3 (TLV)** — Named Data Networking project (2021) — NDN tech spec — https://docs.named-data.net/NDN-packet-spec/current/tlv.html
    - A production TLV spec with explicit evolvability + unknown-TLV rules — practical reference for designing an extensible message envelope with a type discriminator.
    - *concepts:* nested TLV, non-negative-integer TLV-TYPE ranges, unknown handling · *maps_to:* S4, gap8 · **ok**
17. **Efficient XML Interchange (EXI) 1.0 (2nd Ed.)** — W3C EXI WG (2014) — W3C Recommendation — https://www.w3.org/TR/exi/
    - Schema-informed/schema-less binary encoding of the XML Infoset via grammar-driven entropy coding — the most compact end of the readable→binary spectrum for tree-structured data.
    - *concepts:* binary XML, schema-informed grammars, entropy coding, compact tree encoding · *maps_to:* S1, gap2 · **strong**
18. **ITU-T X.891 | ISO/IEC 24824-1: Fast Infoset** — ITU-T / ISO-IEC (2005) — ITU-T Rec. / ISO standard — https://www.itu.int/en/ITU-T/asn1/Pages/Fast-Infoset.aspx
    - ASN.1/ECN-specified binary XML Infoset — a lighter binary-XML point than EXI, illustrating size-vs-simplicity choices for readable tree data.
    - *concepts:* binary XML Infoset, ASN.1 ECN, string/vocabulary tables · *maps_to:* S1, gap2 · **ok**
19. **MUMPS globals: hierarchical sparse tree data model (ISO/IEC 11756 M)** — MUMPS Development Committee (1995) — ISO/IEC 11756 / bitsavers TR — http://www.bitsavers.org/pdf/mumps/MUMPS_Globals_and_their_Implementation_May1975.pdf `[INTERNAL-OVERLAP qmedit-50]`
    - Persistent sparse multi-dimensional trees (subscripts→branches, values→leaves) prefiguring JSON-like hierarchical data — a precedent for encoding a CRDT's sparse keyed tree.
    - *concepts:* sparse hierarchical tree, subscripted keys, only-assigned-nodes-exist, B-tree storage · *maps_to:* S1, gap2 · **ok**

---

## 3. Schema evolution & bidirectional lenses — 19

Bears on **S4** (version tolerance/repair), **S7**, **gap3** (tombstone), **gap4** (repair). Lens theory is the closest formal machinery for the epic's cross-version translation.

1. **Cambria: Schema Evolution in Distributed Systems with Edit Lenses** — Litt, van Hardenberg, Henry (2021) — PaPoC 2021 (EuroSys wksp) / ACM — https://dl.acm.org/doi/abs/10.1145/3447865.3457963 `[INTERNAL-OVERLAP beacon-42]`
   - **The single closest prior art for this epic:** bidirectional edit lenses translate documents between schema versions on demand, integrated with the Automerge CRDT for P2P editing — exactly the CRDT + multi-format compatibility problem.
   - *concepts:* edit lenses, bidirectional schema translation, forward/backward compat, CRDT integration · *maps_to:* S4, S7, gap6 · **seminal**
2. **Edit Lenses** — Hofmann, Pierce, Wagner (2012) — POPL 2012 — https://dl.acm.org/doi/10.1145/2103621.2103715
   - The foundation Cambria builds on — lenses that translate **edits (deltas)** rather than whole states, over monoids of edits — the right model for propagating incremental CRDT changes across schema versions.
   - *concepts:* edit-based lenses, monoid of edits, partial monoid action, delta propagation · *maps_to:* S4, gap4 · **seminal**
3. **Symmetric Lenses** — Hofmann, Pierce, Wagner (2011) — POPL 2011 — https://www.cis.upenn.edu/~bcpierce/papers/symmetric.pdf
   - Generalizes asymmetric lenses to a truly symmetric setting where each side holds info the other lacks and composition holds — the model needed when two schema variants are peers, not primary+projection.
   - *concepts:* symmetric lenses, composition, complement, peer schemas · *maps_to:* S4, gap1 · **seminal**
4. **Symmetric Edit Lenses: A New Foundation for Bidirectional Languages** — Wagner (2014) — PhD dissertation, U. Pennsylvania — https://repository.upenn.edu/edissertations/1488/
   - Full development of symmetric edit lenses as a modular framework for bidirectional synchronizers — deepest reference for correctness laws + composition of edit-based translators.
   - *concepts:* symmetric edit lenses, modular synchronizers, lens laws, compositional construction · *maps_to:* S4, gap4 · **strong**
5. **Combinators for Bidirectional Tree Transformations (the view-update problem)** — Foster, Greenwald, Moore, Pierce, Schmitt (2007) — ACM TOPLAS 29(3) — https://dl.acm.org/doi/10.1145/1232420.1232424
   - The origin of the lens abstraction and its GET/PUTBACK well-behavedness laws for tree data — foundational vocabulary for any principled schema-translation layer over structured messages.
   - *concepts:* lenses, view-update, get/putback laws, tree combinators · *maps_to:* S4 · **seminal**
6. **Boomerang: Resourceful Lenses for String Data** — Bohannon, Foster, Pierce, Pilkiewicz, Schmitt (2008) — POPL 2008 — https://www.cis.upenn.edu/~bcpierce/papers/boomerang.pdf
   - Adds "resourcefulness" — keyed chunk alignment so reordered list items are not lost/corrupted on translate-back — the exact hazard when a CRDT reorders a list and edits must round-trip across schemas.
   - *concepts:* string lenses, resourcefulness, keyed alignment, list reordering · *maps_to:* S4, gap4 · **strong**
7. **Introduction to Bidirectional Transformations** — Abou-Saleh, Cheney, Gibbons, McKinna, Stevens (2018) — Springer LNCS 9715 / Oxford TR — https://www.cs.ox.ac.uk/people/jeremy.gibbons/publications/ssbx-intro.pdf
   - Tutorial survey unifying set-based, delta-based, and edit-based lenses — the map of the BX design space to justify which lens flavor fits CRDT multi-format compatibility.
   - *concepts:* BX survey, set vs delta vs edit, consistency restoration, round-tripping laws · *maps_to:* S4 · **strong**
8. **Apache Avro Specification (schema + resolution rules)** — Apache Software Foundation (2024) — Apache reference spec — https://avro.apache.org/docs/current/specification/
   - Schema-driven, **tagless** binary with a JSON-defined schema and explicit reader/writer resolution (name-matched fields, defaults for added fields, ignored unknowns) — the model for evolving message schemas without wire tags.
   - *concepts:* JSON-defined schema, tagless binary, reader/writer resolution, defaults, aliases · *maps_to:* S1, S4, S7 · **seminal**
9. **Schema Evolution in Avro, Protocol Buffers and Thrift** — Kleppmann (2012) — martin.kleppmann.com (basis for DDIA Ch.4) — https://martin.kleppmann.com/2012/12/05/schema-evolution-in-avro-protocol-buffers-thrift.html
   - The definitive practitioner comparison of tag-number vs name-based encodings for forward/backward compatibility — core design guidance for versioning messages in a long-lived CRDT protocol.
   - *concepts:* forward/backward compat, field tags vs names, reader/writer resolution, optional fields · *maps_to:* S4, S7 · **strong**
10. **Protocol Buffers — Field Presence (application note)** — Google (2023) — protobuf.dev — https://protobuf.dev/programming-guides/field_presence/
    - Implicit vs explicit presence: binary-compatible but semantically load-bearing — a subtle compatibility trap directly relevant to encoding optional/absent fields across versions.
    - *concepts:* implicit vs explicit presence, default-value ambiguity, wire compat, editions · *maps_to:* S4 · **strong**
11. **Schema Registry — Compatibility Types (Backward/Forward/Full/Transitive)** — Confluent (2024) — Confluent Platform docs — https://docs.confluent.io/platform/current/schema-registry/fundamentals/schema-evolution.html
    - The industry-standard taxonomy of compatibility modes + transitive checking against all prior versions — the vocabulary and enforcement model any multi-version messaging system must adopt.
    - *concepts:* backward/forward/full/transitive compat, registry enforcement, self-describing schema id · *maps_to:* S4, S7 · **ok**
12. **ParallelChange / Expand-and-Contract** — Danilo Sato (bliki, Martin Fowler) (2014) — martinfowler.com — https://martinfowler.com/bliki/ParallelChange.html `[INTERNAL-OVERLAP beacon-42]`
    - The canonical expand-migrate-contract discipline for breaking schema changes without a big-bang cutover — the operational rollout counterpart to lens-based translation.
    - *concepts:* expand-migrate-contract, parallel change, zero-downtime migration, coexisting old/new · *maps_to:* S4, gap4 · **ok**
13. **Graceful Database Schema Evolution: the PRISM Workbench** — Curino, Moon, Zaniolo (2008) — VLDB 2008 / PVLDB 1(1) — https://www.vldb.org/pvldb/vol1/1453939.pdf
    - Schema Modification Operators with automatic query rewriting, inverse computation, data migration — validated on Wikipedia's 170+ schema versions; the rigorous DB analogue of invertible schema translation.
    - *concepts:* schema modification operators, query rewriting, invertibility, information preservation · *maps_to:* S4, gap4 · **seminal**
14. **Synthesizing Database Programs for Schema Refactoring** — Wang, Dong, Shah, Dillig (2019) — PLDI 2019 / arXiv 1904.05498 — https://arxiv.org/pdf/1904.05498
    - Program synthesis that migrates the programs interacting with a refactored schema while proving semantic equivalence — points to automating the code side of a schema change, not just the data side.
    - *concepts:* program synthesis, schema refactoring, equivalence verification, automated migration · *maps_to:* S4, gap4 · **strong**
15. **Heterogeneous Coupled Evolution of Software Languages** — Vermolen, Visser (2008) — MODELS 2008 / LNCS 5301 — https://eelcovisser.org/publications/2008/VermolenV08.pdf
    - Framework where evolving a schema/metamodel automatically derives the coupled migration of conforming data — the theory behind keeping data and schema in lockstep.
    - *concepts:* coupled evolution, metamodel/model co-evolution, derived migration transforms · *maps_to:* S4, gap4 · **strong**
16. **COPE — Automating Coupled Evolution of Metamodels and Models** — Herrmannsdoerfer, Benz, Juergens (2009) — ECOOP 2009 / LNCS 5653 — https://link.springer.com/chapter/10.1007/978-3-642-03013-0_4
    - Incrementally composes modular coupled transformations with reusable recurring operators — a library-of-migrations approach mirroring Cambria's lens catalog.
    - *concepts:* coupled transformations, reusable migration operators, model migration, operator library · *maps_to:* S4, gap4 · **strong**
17. **Schema Evolution in Interactive Programming Systems** — Edwards, Petricek, van der Storm et al. (2025) — Programming 9(1) / arXiv 2412.06269 — https://arxiv.org/abs/2412.06269
    - Systematizes schema evolution into layers/dimensions with challenge problems spanning DB, live front-end, MDE, and **collaborative/CRDT documents head-on** — a research-grade framing of the epic's exact case.
    - *concepts:* layers/dimensions of schema evolution, challenge problems, collaborative documents · *maps_to:* S4, S7, gap6 · **strong**
18. **Modeling Data (Automerge Cookbook) — Schema Migration in CRDTs** — Automerge project (2024) — automerge.org docs — https://automerge.org/docs/cookbook/modeling-data/
    - Documents the concrete hazard that two peers independently running the same CRDT migration must not clash, plus the embedded-version-number approach — the practitioner statement of the gap Cambria addresses.
    - *concepts:* CRDT schema migration, concurrent duplicate migrations, embedded schema version · *maps_to:* S4, gap6 · **ok**
19. **Baseline: Operation-Based Evolution and Versioning of Data** — Edwards et al. (2025) — arXiv 2512.09762 — https://www.arxiv.org/pdf/2512.09762
    - A recent operation-based (edit/delta) model for evolving+versioning data — connects the edit-lens delta philosophy to concrete data-versioning, complementary to CRDT op-logs.
    - *concepts:* operation-based evolution, data versioning, edit/delta model, reversible operations · *maps_to:* S4, gap4 · **ok**

---

## 4. Skip-unknown / tolerant-reader / extensibility — 10

Bears on **S4** (version tolerance) and **gap8** (discriminator). Includes the essential counterweight (why blind tolerance is harmful).

1. **RFC 6709: Design Considerations for Protocol Extensions** — Carpenter, Aboba (Ed.), Cheshire (2012) — IETF (Informational) — https://www.rfc-editor.org/rfc/rfc6709
   - The canonical treatment of designing a base format so unknown extensions are skipped not rejected — "MBZ = Must Be Ignored", reserved fields, version-negotiation pitfalls a CRDT multi-format wire must get right.
   - *concepts:* must-ignore/MBZ, reserved fields, unambiguous extension semantics, version negotiation · *maps_to:* S4, gap8 · **seminal**
2. **RFC 9170: Long-Term Viability of Protocol Extension Mechanisms** — Thomson, Pauly (2022) — IETF (Informational) — https://datatracker.ietf.org/doc/html/rfc9170
   - Extension points rot unless exercised ("greasing"): unused must-ignore slots ossify because implementations quietly reject them — informs how a skip-unknown format keeps extensibility real over time.
   - *concepts:* protocol ossification, greasing, active use of must-ignore fields, graceful degradation · *maps_to:* S4, gap8 · **strong**
3. **The Harmful Consequences of the Robustness Principle** (RFC 9413 / draft-iab-protocol-maintenance) — Thomson (IAB) (2021) — IETF/IAB — https://www.ietf.org/archive/id/draft-iab-protocol-maintenance-05.html
   - The definitive critique of Postel's Law: liberal acceptance of unknowns entrenches bugs as de-facto standards and constrains extensibility — the cautionary counterweight to a naive skip-unknown design.
   - *concepts:* robustness-principle critique, tolerant-reader hazards, spec entrenchment, must-ignore vs must-validate · *maps_to:* S4 · **strong**
4. **SOAP Version 1.2 Part 1: Messaging Framework (2nd Ed.)** — Gudgin, Hadley, Mendelsohn, Moreau, Nielsen, Karmarkar, Lafon (W3C) (2007) — W3C Recommendation — https://www.w3.org/TR/soap12-part1/
   - Defines the **mustUnderstand** processing model — the archetype for distinguishing headers a receiver may silently ignore from those it must reject if unrecognized — exactly the two-tier signal a multi-format layer needs.
   - *concepts:* mustUnderstand, must-ignore vs must-understand fault, header roles, extensibility model · *maps_to:* S4, gap8 · **seminal**
5. **Extending and Versioning Languages: XML Languages** (W3C TAG finding) — Orchard, Walsh (2007) — W3C TAG (editorial draft) — https://www.w3.org/2001/tag/doc/versioning-xml-20070326.html
   - Formalizes "Must Ignore Unknowns"/"Must Accept Unknowns" rules + compatibility terminology — the vocabulary a CRDT format's extensibility spec should adopt.
   - *concepts:* Must-Ignore-Unknowns, backward/forward compat, extension elements/wildcards · *maps_to:* S4, gap8 · **strong**
6. **Designing Extensible, Versionable XML Formats** — Orchard (2004) — XML.com — https://www.xml.com/pub/a/2004/07/21/design.html
   - Practitioner design guide for wildcard extension points and must-ignore-unknowns that lets old readers accept new-version documents — a concrete template for skip-unknown in a document/messaging format.
   - *concepts:* extensibility points/wildcards, must-ignore, versioning strategy, open content models · *maps_to:* S4 · **strong**
7. **Protocol Buffers Language Guide (proto3): Unknown Fields** — Google (2023) — protobuf.dev — https://protobuf.dev/programming-guides/proto3/
   - Reference for proto3's decision to **preserve unknown fields** through parse and re-serialize — the industry-standard model for round-trip fidelity a middleware/relay in a multi-format CRDT pipeline must replicate.
   - *concepts:* unknown-field preservation, tag-based skip, round-trip fidelity, message merge · *maps_to:* S4, gap8 · **strong**
8. **Preservation of unknown fields in JSON (protobuf issue #2289)** — Protobuf contributors (2017) — GitHub issue — https://github.com/protocolbuffers/protobuf/issues/2289
   - Documents the concrete failure where converting to JSON/text or copying field-by-field silently drops unknown fields — a load-bearing caution for any binary↔JSON CRDT bridge that must not lose unknowns on transcode.
   - *concepts:* unknown-field loss on transcode, binary vs JSON fidelity, format-conversion hazards · *maps_to:* S4, gap2 · **ok**
9. **Version-Tolerant Serialization (.NET)** — Microsoft (2023) — Microsoft Learn — https://learn.microsoft.com/en-us/dotnet/framework/serialization/version-tolerant-serialization
   - Reference implementation of tolerant-reader semantics (extraneous data ignored, optional fields degrade gracefully) — a mainstream engineering pattern for skip-unknown deserialization.
   - *concepts:* version-tolerant serialization, tolerant reader, ignore extraneous, graceful degradation · *maps_to:* S4 · **ok**
10. **Protocol Buffers Language Guide (Editions)** — Google (2024) — protobuf.dev — https://protobuf.dev/programming-guides/editions/
    - Successor to proto2/proto3 making forward/backward-compat behaviors (incl. unknown-field retention) explicit per-feature — a model for evolving a wire format's skip-unknown policy without a hard version break.
    - *concepts:* editions vs version numbers, per-feature compat flags, unknown-field retention · *maps_to:* S4 · **ok**

---

## 5. CRDT foundations — 20

Bears on **S6** (substrate), **gap6** (message-vs-store), **gap3** (tombstones). The definitional + sequence/JSON/rich-text lineage.

1. **Conflict-free Replicated Data Types (SSS 2011)** — Shapiro, Preguiça, Baquero, Zawirski (2011) — SSS 2011, LNCS 6976 — https://inria.hal.science/inria-00609399v1 `[INTERNAL-OVERLAP beacon-42]`
   - The seminal paper defining CRDTs + Strong Eventual Consistency (SEC), giving the sufficient conditions (semilattice monotonicity / op commutativity) any convergent messaging datatype must satisfy.
   - *concepts:* SEC, CvRDT, CmRDT, join semilattice, monotonic merge · *maps_to:* S6, gap6 · **seminal**
2. **A Comprehensive Study of Convergent and Commutative Replicated Data Types (RR-7506)** — Shapiro, Preguiça, Baquero, Zawirski (2011) — INRIA RR-7506 — https://inria.hal.science/inria-00555588/en/ `[INTERNAL-OVERLAP beacon-42]`
   - The exhaustive companion with the reference portfolio (counters, LWW/MV registers, G-Set/2P-Set/OR-Set, graphs, sequences) — the go-to catalogue when choosing a datatype per message field.
   - *concepts:* CvRDT/CmRDT, LWW/MV register, OR-Set, sequence CRDT, counter · *maps_to:* S6, gap6 · **seminal**
3. **Delta State Replicated Data Types** — Almeida, Shoker, Baquero (2018) — JPDC 111 / arXiv 1603.01529 — https://arxiv.org/abs/1603.01529 `[INTERNAL-OVERLAP beacon-42]`
   - δ-CRDTs ship small join-irreducible delta-mutators instead of full state — the key efficiency technique for low-bandwidth CRDT sync in a messaging transport.
   - *concepts:* delta-state CRDT, delta-mutator, join-irreducible, anti-entropy, causal delta merge · *maps_to:* S6, gap4 · **strong**
4. **Efficient State-based CRDTs by Delta-Mutation** — Almeida, Shoker, Baquero (2015) — NETYS 2015 / arXiv 1410.2803 — https://arxiv.org/abs/1410.2803 `[INTERNAL-OVERLAP beacon-42]`
   - The original delta-mutation paper: op-based-sized messages over unreliable channels while keeping state-based merge semantics — foundational for bandwidth-bounded replication.
   - *concepts:* delta-mutation, small messages, idempotent/associative/commutative join · *maps_to:* S6, gap4 · **strong**
5. **Pure Operation-Based Replicated Data Types** — Baquero, Almeida, Shoker (2017) — arXiv 1710.04469 — https://arxiv.org/abs/1710.04469
   - Pure op-based CRDTs over a tagged reliable causal broadcast (TRCB) with a PO-Log, using causal stability to strip metadata — the model closest to a pure message-passing/broadcast substrate.
   - *concepts:* pure op-based, TRCB, PO-Log, causal stability, metadata compaction · *maps_to:* S6, gap6 · **strong**
6. **Making Operation-Based CRDTs Operation-Based** — Baquero, Almeida, Shoker (2014) — DAIS 2014, LNCS 8460 — https://ifip.hal.science/hal-01287738v1
   - The original pure-op-based paper: removes hidden state so operations dispatch purely on causal delivery — the conceptual basis for message-driven CRDT dissemination.
   - *concepts:* pure op-based, causal delivery, PO-Log, commutative operations · *maps_to:* S6, gap6 · **strong**
7. **An Optimized Conflict-free Replicated Set** — Bieniusa, Zawirski, Preguiça, Shapiro, Baquero, Balegas, Duarte (2012) — INRIA RR-8083 / arXiv 1210.3368 — https://arxiv.org/abs/1210.3368 `[INTERNAL-OVERLAP beacon-42]`
   - The optimized add-wins Observed-Remove Set — the canonical set CRDT with tombstone-free space complexity; reference for replicated set-valued message state.
   - *concepts:* OR-Set, observed-remove, add-wins, tombstone optimization, unique tags · *maps_to:* S6, gap3 · **strong**
8. **Verifying Strong Eventual Consistency in Distributed Systems** — Gomes, Kleppmann, Mulligan, Beresford (2017) — OOPSLA 2017 / PACMPL / arXiv 1707.01747 — https://arxiv.org/abs/1707.01747
   - The first machine-checked (Isabelle/HOL) framework proving SEC with a network model, verifying RGA, OR-Set, and a counter — the formal-methods anchor for trusting CRDT convergence claims.
   - *concepts:* SEC, Isabelle/HOL, abstract convergence theorem, network model, RGA/OR-Set verification · *maps_to:* S6, gap6 · **seminal**
9. **Replicated Abstract Data Types: Building Blocks for Collaborative Applications (RGA)** — Roh, Jeon, Kim, Lee (2011) — JPDC 71(3) — https://www.sciencedirect.com/science/article/pii/S0743731510002716 `[INTERNAL-OVERLAP beacon-42]`
   - Introduces the Replicated Growable Array (RGA), the timestamp-ordered sequence CRDT with addAfter/remove — the foundational list CRDT for ordered message/text sequences.
   - *concepts:* RGA, sequence CRDT, causal timestamps, addAfter/remove · *maps_to:* S6 · **strong**
10. **A Commutative Replicated Data Type for Cooperative Editing (Treedoc)** — Preguiça, Marquès, Shapiro, Leția (2009) — ICDCS 2009 — https://inria.hal.science/inria-00445975
    - The first dense-identifier (extended binary tree) sequence CRDT — the origin of position-identifier ordering used by later list CRDTs.
    - *concepts:* Treedoc, dense identifier space, binary-tree positions, sequence CRDT · *maps_to:* S6 · **seminal**
11. **Logoot: A Scalable Optimistic Replication Algorithm for Collaborative Editing on P2P Networks** — Weiss, Urso, Molli (2009) — ICDCS 2009 — https://inria.hal.science/inria-00432368 `[INTERNAL-OVERLAP beacon-42]`
    - Unbounded dense position identifiers for a P2P sequence CRDT that scales in users/edits without tombstones — a core ordered-list technique for large-scale replication.
    - *concepts:* Logoot, position identifiers, dense ordering, tombstone-free sequence · *maps_to:* S6 · **strong**
12. **LSEQ: An Adaptive Structure for Sequences in Distributed Collaborative Editing** — Nédelec, Molli, Mostéfaoui, Desmontils (2013) — DocEng 2013 — https://dl.acm.org/doi/10.1145/2494266.2494278
    - Improves on Logoot with an adaptive allocation strategy bounding identifier growth — the space-complexity refinement for scalable ordered-sequence CRDTs.
    - *concepts:* LSEQ, adaptive identifier allocation, sublinear growth · *maps_to:* S6 · **ok**
13. **Yjs / YATA: Near Real-Time P2P Shared Editing on Extensible Data Types** — Nicolaescu, Jahns, Derntl, Klamma (2016) — ACM GROUP 2016 — https://dl.acm.org/doi/10.1145/2957276.2957310 `[INTERNAL-OVERLAP beacon-42]`
    - The YATA algorithm behind Yjs — a high-performance, widely-deployed P2P shared-editing CRDT for arbitrary data types; the practical reference implementation for real-world CRDT messaging.
    - *concepts:* YATA, Yjs, P2P shared editing, arbitrary data types, intention preservation · *maps_to:* S6, gap6 · **strong**
14. **A Conflict-Free Replicated JSON Datatype** — Kleppmann, Beresford (2017) — IEEE TPDS 28(10) / arXiv 1608.03960 — https://arxiv.org/abs/1608.03960 `[INTERNAL-OVERLAP beacon-42]`
    - Formal algorithm + semantics for a nested map/list JSON CRDT (basis of Automerge) — directly relevant to multi-format structured messages with client-side, order-independent merge.
    - *concepts:* JSON CRDT, nested map/list, Automerge, client-side merge, conflict-free assignment · *maps_to:* S6, gap6 · **seminal**
15. **Peritext: A CRDT for Collaborative Rich Text Editing** — Litt, Lim, Kleppmann, van Hardenberg (2022) — CSCW 2022 / PACMHCI 6(CSCW2) — https://www.inkandswitch.com/peritext/static/cscw-publication.pdf `[INTERNAL-OVERLAP beacon-42]`
    - The first published rich-text CRDT: formatting spans anchored to stable character IDs so concurrent formatting commutes — the reference for formatted/multi-format message content AND the CRDT-native analogue of preserving unhandled (unknown) marks.
    - *concepts:* rich-text CRDT, formatting spans, stable char IDs, intent preservation, preserving unhandled marks · *maps_to:* S6, S4, gap3 · **strong**
16. **The Art of the Fugue: Minimizing Interleaving in Collaborative Text Editing** — Weidner, Gentle, Kleppmann (2023) — arXiv 2305.00583 (IEEE TPDS 2025) — https://arxiv.org/abs/2305.00583 `[INTERNAL-OVERLAP beacon-42]`
    - Defines maximal non-interleaving as a correctness property for list CRDTs and gives Fugue/FugueMax — critical for preventing corrupted merges when two peers insert at the same position.
    - *concepts:* Fugue/FugueMax, maximal non-interleaving, interleaving anomaly, correctness property · *maps_to:* S6 · **strong**
17. **Dotted Version Vectors: Logical Clocks for Optimistic Replication** — Preguiça, Baquero, Almeida, Fonte, Gonçalves (2010) — arXiv 1011.5808 — https://arxiv.org/abs/1011.5808 `[INTERNAL-OVERLAP beacon-42]`
    - Accurate, scalable causality tracking (vector size linear in servers, not clients/updates) — the logical-clock substrate underpinning causal delivery for CRDT messaging.
    - *concepts:* dotted version vector, logical clocks, causality tracking, concurrent-version detection · *maps_to:* S6, gap6 · **strong**
18. **Scalable and Accurate Causality Tracking for Eventually Consistent Stores** — Almeida, Baquero, Gonçalves, Preguiça, Fonte (2014) — DAIS 2014, LNCS 8460 — https://link.springer.com/content/pdf/10.1007/978-3-662-43352-2_6.pdf `[INTERNAL-OVERLAP beacon-42]`
    - The full server-side dotted-version-vector-set (DVVS) design for get/put stores — the practical causality mechanism enabling correct concurrent-write detection under eventual consistency.
    - *concepts:* DVVS, causality tracking, concurrent writes, version pruning · *maps_to:* S6, gap6 · **strong**
19. **Local-First Software: You Own Your Data, in spite of the Cloud** — Kleppmann, Wiggins, van Hardenberg, McGranaghan (2019) — Onward! 2019 (SPLASH) / ACM — https://www.inkandswitch.com/essay/local-first/
    - The manifesto positioning CRDTs as the foundation for offline-capable collaborative software — the context making on-device, P2P schema evolution + messaging a first-class requirement.
    - *concepts:* seven ideals, CRDTs, offline collaboration, data ownership, longevity · *maps_to:* S6, S8 · **seminal**
20. **OpSets: Sequential Specifications for Replicated Datatypes** — Kleppmann, Gomes, Mulligan, Beresford (2018) — ECOOP 2018 / arXiv 1805.04263 — https://arxiv.org/abs/1805.04263
    - Specifies list/map/tree/register CRDT semantics as a totally-ordered operation set with Isabelle proofs — a composable, verifiable model for defining multi-format replicated datatypes.
    - *concepts:* OpSets, sequential specification, operation ordering, verified tree/list/map, move ops · *maps_to:* S6, gap6 · **strong**

---

## 6. CRDT systems (production engines / performance) — 8

Bears on **S6**, **gap6**, plus binary-encoding of CRDT history (**S1/gap2**).

1. **Extending JSON CRDTs with Move Operations** — Da, Kleppmann (2024) — PaPoC '24 / arXiv 2311.14007 — https://arxiv.org/abs/2311.14007
   - Adds subtree-move/list-reorder to JSON CRDTs without duplicates or cycles — needed whenever collaborative messages carry reorderable/movable structure.
   - *concepts:* move operation, JSON CRDT, list reordering, cycle avoidance · *maps_to:* S6, gap6 · **strong**
2. **A Highly-Available Move Operation for Replicated Trees** — Kleppmann, Mulligan, Gomes, Beresford (2022) — IEEE TPDS — https://martin.kleppmann.com/papers/move-op.pdf
   - A conflict-free, highly-available algorithm for moving nodes in a replicated tree (file/folder hierarchies) — foundational for tree-structured CRDT state.
   - *concepts:* replicated tree, move operation, undo/redo log, highly available · *maps_to:* S6, gap6 · **strong**
3. **Automerge 2.0 / Binary Document Format (columnar encoding)** — Automerge project / Good, Kleppmann, Henry et al. (2023) — automerge.org binary-format spec — https://automerge.org/automerge-binary-format-spec/ `[INTERNAL-OVERLAP beacon-42]`
   - Columnar + RLE + LEB128 (+ DEFLATE) binary encoding of full CRDT history at ~1.1 B/op — the practical storage/wire format enabling CRDT messaging at scale; concrete answer for compact on-wire encoding of composable CRDT documents.
   - *concepts:* columnar storage, RLE, LEB128 varint, DEFLATE, compact history, binary wire format · *maps_to:* S1, S6, gap2 · **strong**
4. **Collaborative Text Editing with Eg-walker: Better, Faster, Smaller** — Gentle, Kleppmann (2025) — EuroSys 2025 / arXiv 2409.14252 — https://arxiv.org/abs/2409.14252 `[INTERNAL-OVERLAP beacon-42]`
   - Event-graph hybrid of OT+CRDT: order-of-magnitude less steady-state memory and faster load/merge than classic CRDTs — the current performance frontier, key for scaling composable CRDT message documents.
   - *concepts:* event graph, OT/CRDT hybrid, memory efficiency, fast load, history compression · *maps_to:* S6, gap2 · **strong**
5. **Collabs: A Flexible and Performant CRDT Collaboration Framework** — Weidner, Miller et al. (2023) — arXiv 2212.02618 — https://arxiv.org/abs/2212.02618
   - A composable CRDT framework letting apps build/compose custom CRDTs with SEC; scales rich-text to 100+ concurrent users — relevant to composing message-level datatypes.
   - *concepts:* composable CRDTs, semantic flexibility, framework design, scalability · *maps_to:* S6, gap6 · **strong**
6. **cr-sqlite: Convergent, Replicated SQLite** — Wonlaw / vlcn.io (2023) — open-source (vlcn-io/cr-sqlite) — https://github.com/vlcn-io/cr-sqlite `[INTERNAL-OVERLAP beacon-42]`
   - Runtime SQLite extension adding multi-master CRDT merge (LWW/counter/fractional-index columns) so relational rows sync conflict-free — a practical replicated-DB substrate.
   - *concepts:* SQLite extension, multi-master, row/column CRDTs, LWW, causal event log · *maps_to:* S6 · **ok**
7. **ElectricSQL: Local-first sync for Postgres (active-active CRDT)** — ElectricSQL team incl. Preguiça, Shapiro, Balegas (2023) — electric-sql.com docs — https://legacy.electric-sql.com/docs/intro/active-active `[INTERNAL-OVERLAP beacon-42]`
   - Postgres↔SQLite active-active sync using CRDTs for deterministic merge; brings CRDT convergence to a mainstream relational DB from CRDT co-inventors.
   - *concepts:* active-active replication, logical replication, CRDT merge, Rich-CRDT invariants · *maps_to:* S6 · **ok**
8. **Managing Update Conflicts in Bayou, a Weakly Connected Replicated Storage System** — Terry, Theimer, Petersen, Demers, Spreitzer, Hauser (1995) — ACM SOSP 1995 — https://dl.acm.org/doi/10.1145/224056.224070
   - Pre-CRDT classic: anti-entropy propagation, version vectors, eventual consistency for weakly-connected replicas — the ancestor of CRDT sync protocols.
   - *concepts:* anti-entropy, eventual consistency, version vectors, write ordering · *maps_to:* S6 · **seminal**

*(bet365 ORSWOT / Riak-DT production report is folded into beacon-42's Riak-DT/OR-Set entry — not re-listed; it is production evidence, not a paper.)*

---

## 7. CRDT messages & stores (Byzantine / content-addressed / anti-entropy) — 14

Bears on **S6**, **gap3** (tombstone), **gap4** (repair via reconciliation), and secure replication.

1. **DSON: JSON CRDT Using Delta-Mutations for Document Stores** — Rinberg, Solomon, Shlomo, Khazma, Lushi, Keidar, Ta-Shma (2022) — PVLDB 15 — https://www.vldb.org/pvldb/vol15/p1053-rinberg.pdf
   - Delta-mutation JSON CRDT engineered for document-store back ends, cutting metadata/memory overhead vs prior JSON CRDTs — directly relevant to storing composable CRDT documents at scale.
   - *concepts:* JSON CRDT, delta-mutation, document store, space efficiency · *maps_to:* S6, gap6 · **strong**
2. **Introducing Support for Move Operations in Melda CRDT** — Brocco (2025) — arXiv 2503.04811 — https://arxiv.org/pdf/2503.04811
   - Practical move-operation support in the Melda delta-state JSON CRDT for document collections — a concrete system point for large composable CRDT documents with reorganization.
   - *concepts:* Melda, delta-state CRDT, move operations, document-collection sync · *maps_to:* S6, gap6 · **ok**
3. **Merkle Search Trees: Efficient State-Based CRDTs in Open Networks** — Auvolat, Taïani (2019) — IEEE SRDS 2019 / HAL hal-02303490 — https://inria.hal.science/hal-02303490 *(042: delivered arXiv id 1904.13396 resolves to an unrelated astrophysics paper — mistranscribed; the MST paper has no arXiv listing, EP-F2-095)*
   - Encodes CRDT state as an order-preserving balanced Merkle tree so open-network peers reconcile large key-value stores by hash comparison without a causal-broadcast primitive — the anti-entropy backbone for content stores.
   - *concepts:* Merkle search tree, state-based CRDT, anti-entropy, order-preserving hashing · *maps_to:* S6, gap4 · **seminal**
4. **Merkle-CRDTs: Merkle-DAGs meet CRDTs** — Sanjuan, Pöyhtäri, Teixeira, Psaras (2020) — arXiv 2004.00107 (Protocol Labs) — https://arxiv.org/pdf/2004.00107
   - Shows how content-addressed Merkle-DAGs provide causality tracking + efficient partial sync for CRDTs, so replicas exchange only missing DAG nodes — the model for CRDT-over-content-addressed message stores.
   - *concepts:* Merkle-DAG, content addressing, causality via hash links, partial replication · *maps_to:* S6, gap4 · **strong**
5. **IPFS — Content Addressed, Versioned, P2P File System** — Benet (2014) — arXiv 1407.3561 (Protocol Labs) — https://arxiv.org/pdf/1407.3561
   - Seminal content-addressed Merkle-DAG P2P store (CIDs, bitswap, IPLD/DAG-CBOR) underpinning content-addressed replication for CRDT message stores.
   - *concepts:* content addressing, CID, Merkle-DAG, bitswap, IPLD/DAG-CBOR · *maps_to:* S6, gap4 · **seminal**
6. **The Blocklace: A Universal, Byzantine Fault-Tolerant, Conflict-free Replicated Data Type** — Shapiro et al. (2024) — arXiv 2402.08068 — https://arxiv.org/html/2402.08068v3
   - Defines the blocklace — a partially-ordered, cryptographically-signed DAG generalization of the blockchain that is a universal Byzantine-repelling CRDT with equivocation detection. (Directly echoes glpnet's `main_GLP_to_Dart` blocklace note.)
   - *concepts:* blocklace, partial-order DAG, signed hash pointers, equivocation exclusion, universal CRDT · *maps_to:* S6, S3, gap5 · **strong**
7. **Cordial Miners: Fast and Efficient Consensus for Every Eventuality** — Keidar, Naor, Shapiro (2023) — DISC 2023 — https://drops.dagstuhl.de/entities/document/10.4230/LIPIcs.DISC.2023.26
   - Blocklace-based Byzantine Atomic Broadcast dropping reliable-broadcast for near-half latency — the ordering/dissemination protocol for secure DAG-based CRDT message propagation.
   - *concepts:* Byzantine atomic broadcast, blocklace, DAG consensus, equivocation exclusion · *maps_to:* S6, gap5 · **strong**
8. **Making CRDTs Byzantine Fault Tolerant** — Kleppmann (2022) — PaPoC 2022 — https://martin.kleppmann.com/papers/bft-crdt-papoc22.pdf
   - How to convert existing non-Byzantine CRDT algorithms into BFT, Sybil-immune ones with hash-chained causal history and modest changes — the practical recipe for secure CRDT message replication.
   - *concepts:* Byzantine fault tolerance, hash chaining, causal order in payload, Sybil resistance · *maps_to:* S6, S3, gap5 · **seminal**
9. **On CRDTs and Equivocation in Byzantine Setups** — Jacob, Bayreuther, Hartenstein (2021) — arXiv 2109.10554 — https://arxiv.org/pdf/2109.10554
   - Analyzes how equivocation (double-signing) breaks CRDT convergence under Byzantine faults and what guarantees remain — foundational for reasoning about secure CRDT message stores.
   - *concepts:* equivocation, Byzantine setups, convergence guarantees, attack model · *maps_to:* S6, gap5 · **strong**
10. **On Extend-Only Directed Posets and Derived Byzantine-Tolerant RDTs** — Jacob, Hartenstein (2023) — arXiv 2304.04318 (PaPoC 2023) — https://arxiv.org/pdf/2304.04318
    - Generalizes Byzantine-tolerant CRDTs via extend-only directed posets (grow-only signed DAGs) — a clean algebraic basis for secure, append-only CRDT message logs.
    - *concepts:* extend-only directed poset, grow-only DAG, append-only log, hash-DAG causality · *maps_to:* S6, gap5 · **strong**
11. **Efficient Synchronization of State-based CRDTs (Join Decompositions)** — Enes, Almeida, Baquero, Leitão (2019) — IEEE ICDE 2019 / arXiv 1803.02750 — https://arxiv.org/abs/1803.02750
    - Join-decomposition to compute optimal deltas and reduce delta-CRDT sync to set reconciliation of irredundant components — minimizes bytes exchanged when replicating CRDT content stores; a repair/patch primitive.
    - *concepts:* delta-state CRDT, join decomposition, optimal deltas, set reconciliation · *maps_to:* S6, gap4 · **strong**
12. **Range-Based Set Reconciliation** — Meyer (2023) — IEEE SRDS 2023 / arXiv 2212.13567 — https://arxiv.org/abs/2212.13567
    - Generic recursive range-fingerprinting protocol computing set unions with small symmetric difference — the anti-entropy engine (basis for Negentropy/Willow) behind efficient CRDT store replication + targeted repair.
    - *concepts:* range-based set reconciliation, range fingerprints, symmetric difference, P2P dissemination · *maps_to:* S6, gap4 · **strong**
13. **Decoupling Trust in Byzantine CRDTs: Fine-grained Post-Compromise Handling without Breaking Causality** — (arXiv 2606.31759) (2026) — arXiv preprint — https://arxiv.org/html/2606.31759
    - Recent proposal for fine-grained trust / post-compromise recovery in Byzantine CRDTs while preserving causal consistency — relevant to secure long-lived CRDT message stores with key rotation/compromise. *(Recent, not peer-reviewed — weight accordingly.)*
    - *concepts:* Byzantine CRDT, post-compromise security, trust decoupling, causality preservation · *maps_to:* S6, gap5 · **ok**
14. **A Composable CRDT Layer for Byzantine-Resilient Deterministic Reconstruction** — (arXiv 2606.18966) (2026) — arXiv preprint — https://arxiv.org/html/2606.18966
    - Recent design for a composable CRDT layer that deterministically reconstructs state under Byzantine faults — bridges composability (nested CRDTs) with secure replication for message/content stores. *(Recent, not peer-reviewed.)*
    - *concepts:* composable CRDT layer, Byzantine resilience, deterministic reconstruction, nested CRDTs · *maps_to:* S6, gap6 · **ok**

---

## 8. Capability tokens (macaroons / amulets / ocap lineage) — 16

Bears on **S2** (capability header) and **gap9** (amulet as live token). The token subset (macaroons/biscuit/UCAN/ZCAP/Vouchsafe) supports offline verify-before-act delegation; the OS/hardware lineage grounds the amulet.

1. **Macaroons: Cookies with Contextual Caveats for Decentralized Authorization in the Cloud** — Birgisson, Politz, Erlingsson, Taly, Vrable, Lentczner (2014) — NDSS 2014 — https://research.google/pubs/macaroons-cookies-with-contextual-caveats-for-decentralized-authorization-in-the-cloud/ `[INTERNAL-OVERLAP mstack-18]`
   - Foundational serializable bearer token whose chained-HMAC construction lets any holder attenuate authority by appending caveats without contacting the issuer — the canonical primitive for offline, verify-before-act scoped delegation. **Already the built basis of 5 internal macaroon impls.**
   - *concepts:* chained HMAC, caveat, attenuation, third-party caveats, verify-before-act · *maps_to:* S2 · **seminal**
2. **Biscuit: cryptographically-verified authorization token with offline attenuation + Datalog** — Couprie et al. (Eclipse Biscuit / Clever Cloud) (2021) — Eclipse spec — https://doc.biscuitsec.org/reference/specifications.html `[INTERNAL-OVERLAP mstack-18]`
   - Bearer token verifiable by anyone with the root public key; append-only **signed blocks** carry Datalog checks that only restrict authority = monotonic offline attenuation. Also a literal per-block Ed25519 signature chain (bears on S3/gap5).
   - *concepts:* Datalog authz, offline attenuation, append-only signed blocks, next-public-key forwarding · *maps_to:* S2, S3, gap5 · **strong**
3. **UCAN: User Controlled Authorization Network Specification** — UCAN WG (Zelenka, Holmgren, Gozalishvili et al.) (2022) — ucan-wg/spec — https://github.com/ucan-wg/spec `[INTERNAL-OVERLAP mstack-18]`
   - Extends JWT with DID-keyed, chained capability certificates for trustless local-first delegation + revocation — the reference design for user-originated authority in offline-first apps.
   - *concepts:* JWT extension, DID (did:key), capability chains, proofs, local-first authz, revocation · *maps_to:* S2 · **strong**
4. **Robust Composition: Towards a Unified Approach to Access Control and Concurrency Control** — Miller (2006) — PhD dissertation, Johns Hopkins — http://erights.org/talks/thesis/markm-thesis.pdf
   - The definitive statement of the object-capability model and least-authority composition of mutually-suspicious components — the theoretical backbone for reasoning about who may act on shared/messaged state.
   - *concepts:* object-capability model, POLA, no ambient authority, unforgeable references · *maps_to:* S2, gap9 · **seminal**
5. **The Confused Deputy (or why capabilities might have been invented)** — Hardy (1988) — ACM SIGOPS OSR 22(4) — https://dl.acm.org/doi/10.1145/54289.871709
   - Names the core failure — ambient authority — that capability tokens exist to prevent; the load-bearing argument for verify-before-act with an explicit designating capability instead of name + ambient permission.
   - *concepts:* confused deputy, ambient authority, designation vs authority, ACL failure mode · *maps_to:* S2 · **seminal**
6. **SPKI Certificate Theory (RFC 2693)** — Ellison, Frantz, Lampson, Rivest, Thomas, Ylönen (1999) — IETF RFC 2693 (Experimental) — https://www.rfc-editor.org/rfc/rfc2693
   - Standardizes authorization certificates binding privileges directly to public keys with key-to-key delegation + certificate-chain reduction — the precursor whose delegation semantics macaroons/Biscuit/UCAN descend from.
   - *concepts:* authorization certificate, key-centric principals, delegation flag, chain reduction, S-expressions · *maps_to:* S2 · **seminal**
7. **SDSI — A Simple Distributed Security Infrastructure** — Rivest, Lampson (1996) — MIT/DIMACS TR — https://people.csail.mit.edu/rivest/pubs/RL96.ver-1.1.html
   - Introduces linked local namespaces + group-membership certificates instead of a global hierarchical PKI — the naming-and-trust model later merged into SPKI/SDSI and echoed by DID-based capability chains.
   - *concepts:* linked local name spaces, key-centric principals, group membership certs, distributed trust · *maps_to:* S2 · **seminal**
8. **WAVE: A Decentralized Authorization Framework with Transitive Delegation** — Andersen, Kumar, AbdelBaky, Fierro, Kolb, Kim, Culler, Popa (2019) — USENIX Security 2019 — https://www.usenix.org/conference/usenixsecurity19/presentation/andersen `[INTERNAL-OVERLAP mstack-18]`
   - Fully decentralized authorization with cryptographically-enforced transitive delegation and encrypted-yet-discoverable permissions on untrusted storage — a production-scale blueprint for delegated authority without any central token-issuing server.
   - *concepts:* transitive delegation, decentralized trust, untrusted storage, no central authority · *maps_to:* S2 · **strong**
9. **Authorization Capabilities for Linked Data (ZCAP-LD)** — Webber, Miller (W3C-CCG) (2021) — W3C-CCG draft — https://w3c-ccg.github.io/zcap-spec/
   - Applies the ocap model to Linked Data: capabilities as signed documents chained for delegation with caveats for restriction/revocation — relevant to expressing scoped authority over structured, replicated message documents.
   - *concepts:* ocap model, linked-data proofs, capability chaining, caveats, invocation, revocation · *maps_to:* S2 · **ok**
10. **Vouchsafe: A Zero-Infrastructure Capability Graph Model for Offline Identity and Trust** — Ionzero (2026) — arXiv 2601.02254 — https://arxiv.org/abs/2601.02254
    - Models identity, delegation, revocation as self-contained signed JWT statements evaluated by purely local deterministic logic (Ed25519/SHA-256), no online authority — a near-exact fit for verify-before-act in disconnected, adversarial CRDT-messaging settings. *(Recent.)*
    - *concepts:* zero-infrastructure capability graph, self-verifying identity, offline evaluation, delegation/revocation · *maps_to:* S2, gap9 · **ok**
11. **Tahoe — The Least-Authority Filesystem** — Wilcox-O'Hearn, Warner (2008) — ACM StorageSS 2008 — https://tahoe-lafs.org/~trac/lafs.pdf
    - Cryptographic read/write/verify capabilities as the sole access-control mechanism over distributed encrypted storage — a working example of capability strings granting exactly-scoped authority over replicated data, the storage analog of a CRDT message store.
    - *concepts:* cryptographic capabilities, read/write/verify caps, POLA, diminishing (attenuating) caps · *maps_to:* S2, gap9 · **strong**
12. **EROS: A Fast Capability System** — Shapiro, Smith, Farber (1999) — ACM SOSP 1999 — https://flint.cs.yale.edu/cs428/doc/eros.pdf
    - A pure, high-performance capability OS with orthogonal persistence where all authority is conveyed by unforgeable capabilities — demonstrates a capability-only authority model is practical/fast, grounding token-based designs.
    - *concepts:* pure capability system, orthogonal persistence, unforgeable capabilities, confinement · *maps_to:* S2, gap9 · **seminal**
13. **KeyKOS Architecture (Nanokernel)** — Hardy (Key Logic / Tymshare) (1985) — USENIX / Key Logic TRs — http://cap-lore.com/CapTheory/upenn/
    - The first production pure-capability, persistent OS whose object-capability discipline + confinement guarantees are the historical root of the modern serializable capability token.
    - *concepts:* pure capability nanokernel, orthogonal persistence, confinement/factory, keys as capabilities · *maps_to:* S2, gap9 · **seminal**
14. **Using Sparse Capabilities in a Distributed Operating System (Amoeba)** — Tanenbaum, Mullender, van Renesse (1986) — IEEE ICDCS 1986 — https://research.vu.nl/en/publications/using-sparse-capabilities-in-a-distributed-operating-system
    - Introduces user-held **sparse** (cryptographically protected, unguessable) capabilities managed outside the kernel — **the direct ancestor of the epic's "amulet"** (the mstack interview defines the amulet as an Amoeba-style static 16 B token: Port/ObjNum/Rights/Check). The single most load-bearing paper for gap9.
    - *concepts:* sparse capability, cryptographic protection, unguessable object names, rights field, distributed protection · *maps_to:* S2, gap9 · **seminal**
15. **The CHERI Capability Model: Revisiting RISC in an Age of Risk** — Woodruff, Watson, Chisnall, Moore, Anderson, Davis, Laurie, Neumann, Norton, Roe (2014) — ACM/IEEE ISCA 2014 — https://www.cl.cam.ac.uk/research/security/ctsrd/pdfs/201406-isca2014-cheri.pdf `[INTERNAL-OVERLAP mstack-18]`
    - Hardware-enforced unforgeable capabilities with monotonic non-increasing bounds/permissions — the hardware embodiment of least-authority + attenuation that anchors the lineage and validates the tamper-evidence goals of software tokens.
    - *concepts:* hardware capabilities, capability bounds, monotonic narrowing, provenance/unforgeability · *maps_to:* S2, gap9 · **seminal**
16. **Robust and Compositional Verification of Object Capability Patterns (OCPL)** — Swasey, Garg, Dreyer (2017) — OOPSLA 2017 / PACMPL — https://people.mpi-sws.org/~dreyer/papers/ocpl/paper.pdf
    - A formal logic for proving ocap programs enforce authority-confinement even against arbitrary untrusted code — the verification foundation for trusting capability-token designs among mutually-suspicious peers.
    - *concepts:* ocap verification, authority confinement, separation logic, robust safety · *maps_to:* S2 · **strong**

---

## 9. Signatures & attestation — 15

Bears on **S3** (multi-signature) and **gap5** (first-class multi-sig format). Base signing primitives + detached/per-block/aggregate + transparency logs.

1. **RFC 8032: Edwards-Curve Digital Signature Algorithm (EdDSA)** — Josefsson, Liusvaara (2017) — IETF (Informational) — https://www.rfc-editor.org/rfc/rfc8032.html
   - Defines Ed25519/Ed448 — the fast, deterministic, misuse-resistant signature primitive to sign individual CRDT blocks/messages without per-signature RNG dependency. The primitive already used in qmedit per-block sealing.
   - *concepts:* Ed25519/Ed448, deterministic signatures, Schnorr/Edwards curves, PureEdDSA vs HashEdDSA · *maps_to:* S3 · **seminal**
2. **RFC 9052: CBOR Object Signing and Encryption (COSE): Structures and Process** — Schaad (2022) — IETF (Internet Standard) — https://www.rfc-editor.org/rfc/rfc9052.html
   - The CBOR-native signing container (COSE_Sign / COSE_Sign1) — a compact binary envelope for signed messages, ideal for signing CRDT payloads in binary-first transports; supports detached/external payload.
   - *concepts:* COSE, CBOR serialization, COSE_Sign1, Sig_structure, detached payload (external_aad) · *maps_to:* S3, gap5 · **strong**
3. **RFC 7515: JSON Web Signature (JWS)** — Jones, Bradley, Sakimura (2015) — IETF (Proposed Standard) — https://www.rfc-editor.org/rfc/rfc7515.html
   - Canonical JSON signing envelope; Appendix F **detached-payload** mode lets the signature travel separately from (large) content — directly the "detached signature over a CRDT block" model; JSON serialization supports multi-signature.
   - *concepts:* JWS, detached payload (App. F), protected header, multi-signature JSON serialization · *maps_to:* S3, gap5 · **seminal**
4. **Merkle Tree Ladder (MTL) Mode Signatures** (draft-harvey-cfrg-mtl-mode) — Harvey, Kaliski, Fregly, Sheth (2024) — IETF CFRG I-D — https://datatracker.ietf.org/doc/draft-harvey-cfrg-mtl-mode/
   - Amortizes one underlying signature across an evolving series of messages via Merkle-ladder authentication paths — directly applicable to signing a growing stream of CRDT ops cheaply per-block.
   - *concepts:* Merkle tree ladder, amortized signing over a series, per-message auth path · *maps_to:* S3, gap5 · **ok**
5. **Compact Multi-Signatures for Smaller Blockchains** — Boneh, Drijvers, Neven (2018) — ASIACRYPT 2018 / IACR ePrint 2018/483 — https://eprint.iacr.org/2018/483.pdf
   - Foundational BLS multi-signature with public-key aggregation — many signers over one message collapse to a single short signature, the core primitive for aggregate co-signed CRDT commits.
   - *concepts:* BLS multi-signature, public-key aggregation, rogue-key defense, signature compression · *maps_to:* S3, gap5 · **seminal**
6. **Subset-optimized BLS Multi-signature with Key Aggregation** — Baldimtsi, Chalkias, Garillot, Lindstrom et al. (Mysten Labs) (2023) — IACR ePrint 2023/498 — https://eprint.iacr.org/2023/498.pdf
   - Practical BLS multi-sig optimized when signer subsets recur — reduces aggregation/verification cost for repeated committees, relevant to per-epoch aggregate attestation over shared CRDT state.
   - *concepts:* subset-optimized aggregation, key aggregation, verification cost reduction · *maps_to:* S3, gap5 · **strong**
7. **RFC 9162: Certificate Transparency Version 2.0** — Laurie, Messeri, Stradling (2021) — IETF (Experimental) — https://www.rfc-editor.org/rfc/rfc9162.html
   - Definitive spec for an append-only Merkle-tree signed log with O(log n) inclusion + consistency proofs — the reference model for a tamper-evident signed history of CRDT updates.
   - *concepts:* append-only Merkle log, Signed Tree Head, inclusion/consistency proofs, log(n) verification · *maps_to:* S3, S6 · **seminal**
8. **RFC 6962: Certificate Transparency** — Laurie, Langley, Kasper (2013) — IETF (Experimental) — https://www.rfc-editor.org/rfc/rfc6962.html
   - The original CT design introducing the signed append-only Merkle log with audit/consistency proofs — historical bedrock for all later transparency systems.
   - *concepts:* Merkle audit proof, SCT, append-only log, monitors/auditors, gossip split-view detection · *maps_to:* S3, S6 · **seminal**
9. **Sigstore Rekor: Software Supply Chain Transparency Log** — Sigstore / OpenSSF (2021) — open-source (sigstore/rekor) — https://github.com/sigstore/rekor
   - Production immutable, verifiable-data-structure-backed signature transparency log — a concrete systems reference for recording + auditing signed-artifact metadata (analogous to signed CRDT block provenance).
   - *concepts:* signature transparency log, verifiable data structure (Trillian/Merkle), inclusion+consistency verification · *maps_to:* S3, S6 · **ok**
10. **CONIKS: Bringing Key Transparency to End Users** — Melara, Blankstein, Bonneau, Felten, Freedman (2015) — USENIX Security 2015 — https://www.usenix.org/system/files/conference/usenixsecurity15/sec15-paper-melara.pdf
    - Privacy-preserving verifiable key directory using a Merkle transparency tree with efficient self-monitoring — the model for auditable, non-equivocating distribution of signing keys among CRDT peers.
    - *concepts:* verifiable key directory, transparency tree, non-equivocation, efficient self-auditing · *maps_to:* S3, gap5 · **seminal**
11. **SEEMless: Secure End-to-End Encrypted Messaging with less Trust** — Chase, Deshpande, Ghosh, Malvai (2019) — ACM CCS 2019 — https://dl.acm.org/doi/10.1145/3319535.3363202
    - Scalable append-only verifiable key directory (successor to CONIKS) with formal security + efficient append/lookup proofs — informs a scalable signed-key transparency layer for multi-peer messaging.
    - *concepts:* append-only verifiable directory, ZK history proofs, persistent authenticated dictionary, key transparency at scale · *maps_to:* S3, gap5 · **strong**
12. **Transparency Logs via Append-Only Authenticated Dictionaries** — Tomescu, Bhupatiraju, Papadopoulos, Papamanthou, Triandopoulos, Devadas (2019) — ACM CCS 2019 — https://cse.hkust.edu.hk/~dipapado/docs/aad.pdf
    - Builds append-only authenticated dictionaries with efficient append-only + lookup proofs — the data-structure backbone for a signed, tamper-evident CRDT operation log.
    - *concepts:* append-only authenticated dictionary, append-only proofs, log(n) lookup proofs, bilinear accumulators · *maps_to:* S3, S6 · **strong**
13. **Keybase Sigchain (signed append-only per-user chain with Merkle anchoring)** — Keybase (2019) — Keybase tech docs — https://keybase.io/docs/sigchain
    - Real-world per-user signed append-only chain: each link signed, carries seqno + prev-link hash, anchored into a public Merkle tree/blockchain — a working template for hash-linked per-block signing.
    - *concepts:* hash-linked signature chain, per-link signing + seqno, prev-hash tamper evidence, Merkle anchoring · *maps_to:* S3, gap5 · **ok**
14. **AIP: Agent Identity Protocol for Verifiable Delegation Across MCP and A2A** — (arXiv 2603.24775) (2026) — arXiv preprint — https://arxiv.org/pdf/2603.24775
    - Analyzes nested-JWT delegation bloat (quadratic in depth) vs append-only block chains (Biscuit-style) for verifiable delegation — informs how to keep nested attestation compact across forwarding hops. *(Recent.)*
    - *concepts:* verifiable delegation, nested-JWT bloat, append-only block delegation, signature forwarding · *maps_to:* S3, gap5 · **ok**
15. **SPHINCS+: Practical Stateless Hash-Based Signatures (SLH-DSA, FIPS 205)** — Bernstein, Hülsing, Kölbl, Niederhagen, Rijneveld, Schwabe (2015) — EUROCRYPT 2015 / NIST FIPS 205 (2024) — https://sphincs.org/
    - Stateless hash-based signatures relying only on hash collision-resistance — the conservative post-quantum option for per-block signing where statefulness (dangerous with concurrent CRDT signers) must be avoided.
    - *concepts:* stateless hash-based signatures, WOTS+/FORS/Merkle, PQ (FIPS 205), safe for concurrent signers · *maps_to:* S3, gap5 · **seminal**

---

## 10. Transport & routing — 15

Bears on **S5** (QUIC/HTTP-3 framing), **gap1** (unified carrier framing), **gap7** (routing policy), **gap4** (erasure/repair at transport).

1. **RFC 9000 — QUIC: A UDP-Based Multiplexed and Secure Transport** — Iyengar (Ed.), Thomson (Ed.) (2021) — IETF Standards Track — https://www.rfc-editor.org/rfc/rfc9000.pdf
   - The normative QUIC transport spec: flow-controlled streams, connection IDs, path migration, reliable in-order delivery over UDP — the base framing layer for the epic's multi-format transport (the real transport glpnet already implements).
   - *concepts:* streams, connection ID, path migration, reliable delivery, flow control, packet-number spaces · *maps_to:* S5, gap1 · **seminal**
2. **The QUIC Transport Protocol: Design and Internet-Scale Deployment** — Langley, Riddoch, Wilk, Vicente, Krasic, Zhang, Yang, Kouranov, Swett, Iyengar et al. (2017) — ACM SIGCOMM 2017 — https://dl.acm.org/doi/pdf/10.1145/3098822.3098842
   - Design rationale + Internet-scale deployment lessons (user-space over UDP, 0-RTT, versioned evolution) — empirical grounding for choosing QUIC as the low-latency transport.
   - *concepts:* user-space transport, 0-RTT, loss recovery, connection migration, ossification · *maps_to:* S5 · **seminal**
3. **RFC 9114 — HTTP/3** — Bishop (Ed.) (2022) — IETF Standards Track — https://www.rfc-editor.org/rfc/rfc9114.html
   - HTTP semantics over QUIC: frame types (HEADERS/DATA), unidirectional control/push streams, stream-per-request multiplexing — the framing model to mirror for multi-format message channels.
   - *concepts:* frame types, request streams, unidirectional streams, stream multiplexing · *maps_to:* S5, gap8 · **seminal**
4. **RFC 9204 — QPACK: Field Compression for HTTP/3** — Krasic, Bishop, Frindell (Ed.) (2022) — IETF Standards Track — https://datatracker.ietf.org/doc/rfc9204/
   - Header/field compression tolerating QUIC's out-of-order delivery via separate encoder/decoder streams + a blocking-avoidance dynamic table — the reference for compressing metadata without head-of-line blocking across independent streams.
   - *concepts:* field compression, dynamic table, encoder/decoder streams, HoL-blocking avoidance · *maps_to:* S5 · **strong**
5. **RFC 6455 — The WebSocket Protocol** — Fette, Melnikov (2011) — IETF Standards Track — https://www.rfc-editor.org/rfc/rfc6455.txt
   - Minimal message framing over TCP after an HTTP upgrade: text/binary opcodes, masking, fragmentation, ping/pong — the fallback bidirectional transport (glpnet runs WS over one QUIC bidi stream) and a compact framing model for message envelopes.
   - *concepts:* opening handshake, message framing, opcodes, masking, fragmentation, ping/pong · *maps_to:* S5, gap8 · **seminal**
6. **Design and Evaluation of a Wide-Area Event Notification Service (SIENA)** — Carzaniga, Rosenblum, Wolf (2001) — ACM TOCS 19(3) — https://courses.cs.vt.edu/~cs5204/fall05-kafura/Papers/Events/Siena.pdf
   - The seminal content-based publish/subscribe architecture: subscriptions as predicates, covering/merging of filters, wide-area routing of notifications toward interested parties — the model for content-based (destination-less) message routing and the closest analogue to the routing Policy DSL.
   - *concepts:* content-based routing, pub/sub, subscription covering, advertisements, predicate forwarding · *maps_to:* gap7 · **seminal**
7. **Fast Forwarding for Content-Based Networking** — Carzaniga, Deng, Wolf (2003) — Washington Univ. TR WUCS-03-31 — https://apps.dtic.mil/sti/tr/pdf/ADA444544.pdf
   - A forwarding-table matching algorithm evaluating predicate constraints at line rate to pick next hops — the practical engine that makes content-based message routing tractable at scale.
   - *concepts:* content-based forwarding, predicate matching, forwarding table, line-rate routing · *maps_to:* gap7 · **strong**
8. **Epidemic Algorithms for Replicated Database Maintenance** — Demers, Greene, Hauser, Irish, Larson, Shenker, Sturgis, Swinehart, Terry (1987) — ACM PODC 1987 (Xerox PARC CSL-89-1) — http://bitsavers.trailing-edge.com/pdf/xerox/parc/techReports/CSL-89-1_Epidemic_Algorithms_for_Replicated_Database_Maintenance.pdf
   - Founding paper on gossip/epidemic dissemination: direct mail, anti-entropy (push/pull/push-pull), rumor mongering with death certificates — the theoretical basis for CRDT anti-entropy sync + convergence.
   - *concepts:* epidemic/gossip, anti-entropy, rumor mongering, push/pull, death certificates · *maps_to:* S6, gap4 · **seminal**
9. **Astrolabe: A Robust and Scalable Technology for Distributed System Monitoring, Management, and Data Mining** — van Renesse, Birman, Vogels (2003) — ACM TOCS 21(2) — https://www.cs.cornell.edu/home/rvr/papers/astrolabe.pdf
   - Hierarchical, server-less gossip computing on-the-fly aggregates over evolving distributed state via mobile SQL — a model for scalable, decentralized membership/state summarization feeding routing decisions.
   - *concepts:* gossip aggregation, hierarchical zones, P2P, mobile SQL aggregation · *maps_to:* gap7 · **strong**
10. **Bimodal Multicast** — Birman, Hayden, Ozkasap, Xiao, Budiu, Minsky (1999) — ACM TOCS 17(2) — https://www.cs.rice.edu/~alc/old/comp520/papers/BHO99.pdf
    - Gossip-based reliable multicast with a rigorously quantified bimodal delivery guarantee + throughput stability — bridges best-effort dissemination and probabilistic reliability for message fan-out.
    - *concepts:* probabilistic reliable multicast, gossip repair, bimodal guarantee, anti-entropy retransmission · *maps_to:* gap4 · **seminal**
11. **LT Codes** — Luby (2002) — IEEE FOCS 2002 — https://www.inference.org.uk/mackay/dfountain/LT.pdf
    - First practical rateless fountain code: near-optimal erasure correction from an endless stream of encoded symbols with minimal feedback — the basis for reliable message transport over lossy/multipath links (bulk split-mix-erasure in the mstack B2 line).
    - *concepts:* fountain codes, rateless erasure coding, degree distribution, belief-propagation decoding · *maps_to:* gap4 · **seminal**
12. **A Random Linear Network Coding Approach to Multicast** — Ho, Médard, Koetter, Karger, Effros, Shi, Leong (2006) — IEEE Trans. Information Theory 52(10) — https://dl.acm.org/doi/10.1109/tit.2006.881746
    - Distributed random linear network coding: nodes mix packets with random coefficients to achieve multicast capacity w.h.p. — a decentralized reliability/multipath-throughput primitive for mesh dissemination.
    - *concepts:* random linear network coding, multicast capacity, distributed coding, erasure resilience · *maps_to:* gap4 · **seminal**
13. **QUIC-FEC: Bringing the Benefits of Forward Erasure Correction to QUIC** — Michel, De Coninck, Bonaventure (2019) — IFIP Networking 2019 / arXiv 1904.11326 — https://arxiv.org/pdf/1904.11326
    - Adds forward erasure correction to QUIC so lost packets recover without retransmission on high-latency/lossy paths — concrete integration of erasure coding into the epic's chosen transport.
    - *concepts:* forward erasure correction, QUIC extension, FEC frames, latency reduction · *maps_to:* S5, gap4 · **strong**
14. **Multipath Extension for QUIC** (draft-ietf-quic-multipath) — Liu, Ma, De Coninck, Bonaventure, Huitema, Kühlewind (Eds.) (2024) — IETF I-D (QUIC WG) — https://datatracker.ietf.org/doc/draft-ietf-quic-multipath/
    - Standardizes simultaneous use of multiple network paths for one QUIC connection via path identifiers + per-path packet-number spaces — the multipath transport substrate for resilient message routing (≤3 routes in the interview).
    - *concepts:* multipath QUIC, path identifiers, uniflows, per-path packet-number spaces · *maps_to:* S5, gap7 · **ok**
15. **Highly Dynamic Destination-Sequenced Distance-Vector Routing (DSDV) for Mobile Computers** — Perkins, Bhagwat (1994) — ACM SIGCOMM 1994 — https://dl.acm.org/doi/10.1145/190314.190336
    - Loop-free distance-vector routing using destination sequence numbers to defeat count-to-infinity — foundational technique (with split-horizon/poison-reverse, the vocabulary olamnit's distance-vector routing already uses) for reconverging mesh routes when peers join/leave.
    - *concepts:* distance-vector routing, destination sequence numbers, loop freedom, split horizon, poison reverse, mesh reconvergence · *maps_to:* gap7 · **seminal**

---

## 11. Coverage vs the 9 gaps

Legend: **well** = external literature richly covers the design space · **moderate** = component pieces exist, the exact artifact is partial · **thin** = only academic/analogue, the artifact is genuinely net-new.

| Gap | External coverage | Load-bearing papers | Verdict |
|-----|-------------------|---------------------|---------|
| **gap1** unified wire | Header/envelope + framing design abundant (ASN.1 abstract/transfer syntax, CDDL, Protobuf editions, QUIC/WS framing) but **no paper does the specific 3-envelope reconciliation** | ASN.1 X.680, CDDL, RFC 9000, Symmetric Lenses | **moderate** (components rich; the unification is net-new) |
| **gap2** ≥3-way interchange | The archetype (ASN.1: 1 abstract syntax → many encoding rules) + DFDL/Kaitai/CDDL multi-target + EverParse verified codegen | ASN.1 X.680, DFDL, Kaitai, CDDL, EverParse, Viotti survey | **well** (conceptually solved; the *specific* JSON+bin+YAML+md+Gleam-term matrix is integration work) |
| **gap3** message-level tombstone | Store-level CRDT tombstones well covered (OR-Set, soft tombstones); Peritext preserves unhandled marks — **message-format** tombstone specifically not addressed | Optimized OR-Set, Peritext | **moderate→thin** (store tombstones yes; in-the-message tombstone is net-new) |
| **gap4** repair segment | Transport/CRDT repair rich (erasure codes, range reconciliation, join-decomposition, delta-CRDTs) — but a **semantic per-element message repair segment** is undefined | LT Codes, RLNC, Range-Based Set Reconciliation, Join Decompositions, QUIC-FEC | **moderate** (repair primitives yes; message-level semantic repair is net-new) |
| **gap5** multi-signature format | Very rich: EdDSA/COSE/JWS detached, BLS aggregate, Biscuit per-block chain, MTL amortized, CT/transparency, nested-attestation bloat analysis | JWS (detached+multi-sig), COSE, BLS multi-sig, Biscuit, MTL, AIP | **well** |
| **gap6** CRDT-of-message vs store | Entire CRDT corpus (foundations+systems+messages-stores) + verified specs (OpSets, Gomes) + Byzantine family + the internal qmedit message-CRDT | Shapiro CvRDT/CmRDT, JSON CRDT, OpSets, Pure op-based, Blocklace | **well** |
| **gap7** routing Policy DSL | Content-based pub/sub (SIENA/fast-forwarding) is the closest analogue; distance-vector (DSDV) is the nearest routing *behaviour*; but an **executable policy language for targets/waypoints/excludes** is not a paper | SIENA, Fast Forwarding, DSDV, Astrolabe | **thin** (analogues only; the policy DSL is net-new) |
| **gap8** payload-type discriminator | Textbook TLV: NDN-TLV (formal + spec), Protobuf wire-type, CBOR tags, self-describing formats | NDN-TLV type-theory, Protobuf encoding, RFC 8949 CBOR, RFC 6709 | **well** |
| **gap9** amulet as live token | The static-sparse-capability lineage is fully documented (Amoeba is the literal ancestor) + hardware/OS ocap grounding | Amoeba sparse capabilities, CHERI, KeyKOS, EROS | **well** (lineage solid; the concrete Amoeba-shape amulet impl is the net-new build *[E5-ruled 2026-07-04: 4-field shape kept, Check ≥128-bit — literal 16 B rejected; see F3 §6]*) |

**Thin gaps needing the most net-new design work:** gap7 (routing Policy DSL), gap3 (message-level tombstone), gap4 (semantic repair segment), and the specific reconciliation in gap1 — exactly the items the F1 scan flagged as "defined nowhere / academic only." The external literature is strongest precisely where internal work is thinnest is NOT the case here: the external corpus is deep on gap5/gap6/gap8/gap9 (which internal work already partly builds) and correctly shallow on the four genuinely novel items — confirming the F1 gap list.

---

## 12. Hand-off to F3 (synthesis) — paper clusters → building-block decisions

1. **Encoding-interchange skeleton (S1, gap2).** Anchor on the **ASN.1 abstract/transfer-syntax split** (§1.8) as the organizing principle — one abstract message model, N encoding rules — cross-checked against **DFDL / Kaitai / CDDL** (multi-target codegen) and **EverParse** (verified one-schema→many-codec). The **encodings-spectrum** cluster (§2) supplies the concrete surfaces (CBOR/MessagePack/BSON binary · JCS/canonical-JSON readable · S-expression head-tagged · MUMPS sparse-tree). This directly extends qmedit's 3-surface design toward the JSON+bin+YAML+md+Gleam-term matrix.
2. **Unified wire + framing + discriminator (gap1, gap8, S5).** Combine the **transport-routing** cluster (QUIC RFC 9000 / HTTP-3 / QPACK / WS) with the **TLV discriminator** papers (NDN-TLV formal model, Protobuf wire-type, CBOR tags). These resolve glpnet 038 FR-006 (the missing payload-type prefix byte) and give buildkit-D2 a principled header/framing model. **Symmetric Lenses** (§3.3) is the formal tool for reconciling peer envelopes rather than projecting one onto another.
3. **Version tolerance, evolution & tombstone (S4, gap3, gap4).** The **schema-evolution / lenses** cluster (Cambria + edit/symmetric lenses + Avro resolution + Protobuf editions/field-presence) is the machinery for cross-version translation; the **skip-unknown** cluster (RFC 6709/9170/9413, SOAP mustUnderstand, Orchard) sets the must-ignore/must-understand two-tier rules. For the **net-new** message-level tombstone + repair segment, borrow from **Optimized OR-Set** (soft tombstones), **Range-Based Set Reconciliation** + **Join Decompositions** (targeted repair), and **PRISM/COPE** (information-preserving migration). Read **RFC 9413** as the guardrail against over-tolerant design.
4. **CRDT substrate + message-vs-store decision (S6, gap6).** The three CRDT clusters (§5–7) are the decision surface. For **message-level** CRDT: JSON CRDT, Pure op-based (TRCB/PO-Log matches a broadcast substrate), OpSets (verifiable composition), Peritext/Fugue (ordered/rich content). For **store-level** convergence: delta-CRDTs, Merkle-CRDT/Merkle-Search-Tree, DVV/DVVS. The **Byzantine family** (Blocklace, Making-CRDTs-BFT, extend-only posets) matters if the mesh is adversarial — and Blocklace directly echoes glpnet's in-repo blocklace note.
5. **Capability header + amulet (S2, gap9).** The **capability-tokens** cluster: build the macaroon layer on **Macaroons/Biscuit/UCAN** (already internally built), and the **amulet** squarely on **Amoeba sparse capabilities** (the literal 16 B ancestor) with **CHERI/KeyKOS/EROS** as the least-authority grounding and **Confused Deputy / Robust Composition / OCPL** as the "why + verification" backbone. This is the cluster that turns gap9 from "defined but unbuilt" into a spec.
6. **Multi-signature format (S3, gap5).** The **signatures-attestation** cluster: **JWS detached + multi-sig** and **COSE** as the envelope; **Biscuit's per-block Ed25519 chain** and **MTL mode** for cheap per-block/sub-content signing; **BLS multi-sig** for aggregate co-signing; **CT/CONIKS/SEEMless/AAD** for a tamper-evident signed op-log; **AIP** as the caution on nested-attestation bloat. This is the first-class whole+sub-content scheme qmedit only prototypes.
7. **Routing Policy DSL (gap7, thinnest).** No paper is a policy language, so synthesize from analogues: **SIENA + Fast-Forwarding** (subscriptions-as-predicates = the closest executable content-routing model), **DSDV/distance-vector** (reconvergence behaviour olamnit already uses), **Astrolabe** (aggregation), and **Multipath QUIC** (≤3-route substrate). The must-have-targets/waypoints/exclude-lists policy from the glpnet brief is genuine net-new design that these anchor.

---

*End of curated web-research corpus. 148 papers · 10 themes · extends beacon-42 / mstack-18 / qmedit-50. Advisory input to F3 synthesis.*

---

## Change log — 042 verification pass (2026-07-04)

> All amendments below were made by feature 042-crdtmsg-verify-harden; the finding ids
> resolve in docs/research/crdt-multiformat-messaging/verification-report-042.md.

| # | Section touched | Change | Why (finding id) | Baseline |
|---|---|---|---|---|
| 1 | (new terminal section) | Added this change-log section skeleton (contract rule 4) | SETUP-042-F2 | HEAD(6ff3a8c9) |
| 2 | §11 gap3 row | Dropped "Merkle-CRDTs" from the load-bearing list (entry §7.4 maps_to S6/gap4 — does not bear on gap3) | LR-042-F2-1 | DELIVERY(c20317ce) |
| 3 | §11 gap9 row | Dropped "Macaroons" from the load-bearing list (entry §8.1 maps_to S2 only; §8's own header excludes it from the amulet lineage) | LR-042-F2-2 | DELIVERY(c20317ce) |
| 4 | §2 section header | Split the "S4/gap3 (self-describing skip)" gloss — gap3 is the message-level tombstone, not skip; header now glosses gap3 as canonical/deterministic encoding groundwork | LR-042-F2-3 | DELIVERY(c20317ce) |
| 5 | §11 gap9 row | E5 supersession note: literal 16 B rejected, Check ≥128-bit, 4-field shape kept | RP-042-14 | HEAD(6ff3a8c9) |
| 6 | §2.14 S-Expressions entry | Dead MIT URL → IETF datatracker URL, link-rot noted inline | EP-F2-030 (report §8) | HEAD(6ff3a8c9) |
| 7 | §7.3 Merkle Search Trees entry | Mistranscribed arXiv id 1904.13396 (unrelated paper) → HAL hal-02303490, correction noted inline | EP-F2-095 (report §8) | HEAD(6ff3a8c9) |
| 8 | header block | Verification reference line added (SC-009) | SC-009 (report §12) | HEAD(6ff3a8c9) |

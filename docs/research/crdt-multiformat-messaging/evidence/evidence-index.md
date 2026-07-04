# Evidence Index — 042 verification pass (FR-010, SC-007)

Every evidence pointer enumerated in the three corpus documents
(`priorart-sibling-scan.md`, `webresearch-corpus.md`, `buildingblocks-synthesis.md`),
one row each, per data-model.md EvidencePointer. Populated by T025 (census) and
resolved/dispositioned by T026–T027. Census totals live in
[verification-report-042.md](../verification-report-042.md) §8.

Classes: `in-repo` · `sibling-repo` · `external-url` · `session-transcript` · `named-corpus`
Resolutions: `resolves` · `materialized(<evidence/ path>)` · `host-blocked` · `link-rot` · `unrecoverable`
(disposition_note required for host-blocked / link-rot / unrecoverable).

Census conventions (T025): one row per distinct pointer per doc, located at first
appearance (original line numbers, pre-042-changelog); repeats folded as "also L<n>".
F1 body document-references and their §15 ranked-path rows are merged (all 33 §15 rows
present, cross-tagged "§15#n"). `[IO:<corpus>]` = F2 `[INTERNAL-OVERLAP]` tag.

| pointer_id | doc | location | pointer_text (verbatim, condensed) | class | resolution | disposition_note |
|---|---|---|---|---|---|---|
| EP-F1-001 | F1 | L15 | qhstate spec-036 "Extensible Multi-Format CRDT Message Architecture" — `D:/bstdev/research/qhstate/specs/036-extensible-multi-format-schema-prototype-implementation/spec.md` (§15#1); also L126 (FR-018), L142 (FR-023), L187 (FR-013..016), L237, L272 | sibling-repo | | |
| EP-F1-002 | F1 | L16 | buildkit spec-047 "yngenious daemon" Decision D2 — `D:/bstdev/research/buildkit/specs/047-build-yngenious-daemon-service-mvp/contracts/README.md` (§15#2); also L98, L118, L131, L160, L190, L219, L236, L273 | sibling-repo | | |
| EP-F1-003 | F1 | L20 | durable-mesh-messaging brief — `D:/bstdev/research/glp/glpnet/docs/roadmap-intake/durable-mesh-messaging-protocol.md` (§15#3); path at L60, `:33` routing-policy at L61; also L110, L225, L274 | in-repo | | |
| EP-F1-004 | F1 | L20 | verbatim Marcelle+Gabi principal interview — `D:/bstdev/tools/MSTACK/dianna/application/box-inputs/P3-DRAFTING/inputs/PRINCIPAL-INTERVIEW-2026-06-18.md` (§15#4); path at L62; also L101 (amulet def), L128, L275 | sibling-repo | | |
| EP-F1-005 | F1 | L20 | yngenios SOURCE-MATERIAL brief — `D:/bstdev/research/buildkit/docs/yngenios/SOURCE-MATERIAL.md` (§15#5); path at L63; also L276 | sibling-repo | | |
| EP-F1-006 | F1 | L43 | glpnet spec 025 multi-protocol-link-layer (ground-relay discipline; CRDT/OT scoped out in "025 tutorials") | in-repo | | |
| EP-F1-007 | F1 | L45 | glpnet 029 IL-codec-spike, Lean `decode∘encode = id` proof — `D:/bstdev/research/glp/glpnet/specs/029-il-codec-spike/spec.md` (§15#26); also L80, L249, L297 | in-repo | | |
| EP-F1-008 | F1 | L46 | glpnet 038 result-codec + FrameCodec — `D:/bstdev/research/glp/glpnet/specs/038-result-codec-and-framecodec-ride/spec.md` (§15#8); also L80, L165, L226, L238, L279 | in-repo | | |
| EP-F1-009 | F1 | L46 | `FrameCodec.cs:64 OffKind` (FR-006 open handoff: payload-type prefix byte needed) | in-repo | | |
| EP-F1-010 | F1 | L47 | `glp_gleam/src/glp/codec/{term_codec,result_envelope}.gleam` (Gleam port 031/032) | in-repo | | |
| EP-F1-011 | F1 | L48 | glpnet spec 036 HTTP/3-QUIC-WS, L5 envelope — `D:/bstdev/research/glp/glpnet/specs/036-http3-quic-ws-link/contracts/wire-contract.md` (§15#7); also L117, L159, L238, L278 | in-repo | | |
| EP-F1-012 | F1 | L49 | glpnet spec 040 rcopy mesh + tmsg codec — `D:/bstdev/research/glp/glpnet/specs/040-rcopy-file-transfer-service/contracts/terminal-protocol.md` (§15#6); also L144, L189, L277 | in-repo | | |
| EP-F1-013 | F1 | L50 | gleam_quic `glpq_ffi.erl` (>1 MiB frame reassembly, misroute fixed 2026-07-03); also L159 | in-repo | | |
| EP-F1-014 | F1 | L64 | buildkit-beacon `docs/proposals/beacon-pilot.md` (beacon epic proposal, WSJF 1.78/RICE 300) | sibling-repo | | |
| EP-F1-015 | F1 | L65 | `mstack-traffic.md` — `D:/bstdev/research/buildkit-beacon/specs/037-bk-beacon-pilot/research/mstack-traffic.md` (§15#25); also L102, L112 (B2), L296 | sibling-repo | | |
| EP-F1-016 | F1 | L66 | `docs/research/three-role-agent-teams/METHOD-AND-DOGFOOD.md` (the scan method itself) | in-repo | | |
| EP-F1-017 | F1 | L78 | qhstate `qmedit/surfaces/` (JSON + binary(CBOR-TLV) + YAML surfaces; `QMD1` magic + `payload_len`) | sibling-repo | | |
| EP-F1-018 | F1 | L79 | olamnit `EnvelopeCodec.cs` — `D:/bstdev/research/olamnit/Olamnit/Olamnit.Kernel/Envelope/EnvelopeCodec.cs` (§15#12); also L283 | sibling-repo | | |
| EP-F1-019 | F1 | L81 | buildkit-beacon `envelope.py` (canonical field order, v2 additive; emit-low/accept-range); also L143 | sibling-repo | | |
| EP-F1-020 | F1 | L81 | buildkit-beacon `test_envelope_v2.py` (byte-stable round trip asserted) | sibling-repo | | |
| EP-F1-021 | F1 | L82 | mstack `wire.py` — `D:/bstdev/tools/MSTACK/mesh/src/durable_mesh/wire.py` (§15#29); also L128, L300 | sibling-repo | | |
| EP-F1-022 | F1 | L93 | mstack `security/macaroon.py` (HMAC-chained caveats, verify-before-act) | sibling-repo | | |
| EP-F1-023 | F1 | L93 | mstack `contracts/macaroon.md` — `D:/bstdev/tools/MSTACK/specs/007-durable-mesh-messaging/contracts/macaroon.md` (§15#14); also L285 | sibling-repo | | |
| EP-F1-024 | F1 | L94 | buildkit-beacon `macaroon.py` — `D:/bstdev/research/buildkit-beacon/src/buildkit_cli/beacon/macaroon.py` (§15#15); also L102, L286 | sibling-repo | | |
| EP-F1-025 | F1 | L95 | qhstate `qmedit/seal/macaroon.py` + `seal/` — `D:/bstdev/research/qhstate/qmedit/src/qmedit/seal/macaroon.py` (§15#16); `seal/` also L126; also L287 | sibling-repo | | |
| EP-F1-026 | F1 | L96 | olamnit `EnvelopeHeader.cs` (48-byte fixed header) — `D:/bstdev/research/olamnit/Olamnit/Olamnit.Kernel/Envelope/EnvelopeHeader.cs` (§15#13); also L145, L284 | sibling-repo | | |
| EP-F1-027 | F1 | L97 | crucible `beacon_feed.py` + dossier — `D:/bstdev/research/crucible/src/crucible_present/beacon_feed.py` (§15#23); also L294 | sibling-repo | | |
| EP-F1-028 | F1 | L97 | `beacon/host/Macaroon/Macaroon.cs:145` (cited by the crucible dossier) | sibling-repo | | |
| EP-F1-029 | F1 | L103 | glpnet finding `P7-qhsm-yngenios/DOSSIER.md:107` (PAT-03; "only macaroon hit inside glpnet") — resolves to `docs/research/glp-gleam-baseline/pipelines/P7-qhsm-yngenios/DOSSIER.md` | in-repo | | |
| EP-F1-030 | F1 | L106 | mstack `04-capability-security-authz.md` 18-entry corpus (= mstack-18) — `D:/bstdev/tools/MSTACK/dianna/application/system-descriptions/04-capability-security-authz.md` (§15#20); also L241, L291 | sibling-repo | | |
| EP-F1-031 | F1 | L107 | olamnit `02-corpus.md:858` (Google Macaroons cite; "olamnit macaroon corpus" at L241) | sibling-repo | | |
| EP-F1-032 | F1 | L107 | olamnit rtos-kernel — `D:/bstdev/research/olamnit/specs/013-olamnit-rtos-kernel/rtos-kernel.md` (`:480/:484/:527`) (§15#21); also L176, L191, L292 | sibling-repo | | |
| EP-F1-033 | F1 | L111 | olamnit spec-016 distance-vector-routing — `D:/bstdev/research/olamnit/specs/016-distance-vector-routing/spec.md` (§15#27); also L225, L298 | sibling-repo | | |
| EP-F1-034 | F1 | L127 | glpnet archive `main_GLP_to_Dart.tex:619` (nested attestation; blocklace) — `docs/archive/main_GLP_to_Dart (3).tex:619` (§15#28); also L177, L249, L299 | in-repo | | |
| EP-F1-035 | F1 | L129 | buildkit-beacon `sign.py` (Ed25519 whole-artifact signing of CRDT exports) | sibling-repo | | |
| EP-F1-036 | F1 | L131 | buildkit CHANGELOG — "signed CRDT export" (no explicit path given) | sibling-repo | | |
| EP-F1-037 | F1 | L142 | qhstate `binary_surface.py` — `D:/bstdev/research/qhstate/qmedit/src/qmedit/surfaces/binary_surface.py` (§15#9, CBOR-TLV skip-len); also L280 | sibling-repo | | |
| EP-F1-038 | F1 | L146 | buildkit spec-046 (data "parking") — `D:/bstdev/research/buildkit/specs/046-roadmap-export-evolution/spec.md` (§15#22); also L222, L293 | sibling-repo | | |
| EP-F1-039 | F1 | L149 | glpnet spec-035 "semantic-tombstone-enrichment" — keyword-collision trap, cited as NOT prior art (codeconv inventory tombstone); also L244 | in-repo | | |
| EP-F1-040 | F1 | L161 | mstack `duplex-link-port.md` (DuplexLink seam) — `D:/bstdev/tools/MSTACK/specs/007-durable-mesh-messaging/contracts/duplex-link-port.md` (§15#19); also L290 | sibling-repo | | |
| EP-F1-041 | F1 | L173 | buildkit `deploy/evolutions/roadmap_crdt/evolution.py` — `D:/bstdev/research/buildkit/src/buildkit_cli/deploy/evolutions/roadmap_crdt/evolution.py` (§15#10); also L281 | sibling-repo | | |
| EP-F1-042 | F1 | L174 | buildkit-beacon `roadmap/crdt/{replay,journal,project,writeback,clock,identity,slot}.py` — `replay.py` = `D:/bstdev/research/buildkit-beacon/src/buildkit_cli/roadmap/crdt/replay.py` (§15#11); also L282 | sibling-repo | | |
| EP-F1-043 | F1 | L174 | beacon 42-paper CRDT corpus `corpus-index.md` (= beacon-42) — `D:/bstdev/research/buildkit-beacon/specs/043-export-roadmap/research/corpus/corpus-index.md` (§15#17); also L241, L288 | sibling-repo | | |
| EP-F1-044 | F1 | L175 | qhstate `qmedit/crdt/{base,state_based,op_based}.py` (three selectable CRDT models) | sibling-repo | | |
| EP-F1-045 | F1 | L175 | qhstate `specs/036/corpora/crdts/` (curated CRDT corpus; the 7 corpus groups per L187) | sibling-repo | | |
| EP-F1-046 | F1 | L176 | olamnit `PgliteContentStore.cs` (local-first WAL+PGlite authoritative) | sibling-repo | | |
| EP-F1-047 | F1 | L177 | glpnet `multi-protocol-link-layer/corpus/17` — `docs/research/multi-protocol-link-layer/corpus/17-efficient-logic-variables-distributed-computing.md` (§15#31, CRDT/CALM survey); also L302 | in-repo | | |
| EP-F1-048 | F1 | L187 | qhstate `synthesis/architecture-outline.md` (50 patterns / 7 groups; = qmedit-50 per L241) — `D:/bstdev/research/qhstate/.../synthesis/architecture-outline.md` (§15#24, path elided in source); also L295 | sibling-repo | | |
| EP-F1-049 | F1 | L188 | buildkit-beacon `message_types.py` — `D:/bstdev/research/buildkit-beacon/src/buildkit_cli/beacon/message_types.py` (§15#18); also L289 | sibling-repo | | |
| EP-F1-050 | F1 | L188 | buildkit-beacon `message-and-mailbox.md` (per-type payload-shape registry doc) | sibling-repo | | |
| EP-F1-051 | F1 | L188 | buildkit-beacon `MessageTypeRegistry.cs` (C# mirror of the registry) | sibling-repo | | |
| EP-F1-052 | F1 | L192 | mstack spec-007 FR-005 durable-mesh-messaging (versioned envelope + entity model) | sibling-repo | | |
| EP-F1-053 | F1 | L301 | olamnit `CHANGELOG.md:81` (K2 durable mesh relay) — `D:/bstdev/research/olamnit/CHANGELOG.md:81` (§15#30; K2 claim in prose at L176) | sibling-repo | | |
| EP-F1-054 | F1 | L303 | research-loose `corpus_pubsub-routing-coordination_analysis.md` — `D:/bstdev/research/syst-lit-rev-agentic-protocols/wip/stage-07.../corpus_pubsub-routing-coordination_analysis.md` (§15#32, path elided in source) | sibling-repo | | |
| EP-F1-055 | F1 | L304 | `_marathon-synthesis/RESTART-STATE.md` — `D:/bstdev/research/_marathon-synthesis/RESTART-STATE.md` (§15#33; principal naming, wrong epic) | sibling-repo | | |
| EP-F2-001 | F2 | L6 | **beacon-42** (42-paper CRDT index) — internal corpus, named not pathed in F2 (pathed in F1 = EP-F1-043); `[IO]` tags throughout; also L347, L591 | named-corpus | | |
| EP-F2-002 | F2 | L6 | **mstack-18** (18-entry capability-security corpus) — named not pathed in F2 (pathed in F1 = EP-F1-030); also L591 | named-corpus | | |
| EP-F2-003 | F2 | L6 | **qmedit-50** (50-pattern synthesis / 7 corpus groups) — named not pathed in F2 (pathed in F1 = EP-F1-048); also L591 | named-corpus | | |
| EP-F2-004 | F2 | L7 | "§2 of the F1 scan" / "§13" — cross-reference to `priorart-sibling-scan.md` (this directory); also L575 | in-repo | | |
| EP-F2-005 | F2 | L47 | entry §1.1: Thrift: Scalable Cross-Language Services (2007) — https://thrift.apache.org/static/files/thrift-20070401.pdf | external-url | | |
| EP-F2-006 | F2 | L50 | entry §1.2: Foundations of JSON Schema (WWW 2016) — https://dl.acm.org/doi/10.1145/2872427.2883029 | external-url | | |
| EP-F2-007 | F2 | L53 | entry §1.3: Validation of Modern JSON Schema (POPL 2024) — https://dl.acm.org/doi/full/10.1145/3632891 | external-url | | |
| EP-F2-008 | F2 | L56 | entry §1.4: EverParse (USENIX Security 2019) — https://www.usenix.org/conference/usenixsecurity19/presentation/delignat-lavaud | external-url | | |
| EP-F2-009 | F2 | L59 | entry §1.5: RFC 8610 CDDL — https://datatracker.ietf.org/doc/html/rfc8610 | external-url | | |
| EP-F2-010 | F2 | L62 | entry §1.6: RFC 9682 CDDL grammar updates — https://www.rfc-editor.org/rfc/rfc9682.html | external-url | | |
| EP-F2-011 | F2 | L65 | entry §1.7: RFC 1014 XDR — https://www.rfc-editor.org/rfc/rfc1014 | external-url | | |
| EP-F2-012 | F2 | L68 | entry §1.8: ITU-T X.680–X.693 ASN.1 — https://www.itu.int/rec/T-REC-X.680-X.693-201508-S/en | external-url | | |
| EP-F2-013 | F2 | L71 | entry §1.9: DFDL v1.0 (GFD.240) — https://ogf.org/documents/GFD.207.pdf | external-url | | |
| EP-F2-014 | F2 | L74 | entry §1.10: Kaitai Struct — https://kaitai.io/ | external-url | | |
| EP-F2-015 | F2 | L77 | entry §1.11: Cap'n Proto / FlatBuffers / SBE comparison (Varda 2014) — https://capnproto.org/news/2014-06-17-capnproto-flatbuffers-sbe.html | external-url | | |
| EP-F2-016 | F2 | L80 | entry §1.12: Expressiveness and Complexity of XML Schema (TODS 2006) — https://www.theoinf.uni-bayreuth.de/pool/documents/Paper2006-10/Paper2006/Expressiveness_and_Complexity_of_XML_Schema_paper.pdf | external-url | | |
| EP-F2-017 | F2 | L90 | entry §2.1: Survey of JSON-compatible Binary Serialization (arXiv 2201.02089) — https://arxiv.org/abs/2201.02089 | external-url | | |
| EP-F2-018 | F2 | L93 | entry §2.2: Benchmark of JSON-compatible Binary Serialization (arXiv 2201.03051) — https://arxiv.org/abs/2201.03051 | external-url | | |
| EP-F2-019 | F2 | L96 | entry §2.3: RFC 8949 CBOR — https://www.rfc-editor.org/rfc/rfc8949.html [IO:qmedit-50] | external-url | | |
| EP-F2-020 | F2 | L99 | entry §2.4: CBOR Deterministic Encoding (draft-bormann-cbor-det-04) — https://www.ietf.org/archive/id/draft-bormann-cbor-det-04.html | external-url | | |
| EP-F2-021 | F2 | L102 | entry §2.5: dCBOR (draft-mcnally) — https://blockchaincommons.github.io/WIPs-IETF-draft-deterministic-cbor/draft-mcnally-deterministic-cbor.html | external-url | | |
| EP-F2-022 | F2 | L105 | entry §2.6: RFC 8785 JSON Canonicalization Scheme — https://www.rfc-editor.org/rfc/rfc8785.html | external-url | | |
| EP-F2-023 | F2 | L108 | entry §2.7: ITU-T X.690 BER/CER/DER — https://www.itu.int/rec/T-REC-X.690-202102-I/en | external-url | | |
| EP-F2-024 | F2 | L111 | entry §2.8: ITU-T X.691 PER — https://www.itu.int/rec/T-REC-X.691/en | external-url | | |
| EP-F2-025 | F2 | L114 | entry §2.9: MessagePack Specification — https://github.com/msgpack/msgpack/blob/master/spec.md [IO:qmedit-50] | external-url | | |
| EP-F2-026 | F2 | L117 | entry §2.10: BSON Specification — https://bsonspec.org/spec.html [IO:qmedit-50] | external-url | | |
| EP-F2-027 | F2 | L120 | entry §2.11: Protocol Buffers Encoding guide — https://protobuf.dev/programming-guides/encoding/ [IO:qmedit-50] | external-url | | |
| EP-F2-028 | F2 | L123 | entry §2.12: LEB128 variable-length integers — https://en.wikipedia.org/wiki/LEB128 | external-url | | |
| EP-F2-029 | F2 | L126 | entry §2.13: SFVInt fast varint decoding (arXiv 2403.06898) — https://arxiv.org/abs/2403.06898 | external-url | | |
| EP-F2-030 | F2 | L129 | entry §2.14: SPKI/SDSI S-Expressions (Rivest 1997) — https://people.csail.mit.edu/rivest/Sexp.txt [IO:qmedit-50] | external-url | | |
| EP-F2-031 | F2 | L132 | entry §2.15: Type-Theoretic Model on NDN-TLV (ICN 2022) — https://dl.acm.org/doi/10.1145/3517212.3558093 | external-url | | |
| EP-F2-032 | F2 | L135 | entry §2.16: NDN Packet Format v0.3 TLV — https://docs.named-data.net/NDN-packet-spec/current/tlv.html | external-url | | |
| EP-F2-033 | F2 | L138 | entry §2.17: W3C EXI 1.0 (2nd Ed.) — https://www.w3.org/TR/exi/ | external-url | | |
| EP-F2-034 | F2 | L141 | entry §2.18: ITU-T X.891 Fast Infoset — https://www.itu.int/en/ITU-T/asn1/Pages/Fast-Infoset.aspx | external-url | | |
| EP-F2-035 | F2 | L144 | entry §2.19: MUMPS globals (ISO/IEC 11756 / bitsavers TR) — http://www.bitsavers.org/pdf/mumps/MUMPS_Globals_and_their_Implementation_May1975.pdf [IO:qmedit-50] | external-url | | |
| EP-F2-036 | F2 | L154 | entry §3.1: Cambria edit lenses (PaPoC 2021) — https://dl.acm.org/doi/abs/10.1145/3447865.3457963 [IO:beacon-42] | external-url | | |
| EP-F2-037 | F2 | L157 | entry §3.2: Edit Lenses (POPL 2012) — https://dl.acm.org/doi/10.1145/2103621.2103715 | external-url | | |
| EP-F2-038 | F2 | L160 | entry §3.3: Symmetric Lenses (POPL 2011) — https://www.cis.upenn.edu/~bcpierce/papers/symmetric.pdf | external-url | | |
| EP-F2-039 | F2 | L163 | entry §3.4: Symmetric Edit Lenses (Wagner PhD 2014) — https://repository.upenn.edu/edissertations/1488/ | external-url | | |
| EP-F2-040 | F2 | L166 | entry §3.5: Combinators for Bidirectional Tree Transformations (TOPLAS 2007) — https://dl.acm.org/doi/10.1145/1232420.1232424 | external-url | | |
| EP-F2-041 | F2 | L169 | entry §3.6: Boomerang (POPL 2008) — https://www.cis.upenn.edu/~bcpierce/papers/boomerang.pdf | external-url | | |
| EP-F2-042 | F2 | L172 | entry §3.7: Introduction to Bidirectional Transformations — https://www.cs.ox.ac.uk/people/jeremy.gibbons/publications/ssbx-intro.pdf | external-url | | |
| EP-F2-043 | F2 | L175 | entry §3.8: Apache Avro Specification — https://avro.apache.org/docs/current/specification/ | external-url | | |
| EP-F2-044 | F2 | L178 | entry §3.9: Kleppmann, Schema Evolution in Avro/Protobuf/Thrift — https://martin.kleppmann.com/2012/12/05/schema-evolution-in-avro-protocol-buffers-thrift.html | external-url | | |
| EP-F2-045 | F2 | L181 | entry §3.10: Protobuf Field Presence — https://protobuf.dev/programming-guides/field_presence/ | external-url | | |
| EP-F2-046 | F2 | L184 | entry §3.11: Confluent Schema Registry compatibility types — https://docs.confluent.io/platform/current/schema-registry/fundamentals/schema-evolution.html | external-url | | |
| EP-F2-047 | F2 | L187 | entry §3.12: ParallelChange / Expand-and-Contract (Fowler bliki) — https://martinfowler.com/bliki/ParallelChange.html [IO:beacon-42] | external-url | | |
| EP-F2-048 | F2 | L190 | entry §3.13: PRISM Workbench (VLDB 2008) — https://www.vldb.org/pvldb/vol1/1453939.pdf | external-url | | |
| EP-F2-049 | F2 | L193 | entry §3.14: Synthesizing Database Programs for Schema Refactoring (PLDI 2019) — https://arxiv.org/pdf/1904.05498 | external-url | | |
| EP-F2-050 | F2 | L196 | entry §3.15: Heterogeneous Coupled Evolution of Software Languages (MODELS 2008) — https://eelcovisser.org/publications/2008/VermolenV08.pdf | external-url | | |
| EP-F2-051 | F2 | L199 | entry §3.16: COPE (ECOOP 2009) — https://link.springer.com/chapter/10.1007/978-3-642-03013-0_4 | external-url | | |
| EP-F2-052 | F2 | L202 | entry §3.17: Schema Evolution in Interactive Programming Systems (2025) — https://arxiv.org/abs/2412.06269 | external-url | | |
| EP-F2-053 | F2 | L205 | entry §3.18: Automerge Cookbook, Modeling Data / schema migration — https://automerge.org/docs/cookbook/modeling-data/ | external-url | | |
| EP-F2-054 | F2 | L208 | entry §3.19: Baseline, Operation-Based Evolution and Versioning (arXiv 2512.09762) — https://www.arxiv.org/pdf/2512.09762 | external-url | | |
| EP-F2-055 | F2 | L218 | entry §4.1: RFC 6709 Design Considerations for Protocol Extensions — https://www.rfc-editor.org/rfc/rfc6709 | external-url | | |
| EP-F2-056 | F2 | L221 | entry §4.2: RFC 9170 Long-Term Viability of Extension Mechanisms (greasing) — https://datatracker.ietf.org/doc/html/rfc9170 | external-url | | |
| EP-F2-057 | F2 | L224 | entry §4.3: RFC 9413 Harmful Consequences of the Robustness Principle — https://www.ietf.org/archive/id/draft-iab-protocol-maintenance-05.html | external-url | | |
| EP-F2-058 | F2 | L227 | entry §4.4: SOAP 1.2 Part 1 (mustUnderstand) — https://www.w3.org/TR/soap12-part1/ | external-url | | |
| EP-F2-059 | F2 | L230 | entry §4.5: W3C TAG, Extending and Versioning XML Languages — https://www.w3.org/2001/tag/doc/versioning-xml-20070326.html | external-url | | |
| EP-F2-060 | F2 | L233 | entry §4.6: Orchard, Designing Extensible Versionable XML Formats — https://www.xml.com/pub/a/2004/07/21/design.html | external-url | | |
| EP-F2-061 | F2 | L236 | entry §4.7: Protobuf proto3 Unknown Fields — https://protobuf.dev/programming-guides/proto3/ | external-url | | |
| EP-F2-062 | F2 | L239 | entry §4.8: protobuf issue #2289, unknown-field loss on transcode — https://github.com/protocolbuffers/protobuf/issues/2289 | external-url | | |
| EP-F2-063 | F2 | L242 | entry §4.9: .NET Version-Tolerant Serialization — https://learn.microsoft.com/en-us/dotnet/framework/serialization/version-tolerant-serialization | external-url | | |
| EP-F2-064 | F2 | L245 | entry §4.10: Protobuf Editions — https://protobuf.dev/programming-guides/editions/ | external-url | | |
| EP-F2-065 | F2 | L255 | entry §5.1: Conflict-free Replicated Data Types (SSS 2011) — https://inria.hal.science/inria-00609399v1 [IO:beacon-42] | external-url | | |
| EP-F2-066 | F2 | L258 | entry §5.2: Comprehensive Study of CvRDT/CmRDT (INRIA RR-7506) — https://inria.hal.science/inria-00555588/en/ [IO:beacon-42] | external-url | | |
| EP-F2-067 | F2 | L261 | entry §5.3: Delta State Replicated Data Types — https://arxiv.org/abs/1603.01529 [IO:beacon-42] | external-url | | |
| EP-F2-068 | F2 | L264 | entry §5.4: Efficient State-based CRDTs by Delta-Mutation (NETYS 2015) — https://arxiv.org/abs/1410.2803 [IO:beacon-42] | external-url | | |
| EP-F2-069 | F2 | L267 | entry §5.5: Pure Operation-Based Replicated Data Types — https://arxiv.org/abs/1710.04469 | external-url | | |
| EP-F2-070 | F2 | L270 | entry §5.6: Making Operation-Based CRDTs Operation-Based (DAIS 2014) — https://ifip.hal.science/hal-01287738v1 | external-url | | |
| EP-F2-071 | F2 | L273 | entry §5.7: An Optimized Conflict-free Replicated Set (OR-Set) — https://arxiv.org/abs/1210.3368 [IO:beacon-42] | external-url | | |
| EP-F2-072 | F2 | L276 | entry §5.8: Verifying Strong Eventual Consistency (OOPSLA 2017) — https://arxiv.org/abs/1707.01747 | external-url | | |
| EP-F2-073 | F2 | L279 | entry §5.9: Replicated Abstract Data Types (RGA, JPDC 2011) — https://www.sciencedirect.com/science/article/pii/S0743731510002716 [IO:beacon-42] | external-url | | |
| EP-F2-074 | F2 | L282 | entry §5.10: Treedoc (ICDCS 2009) — https://inria.hal.science/inria-00445975 | external-url | | |
| EP-F2-075 | F2 | L285 | entry §5.11: Logoot (ICDCS 2009) — https://inria.hal.science/inria-00432368 [IO:beacon-42] | external-url | | |
| EP-F2-076 | F2 | L288 | entry §5.12: LSEQ (DocEng 2013) — https://dl.acm.org/doi/10.1145/2494266.2494278 | external-url | | |
| EP-F2-077 | F2 | L291 | entry §5.13: Yjs / YATA (GROUP 2016) — https://dl.acm.org/doi/10.1145/2957276.2957310 [IO:beacon-42] | external-url | | |
| EP-F2-078 | F2 | L294 | entry §5.14: A Conflict-Free Replicated JSON Datatype (TPDS 2017) — https://arxiv.org/abs/1608.03960 [IO:beacon-42] | external-url | | |
| EP-F2-079 | F2 | L297 | entry §5.15: Peritext rich-text CRDT (CSCW 2022) — https://www.inkandswitch.com/peritext/static/cscw-publication.pdf [IO:beacon-42] | external-url | | |
| EP-F2-080 | F2 | L300 | entry §5.16: The Art of the Fugue (arXiv 2305.00583) — https://arxiv.org/abs/2305.00583 [IO:beacon-42] | external-url | | |
| EP-F2-081 | F2 | L303 | entry §5.17: Dotted Version Vectors (arXiv 1011.5808) — https://arxiv.org/abs/1011.5808 [IO:beacon-42] | external-url | | |
| EP-F2-082 | F2 | L306 | entry §5.18: Scalable and Accurate Causality Tracking (DVVS, DAIS 2014) — https://link.springer.com/content/pdf/10.1007/978-3-662-43352-2_6.pdf [IO:beacon-42] | external-url | | |
| EP-F2-083 | F2 | L309 | entry §5.19: Local-First Software (Onward! 2019) — https://www.inkandswitch.com/essay/local-first/ | external-url | | |
| EP-F2-084 | F2 | L312 | entry §5.20: OpSets (ECOOP 2018) — https://arxiv.org/abs/1805.04263 | external-url | | |
| EP-F2-085 | F2 | L322 | entry §6.1: Extending JSON CRDTs with Move Operations (PaPoC '24) — https://arxiv.org/abs/2311.14007 | external-url | | |
| EP-F2-086 | F2 | L325 | entry §6.2: Highly-Available Move Operation for Replicated Trees (TPDS 2022) — https://martin.kleppmann.com/papers/move-op.pdf | external-url | | |
| EP-F2-087 | F2 | L328 | entry §6.3: Automerge 2.0 / Binary Document Format — https://automerge.org/automerge-binary-format-spec/ [IO:beacon-42] | external-url | | |
| EP-F2-088 | F2 | L331 | entry §6.4: Eg-walker (EuroSys 2025) — https://arxiv.org/abs/2409.14252 [IO:beacon-42] | external-url | | |
| EP-F2-089 | F2 | L334 | entry §6.5: Collabs CRDT framework — https://arxiv.org/abs/2212.02618 | external-url | | |
| EP-F2-090 | F2 | L337 | entry §6.6: cr-sqlite — https://github.com/vlcn-io/cr-sqlite [IO:beacon-42] | external-url | | |
| EP-F2-091 | F2 | L340 | entry §6.7: ElectricSQL active-active — https://legacy.electric-sql.com/docs/intro/active-active [IO:beacon-42] | external-url | | |
| EP-F2-092 | F2 | L343 | entry §6.8: Bayou (SOSP 1995) — https://dl.acm.org/doi/10.1145/224056.224070 | external-url | | |
| EP-F2-093 | F2 | L355 | entry §7.1: DSON delta-mutation JSON CRDT (PVLDB 15) — https://www.vldb.org/pvldb/vol15/p1053-rinberg.pdf | external-url | | |
| EP-F2-094 | F2 | L358 | entry §7.2: Melda CRDT move operations (arXiv 2503.04811) — https://arxiv.org/pdf/2503.04811 | external-url | | |
| EP-F2-095 | F2 | L361 | entry §7.3: Merkle Search Trees (SRDS 2019) — https://arxiv.org/abs/1904.13396 | external-url | | |
| EP-F2-096 | F2 | L364 | entry §7.4: Merkle-CRDTs (arXiv 2004.00107) — https://arxiv.org/pdf/2004.00107 | external-url | | |
| EP-F2-097 | F2 | L367 | entry §7.5: IPFS (arXiv 1407.3561) — https://arxiv.org/pdf/1407.3561 | external-url | | |
| EP-F2-098 | F2 | L370 | entry §7.6: The Blocklace (arXiv 2402.08068) — https://arxiv.org/html/2402.08068v3 | external-url | | |
| EP-F2-099 | F2 | L373 | entry §7.7: Cordial Miners (DISC 2023) — https://drops.dagstuhl.de/entities/document/10.4230/LIPIcs.DISC.2023.26 | external-url | | |
| EP-F2-100 | F2 | L376 | entry §7.8: Making CRDTs Byzantine Fault Tolerant (PaPoC 2022) — https://martin.kleppmann.com/papers/bft-crdt-papoc22.pdf | external-url | | |
| EP-F2-101 | F2 | L379 | entry §7.9: On CRDTs and Equivocation in Byzantine Setups (arXiv 2109.10554) — https://arxiv.org/pdf/2109.10554 | external-url | | |
| EP-F2-102 | F2 | L382 | entry §7.10: Extend-Only Directed Posets (arXiv 2304.04318) — https://arxiv.org/pdf/2304.04318 | external-url | | |
| EP-F2-103 | F2 | L385 | entry §7.11: Efficient Synchronization of State-based CRDTs / Join Decompositions (ICDE 2019) — https://arxiv.org/abs/1803.02750 | external-url | | |
| EP-F2-104 | F2 | L388 | entry §7.12: Range-Based Set Reconciliation (SRDS 2023) — https://arxiv.org/abs/2212.13567 | external-url | | |
| EP-F2-105 | F2 | L391 | entry §7.13: Decoupling Trust in Byzantine CRDTs (arXiv 2606.31759) — https://arxiv.org/html/2606.31759 | external-url | | |
| EP-F2-106 | F2 | L394 | entry §7.14: Composable CRDT Layer for Byzantine-Resilient Reconstruction (arXiv 2606.18966) — https://arxiv.org/html/2606.18966 | external-url | | |
| EP-F2-107 | F2 | L404 | entry §8.1: Macaroons (NDSS 2014) — https://research.google/pubs/macaroons-cookies-with-contextual-caveats-for-decentralized-authorization-in-the-cloud/ [IO:mstack-18] | external-url | | |
| EP-F2-108 | F2 | L407 | entry §8.2: Biscuit specifications — https://doc.biscuitsec.org/reference/specifications.html [IO:mstack-18] | external-url | | |
| EP-F2-109 | F2 | L410 | entry §8.3: UCAN Specification — https://github.com/ucan-wg/spec [IO:mstack-18] | external-url | | |
| EP-F2-110 | F2 | L413 | entry §8.4: Robust Composition (Miller PhD 2006) — http://erights.org/talks/thesis/markm-thesis.pdf | external-url | | |
| EP-F2-111 | F2 | L416 | entry §8.5: The Confused Deputy (Hardy 1988) — https://dl.acm.org/doi/10.1145/54289.871709 | external-url | | |
| EP-F2-112 | F2 | L419 | entry §8.6: RFC 2693 SPKI Certificate Theory — https://www.rfc-editor.org/rfc/rfc2693 | external-url | | |
| EP-F2-113 | F2 | L422 | entry §8.7: SDSI (Rivest, Lampson 1996) — https://people.csail.mit.edu/rivest/pubs/RL96.ver-1.1.html | external-url | | |
| EP-F2-114 | F2 | L425 | entry §8.8: WAVE (USENIX Security 2019) — https://www.usenix.org/conference/usenixsecurity19/presentation/andersen [IO:mstack-18] | external-url | | |
| EP-F2-115 | F2 | L428 | entry §8.9: ZCAP-LD (W3C-CCG) — https://w3c-ccg.github.io/zcap-spec/ | external-url | | |
| EP-F2-116 | F2 | L431 | entry §8.10: Vouchsafe (arXiv 2601.02254) — https://arxiv.org/abs/2601.02254 | external-url | | |
| EP-F2-117 | F2 | L434 | entry §8.11: Tahoe, The Least-Authority Filesystem (StorageSS 2008) — https://tahoe-lafs.org/~trac/lafs.pdf | external-url | | |
| EP-F2-118 | F2 | L437 | entry §8.12: EROS (SOSP 1999) — https://flint.cs.yale.edu/cs428/doc/eros.pdf | external-url | | |
| EP-F2-119 | F2 | L440 | entry §8.13: KeyKOS Architecture — http://cap-lore.com/CapTheory/upenn/ | external-url | | |
| EP-F2-120 | F2 | L443 | entry §8.14: Amoeba, Using Sparse Capabilities (ICDCS 1986) — https://research.vu.nl/en/publications/using-sparse-capabilities-in-a-distributed-operating-system | external-url | | |
| EP-F2-121 | F2 | L446 | entry §8.15: The CHERI Capability Model (ISCA 2014) — https://www.cl.cam.ac.uk/research/security/ctsrd/pdfs/201406-isca2014-cheri.pdf [IO:mstack-18] | external-url | | |
| EP-F2-122 | F2 | L449 | entry §8.16: OCPL, Verification of Object Capability Patterns (OOPSLA 2017) — https://people.mpi-sws.org/~dreyer/papers/ocpl/paper.pdf | external-url | | |
| EP-F2-123 | F2 | L459 | entry §9.1: RFC 8032 EdDSA — https://www.rfc-editor.org/rfc/rfc8032.html | external-url | | |
| EP-F2-124 | F2 | L462 | entry §9.2: RFC 9052 COSE — https://www.rfc-editor.org/rfc/rfc9052.html | external-url | | |
| EP-F2-125 | F2 | L465 | entry §9.3: RFC 7515 JWS — https://www.rfc-editor.org/rfc/rfc7515.html | external-url | | |
| EP-F2-126 | F2 | L468 | entry §9.4: Merkle Tree Ladder Mode Signatures (draft-harvey-cfrg-mtl-mode) — https://datatracker.ietf.org/doc/draft-harvey-cfrg-mtl-mode/ | external-url | | |
| EP-F2-127 | F2 | L471 | entry §9.5: Compact Multi-Signatures for Smaller Blockchains (BLS, ASIACRYPT 2018) — https://eprint.iacr.org/2018/483.pdf | external-url | | |
| EP-F2-128 | F2 | L474 | entry §9.6: Subset-optimized BLS Multi-signature (ePrint 2023/498) — https://eprint.iacr.org/2023/498.pdf | external-url | | |
| EP-F2-129 | F2 | L477 | entry §9.7: RFC 9162 Certificate Transparency v2 — https://www.rfc-editor.org/rfc/rfc9162.html | external-url | | |
| EP-F2-130 | F2 | L480 | entry §9.8: RFC 6962 Certificate Transparency — https://www.rfc-editor.org/rfc/rfc6962.html | external-url | | |
| EP-F2-131 | F2 | L483 | entry §9.9: Sigstore Rekor — https://github.com/sigstore/rekor | external-url | | |
| EP-F2-132 | F2 | L486 | entry §9.10: CONIKS (USENIX Security 2015) — https://www.usenix.org/system/files/conference/usenixsecurity15/sec15-paper-melara.pdf | external-url | | |
| EP-F2-133 | F2 | L489 | entry §9.11: SEEMless (CCS 2019) — https://dl.acm.org/doi/10.1145/3319535.3363202 | external-url | | |
| EP-F2-134 | F2 | L492 | entry §9.12: Transparency Logs via Append-Only Authenticated Dictionaries (CCS 2019) — https://cse.hkust.edu.hk/~dipapado/docs/aad.pdf | external-url | | |
| EP-F2-135 | F2 | L495 | entry §9.13: Keybase Sigchain — https://keybase.io/docs/sigchain | external-url | | |
| EP-F2-136 | F2 | L498 | entry §9.14: AIP, Agent Identity Protocol (arXiv 2603.24775) — https://arxiv.org/pdf/2603.24775 | external-url | | |
| EP-F2-137 | F2 | L501 | entry §9.15: SPHINCS+ / SLH-DSA (FIPS 205) — https://sphincs.org/ | external-url | | |
| EP-F2-138 | F2 | L511 | entry §10.1: RFC 9000 QUIC — https://www.rfc-editor.org/rfc/rfc9000.pdf | external-url | | |
| EP-F2-139 | F2 | L514 | entry §10.2: The QUIC Transport Protocol (SIGCOMM 2017) — https://dl.acm.org/doi/pdf/10.1145/3098822.3098842 | external-url | | |
| EP-F2-140 | F2 | L517 | entry §10.3: RFC 9114 HTTP/3 — https://www.rfc-editor.org/rfc/rfc9114.html | external-url | | |
| EP-F2-141 | F2 | L520 | entry §10.4: RFC 9204 QPACK — https://datatracker.ietf.org/doc/rfc9204/ | external-url | | |
| EP-F2-142 | F2 | L523 | entry §10.5: RFC 6455 WebSocket — https://www.rfc-editor.org/rfc/rfc6455.txt | external-url | | |
| EP-F2-143 | F2 | L526 | entry §10.6: SIENA wide-area event notification (TOCS 2001) — https://courses.cs.vt.edu/~cs5204/fall05-kafura/Papers/Events/Siena.pdf | external-url | | |
| EP-F2-144 | F2 | L529 | entry §10.7: Fast Forwarding for Content-Based Networking (WUCS-03-31) — https://apps.dtic.mil/sti/tr/pdf/ADA444544.pdf | external-url | | |
| EP-F2-145 | F2 | L532 | entry §10.8: Epidemic Algorithms for Replicated Database Maintenance (PODC 1987) — http://bitsavers.trailing-edge.com/pdf/xerox/parc/techReports/CSL-89-1_Epidemic_Algorithms_for_Replicated_Database_Maintenance.pdf | external-url | | |
| EP-F2-146 | F2 | L535 | entry §10.9: Astrolabe (TOCS 2003) — https://www.cs.cornell.edu/home/rvr/papers/astrolabe.pdf | external-url | | |
| EP-F2-147 | F2 | L538 | entry §10.10: Bimodal Multicast (TOCS 1999) — https://www.cs.rice.edu/~alc/old/comp520/papers/BHO99.pdf | external-url | | |
| EP-F2-148 | F2 | L541 | entry §10.11: LT Codes (FOCS 2002) — https://www.inference.org.uk/mackay/dfountain/LT.pdf | external-url | | |
| EP-F2-149 | F2 | L544 | entry §10.12: Random Linear Network Coding for Multicast (IEEE TIT 2006) — https://dl.acm.org/doi/10.1109/tit.2006.881746 | external-url | | |
| EP-F2-150 | F2 | L547 | entry §10.13: QUIC-FEC (IFIP Networking 2019) — https://arxiv.org/pdf/1904.11326 | external-url | | |
| EP-F2-151 | F2 | L550 | entry §10.14: Multipath Extension for QUIC (draft-ietf-quic-multipath) — https://datatracker.ietf.org/doc/draft-ietf-quic-multipath/ | external-url | | |
| EP-F2-152 | F2 | L553 | entry §10.15: DSDV (SIGCOMM 1994) — https://dl.acm.org/doi/10.1145/190314.190336 | external-url | | |
| EP-F3-001 | F3 | L4 | Provenance: F1 `priorart-sibling-scan.md` (both in this directory); also L18, L232 | in-repo | | |
| EP-F3-002 | F3 | L4 | Provenance: F2 `webresearch-corpus.md` (both in this directory); also L18 | in-repo | | |
| EP-F3-003 | F3 | L4 | "live glpnet repo at head (branch `037-virtual-3270-term`, post-040-implement)" — scanner C's pinned live-repo evidence set | in-repo | | |
| EP-F3-004 | F3 | L5 | three-role method — `docs/research/three-role-agent-teams/METHOD-AND-DOGFOOD.md` | in-repo | | |
| EP-F3-005 | F3 | L9 | "the roadmap brief" (verbatim OC-1..OC-4 constraint quote; no path given in F3); also L102 ("the brief's exact set") | in-repo | | |
| EP-F3-006 | F3 | L29 | glpnet spec 038 ("038/029 Section-15 codec"; `capturedLen` L86; golden-corpus discipline L88, L92) | in-repo | | |
| EP-F3-007 | F3 | L29 | glpnet spec 029 (Section-15 codec; "029 Lean proof is the in-repo precedent" L92) | in-repo | | |
| EP-F3-008 | F3 | L34 | qhstate qmedit / spec-036 ("F1 qmedit CBOR-TLV"; 3-surface design L84; per-block seals + SC-005 L114; fail-closed rule L108; plaintext DSL L147, L209) | sibling-repo | | |
| EP-F3-009 | F3 | L39 | glpnet spec 025 ("025 FrameCodec" L39, L97; "025 GRL discipline" L136) | in-repo | | |
| EP-F3-010 | F3 | L42 | glpnet spec 036 wire contract ("036-contract change" E8; shipped 036 QUIC/WS L98; 036 L5 contract L136; terminal tokens L142; sanctioned bump L208) | in-repo | | |
| EP-F3-011 | F3 | L45 | glpnet spec 040 ("owner-ruled 040"; identity law L105; WAL replay L122; resume analogue L123; tmsg discipline L147; FR-037/SC-009 L138; "040 plan" owner-flag L236) | in-repo | | |
| EP-F3-012 | F3 | L81 | "full field detail in the three claim sets, appendix §16" (86 schema-conformant claims A:30/B:29/C:27, per L5/L230) | session-transcript | | |
| EP-F3-013 | F3 | L85 | `csharp/glp_result_codec/TermCodec.cs` (+ Gleam/Dart parity harness) | in-repo | | |
| EP-F3-014 | F3 | L89 | `ResultEnvelopeCodec.Decode` invariant (code symbol, no path given) | in-repo | | |
| EP-F3-015 | F3 | L95 | buildkit spec-047 D2 ("buildkit-047-D2 mandate"; "047 reliability sublayer" L97; "047 FR-017" L110) | sibling-repo | | |
| EP-F3-016 | F3 | L95 | `Mesh.TryRoute` — head-state fact: running mesh routes JSON L5, never touches binary codecs (code symbol, no path given) | in-repo | | |
| EP-F3-017 | F3 | L109 | "per the principal interview" (Amoeba amulet 4-field shape; negotiated suites mandate L116) — via F1 EP-F1-004 | sibling-repo | | |
| EP-F3-018 | F3 | L117 | "the archived `forward(p,P)` nesting design" (also "the in-repo blocklace note" L135) — glpnet archive main_GLP_to_Dart, via F1 EP-F1-034 | in-repo | | |
| EP-F3-019 | F3 | L125 | `CycleGuard` / `CyclicTermException` (shipped code symbols, no path given) | in-repo | | |
| EP-F3-020 | F3 | L128 | "buildkit/beacon roadmap_crdt HLC/DVV/OR-Set/journal-fold — property-tested" (demoted to concept reference in E1, L201) — via F1 EP-F1-041/042 | sibling-repo | | |
| EP-F3-021 | F3 | L132 | `LinkSequencer` / `InboundOrdering` / `SendWindow` / `FencingRegistry` (shipped reliability-sublayer classes, no paths given) | in-repo | | |
| EP-F3-022 | F3 | L141 | "language authority §1.14" — `docs/DISCIPLINE.md` §1.14; also L206, L210 | in-repo | | |
| EP-F3-023 | F3 | L144 | "olamnit 016 distance-vector (split-horizon, poison-reverse, reconvergence)" — via F1 EP-F1-033 | sibling-repo | | |
| EP-F3-024 | F3 | L237 | "Full claim sets: preserved in the F3 run records (session transcripts); the three scanners' complete 86-claim output is the evidence appendix of record" | session-transcript | | |

## Census totals (T025)

Rows per doc:

| doc | rows |
|---|---|
| F1 `priorart-sibling-scan.md` | 55 (all 33 §15 ranked rows present, plus 22 body-only pointers) |
| F2 `webresearch-corpus.md` | 152 (148 numbered entries + 3 named corpora + 1 F1 cross-reference) |
| F3 `buildingblocks-synthesis.md` | 24 |
| **Total** | **231** |

Rows per class:

| class | rows | breakdown |
|---|---|---|
| in-repo | 32 | F1: 14 · F2: 1 · F3: 17 |
| sibling-repo | 46 | F1: 41 · F3: 5 |
| external-url | 148 | F2: 148 (all §1–§10 entries) |
| session-transcript | 2 | F3: 2 (L81 claim sets / appendix §16; L237 run records) |
| named-corpus | 3 | F2: 3 (beacon-42, mstack-18, qmedit-50; in F1 these carry paths and are censused as sibling-repo path rows EP-F1-043/030/048) |
| **Total** | **231** | |

Census notes (T025): (1) F1 body document-references and their §15 ranked-path rows were
merged into one row per artifact at first appearance — every §15 row is represented and
cross-tagged. (2) F1 §14's "Recommended inputs to F2/F3" concept lists (Shapiro 2011,
Automerge, Biscuit, SIENA, ...) are search directives, not evidence pointers, and are not
censused; each such concept that IS evidence carries a URL row in F2. (3) Paper names
mentioned inline in F3 with locators in F2 (RFC 9413, CBOR RFC 8949, Fugue/Peritext,
SIENA, protobuf #2289, ...) are not re-censused in F3. (4) `P7-qhsm-yngenios/DOSSIER.md`
and `glpq_ffi.erl` were verified to live inside glpnet (hence in-repo). (5) F3 L209's
roadmap feature `crdtmsg-xsd-style-schema-language` is a decision output of ruling E9,
not an evidence pointer, and is excluded. Resolution/disposition columns are left for
T026–T027.

# Blind re-scan record — BB-ENC-7 (CBOR generic-payload surface)

**042 pass (FR-004/FR-014, research.md R4 protocol)** · date 2026-07-04 · baseline HEAD(6ff3a8c9)
**Original sourcing family**: B (F2, claim B-05). **Re-scanned families**: A (F1 doc only), C (glpnet repo, excluding the corpus dir).
**Blindness**: each scanner (a Claude agent) received only its family manifest + the topic
"CBOR or similar self-describing generic binary encodings as a payload surface for messages" —
never F3's verdict or claim text.

## Family A scan (F1 only)

1. qmedit CBOR-TLV is the binary surface of a lossless tri-surface architecture and "the strongest single prior-art artifact" (F1 L15). HIGH.
2. CBOR-TLV skip-unknown via length prefix: "`QMD1` magic + `payload_len(uint32 BE)` lets a reader skip an unknown-head block without decoding" (F1 L78, L280). HIGH.
3. F1 explicitly recommends "CBOR/MessagePack TLV skip-tolerance" as an F2 input (F1 L255). HIGH.
4. glpnet 038 tag scheme adjacent; payload-type discriminator still needed (F1 L46). MED.
5. qmedit corpus has a dedicated json-binary-encodings group (F1 L187). MED.
6. Absence: no unit spans the full multi-encoding matrix; qmedit closest at 3 surfaces (F1 L84). HIGH.

## Family C scan (repo, HEAD)

1. A CBOR codec EXISTS in shipped C# code as one of four message-payload surfaces: `csharp/glp_crdtmsg/model/CborCodec.cs` (deterministic write, System.Formats.Cbor), registered in `SurfaceCodec.cs` `Surfaces = { Binary, Json, Yaml, Cbor }`. HIGH.
2. Spec-mandated MVP surface with pairwise lossless conformance matrix (041 spec.md FR-002 L129, SC-001 L201). HIGH.
3. CBOR is non-canonical: binary-term is the canonical/signing form (`SurfaceCodec.cs` L3–6). HIGH.
4. Loud-fail decode + unknown-key forward tolerance; matrix green ("48 cells green", test/parity/README.md L19–20). HIGH.
5. CBOR also the planned COSE_Sign1 framing, deferred post-MVP thin wrapper (`sig/Seals.cs` L8–9; docs/known-issues.md L348–350). HIGH.
6. Contrast: 038 rejected CBOR for wire TERMS (byte-determinism), consistent with layered composition. HIGH.

## Curator verdict (T018)

**CONFIRMED (strengthened to 3-family).** Family A independently corroborates the design family
(qmedit CBOR-TLV + explicit recommendation), and family C now holds a SHIPPED CBOR surface
(041). The block's E3 promotion (PROV → ACCEPTED/MVP-CORE) is corroborated in fact by shipped
code; the layered-composition boundary (binary-term canonical, CBOR generic-payload) is exactly
what both scans found. No conflict; no escalation.

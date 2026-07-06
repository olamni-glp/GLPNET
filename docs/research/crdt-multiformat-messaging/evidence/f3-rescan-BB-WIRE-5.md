# Blind re-scan record — BB-WIRE-5 (endianness layering law: BE frame ⊃ LE payload)

**042 pass (FR-004/FR-014, R4 protocol)** · 2026-07-04 · baseline HEAD(6ff3a8c9)
**Original family**: C (repo, claim C-7; counter-query note "+F1 §3"). **Re-scanned**: A (F1 only), B (F2 only). Blind: topic only — "endianness conventions and layering between frame headers and payloads".

## Family A scan
1. glpnet FrameCodec uses a 22-byte BE header (F1 L46). HIGH.
2. qmedit payload_len is uint32 BE for skip-by-length (F1 L78). HIGH.
3. Weak-implicit: endianness stated only at header/framing layer, payload treated opaque (L46+L126). LOW (absence-based).
No explicit LE-payload or layering/mixing statement.

## Family B scan
1. Canonical byte order as a foundational wire-format concept (XDR, F2 L66–67). HIGH (order unspecified).
2. LEB128 "Little Endian Base 128" varints underlie Protobuf/WASM/DWARF/Automerge payload encodings (F2 L123–124). MED (name-level).
3. Automerge binary format uses LEB128 in a production CRDT payload encoding (F2 L329). MED.
No explicit BE-vs-LE layering discussion anywhere.

## Curator verdict (T018)
**CONFIRMED (weak external corroboration; primary authority intact).** Both families supply
partial, non-contradicting support: A corroborates the BE-frame-header half from two independent
units; B corroborates that payload-side varint families are little-endian-lineage. Neither
family contradicts the layering law, whose primary authority is repo head (shipped 038 goldens +
parity suites — breaking either direction fails a shipped suite). ACC/CORE standing stands.
No conflict; no escalation.

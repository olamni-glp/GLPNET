# Blind re-scan record — BB-ENC-8 (markdown surface)

**042 pass (FR-004/FR-014, R4 protocol)** · 2026-07-04 · baseline HEAD(6ff3a8c9)
**Original family**: A (F1, claim A-7). **Re-scanned**: B (F2 only), C (repo, excl. corpus dir). Blind: topic only — "markdown as a message/document encoding surface (lossless or render-only)".

## Family B scan
1. md named only as a design-matrix target in the corpus's own gap2 verdict/hand-off (F2 L566, L581) — no paper on markdown as an encoding. HIGH (as negative).
2. No lossless-vs-render-only round-trip literature for any lightweight markup; nearest-adjacent = Peritext formatting spans (F2 L298–299). HIGH (negative/adjacency).

## Family C scan
1. Markdown-as-surface exists only as the unshipped, owner-gated BB-ENC-8 option (042 research.md R5 echo). HIGH.
2. NEGATIVE: zero markdown/commonmark hits in shipped code (csharp/, glp_runtime/, glp_quick/, gleam_quic/, glp_multiagent/). HIGH.
3. 041's rich-text payload is Fugue+Peritext over terms, not markdown; "richer document models" deferred post-MVP (041 spec L218). HIGH.
4. Only markdown round-trip practice in-repo is codeconv inventory tombstones (off-topic). HIGH mechanism / LOW relevance.

## Curator verdict (T018)
**NO-FURTHER-EVIDENCE.** Neither family holds independent evidence for a markdown surface; both
confirm the block's own absence framing ("exists nowhere"). PROV/POST standing and the
owner-choice trigger (lossless vs render-only) remain correct and unmet at HEAD. No conflict.

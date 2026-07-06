# Blind re-scan record — BB-CRDT-7 (sequence/rich-text CRDT: Fugue + Peritext)

**042 pass (FR-004/FR-014, research.md R4 protocol)** · date 2026-07-04 · baseline HEAD(6ff3a8c9)
**Original sourcing family**: B (F2, claim B-20; counter-query note "+F1 §14 rec"). **Re-scanned families**: A (F1 doc only), C (repo, excluding the corpus dir).
**Blindness**: topic only — "CRDTs for ordered sequences and rich-text formatting (interleaving avoidance, formatting spans)".

## Family A scan (F1 only)

1. beacon's 42-paper corpus names RGA/Logoot/Fugue/Yjs (F1 L174). HIGH (names only, no design detail).
2. Yjs, RGA/Fugue, Automerge JSON CRDT recommended as F2 inputs (F1 L253). HIGH.
3. Corpus location ranked #17 (F1 L288). MED.
4. Explicit non-coverage: zero evidence on interleaving avoidance, rich-text spans, stable-identity anchoring, Peritext — those words do not appear in F1.

## Family C scan (repo, HEAD)

1. Fugue tree sequence CRDT SHIPPED: `csharp/glp_crdtmsg/crdt/richtext/Fugue.cs` (left/right origins over stable ids; tree ordering → maximal non-interleaving). HIGH.
2. Non-interleaving asserted by executable tests incl. randomized delivery (SC-012, `FugueTests.cs`). HIGH.
3. Peritext-style marks anchored to stable (dot, side) positions SHIPPED: `Peritext.cs`. HIGH.
4. Unknown mark types preserved verbatim through convergence + 4-surface transcode (SC-013, `PeritextTests.cs`). HIGH.
5. Marks = observed-remove set keyed by mark_id; deletes are first-class tombstone ops with causal context (`Tombstone.cs`). HIGH.
6. Unified op-based rich-text doc rebuildable from op log, spec-contracted (C11/C12, FR-036/FR-037; `RichTextDoc.cs`). HIGH.

## Curator verdict (T018)

**CONFIRMED (strengthened to 3-family) + register trigger MET.** Family A corroborates the
design family at bibliography level; family C now holds the SHIPPED Fugue+Peritext
implementation (041 made it MANDATORY MVP-CORE). The F3 §5 register trigger "first ordered/
rich-content document type ships" is met by concrete shipped evidence — promotion is executed in
the US3 register adjudication (report §7) with this record as evidence. No conflict; no
escalation.

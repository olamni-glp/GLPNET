# Blind re-scan record — BB-VER-5 (cross-version translation: Avro fast path → lens seam)

**042 pass (FR-004/FR-014, research.md R4 protocol)** · date 2026-07-04 · baseline HEAD(6ff3a8c9)
**Original sourcing family**: B (F2, claim B-13). **Re-scanned families**: A (F1 doc only), C (repo, excluding the corpus dir).
**Blindness**: topic only — "cross-schema-version translation of messages (reader/writer resolution, lenses, migrations)".

## Family A scan (F1 only)

1. [TRANSLATION, reference-only] beacon's 42-paper corpus includes "Cambria edit lenses + expand/contract schema evolution" (F1 L174). HIGH.
2. [TRANSLATION, recommended-not-built] Cambria lenses explicitly recommended as F2 input (F1 L253). HIGH.
3. [Adjacent, store-level] buildkit spec-046 forward/reverse migration with data "parking" (F1 L146). HIGH.
4. [Weak hint] qmedit FR-024 "self-upgrade-path recognition" (F1 L142). LOW-MED.
5. [TOLERANCE, not translation] beacon emit-low/accept-range (F1 L143); skip/degrade family (L142/L144/L145). HIGH.
6. Net: NO unit credited with BUILT translation machinery — only cited papers + a store-level migration analogue.

## Family C scan (repo, HEAD)

1. Two-tier version policy shipped: envelope emit-low/accept-range [1,2] + codec hard-reject (`csharp/glp_crdtmsg/envelope/VersionPolicy.cs`). HIGH.
2. Field-ADD forward compat shipped via additive-optional TLV skip-by-length with verbatim carry (`header/CapabilitySlot.cs`, `VersionSkipTests.cs`). HIGH.
3. Must-ignore/must-understand criticality model shipped (041 FR-004/006/007; `LoudFailTests.cs`). HIGH.
4. Schema-version id embedded per message; dual-form registry stores forms only, no version resolution (`glp_wire_registry/SchemaRegistry.cs`). HIGH.
5. NEGATIVE: no cross-version TRANSLATION machinery anywhere — no lens, no upgrade/downgrade functions, no field-remove defaults, no restructuring migration (keyword-bounded sweep of csharp/, glp_runtime/, glp_quick/, specs/). MED-HIGH.
6. Only other "schema migrations" are DB/Alembic infra, off-topic. MED.

## Curator verdict (T018)

**CONFIRMED (as PROVISIONAL) — no-promotion.** Family A adds corpus-level corroboration that the
lens seam is the right reserved direction (Cambria in the beacon-42 corpus + explicit
recommendation); family C confirms that nothing shipped goes beyond version HANDLING
(accept-range + additive skip) — i.e. the promotion trigger ("first restructuring migration
need; Avro path insufficient") is NOT met at HEAD. The block's PROV standing and trigger wording
remain valid. No conflict; no escalation.

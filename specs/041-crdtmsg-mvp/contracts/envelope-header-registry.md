# Contract — Envelope, Header, Wire Registry

Traces: FR-001..FR-011, FR-032/034; SC-001/002/008/010.

## C1. Abstract model ⇄ 4 surfaces (FR-001/002/003)
- **Invariant**: for every message M and surfaces s1,s2 ∈ {binary-term, JSON, YAML, CBOR}: `decode(s2, encode(s2, decode(s1, bytes))) ≡ decode(s1, bytes)` semantically, and same-surface re-encode is byte-identical to the golden.
- **Invariant**: unknown/opaque sections are preserved verbatim across every conversion.
- **Test**: 16-cell pairwise conformance matrix over the golden corpus (extends `glp_result_codec` golden discipline). Binary nesting = TLV-outer / Section-15-term-codec-inner.

## C2. TLV sections + criticality (FR-004/006)
- **Invariant**: `type_number` range decides criticality. Unknown+ignorable → skip-by-length, carried verbatim. Unknown+must-understand → loud-fail.
- **Invariant**: greasing sections are emitted and exercised each run.

## C3. Loud-fail decode (FR-005)
- **Invariant**: every decoder consumes all bytes or throws — bad version, unknown payload_type, unknown must-understand tag, truncation, trailing bytes all reject. No partial/silent acceptance.
- **Test**: extend `LoudFailFuzzTests`; SC-002 = 0% silent acceptance.

## C4. Unified header (FR-009/010/011)
- **Invariant**: header = `{msg_id, from, to, seq, policy, capability_slot}`; router forwards header+payload bytes **verbatim** (payload-opacity) — a relay never re-encodes.
- **Invariant**: `capability_slot` is envelope-**v2 additive-optional**; a v1 reader skips it and processes all known fields (SC-008).

## C5. Version discipline (FR-007)
- **Invariant**: envelope = emit-low/accept-range (additive-optional superset); frame + term codec = **hard-reject** unexpected version bytes. Schema-version id embedded per message.

## C6. Wire registry — single source (FR-008/032/034)
- **Invariant**: exactly one constant table (`glp_wire_registry`): 0x10 IL, 0x11 RESULT_ENVELOPE, 0x12+ messaging; `glp_il_codec` + `glp_result_codec` reference it — **zero duplicated constants** (SC-010).
- **Invariant**: each entry carries `{payload_type, functor, compat_mode, qmedit_dsl, cddl}`; CDDL is the registered artifact, both DSL forms stored; translation is Claude-agentic (Constitution V).

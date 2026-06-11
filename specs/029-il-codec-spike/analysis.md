# Cross-Artifact Analysis — IL/Bytecode Round-Trip Codec Spike

**Date**: 2026-06-11 | **Scope**: spec.md, plan.md, research.md, data-model.md, contracts/il-codec-contract.md, tasks.md vs constitution v1.0.0
**Mode**: non-destructive / advisory.

## Coverage matrix (requirements → tasks)

| FR | Tasks | SC | Tasks | Status |
|---|---|---|---|---|
| FR-001 encode/decode | T010,T011 | SC-001 round-trip | T014 | ✅ |
| FR-002 structural identity | T014 | SC-002 execute-equiv | T015 | ✅ |
| FR-003 execute-equivalence | T015 | SC-003 opcode coverage | T021 | ✅ |
| FR-004 preserved properties | T017 | SC-004 zero silent fail | T016 | ✅ |
| FR-005 recursive constants | T008,T013,T021 | SC-005 5 named gates | T017 | ✅ |
| FR-006 whitelist loud-fail | T008,T016 | SC-006 contract locatable | T020 | ✅ |
| FR-007 ≥10 corpus | T013,T018,T021,T025 | SC-007 Lean sorry-free | T023 | ✅ |
| FR-008 coverage gate | T021 | | | ✅ |
| FR-009 heap scope (phased) | a:T010–T015 / b:T024–T025 | | | ✅ |
| FR-010 Lean proof | T022,T023 | | | ✅ |
| FR-011 correctness contract | contracts/ + T020 | | | ✅ |
| FR-012 no semantic change | design + T028 | | | ✅ |

User stories: US1→T013–T015 (+T024–T025), US2→T016–T020, US3→T021–T023. No FR/SC/story is uncovered.

## Constitution check (v1.0.0)
- **III SRSW (machine-checkable)**: PASS — codec *preserves* polarity; the literal `skipSRSW` appears only in a negative compliance assertion (tasks.md), within the self-mention boundary → not a violation.
- **V Claude-only LM (machine-checkable)**: PASS — `OPENAI_API_KEY`/`litellm`/`openai` appear only in negative assertions (plan.md); A6 mandates no external API on the Lean path.
- **VI-a migrations (machine-checkable)**: PASS — no DB migration; head `0010` untouched.
- I, II, IV-a/b, VI-b, VII, VIII: PASS (judgement/advisory) — see plan Constitution Check table.

## Findings (severity-ranked)

| # | Sev | Finding | Location |
|---|---|---|---|
| F1 | HIGH | Discriminant-table completeness is corpus-driven only. A concrete `IOp`/`IOpV2` subtype absent from every corpus program would be missed by both the table and the coverage gate → a silent gap vs SC-004/FR-008. | T006,T012,T021,data-model |
| F2 | MEDIUM | Execute-equivalence on the **empty program** is undefined — "execute an empty program" has no defined `ExecutionResult`; FR-003 × empty-program edge case unresolved. | spec Edge Cases, D6, T015 |
| F3 | MEDIUM | "≥10 programs" (FR-007) currently lists 9 named cases + a sweep; the ≥10 floor is not guaranteed by construction. | data-model corpus table, T013 |
| F4 | MEDIUM | Constitution VII wants baseline-GREEN **before** change; tasks only re-check baseline after (T028). No pre-work baseline task. | tasks.md |
| F5 | MEDIUM | Unverified prerequisites: (a) Lean 4 + lake + Lean-LSP-MCP on the Windows host (blocks SC-007); (b) `FrameCodec` exposes a public API to wrap an arbitrary payload (T019); (c) how the phase-b test heap with an embedded `ModuleTerm` is constructed (T024/T025). | plan, T003/T019/T024 |
| F6 | LOW | The property→gate map is duplicated in data-model.md and contracts/. Acceptable (contract is the consumer-facing artifact) but a drift risk. | data-model, contracts |
| F7 | LOW | The literal constitution-scan tokens in plan.md/tasks.md (negative assertions) trip a naive literal scanner; reword to e.g. "no external-LM-API credentials" to keep the machine scan clean. | plan.md, tasks.md |
| F8 | LOW | Phase b (FR-009b) has no dedicated success criterion; covered transitively by SC-001/002 via corpus case 9. | spec SC, data-model |

## Top remediations (in priority order)
1. **F1** — add a reflection-based completeness test: assert every concrete `IOp`/`IOpV2` subtype has a discriminant entry, and that encode of a known-family instruction with no table entry fails loud. Strengthen T006/T012/T021. (Highest leverage: closes a silent-gap class against SC-004.)
2. **F2** — decide the empty-program execute semantics: either define its expected `ExecutionResult`, or scope the empty program to the structural-identity gate only and exempt it from execute-equivalence. Encode the decision in spec/data-model/T015.
3. **F3** — make the corpus floor explicit (≥10 concrete compiled programs); have the constant-type sweep contribute named programs so the count is guaranteed.
4. **F4** — add a pre-work "baseline suites green" task before Phase 2 (Constitution VII).
5. **F5** — verify the three prerequisites before their phases start (Lean toolchain; `FrameCodec` public surface; phase-b heap construction); convert each into an explicit setup precondition.

No CRITICAL findings. Recommended next step: apply F1–F3 (cheap, prevent rework), then proceed to `/buildkit-implement` (MVP = Phases 1–3).

## Resolution (folded 2026-06-11)
All five top remediations folded into the artifacts:
- **F1** → T012 (loud-fail on missing entry) + new **T029** reflection completeness test; data-model & contract Guarantee 3 updated.
- **F2** → empty program execute-exempt: spec FR-003/SC-002/Edge Cases, research D6, data-model, contract Guarantee 2, task T015.
- **F3** → ≥10 concrete-program floor explicit: spec FR-007 context, data-model corpus table, task T013.
- **F4** → new **Phase 0 / T000** baseline-green-before-change.
- **F5** → new prereq tasks **T031** (Lean toolchain), **T032** (`FrameCodec` public API), **T033** (phase-b heap construction), each gating its dependent task.
- F6/F7/F8 (LOW) noted; not folded (acceptable as-is).

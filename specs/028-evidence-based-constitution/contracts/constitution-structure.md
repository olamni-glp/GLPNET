# Contract: Constitution Document Structure

**Feature**: 028-evidence-based-constitution | **Date**: 2026-06-10

This is the contract the written `.specify/memory/constitution.md` MUST satisfy. The "consumer" of this contract is the `/buildkit-analyze` Constitution Check (an LM reviewer that loads the file, extracts each MUST, and treats conflicts as CRITICAL). The contract is structural, not executable — conformance is verified by inspection + the FR-016/FR-017 evidence, not by an automated schema check (FR-015: no harness).

## Required document shape

```
# <Project> Constitution

## Core Principles

### <Numeral>. <Name>
<normative MUST/SHOULD statement>
- **Evidence**: <file path> · <heading | FR-number | test-name>     (resolved on disk)
- **buildkit analog**: <analog>                                     (omit line if none)
- **Gate-ability**: <machine-checkable | judgement-gate-able | advisory>

… (repeat per principle; numerals III/IV/V/VI MUST be stable) …

## Governance
<supersession + amendment procedure; references DISCIPLINE/CLAUDE/specs, no duplication>
<non-elevation note: why DISCIPLINE §1.12 GLP-First and §1.13 FCP-Reference-Architecture are not principles>

**Version**: 1.0.0 | **Ratified**: 2026-06-10 | **Last Amended**: 2026-06-10
```

## Conformance clauses

- **C-01 (FR-001)**: 6–8 principles present; none of the `[PLACEHOLDER]` tokens remain.
- **C-02 (FR-002)**: principle set drawn from {I Spec-First, II Bug-Protocol, III SRSW, IV-a Language-Authority, IV-b Preserve-Internals, V Claude-Only-LM, VI-a Single-Head-Persistence, VI-b Single-PGLite-Cluster, VII Test-Gated-Shipping, VIII Single-Source-of-Truth} (modulo the two sanctioned content merges).
- **C-03 (FR-003)**: every principle has all four parts — normative statement, resolved Evidence, analog-or-omitted, exactly one Gate-ability label.
- **C-04 (FR-004)**: III/V/VI-a state their MUST as an analyze-LM scan instruction with the exact tokens / test family named.
- **C-05 (FR-005)**: scan instructions are explicitly scoped to "artifacts under review", with a stated boundary that the constitution's own token mentions are not violations.
- **C-06 (FR-006)**: VII labelled `advisory`; VIII roadmap-linkage clause labelled `advisory`.
- **C-07 (FR-007)**: numerals III/IV/V/VI unchanged from this numbering; count frozen before write.
- **C-08 (FR-008)**: `Version` is semantic (`1.0.0`), not CalVer; `Ratified` + `Last Amended` stamped.
- **C-09 (FR-009)**: references DISCIPLINE/CLAUDE/specs; does not duplicate their content.
- **C-10 (FR-010)**: explicit non-elevation note for DISCIPLINE §1.12 + §1.13.
- **C-11 (FR-011 / SC-003)**: 100% of Evidence lines resolve on disk; 0 fabricated.
- **C-12 (SC-005)**: the file's own occurrences of `skipSRSW`/`OPENAI_API_KEY`/`litellm`/`openai` do not cause self-flagging.

## Acceptance evidence (not part of the file itself)

- **A-01 (SC-001)**: `evidence/analyze-before.md` shows 0 MUSTs; `evidence/analyze-after.md` shows ≥6 MUSTs extracted on the same feature (026/027).
- **A-02 (SC-002)**: `evidence/negative-control.md` shows planted `skipSRSW`→CRITICAL (III) and `OPENAI_API_KEY`→CRITICAL (V).
- **A-03 (SC-006)**: git diff confined to `.specify/memory/constitution.md` + `specs/028-evidence-based-constitution/**`; no GLP runtime/`.glp`/language-definition file, no `/buildkit-analyze` skill change, no grep harness.
- **A-04 (SC-007)**: no pipeline command auto-invoked; no write to the file before full owner walkthrough approval.

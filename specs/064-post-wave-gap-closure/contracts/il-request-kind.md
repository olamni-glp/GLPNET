# Contract — IL request kinds in the split protocol

**Goal**: the thin client compiles locally and ships IL; the engine executes IL with no compiler reference on the execute path (FR-006/FR-007).

## New request kinds (WireProtocol)

| Kind | Body | Response |
|---|---|---|
| LOAD_IL | CompiledIlEnvelope (il_version, digest, source_metadata, IL body — the existing 062 envelope, unchanged) | LOAD_OK {module_ref} \| typed error |
| RUN_GOAL_IL | {goal_ref, optional inline CompiledIlEnvelope for one-shot goals} | the existing result envelope stream |

## Rules (normative)

1. The envelope format is the shipped 062 `CompiledIlEnvelope` — no new envelope. Version/digest checks and the hardening refusal taxonomy (malformed | il_version_mismatch | digest_mismatch | mid_transfer_truncation) apply verbatim.
2. Refusal is a typed error response; the engine keeps serving (never crashes, never falls back silently to the text path).
3. LOAD_SOURCE/RUN_GOAL (text kinds) remain valid during a deprecation window; a client chooses per session, never mixed per module.
4. Result equivalence: for every corpus program, IL-path results == text-path results (SC-003 gate, full regression corpus).
5. The engine host's execute path MUST NOT reference the compiler assembly; the client gains the compiler reference (project-file assertion + build check).

## Acceptance

Corpus sweep both paths with diff; refusal taxonomy cases; project-reference assertion test; cross-check with the 038 result-codec ride unchanged.

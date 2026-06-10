# Negative-Control Demonstration

**Captured**: 2026-06-10 | **FR-016 / SC-002** | One-time validation — NOT committed as a recurring harness (FR-015).

Two throwaway "artifact-under-review" fragments were constructed to prove Principles III and V fire CRITICAL when a real violation is present. These fragments are reproduced here as evidence only; they are not added to any test suite or scanned path.

## Control 1 — Principle III (SRSW / `skipSRSW`)

**Planted fragment (scratch spec-under-review):**

```
- FR-09X: The compiler MUST accept a `skipSRSW` flag to bypass single-reader/single-writer
  checking for performance-critical clauses.
```

**Constitution Check verdict via Principle III:**
- III scan instruction: nonzero count of literal `skipSRSW` in artifacts under review ⇒ CRITICAL.
- Count in the planted fragment = 1 (and the requirement proposes *using* the escape).
- **Verdict: CRITICAL.** ✓ (SRSW is inviolable; no `skipSRSW` escape may be proposed.)

## Control 2 — Principle V (Claude-Only LM / no external API)

**Planted fragment (scratch plan-under-review):**

```
- The proposer seam calls the OpenAI API via litellm; set OPENAI_API_KEY in the
  environment and route generation through openai.ChatCompletion.
```

**Constitution Check verdict via Principle V:**
- V scan instruction: nonzero count of `OPENAI_API_KEY` / `litellm` / `openai` **on an LM path** ⇒ CRITICAL.
- The fragment puts all three tokens directly on the generation (LM) path — an API-usage path, not a prohibition mention.
- **Verdict: CRITICAL.** ✓ (LM work must run in Claude via Agent/MCP; "needs an API" is a defect to delete.)

## Contrast with the AFTER baseline (why this matters)

In `analyze-after.md`, feature 027 also contains `OPENAI_API_KEY`/`litellm`/`openai` — but only as the *prohibition rule*, so V does **not** fire. Here the tokens are on an actual API path, so V **does** fire. The pair (027 = no-flag, this control = flag) demonstrates the gate discriminates real violations from rule-mentions.

## Self-flag check (SC-005)

Re-running the check confirms the **constitution document itself** — which also contains `skipSRSW`, `OPENAI_API_KEY`, `litellm`, `openai` — is **not** flagged, because the Governance "self-mention boundary" scopes III/V to the artifacts under review, never to the constitution that supplies the instruction. ✓

## Disposition

Both planted fragments discarded after capture. No recurring test added (FR-015/FR-016).

<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Clarification record — 105-ynet-election-integrity

**Date**: 2026-09-05 · **Stage**: `/bk-clarify`

## Outcome: no clarification questions were raised

The specification carries **zero** `[NEEDS CLARIFICATION]` markers. This section records **why**,
because "no questions" is a claim that should be auditable rather than assumed — a stage that
cannot raise a question is not a clarification stage.

The four decisions that would ordinarily need an engineer were all **already ruled today**, and
each is cited in the spec rather than re-asked:

| Would normally need clarifying | Already settled by | Where it lands in the spec |
|---|---|---|
| Are hosts or lanes the electors, and what is quorum? | `RULINGS-20260905T0050Z-shiras-hatzinor` — "quorum is 4 host oracles, not 15 lanes" | Assumptions; FR-005 |
| Is `actor != voter` a defect or a delegation? | Measured, not opinion: all five delegations verify and are key-bound (14:20Z) | Spec preamble; FR-001..FR-004 |
| Should a failed proof fall back to the actor? | Engineer ruling **G31-06**: refuse, never downgrade | FR-002 |
| Is a leader recognised on term 2? | Engineer ruling **G31-06** — supersedes G31-04 | Out of scope here; this feature supplies the rules the ruling rests on |

## One thing this stage DID change

Reviewing FR-007 against the live records exposed a gap the first draft had merged into one
requirement: **"one franchise submitted twice" and "one franchise submitted twice FOR DIFFERENT
CANDIDATES" are not the same event.** The live case (term 2, `gavriella`) is the benign one — both
submissions named the same candidate — and drafting to the live case would have specified only the
harmless half.

Split into **FR-007** (repeat submission → deduplicate to one, report) and **FR-008** (conflicting
submissions → report as a conflict, and do **not** silently choose). FR-008 has **no live
instance**; it is specified because nothing prevents it, and the rule must exist before the event
rather than after it.

## What was deliberately NOT clarified

**Key distribution, revocation and replay.** Verifying a signature says the holder of a key signed
a payload. It does not say the key is still authorised, or that the record is not a replay of an
older one. That is a real gap and it is written into Assumptions as out of scope — **named, not
quietly omitted.** It needs its own feature and probably its own owner; folding it in here would
have widened an era that is meant to close two measured defects.

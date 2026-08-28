<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# codexreview `20260828T004446Z` — 12 findings over the whole `codeconv` toolchain

    lane      gavriella @ GAVRIELLA · repo GLPNET · run mrun-20d9230f767b
    command   buildkit-codexreview review --review-only --max-cycles 1 --scope codeconv
              --aspect general --reasoning-effort high --max-seconds 1500
    result    exit 0, 752s, NOT timed out
    artifacts reviews/develop/20260828T004446Z/{codex.md,run.json,verdict.md}

## ⚠️ Read the count honestly

`findings UNCONFIRMED — no machine-readable block, but 12 finding(s) recovered from structured
prose`. **12 is a parse fallback, not a machine-readable count.** The individual findings below are
the evidence; the number is not. Same caveat as the 2026-08-24 round.

## ⭐ THE SCOPE CHANGED, AND THAT IS THE HEADLINE

Previous rounds reviewed **078's own module**. This round reviewed **`codeconv` entire** (332 files).
Of 12 findings, **exactly one is inside 078** (`receipts/manifest.py`) and **one is a test-suite
breakage** — the other ten are **pre-existing defects in the conversion toolchain** (features
012–020: `discover`, `builder`, `durable`, `depgraph`, `planagents`, `init`, `equiv`, `codegen`),
none of which 078 introduced or touches.

**078's own arc across four rounds: 10 findings / 8 HIGH → 4 HIGH → 1 HIGH → 1.** That is a
converging feature, not a stalled one.

## ✅ FIXED THIS SESSION — the two that were verified by measurement first

### 1 · `receipts/manifest.py` + `receipts/consumer.py` — an unknown adoption state took ADOPTED semantics

The finding: `load_adoption` accepted any string as a state; the consumer gate is the single equality
`if state == "non-adopted"`, so **every other value fell through to adopted semantics**. A typo
(`"adoped"`, `"nonadopted"`, `"pending"`) did not disable the gate loudly — **it turned an unearned
verdict GREEN**, through the very manifest that is supposed to authorise the refusal. This is
FR-008's own failure mode arriving via FR-019.

Fixed at **both** layers, because `adoption` is a plain dict any caller may build and so the loader
alone is not sufficient:

| layer | change |
|---|---|
| `manifest.ADOPTION_STATES` | the two legal values, named once |
| `manifest.load_adoption` | new `UndeclaredState`; rejects any illegal state **and** a duplicated area (two states for one area, one silently discarded) |
| `consumer.read` | refuses an unrecognised state at the gate itself rather than falling through to adopted |

Regression cover added in `tests/faultinj/test_manifest.py` — **11 new assertions**, including a
parametrised near-miss set over both legal values (`adoped`, `nonadopted`, `non_adopted`, `pending`,
`""`, `ADOPTED`), the duplicate-area case, the load-bearing consumer-gate case, and a positive test
that the guard still admits the two values it exists to distinguish. **51/51 faultinj green.**

### 2 · `tests/test_migration_{0008,0009}_single_head.py` — **the suite failed unconditionally**

Verified before touching anything: **4 failed, 4 passed**. Both files asserted
`heads == ["0010"]` and pinned the *entire* revision chain by name, while migrations `0011` and
`0012` have since landed.

**The defect is not the stale number — it is that the assertion restates the calendar.** Any
migration by any feature broke a test that claims to protect "single head, no branch". Rewritten to
assert the invariant **structurally**: exactly one head; exactly one root; no merge revision (tuple
`down_revision`); no revision with two children; and the revision each file *owns* is present with
its documented parent. **8/8 green, and the next migration will not break them.**

## 📋 CARRIED — 10 pre-existing conversion-toolchain findings, NOT in 078

Captured durably on the marathon run rather than fixed here: they are a different feature area, and
folding a ten-defect remediation into 078 would make 078 unshippable and unreviewable.

| # | sev | site | defect |
|---|---|---|---|
| 1 | P1 | `tools/discover/workflow.py:888` | `--from-tombstones` accepts an absolute or `../` path verbatim into the inventory; scaffold later joins it to the staging dir — **path traversal out of the target tree** |
| 2 | P1 | `tools/builder/__init__.py:520` | `builder retry --file` is a **no-op that exits successfully**: `resume_pending()` filters `builder:` ids while `target` is a legacy `file:` id (current children use `file:pre:` / `file:post:`) |
| 3 | P1 | `durable/steps.py:189` | the plan step marks itself complete **without spawning the planning agent or checking the artifact exists** — unblocks dependents with no conversion plan |
| 4 | P1 | `tools/depgraph/tombstone_writer.py:91` | a re-scaffold rewrite keeps only base + 015 fields, **erasing plan/convspec/codegen/equivalence/no-emit/enrichment state** |
| 5 | P1 | `tools/planagents/tombstone_writer.py:141` | same class: a replan drops **every 018-and-later field** |
| 6 | P1 | `tools/equiv/manifest.py:133` | validation counts every matching subsystem, so the checked-in manifest's deliberate `heap_fcp` ⊂ `runtime/` overlap is rejected as multiply-classified — **the authoritative manifest cannot validate**; should use `Manifest.classify()` longest-prefix |
| 7 | P1 | `tools/equiv/relation.py:267` | dropping `vars`/`reader`/`writer` makes a 1-var and a 2-var `UNIFY` compare **equal** — divergent conversions can be promoted as equivalent |
| 8 | P2 | `tools/discover/workflow.py:439` | reviving an orphan **overwrites the freshly-written live tombstone** with the stale orphan copy |
| 9 | P2 | `tools/init/workflow.py:424` | a re-init with a changed exclusion set only upserts; **removed exclusions stay active forever** and the idempotent branch is never reached |
| 10 | P2 | `tools/codegen/workflow.py:803` | retrying a `no_emit` row leaves `no_emit=true` and its tombstone keys, so readiness still excludes it though `retry` reports it reopened |

**Recommended disposition:** #1 (traversal), #2 (silently-successful no-op) and #7 (false-equivalence
promotion) are the three that can produce a WRONG RESULT rather than a stuck one, and should lead.
#4 and #5 are one defect in two places and should be fixed together.

— `gavriella` · `glpnet` · `mrun-20d9230f767b` · 2026-08-28

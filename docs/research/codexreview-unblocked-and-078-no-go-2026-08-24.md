<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# `/bk-codexreview` is unblocked — root cause, working route, and the NO-GO it then returned on 078

| field | value |
|---|---|
| lane / host / repo | `gavriella` · `Gavriella` · `GLPNET` |
| marathon run | `mrun-20d9230f767b`, feature `078-verification-receipts` |
| at | 2026-08-24T18:0xZ (session 5) |
| supersedes | `docs/research/codexreview-two-blocking-defects-2026-08-24.md` — **defect 2 is root-caused and routed around; the release gate is no longer a tool block** |

---

## 1. What was believed, and what is true

The session-4 hand-off recorded, in bold, that **`/bk-codexreview` cannot be discharged on this
host**, that both documented routes fail, and that *"no release can be cut under the 'codex
reviewed' criterion until buildkit fixes defect 1 or 2."*

That is **no longer true**, and the reason is not that buildkit fixed anything.

## 2. Root cause of defect 2 — it is in **git**, not in buildkit

`buildkit_cli/codexreview/scope.py:resolve_path` resolves a path scope with:

```python
_git(["ls-files", "--", *pathspec, *excludes], repo)
```

where `excludes` is the eight-entry `_DIFF_EXCLUDES` tuple. Measured on
**git 2.55.0.windows.3**, in this repo:

| command | files |
|---|---:|
| `git ls-files -- codeconv/src/codeconv/receipts` | **8** |
| `git ls-files -- codeconv/src/codeconv/receipts :(exclude)**/*.map` | **0** |
| `git ls-files -- codeconv/src/codeconv/receipts :(exclude)reviews/**` | **0** |
| `git ls-files -- codeconv/src/codeconv/receipts :(exclude)**/yarn.lock` | 151→ n/a, **8** unaffected¹ |
| `git ls-files -- codeconv/src` *(all 8 excludes)* | **0** |
| `git ls-files -- codeconv/src` `:(exclude)**/yarn.lock` | **151** |
| `git ls-files -- docs/research` *(all 8 excludes)* | **245** |
| `git ls-files -- codeconv` *(all 8 excludes)* | **332** |

¹ per-exclude bisection on `codeconv/src`: only **`:(exclude)**/*.map`** and
**`:(exclude)reviews/**`** empty it; the other six leave 151.

**Two of the eight excludes each independently empty a nested pathspec that they cannot possibly
match**, while leaving other pathspecs untouched. git returns an empty list; buildkit then
correctly refuses `empty_scope`. **buildkit's refusal is honest — its input is wrong.**

> ### 🔴 An intermediate conclusion of mine, withdrawn
>
> I first measured this with **one** exclude and published the tidy rule *"any pathspec of three or
> more components is emptied when any `:(exclude)` is present."* **That rule is FALSE.** Re-measured
> against the full eight-exclude set the tool actually uses: `docs/research` (two components)
> survives all eight, and `codeconv/src` (two components) does not. The pattern is per-exclude and
> per-path, not depth. I am recording the reproducible commands above instead of the rule I wanted.

## 3. The working route — usable by every lane today

**Use a single-component (repo-root) directory as `--scope`.** It contains the nested subtree
anyway, so nothing is lost:

```
buildkit-codexreview brief  --scope codeconv --json                       # ok — 332 files
buildkit-codexreview review --scope codeconv --review-only --max-cycles 1 \
    --max-seconds 1500 --aspect "078 verification receipts: no check may pass without proving it ran"
```

**Do not spend another session concluding codexreview is unusable.**

**Fix owed upstream (buildkit, two-repo — not made from this repo):** drop or `:(glob)`-qualify the
two offending excludes, or apply exclusion in Python after an unfiltered `ls-files`; add a
regression test asserting a nested subtree resolves non-empty.

## 4. Defect 1 was not re-tested — stated, not implied

`--scope diff` inlining the diff body was **not** re-tested this session. The root-scope route makes
it unnecessary, and re-running a 66 236-insertion diff to re-confirm a known overflow is not worth
the wall-clock. **It should be assumed still live.**

---

## 5. The review then ran — and returned **NO-GO on 078 itself**

| field | value |
|---|---|
| run | `20260824T165651Z` |
| scope / form / mode | `path` · `prompt` · `review_only` |
| cycles | 1 / cap 1 |
| exit · timed_out | **0** · **false** |
| residual findings | **10 — 8 HIGH, 2 MEDIUM** |
| artifacts | `reviews/develop/20260824T165651Z/{codex.md,verdict.md,run.json}` (gitignored) |

> ⚠️ **Honesty caveat on the count.** `run.json` carries `findings_count_status: "unconfirmed"` and
> `prose_fallback_findings: 10` — codex returned **prose, not structured JSON**, so 10 is a
> *parse fallback*, not a validated count. Treat the individual findings as the evidence and the
> total as approximate.

### The findings are exactly on-thesis

078 exists so that **no check may pass without proving it ran.** The review found that the receipts
module *itself* can pass without proving it ran:

| sev | file:line | defect |
|---|---|---|
| high | `receipts/consumer.py:73-74` | `read()` accepts a successful receipt from **another check/area/prior run** — neither model carries a verifiable run ID, so one check can reuse another's PASS |
| high | `receipts/receipt.py:162-170` | validation has **no PASS branch**; a PASS with an unresolved target or unknown total validates, then is reported successful |
| high | `receipts/receipt.py:157-161` | reconciliation enforces only `examined <= total`; FR-010 requires **examined + skipped <= total**, so 5 examined / total 5 / 1 skipped is accepted |
| high | `receipts/manifest.py:72-80` | an `expected.json` that is `{}`, an empty list, or carries a **different `run_id`** is accepted as an empty expected set ⇒ `missing_checks()` reports none |
| high | `receipts/manifest.py:88-90` | run reconciliation treats any correctly-**named** `*.receipt.json` as proof the check ran — never loads or validates it |
| high | `tests/faultinj/conformance.py:61-68` | the fixture reaches `passed == len(_CASES)` **without exercising the declared `BOUNDED` case** |
| high | `tests/faultinj/test_guard_weakening.py:22-27` | the mutation test replaces the validator with a no-op and asserts the invalid receipt slipped through — so **it stays GREEN under a weakened guard**, the inverse of SC-007 |
| high | `receipts/override.py:66-73` | `applies()` compares only area and check, ignoring the recorded **reason** — one override authorises every other refusal from that check until expiry |
| med | `receipts/consumer.py:58-59` | a declared non-adopted area's original verdict is **discarded** rather than kept behind a marker; since all real glpnet areas start non-adopted, the manifest disables verdicts instead of phasing adoption |
| med | `receipts/consumer.py:72-79` | malformed shapes raise `TypeError`/`AttributeError` from `load()` uncaught — the consumer **crashes** instead of returning the required named UNREAD refusal |

### 🔴 Release decision: **NO-GO**, and for a better reason than before

`/bk-release` is **not** cut this session. The reason has changed in a way that matters:

* **Before:** *"we cannot review, so we cannot certify."* — a tool block.
* **Now:** *"we reviewed, and it failed."* — an evidence-based refusal.

Shipping 078 today would ship, inside the feature whose entire purpose is to eliminate
pass-without-proof, **eight distinct ways to pass without proof.** The two test findings are the
sharpest: a conformance fixture that reports full coverage without running a declared case, and a
mutation test that stays green when the guard it guards is removed.

**These 10 findings are the concrete work item for 078's next implementation slice.** They are
specific, file-and-line located, and each names the requirement it violates.

---

*Every figure names its command, ref or artifact path. Verify by content, never by exit code.*

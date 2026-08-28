<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# `buildkit-codexreview` — two defects that together block **every release on this host**

| field | value |
|---|---|
| host | `gavriella` · repo `GLPNET` · run `mrun-20d9230f767b` |
| measured at | **2026-08-24T08:0x–08:3xZ** |
| codex | `codex-cli 0.145.0`, `gpt-5.6-sol`, **verified working standalone** |
| buildkit | working tree `088-takt-phase-wall-clock`; `origin/main` also v1 |
| severity | **blocking** — the release criterion is "fully implemented **and codex reviewed**" |

## Why this matters

078's MVP is implemented and green on `develop` (29/29 targeted tests). The **only** remaining
gate to a release is a codexreview run. **It cannot be produced on this host.** Both documented
routes to a review fail, for different reasons.

---

## Defect 1 — `--scope diff` inlines the diff body and overflows the model context

The tool's own contract says the opposite:

> *"the review context is delivered as a size-invariant BRIEF (spec + changed-files list, **NEVER
> the diff body**) so a huge diff cannot overflow the context window"*

**Measured.** Run `20260824T081610Z`, base `0616f253` (the commit immediately before the 078 code
landed), diff = **35 files / 66,236 insertions**. `codex_stderr.txt` is **2,675,391 bytes** and
contains **verbatim diff hunks** (`-*Standing caveat:* …` / `+*Standing caveat (superseded):* …`).
It terminates:

```
ERROR: Codex ran out of room in the model's context window.
Start a new thread or clear earlier history before retrying.
codex
Review was interrupted. Please re-run /review and wait for it to complete.
```

So the brief **is not size-invariant** — it carries the diff body, and a large diff does exactly
what the design says it cannot.

## Defect 2 — `--scope <path>` refuses a subtree that demonstrably has tracked content

```
$ buildkit-codexreview review --review-only --scope codeconv/src/codeconv/receipts \
      --base 0616f253… --max-cycles 1
[buildkit-codexreview] refused: empty_scope —
  path 'codeconv/src/codeconv/receipts' matched no tracked, reviewable content
```

Contradicted directly:

```
$ git ls-files codeconv/src/codeconv/receipts/ | wc -l
8
```

Reproduced **twice** — with and without an explicit `--base`. The path-scope route is the natural
escape hatch from defect 1 (review a small subtree instead of a huge diff), and it is closed.

**Together these leave no working route to a review of a large change.**

---

## What is NOT wrong — ruled out by measurement, so nobody re-investigates

| hypothesis | verdict | evidence |
|---|---|---|
| codex CLI broken / not logged in | **REFUTED** | `codex exec` → `CODEX_OK`, 13,594 tokens |
| `codex review` subcommand gone in 0.145.0 | **REFUTED** | `codex review --help` exits 0 |
| stdin-prompt (`review -`) form broken | **REFUTED** | streams normally |
| Windows `.cmd` launcher output not captured | **REFUTED** | the harness's own resolved argv `['cmd','/c',…codex.cmd]` returned **16 bytes of stdout** in a direct reproduction |
| the harness discards codex output | **REFUTED** | it persists `codex_stderr.txt` faithfully — 2.6 MB of it |

**Correct behaviour worth preserving:** the harness reported **`findings UNCONFIRMED`**, not a
clean review. It refused to convert "no parseable findings" into a pass. That is 078's own thesis
working inside the review tool, and it is the reason this was caught rather than banked as a green
review.

## A third, milder issue

The failure reason (`context window`) is only discoverable by reading **2.6 MB of stderr**. The
CLI line says merely *"codex emitted no machine-readable findings block"* and **exits 0**. An
unattended caller cannot distinguish *"reviewed, nothing found"* from *"the model never got the
input"*. The exit code should differ, or the surfaced message should name the overflow.

Also observed, non-blocking: `token record skipped (cycle 1, codex): pgdb/.lock held by PID 24084`
— the token row for the review cycle was silently dropped, so even a successful review would not
have been costed.

## Asks of the buildkit lane

1. **Make the brief actually size-invariant** — send the changed-files list and the spec, not the
   diff body; or chunk per file with a budget.
2. **Fix `--scope <path>`** so a tracked subtree is reviewable. This is the workaround for (1) and
   is currently unavailable.
3. **Exit non-zero (or name the cause) on context overflow.** `findings UNCONFIRMED` + exit 0 reads
   as benign.

## Consequence for this repo, stated plainly

**No release can be cut here under the "codex reviewed" criterion until defect 1 or 2 is fixed.**
078's implementation is ready and green; the gate is the review tool, not the code.

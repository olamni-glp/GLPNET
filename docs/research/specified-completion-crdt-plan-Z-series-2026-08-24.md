<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# Specified-features completion — **Z-series** CRDT plan (glpnet lane, 2026-08-24)

**Marathon**: `mrun-20d9230f767b` · **Lane**: `gavriella` · **Host**: `GAVRIELLA` · **Repo**: `GLPNET`

> **EXTENDS, DOES NOT REPLACE**, `docs/research/specified-completion-crdt-plan-2026-08-23.md`
> (OLAMNIT lane, `mrun-76da6e46bd44`, from 3rtask run `20260823T093108Z-30dd`). That file covers
> **three** features (082/083/085) from a **different lane's** run. This file covers **all six**
> features at `specified` today, from **this** lane, and records what changed in the 32 hours since.
> **Where the two disagree on 082/083/085, the newer measurement here wins; where this file is
> silent, that file governs.** Same contract as both predecessors: **marathon items are the state
> machine, this file is the authoritative content. This file wins over item names.**

🔴 **I nearly authored a duplicate.** I had the six-feature inventory measured and was about to write
a fresh plan when `git log --grep` surfaced `a02f983e` — a CRDT plan for this exact task, 32 hours
old. **Verify absence before building** fired again, and only because I searched commit messages
rather than the docs tree. Third occurrence of this class in two sessions.

---

## 1 · 🔴 THE HEADLINE — all six are **implemented in fact and specified on paper**

**Every one of the six features at `specified` already has its code on `origin/develop`.** Measured
this session with `git rev-list -1 origin/<branch> --not origin/develop`:

| feature | branch state vs `develop` | how it landed |
|---|---|---|
| `065-ynet-consolidation` | **fully contained** | merged |
| `080-occurs-checked-substitution` | **fully contained** | TIDY-Y04 `78c056a4` |
| `082-feature-stream-superset` | **fully contained** | TIDY-W03, 2026-08-20 |
| `083-glptutorial-corpus-goldens` | no origin branch — **merged and auto-deleted** | TIDY-Y01 `8a83bfc2` |
| `085-onrestart-fleet-resume` | no origin branch — **merged and auto-deleted** | PR #210 `3037f155` |
| `079-madglp-writer-reader-discipline` | no origin branch — **merged and auto-deleted** | PR #172 `fb038d11` |

> **The stall is entirely in the pipeline record, not in the work.** These are not six features
> waiting to be built. They are six features whose *code shipped to `develop`* while their roadmap
> row stayed at `specified` and their spec header stayed `Status: Draft`. The 08-23 plan named
> "pipeline-pointer drift" as the dominant executable cause; **this measurement is the sharper
> statement of the same thing** — the drift is not that no stage can advance, it is that **the work
> advanced past the record and the record never caught up.**

**Consequence that must not be lost:** driving these to `close` is mostly **reconciliation and
verification**, not implementation. Any plan that budgets them as six fresh build-outs is wrong by
roughly the cost of the work already done. **But `close` must not be reached by simply stamping the
record** — each still owes the stages it skipped, above all `/bk-codexreview`, because code that
reached `develop` without review is exactly the class 078 exists to eliminate.

## 2 · What changed since the 08-23 plan — measured, not assumed

| 08-23 claim | status today | evidence |
|---|---|---|
| "all three specs are `Status: Draft`" (082/083/085) | **083 is no longer Draft** | `083 spec:11` now reads `Status: Clarified — ALL RULINGS CLOSED … Ready for /bk-plan` |
| `G083b` — FR-002 §1.14 ruling gates specify→plan | ✅ **DISCHARGED** | ruled **(b) record the rejection** by the engineer 2026-08-24; FR-009 consequently **IN SCOPE** (`88174d1b`) |
| `G083a` — homing: 083 allocated to `ariellas` | **still open** | not re-ruled; 083's code has since merged via this lane's TIDY-Y01 |
| `G082` — capability-name + is 082 a glpnet feature? | **still open**, now compounded | 082 also has **no `feature_pipeline` row** (21 rows, 082 absent) ⇒ `/bk-clarify` default-denied |
| `G085` — homing: canonical = mstack BUILDKIT lane | **still open** | 085's code merged via PR #210 regardless |
| — | 🔴 **NEW GATE on 080** | `080 spec:11`: `Status: Draft — 🔴 BLOCKED on a §1.14 language-authority decision by Udi` |
| — | 🔴 **NEW: 079 is far more advanced than any of them** | spec + plan + tasks + research + data-model + quickstart + contracts, **0 of 20 tasks checked**, PR #172 merged |

## 3 · Per-feature state — what exists, what is missing, what blocks

Legend: ✅ present · ❌ absent · ▶ next executable stage.

| feature | spec | clarify | plan | tasks | analyze | code on develop | ▶ next stage | blocked by |
|---|:--:|:--:|:--:|:--:|:--:|:--:|---|---|
| **083** glptutorial-corpus-goldens | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ | **`/bk-plan 083`** | **NOTHING — ready now** (homing G083a advisory only) |
| **079** madglp-writer-reader | ✅ | ❌ | ✅ | ✅ 0/20 | ❌ | ✅ | **`/bk-analyze 079`** | no Clarifications section — clarify was skipped, not recorded |
| **085** onrestart-fleet-resume | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ | `/bk-plan 085` | **G085** homing (mstack canonical) |
| **080** occurs-checked-substitution | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ | `/bk-plan 080` | 🔴 **Udi §1.14**: `UnifyFail` vs `CompileError` when the occurs-check fires |
| **082** feature-stream-superset | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | `/bk-clarify 082` | **G082** homing **+ no `feature_pipeline` row** + would evict 078 from the single active slot |
| **065** ynet-consolidation | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | resume `mrun-7939e12b5b70` | engineer: rule the **G2 / FR-008 five-escalate gate** |

## 4 · The Z-series ledger

Sizes: nano/micro/mini/midi/maxi/saga. States: ▶READY · BLOCKED · DEFERRED · DONE.

| ID | Item | Size | Pts | State | Gate |
|---|---|---|---:|---|---|
| **Z00** | Reconcile the pipeline pointer + roadmap state for all six: record that code is on `develop` and move each row off `specified` to its true state | mini | 7 | ▶READY | in-lane |
| **Z01** | **083** `/bk-plan` → `/bk-tasks` → `/bk-analyze` → `/bk-implement` (verify against merged code) → `/bk-codexreview` → `/bk-ship` → `/bk-close` | maxi | 17 | ▶**READY — START HERE** | none |
| **Z02** | **079** `/bk-analyze` → `/bk-implement` (20 tasks, 0 done) → `/bk-codexreview` → `/bk-ship` → `/bk-close` | maxi | 17 | ▶READY | record the skipped clarify first (Z02a) |
| Z02a | **079** record why `/bk-clarify` produced no Clarifications section — a skipped stage that left no trace is a false green | nano | 3 | ▶READY | in-lane |
| Z03 | **080** ruling → `/bk-plan` → … → `/bk-close` | maxi | 17 | **BLOCKED** | 🔴 **G080 — Udi §1.14** |
| G080 | **UDI / §1.14 LANGUAGE AUTHORITY**: when the bind-time occurs-check fires, is the outcome `UnifyFail` (a runtime unification failure) or `CompileError`? The spec deliberately presents both and **must not decide** | midi | 11 | DEFERRED | Udi's express decision |
| Z04 | **085** `/bk-plan` → … → `/bk-close`, **or** formal hand-off to the mstack BUILDKIT lane | maxi | 17 | **BLOCKED** | G085 homing |
| G085 | **ENGINEER**: `bk-onrestart` canonical is mstack (P02, do-not-fork). Does 085 belong on the **glpnet** roadmap at all, given its code already merged here via PR #210? | mini | 7 | DEFERRED | engineer ruling |
| Z05 | **082** `/bk-clarify` → … → `/bk-close`, **or** fold | maxi | 17 | **BLOCKED** | G082 + the missing pipeline row |
| G082 | **ENGINEER**: fold 082 into `scheduler-feature-stream-durable-healing-and-hardening`, or scope it as the engine half? (= engineer block **B7**; also needs a `feature_pipeline` row before any stage can run) | midi | 11 | DEFERRED | engineer ruling |
| Z06 | **065** resume its own marathon `mrun-7939e12b5b70` → … → `/bk-close` | maxi | 17 | **BLOCKED** | G065 |
| G065 | **ENGINEER**: rule the **G2 / FR-008 five-escalate gate** (ruling already requested 2026-08-23, still owed) | mini | 7 | DEFERRED | engineer ruling |
| Z07 | **Codexreview sweep**: every feature whose code reached `develop` unreviewed gets a `--scope <root-dir>` review before its `/bk-close` | midi | 11 | ▶READY | route proven today |
| Z08 | Discharge check: no feature reaches `close` with an open gate or an unrecorded skipped stage | mini | 7 | ▶READY | last |

**Total 166 pts** · **▶READY now: Z00 · Z01 · Z02 · Z02a · Z07 · Z08 = 62 pts** ·
**BLOCKED on four rulings (G080 · G085 · G082 · G065): 68 pts** · **gates themselves: 36 pts.**

## 5 · Execution order

1. **Z00** — reconcile the record first. Every later stage reads it, and it is the cause the 08-23
   plan named as dominant.
2. **Z01 (083)** — the only feature with **zero blockers** and a spec that says *"Ready for
   `/bk-plan`"* in its own header. Start here.
3. **Z02a → Z02 (079)** — furthest through the pipeline; needs `/bk-analyze` and 20 tasks verified
   against code that already merged.
4. **Z07** — review sweep, folded into each spine at its `codexreview` stage rather than run as one
   late batch.
5. **Z03 / Z04 / Z05 / Z06** — only after G080 / G085 / G082 / G065 are ruled. **Do not start on
   assumption** (method R3/R5, carried from both predecessor plans).
6. **Z08** — discharge.

## 6 · Discharge condition

This programme discharges only when: **Z00 done**; each of Z01–Z06 either driven to `/bk-close` or
formally handed to its correct lane with the hand-off recorded; **every G-gate resolved or
explicitly re-parked with a rationale**; and **Z07 has reviewed every feature whose code reached
`develop` unreviewed**.

🔴 **No feature is "completed" by silently advancing past an open gate, and none is completed by
stamping a record to match code that was never reviewed.** Both are the false-green this marathon's
own feature exists to eliminate.

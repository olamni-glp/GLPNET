<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: YNET election integrity

**Feature**: `105-ynet-election-integrity` · **Date**: 2026-09-05
**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md)

Ordered. `[P]` may run in parallel with the task above it.

## Phase 1 — the control comes first (TDD: red before green)

- [x] **T001** Extend the audit fixture with a **franchise conflict**: one franchise, one term, two
  submissions naming **different** candidates. *(FR-008)*
- [x] **T002** Extend the self-test to require **F6** to fire on T001's fixture, the conflicting
  franchise to contribute **nothing** to either candidate, and the run to exit non-zero.
  **T002 MUST FAIL before T003.** A control that passes before the code exists is testing nothing.
  *(FR-008, FR-012, SC-005)*
- [x] **T003 [P]** Extend the self-test with the **negative control** for F6: a franchise submitting
  **twice for the same candidate** must produce **F4 and NOT F6**. Without this, "always report a
  conflict" would pass T002. *(FR-007, SC-005)*

## Phase 2 — implement FR-008

- [x] **T004** Track `franchise → {candidates}` per term during resolution. *(FR-008)*
- [x] **T005** Emit **F6** when a franchise names more than one candidate, naming the franchise and
  every candidate with its timestamp. *(FR-008)*
- [x] **T006** **Exclude** a conflicted franchise from every candidate's host tally — do not pick
  one. Excluding is the requirement; a tie-break would be the silent choice FR-008 forbids. *(FR-008)*
- [x] **T007** Add F6 to the non-zero exit set alongside F1/F2/F3. A conflict can change which
  candidate wins, so a gate that consumes the exit status must see it. *(FR-011)*
- [x] **T008** Confirm T002 and T003 now pass, and that **every earlier control still fires** —
  the F6 change must not weaken F1/F2/F3.

## Phase 3 — verify against the live records

- [x] **T009** Run against the live oplog. Expected, from the pre-change measurement:
  term 1 → F3 + F4; term 2 → F4 + F5; **neither acquires F6**. A new F6 on live records means the
  rule is wrong, not that the records moved. *(SC-003)*
- [x] **T010** Confirm the tallies are unchanged by this feature: term 1 `1b23876b` 3 hosts,
  term 2 `2af0d277` 3 hosts. **A change here would mean FR-008 altered a decided election** and
  must stop the era. *(SC-002, SC-003)*
- [x] **T011 [P]** Confirm exit-code discipline end to end: clean → 0; findings → 1; no records,
  unreadable root, or no signature library → 2. *(FR-011, SC-006)*

## Phase 4 — pin it so it cannot rot

- [x] **T012** Ensure Section W (`test/run_all_tests.sh`) runs the self-test and requires it to
  pass, so a regression surfaces in the suite rather than in an election. *(FR-012)*
- [x] **T013 [P]** Republish the corrected audit to `<COOP_ROOT>/_standards/` so every lane runs
  the same instrument, and state in the broadcast that the earlier copy enforced a withdrawn rule.
- [x] **T014** Full suite run. Record the result **including the two known Section T failures**
  (`glpquick-cert/glpquick.pfx` absent since 2026-08-12) — they are unrelated to this feature and
  must be reported, not absorbed.

## Phase 5 — hand off what this lane does not own

- [x] **T015** Publish the F3 and F6 rules to the owner of the election code, with the audit as the
  independent check on their tally fix. **This lane owns the rules and the instrument; it does not
  own the emitter or the board's tally, and must not write either.**

## Out of scope — named, not omitted

- **Key distribution, revocation, replay.** Verifying a signature proves the key holder signed the
  payload; it does not prove the key is still authorised or that the record is not a replay. Needs
  its own feature and probably its own owner.
- **The board's tally fix.** `verify_voter` already exists in the election code and is already
  correct; nothing calls it. Ruling **G31-06** assigns that fix; T015 hands over the rules for it.
- **The two Section T failures.** Environmental — trust material absent since 2026-08-12 — and
  deliberately not regenerated so the destruction mechanism stays evidenced.


---

## Completion record — 2026-09-05

**T001–T003 (the control, red first).** Written before the implementation and confirmed FAILING:

    SELF-TEST FAIL: F6 did not fire on a franchise naming two candidates in one term
    SELF-TEST FAIL: conflicted franchise still counted for cand: excluded means excluded
    SELF-TEST FAIL: conflicted franchise still counted for other: excluded means excluded
    exit=1

**A defect the control found in the FIXTURE, not in the code.** After F6 landed, F3 stopped
firing — and F3 was right to stop. The original F3 case was ONE franchise naming two candidates,
which is an F6, not an F3. So **F3 had never been tested by its own shape**: two DISTINCT node ids
on ONE host, each naming one candidate, which only the host grouping can see. The fixture now uses
that shape (hosts `P`/`idE`/`idF`), matching the live term-1 `shiras` case. Without F6 forcing the
question, the F3 control would have kept passing on a case that was never F3.

**T004–T008.** `franchise -> candidate -> [ts]` tracked per term; F6 emitted naming the franchise,
every candidate and every timestamp; the conflicted franchise **removed from every candidate and
host bucket**. `FATAL_FINDINGS = (F1, F2, F3, F6)` and the exit status is now **derived from the
findings** rather than set at each emit site — a new code added without touching that line would
otherwise default to non-fatal silently, which is how a check stops gating unnoticed.

**T009/T010 — the load-bearing check: the live tallies are UNCHANGED.**

| term | candidate | hosts | findings | exit |
|---|---|---|---|---|
| 1 | `1b23876b` | 3 — gavriella, olamnit, shiras | F3, F4 | 1 |
| 2 | `2af0d277` | 3 — ariellas, gavriella, olamnit | F4, F5 | **0** |

**No F6 on any live record.** FR-008 did not alter a decided election — which is what T010 existed
to prove, and would have stopped the era had it come out otherwise. Term 2 alone exits **0**,
independently supporting ruling G31-06's choice of the clean term.

**T011.** live → 1 · unreadable root → 2 · empty oplog → 2 · term 2 alone → 0. Verified with the
audit's OWN exit status, not a pipeline's — era 102 closed on "a zero from a failed build is not
a zero", and the first reading here came from a `grep` and said 0.

**T012/T013.** Section W runs the control in the suite; the corrected audit is republished to
`<COOP_ROOT>/_standards/`, replacing the copy that enforced the withdrawn `actor == voter` rule.

## Codexreview — round 1 was INCONCLUSIVE; rounds 2 and 3 RAN, and found 18 defects

### Round 1 (2026-09-05T15:20Z) — **INCONCLUSIVE, NOT CLEAN**

`codex exec` ran and returned **zero findings**. That was **not** a clean review, and it was
recorded as inconclusive rather than clean. That decision is the only reason the rest of this
section exists.

### 🔴 THE ROOT CAUSE, found 2026-09-05T16:05Z — **the repo disabled its own second instrument**

The `blocked by policy` errors were real but were **not** the reason. The reason is that
**`AGENTS.md` told codex to stop.** Its transcript ends:

> *"I have read AGENTS.md, docs/DISCIPLINE.md, docs/typed-glp-manual.md, and
> docs/glp-cheat-sheet.md completely. Per AGENTS.md, I am stopping before reading any review-scope
> files."*

The mandatory-reading rule ends in **"STOP AND WAIT ... until Gabi gives direction"** — correct for
an interactive session, fatal for a non-interactive one whose direction *was* the prompt it was
started with. `AGENTS.md` was also **111 days stale** and instructed codex to use
`C:/pglite/research/glpnet`, which `CLAUDE.md` now marks **STRICTLY PROHIBITED**.

**Remedy (engineer ruling, 2026-09-05):** `AGENTS.md` regenerated as a thin pointer to `CLAUDE.md`
plus an explicit non-interactive carve-out. **Verified by measurement, not assumed:** round 3 was
run with a **bare prompt containing no override text at all**, and codex proceeded straight to the
review — so the fix is in the file, not in whoever writes the prompt.

### Round 2 (16:07Z) — **11 findings, 5 HIGH**. Round 3 (17:00Z) — **7 more, 2 HIGH**.

Every round-3 finding was in **round 2's fixes** or in code round 2 had not reached. A fix is a
code change and carries the same defect rate as any other; this is the same result this repo has
recorded before and it should stop being surprising.

| # | sev | finding | disposition |
|---|---|---|---|
| 1 | **HIGH** | an **incomplete** delegation proof (`voter` + `voter_sig`, no `voter_spki`) was downgraded to a **direct actor vote** — strip a field off someone else's delegated vote and it becomes yours | **FIXED**; control proven to fail pre-fix |
| 2 | **HIGH** | franchise ids **truncated to 12 chars before comparison**, so F6 could exclude an innocent franchise sharing a prefix | **FIXED** — identities are never truncated for comparison, only for display |
| 3 | HIGH | `PollOnce` could run **concurrently with the background pump** and deliver a frame twice; the comment claimed it was impossible | **FIXED** — refused, not commented |
| 4 | HIGH | `Open`/`Close` were **check-then-set on a `volatile bool`** — two threads could each start a pump and `Close` stop only one | **FIXED** — serialized |
| 5 | HIGH | a `.frame` containing `{}` parsed into empty defaults and was **delivered as `unknown-origin`** — a message manufactured from an empty file | **FIXED** — unaddressed frames are strays |
| 6 | **HIGH** | **a frame was consumed before its alert was durable.** `Received` only enqueues; on the mailbox-overflow path the loss was *certain*, not racy | **FIXED** — `ConfirmDurable` gate; the frame stays in the inbox until the record exists |
| 7 | **HIGH** | `Origin` was never checked against `SenderNode`/`SenderActor`, so a frame could **claim any origin it liked** and be displayed as coming from the victim | **FIXED** — they must agree |
| 8 | MED | **F4 grouped by host, not franchise** — two different franchises on one host voting once each was reported as a repeat submission (a *wrong finding* on a shared audit) | **FIXED**; control proven to fail pre-fix |
| 9 | MED | the admitted/completed counters **moved the `WaitForIdle` window rather than closing it** — the increment came after `TryAdd` | **FIXED** — count before admitting, withdraw on refusal |
| 10 | MED | a `record_id` reused with **different content** was silently dropped: renaming a file could change the tally | **FIXED** — reported as a conflict |
| 11 | MED | candidate ids truncated into report keys, so two candidates sharing a prefix **overwrote each other** | **FIXED** |
| 12 | MED | `doctor` classified by **filename suffix alone** and exited 0 on an inbox the receiver treated as strays | **FIXED** — one shared classifier |
| 13–18 | MED/LOW | five tests that **could not fail** (an idempotence test on a carrier with no pump; a dedupe assertion counting unique dictionary keys; the stress probe below), and an unbounded stray list | **FIXED or DELETED** |

### 🔴 F3 vs F6 was a SPEC GAP, not a bug — referred, not guessed

Round 2 reported that F3 was evaluated *after* F6's exclusions and could hide a real conflict.
**FR-006 does not say which order applies**, and the obvious fix would have made every F6 also fire
F3 — regressing the F3/F6 distinction established the previous day. It was referred as a BK-STD-2
engineer question rather than decided here.

**Engineer ruling (2026-09-05):** F3 fires when a host holds **two or more distinct franchises**
that between them name more than one candidate, computed **before** F6 exclusion. Implemented, with
both controls: the positive case (a host whose conflict survives only pre-exclusion) and the
negative one (a lone self-conflicting franchise stays purely F6). Proven to fail against the old
ordering.

### One control was deleted rather than kept

A 400-iteration stress probe was written as the regression control for finding 9. Run against the
**pre-fix** implementation it **passed** — it discriminated nothing, so it was removed rather than
kept as a green decoration. The instrument that actually finds that defect is **the full suite run
in parallel**, and the file says so.

**Era 105 is now codex-reviewed.** Three rounds, 18 findings, all dispositioned.

## T014 — full suite, 2026-09-05T15:40Z

    Total: 592 | Passed: 590 | Failed: 2 | Skipped: 0 | Unsearchable: 0

**Unsearchable is now 0** — it was 4 before this era's TFM fix, all four hidden behind a stale C#
binary. Making them run is what exposed the two failures below; they were always there.

**The 2 failures are Section T (`T-1`, `T-2`) and are NOT this feature's.** The REPL fails **closed**
with a named diagnostic because `glpquick-cert/glpquick.pfx` is absent. Reported, not absorbed —
an era that quietly inherits another defect's red is how a known defect becomes invisible.

**New evidence, and it retires a fleet theory:** the directory's mtime is `2026-08-12 09:06:39` and
nothing has been removed from it since. So the "**fourth** destruction" reported by this lane on the
morning of 2026-09-05 was **one destruction on 2026-08-12, re-observed 24 days later.** There is no
recurring mechanism to hunt. **Not regenerated** — regenerating is a workaround and would erase the
only evidence there is.

**Section W (W-1..W-6) all pass**, so this era's control is pinned in the suite.

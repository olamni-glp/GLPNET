<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# The evidence-signal invariant

**Published for fleet adoption · feature 108 · olamnit-glpnet · 2026-09-06**

> **A signal a caller treats as evidence MUST NOT be observable in a state that reports completion
> before the work it reports has completed — and MUST NOT report completion for work that does not
> survive the next restart.**

---

## What this is, and what it is not

Feature **078** governs signals whose declared job is to state a **verdict** — a check reporting
PASS, clean, or zero findings. Its remedy is a receipt proving the check ran against its target.

This governs the **other** class: signals that state **no verdict at all** but that callers read as
evidence anyway. A wait that returns. An idle predicate that reads true. A liveness flag. A process
exit status. An empty result set. None of them claims to be a verdict, so none is covered by 078 —
and every one of them is read as "the work is done", and acted on.

The two features partition the space. **Neither widens into the other.** Where a signal both states
a verdict and is observable early, 078 governs the verdict and 108 governs the ordering; both bind
and neither is weakened. This boundary is not fussiness: the fleet has already paid for minting a
duplicate — feature 012 twice, three rival M6 clients in one morning, five rival elections in one
day.

## The eight measured instances

| # | signal | the caller read | what was true | lane |
|---|---|---|---|---|
| 1 | `WaitForIdle` returned | the pump drained | the pump had *taken* an item but not marked itself busy; caller read null. ~1 in 3 | olamnit-glpnet |
| 2 | `doctor` said `m6_met: true` | a client is running | nothing was running — the flag came from configuration, not observation | shiras-glpnet |
| 3 | `codex exec` exited 0, no findings | review found nothing | prompt passed positionally; **no review ran** | olamnit-glpnet |
| 4 | `scheduler reject` exited 0 | it succeeded | it **refused** | fleet tooling |
| 5 | election board rendered green | a leader is seated | running process and its own disk disagreed | shiras-ynglin |
| 6 | `codex exec` exited 0, **116 KB** | big, therefore real | read `AGENTS.md`, obeyed a STOP-AND-WAIT gate, stopped before opening code | olamnit-glpnet |
| 7 | `ack` exited 0, `doctor` said 0 pending | 13 alerts acked | a restart returned the same 13 ids unacknowledged | shiras-glpnet |
| 8 | `alerts` said `acknowledged: true`, on disk | the ack is durable | the ack **is** durable; the **startup replay** re-raises from the retained WAL and clobbers the record, re-stamping `arrived_utc` to the restart time, with `frames_accepted: 0` | olamnit-glpnet |

**Instance 6 is why a heuristic is not enough.** After instance 3 the fleet adopted *"39 bytes means
fake, a big transcript means real"*. Instance 6 is 116 KB of the four mandatory documents and zero
review. It passes that heuristic cleanly. **A defence tuned to one mechanism does not generalise to
the class.**

**Instance 8 corrects instance 7's stated mechanism**, which matters because the two imply different
fixes. The ack is not lost — it survives the process dying, on disk and through `alerts`. The
*replay path* destroys it. So the fix is not "make ack durable"; it is "replay must merge by
`message_id`, never overwrite" — or advance the high-water so replay knows the message was
delivered.

## The four mechanisms, and what actually catches each

| mechanism | catches it | does **not** catch it |
|---|---|---|
| early wait | count work outstanding from **acceptance**, not commencement | any amount of retrying; the window is a few instructions wide |
| did-not-run / refused | assert on **content only the completed work could produce** | exit status, output presence, elapsed time |
| size-as-evidence | same as above | any byte threshold — instance 6 defeats all of them |
| non-durable completion | observe → **restart** → re-observe, compared mechanically | anything that never restarts the component |

## Two rules that carry most of the value

**1. Absence of evidence is `unproven`, never `conforming`.** A surface with no conformance check
is a visible piece of work. A surface nobody declared is invisible. Both are worse than a failing
check, and only the second is silent.

**2. A check never shown capable of failing scores zero.** Every conformance check ships with a
**negative control** — a demonstration that it fails against the defect it governs. Of the eight
instances above, **zero** would have been caught by a check that ran but had never been shown
capable of failing.

Rule 2 was learned here twice in one afternoon. This repo's own C# suite already recorded it before
the rule existed: a 400-iteration stress probe *passed against the pre-fix code*, so it was removed
rather than kept as a green decoration. And while implementing this feature, a shell round-trip
wrote literal backspace bytes where word-boundary escapes were intended; every scan pattern became
unmatchable; the audit ran, wrote a report, emitted a receipt, and found **1** hit where ground
truth had **~400**. Its exit code and its report were both fine. Only asserting on content that only
a working scan could produce caught it.

## Adopting it

Adoption is declared in **feature 078's existing per-area manifest**, and one declaration covers
both features. There is deliberately **no second override mechanism**: a refusal is lifted only by
078's informed-consent override — briefing, acknowledgement, rationale, declared scope, and a
**mandatory expiry**. An override with no expiry is rejected when it is *recorded*, not when it is
relied on. An area with **no declaration at all** is an error, never non-adoption.

Practical steps, in order:

1. Run the audit. On an empty manifest every scan hit is undeclared and it exits **3**. That is
   correct — it is telling you evidence-bearing signals exist that nobody has declared.
2. Declare your scope, with a **rationale per region**. The scope is the coverage denominator, so an
   undocumented boundary is indistinguishable from an oversight. Everything outside it is *reported*
   as out-of-scope on every run, never silently dropped.
3. Declare each surface honestly, with `conformance_check: null`. The audit now exits **1**
   (unproven) instead of **3** (undeclared). That is measurable progress.
4. Prove one at a time: write the check **and** its negative control together.
5. **When you find a defect you do not own: report it, do not fix it in their tree.** Set
   `disposition: "disclosed"`, name the owner, and land a conformance test that *fails*. That is the
   Bug-Protocol's reporting mechanism, not a workaround.

## Reference implementation

- Audit + cross-check: `scripts/evidence_signal_audit.py` (stdlib only — an audit that cannot run
  for want of a dependency is the failure mode it exists to prevent)
- Manifest: `.specify/evidence-signals/manifest.json`
- Harness with controls: `scripts/tests/test_evidence_signal_conformance.py`
- Spec, contracts, quickstart: `specs/108-evidence-signal-ordering/`

Exit codes are distinct per failure class — `0` clean, `1` findings, `2` usage, `3` manifest/scan
disagreement, `4` unreadable region — and the tool **never exits 0 while reporting a problem**. An
audit for that class committing that class would be worthless.

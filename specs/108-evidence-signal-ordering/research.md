<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Phase 0 Research — feature 108

**Date**: 2026-09-06 · **Host**: OLAMNIT · **Lane**: `olamnit-glpnet`

---

## 1. Instance 8 — measured during planning, root cause localised

This is the most important result in this document, and it was not in the spec when planning began.

### What was claimed

`shiras-glpnet`, 2026-09-06: *"a receiver restart resurrects already-acked alerts."* Thirteen alerts
acked (exit 0 each, `doctor` → 0 pending), receiver restarted, and the **same 13 `message_id`s**
returned as `"acknowledged": false` with `arrived_utc` = the restart time. Their published note said
**`ack` itself is sound** and named the mechanism as **inferred**, not measured.

### What was measured here

A single alert was driven through the full cycle on OLAMNIT against build `eea87e02` — which is
**newer** than the build that produced the original report, and includes the R-C merge.

| # | state | `alerts --all` | `alerts` (pending view) | `doctor.pending_alerts` | on-disk `acknowledged` |
|---|---|---|---|---|---|
| 1 | delivered, before ack | `false`, arrived `14:54:30` | 1 | 1 | `false` |
| 2 | after `ack` (exit 0) | **`true`** | 0 | — | **`true`** |
| 3 | receiver process dead | **`true`** | 0 | **1** ← disagrees | **`true`** |
| 4 | after receiver restart | **`false`**, arrived **`14:56:18`** | 1 | 1 | `false` |

Row 4's `arrived_utc` is the **restart time**, not the original arrival. `frames_accepted` after the
restart was **0** — so no new frame was delivered by the carrier.

### The mechanism — now measured, not inferred

The ack is **durable**. It is written to the alert file and survives the process dying (row 3, on
disk). What destroys it is the **startup replay path**:

- `wal/unified-mailbox.wal` still holds the frame after delivery — it is not drained or compacted.
- `wal/dedup-seen.journal` correctly holds `olamnit/olamnit-glpnet-probe  1  1789311270371`.
- `origin_high_water` reads **0** even after a frame was accepted — the high-water never advanced.
- On start, the receiver replays the retained WAL entry and **re-raises the alert unconditionally**,
  overwriting the existing record's `acknowledged` flag and re-stamping `arrived_utc`.

So the peer's headline was right and their stated mechanism was wrong in a way that matters: the fix
is **not** "make ack durable" — ack is already durable. The fix is that **replay must reconcile
against existing alert records by `message_id`, never clobber them** — or the high-water must
advance so replay knows the message was already delivered. Two candidate fixes, one defect.

### A second, separable defect found in row 3

With the receiver dead and the ack durably `true`, `doctor` reported `pending_alerts: 1` while
`alerts` reported 0. **Two observers of the same state disagree.** This is FR-013 exactly, and it is
independent of the replay defect: `doctor` appears to count alert *files* rather than *unacknowledged
records*. Files are retained deliberately, so a file count can never be a pending count.

### Disposition

**Reported, not fixed.** `YngeniOS.Ynet.Client` is canonical per `Q-glpnetshiras-50` and this lane
is a contributor. Patching it here produces the fourth rival client. Per SC-001 this instance is
carried as **disclosed with a named owner** (`@ariellas-qhstate`), and this feature contributes a
**failing** conformance test that names it, so the defect is loud rather than remembered.

---

## 2. Why the fleet's existing defence does not generalise

The fleet adopted a heuristic after instance 3: *"39 bytes means the review did not run; a big
transcript means it did."* Instance 6 emitted **116 KB** and reviewed nothing — it read `AGENTS.md`,
obeyed a **"STOP AND WAIT"** reading gate, and stopped.

Applying each candidate defence to all eight instances:

| defence | catches | misses |
|---|---|---|
| exit-status check | — | 3, 4, 6 (all exit 0) |
| output-size threshold | 3 | 4, 6, and every non-tool instance |
| "did it produce output at all" | 3 | 4, 6 |
| **assert on content only the completed work could produce** | 3, 4, 6 | — for the tool class |
| outstanding-counter at *acceptance* | 1 | — for the wait class |
| observe → restart → re-observe | 5, 7, 8 | — for the durability class |
| two-observer agreement | 8's second defect | — |

**Decision**: FR-010 forbids size, presence and elapsed time as evidence and requires an assertion on
content only the completed work could produce. **Rationale**: it is the only defence in the table
that catches the whole tool class rather than one mechanism. **Alternative rejected**: raising the
byte threshold — instance 6 defeats any threshold, because the bytes were real, they were just the
wrong bytes.

---

## 3. Why the negative control was promoted from "recommended" to load-bearing

Survey question: *of the eight measured instances, how many would have been caught by a conformance
check that ran but had never been shown capable of failing?*

**Zero.** Every one of the eight is a signal that reports success in a state where it should not.
A check that always passes is indistinguishable from a check that correctly passes, and eight
instances is enough evidence that the fleet cannot tell them apart by inspection.

**Decision**: FR-018a — a conformance check ships with a demonstrated failure against the defect it
governs, and an unfalsifiable 100% scores zero (SC-003, SC-005).
**Alternative rejected**: recommending negative controls in guidance. Guidance that is not gated is
the same shape as the notes this repo has already had to retire — `CLAUDE.md`'s Known Limitations
block carried **two false claims and one understated one** until feature 101 measured them.

---

## 4. Scan design: crude and cross-checked, not clever and blind

**Decision**: text-pattern scan across five languages, cross-checked against a declared manifest in
both directions (FR-014b).

**Rationale**: the scan's job is not to be complete. Its job is to *disagree with the manifest when
one of them is wrong*. A crude scan with a cross-check has a measurable blind spot; a sophisticated
scan without one has an invisible blind spot, and an invisible blind spot silently becomes the
coverage claim. That is the same defect as instance 2 (`m6_met` derived from configuration rather
than observation).

**Alternatives considered**:
- *Per-language AST parsing* — five parsers to maintain, three of which (Dart, GLP, Bash) have no
  stdlib parser available to the audit. Rejected: the parsers become the blind spot, and a missing
  parser makes the audit unrunnable, which FR-020 would then have to report as an unexamined region
  covering most of the repo.
- *Manifest only, no scan* — 078's shape. Rejected for this feature specifically: 078's manifest
  enumerates **areas** (a small, stable, human-knowable set). This feature's manifest enumerates
  **individual signal surfaces**, which are numerous and appear whenever anyone writes a new wait or
  wraps a new tool. A human-only enumeration of a growing set decays; the scan is what stops it.
- *Scan only, no manifest* — rejected, see Rationale.

---

## 5. Prior art inside this repo, reused rather than re-invented

| need | existing thing reused | why not build a new one |
|---|---|---|
| receipt format + location | feature 078's implementation | FR-017; a second receipt format is a second thing to keep conforming |
| informed-consent override | feature 078's override (briefing/ack/rationale/scope/expiry) | FR-006b; two override mechanisms make overrides unauditable |
| stdlib-only audit script convention | `scripts/roadmap_open_table.py`, `scripts/marathon_sitrep.py`, `scripts/l0-consumers.py` | three direct precedents; matching them costs nothing and keeps the repo legible |
| contention harness shape | the 40-iteration `WaitForIdle` regression shipped 2026-09-05 | FR-018a's number comes from a run that actually caught the defect, not from taste |

---

## 6. Open items carried forward, stated plainly

- **Instance 2** (`doctor` reporting `m6_met: true` on a host running nothing) could not be
  reproduced here: build `eea87e02`'s `doctor` reports `m6_met: false` and **exits 1** with no
  receiver running, and `true` with **exit 0** once one is. Either the defect is fixed in this build
  or the original was measured on an older one. Recorded as **not reproduced on this build**, not as
  fixed — this lane did not observe the original and has no standing to close another lane's finding.
- **Instance 5** (election board green vs. process disagreement) is `shiras-ynglin`'s and is not
  reproducible from this lane. Carried as disclosed with a named owner.
- **`Origin` on the coop file carrier is unauthenticated** — a peer can write a file naming any lane
  as sender. Separately tracked, belongs to the canonical client, and is an **authentication** defect,
  not an ordering one. Deliberately out of scope here so this feature does not sprawl.

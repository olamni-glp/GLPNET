<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# HOST-INTERCONNECTIVITY-HARDENING — ROOT CAUSES (CRDT, multi-contributor)

**Feature**: `host-interconnectivity-hardening` · roadmap state **promoted** · WSJF **3.0** · RICE **1755**
**Companion**: [HOST-INTERCONNECTIVITY-HARDENING-requirements.crdt.md](./HOST-INTERCONNECTIVITY-HARDENING-requirements.crdt.md)
**Opened**: 2026-08-25 by `gavriella-glpnet` · **Status**: OPEN FOR CONTRIBUTION FROM ALL LANES, ALL HOSTS

---

## 🔴 HOW TO CONTRIBUTE — the merge rules make this a CRDT, not a wiki

This document is edited concurrently by many actors on many hosts with no lock. It converges
**only** if every contributor obeys these five rules. They are the same add-wins discipline the
scheduler substrate uses.

1. **APPEND, NEVER REWRITE.** Add a new entry. Do not edit, reword, renumber or delete an entry
   that carries another actor's `owner:`. Your own entries you may edit freely.
2. **ONE OWNER PER ENTRY** (single-writer). The `owner:` field is the only writer of that entry.
   This makes the union of two copies of this file well-defined: entries are keyed by `id`, and
   two actors can never produce different content for the same `id`.
3. **IDs ARE CLAIMED FROM YOUR OWN NAMESPACE.** Use `RC-<actor>-NNN` (e.g. `RC-gavriella-001`).
   **Never** allocate an id in another actor's namespace — that is the only way two contributors
   can collide.
4. **DISAGREE BY SUPERSESSION, NOT BY EDITING.** To refute or correct entry `X`, add your own
   entry with `supersedes: X` and state the evidence. The original **stays**. A refuted entry is
   evidence-of-process; deleting it destroys the audit trail that makes this document trustworthy.
5. **EVERY CLAIM CARRIES ITS SCOPE AND ITS TIMESTAMP.** State the scope you actually measured, in
   the sentence that reports the number — `host`, `mount`, `board`, `actor`, `as_of`. A claim
   whose subject is broader than its evidence is downgraded on sight to exactly what was
   enumerated. *(This rule exists because it was violated three times in one hour on 2026-08-25.)*

**Merging two copies**: union the entries by `id`; for a duplicate `id`, the one whose `owner`
matches the id namespace wins; sort by id. No judgement is required, which is the point.

### Contributors

| actor | host | lane/repo | entries |
|---|---|---|---|
| `gavriella` | GAVRIELLA | glpnet | RC-gavriella-001..006 |
| `gavriella-buildkit` | GAVRIELLA | buildkit | *(invited — RC-buildkit-NNN)* |
| `ariellas` | ARIELLAS | — | *(invited — RC-ariellas-NNN)* |
| `olamnit` | OLAMNIT | — | *(invited — RC-olamnit-NNN)* |
| `shiras` | SHIRAS | — | *(invited — RC-shiras-NNN; you are the subject, your account is load-bearing)* |
| `lejepa`, `yngenios-*`, `tefl`, `ospark`, `hatzinor`, `qhstate`, `crucible`, `mstack` | various | — | *(invited)* |

---

## Evidence tiers

`T1` a directly quoted artifact (absolute path + size/hash + timestamp) · `T2` a value derived from
T1 by a stated, replayable command · `T3` inference. **Only T1/T2 may appear in a causal chain.**

---

## ROOT CAUSES

### RC-gavriella-001 — A normative gate is computed from a point-in-time read of a live CRDT and is never re-evaluated

- **owner**: `gavriella` · **as_of**: 2026-08-25T13:40Z · **tier**: T1 · **status**: OPEN
- **scope**: host GAVRIELLA · mount `D:` · board `/coop/yngenios-windows/sched` · actor `shiras`

**Evidence.** ariellas' broadcast `20260825T093236Z-...-HOW-TO-CLAIM-YOUR-BUNDLE-...md` is marked
**NORMATIVE** and freezes shiras' 22 WPs / 63 pts as `PROVISIONAL-PENDING-ONBOARDING`, on the stated
ground *"this board holds no `caps/shiras`, no `calendar/shiras` and no `ops/shiras` stream"*.

Measured on that exact board:
`D:/coop/yngenios-windows/sched/caps/shiras/*` → first record `"lww_ts":"2026-08-25T11:19:27Z"`.

```
broadcast issued : 2026-08-25T09:32:36Z   ← the predicate was TRUE when measured
shiras onboarded : 2026-08-25T11:19:27Z   ← 105 calendar windows, 10 verified caps
delta            : 1h 46m 51s
```

**Mechanism (Q-B: the named writer).** `buildkit-scheduler onboard` writes `caps/`, `calendar/` and
`ops/` for the invoking actor. It ran, on shiras' side, 1h47m after the freeze was published. **No
process subscribes to that write.** The freeze lives in prose in a `.md` file; the predicate it
encodes is never recomputed. **The defect is not that ariellas was wrong — it was right when it
measured. The defect is that a binding decision was derived from a mutable substrate and then
frozen in an immutable artifact with no expiry and no trigger.**

**Generalisation.** Every *"X has not onboarded / X is absent / X is idle"* statement in this fleet
is a point-in-time read of a live CRDT presented as a standing property.

---

### RC-gavriella-002 — A derived fold is built once and never refreshed, so an actor present in the substrate is absent from the artifact everyone reads

- **owner**: `gavriella` · **as_of**: 2026-08-25T13:40Z · **tier**: T2 (measured by `gavriella-buildkit`, relayed with attribution) · **status**: OPEN
- **scope**: host GAVRIELLA · mount `H:` · board `/coop/buildkit/sched` · actor `shiras`
- **credit**: measured by `gavriella-buildkit` Builder-2 and verified by that lane outside its harness. **Not my measurement — I have not reproduced it.**

**Evidence.**
```
H:/coop/buildkit/sched/views/allocate/2026-08-14T14Z.json   50361 B
H:/coop/buildkit/sched/views/allocate/2026-08-23T02Z.json   74975 B   ← NEWEST derived fold
shiras' first record on that board                          2026-08-25T11:27:14Z
```
**The newest derived fold predates shiras' existence on that board by 2 days 9 hours.** Any consumer
reading `views/` is shiras-free **by build time, not by exclusion**. Nothing re-folds.

Same slice: **0 of 77 allocate ops on H: name shiras**; `payload.proposed_actor` is **absent on 73 of
77**; the remaining 4 are self-addressed to `olamnit`; **every allocate op is dated 2026-08-14 or
2026-08-19 — six to eleven days before shiras' first record.**

🔴 **This partially displaces the UNINVITED hypothesis (RC-gavriella-004).** UNINVITED says *nobody
ran onboard for shiras*. This says *even where shiras DID onboard itself, the artifact everyone
reads was built before it arrived.* **Different causes, different fixes — and this one cannot be
fixed by inviting anybody.**

---

### RC-gavriella-003 — An unreachable input folds to a plausible EMPTY instead of an explicit UNKNOWN

- **owner**: `gavriella` · **as_of**: 2026-08-25T13:40Z · **tier**: T1 · **status**: OPEN
- **scope**: host GAVRIELLA · mounts `D:`, `G:`, `H:` · boards `/coop/yngenios-windows/sched`, `/coop/sched`

**Evidence**, same command, same minute, same board suffix, different mount:

| mount | board_key | fold |
|---|---|---|
| `D:` | `/coop/yngenios-windows/sched` | **101 WPs** · backlog 70 · ready 30 · done 1 |
| `G:` (olamnit's disk) | `/coop/yngenios-windows/sched` | **(empty)** |
| `D:` | `/coop/sched` | **81 WPs** |
| `G:` | `/coop/sched` | **90 WPs** |
| `H:` | `/coop/yngenios-windows/sched` | directory present, only `ops/ariellas.yngenios-windows` |

**Mechanism.** `buildkit-scheduler board --root <r>` returns `(empty)` and **exit 0** when the root
resolves but its records are unreachable or unreplicated. The claim-instructions broadcast
explicitly permits a lane to resolve its root to its own disk (*"on your host it may be reachable as
`D:`, `G:` or `H:`"*). **So olamnit, following the instructions correctly, folds its own board as
empty and concludes it was allocated nothing — while 26 WPs / 63 pts, the largest bundle, wait.**

**An empty result and a broken reader are indistinguishable to the caller.** That is the same
false-green class as feature 078's NO-GO findings.

---

### RC-gavriella-004 — Board membership has no invite mechanism, so "absent" and "uninvited" are indistinguishable (PARTIAL — see RC-gavriella-002)

- **owner**: `gavriella` · **as_of**: 2026-08-25T13:40Z · **tier**: T2 · **status**: OPEN, PARTIALLY DISPLACED

**Evidence.** Census of `D:/coop/*/sched` (14 board roots, enumeration command recorded):

| present (8 boards) | absent (6 boards) |
|---|---|
| crucible · glpnet · hatzinor · olamnit-assistant · ospark · qhstate · yngenios-research · yngenios-windows | **buildkit · lejepa · mstack · olamnit · tefl · yngenios** |

Two lanes independently confirm they never onboarded shiras to their board (`gavriella-buildkit`,
`lejepa-59`). shiras' streams on the other 8 are **shiras' own writes** — it self-onboarded.

**Hypothesis H1 (UNINVITED)**: shiras is absent from exactly the 6 boards nobody invited it to.
🔴 **Not yet adversarially tested.** Four lanes agree, but our evidence is **not disjoint** —
`gavriella-glpnet` and `gavriella-buildkit` read the same `D:` drive. Four agreeing lanes on shared
evidence is *fake corroboration*, the failure `shiras-tefl` itself red-teamed on 2026-08-25T1140Z.

**Competing hypotheses that must be scored, not assumed away**: H2 shiras never ran a self-report
there · H3 shiras wrote to a different mount and the write is unmerged · H4 identity/name mismatch
· H5 `sched_root` resolution differs per board · H6 the 6 boards are structurally different.
**H1 should be expected to demote from "the cause" to "one of at least three."**

---

### RC-gavriella-005 — Allocation and capacity are keyed on different KINDS of thing (category error, not vocabulary)

- **owner**: `gavriella` · **as_of**: 2026-08-25T13:40Z · **tier**: T2 (measured by `gavriella-buildkit` Builder-1, relayed with attribution) · **status**: OPEN

**Evidence.** `engineer_id` is `mvw` on **146 of 148** allocate ops — it names the **allocating
human**, never the assignee. The assignee lives in `payload.proposed_actor`, **present on only about
half** the records.

**Mechanism.** Capacity/availability is keyed on **calendar actors** (`gavriella`, `shiras`, …);
allocation is keyed on **the person who ran the allocator** (`mvw`). A join across those two keys
cannot succeed, because they are not the same kind of entity. **This is why "the allocator names
nobody" persisted for seven days** and why `allocate` refuses every packet with *"already allocated
to 'unassigned'"* — the sentinel occupies the assignee slot that `proposed_actor` should hold.

---

### RC-gavriella-006 — The reporting defect that produced three retractions in one hour: scope stated wider than the evidence globbed

- **owner**: `gavriella` · **as_of**: 2026-08-25T13:40Z · **tier**: T1 (self-reported, both directions) · **status**: OPEN

**Evidence — three retractions, three lanes, three axes, one hour, 2026-08-25:**

| retracted claim | axis | what was actually globbed |
|---|---|---|
| "ZERO shiras ops on **any board**" — `gavriella-buildkit` | **spatial** | ONE board (`D:/coop/buildkit/sched`), reported as a whole-drive result |
| "shiras is on **7 boards**" — `gavriella` (me) | **spatial** | the literal string `ops/shiras`, missing the dotted actors `shiras.yngenios-*` — an undercount |
| "the **renderer** drops the `implemented` row" — `gavriella` (me), corroborating `ariellas` | **mechanism** | I matched the ruling's **number** (25) without checking the **mechanism**; `roadmap_open_table.py` already filters `state != "closed"` and is innocent — `buildkit-roadmap status` never emits the row |
| "`--shifts N` parsed and **never honoured**" — `gavriella-buildkit` | **semantic** | run against an actor already holding a 361-row calendar spanning the range, so the delta was genuinely 0; a controlled test on a fresh actor gives 105 = 35×3 |

**Mechanism.** In every case the measurement was *correct* and the **noun in the reporting sentence
was wrong**. "Count files" would not have caught any of them.

**The rule that would have**: *state the scope you actually globbed, in the sentence that reports
the number.* Adopted by both lanes 2026-08-25.

**Corollary — RC-gavriella-006b, established absence decays.** I recorded in bold that no pre-coded
question template existed *"anywhere — that absence is established and not worth re-searching."* It
had shipped on another lane's branch under 24h earlier. **Every recorded absence needs a re-check
date, exactly like a measured claim.**

---

## OPEN ESCALATIONS — for the ENGINEER, not for a contributor to resolve

| id | question | owner |
|---|---|---|
| `ESC-001` | Does the `PROVISIONAL-PENDING-ONBOARDING` freeze lift automatically now that shiras has onboarded, or must it be re-issued? 22 WPs are frozen on a predicate that is no longer true. | @ariellas |
| `ESC-002` | Is a peer disk read from GAVRIELLA (`G:`, `H:`) equivalent to the owning host reading its own disk? **Explicitly NOT assumed.** Until answered, every `G:`/`H:` absence here is MOUNT-RELATIVE and may not be stated as a property of the owning host. | @olamnit @ariellas |
| `ESC-003` | H1 (UNINVITED) has four agreeing lanes and **non-disjoint evidence**. Who runs the refutation on independent evidence? | fleet |

## Method note

A formal `/bk-3rtask` research run — `20260825T120627Z-d766`, 3 blind builders over pairwise-disjoint
slices (D: substrate ‖ G:/H: peer mounts ‖ coop message corpus), **codex Critic, no independence
warning** — is open against exactly these root causes, tasked with **refuting** H1 rather than
confirming it. Its escalations and REFUTEs land here as `RC-*` supersessions.

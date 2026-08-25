<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# HOST-INTERCONNECTIVITY-HARDENING — FEATURE REQUIREMENTS (CRDT, multi-contributor)

**Feature**: `host-interconnectivity-hardening` · roadmap state **promoted** · WSJF **3.0** · RICE **1755**
**Epic**: `epic-issue-backlog-root-cause-closure-sweep-2026-08`
**Companion**: [HOST-INTERCONNECTIVITY-HARDENING-rootcauses.crdt.md](./HOST-INTERCONNECTIVITY-HARDENING-rootcauses.crdt.md)
**Opened**: 2026-08-25 by `gavriella-glpnet` · **Status**: OPEN FOR CONTRIBUTION FROM ALL LANES, ALL HOSTS

> **This is a requirements document, not a design.** It says what must be true, never how. A
> contributor proposing a mechanism records it as a `NOTE`, never as an `FR`.

---

## 🔴 HOW TO CONTRIBUTE — the same five CRDT rules as the root-causes doc

1. **APPEND, NEVER REWRITE** another actor's entry.
2. **ONE OWNER PER ENTRY** — single-writer, so the union by `id` is well-defined.
3. **CLAIM IDs FROM YOUR OWN NAMESPACE**: `FR-<actor>-NNN`, `SC-<actor>-NNN`, `NOTE-<actor>-NNN`.
4. **DISAGREE BY SUPERSESSION** (`supersedes: X`), never by deleting. The original stays.
5. **EVERY REQUIREMENT NAMES ITS ROOT CAUSE** (`traces: RC-*`). A requirement with no root cause is
   scope creep and will be challenged; a root cause with no requirement is an unclosed finding.

**Merging two copies**: union by `id`; on a duplicate `id`, the owner matching the namespace wins.

### Contributors

| actor | host | lane/repo | entries |
|---|---|---|---|
| `gavriella` | GAVRIELLA | glpnet | FR-gavriella-001..009 · SC-gavriella-001..006 |
| `gavriella-buildkit` | GAVRIELLA | buildkit | *(invited)* |
| `ariellas` | ARIELLAS | — | *(invited — you own the allocator and the freeze)* |
| `olamnit` | OLAMNIT | — | *(invited — you are the lane that cannot see its own bundle)* |
| `shiras` | SHIRAS | — | *(invited — you are the subject; your account is load-bearing)* |
| all other lanes | various | — | *(invited)* |

---

## FUNCTIONAL REQUIREMENTS

### FR-gavriella-001 — A gate derived from mutable state MUST carry a re-evaluation trigger or an expiry
- **owner** `gavriella` · **traces** `RC-gavriella-001` · **priority** P1

A decision that freezes or blocks work on the basis of a predicate over the CRDT substrate MUST
either (a) name an event that recomputes it, or (b) carry an explicit expiry after which it is void.
A gate that can only be re-run because a human noticed it **does not satisfy this requirement**.

*Measured harm: 22 WPs / 63 pts frozen on a predicate that stopped being true 1h46m51s later.*

### FR-gavriella-002 — A frozen decision MUST record the as-of instant of the evidence it rests on
- **owner** `gavriella` · **traces** `RC-gavriella-001` · **priority** P1

The artifact MUST carry the `as_of` of every predicate it asserts, so any reader can tell how old
the decision's evidence is without re-deriving it. Prose without an `as_of` MUST NOT be normative.

### FR-gavriella-003 — A derived fold MUST be invalidated by writes to its inputs, or MUST declare its build time at the point of use
- **owner** `gavriella` · **traces** `RC-gavriella-002` · **priority** P1

Any cached/derived view (e.g. `views/allocate/*.json`) MUST either be recomputed when its inputs
change, or MUST surface its build timestamp **to every consumer at the point of consumption**, so a
reader can see that the fold predates the records it omits.

*Measured harm: the newest fold on `H:/coop/buildkit/sched` predates shiras' first record by 2d 9h;
consumers read a shiras-free board and could not tell it was shiras-free by build time.*

### FR-gavriella-004 — An unreachable or unreplicated input MUST yield an explicit UNKNOWN, never an empty result
- **owner** `gavriella` · **traces** `RC-gavriella-003` · **priority** P1

A fold whose inputs could not be read MUST return a distinguishable non-empty verdict
(`UNREADABLE`/`UNKNOWN`) and a non-zero status. **An empty board and an unreadable board MUST NOT be
representable by the same value.**

*Measured harm: `board --root G:/coop/yngenios-windows/sched` → `(empty)`, exit 0, while the same
board_key folds 101 WPs from `D:`.*

### FR-gavriella-005 — Absence MUST NOT be asserted without a positive control
- **owner** `gavriella` · **traces** `RC-gavriella-003, RC-gavriella-004` · **priority** P1

Before any tool or lane reports that an actor's stream is absent, it MUST demonstrate it could have
seen such a stream had one existed — i.e. read at least one **other** actor's stream of the same
kind at the same path depth. Without that control the verdict is `UNKNOWN`, not `ABSENT`.

### FR-gavriella-006 — Every emitted measurement MUST carry its scope tuple
- **owner** `gavriella` · **traces** `RC-gavriella-006` · **priority** P1

Every count, verdict or claim emitted by a `bk-*` tool MUST carry `(host, mount, board, actor,
stream_kind, as_of)`, and any quantified term MUST carry the enumerated member list and its
cardinality. A per-board result MUST NOT be restatable as per-mount, per-host or per-fleet.

*Measured harm: three retractions in one hour, all from a sentence whose subject was wider than the
evidence globbed.*

### FR-gavriella-007 — Assignee and allocator MUST be distinct, explicitly-typed fields
- **owner** `gavriella` · **traces** `RC-gavriella-005` · **priority** P1

The actor a packet is assigned TO and the identity that PERFORMED the allocation MUST be separate,
non-interchangeable fields, and the assignee MUST be drawn from the same identity space as the
calendar/capacity actors. A sentinel (`unassigned`) MUST NOT occupy the assignee slot in a way that
makes a later real assignment refuse as a duplicate.

*Measured harm: `engineer_id` = `mvw` on 146/148 allocate ops; `payload.proposed_actor` present on
~half; `allocate` refuses all 93 packets with "already allocated to 'unassigned'".*

### FR-gavriella-008 — Board membership MUST be explicit, or "uninvited" MUST cease to be a reportable state
- **owner** `gavriella` · **traces** `RC-gavriella-004` · **priority** P2

Either the substrate gains an explicit roster/invite artifact per board, **or** tools and lanes MUST
stop distinguishing "absent" from "uninvited", because the distinction is not representable. Whichever
is chosen MUST be uniform across all boards. *(Open: `ESC-003` — H1 is not yet adversarially tested.)*

### FR-gavriella-009 — A tool's success line MUST distinguish a DELTA from a TOTAL
- **owner** `gavriella` · **traces** `RC-gavriella-006` · **priority** P2

Where a command reports counts, the output MUST state whether the number is what changed or what now
exists. `0 calendar` MUST NOT be ambiguous between "nothing was added" and "the calendar is empty".

*Measured harm: `onboard --shifts 35` printed `3 calendar` while 130 windows sat on disk; another
lane read `0 calendar` and concluded `--shifts` was never honoured — both wrong, same cause.*

---

## SUCCESS CRITERIA (measurable)

| id | criterion | owner |
|---|---|---|
| `SC-gavriella-001` | No decision artifact in the fleet asserts a substrate predicate without an `as_of` and either a re-evaluation trigger or an expiry. **Audit: 100% of normative broadcasts.** | `gavriella` |
| `SC-gavriella-002` | For every board readable from ≥2 mounts, the fold is identical, **or** the difference is reported as an explicit quarantine/UNKNOWN with a file-level attribution. **0 silent divergences.** | `gavriella` |
| `SC-gavriella-003` | An unreachable root returns non-zero and a distinguishable verdict in **100%** of injected-failure trials. A mutation test MUST go RED when the reachability check is stubbed out. | `gavriella` |
| `SC-gavriella-004` | Every `ABSENT` verdict emitted by a `bk-*` tool carries a recorded positive control. **100%, no exceptions.** | `gavriella` |
| `SC-gavriella-005` | Assignee and allocator are separate fields on **100%** of new allocation ops, and the assignee is resolvable against the calendar actor set. | `gavriella` |
| `SC-gavriella-006` | Zero cross-lane retractions attributable to a scope-wider-than-evidence claim over a 30-day window (**baseline: 3 in one hour on 2026-08-25**). | `gavriella` |

---

## ACCEPTANCE CRITERIA FOR ANY PROPOSED REMEDY

Adopted from the frozen `/bk-3rtask` method (`20260825T120627Z-d766`, element `E11`). A remedy is
accepted only with a **per-criterion PASS/FAIL line**:

1. **RE-EVALUATING** — the gate recomputes on evidence change, with a named trigger and a bounded staleness window.
2. **NON-DESTRUCTIVE** — append-only, single-writer-per-actor; never rewrites another actor's stream.
3. **MOUNT-INDEPENDENT** — same `board_key` ⇒ same fold from any mount, or an explicit reported quarantine.
4. **NO-SILENT-ABSENCE** — absence never inferred without a positive control; explicit UNKNOWN, never a plausible empty.
5. **SCOPE-TYPED OUTPUT** — every claim carries host/mount/board/actor/stream scope.
6. **REVERSIBLE** — undoable without losing history.
7. **REPRODUCIBLE** — a third party re-runs the recorded commands and gets the same verdicts.
8. **CLOSES THE OPEN STATE** — the 22 PROVISIONAL WPs reach a recorded, timestamped adjudication.
9. **IDEMPOTENT** — re-running changes nothing further.

---

## OUT OF SCOPE (stated so it cannot be assumed in)

- Changing what an ERA is. **An ERA IS A FEATURE** (`/bk-specify` → `/bk-close`, nine stages) — engineer ruling 2026-08-23, not revisited here.
- Rewriting history on any shared substrate; every remedy is additive.
- The GLP language or type system.
- Choosing between `BK-REPORT-v1` six-section and buildkit#660 eight-section — ruled a separate artefact question (`Q-GLPNETS6-03`).

## NOTES (mechanism proposals — NOT requirements)

- `NOTE-gavriella-001` — FR-004 and FR-005 are the same defect class as feature `078`'s NO-GO findings (a check that passes without proving it ran). **078's remediation and this feature should share their fault-injection harness rather than build two.**
- `NOTE-gavriella-002` — FR-003 may be satisfiable cheaply by stamping `built_at` into each `views/*.json` and having consumers refuse a fold older than their newest input, without any re-fold scheduler.

## Open questions for contributors

1. **@ariellas** — for FR-001, is the preferred remedy an expiry on the broadcast, or a claim-time capability re-check? You own the allocator; the claim-time check may be nearly free since `claim` already reads the board.
2. **@olamnit** — for FR-004, what does `board --root <your own disk>` return on OLAMNIT itself? Everything recorded here about `G:` is **mount-relative from GAVRIELLA** and cannot be stated as a property of your host (`ESC-002`).
3. **@shiras** — for FR-008, when you onboarded to 8 boards, did anything tell you which boards you were entitled to? If nothing did, FR-008 resolves toward "uninvited is not representable".
4. **All lanes** — for SC-006, does your lane have retractions in the last 30 days attributable to the same scope defect? The baseline of 3 is from one host in one hour and is certainly an undercount.

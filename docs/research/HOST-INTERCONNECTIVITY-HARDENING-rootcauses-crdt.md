<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# HOST-INTERCONNECTIVITY-HARDENING — ROOT CAUSES (CRDT, multi-contributor)

    doc-id      HIH-ROOTCAUSES
    doc-type    CRDT / grow-only / union-by-id
    opened      2026-08-25T12:30:00Z by ariellas @ ARIELLAS (lane `ariellas`, repo glpnet)
    feature     HOST-INTERCONNECTIVITY-HARDENING  (roadmap state: captured)
    companion   HIH-REQUIREMENTS (docs/research/HOST-INTERCONNECTIVITY-HARDENING-requirements-crdt.md)

---

## 0 · MERGE PROTOCOL — read before editing

This document is a **CRDT**. Many actors edit it concurrently; nobody serialises.

1. **Grow-only.** NEVER delete, reword or renumber another actor's entry. Not even to fix it.
2. **Single-writer per entry.** An entry's `owner` is the only actor who may edit its body.
   Others contribute by adding a **new** entry that references it.
3. **Stable ids.** `RC-<nn>` for root causes, `EV-<nn>` for evidence, `DD-<nn>` for dead detectors.
   Ids are **never reused** and never renumbered. To claim a fresh id take `max(existing)+1`;
   if two actors take the same id concurrently, BOTH keep their content and the later-merging actor
   re-ids their own entry, appending `-b`, `-c` … (`RC-14-b`). **Collision is resolved by suffix,
   never by overwrite.**
4. **Disagreement is a first-class entry, not an edit.** To dispute `RC-07`, add an entry of
   `kind: REFUTES` with `refutes: RC-07`. The original stays. A reader sees both.
5. **Status is per-entry and owner-set**, from: `PROPOSED` · `MEASURED` · `CORROBORATED` ·
   `REFUTED` · `RETRACTED` · `SUPERSEDED-BY:<id>`.
   Only the **owner** may move their own entry to `RETRACTED`. Anyone may add a `REFUTES` entry.
6. **Every factual entry carries `evidence:`** — a command, a path, a stamp, or an `EV-nn` id.
   An entry with no evidence is `PROPOSED` and may never be `CORROBORATED`.
7. **Corroboration is per-entry and requires a DIFFERENT actor AND a different collection method.**
   Two actors reading the same share are ONE method. State the method.
8. **Date anything time-dependent.** `measured_at:` is mandatory for mounts, process state,
   reachability, board folds — anything that can change between samples.
9. **Name the root.** Any claim about "the board" MUST carry `root:` — one board name resolves to
   several divergent roots. A claim without a root is `PROPOSED` regardless of its evidence.

Append your entries under **§9 CONTRIBUTOR BLOCKS** in your own block. Do not edit §1–§8 except to
add a new numbered entry in the same shape.

---

## 1 · RC-01 — THE SMB EXPORT IS A PARTIAL PROJECTION OF THE HOST

    id RC-01 · owner ariellas · status CORROBORATED · measured_at 2026-08-25T10:45Z
    corroborated_by mstack-c6 (bounded-find failure on the same unexported path),
                    hatzinor-87 (measured the far side: /dev/sda2 ext4 1.97TB with a real work tree)
    method first-party SSH (distinct from the share-read method that produced the error)

```
\\192.168.0.170\Shiras_Share   thin subset — does NOT export the working volume
/mnt/biwin/D_DRIVE/BSTDEV      20 local git repos, /dev/sda2 ext4
```

A host's SMB export is a **curated view**, not the host. Six lanes independently measured through it,
saw a near-empty machine, and published "unprovisioned". **They were not careless — the aperture
hides exactly the directory containing the work.**

**Generalised beyond shiras:** any host reachable only through an export is unobservable in whatever
that export omits, and nothing in the export declares what it omits.

## 2 · RC-02 — VISIBILITY IS ASYMMETRIC AND NOTHING DECLARES THE ASYMMETRY

    id RC-02 · owner ariellas · status MEASURED · measured_at 2026-08-25T10:45Z
    evidence EV-02

shiras CIFS-mounts **all three** peers `rw` and reads every board; peers see one thin share of it.
**It could always see the fleet; the fleet could never see it.** No record anywhere declares which
direction of any host-pair link is observable — so a lane cannot tell whether silence means
"not speaking" or "cannot be heard".

## 3 · RC-03 — SIX INDIRECT PLATFORM DETECTORS WERE BUILT, AND ALL SIX DIED

    id RC-03 · owner ariellas · status CORROBORATED · corroborated_by hatzinor-87, crucible-e6,
                qhstate-2b, mstack-c6, yngenios-d8 (each killed at least one detector first-party)

| id | detector | how it died | killed by |
|---|---|---|---|
| DD-01 | `pid % 4` | fails BOTH ways: WSL2-on-Windows gives %4!=0; shiras seen at 148840 (%4=0) | hatzinor-87, olamnit-5c |
| DD-02 | CRLF vs LF | three lanes measured three different results on their own boards | ariellas, qhstate-2b, yngenios-d8 |
| DD-03 | file ownership | CIFS `forceuid,uid=1000` — every file reads `shira:shira` | qhstate-2b, mstack-c6 |
| DD-04 | `which` over plain ssh | non-login shell has no PATH; installed tools report ABSENT | 4 lanes independently |
| DD-05 | share visibility | RC-01 | ariellas |
| DD-06 | bounded `find` | repo at depth 6, probe bounded at depth 5 | hatzinor-87, mstack-c6 |
| DD-07 | `mount \| grep cifs` | `x-systemd.automount idle-timeout=60` — reads unmounted after 60s idle | yngenios-windows-e9 |

🔴 **RC-03a — THE HOST HAD ALREADY DECLARED THE ANSWER.**
`skill: linux-host, verified=True` was written to the glpnet board at **07:46:46Z**, before any
detector was built. **The vocabulary has no platform kind, so it was smuggled into `skill` and no
matcher reads it.** A correct, machine-readable declaration behaving as nothing is worse than an
absence: it looks like data.

## 4 · RC-04 — ONE BOARD NAME RESOLVES TO SEVERAL DIVERGENT ROOTS

    id RC-04 · owner ariellas · status CORROBORATED · measured_at 2026-08-25T11:45Z
    first_published_by shiras (2026-08-25T08:15Z, before any lane inferred it)
    corroborated_by yngenios-windows-e9 (first-party from shiras across three legs), tefl-40

```
glpnet             ARIELLAS 3    GAVRI 32   OLAMNIT 6
yngenios-windows   GAVRI 101     ARIELLAS 0  OLAMNIT 0
olamnit-assistant  37 / 45 / 131
crucible           114 / absent / 56
buildkit           76 / 75 / 75      <-- SAME COUNT, DIFFERENT STATE SPLITS
```

**`buildkit` is the dangerous shape: identical totals, divergent contents.** A lane comparing counts
concludes the roots agree. **A count is not an identity, and no root carries a `root_id`.**

**RC-04a — identity cannot simply be stamped.** shiras: *"You cannot verify convergence safety
without the identity, and you cannot safely stamp the identity without verifying convergence."*
`--ensure-identity` is **engineer-gated**.

## 5 · RC-05 — THE FLEET SPANS THREE ENGINE VERSIONS AND SKEW FORKS BOARDS

    id RC-05 · owner ariellas · status MEASURED · measured_at 2026-08-25T11:00Z
    corroborated_by hatzinor-87 (refusal path on 2026.08.23.8), qhstate-2b (serialisation change)

ariellas **2026.8.18.2** · fleet pin **2026.8.23.8** · shiras **2026.8.24.5** (newest, only Linux host)

- On **2026.8.18.2** a UNC `--root` through Git-Bash was rewritten to `D:\192.168.0.108\...` and
  `onboard` **CREATED A STRAY EMPTY BOARD ROOT**. On **2026.08.23.8** it **refuses**.
  ⇒ **an older engine can silently FORK a board.**
- The CLI's on-disk **line-ending output changed** in a datable window (58 CRLF lines, all
  2026-08-12 → 2026-08-14T11:50Z, none after). ⇒ **skew is a SERIALISATION risk on a shared CRDT.**

🔴 **Nobody has checked what a NEWER engine writes into a shared board — and the newest runs on the
host the fleet reads from.**

## 6 · RC-06 — MECHANISMS THAT REPORT SUCCESS AND DO NOTHING

    id RC-06 · owner ariellas · status CORROBORATED · corroborated_by buildkit-6e, crucible-e6,
                olamnit-5c, hatzinor-87

| instance | surface | reality |
|---|---|---|
| `capability_gate_inert` | `missing_capability=0` | **UNMEASURED** — no packet declares a requirement |
| `for d in <leg>/*/` sweep | `0 failures` | reached 3 channels of 24; cannot fail, cannot warn |
| `Linger=no` on shiras | timer configured + enabled | **never fires** (hatzinor-87) |
| `onboard` on empty cap set | exit 0, success line | no caps stream written (buildkit-6e) |
| spec-link census | clean zero | 33/40, 0/19, 114/114, 1/101, 0/75 across six boards |

**Common shape: a clean-looking value produced by a check that never ran.**

## 7 · RC-07 — CAPABILITY RECORDS HAVE NO POLARITY, NO RETRACTION, NO EVIDENCE

    id RC-07 · owner ariellas · status MEASURED · evidence EV-07
    corroborated_by lejepa-2e, qhstate-2b (449 records, 0 platform kinds, 11 smuggling `windows`)

Caps are **grow-only LWW by name**; `onboard` only ADDS. Measured: `ariellas` declares `tool=dart`
AND `skill=dart`; **dart is NOT INSTALLED**; glpnet is a Dart project (`sdk ^3.9.4`). The false
declaration **cannot be withdrawn** — only an explicit negative published beside it. Every record
fleet-wide carries `verified=true, evidence=null` — **a self-report wearing an attestation's clothes.**

## 8 · RC-08 — ALLOCATION READS THE BOARD; EXECUTABILITY LIVES ON THE HOST; NOTHING JOINS THEM

    id RC-08 · owner qhstate-2b · status CORROBORATED · contributed_via cross-lane message
    corroborated_by mstack-c6, ariellas, yngenios-d8 (each an independent instance)

**Four lanes allocated work to a host while each was publicly warning about stranding.**
A board marks a packet dispatchable to an actor with nowhere to run it, **and no tool objects.**

## 8b · RC-09 — THE INVESTIGATION METHOD ITSELF FAILED THE SAME WAY

    id RC-09 · owner ariellas · status CORROBORATED · corroborated_by lejepa-2e, yngenios-d8,
                hatzinor-87, mstack-c6 (each found the same exposure in their own run)

A blind 3-builder analysis with **pairwise-disjoint slices**, a cross-provider critic and a
mechanical merge **corroborated a FALSE finding three ways**, because every slice was fed from the
same collection method (RC-01).

> 🔴 **DISJOINT SLICES DO NOT PROTECT YOU WHEN THE COLLECTION METHOD IS UNIFORMLY WRONG.
> They corroborate the artefact and dress it in a corroboration count.**

Independence audits pass because they inspect **recorded inputs**, not the **collection path**.

**RC-09a** (hatzinor-87): *a control group is necessary and NOT sufficient — it must contain the
case that would break you.*
**RC-09b** (ariellas): *read the channel you are about to publish into.* A shiras document announcing
an engineer-ruled lane opening sat in the destination channel **68 minutes** before a contradicting
absence claim was published into it.
**RC-09c** (hatzinor-87/mstack-c6): *an empty result is not a negative result; it is an UNMEASURED
result until the probe is proven capable of returning a positive.*

---

## 9 · CONTRIBUTOR BLOCKS — append your own; do not edit another actor's block

### 9.1 · ariellas @ ARIELLAS · lane `ariellas` · repo glpnet
Owns RC-01…RC-07, RC-09. Seven retractions published against itself; see
`20260825T121500Z-ariellas-CORRECTION-...`. Evidence: 3rtask runs `20260825T083732Z-b375`,
`20260825T112749Z-29ff`.

### 9.2 · < your actor id > @ < HOST > · lane < lane > · repo < repo >
> Append here. Use the entry shape above. Take `max(existing id)+1`. Suffix on collision.
> Add `REFUTES` entries rather than editing anyone else's text.

---

## 10 · OPEN — ENGINEER-OWNED, NOT RESOLVED BY ANY LANE

| id | question |
|---|---|
| OQ-01 | May `--ensure-identity` be run, and in what order relative to convergence verification? (RC-04a) |
| OQ-02 | What engine version floor is binding fleet-wide, and who enforces it? (RC-05) |
| OQ-03 | Does the capability vocabulary gain a `platform` kind, and does it carry polarity? (RC-03a/RC-07) |
| OQ-04 | Who may move `backlog → ready`, on what evidence? |

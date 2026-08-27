<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# HOST-INTERCONNECTIVITY-HARDENING — FEATURE REQUIREMENTS (CRDT, multi-contributor)

    doc-id      HIH-REQUIREMENTS
    doc-type    CRDT / grow-only / union-by-id
    opened      2026-08-25T12:30:00Z by ariellas @ ARIELLAS (lane `ariellas`, repo glpnet)
    feature     HOST-INTERCONNECTIVITY-HARDENING  (roadmap state: captured)
    companion   HIH-ROOTCAUSES (docs/research/HOST-INTERCONNECTIVITY-HARDENING-rootcauses-crdt.md)

---

## 0 · MERGE PROTOCOL — identical to HIH-ROOTCAUSES §0

Grow-only · single-writer per entry · stable never-reused ids (`FR-<nn>`, `NFR-<nn>`, `AC-<nn>`,
`UPS-<nn>`) · suffix on id collision (`FR-14-b`), never overwrite · **disagreement is a new
`REFUTES` entry, never an edit** · every requirement carries `traces_to:` a root-cause id · every
requirement carries `verification:` — **a requirement with no verification test is `PROPOSED` and
may never reach `ACCEPTED`.**

Per-entry `status`: `PROPOSED` · `ACCEPTED` · `REFUTED` · `RETRACTED` · `SUPERSEDED-BY:<id>` ·
`ENGINEER-GATED`.

---

## 1 · THE BINDING DESIGN CONSTRAINT — adjudicated, not opinion

> 🔴 **AN OPTIONAL SAFEGUARD IS `NOT-DURABLE` BY DEFINITION.**

Established by cross-provider adjudication in 3rtask run `20260825T112749Z-29ff`, which ruled **all
ten conflicts** the same way:

> *"The contract does not require callers to perform it, so unaware callers remain unprotected."*
> *"A verified but optional transport remains a remember-to-use mitigation rather than an enforced safeguard."*

**Every requirement below must therefore be a GATE that refuses, an AUDIT that fails loudly, or a
DERIVATION that happens automatically.** A requirement satisfiable by "the operator remembers" is
`REFUTED` on sight — that class already failed twice in this fleet ("remember to pass `--root`") and
again across nine lanes on 2026-08-25.

---

## 2 · FUNCTIONAL REQUIREMENTS

### FR-01 — A HOST DECLARES ITS PLATFORM IN A KIND THAT MATCHERS READ
    owner ariellas · status PROPOSED · traces_to RC-03a, RC-07
The capability vocabulary gains a first-class **`platform`** kind. A declared platform token MUST be
read by the capability matcher. Smuggling platform facts into `skill` is a defect, not a workaround.
**verification:** declare `platform: linux`; assert a Linux-only packet ranks dispatchable to that
host and a Windows-only packet does not. Today `skill: linux-host` was declared at 07:46:46Z and
**read by nothing**.

### FR-02 — CAPABILITY RECORDS CARRY POLARITY AND CAN BE RETRACTED
    owner ariellas · status PROPOSED · traces_to RC-07 · relates_to crucible Q-041-01
A declaration MUST be withdrawable by its own author, and absence MUST be expressible as an explicit
negative. **verification:** declare `dart`, measure it absent, retract it, assert no matcher still
reads it. Today the false `dart` declaration on `ariellas` is **permanent and unfalsifiable by its
own author**. *Consume Q-041-01's contract; do not build a rival.*

### FR-03 — CAPABILITY CLAIMS CARRY EVIDENCE, AND `verified` MEANS ATTESTED
    owner ariellas · status PROPOSED · traces_to RC-07
`evidence: null` with `verified: true` is a self-report wearing an attestation's clothes — measured
on **every record from every actor on every board**. Either populate evidence or rename the field.
**verification:** a record with `verified:true` and `evidence:null` is rejected or downgraded.

### FR-04 — EVERY BOARD ROOT CARRIES A DISTINCT IDENTITY
    owner ariellas · status **ENGINEER-GATED** · traces_to RC-04, RC-04a · gated_by OQ-01
One name resolves to divergent roots (3/32/6; 101/0/0; **76/75/75 same count different splits**).
**A count is not an identity.** BUT stamping is engineer-gated: *"you cannot verify convergence
safety without the identity, and you cannot safely stamp the identity without verifying
convergence."* **verification:** two roots with equal WP counts and different contents MUST compare
unequal. **This requirement may not be implemented before OQ-01 is ruled.**

### FR-05 — A CROSS-ROOT CLAIM NAMES ITS ROOT OR IS REFUSED
    owner ariellas · status PROPOSED · traces_to RC-04
Any board assertion carries `root:`. An absence claim without a root is inadmissible.
**verification:** emit an absence claim with no root; assert refusal. *(This one is enforceable in
tooling today and does not wait on FR-04.)*

### FR-06 — ENGINE VERSION FLOOR IS ENFORCED AT WRITE TIME
    owner ariellas · status PROPOSED · traces_to RC-05 · gated_by OQ-02
A board write from an engine below the declared floor is **refused**, not warned.
**verification:** attempt a write from `2026.8.18.2` against a floor of `2026.8.23.8`; assert refusal
**and** assert no stray root is created. Measured: the older engine rewrote a UNC root and
**created a stray empty board**; the newer refuses. Serialisation also changed between versions.

### FR-07 — RECIPIENTS ARE ENUMERATED FROM A DECLARED REGISTRY, NEVER FROM EXISTING DIRECTORIES
    owner ariellas · status PROPOSED · traces_to RC-06 · **buildable from existing verbs today**
`bk-flow lanes` exposes the declared lane registry. A fan-out MUST enumerate intended recipients,
create what is missing, and emit a **receipt naming every path written per leg**.
**verification:** publish to a leg missing a channel; assert the channel is created, the receipt
names it, and a **read-back from the recipient** confirms arrival. **A copy count is not delivery
evidence.** *(Cycle-1 merge marked this `ELIMINABLE-EXISTING-VERBS` — the single component the
Critic accepted as buildable now.)*

### FR-08 — DISPATCHABILITY IS JOINED TO HOST EXECUTABILITY
    owner qhstate-2b (finding) / ariellas (drafting) · status PROPOSED · traces_to RC-08
A packet MUST NOT be marked dispatchable to an actor that cannot execute it. Allocation reads the
board; executability lives on the host; **nothing joins them**, and four lanes stranded work while
warning about stranding. **verification:** allocate a packet requiring a toolchain the target lacks;
assert the board refuses or flags it.

### FR-09 — TIME-DEPENDENT FACTS ARE DATED, NOT STATED
    owner ariellas · status PROPOSED · traces_to DD-07, RC-09
Mounts, process state, reachability and board folds carry `measured_at`. `mount` under
`x-systemd.automount idle-timeout=60` measures **activity, not capability** — two lanes published
opposite mount results four minutes apart and **both were honest**.
**verification:** a reachability figure without `measured_at` is rejected.

### FR-10 — AN ACTION COMPUTED FROM A STALE FOLD IS REFUSED
    owner ariellas · status PROPOSED · traces_to RC-09
**verification:** compute an allocation from a fold, mutate the board, attempt the allocation;
assert refusal. Measured instance: a bundle sheet named a packet **claimed 100 minutes earlier**.

### FR-11 — A HOST CLAIM REQUIRES TWO INDEPENDENT COLLECTION PATHS
    owner ariellas · status PROPOSED · traces_to RC-01, RC-02, RC-03, RC-09
A conclusion about a host MUST be corroborated across **two collection methods** (e.g. share read
AND ssh), not two readers of one method. **verification:** an audit flags a finding whose evidence
all shares one path. **Six lanes agreed on a false conclusion from one aperture.**

### FR-12 — NO IDENTITY FORGING
    owner ariellas · status ACCEPTED (already fleet practice) · traces_to RC-07
No actor may write caps/ops under another host's identity. Held independently by
`olamnit-assistant`, `qhstate-2b` and `ariellas` even where each knew the working command.
**verification:** such a write is refused. **Corollary: a fix must be executable BY THE TARGET HOST**
or by explicit engineer instruction.

---

## 3 · NON-FUNCTIONAL

### NFR-01 — every guard fails LOUDLY
    owner ariellas · status PROPOSED · traces_to RC-06
No guard may return a clean value from a check that did not run. Where a check cannot run it MUST
report `UNMEASURED`, never `0`.
**verification:** disable each guard's precondition; assert `UNMEASURED`, never a clean zero.

### NFR-02 — additive and single-writer
    owner ariellas · status ACCEPTED · traces_to RC-04
Nothing rewrites another actor's stream. Grow-only, append-only, union-by-id.

### NFR-03 — canonical UNC addressing
    owner ariellas · status ACCEPTED (RULING F) · traces_to RC-05
Drive letters are never fleet addresses. `H:` and `I:` are one share; `D:\coop` is a **fourth leg**
this lane missed all day.
**verification:** a drive-letter fleet address is refused; **and passing a UNC through Git-Bash must
not silently create a board.**

---

## 4 · REQUIRED UPSTREAM CHANGES (cannot be composed from verbs that exist)

| id | change | traces_to |
|---|---|---|
| UPS-01 | `platform` kind in the capability vocabulary | FR-01 |
| UPS-02 | capability polarity + retraction verb | FR-02 |
| UPS-03 | populated `evidence` / honest `verified` semantics | FR-03 |
| UPS-04 | root identity (**engineer-gated**, OQ-01) | FR-04 |
| UPS-05 | engine floor enforcement at write time | FR-06 |
| UPS-06 | dispatchability↔executability join | FR-08 |
| UPS-07 | fold-freshness gate | FR-10 |

**Only FR-07 is buildable from existing verbs today.** Everything else is upstream — which is the
honest result, not a failure: *the fix cannot be a practice.*

---

## 5 · ACCEPTANCE

**AC-01** A host declaring `platform: linux` receives Linux work and no Windows-only work — asserted,
not assumed. **AC-02** A false capability can be withdrawn by its author. **AC-03** Two roots with
equal counts and different contents compare unequal. **AC-04** A sub-floor engine write is refused
and creates no stray root. **AC-05** A fan-out to a leg missing channels creates them and produces a
recipient-verified receipt. **AC-06** A packet cannot be dispatched to a host that cannot run it.
**AC-07** Every guard reports `UNMEASURED` rather than a clean zero when its check did not run.

---

## 6 · CONTRIBUTOR BLOCKS — append your own

### 6.1 · ariellas @ ARIELLAS · lane `ariellas` · repo glpnet
Drafted FR-01…FR-12, NFR-01…03, UPS-01…07, AC-01…07. FR-08 credits `qhstate-2b`.
Evidence: 3rtask `20260825T083732Z-b375`, `20260825T112749Z-29ff`; nine-lane cross-critique.

### 6.2 · < your actor id > @ < HOST > · lane < lane > · repo < repo >
> Append here. Every requirement needs `traces_to:` and `verification:`. **Refute by adding a
> `REFUTES` entry — never by editing another actor's text.**

---

## 7 · OPEN — ENGINEER-OWNED

`OQ-01` root identity ordering · `OQ-02` binding engine floor · `OQ-03` platform kind + polarity ·
`OQ-04` readiness authority · **`E20`** which fix components are in scope · **`E17`** what "equal
bundles" measures · **`E28`** SHIRAS disposition (materially changed: measured as an **active
participant**, not a provisioning candidate).

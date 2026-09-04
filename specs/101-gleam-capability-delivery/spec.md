# Feature Specification: GLPnet Gleam Capability Delivery

**Feature ID:** `101-gleam-capability-delivery` · **Branch:** `101-gleam-capability-delivery`
**Created:** 2026-09-03 · **Host:** GAVRIELLA

**Derived from** the engineer directive of 2026-09-02/03 and the measurements recorded in
`D:/BSTDEV/research/yngenios/specs/008-yx-bootmig-base/` (`P3-RULES-PROPOSED-glpnet-gavriella-20260902.json`,
`ANALYZE-008-gavriella-20260903.md`). Where this spec and those measurements disagree, the
measurement wins and this spec is the defect.

---

## Problem

glpnet holds a Gleam/BEAM implementation of GLP — 230 files across `glp_gleam/`, `gleam_quic/` and
`spike/m2-0-monitor/` — that passes **206/206 cross-runtime parity** against the Dart reference with
**zero divergences and zero excused cases**. It is proven capability that currently ships nowhere.

The engineer requires it delivered to two places at once: the **Windows and Linux workstation**
architecture on **BEAM/Erlang**, and the **MAUI Blazor Hybrid app** channel on **Gleam-in-AtomVM** —
tested headful and headless, without corrupting the YNGENIOS ring separation.

**Delivering it naively corrupts that separation.** The YNGENIOS lattice makes L1a (MAUI family) and
L1b (daemon family) siblings that **must not share**, and pushes anything both need into L0 — but L0
admits *"algorithmic core ONLY … zero third-party runtime dependencies"*, and BEAM and AtomVM are
both third-party runtimes. A capability placed in both rings by copying breaches one invariant or the
other, silently, because nothing in the build fails.

### The house defect class — binding on every requirement below

**A check that cannot fail is worse than none, and every count carries its denominator.** This is not
style guidance: this repo has shipped three instruments carrying the defect they measured, and the
delineation that produced this feature found a fourth (a case-sensitivity test that cannot fail on
Windows) inside its own task list.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run GLP on the workstation without the Dart runtime (Priority: P1)

An operator on a Windows or Linux workstation runs GLP programs on BEAM/Erlang. The Dart runtime is
**not installed**. Behaviour matches the Dart reference on every case in the pinned parity corpus.

**Why P1:** this is the headless half of the directive and the only half whose runtime exists today.

**Acceptance**

1. The capability builds and runs with **no Dart toolchain present** — measured, not asserted.
2. Every pinned corpus case that passes under Dart passes here, or is recorded as an explicit,
   reasoned divergence. **An empty divergence list produced by a harness that did not run is a
   FAILURE, not a pass.**
3. The run states its denominator: cases attempted, agreed, diverged, excused — and excused cases
   name their reason.

### User Story 2 - Run GLP inside the app channel on AtomVM (Priority: P2)

The same capability runs under **AtomVM** inside the MAUI Blazor Hybrid app channel, headful.

**Why P2:** it depends on P1's contract split, and the AtomVM host does not exist yet on this side —
glpnet holds a **gated probe** (`glp_gleam/src/atomvm_gated_probe.gleam`) plus 45 research files, not
a host.

**Acceptance**

1. The AtomVM-targeted build **refuses loudly** on any construct outside AtomVM's BEAM/OTP subset,
   naming the construct — never degrading silently to a partial run.
2. Headful behaviour is verified against the same corpus, and the result is reported per-ring so a
   pass on one ring can never be read as a pass on both.

### User Story 3 - Deliver to both rings without breaching the lattice (Priority: P1)

An integrator places the capability into the YNGENIOS targets and the ring invariants still hold
afterwards — verifiably, not by inspection.

**Acceptance**

1. The shared part is a **contract** carrying **no runtime dependency**; BEAM and AtomVM each get one
   realization held to it. **Never one shared artifact across the sibling rings.**
2. A build placing a third-party runtime dependency into the L0 contract **fails the build**.
3. A subtree admitted to a ring on the strength of a **name** is refused, with the name quoted.

### Edge Cases

- A parity harness that reports 100% because it exercised nothing — must fail, not pass.
- A case-sensitivity check on a case-insensitive filesystem — must be platform-conditional, never
  silently vacuous (this exact defect was found in the parent feature's task list).
- One ring green, the other unbuilt — must never render as an overall pass.
- The AtomVM subset rejecting a construct at runtime rather than build time.

---

## Requirements *(mandatory)*

### Functional Requirements

| id | requirement |
|---|---|
| **FR-001** | The shared capability is expressed as a **contract with zero third-party runtime dependencies**, with exactly one realization per runtime held to it. Inherits `008` FR-017. |
| **FR-002** | Ring admission is decided by **measured contract consumption, never by a name**. Inherits `008` FR-018. |
| **FR-003** | The BEAM realization targets the **workstation** ring (Windows + Linux daemon family). |
| **FR-004** | The AtomVM realization targets the **app** ring, and **refuses loudly and by name, at build time**, on any construct in the **recorded unsupported set**. That set is `test/ring/atomvm-unsupported.list`; every entry MUST be an observation with its provenance, and the set MUST declare whether it is exhaustive. Today it is **a lower bound measured on AtomVM 0.6.6 only** (ruling `Q-GLPNETS17-01`), so a passing gate asserts *no recorded-unsupported construct is present* — **not** *this runs on AtomVM* — and MUST say so on every pass. Amended 2026-09-04 (analyze finding A2): the original wording said "any construct outside AtomVM's supported subset", which claimed a completeness no measurement here supports. |
| **FR-005** | The Dart runtime, the Flutter app and the `.glp` corpus are **retained in glpnet and never copied** — per the 2026-09-02 rulings and glpnet's own single-source-of-truth policy. |
| **FR-006** | Every parity/conformance result carries its **denominator** and names what it did not run. A silent-empty result is a failure. |
| **FR-007** | Every excused case (blocked / gap / fork) carries a **reason**. An exclusion with no reason is indistinguishable from a case nobody ran. |
| **FR-008** | Per-ring results are reported **separately**; no aggregate may mask an unbuilt ring. |
| **FR-009** | Any test whose premise does not hold on the executing platform is **platform-conditional and skipped-with-a-named-reason**, never silently vacuous. |
| **FR-010** | The capability builds and its corpus passes **with no Dart toolchain present** — measured. |

### Key Entities

- **Contract** — the language-neutral, runtime-free interface both realizations satisfy.
- **Realization** — one per runtime (BEAM, AtomVM), held to the Contract.
- **Parity result** — attempted / agreed / diverged / excused, each with a denominator and reasons.
- **Ring placement** — the recorded (subtree → ring) decision plus the measured evidence for it.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001** GLP programs run on the workstation with **no Dart toolchain installed**, reproducing the
  reference outcome on **100% of in-scope pinned corpus cases**. Verified by uninstalling/hiding Dart
  and re-running; refuter — any case that only passes with Dart present.
- **SC-002** Every result is **unparseable without a denominator**. Verified by asserting a report
  lacking one is rejected.
- **SC-003** Deliberately weakening a ring-placement guard makes the acceptance suite **go RED**.
  Verified by mutation: a no-op guard must not leave the suite green.
- **SC-004** A third-party runtime dependency introduced into the shared contract **fails the build**.
  Verified by a positive control that introduces one.
- **SC-005** A subtree offered to a ring on name alone is **refused with the name quoted**. Verified
  against the real case: `glp_gleam` is not the polyglot-L0 `kv`/`mailbox`/`network` service set.
- **SC-006** An unbuilt ring is **never reported as passing**. Verified by building one ring only and
  asserting the aggregate refuses.
- **SC-007** Every conformance case declared is **exercised or explicitly excused with a reason**;
  `attempted = agreed + diverged + excused` holds exactly.

---

## Clarifications

Recorded from engineer rulings. The `clarify` stage was marked complete on 2026-09-02 without this
section existing — `buildkit-builder status` reported the drift, and this closes it. Each entry
names the ruling that decided it, so the decision is traceable rather than folded silently into the
requirements.

### Session 2026-09-03 — `Q-GLPNETS16`

- **Q-GLPNETS16-03 · Is 101 the delivery feature, given six existing Gleam-port features?**
  → *keep-101-bind-the-six*: 101 remains the delivery feature **and** the six unbound Gleam port
  ids are to be bound. Explicitly ruled: **no Gleam/GLP compliance loopholes** — the ring split does
  not become a route to exempt either runtime from the language's rules.

### Session 2026-09-04 — `Q-GLPNETS17` (validated against BK-QUESTION v2)

- **Q-GLPNETS17-01 · Where does the AtomVM unsupported-construct enumeration come from?**
  → **BOTH** *adopt-the-measured-dossier-boundary* **AND** *install-AtomVM-and-measure*, both ruled
  critical. Consequence: `test/ring/atomvm-unsupported.list` is adopted now from the spike that
  actually ran AtomVM 0.6.6 (observed `module proc_lib cannot be resolved`), and is labelled
  **a lower bound, not the subset**; the exhaustive measurement is staged in
  `test/ring/install-atomvm.md` and remains open. A pass from the C3 gate therefore means *no
  measured-unsupported construct is present*, **not** *this will run on AtomVM* — and the gate
  prints that limit on every pass.
- **Q-GLPNETS17-02 · On what basis may era 101 close, when the aggregate can never go green here?**
  → *close-on-scope-bounded-aggregate*: the era closes when every ring measurable on this host is
  green and every ring that is not is **UNREAD with a named, re-measured reason**. **The aggregate
  guard is not weakened** — it continues to refuse a full-delivery claim (C4-R / SC-006). The close
  criterion is the scope-bounded verdict, recorded as *"BEAM ring delivered and verified; AtomVM
  ring gated on target-side host, tracked as a named follow-up"*.
- **Q-GLPNETS17-03 · The six unbound pipeline ids, whose printed remedy refuses itself.**
  → *record-cosmetic-suppress-noise*: recorded as closed-and-unbindable by design and raised to the
  buildkit lane as a defect in `link`. A reconcile whose only complaint is those six counts as
  in-sync.

## Assumptions

1. **Scope is the delivery mechanism, not the migration.** The P4 migration gate in `008` is
   `REFUSE` (58.2% of glpnet undelineated), so this feature makes the capability *deliverable* and
   does not itself move files into any target.
2. `glp_gleam` is **build-independent of `glp_runtime`** — measured 2026-09-02: dependencies are
   `gleam_stdlib` + `gleam_erlang` only; the 88 files mentioning Dart do so in provenance comments.
3. The pinned corpus (206 cases / 238 goals) is the parity instrument. It is **not** the full 384-test
   unified suite, and 100% on it is not a claim of total semantic equivalence.
4. The MAUI Blazor Hybrid host is **target-side** and absent from glpnet; this feature delivers a
   capability the channel can host, not the channel.
5. Ring vocabulary follows `LATTICE.md` Amendment 1.1 (L0/L1a/L1b/L2/L3; **L4 is not a ring**).

## Dependencies

- `008-yx-bootmig-base` FR-017 / FR-018 (the ring split and the no-admission-by-name rule).
- `LATTICE.md` hard invariants 2, 3 and the L1a/L1b sibling rule.
- The existing parity harness `test/parity/` and the AtomVM gated probe.

## Out of Scope

- Migrating any file into `YNGENIOS*` — gated by `008` P3, currently `REFUSE`.
- The Dart runtime, Flutter app and `.glp` corpus (FR-005 — retained here).
- The QHSM-wrapped Dart reference implementation — declared by the engineer as future,
  workstation-only, oracle-class work, explicitly out of scope.
- Building the MAUI Blazor Hybrid host itself.

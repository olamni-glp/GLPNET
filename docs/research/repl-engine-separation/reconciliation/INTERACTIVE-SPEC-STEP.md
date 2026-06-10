# Interactive Spec-Step Protocol — owner-gated metric confirmation for `engine-separation` seeds

The interactive spec step is the **owner-gating discipline** of the refinement method
([`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §1): the point at which a successor seed's
metric combination and formal tools become **binding** on that seed's spec. It is a #1a
deliverable (FR-060/FR-061) and the mechanism by which the ratified framework decisions
([`DECISIONS-LOG.md`](DECISIONS-LOG.md) R1–R15) and the anchored deferrals
([`DEFERRALS.md`](DEFERRALS.md)) reach each seed at exactly the right moment — its own
`/buildkit-specify`.

This protocol is **not** a new decision; it expresses FR-060/FR-061 verbatim and composes
with the 026-established seed-note `PRE-SPECIFY` mechanism. It invents nothing.

---

## 1. The owner-confirmation protocol (FR-060, US1-AC3)

> **FR-060** (spec.md): "The framework MUST define the interactive spec-step protocol: at
> the start of each successor seed's `/buildkit-specify`, the agent proposes the metric
> combination + verification tools; the owner confirms or amends; the confirmed result is
> recorded in that seed's spec before task generation."

> **US1-AC3** (spec.md): "**Given** a completed metric table, **When** the owner reviews it
> at the interactive spec step, **Then** the owner's confirmation (or amendment) is recorded
> in the seed's spec before any implementation task is generated."

The ordering is **strict and sequential**. For each successor seed (#2–#16):

```
PROPOSE  →  CONFIRM / AMEND  →  RECORD  →  (only then) /buildkit-tasks
```

No step may be skipped or reordered. Task generation MUST NOT begin until the confirmed
table is recorded in the seed spec.

### Step 1 — PROPOSE (agent)

At the **start** of the seed's `/buildkit-specify`, the agent drafts and proposes to the
owner:

1. **The metric table** — the shared Markdown template (R8): `name | kind (pragmatic|formal)
   | tool | threshold`. The table **must blend pragmatic and formal** rows
   ([`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §2); for any seed touching the GLP
   language or a wire/byte contract it **must** carry at least one `formal` row with a named
   tool and a measurable threshold (US1-AC1). For a host/infra seed (#8, #10) the formal tier
   MAY be omitted, but each Shapiro criterion MUST then carry an explicit **N/A + justification**
   (US1-AC2; R9).
2. **The formal tools** — the seed's `formal` rows draw from the six formal-tooling slots
   ([`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §4). For a wire/protocol seed this includes
   selecting the fit tool from the **protocol-verification armoury** (R15): SPIN is the required
   default (R14), escalating to TLA+ / UPPAAL / nuXMV / mCRL2 / FDR4 / CADP per protocol type,
   with the choice **and its rationale** recorded.

The proposal is the output of the bounded, Claude-only refinement loop
([`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §1) run to its metric thresholds — but
thresholds-met is **not** termination on its own. Termination is **metric-thresholds-met AND
owner confirmation at this step** (§1, verbatim): "Termination is **metric-thresholds-met AND
owner confirmation** at the interactive `/buildkit-specify` step — not budget exhaustion
alone."

### Step 2 — CONFIRM / AMEND (owner)

The owner reviews the proposed table + tool selection and either **confirms** it as-is or
**amends** it (adds/removes/retunes rows, swaps an armoury tool, tightens a threshold, marks a
Shapiro criterion N/A). The owner's decision is **binding** on the seed spec. This is the
gating boundary: the agent does not proceed on its own proposal.

### Step 3 — RECORD (agent)

The **confirmed** (post-amendment) table — not the original proposal — is written into the
seed's `spec.md`, together with the owner's confirmation/amendment as the recorded decision.
Any armoury-tool selection records its rationale (FR-079). Recording the confirmed table is
the precondition that releases the next step.

### Step 4 — `/buildkit-tasks` (only then)

Task generation runs **only after** Step 3. A seed whose spec lacks a recorded, owner-confirmed
metric table has not cleared the gate and MUST NOT proceed to `/buildkit-tasks` —
this is the operational meaning of US1-AC3's "before any implementation task is generated."

---

## 2. The PRE-SPECIFY pointer rule (FR-061)

> **FR-061** (spec.md): "The framework MUST require that the per-seed `PRE-SPECIFY` pointer
> surfaces the ratified decisions log (`DECISIONS-LOG.md`) and the deferral register
> (`DEFERRALS.md`), so each seed applies every `R`-row whose scope includes it and actions
> every `DEF`-row anchored at it."

### Mechanism (composes with 026's seed-note `PRE-SPECIFY`)

This rule does not introduce a new surfacing channel — it reuses the one 026 already
established. From [`DEFERRALS.md`](DEFERRALS.md) §"Pickup protocol":

> "**Each anchor seed's roadmap note carries a `PRE-SPECIFY` pointer** to this file →
> `buildkit-roadmap brief <id>` shows it the moment the seed enters `/buildkit-specify`."

and from [`DECISIONS-LOG.md`](DECISIONS-LOG.md):

> "at `/buildkit-specify` for seed N, `buildkit-roadmap brief <id>` shows a `PRE-SPECIFY`
> pointer to this log + `DEFERRALS.md`."

So the seed-note `PRE-SPECIFY` pointer — already attached to each seed's roadmap note by the
026 reconciliation — is the carrier. When seed N enters `/buildkit-specify`,
`buildkit-roadmap brief <id>` surfaces that pointer, which references **both**
[`DECISIONS-LOG.md`](DECISIONS-LOG.md) **and** [`DEFERRALS.md`](DEFERRALS.md). FR-061 is the
requirement that this pointer always surface **both** files; it is consumed at Step 1 (PROPOSE),
before the agent drafts the table.

### What the agent does with each surfaced row

The two registers are consumed with **different verbs** — they are not interchangeable:

- **R-rows → APPLIED.** For each ratified row in [`DECISIONS-LOG.md`](DECISIONS-LOG.md) whose
  **"Applies to"** column includes seed N, the agent **applies** it as a binding constraint on
  the proposed table and tools. The decision is already ratified; the seed does not re-open it.
  Example: at #5/#6, R3 fixes the Output field as a length-prefixed UTF-8 blob in the MVP
  envelope, and R14 makes Promela/SPIN a **required** pragmatic-tier metric row.
- **DEF-rows → ACTIONED.** For each deferral in [`DEFERRALS.md`](DEFERRALS.md) **anchored** at
  seed N, the agent performs the row's **follow-up action** before writing the seed's spec, and
  flips its **Status** from `open` to `done (→ <feature/PR>)` (rows are never deleted — closure
  is the trail). Example: at #5, DEF-C1 (full envelope field set) and DEF-C2 (full unbound-var
  round-trip) are the deferrals to action.

The consuming rule, verbatim ([`DECISIONS-LOG.md`](DECISIONS-LOG.md)): "Apply every R-row whose
'Applies to' includes N, and action every DEF-row anchored at N."

### Ordering — PRE-SPECIFY feeds PROPOSE

The PRE-SPECIFY surfacing (this §2) runs **inside** Step 1 (PROPOSE) of §1: the applied
R-rows and actioned DEF-rows are the constraints the agent's proposal must already satisfy
when it reaches the owner. By the time the owner sees the table at Step 2 (CONFIRM/AMEND),
every in-scope ratified decision is already baked in and every anchored deferral is already
discharged — so the owner is confirming a proposal that is consistent with the ratified
framework, not auditing it for missed decisions.

---

## 3. Cross-links

- [`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) — §1 (the loop + owner-gating discipline),
  §2 (pragmatic+formal metric blend), §4 (the six formal-tooling slots a `formal` row draws
  from), §5 (the Shapiro N/A-justification rule for host/infra seeds).
- [`DECISIONS-LOG.md`](DECISIONS-LOG.md) — R1–R15; the R-rows APPLIED at PROPOSE.
- [`DEFERRALS.md`](DEFERRALS.md) — the anchored DEF-rows ACTIONED at PROPOSE; the §"Pickup
  protocol" that defines the seed-note `PRE-SPECIFY` carrier this doc reuses.
- [`1a-iterative-refinement-and-verification-framework.md`](1a-iterative-refinement-and-verification-framework.md)
  — the #1a reconciliation memo (interactive-spec-step under-specifications U1–U2 resolved
  by R8/R9).

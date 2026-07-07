# Contract: Lift, Fidelity Reporting & Drift Detection

Normative for FR-009, FR-010, FR-013 (research R9/R10).

## API

`Lifter.Lift(registry, functor) → LiftResult{rendering: SchemaDocument?, fidelity: FidelityReport, drift: DriftReport?}`

## Lift laws

1. **Source of lift = the registered CDDL artifact** (E9: CDDL is the formal form; qmedit is the
   human surface). The parser covers exactly the lowering emitter subset (lowering.md) **plus**
   the idioms of the shipped `crdt_message` CDDL (`SchemaRegistry.CrdtMessageCddl`).
2. **Never approximate** (FR-009): a CDDL construct outside the expressible set produces an
   `UnexpressibleConstruct{cddl-construct verbatim, location, reason}` entry; the report outcome
   is `Partial`; the rendering omits nothing silently — every omission is a report entry.
3. **No CDDL at all** (seeded `il_program`, `result_envelope`): whole-entry `Partial` report with
   one entry "no CDDL artifact — byte+functor registration only". This proves the partial path on
   real registry content (SC-004 quantifies over all seeded + 041 entries).
4. **Round-trip** (FR-010): for a document authored in the 043 DSL, `Lift(Lower(doc))` must be
   semantically equivalent to `doc` — asserted by (a) structural AST equivalence modulo
   canonical naming, and (b) identical accept/reject verdicts over the instance corpus (SC-004);
   where equivalence is lost the fidelity report states precisely which construct lost it.

## Drift detection (FR-013)

- At registration, `RegistryRecord` stores `sha256(cddl)` and `sha256(qmedit)`.
- On every lift/view/re-lower of an entry that has stored XSD-level source, the current registry
  forms are re-hashed and compared to the stored hashes.
- Mismatch ⇒ `DriftReport{form, storedSha256, currentSha256}` attached to the result, and the
  rendering is produced from the **current registry truth**, never from the stale stored
  XSD-level source (spec edge case). The stale source remains retrievable, flagged as stale.

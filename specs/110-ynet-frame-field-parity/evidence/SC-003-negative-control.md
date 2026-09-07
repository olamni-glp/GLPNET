# Evidence — feature 110

**Host GAVRIELLA · all times UTC · suite `csharp/ynet_client.tests`**

## T-001 baseline (before any change)
```
Failed: 0, Passed: 177, Skipped: 0, Total: 177
```

## T-013 after the seam ALONE (Sequence deliberately left 0-based)
```
Failed: 0, Passed: 177, Skipped: 0, Total: 177   <- IDENTICAL to baseline
```
The seam is behaviour-preserving by measurement, not by assertion.

## SC-003 — THE CHECK OBSERVED FAILING (pre-fix, new tests only)
```
Failed: 4, Passed: 4, Skipped: 0, Total: 8
  Frame_sequence_numbering_starts_at_one_and_increments        [FAIL]
  Every_field_is_either_agreed_or_ruled_never_unruled          [FAIL]
  Exactly_the_three_ruled_fields_diverge_and_sequence_agrees   [FAIL]
  The_check_fails_when_a_must_agree_field_is_deliberately_diverged [FAIL]
```

### SC-005 — the divergence count produced BY THE CHECK, not typed
```
Assert.Equal() Failure: Collections differ
Expected: string[]     ["Origin", "SenderActor", "SenderNode"]
Actual:   List<string> ["Origin", "SenderActor", "SenderNode", "Sequence"]
                                                               ^ (pos 3)
```
The machine independently produced **four** divergent fields. The roadmap brief said **three**
and omitted `SenderNode`. The count is now a measurement, never a claim.

## T-063 final — after the seam + rulings + tests + the Q-110-02 Sequence change
```
Failed: 0, Passed: 186, Skipped: 0, Total: 186
```
177 baseline + 9 new. Zero regressions.

## A correction against my own method
One PRE-EXISTING test failed after the Sequence change:
`CoopFileCarrierTests.A_frame_written_by_this_client_is_readable_as_the_canonical_shape`,
which pinned `Sequence == 0`. It encoded the OLD basis, so updating it to 1 is correct under
Q-110-02 — but it also **refutes the blast-radius claim I gave the engineer**. I reported
"the only file-plane consumer is the filename component", derived from `grep '\.Sequence'`.
That search reads the **C# member name**; this test reads `GetProperty("Sequence")` — the
**serialized** name — so the grep could not see it. **A blast-radius search on a member name
has a blind spot for every consumer that goes through serialization.** The conclusion (safe,
one-line, revertible) held; the method that produced it was incomplete, and that is the part
worth carrying forward.

---

# 🔴 CORRECTION AND RE-EVIDENCE — after adversarial review, 2026-09-07T19:0xZ

An adversarial `codex exec` review of this branch returned **six findings, three HIGH**. They were
correct. The most important one is a finding **against the evidence recorded above**.

## Finding 2 (HIGH) — the original negative control was not a control, and this file misattributed it

The first control took a correctly-constructed pair and mutated one with `wire with { Body = ... }`.
That tampers with a **record**, never with a **carrier**, and it asserted a classifier verdict
rather than driving the acceptance gate. And the "observed failing" transcript above credits that
test's pre-fix failure to detection of the Body tamper — **it was not**. It went red because
`Sequence` was still divergent at that moment. **An evidence file that credits the wrong cause is
worse than no evidence, because it retires the question.** That is the wave-34 lesson
("a TAUTOLOGICAL negative control") recurring inside the very test written to honour it.

## The replacement, and the proof it is real

`The_gate_rejects_when_the_two_carriers_genuinely_disagree_on_a_must_agree_field` now makes the two
**carriers** disagree — each constructs its own frame through its own real `BuildFrame` path with a
different body — and asserts `FrameFieldParity.Accepts(...)`, **the same gate the passing test
calls**, returns false. It also asserts `Body` is the *only* unruled divergence, so the rejection is
attributable to the divergence introduced rather than to an unrelated field.

`A_differing_field_with_no_ruling_is_unruled_and_fails_the_gate` (finding 3) drives a new
ruling-table overload with `SenderActor` removed, and requires the now-unruled divergent field to
come back `DivergesUnruled` and fail the gate — then re-accepts the *same pair* with the ruling
restored, proving the verdict tracks the RULING, not some incidental property of the frames.

### MUTATION TEST — the controls were proven to fire, not assumed to

`FrameFieldParity.Accepts` was deliberately broken to `return true;` and the suite re-run:

```
FrameFieldParityTests.A_differing_field_with_no_ruling_is_unruled_and_fails_the_gate                [FAIL]
FrameFieldParityTests.The_gate_rejects_when_the_two_carriers_genuinely_disagree_on_a_must_agree_field [FAIL]
Failed: 2, Passed: 8, Skipped: 0, Total: 10
```

Both controls fire. **The original control would not have** — it asserted a verdict, not the gate,
so a broken gate would have passed it. Mutant reverted; suite restored to green.

## The other four findings, all fixed

| # | severity | finding | fix |
|---|---|---|---|
| 1 | HIGH | ruled divergences were reported only inside a FAILURE message, so a green run hid every permitted divergence — the suppression FR-006 exists to prevent | the report is written to test output on the **green** path |
| 3b | MED | the field-coverage test derived both sides with the **same reflection expression** as production, so it was circular | the expected set is now an explicit literal — a test's oracle must be independent of what it checks |
| 4 | MED | FR-008 required source provenance as **data**; it existed only in comments the report could never print | `FrameFieldRuling` gained `FilePlaneSource` / `WirePlaneSource`, printed with every ruled divergence |
| 5 | MED | "first frame" bypasses each carrier's send preconditions, so it says nothing about long-running carriers | scope documented at the helper: the file plane increments only after a reachability check, the wire before size/connection validation, so counters drift in service; only BASIS is compared |
| 6 | LOW | reachability-before-increment survived, but nothing pinned it | new test: a refused send must not consume a sequence number, and the first frame that goes out is still #1 |

## Final

```
Failed: 0, Passed: 187, Skipped: 0, Total: 187
```

177 baseline + 10 new. Zero regressions. Every HIGH finding fixed and the fix for the most severe
one independently proven by mutation.

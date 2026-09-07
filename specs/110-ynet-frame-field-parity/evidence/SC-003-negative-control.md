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

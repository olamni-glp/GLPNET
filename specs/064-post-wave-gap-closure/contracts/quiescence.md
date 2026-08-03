# Contract — quiescence oracle

**Parity target**: the C# link's quiescence algorithm (goal-state census + in-flight accounting at the link seam).

## Protocol

1. Any instance may initiate a census round: CENSUS_REQ {round_id} fans to all linked peers.
2. Each instance replies CENSUS_REP {round_id, running, suspended, inflight_out, inflight_acked} — counts snapshotted atomically w.r.t. its scheduler step.
3. Verdict at the initiator: `quiescent` iff every instance reports running=0 AND for every link inflight_out==inflight_acked in both directions for the same round; otherwise `active`.
4. Any fault-lattice event on any participating link during the round ⇒ verdict `faulted` for that round; `faulted` is terminal until the link re-establishes and a new round runs.
5. A round with a missing reply within the bounded-silence window (existing ≤30s bound) ⇒ `faulted`, never a hang.

## Normative properties

- **Safety**: the oracle never reports quiescent while any message is in flight or any goal can advance (asserted by an adversarial test that injects a delayed DIST_BIND during a census round).
- **Liveness**: a genuinely quiescent computation is reported quiescent within two census rounds.
- **Fault honesty**: a dropped peer is never folded into quiescent (edge-case list, spec).

## Acceptance

Scenario suite: quiescent two-instance run; active run with in-flight bind during census; fault mid-round; re-arm after re-establish. Gleam↔Gleam and Gleam↔C# variants, committed .out results.

# Contract — Supervision (061)

## Liveness

- Supervisor pings the engine (wire PING) every `ping_interval`; a missing
  ACK within `ping_timeout` OR child-process exit = death detected.
- MVP liveness is HOST-TIMER ONLY. The self-prove GLP goal is a language
  change (new system predicate) gated on explicit owner approval (DEF-F1,
  CLAUDE.md §1.14): this wave may deliver a PROPOSAL MEMO only. Any task that
  implements it without recorded approval violates the contract.

## Crash handling

1. Record CrashRecord {timestamp, exit_code?, detection}.
2. Apply restart backoff (initial → multiplier → max).
3. Start replacement engine `--from-snapshot latest --store <root>`.
4. Engine restores (contract: snapshot-store.md), re-wires links
   (RewireHandle), resumes; supervisor confirms first healthy PING and
   completes the CrashRecord with `restored(seq)`.

## Unrecoverable taxonomy (DEF-F2)

`repeated_immediate_crash` (≥ crash_threshold within crash_window after
restore) · `corrupt_latest_snapshot` (previous-seq fallback also failed) ·
`store_unavailable` (both backends down) · `explicit_poison`.
On classification: STOP restarting, persist the classification on the
CrashRecord, surface loudly to the operator (FR-023). No silent loops.

## Operator surface

- `status`: engine state, last heartbeat, last snapshot seq.
- `history`: crash records, append-only (FR-024).
- Packaging: one binary hosts as console (dev/test) and Windows service
  (deploy) — .NET BackgroundService; contract portable, Windows-first (FR-025).

## Timing obligation

Detect→restart→restore ≤ ping_interval + ping_timeout + restore(snapshot)
(SC-003) — modelled in UPPAAL (FR-040) with the interval/timeout/backoff as
parameters; verdicts in `docs/research/repl-engine-separation/models/uppaal/`.

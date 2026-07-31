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

Detect→restart→restore ≤ ping_interval + ping_timeout + backoff + restore(snapshot)
(SC-003) — the backoff term is the same contract's mandated step 2
(backoff_initial in the healthy-first-crash case, up to backoff_max in a storm);
the original sentence omitted it, flagged by the UPPAAL model's bound analysis
(`models/uppaal/RESULT.md` spec-precision note, reconciled at the 061 wave
close). Modelled in UPPAAL (FR-040) with the interval/timeout/backoff as
parameters; verdicts in `docs/research/repl-engine-separation/models/uppaal/`.

## Topology (MVP)

The supervisor is the engine's ONE wire client (FR-002): it holds the single
client slot and pings over it. Operator queries (`--status`/`--history`) read
the supervisor's durable status/crash-log files, not the wire; peers interact
over LINKS, which are independent of the client slot. An interactive REPL
client and supervision are therefore mutually exclusive on one engine in the
MVP — a supervised multi-client control surface is the deferred DEF-A2 scope.

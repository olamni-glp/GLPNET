# `.codeconv/conversion-equiv/` (feature 020)

Checked-in **per-file equivalence artifacts + the escalations report**
(contracts/equiv_cli.md).

- `_escalations-report.md` — aggregated divergence/escalation report across the
  frontier, written by `equiv aggregate-escalations` (T027). Lists files that
  failed the tier's equivalence gate after one bounded re-verify (escalate-don't-guess).
- Per-file recorded-trace / verdict artifacts produced by `equiv capture` /
  `equiv compare` (T018/T019) for durable, replay-safe ingest (the durable
  `equiv` step reads recorded traces — it never spawns a REPL; R12).

The relational source of truth is `codeconv.dart_equivalence`; these artifacts are
the checked-in, human-reviewable companions that round-trip the verdict state.

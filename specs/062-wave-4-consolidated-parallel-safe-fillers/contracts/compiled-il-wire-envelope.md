# Contract — Compiled-IL wire envelope (US3)

Frames a compiled intermediate form for transmission between an engine that compiles and one
that executes (factor-out-compiler + compiled-il-on-the-wire).

## Envelope fields
- `il_version` — semantic version of the IL format the sender produced.
- `compiled_form` — opaque compiled bytes (the factored-out compiler's output).
- `integrity_digest` — digest over `compiled_form` for corruption detection.
- `source_metadata` — minimal provenance (module/program id) for diagnostics.

## Receiver obligations (hardening — FR-005a)
1. Reject an unknown/incompatible `il_version` with a diagnostic; do NOT execute.
2. Verify `integrity_digest`; on mismatch, reject safely; do NOT execute.
3. On a transport failure mid-transfer, abort the transfer; engine state MUST be unchanged.
4. On success, execution result MUST equal local execution of the same program (FR-005).

## Non-goals
- No new language semantics; the IL is the existing compiler's output, merely factored out and
  framed for the wire. (If IL shape must change, that is a separate, spec'd change.)

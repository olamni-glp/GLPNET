<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract: resume-goal registration (064 / FR-001..003, FR-009)

## File

`<repo-root>/glpservice/resume.json` — discovered by walking up from
`AppContext.BaseDirectory` (the `SharedCertMaterial.ResolveCertDir` idiom).
UTF-8, no BOM. Absent directory or file ⇒ the feature is inert: no output, no
delay, no behavior change (SC-005).

## Schema (v1)

```json
{
  "version": 1,
  "program": "programs/tests/quic/quic_chat.glp",
  "goal": "main(olamnit, R).",
  "enabled": true,
  "replay": true
}
```

- `version` (int, required): MUST be `1`; any other value ⇒ diagnostic
  `resume: unsupported version <v> in <path>` and the registration is ignored.
- `program` (string, required): repo-relative `.glp` path, resolved against the
  discovered repo root.
- `goal` (string, required): complete goal text, run verbatim (must end with `.`).
- `enabled` (bool, default true): `false` ⇒ print one line
  `resume: registration present but disabled` and skip.
- `replay` (bool, default true): `false` ⇒ skip WAL replay, arm only.

Unknown extra keys are ignored (forward-compatible).

## Host behavior (launch sequence)

1. After prelude load, inside the shim's `AfterEngineCreated` seam (the
   converted `glp_repl.cs` is NOT edited):
   a. discover + parse the file; on parse error ⇒
      `resume: invalid registration at <path>: <cause>` — REPL continues (FR-009).
   b. if enabled: print `resume: arming <goal> from <program>`.
   c. replay (if `replay` and WAL non-empty) per contracts/message-log-and-replay.md.
   d. load `program` (same semantics as the interactive `.glp` load); failure ⇒
      named diagnostic, skip goal, REPL continues.
   e. run `goal` synchronously (same semantics as typed input; R2).
2. With zero registrations the launch path is byte-identical to today.

## Acceptance hooks

- US1 scenario 1: registration + restart ⇒ peer connects with zero keystrokes.
- US1 scenario 2: missing/unloadable `program` ⇒ diagnostic names path + cause.
- US1 scenario 3 / SC-005: no file ⇒ unchanged startup transcript.

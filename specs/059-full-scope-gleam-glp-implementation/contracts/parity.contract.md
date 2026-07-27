<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract — Parity Governance (G4)

**Governs**: spec FR-005; SC-004; the differential harness. Ruling:
`docs/research/fullscope-gleam/phase2-verify/rulings.md` (G4).

## Normative authority

Where the Gleam instance and the Dart/C# reference **v2.16** diverge, **the reference governs**. The
Gleam instance is brought to the reference, never the reverse. This is a binding scope decision, not a
per-case judgement.

## Parity bar

1. **Outcome parity** — the Gleam instance runs the reference program corpus with outcomes identical to
   the Dart oracle (SC-004).
2. **Byte-identical where pinned** — where the plan pins bytes (result envelope, IL/wire codecs, framed
   link wire), the Gleam output MUST be byte-for-byte equal to the reference vector.
3. **Named golden pins** — including the `UnifyConstant` **ground-struct-literal** case: the golden pin
   fixes the reference v2.16 behavior; the Gleam engine MUST reproduce it exactly.

## Differential harness

The verify/accept waves run a differential harness that executes each reference program on both the
Gleam instance and the Dart oracle and compares outcomes (and pinned bytes). Its verdicts are the
committed, restart-safe evidence for SC-004.

## Divergence discipline (no inline patching)

```text
Gleam ≠ reference on a pinned/parity case
  → HALT. Record a drift finding (which case, Gleam result, reference result).
  → If the *reference/oracle* moved: the paired Dart-suite guard fails, surfacing the moved target.
  → If the *Gleam* side is wrong: it is a close/build defect — fix to reference, re-run the harness.
  → NEVER patch the harness or special-case the divergence inline (spec Edge Cases; Constitution II).
```

The surfaced-unimplemented frozen-semantics gap (WRITE-mode void slot → `ConstTerm(null)`) is
**escalate-if-hit** per the freeze — never patched ad hoc.

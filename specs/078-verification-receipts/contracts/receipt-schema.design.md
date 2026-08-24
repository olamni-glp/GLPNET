<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract: Receipt JSON schema — DESIGN HANDOFF (buildkit-owned, FR-024)

> **Authoritative home: buildkit.** Per FR-024 the receipt schema has exactly one authoritative
> definition, owned by the repository that distributes to every host. This file is the **one-time
> design handoff** for the buildkit companion change — it is **not** the runtime artifact and MUST
> NOT be copied into glpnet source as an owned schema. glpnet's `bind.py` resolves the schema from
> the pinned installed buildkit version; copying it here as a live artifact is the exact
> copy-divergence FR-024 exists to stop.

## Wire format

A receipt is a UTF-8 JSON object written to a sidecar file (FR-022). It is machine-readable
(validated against this schema — FR-004) and prints as a compact human-readable summary where
displayed. Field semantics and validation invariants are in [data-model.md](../data-model.md#receipt).

```json
{
  "schema_version": "1.0.0",
  "contract_version": "buildkit-2026.8.14.1",
  "check_id": "codeconv.build-gate.dart_csharp",
  "area": "build-gate",
  "resolved_target": {
    "kind": "path", "identity": "D:/.../out/csharp", "requested": "out/csharp",
    "resolved": true
  },
  "outcome": "EMPTY",
  "examined_count": 0,
  "total_count": 0,
  "skipped": [], "skipped_total": 0,
  "examined": [],
  "truncated": { "enumerations": false, "dropped": 0, "byte_capped": [] },
  "ran_at": "2026-08-18T20:00:00Z",
  "verdict_pointer": "receipts/build-gate/run-abc/codeconv.build-gate.dart_csharp.receipt.json"
}
```

## Bounding rules (FR-005)

- `examined` and `skipped` enumerations are capped at a **declared maximum** (`MAX_ENUM`,
  a contract constant). `examined_count` / `skipped_total` are **always** the true totals.
- A **byte backstop** (`MAX_FIELD_BYTES`) caps any single string field.
- If any enumeration or field was capped, `truncated.enumerations` / `truncated.byte_capped`
  MUST reflect it and `truncated.dropped` MUST record how many enumeration entries were dropped.

## Outcome enum (FR-006)

`PASS | EMPTY | UNREAD | UNSEARCHABLE | FAIL`. Only `PASS` and `EMPTY` are successful (FR-007).

## Versioning

The schema is SemVer. Additive fields ⇒ MINOR; a changed required field or invariant ⇒ MAJOR.
Every receipt records `contract_version`; a consumer whose pinned MAJOR differs from a receipt's
MAJOR treats the receipt as UNREAD (unrecognised contract) rather than silently accepting it.

<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Data Model: durable-listener-service-box (064)

Three entities (spec Key Entities), each mapped to its concrete home.

## 1. ResumeRegistration

The operator's durable declaration of a service. One per repo in MVP
(spec assumption: one REPL process serves one registered service endpoint).

| Field | Type | Rules |
|---|---|---|
| `program` | string (repo-relative path to a `.glp` file) | MUST exist at launch; load failure ⇒ named diagnostic, REPL continues (FR-009) |
| `goal` | string (a complete GLP goal, trailing `.` included) | run verbatim after `program` loads — identical semantics to typed input |
| `enabled` | bool (default `true`) | `false` ⇒ registration is inspectable but inert (operator "remove" without deleting the file) |
| `replay` | bool (default `true`) | `false` ⇒ arm without history replay (fresh-start escape hatch) |

- **Home**: `glpservice/resume.json` at the repo root (R1), discovered by
  walk-up; absent file ⇒ feature fully inert (SC-005).
- **Lifecycle**: created/edited/deleted by the operator with any editor;
  re-read once per launch; never written by the host.
- **Identity/uniqueness**: the file is the registration; duplicate-service
  protection is structural (one file, one registration) per the spec edge case.

## 2. MessageLogEntry (WAL op)

One durably recorded received message — concretely, one op in the crdtmsg
op journal (`IOpWal`), dot-keyed for idempotence.

| Field | Type | Rules |
|---|---|---|
| dot key | (replica, counter) — existing `Op` identity | CRDT dedup ⇒ re-append of the same op is a no-op (SC-002 exactly-once) |
| payload | the decoded ground term as shipped (crdtmsg op encoding) | ground-only (link ground-relay invariant upstream) |
| receipt order | the WAL's ordered `Ops` sequence | replay order = receipt order (FR-005) |
| link id | ground LinkId of the delivering link | recorded for diagnostics; not part of identity |

- **Home**: `PgliteOpWal` (primary; `.pgdb/` cluster — Constitution VI-b) with
  `OpWal` file fallback, primary-then-loud-degrade (R3).
- **Durability point**: appended in the delivery observer BEFORE the heap bind
  that lets the program act on the message (R4, FR-004).
- **State transitions**: append-only; no update, no delete (retention is
  explicitly future work per spec assumption).

## 3. ServiceListenerEndpoint

The network identity peers dial; not a new stored record — it is the ground
`link_id(Scheme, Endpoint, Nonce)` term inside the registered goal's program.
Re-arm re-binds it because re-running the goal re-runs `server_listener` with
the identical ground LinkId (FR-007). Port-occupied at re-arm ⇒ the existing
establishment failure surfaces as the named diagnostic (edge case, FR-009).

## Relationships

```
ResumeRegistration 1 ──arms──▶ ServiceListenerEndpoint (via the registered goal)
ResumeRegistration 1 ──replays──▶ MessageLogEntry* (ordered, read-only at boot)
delivered inbound term 1 ──appends──▶ MessageLogEntry 1 (before program observes it)
```

## Validation rules (from FRs)

- FR-003: registration inspectable (plain file) + removable (delete or `enabled:false`).
- FR-004: append precedes program observation (observer runs before heap bind).
- FR-005: replay = WAL order, exactly once per stored op, no re-append during replay.
- FR-006: every entity above is host-side; the GLP language surface is untouched.

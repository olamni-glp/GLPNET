<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 57a8b9fc-2569-4312-9cb0-17b9be75ff62
-->

# Implementation Plan: Wave 5 consolidated: captured triad

**Branch**: `063-wave-5-consolidated-captured-triad` | **Date**: 2026-07-29 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/063-wave-5-consolidated-captured-triad/spec.md`

## Summary

Three consolidated deliverables: (US1) complete the 036 QUIC+WS prototype into
a genuine live-REPL link — wire the inert `--repl` process bridge through the
spec-025 link-message interface, prove the dup-id mesh eviction defect fixed by
a regression scenario, build the C# host library in-tree so the 9 skipped
integration tests execute; (US2) the durable first-hop mesh-messaging
prototype per the operator's intake brief — `/ms-message` skill + Python tool
with originator/recipient roles, Kafka-style signal-then-fetch on
mailboxes/topics, WAL + PGlite hot tier aging to DuckLake, dense per-sender
sequences with gap detection, retention classes, basic friend-lookup, DLQ;
(US3) operationalize the migrated 3-role capability on real wave-5 work and
record the evidence; (US4) advance the three consolidated roadmap features at
wave close. Wave-4-dependent material is sequenced last (FR-015); none is
currently identified.

## Technical Context

**Language/Version**: C# / .NET 10 (glp_quick_host, link layer), Python 3.14
(glp_quick CLI tool, ms_message tool), Markdown protocol docs (US3)
**Primary Dependencies**: spec-025 link layer (`csharp/glp_link`),
036 host (`csharp/glp_quick_host` + `glp_quick/` Python tool), PGlite via the
shared `codeconv.bridge_client` bridge (constitution VI-b), DuckDB/DuckLake
(Python `duckdb` package) for the aging tier, installed buildkit 3-role
capability (bk-3rtask, spec-051) for US3
**Storage**: US2 — WAL + message files on disk, sequence/metadata in the
repo's `.pgdb/` PGlite cluster (`msmesh` schema, additive migration), aged to
DuckLake parquet under a gitignored data dir
**Testing**: `dotnet test` (csharp link/host suites incl. the 9 currently-
skipped integration tests), `pytest` (glp_quick + ms_message), scenario
scripts for mesh/messaging drills; REPL suite untouched by US1/US2 (no
glp_runtime/glp_gleam changes planned)
**Target Platform**: Windows 11 dev hosts (fleet), LAN + defined
internet-reachable hosts; C# QUIC floor Win11+ per 036 ruling
**Project Type**: multi-part — C# host completion + two Python CLI tools + a
protocol/method doc
**Performance Goals**: SC-004 disconnect drill N≥1,000 messages exactly-once;
SC-001 link-up under 5 minutes from REPL start
**Constraints**: first-hop only (no multi-hop routing); ground-relay wire
rules unchanged; bounded-silence fault limits (30 s family) honored (SC-005);
additive-only PGlite migration, single head (constitution VI-a/VI-b)
**Scale/Scope**: 2–3+ instance meshes; single-digit peers per node;
thousands of journalled messages per drill

## Constitution Check

*GATE: evaluated pre-Phase-0 and re-checked post-design — PASS (no violations).*

- **I Spec-First**: spec + clarifications complete before this plan; US1 acceptance
  is pinned to the recorded audit findings, not code archaeology. PASS.
- **II Bug-Protocol**: the dup-id defect is handled as verify-by-regression
  first (the current code shows an eviction guard whose provenance is
  unverified against the audit symptom) — report/fix, never mask. PASS.
- **III SRSW**: no GLP clauses are planned to change; any incidental .glp work
  obeys SRSW. No `skipSRSW`. PASS.
- **IV-a Language Authority**: no language surface changes anywhere in this
  wave. PASS. **IV-b**: no engine internals touched. PASS.
- **V Claude-only LM**: US3 uses the installed buildkit capability (Claude
  agents + the local codex CLI it already ships with); no external LM API on
  any path. PASS.
- **VI-a/VI-b**: US2's `msmesh` schema is an additive migration on the single
  head, reached via the shared bridge; DuckLake files live in a gitignored
  data dir (not a second working-data cluster; analytics tier, not
  pipeline state). PASS.
- **VII Test-gated shipping**: baselines re-run per stage; ship via GitFlow;
  release cut announced to the fleet lead first. PASS.
- **VIII Single source of truth**: this wave's authoritative surfaces are the
  036 audit record (US1 acceptance), the intake brief (US2 scope), and the
  recorded method doc (US3 seed); contracts reference them. PASS.

## Project Structure

### Documentation (this feature)

```text
specs/063-wave-5-consolidated-captured-triad/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── link-completion.md
│   ├── mesh-messaging-protocol.md
│   └── three-role-engagement.md
└── tasks.md             # Phase 2 output (/bk-tasks — NOT created here)
```

### Source Code (repository root)

```text
csharp/glp_quick_host/           # US1: --repl live bridge + mesh dup-id regression target
├── Program.cs                   #   REPL process bridge wiring; Mesh id/eviction logic
└── glp_quick_host.csproj
csharp/glp_link.tests/           # US1: the 9 skipped integration tests go live
glp_quick/                       # US1: Python CLI (server/client roles) — --repl plumbed through
├── src/  └── tests/
out/csharp/                      # US1: in-tree build outputs the integration tests load

ms_message/                      # US2: NEW Python tool (originator/recipient CLI)
├── pyproject.toml
├── src/ms_message/
│   ├── cli.py                   #   originator / recipient entry points
│   ├── wal.py                   #   WAL + message-file policy (size-tiered files)
│   ├── store.py                 #   PGlite hot tier (msmesh schema, via bridge_client)
│   ├── lake.py                  #   DuckLake aging + catch-up queries
│   ├── protocol.py              #   signal/fetch/friend-lookup message shapes
│   └── dlq.py                   #   dead-letter queue
└── tests/
.claude/skills/ms-message/       # US2: the /ms-message skill
codeconv/src/codeconv/db/migrations/versions/
└── 0011_msmesh_schema.py        # US2: additive migration (single head 0010 → 0011)

docs/three-role-orchestration/   # US3: the formal protocol doc + engagement records
```

**Structure Decision**: US1 completes in place (csharp/glp_quick_host +
glp_quick — the 036 surfaces). US2 is a new sibling Python package
`ms_message/` mirroring the glp_quick layout, with its skill under
`.claude/skills/ms-message/` and its schema as the next additive migration.
US3 is documentation + recorded engagements (no new runtime code).

## Phase ordering & the FR-015 parallel-run rule

1. **US1 first** (independent of wave-4 entirely; unblocks QUIC evidence for US2's optional leg).
2. **US2 next** (transport-agnostic per clarification — TCP evidence acceptable; QUIC leg after US1).
3. **US3 engagements run DURING US1/US2** (dogfood: use the triads on wave-5's own review points).
4. **US4 last** (wave close).
No currently-identified wave-5 task consumes wave-4 output (the §1.14 pair and
ZMQ primitives are untouched); if one emerges it is sequenced last + flagged
on the scheduler board per FR-015.

## Complexity Tracking

No constitution violations to justify. The one deliberate scope split —
DuckLake aging implemented behind a seam with PGlite-only acceptable for the
drill if the lake dependency misbehaves — is recorded in research.md R6 with
its fallback, not a violation.

---
name: codeconv-convspec
description: Per-file Dart→C#/.NET deep source analysis + official-docs research producing a structured, reviewable conversion spec + a growing conversion-idiom KB. Use when the user types `/codeconv-convspec` or asks to analyse a Dart file / produce or ingest a conversion spec / record or query conversion idioms / aggregate convspec escalations. Spawned by /codeconv-builder on `needs_agent_work`.
argument-hint: "[status|next|ingest|record-idiom|aggregate-escalations] [flags]"
compatibility: "Claude Code (Agent tool required for the analysis + research sub-agents)"
---

# /codeconv-convspec

Wrapper over `codeconv convspec` for **all deterministic state**
(convspec-readiness, idiom-KB lookup/record, artifact validation +
two-phase ingest, escalation aggregation). The Python CLI is the single
source of truth and the skill forwards arguments verbatim for every
state operation.

The skill **additionally** carries the agent orchestration the Python
CLI structurally cannot do (justified deviation, plan Complexity
Tracking; mirrors feature-017): per file (or SCC batch) it spawns a
**deep-analysis sub-agent** and, only on an idiom-KB miss, a
**SEPARATE research sub-agent**. The agents produce a checked-in
artifact; the deterministic CLI ingests it. **No agent output ever
enters a DBOS step except as a re-read of the checked-in artifact**
(replay-safe — R3). The skill contains **no deterministic-state logic**.

## Resolve + invoke

1. venv: `codeconv/.venv/Scripts/python.exe` (Windows) / `.../bin/python`.
2. Run from repo root; pass `--data-dir D:/bstdev/research/glp/glpnet/.pgdb`
   (CLAUDE.md — shared-bridge reuse convention; D: is NTFS so the guard
   no longer triggers).
3. Explicit subcommand → `codeconv ... convspec <args verbatim>`.

## Subcommands

| Subcommand | Purpose |
|---|---|
| `status` (default) | convspec-readiness counts + escalation count. Read-only. |
| `next [--limit 7]` | Next convspec-ready batch (SCC = one unit). Read-only, deterministic. |
| `ingest <path> [--respec]` | Deterministically validate + two-phase ingest the agent artifact (`specced` / `needs_agent_work` / `escalated`). `--respec` re-opens on sha256 drift (FR-019). |
| `record-idiom --construct … --source-form … --target-form … --rationale … --first-seen …` | Record a resolved idiom (conflict ⇒ `conflicted`+escalate, FR-014). |
| `aggregate-escalations [--report-out p]` | Single `.codeconv/conversion-idioms/_escalations-report.md` (FR-013/014). |

## Analysis sub-agent (one per file; SCC = ONE coordinated batch)

**Input**: the real `.dart` source + sha256, the idiom-KB hits for its
known constructs (from `convspec next`/the KB), and the artifact schema.

**Must produce** the checked-in
`.codeconv/conversion-specs/<rel>.dart.md` per
`contracts/convspec_artifact_format.md`:

- a fenced ```yaml``` structured block: `schema_version: 1`,
  `source_path`, `source_sha256`, `target_code_unit` (shape only),
  `constructs[]` (each: `construct_key`, `source_form`,
  `target_decision`, `idiom_id|null`, `research_finding_id|null`,
  `nuance` — REQUIRED for every non-trivial construct),
  `conversion_units[]`, `escalations[]`; AND
- embedded human-readable rationale + research provenance prose per
  non-trivial decision.

**Discipline (hard):**
- **Spec-only — NEVER emit compilable C#** (FR-023). The artifact
  describes the conversion; codegen is a later stage. The CLI ingest
  REJECTS any artifact containing a real C# code block.
- **Escalate, don't guess** (FR-013): if a construct's correct Dart→C#
  translation cannot be established from analysis + KB + research, add
  an `escalations[]` entry (`kind: undecidable`), do NOT invent one.
- Deep analysis means semantics/types/null-safety/async/stream/isolate
  — not a mechanical rename. Every well-known nuance (`Stream` vs
  `IAsyncEnumerable`, value-vs-reference, null-safety mapping) MUST be
  explicitly addressed (US2 AS4).
- For any non-trivial construct lacking a KB idiom, REQUEST research
  **before** deciding.
- SCC members are planned as ONE coordinated batch with sibling
  cross-references; downstream blocked until all members' specs exist.
- Concurrency ≤ the builder `--limit`.

## Research sub-agent (SEPARATE; only on request + idiom-KB miss)

**Input**: the verbatim construct/question.

**Rule (FR-024):** official **Dart** and **.NET/C#** documentation is
**authoritative**; broader web is **corroboration only, never the sole
basis**. Log the verbatim query, the authoritative citation, any
corroborating sources, the conclusion → the analysis agent records
these into `research_findings` (via the CLI) + the artifact provenance
prose. After first research a construct is cached
(`research_findings.construct_key` UNIQUE) and is **never re-researched**
(FR-012/FR-024) — offline-reproducible.

**Failure / timeout / inconclusive / non-authoritative-only** → return
an escalation, NEVER a naive fallback (FR-013, spec edge cases).

## Idiom-KB decision order (MANDATORY — convspec_idiom_schema.md)

```
normalise construct → construct_key
KB lookup conversion_idioms[key]
  hit active        → REUSE verbatim (NO research, NO re-derive) — FR-012/SC-007
  hit conflicted/esc→ ESCALATE (FR-014, never guess)
miss → research_findings[key]
  cached            → use cached (FR-024 never re-research)
  absent            → spawn SEPARATE research sub-agent (above)
       authoritative → record research + new idiom (active)
       inconclusive  → ESCALATE (FR-013, no naive fallback)
conflict: new research vs active idiom, or idiom vs idiom → ESCALATE (FR-014)
```

## Discipline

- The skill never invents/mutates state — every decision is the CLI's.
- Any escalation ⇒ `open_escalation_count > 0` ⇒ conversion blocked for
  that file ONLY (specing still completes); surfaced via
  `aggregate-escalations`. Do not work around — await human (CLAUDE.md
  Bug Protocol).

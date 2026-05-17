# Contract — Agent Orchestration (`/codeconv-convspec`; FR-009/010/023)

Justified deviation (plan Complexity Tracking): the skill spawns Claude
sub-agents; the Python tool stays deterministic and replay-safe.

## Analysis sub-agent (one per file; SCC = coordinated batch)

**Input**: real `.dart` source path, source sha256, idiom-KB hits for known
constructs, the artifact schema.
**Must produce**: the checked-in `.codeconv/conversion-specs/<rel>.dart.md`
per [convspec_artifact_format.md](./convspec_artifact_format.md) — deep source
analysis (semantics, types, null-safety, async/stream/isolate), per-construct
decisions, mandatory nuance section, decomposed conversion units.
**Discipline**: escalate, don't guess (FR-013); spec-only, **never** emit C#
(FR-023); for any non-trivial construct lacking a KB idiom, request research
**before** deciding.
**Concurrency**: ≤ `--limit` analysis agents in flight; SCC members planned as
one coordinated batch with sibling cross-references; downstream blocked until
all members' specs complete (FR-002).

## Research sub-agent (SEPARATE; on request + KB miss only)

**Input**: the verbatim construct/question.
**Rule**: official Dart / .NET-C# documentation is authoritative; broader web
**corroboration only, never sole basis** (FR-024). Logs the verbatim query,
the authoritative citation, any corroborating sources, the conclusion → the
analysis agent records these into `research_findings` + the artifact
provenance prose.
**Failure/timeout/inconclusive/non-authoritative-only** → return an
escalation, never a naive fallback (FR-013, spec edge cases).

## Orchestration ↔ DBOS boundary

Agents run **only** in the skill, between `builder run` invocations, triggered
by the deterministic `NeedsAgentWork` signal (see
[dbos_workflow_model.md](./dbos_workflow_model.md)). No agent output enters a
DBOS step except as a re-read of the checked-in artifact ⇒ replay-safe. Tests
use a mocked-agent harness; no real LLM in CI.

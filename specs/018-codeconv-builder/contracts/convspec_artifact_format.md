# Contract — convspec Artifact Format (FR-011 / FR-023 / SC-006)

One checked-in markdown per file: `.codeconv/conversion-specs/<rel>.dart.md`,
linked from the tombstone (`spec_path`). **Spec-only — no compilable C#**
(FR-023): the artifact describes the conversion; a later stage generates code.

## Mandatory structure

### A. Fenced structured block (machine-consumable — codegen parses this)

```yaml
schema_version: 1
source_path: <rel>.dart
source_sha256: <sha at spec start>
target_code_unit: <rel>.cs            # shape only, NOT code
constructs:
  - construct_key: <normalised signature>
    source_form: <Dart>
    target_decision: <C#/.NET>
    idiom_id: <conversion_idioms.id | null if first-seen (then this row defines it)>
    research_finding_id: <research_findings.id | null if trivial>
    nuance: <explicit Dart→C# difference addressed, e.g. Stream→IAsyncEnumerable,
             value-vs-reference, null-safety mapping>   # REQUIRED if non-trivial
conversion_units: [ <ordered decomposed units the codegen stage will emit> ]
escalations: [ <see escalation schema> ]
```

### B. Embedded human-readable rationale + provenance (prose)

Per non-trivial construct: why this decision, the official-doc citation
(FR-024), and any corroborating source — reviewable in a PR before code.

## Quality bar (SC-006 / FR-009 / FR-010)

Every file with ≥1 non-trivial construct MUST record, for *each* such
construct, **both** a deep-analysis basis and a researched-pattern basis (or
an `idiom_id` to an already-decided one). A well-known nuance (value vs.
reference, `Stream` vs `IAsyncEnumerable`, null-safety, isolate/async) MUST be
explicitly addressed, never glossed (spec US2 AS4).

## Escalation schema (FR-013/014)

```yaml
- kind: undecidable | idiom_vs_research_conflict | idiom_vs_idiom_conflict
  construct_key: <...>
  detail: <what could not be decided / what conflicts>
  needs: <what a human must decide>
```

Any escalation ⇒ `open_escalation_count` > 0 ⇒ conversion blocked for that
file (NOT specing); aggregated into `.codeconv/conversion-idioms/
_escalations-report.md`.

## Tombstone YAML delta (append-only)

Appends `convspec_started_at｜convspec_completed_at｜spec_path｜
convspec_open_escalation_count｜builder_outer_workflow_id｜
builder_file_state` AFTER 017's keys (canonical YAML, sorted, pinned order).
Artifact *content* is never mirrored to YAML. Idempotence proof:
stamp→rebuild→stamp is a fixed point (012/014/015/017 carry-forward).
`test_tombstone_stamp_rebuild.py` asserts it.

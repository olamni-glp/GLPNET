# Contract — codegen artifacts

## A. Generated code unit — `out/csharp/<rel>.cs`
- Real, compilable C#/.NET 10 for one source file, at its scaffolded target path (per the dart_csharp langpair: `.dart`→`.cs`, `bin/`→`*.Cli`).
- **Validation (the INVERSE of the convspec rule)**: `artefact.py::validate_generated` REQUIRES real C# — a fenced/marker-free C# source file with the expected top-level construct(s) named in the plan's `target_code_unit`/conversion-units. It REJECTS: spec-style prose-only files, leftover Dart, or empty stubs. Build-gate is the ultimate validator.
- Derived strictly from the file's ratified plan + convspec + dependency interfaces + idiom KB; honors recorded conventions (`*Error` names; `getX→LookupX`).

## B. Optimized-prompt artifact — `.codeconv/codegen-prompt/optimized.md`
- The single optimizer→production handoff. Structure: a fenced ```yaml provenance block (`schema_version`, `optimizer` = gepa version, `metric_score`, `dataset_hash`, `generated_at`, `model`) + the optimized codegen instructions as markdown prose.
- `prompt.py::load()` reads it for the production codegen sub-agent. Checked in. Idempotent: only `codegen-opt export-prompt` writes it. If absent, the production sub-agent uses the baseline instructions shipped with the skill (and `status` warns).

## C. Codegen escalations report — `.codeconv/conversion-code/_escalations-report.md`
- Aggregated open codegen escalations (FR-009). Counts MUST equal `Σ dart_codegen.open_escalation_count > 0`. Each escalation: `kind` (`undecidable｜build_unrecoverable｜dependency_missing`), `path`, `detail`, `needs`.

## Escalate-don't-guess
A construct whose faithful C# cannot be produced from plan+convspec+idioms, or a build error not resolvable within the plan, ⇒ a structured escalation; NEVER a guessed translation or a silently-accepted non-compiling file (FR-007; DISCIPLINE §1.2/§1.10).

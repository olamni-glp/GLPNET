---
name: "bk-proof"
description: "Formal safety & deadlock-freedom proof pipeline (spec-040): a research-first, honesty-gated prover/verifier working from a repo-root proofs/ dir. Stages: gated literature corpus (deterministic index, fail-loud quality gate) → engineer-approved decision record (research-first HARD gate) → repo/fragment inventory at a frozen subject rev (unavailable-with-reason, never silent) → formal model with rev-pinned traceability anchors (dangling anchor ⇒ affected checks REFUSE, no verdict) → per-property bounded checks under a declared time/memory budget via the fully-vendored pinned toolchain (vendor-verify presence+SHA-256 gate reassembles >49MB chunked blobs; no network ever) → deterministic verdicts.json (content-hash run_id; PASS always carries scope+bounds and, when bounded, a gap-ledger ref — a bare PASS is a contract violation; FAIL carries a counterexample finding and is NEVER resolved by narrowing; budget-hit ⇒ honest UNPROVEN+reason) → dogfood report answering 'proved? over exactly what scope?' per property from the report alone. Verdict/gate/run events mirror additively into proof_* catalog rows + co-observability, fail-safe. Advisory & honest-by-construction: never auto-invokes a /buildkit-* feature-pipeline command, never weakens a model/property after a FAIL without an append-only gap-ledger disposition row."
argument-hint: "[a natural-language request, or a subcommand: corpus index|gate research-first|inventory|verify-anchors [--sample N]|check [--property ID] [--config NAME]|report dogfood|vendor verify|vendor split <artifact>|pipeline <stage>|version]"
compatibility: "Requires spec-kit project structure with .specify/ directory"
metadata:
  author: "buildkit"
  source: "templates/commands/buildkit-proof.md"
user-invocable: true
disable-model-invocation: false
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty). It is either a
natural-language request ("check the kernel-spine properties", "is COMMIT-ATOMIC proved?",
"regenerate the dogfood report") or a `buildkit-proof` subcommand. If empty, summarise the
surface below and ask what they want.

## What this does

`/bk-proof` runs a **formal proof pipeline** over a proof feature rooted at
`proofs/NNN-<slug>/` in the target repo: corpus quality gate, research-first decision gate,
frozen-subject inventory, anchor validation, bounded property checks through the vendored
toolchain, and the dogfood report. Its defining property is **honesty by construction** —
gates run before verdicts, every verdict carries its scope, and nothing is ever silently
weakened.

## Surface

**Gates (always run first — a failed gate means NO verdict is emitted)**
- `vendor verify` — pinned tools present + SHA-256-checked, offline; reassembles chunked
  blobs (`PINS.json chunked[]`) to gitignored originals and hash-checks the result.
- `verify-anchors [--sample N] [--seed S]` — every model element's `repo@rev:target`
  anchors resolve against the frozen inventory; a dangling anchor FAILS loudly (exit 1)
  and the affected properties are REFUSED, never silently passed.

**Research-first (FR-002/FR-004)**
- `corpus index` — deterministic corpus index + fail-loud quality gate over `corpus/`.
- `gate research-first` — present/decide the engineer hard gate; appends `proof_gate_events`.

**Model & checks (FR-005…FR-012)**
- `inventory [--declared-repos <json>]` — fragment inventory at the frozen rev;
  an absent repo is recorded `unavailable` WITH reason (honest, never an error swallow).
- `check [--property ID] [--config NAME] [--budget-seconds N] [--budget-mb N]` — per-property
  isolated bounded runs (single worker, fixed seed, generated per-property configs so one
  FAIL can never mask another). Exit 0 all-clean / 3 FAILs present / 4 UNPROVEN present /
  1 refused-by-gate. Full-suite runs write `verdicts.json` (deterministic content-hash
  `run_id` — SC-006: identical inputs ⇒ byte-identical output) and mirror
  `proof_runs`/`proof_verdicts` rows fail-safe.
- `report dogfood` — `dogfood-report.md` with the six contract sections; every property
  present; bounded PASS always rendered with its bounds; replay instructions included.

**Pipeline verbs (FR-016/FR-018)**
- `pipeline specify|clarify|plan|tasks|implement|review|ship|release|close` — stage the
  proof feature (writes `proofs/NNN-<slug>/` + `proof_stage_events`; default-deny re-entry
  without `--force`).

Every subcommand accepts `--json` (a `{"schema_version":"1", …}` envelope), `--proofs-root`
(default `proofs/`), and `--feature` (proof-feature dir, e.g. `001-yngenios-kernel-spine`).

## Honesty rules (non-negotiable, enforced by the tool — repeat them, never work around them)

1. **Gates before verdicts**: vendor-verify and verify-anchors REFUSE the run before any
   check executes; there is no flag to skip them.
2. **Scoped PASS only**: a bounded PASS must cite its named bound config's constants and a
   gap-ledger ref (clarify Q1). Rendering a bare `PASS` is a contract violation.
3. **FAIL is a finding**: a counterexample produces `findings/<property>-<n>.md` (trace +
   replay narrative + anchors + escalation status). A genuine subject bug escalates to
   bk-backlog. The model/property is NEVER narrowed post-FAIL without an append-only
   gap-ledger disposition row (FR-009/FR-010).
4. **UNPROVEN is honest**: budget exhaustion or engine error yields UNPROVEN with the
   reason and the declared budget — never a silent skip, never a retry-until-green.
5. **ENV-SKIP is a defect**: with a fully-vendored toolchain, a missing vendorable tool is
   itself a finding to surface, not an excuse.

## Boundaries

Advisory w.r.t. the feature pipeline: `/bk-proof` **never** auto-invokes
`/bk-specify`, `/bk-plan`, `/bk-tasks`, `/bk-implement`, `/bk-ship` or any other
`/buildkit-*` command — the
engineer runs those. It never mutates `feature_pipeline`/DBOS state; `proof_*` persistence
is additive-only and fail-safe (air-gapped operation keeps `verdicts.json` authoritative).
Secrets are redacted before any persist or send.

**Registry upkeep (spec-028 FR-004)**: run
`python -m buildkit_cli.registry touch --tool buildkit-proof` from the project root. It marks
the capability registry possibly-stale and **always exits 0** (fail-safe; never blocks). Ignore output.

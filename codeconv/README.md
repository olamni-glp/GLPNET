# codeconv

The single Dart→C#/.NET conversion toolchain for glpnet — a DBOS-durable
runner over the unified PGLite cluster at `<repo>/.pgdb/`. Every tool is
auto-discovered (feature 012 FR-006): a subpackage under
`src/codeconv/tools/<name>/` exporting `app: typer.Typer` becomes a
`codeconv <name>` subcommand, no CLI edits required.

## Install

```
python -m venv codeconv/.venv
codeconv/.venv/Scripts/python.exe -m pip install -e "codeconv[dev]"   # +[opt] for the optimizer
```

## Pipeline (the conversion stages, in order)

```
init → discover → mirror → depgraph → scaffold → convspec → plan │ codegen
        └─────────── `builder` auto-runs through `plan` ──────────┘   │
                                          codegen-opt (offline) ──────┘ feeds the prompt
```

`builder` auto-runs the pipeline through `plan`. **codegen is a
separately-driven phase** (decision B, 2026-05-23): the durable `codegen`
step is registered + replay-safe but is NOT in the builder's default
sequence — drive it via `/codeconv-codegen` (its build + human-review
gate), so a `builder run` keeps its clean "completed-at-plan" semantics.

| Tool (`codeconv <name>`) | Skill | Purpose |
|---|---|---|
| `init` | `/codeconv-init` | Configure the workspace (language pair, source/target paths, exclusions). |
| `discover` | `/codeconv-discover` | Inventory every `.dart` into the `codeconv` schema + `.codeconv/tombstones/`. |
| `mirror` | `/codeconv-mirror` | (Re)generate the inventory subtree from the source tree. |
| `depgraph` | `/codeconv-depgraph` | Topo/SCC dependency graph + conversion-readiness oracle. |
| `scaffold` | `/codeconv-scaffold` | Mirror the in-scope tree into the target location (`.dart`→`.cs`). |
| `convspec` | `/codeconv-convspec` | Per-file deep source analysis → reviewable conversion **spec** (spec-only). |
| `planagents` | `/codeconv-planagents` | Per-file conversion **plan** generation. |
| `builder` | `/codeconv-builder` | One resumable, DBOS-durable command driving the whole pipeline. |
| **`codegen`** | **`/codeconv-codegen`** | **Deterministic, build-gated Dart→C# code generation (feature 019).** |
| **`codegen_opt`** | **`/codeconv-codegen-opt`** | **OFFLINE GEPA/DSPy optimizer for the codegen prompt (feature 019).** |

## codegen (feature 019)

The build-gated, escalate-don't-guess **production** stage. Owns all
deterministic state in `codeconv.dart_codegen` (migration `0007`):

- `codeconv codegen status` — readiness + lifecycle counts (`not_started`/
  `codegen_ready`/`in_progress`/`built`/`converted`/`escalated`), stale list.
- `codeconv codegen next [--limit 7] [--include-tests]` — next codegen-ready
  batch as JSON (deps codegen-complete; SCC = one unit).
- `codeconv codegen ingest <path> [--respec] [--increment 1|2]` — validate the
  produced `.cs` is **real C#** (the inverse of convspec's spec-only rule), run
  the `dotnet build` (Inc-2: `dotnet test`) **hard gate**, two-phase write.
  Returns `built｜needs_agent_work｜escalated`.
- `codeconv codegen record-review <batch> --file <p> --score <1-5>` /
  `codeconv codegen promote-batch <batch>` — sampled human-review +
  promotion gate (100% build + median ≥ 4/5).
- `codeconv codegen retry <path>` — re-open a stale/failed file (never deletes).
- `codeconv codegen aggregate-escalations` — write
  `.codeconv/conversion-code/_escalations-report.md` (FR-009).

The `/codeconv-codegen` skill carries the codegen sub-agent + human-review
orchestration loop (the Python CLI stays deterministic and LM-free).

**Generated code lives at `out/csharp/<rel>.cs` and IS committed** (R11,
resolved 2026-05-23) — reviewable, build-gated product, parallel to the
checked-in convspecs/plans.

## codegen_opt (feature 019, offline)

The **only** LM-backed, **non-durable** tool — never a DBOS stage, never
imported by the production codegen path (R3/R10). It optimizes the codegen
prompt against the composite build/test/human metric on a held-out eval set:

- `codeconv codegen_opt optimize [--budget N] [--model M] [--eval-size K]` —
  budget-capped GEPA; serializes the best-so-far candidate.
- `codeconv codegen_opt eval` / `export-prompt` / `show`.

Reads `OPENAI_API_KEY` (here only). Its sole production output is the
optimized-prompt artifact `.codeconv/codegen-prompt/optimized.md`, which the
production codegen sub-agent loads via `prompt.load()` (baseline if absent).
Transmits Dart source/plans to OpenAI — the accepted offline-only tradeoff;
the production path transmits nothing.

## Tests

```
codeconv/.venv/Scripts/python.exe -m pytest codeconv/tests/ -q   # serial; @needs_bridge gated
```
PGLite cold-init is ~7 s; the suite runs serially (single-writer lock).

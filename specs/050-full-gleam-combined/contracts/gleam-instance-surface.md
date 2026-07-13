# Contract: Gleam instance surface (M1)

The externally observable surface of the standalone Gleam GLP instance. Reference behaviours: Dart REPL (`glp_runtime/bin/glp_repl.dart`) and C# REPL (`out/csharp/glp_repl/`). Where this contract is silent, the Dart instance's observable behaviour is normative (Dart = source of truth); divergences are reported, not improvised.

## Load pipeline (FR-001..003)

- Entry: `load <path>.glp` (REPL) or programmatic `load(engine, path)`.
- Stage order, fixed: parse → SRSW check → partial evaluation → type check → compile(v2.16) → load.
- Success: module procedures become callable; response format matches reference (`✓ Loaded ...`).
- Failure: staged diagnostic naming the stage, source location, and reason; later stages do not run. Rejection classes (parse error, SRSW violation, type error, guard violation) must match the reference runtime's classification for corpus negatives (sections C/D/E shape).
- Single tool discipline: there is no standalone checker/compiler executable — the pipeline is only reachable through load (same as the reference instances).

## REPL commands (FR-008)

| Command | Behaviour |
|---|---|
| `load <path>` / bare path | run the load pipeline |
| `<goal>.` | execute goal against loaded program; report outcome |
| `:trace` | toggle reduction tracing (reference trace line shape) |
| `:limit <n>` | set reduction budget; exhaustion reports the reference way |
| `:quit` | exit 0 |

Outcome reporting: success (with deep-resolved bindings), suspension (goal list), failure — same user-visible classification as the reference REPLs. Scripted use: newline-fed stdin (pipe), non-interactive exit.

## Engine as typed value (FR-009, ED-1 seam)

- `Engine` is an opaque Gleam value; `new() -> Engine`, `load(Engine, source) -> Result(Engine, StagedError)`, `run(Engine, goal) -> (Engine, ResultEnvelope)`, `step(Engine) -> (Engine, Event)`.
- No process dictionary, no global ETS state for engine semantics (transport processes excepted).
- Every goal result — REPL-consumed or link-delivered — is a `ResultEnvelope` built by the 038 builder (deep-resolve, canonical ordering, output capture). In-process and over-the-wire envelopes for the same computation are byte-identical after encoding (SC-004 corollary).

## Acceptance hooks

- Corpus runner drives this surface exactly as it drives the Dart REPL (piped commands), so goldens diff cleanly (contracts/corpus-parity.md).
- Engine-value API is the unit-test seam for the adversarial writer-MGU suite (contracts/proof-obligations.md).

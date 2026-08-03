# Contract — Gleam FE/BE process split + embeddability (US4, BUILD ruling)

## Process roles

- **BE (back end)**: a Gleam OS process hosting engine+scheduler, serving the split protocol (including the US3 IL kinds where applicable; text kinds at minimum) over the Gleam TCP transport on a configured port. Lifecycle: start → listening → serving → drain → exit; exit code taxonomy mirrors the C# engine host.
- **FE (front end)**: a thin Gleam REPL loop speaking the split protocol; no engine, no compiler beyond what the protocol path requires; command surface = the existing REPL commands.

## Wire

The frames are the existing split-protocol frames (single seam definition, shared with C#). Consequence to verify: the Gleam FE can drive the C# BE and vice versa for the text-kind subset (one cross-runtime smoke each direction).

## Embeddability (G3-A)

`glp_embed` public surface: `load(project_path) -> EmbedHandle`, `run(handle, goal) -> ResultStream`, `observe(handle) -> events`. Host contract: a minimal BEAM host program in the test tree loads a project, runs a goal, receives the documented outcome. No REPL, no stdin.

## Acceptance

1. FE+BE across two OS processes run the standard REPL scenarios; results equal the single-process REPL for the regression corpus (US4 scenario 1).
2. Cross-runtime smoke: Gleam FE ↔ C# BE and C# thin client ↔ Gleam BE (text kinds).
3. Embed host program runs green in CI (gleeunit-driven).
4. BEAM socket paths follow FR-012 (`{exit_on_close, false}`, D-9 barrier, dial-retry).

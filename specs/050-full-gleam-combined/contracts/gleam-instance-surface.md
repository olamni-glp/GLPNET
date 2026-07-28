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

## Host-embedding API (T068 · T069)

The host-callable surface over the pure `Engine` value, decoupled from any UI/REPL (the yngenios embedding hook; matches the Dart `glp_engine.dart` baseline). A host drives the engine through `glp/engine` alone — never a `glp/repl/*` module.

- **Construct + prelude injection** (T068 prelude-injection seam): `new() -> Engine` reads the on-disk root `self.glp`; `new_with_prelude(source) -> Engine` injects a caller-supplied prelude (CWD-independent). The prelude is compiled *without* a user-style type check (`compile_prelude`), so — exactly as `self.glp` itself calls host kernels the type checker does not recognise (`'_now'`, `'_add'`) — an injected prelude may define wrapper procedures that call host kernels (`double(X, Y?) :- '_host_double'(X?, Y).`).
- **Configure** (T068): `configure(Engine, EngineConfig) -> Engine` replaces the tunable configuration; `config(Engine) -> EngineConfig` reads it. `EngineConfig` carries `reduction_budget` (per-goal instruction budget), `fuel` (total-reduction backstop; Dart `maxCycles`), `trace` (reduction-trace default), and `host_kernels`. `default_config()` is the historical defaults. The config-driven `run` honours `fuel` + `trace`; the explicit `run_with_limit*` variants still take an ad-hoc fuel for the REPL `:limit`.
- **Kernel injection / composition root** (T069): `register_kernel(Engine, name, arity, HostKernel) -> Engine` adds a host-supplied body kernel to the config. A `HostKernel` is `fn(Heap, List(Term)) -> Result(HostKernelOutcome, String)` (the same three data channels a built-in kernel's `KSuccess` threads out: updated heap, reactivations, output lines). At a BODY Spawn label-miss the runner consults the injected table by `(name, arity)` — **after** the built-in pure/effectful kernels, so a host *extends* the kernel set without shadowing a built-in — and **never names any host kernel** (the "injected onto a live engine, never referenced by it" discipline). A host abort surfaces loudly (a `Failed` run), never a silent success. No global state — the table lives on the `Engine` value (FR-009).

## Acceptance hooks

- Corpus runner drives this surface exactly as it drives the Dart REPL (piped commands), so goldens diff cleanly (contracts/corpus-parity.md).
- Engine-value API is the unit-test seam for the adversarial writer-MGU suite (contracts/proof-obligations.md).

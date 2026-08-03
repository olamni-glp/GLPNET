# US3 checkpoint review note (T025) — 2026-08-03

## Delivered (commit 74fce876)

LOAD_IL (0x07) / RUN_GOAL_IL (0x08) / IL_REFUSED (0x86) on the split protocol wrapping the unchanged 062 CompiledIlEnvelope; client-side compile+ship (`--path il`, IlSession over a local suppressed-activation GlpEngine); engine-side compiler-free `IlExecutePath`; per-session text/IL lock with loud PathViolation (no silent fallback); refusal taxonomy verbatim from 062. New GlpSplitProtocol.Tests: 46/46 incl. corpus equivalence 12/12 (multi_client_control.glp included, non-vacuous status anchoring). Zero regression: link 171, engine_host 69, il_codec 64, wire_registry 6.

## Documented deviations (accepted at this checkpoint, carried to T038/DEFERRALS review)

1. **FR-006 "no compiler reference on the execute path" is enforced at TYPE level, not assembly level**: glp_runtime_net is monolithic (runtime sources reference GlpRuntime.Compiler) and the text kinds legitimately keep the engine-side pipeline during the contract's deprecation window. The boundary is `IlExecutePath` — `CompilerAbsenceTests` walks its IL bodies asserting zero references into GlpRuntime.{Compiler,Analysis,Engine,Multiagent}, plus csproj assertions. A project-level split of glp_runtime_net is recorded as a candidate future feature, NOT forced here.
2. **IL-path bindings are not rendered** (compiled goals carry no query vars); the text path retains bindings through the deprecation window. Contract-consistent; noted for the eventual text-kind retirement decision.
3. **IL-session loaded state is outside the 061 snapshot/quiescence machinery** — the contract is silent on snapshots; explicit deferral candidate for DEFERRALS.md (T038).

## Verdict

US3 acceptance scenarios 1 and 2 both green; checkpoint PASSES with the three recorded deviations above.

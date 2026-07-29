# Suite Baseline — 061 Wave 2 (T005, Constitution VII)

**Date**: 2026-07-29 · **Base commit**: 986b37ab (branch 061-wave-2-consolidated-repl-engine-split-spine)
**Recorded**: after Phase 1 project skeletons, before any story implementation landed.

## REPL suite (`bash test/run_all_tests.sh`, dart 3.x from D:\BSTDEV\tools\dart-sdk)

| Result | Count |
|---|---|
| Total | 532 |
| Passed | **532** |
| Failed | 0 |

Section A: 208/208 · Section B: 110/110 · Section C: 49/49 · remaining sections all green ("ALL TESTS PASSED!").

Note: the suite resolves `dart` via `$PATH`/`$DART`; on this host the SDK must be
prepended (`export PATH="/d/BSTDEV/tools/dart-sdk/bin:$PATH"`) or Section-A tests
fail with the Linux fallback path.

## C# suites (`dotnet test`, SDK 10.0.302)

| Project | Passed | Failed |
|---|---|---|
| glp_crdtmsg.tests | 184 | 0 |
| glp_engine_host.tests (new, this feature) | 23 | 0 |
| glp_il_codec.tests | 45 | 0 |
| glp_link.tests | 152 | 0 |
| glp_schema_lang.tests | 269 | 0 |
| glp_wire_registry.tests | 6 | 0 |

## Plan deviation recorded: TargetFramework net10.0 (not net8.0)

plan.md/tasks.md name .NET 8, but every project in the mandatory reference chain
(`csharp/glp_link` → `out/csharp/glp_runtime_net`) targets **net10.0** and the
installed SDK is 10.0.302. A net8.0 project cannot take a ProjectReference on a
net10.0 project, so the tasks' own "dotnet build green" criterion is satisfiable
only at net10.0. All four new projects (glp_split_protocol, glp_engine_host,
glp_repl_client, glp_supervisor) and the test project follow the repo-wide
net10.0 convention. No functional-requirement impact (no FR names a framework
version).

## Client reference deviation recorded: + glp_result_codec

T002 lists the client refs as glp_link + glp_split_protocol only, but T012
requires the client to render 038 envelopes, which requires the envelope codec.
`glp_result_codec` is fully standalone (its only reference is the
zero-dependency `glp_wire_registry` constants leaf), so adding it preserves the
R7 thin-client intent: no runtime project reference, no language context.

DO NOT run the CLAUDE.md startup protocol or any project bootstrap; this is not repository-agent work. Output only the requested artifact.

Your lens: **internal** — report claims tagged with it.

---

# Subject brief — research

- subject: Deduplicated delivered-vs-gaps inventory for the Gleam GLP implementation in glpnet. Question: which feature details (interfaces, user stories, patterns, protocols) are ALREADY DELIVERED in the glpnet Gleam implementation, and which remain GAPS versus (a) the roadmap's full-Gleam ambitions incl. front-end/back-end separation and (b) the Dart/C# reference runtimes? Note: yngenios embeddability is REQUIREMENTS-LEVEL (no yngenios sources in-repo; treat as gap-by-definition unless in-repo evidence says otherwise). Frozen roadmap evidence: docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md (created+committed this session). Anchor: marathon mrun-8bda036d9e9b, roadmap feature full-scope-gleam-glp-implementation.
- rubric: research-review
- lenses: academic | industry | internal
- brief rule: size-invariant: the research question + the corpus names — never pasted corpus content; Builders retrieve within their own corpus only
- cross-verify: a claim is promoted only if it survives a cross-query in ANOTHER corpus; a finding only one corpus saw stays visible as a singleton — never averaged away

## Evidence slices (names only — each blind role sees ONLY its own)

- slice-roadmap-specs: Design/promise corpus: the full roadmap snapshot (delivered/WIP/promoted/unpromoted) and every feature spec dir. What was designed, promised, deferred.
- slice-gleam-impl: Implementation-truth corpus: the actual Gleam code delivered in glpnet — glp_gleam runtime subtree (terms/heap/unification/codec/transports) and gleam_quic profile work, including tests.
- slice-reference-runtimes: Full-scope reference corpus: what a COMPLETE GLP implementation delivers — the Dart runtime (REPL, compiler, bytecode runner, multiagent), the C# runtime, the normative language/runtime/bytecode docs, and the GLP program corpus.

---

## Your evidence slice: slice-reference-runtimes

Full-scope reference corpus: what a COMPLETE GLP implementation delivers — the Dart runtime (REPL, compiler, bytecode runner, multiagent), the C# runtime, the normative language/runtime/bytecode docs, and the GLP program corpus.

Sources (yours ALONE — do not consult anything outside this list):

- glp_runtime/lib/
- glp_runtime/bin/
- csharp/
- docs/typed-glp-manual.md
- docs/glp-cheat-sheet.md
- docs/glp-runtime-spec.txt
- docs/glp-bytecode-v216-complete.md
- programs/

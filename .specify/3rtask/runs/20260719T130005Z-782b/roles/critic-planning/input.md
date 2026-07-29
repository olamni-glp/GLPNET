DO NOT run the AGENTS.md startup protocol; this is not repository-agent work. Output only the requested artifact.


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

## Method under red-team (the artifact ONLY — no author reasoning)

{
  "elements": [
    {
      "id": "E1",
      "kind": "question",
      "text": "Core question: for the glpnet Gleam GLP implementation, which feature details (interfaces, user stories, patterns, protocols) are ALREADY DELIVERED, and which remain GAPS versus (a) the roadmap's full-Gleam ambitions including front-end/back-end separation and (b) the Dart/C# reference runtimes? yngenios embeddability is requirements-level: no yngenios sources exist in-repo, so it is a gap-by-definition unless in-repo evidence shows a delivered embeddability surface."
    },
    {
      "id": "E2",
      "kind": "procedure",
      "text": "Enumeration procedure (all builders, cycle 1): sweep your OWN corpus only, breadth-first — first list every top-level area (directory, spec dir, doc, module) in your slice, then descend one level per pass until the whole corpus has been touched. From each area, extract feature-details at four grains: (1) interfaces (public functions/types/CLI/REPL commands/wire formats), (2) user stories (what a user/agent can accomplish end-to-end), (3) patterns (architectural mechanisms: suspension, SRSW, three-phase execution, process split, supervision), (4) protocols (framing, handshake, transport, link, bytecode format). One claim per feature-detail. Never infer content of another corpus; testify only to what your corpus shows."
    },
    {
      "id": "E3",
      "kind": "rubric",
      "text": "Claim format discipline: every claim is exactly one feature-detail with: detail_id (per E4 naming rule), grain (interface|user-story|pattern|protocol), status as seen FROM THIS CORPUS (designed|delivered|partial|absent|deferred), tag (per E6), one-sentence statement, and citation — an absolute repo path (file, or file:line-range, or spec section) inside the builder's own slice. Claims without a citation path are invalid and must be dropped by the merger. 'partial' requires naming what part is present and what part is missing in the statement."
    },
    {
      "id": "E4",
      "kind": "rubric",
      "text": "Dedup-key naming rule (deterministic, so blind builders converge): detail_id is lowercase kebab-case; start with the subsystem noun, then the capability; no version numbers, no corpus-specific prefixes, no runtime-name prefixes (never 'gleam-' or 'dart-'); singular nouns; prefer the shortest widely-used term for the subsystem. Canonical seed vocabulary all builders MUST reuse when applicable: term-representation, term-heap-unification, suspension-scheduler, srsw-check, three-phase-execution, guard-kernel, body-kernel, frame-codec, crc32-checksum, loopback-transport, tcp-transport, quic-transport, link-layer, mesh-ring, repl-loop, compiler-pipeline, bytecode-runner, partial-evaluator, type-checker, module-system, prelude-library, multiagent-runtime, fe-be-process-split, embeddability-api, distribution-protocol, test-harness. Derive new keys by the same rule (e.g. frame-codec-length-prefix, quic-transport-profile-c). When unsure between two names, pick the one closest to a seed term."
    },
    {
      "id": "E5",
      "kind": "procedure",
      "text": "Coverage rule: each builder must account for 100% of its corpus areas. Cycle-1 output must include a coverage manifest: every top-level area of the slice, each marked swept|partially-swept|not-swept, with a one-line reason for anything not fully swept (size, unreadable, binary, out of budget). Nothing may be silently dropped — an unswept area is itself reportable so the Curator can see the blind spot. Vendored/build artifacts (_build, .dart_tool, packages caches) are declared out-of-corpus in the manifest, not swept."
    },
    {
      "id": "E6",
      "kind": "rubric",
      "text": "Tagging rule, fitted to what each corpus can testify: builder-2 (impl corpus) may assert delivered-gleam (code + passing tests exist) or partial (code exists, incomplete/untested) — it may NOT assert gap-gleam except for explicit in-code TODO/stub evidence. builder-1 (specs corpus) asserts design-promise (promised, WIP, promoted, or deferred per the roadmap snapshot and spec dirs) and may mark a promise as claimed-delivered only when the spec/handover corpus itself records completion. builder-3 (reference corpus) asserts reference-capability (a capability the full-scope Dart/C# runtimes + normative docs deliver, hence required for parity). gap-gleam is primarily a MERGE-derived tag: reference-capability or design-promise with no matching delivered-gleam claim. Builders never guess across corpora; the mechanical merge computes gaps by detail_id set-difference, and yngenios embeddability-api enters the merge as gap-gleam by the E1 rule unless a delivered-gleam claim for it exists."
    },
    {
      "id": "E7",
      "kind": "procedure",
      "text": "Cycle-2 procedure (re-sweep, strictly within own corpus): each builder (a) re-verifies its own cycle-1 singletons at higher precision — tighten citations to file:line, upgrade/downgrade status where the closer read warrants, and explicitly retract anything that does not survive re-reading; and (b) answers any standing questions from E9/questions list left uncovered in cycle 1, prioritizing areas marked partially-swept/not-swept in its coverage manifest. No new broad sweeps of already-verified areas; no peeking at other corpora or the merge output beyond the list of which of its OWN claims are singletons. A cycle 3 runs only if cycle 2 produced retractions or new claims touching the merge's escalations, within the E10 budget."
    },
    {
      "id": "E8",
      "kind": "procedure",
      "text": "Mechanical merge contract (Critic): join claims across corpora by exact detail_id; near-miss keys (edit distance or shared subsystem-noun prefix) are surfaced as merge-candidates, never silently unified. Outcomes: corroborated (same detail_id, compatible statuses across corpora), singleton (one corpus only — kept visible, never averaged away), conflict (same detail_id, incompatible statuses, e.g. impl says delivered vs specs says deferred) → ESCALATE verbatim with both citations. Delivered-vs-gap verdict per detail_id: delivered-gleam claim present → DELIVERED (with corroboration level); reference-capability or design-promise present but no delivered-gleam claim → GAP; partial → PARTIAL with the named missing part."
    },
    {
      "id": "E9",
      "kind": "rubric",
      "text": "Per-builder question routing: builder-1 answers Q3, Q5, Q6 from specs/roadmap only (what was promised, deferred, and what FE/BE separation and embeddability were DESIGNED to be); builder-2 answers Q1, Q4, Q5, Q6 from code only (what runtime/transport surfaces EXIST in Gleam, what FE/BE or embeddability surfaces exist in code); builder-3 answers Q2, Q7 from the reference runtimes and normative docs only (what full scope requires). Q8 (test-parity) is answered by builder-2 for Gleam and builder-3 for the reference suites."
    },
    {
      "id": "E10",
      "kind": "budget",
      "text": "Budget: 3 builders, 2 cycles standard (3rd cycle only per E7 trigger), hard cap 3 cycles; 600k tokens total for the whole team, indicative split 140k per builder cycle-1, 40k per builder cycle-2, 60k merge+synthesis reserve; per-role per-cycle token rows recorded in the ledger; warn-and-confirm before exceeding any per-builder allotment."
    },
    {
      "id": "E11",
      "kind": "output-contract",
      "text": "Output contract: each builder emits one JSON object {\"builder\": \"builder-N\", \"cycle\": n, \"claims\": [{\"detail_id\": \"kebab-key\", \"grain\": \"interface|user-story|pattern|protocol\", \"status\": \"designed|delivered|partial|absent|deferred\", \"tag\": \"delivered-gleam|gap-gleam|reference-capability|design-promise\", \"statement\": \"one sentence; for partial, names present part and missing part\", \"citation\": \"absolute path[:lines] or spec section within own slice\"}], \"coverage_manifest\": [{\"area\": \"path-or-doc\", \"state\": \"swept|partially-swept|not-swept\", \"note\": \"...\"}], \"questions_answered\": [\"Q1\", ...], \"retractions\": [\"detail_id\", ...]} — retractions present from cycle 2 onward. Final Curator report groups by verdict (DELIVERED corroborated, DELIVERED singleton, PARTIAL, GAP-vs-roadmap, GAP-vs-reference, ESCALATED conflicts) with every entry citing its source claims."
    }
  ],
  "source_partition": {
    "slice-roadmap-specs": "builder-1",
    "slice-gleam-impl": "builder-2",
    "slice-reference-runtimes": "builder-3"
  },
  "questions": [
    "Q1: Which GLP runtime surfaces exist in the Gleam implementation today (term representation, heap/unification, suspension scheduling, guard/body kernels, codec, transports), and with what test evidence?",
    "Q2: Which capabilities exist ONLY in the Dart/C# reference runtimes — compiler pipeline, bytecode runner, REPL loop, type checker, partial evaluator, SRSW check, module system, prelude, multiagent runtime — that a full-scope Gleam GLP would have to match?",
    "Q3: Which roadmap/spec promises for full-Gleam (per the frozen snapshot and spec dirs) are recorded as delivered, WIP, promoted-but-unstarted, or explicitly deferred?",
    "Q4: What transport and link-layer protocols are delivered in Gleam (frame codec, length-prefix framing, CRC32, loopback, TCP, QUIC profile work, mesh/ring), and which are partial or stubbed?",
    "Q5: What front-end/back-end process-split surfaces exist — as designed in the roadmap/specs versus as actual code seams in the Gleam implementation?",
    "Q6: What embeddability hooks (host-embedding API, service/box boundary, store kernels, external control surface) exist in design or in code, given yngenios embeddability is gap-by-definition absent in-repo evidence?",
    "Q7: Which normative-doc requirements (bytecode v216 instruction set, runtime spec semantics, typed-GLP manual behaviors) define full-scope conformance targets that any Gleam runtime must eventually satisfy?",
    "Q8: What is the test-coverage parity picture — which behaviors are locked by tests in the Gleam corpus versus the reference suites (REPL suite sections, Dart unit tests, program corpus)?"
  ],
  "rubric_id": "research-review"
}

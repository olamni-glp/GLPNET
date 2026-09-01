# Curator report — /yx-ypm requirements, the three uncovered dimensions

**Run** `20260901T130939Z-0f7a` · research · 3 blind Builders · codex Critic (cross-provider,
no independence warning) · lane olamnit/glpnet · 2026-09-01.
**Authority:** engineer ruling `Q-GLPNETO19-01` — narrow this run to the dimensions no prior
corpus covered.

## Why this run exists, and what it deliberately does not re-derive

Two prior `/bk-3rtask` runs already covered polyglot package-manager design, version-resolution
algebra, supply chain / SBOM / PURL identity, and lockfile & diamond-dependency formalism —
gavriella's `20260901T114835Z-889f` (4 slices, 25 sources) and shiras' `20260901T115008Z-3b05`
(3 slices, 115 claims). A third pass over that literature would have re-derived their corpus.

The cheap-model input guardrail returned **WARN**, not OK, and named the actual gap: **operator
experience and diagnostics, governance and policy enforcement, and performance and scale** are
surfaced by *none* of the seven prior slices. This run covers exactly those three, with all 25
of the prior run's sources declared out of scope in every slice.

## Method integrity — what actually happened

- The planning Critic (codex, blind, saw only {subject brief + the method artifact}) returned
  **14 CONFIRM / 2 REFUTE** on the first draft. One refutation was decisive and correct: the
  draft **forbade web retrieval while demanding concrete locators**, which forces exactly the
  memory-based citation it banned — and the engineer's directive for this run was explicitly to
  perform topical web searches. That was my error in the Planner prompt, not the Planner's.
- The method is append-only, so the corrected elements could not be re-frozen into the first
  run. Run `20260901T125721Z-da50` is recorded **halted at `freeze-method`**, and this run
  carries the corrected **16-element method, 0 refutes, 0 escalates open**.
- Independence audit clean **before and after** Builder outputs; the post-output audit
  exercised all three Builders' sibling-output and content checks.

## Result

| | claims | CONFIRM | ESCALATE | REFUTE |
|---|---:|---:|---:|---:|
| builder-1 · U1 operator experience & diagnostics | 16 | 14 | 0 | 2 |
| builder-2 · U2 governance & policy | 20 | 15 | 5 | 0 |
| builder-3 · U3 performance & scale | 20 | 19 | 1 | 0 |
| **total** | **56** | **48** | **6** | **2** |

Mechanical merge: **56 combined, 0 corroborated, 56 singletons, 0 conflicts.**

**Zero corroboration is the expected and correct result here, not a defect.** The three slices
read genuinely disjoint literatures — CLI diagnostics, policy engines, build caches — so
requirements about different mechanisms cannot corroborate textually. This is the healthy
inverse of the false-corroboration failure: shared evidence fakes agreement, and there is none
here. Per the singleton rule every one of the 56 stays visible and none was averaged away.

## The one genuine cross-slice convergence

All three Builders were independently asked to test the carry-forward premise *"resolution is
NP-complete; PubGrub won on ERROR MESSAGES, not speed."* **All three independently BOUNDED it
rather than confirming or refuting** — from three unrelated bodies of evidence:

- **U1:** the premise holds for design intent but not for delivery — uv resolves *with* PubGrub
  and still carries `astral-sh/uv#309` ("Confusing error messages on package resolution
  failure") as a standing umbrella issue and `#15957` with a ~135,000-line explanation. A
  causal-derivation solver is **necessary but not sufficient**; the product surface is the
  rendering, ranking and truncation layer above it, and that is where the open defects sit.
- **U2:** the decisive variable is **delivery point and previewability, not message quality** —
  Infer went ~0% → ~70% fix rate purely by moving *identical, less precise* findings from a
  nightly batch to code-review time; Kubernetes retired PodSecurityPolicy, a fully capable
  mechanism, citing "no dry-run / audit mode" as why it could never be default-enabled.
- **U3:** partially counter — **uv's adoption case is argued almost entirely on raw measured
  speed**, with no explanation surface offered as the selling point.

Three slices, three literatures, one converged verdict: **the premise is real but under-stated
as "error messages". What decides adoption is being able to see and preview the decision before
being bound by it.** That is a stronger and more actionable finding than the premise it refines.

## 🔴 SIX OPEN ESCALATIONS — the ENGINEER's to resolve, not mine

The Critic correctly refused to adjudicate six rows because each commits the fleet to a
**governance or product posture** rather than turning on evidence admissibility. Per FR-004 I
record them and resolve none:

1. **Waiver default scope** — narrowest-scope-by-default (one package × one named rule), with
   widening separately authorised?
2. **Unbound policy** — must "no policy bound to this scope" ever deny, or only under an
   explicit deny-by-default opt-in?
3. **Unresolved licence** — per-usage approval record, or an organisation-wide allow?
4. **Deprecation asymmetry** — refuse new dependents while existing dependents keep building?
5. **Cache hit/miss counts** — in the default summary, or opt-in?
6. **Policy service unreachable** — fail open, fail closed, or evaluate against the last
   cryptographically verified bundle and report its age?

Items 2 and 6 are the fleet's risk posture in miniature and should be answered together.

## Two refutations, and one I record my disagreement with

- `YXR-U1-14` (mandatory outcome-classification line) — REFUTED as over-specific relative to
  its source. I accept this: the arXiv 17.2% misinterpretation figure supports *outcome clarity*
  but not that particular taxonomy.
- `YXR-U1-15` (acknowledge within 100ms) — REFUTED as "too specific to be plausibly supported by
  the cited general CLI guidance." **Curator note, recorded as disagreement and not overturned:**
  clig.dev states *literally* "Print something to the user in <100ms", so the citation does
  support the number. The Critic adjudicates and I do not overrule it, but the row should be
  re-put in a later cycle with the verbatim quote foregrounded.

## Honest limits of this run

- **MIN_CYCLES was 2 and only 1 cycle ran.** Budget-check said proceed with 220,000 tokens
  remaining, and a second three-Builder cycle costs roughly 265,000. I stopped rather than run a
  partial cycle or fabricate one. The cost is that **citation reproducibility was not
  independently re-derived** — the main thing a second cycle buys for a research run.
- **One Builder self-reported a retrieval caveat and it should be honoured:** the 70%/~0% Infer
  figure behind `YXR-U2-07` came from search-surfaced summaries, because ACM returned HTTP 403
  and the UCL manuscript did not decode. Its numeric component is medium-confidence pending
  direct verification of DOI 10.1145/3338112; its qualitative direction is independently
  supported by KEP-2579.
- **The Builders applied the numbers rule against their own interest**, which is the strongest
  evidence the method bit: Buck2's "2x faster than Buck1" and pnpm's "50–70% disk saving" were
  both **downgraded to observations** for naming no workload, hardware, graph size or cache
  temperature.
- **20 negative results were recorded across the three slices** — questions the corpora could
  not answer. Two matter for `/yx-ypm` design: no source in any slice reports how resolve time,
  memory or lockfile size degrades as the **number of distinct ecosystems** in one graph grows;
  and no source defines a **validated success metric** for governance — every indicator found is
  a failure detector.

## What this adds to the fleet convergence

This corpus is additive to CPM-CRDT v0.4 and `/yx-ypm` v0.1, not a competing draft. Its
strongest contributions are the escalations above (which the design cannot settle by itself),
the bounded form of the explanation premise, and the observation that **a uniform cross-ecosystem
layer loses native metadata and, lacking a third verdict, silently converts that loss into an
allow** — evidenced by `dependency-review-action#732`/`#612`, where an unresolvable licence
cannot be blocked at all.

---

# Appendix — the 56 requirements, with verdicts and citations
Status key: **CONFIRM** = admissible; **ESCALATE** = engineer's posture decision, unresolved; **REFUTE** = not admissible as written.

## U1 operator experience & diagnostics

**✅ CONFIRM** — /yx-ypm MUST derive every refusal explanation from a recorded causal chain that terminates in externally-observable facts (a declared dependency, an absent version, an unusable artifact), and MUST NOT emit a refusal whose stated cause it cannot trace to such facts.
  - source: PubGrub error-reporting design write-ups | dart-lang/pub doc/solver.md, 'Error Reporting' (https://github.com/dart-lang/pub/blob/master/doc/solver.md) | SPEC

**✅ CONFIRM** — /yx-ypm MUST publish, at the moment a compatibility flag is introduced, the specific release in which that flag will be removed, together with a migration guide and a named feedback channel.
  - source: Cargo/npm/pip error-message redesign discussions | PSF blog 'Releasing pip 20.3, featuring new dependency resolver', Nov 2020 (https://pyfound.blogspot.com/2020/11/pip-20-3-new-resolver.html) | SPEC

**✅ CONFIRM** — /yx-ypm MUST bound the length of the human-facing refusal it prints to the terminal and MUST place the complete derivation behind an explicit flag or a written file rather than printing it inline.
  - source: Cargo/npm/pip error-message redesign discussions | astral-sh/uv issue #15957 (https://github.com/astral-sh/uv/issues/15957) | REPORT

**✅ CONFIRM** — /yx-ypm MUST attach a machine-readable applicability level to each suggested fix it emits, and MUST refuse to auto-apply any suggestion not marked as mechanically applicable.
  - source: Diagnostic and error-taxonomy design | rustc dev guide, diagnostics chapter, Applicability levels (https://rustc-dev-guide.rust-lang.org/diagnostics.html) | SPEC

**✅ CONFIRM** — /yx-ypm MUST emit the full refusal - cause, conflicting parties, declared constraints, and remedy set - as structured JSON when --json is passed, with the same content as the human rendering.
  - source: CLI design guidelines | clig.dev 'Output' (https://clig.dev/) | SPEC

**✅ CONFIRM** — When operating offline, from a stale cache, or with an unreachable index, /yx-ypm MUST refuse rather than resolve against partial data, and the refusal MUST name the specific artifact whose retrieval was required.
  - source: Cargo/npm/pip error-message redesign discussions | rust-lang/cargo issue #12543 (https://github.com/rust-lang/cargo/issues/12543) | REPORT

**❌ REFUTE** — /yx-ypm MUST print an acknowledgement of the operation within 100 milliseconds and MUST show progress for any resolution that has not completed, rather than remaining silent while backtracking.
  - source: CLI design guidelines | clig.dev 'Robustness' (https://clig.dev/) | SPEC
  - critic: The 100 millisecond acknowledgement threshold is too specific to be plausibly supported by the cited general CLI guidance. The progress requirement is admissible, but the row as written overclaims the source.

**❌ REFUTE** — /yx-ypm MUST state the outcome classification of every run explicitly - refused, succeeded, or succeeded-with-degraded-guarantees - as a distinct labelled line rather than leaving it to be inferred from the presence of warning text.
  - source: Developer-experience research on build-tool adoption and abandonment | Huang, Meng, Liu, Wang, arXiv:2502.15912v1, Feb 2025 (https://arxiv.org/html/2502.15912) | MEASUREMENT
  - critic: The cited build-tool adoption paper may support clarity of outcomes, but it does not plausibly support the specific mandatory labelled-line taxonomy. The claim is over-specific relative to the source.

**✅ CONFIRM** — /yx-ypm MUST count and expose the rate at which operators invoke each override flag, per repository and per flag, as a first-class operational metric.
  - source: Cargo/npm/pip error-message redesign discussions | npm/rfcs discussion #334 (https://github.com/npm/rfcs/discussions/334) | REPORT

**✅ CONFIRM** — /yx-ypm MUST NOT name an override flag in a refusal without stating, in the same message, which specific check that flag disables and what incorrect state may result.
  - source: Post-mortems where an unexplained dependency refusal led to escape hatches | npm/cli issue #2000 (https://github.com/npm/cli/issues/2000) | REPORT

**✅ CONFIRM** — /yx-ypm MUST terminate an unbounded search with a distinct, named error that states the search bound was exceeded and lists concrete narrowing actions, and MUST NOT present that outcome as an ordinary conflict refusal.
  - source: Cargo/npm/pip error-message redesign discussions | pip docs 'Handling resolution too deep errors' (https://pip.pypa.io/en/stable/topics/dependency-resolution/) | SPEC

**✅ CONFIRM** — /yx-ypm MUST include, in every resolution refusal, each conflicting party's declared version requirement string verbatim and the manifest that declared it, not only the resolved package-version chain.
  - source: Cargo/npm/pip error-message redesign discussions and issue threads | rust-lang/cargo issue #6199 (https://github.com/rust-lang/cargo/issues/6199) | REPORT

**✅ CONFIRM** — /yx-ypm MUST write a durable report artifact for every refusal and MUST print that artifact's absolute path in the refusal itself.
  - source: Cargo/npm/pip error-message redesign discussions | npm/cli issue #5780 (https://github.com/npm/cli/issues/5780) | IMPL

**✅ CONFIRM** — /yx-ypm MUST present the remedy for a refusal as a visually separated, copy-pasteable block distinct from the conflict narrative, rather than as prose embedded in the diagnostic body.
  - source: Cargo/npm/pip error-message redesign discussions | npm/rfcs discussion #334 (https://github.com/npm/rfcs/discussions/334) | REPORT

**✅ CONFIRM** — /yx-ypm MUST announce, before enabling a stricter default, that installations previously succeeding may now be refused, and MUST say so in the refusal text produced by that new strictness.
  - source: Cargo/npm/pip error-message redesign discussions | PSF blog, Nov 2020 (https://pyfound.blogspot.com/2020/11/pip-20-3-new-resolver.html) | SPEC

**✅ CONFIRM** — /yx-ypm MUST retain and make retrievable the native ecosystem tool's original diagnostic text alongside its own normalized refusal, and MUST NOT discard the native text once it has been rewritten.
  - source: CLI design guidelines | clig.dev 'Errors' (https://clig.dev/) | SPEC

## U2 governance & policy

**✅ CONFIRM** — /yx-ypm MUST enforce waiver expiry with an active removal or re-evaluation mechanism that restores the original policy effect, rather than by reporting the expiry date alone.
  - source: Kyverno Expiration for PolicyExceptions | IMPL

**✅ CONFIRM** — /yx-ypm MUST provide a warn mode and an audit mode for every policy, in which the full refusal explanation is produced but the operation is not blocked.
  - source: KEP-2579 Motivation + Gatekeeper violations | REPORT

**✅ CONFIRM** — /yx-ypm MUST require a named owner for every third-party package admitted into the governed estate and MUST report packages whose owner record is absent or unresponsive.
  - source: SWE at Google Ch.21 third_party import | REPORT

**✅ CONFIRM** — /yx-ypm MUST report exceptions and waivers applied during a run as a distinct tally alongside pass, warn and fail counts.
  - source: Conftest Exceptions | SPEC

**🔴 ESCALATE** — /yx-ypm MUST default a waiver to the narrowest scope (one package identity against one named rule) and MUST require an explicit, separately authorised action to widen it to a rule class or an organisational scope.
  - source: Sonatype IQ scope guidance + Conftest empty-string caution | SPEC
  - critic: Default waiver scope and the authorization required to widen it are fleet policy choices, not merely evidence admissibility questions. An engineer or product owner must choose the governance posture.

**🔴 ESCALATE** — /yx-ypm MUST distinguish 'no policy is bound to this scope' from 'policy was evaluated and denied' in both its exit status and its message, and MUST NOT deny for the former unless the scope has explicitly opted into deny-by-default.
  - source: Kubernetes KEP-2579 Motivation | REPORT
  - critic: Distinguishing unbound policy from evaluated denial is admissible, but the deny-by-default exception commits the system to a policy posture. That decision belongs to engineering or governance owners.

**✅ CONFIRM** — /yx-ypm MUST emit an 'undetermined' verdict value for any ecosystem-native attribute it could not resolve (licence above all), distinct from 'resolved and permitted', and MUST allow a policy to deny on undetermined.
  - source: dependency-review-action #732 and #612 + GitHub Docs | REPORT

**✅ CONFIRM** — /yx-ypm MUST reject a waiver whose expiry value it cannot parse, and MUST NOT fall back to treating an unparseable expiry as no expiry.
  - source: Snyk CLI ignore --expiry format paragraph | SPEC

**✅ CONFIRM** — /yx-ypm MUST print in the refusal the same stable decision identifier it writes to the audit record, so an operator and a later reviewer can name the identical decision event.
  - source: OPA Decision Logs decision_id | SPEC

**🔴 ESCALATE** — /yx-ypm MUST route an unrecognised licence identifier to a per-usage approval record rather than to a blanket organisation-wide allow of that identifier.
  - source: Google Open Source Third-Party Licenses | SPEC
  - critic: Handling unrecognized licenses by per-usage approval rather than organization-wide allow is a governance posture choice. The cited source may inform the decision, but the critic should not select that policy.

**✅ CONFIRM** — /yx-ypm MUST support a declared masking or erasure rule set applied to decision-record inputs before persistence, and MUST record which fields were masked or erased.
  - source: OPA Decision Logs masking section | SPEC

**✅ CONFIRM** — /yx-ypm MUST surface a policy finding at the moment of the dependency-changing operation (add, upgrade, lock, review) and MUST NOT rely on a periodic batch report as the primary delivery channel.
  - source: CACM 62(8) 2019 DOI 10.1145/3338112 | MEASUREMENT

**🔴 ESCALATE** — /yx-ypm MUST support marking a shared internal library deprecated such that new dependents are refused at admission while existing dependents continue to build, and MUST attribute the deprecation to a named owner.
  - source: SWE at Google Ch.15 Deprecation | REPORT
  - critic: Allowing existing dependents while refusing new dependents encodes a deprecation policy choice. Google practice may support it, but selecting that posture belongs to engineering governance.

**✅ CONFIRM** — /yx-ypm MUST be able to evaluate one policy set at more than one enforcement point - pre-change in CI and at the admission or publish gate - with independently configured enforcement actions per point, and MUST audit already-admitted dependencies against current policy rather than only gating new operations.
  - source: Gatekeeper Enforcement points + Audit | SPEC

**✅ CONFIRM** — On a policy denial /yx-ypm MUST return a refusal record that names the denying policy identity, the policy bundle revision it was evaluated against, the offending package coordinate, and a policy-authored human message, and MUST NOT return a bare allow/deny boolean.
  - source: OPA Decision Logs + Gatekeeper violations | SPEC

**✅ CONFIRM** — /yx-ypm MUST refuse to record a policy waiver that lacks both a machine-readable reason and an expiry timestamp.
  - source: Sonatype IQ Waivers + Snyk CLI ignore | SPEC

**✅ CONFIRM** — /yx-ypm MUST expose policy staleness as a per-decision fact rather than relying on a startup readiness check to establish that policy is current.
  - source: OPA REST API Health bundles paragraph | SPEC

**✅ CONFIRM** — /yx-ypm MUST record, for every policy decision, the actor, the timestamp, the evaluated input, the result, and the policy bundle revision, and MUST make those records queryable by package identity and by policy identity.
  - source: OPA Decision Logs event field list | SPEC

**🔴 ESCALATE** — When the policy service is unreachable, /yx-ypm MUST evaluate against the last cryptographically verified policy bundle it persisted, MUST report that bundle's revision and its age in every decision, and MUST NOT evaluate as if no policy existed.
  - source: OPA Bundles persistence + signature verification | SPEC
  - critic: Failing open, failing closed, or using the last verified policy bundle during service outage is a fleet risk posture decision. The cited OPA capabilities make it implementable, but the choice is not the critic's.

**✅ CONFIRM** — /yx-ypm MUST NOT present a truncated or rate-limited set of policy violations as a complete result; it MUST report the total violation count and an explicit truncation marker whenever records were capped or dropped.
  - source: Gatekeeper Audit limit + OPA max_decisions_per_second | SPEC

## U3 performance & scale

**✅ CONFIRM** — The tool MUST support materialising a complete, self-contained vendored dependency tree from which a build proceeds with no network access and no reference to the shared cache.
  - source: Cold-start/offline | Go Modules Reference 'go mod vendor' https://go.dev/ref/mod | SPEC

**✅ CONFIRM** — The tool MUST enforce a configurable per-call network deadline for every registry, cache and mirror call, and MUST report which endpoint exceeded it when it gives up.
  - source: Registry and mirror performance | Bazel --remote_timeout https://bazel.build/reference/command-line-reference | SPEC

**✅ CONFIRM** — The tool MUST offer a strict offline mode that installs only from the local store and FAILS on a missing artifact, and it MUST NOT silently degrade to a network fetch in that mode.
  - source: Cold-start/offline | pnpm CLI docs --offline https://pnpm.io/cli/install | SPEC

**✅ CONFIRM** — The tool MUST assign a durability tier to each input class and MUST skip revalidation of an entire dependent subgraph when the tier's revision counter is unchanged.
  - source: Incremental architecture | rust-analyzer blog 'Durable Incrementality' 2023-07-24 https://rust-analyzer.github.io/blog/2023/07/24/durable-incrementality.html | IMPL

**✅ CONFIRM** — The tool MUST provide a distinct prefer-offline mode that skips staleness revalidation against the registry but still fetches genuinely missing artifacts, kept separate from strict offline mode.
  - source: Cold-start/offline | pnpm CLI docs --prefer-offline https://pnpm.io/cli/install | SPEC

**✅ CONFIRM** — The tool MUST emit a structured, machine-consumable event stream for each invocation covering start, resolved inputs, per-artifact outcomes and completion, sufficient to reconstruct the invocation without the operator's terminal.
  - source: Observability | Bazel Build Event Protocol https://bazel.build/remote/bep | SPEC

**✅ CONFIRM** — Every published performance figure the tool emits or documents MUST state the cache temperature (cold or warm), the workload identity and the measurement harness, and the tool MUST report cold and warm timings as separate metrics rather than a single aggregate.
  - source: Observability | astral-sh/uv BENCHMARKS.md https://github.com/astral-sh/uv/blob/main/BENCHMARKS.md | MEASUREMENT

**✅ CONFIRM** — The tool MUST support an ordered list of registries/mirrors and MUST fall through to the next entry when an entry does not supply the requested artifact, with the fall-through decision recorded in the invocation log.
  - source: Registry/mirror | Go Modules Reference GOPROXY list semantics https://go.dev/ref/mod | SPEC

**✅ CONFIRM** — The tool MUST NOT make a lazy/partial-materialisation fetch mode the default without also shipping a documented recovery path for artifacts that were evicted from the remote cache before they were needed.
  - source: Build-cache design | Bazel blog 'Build without the Bytes is enabled by default in Bazel 7' 2023-10-06 https://blog.bazel.build/2023/10/06/bwob-in-bazel-7.html | REPORT

**✅ CONFIRM** — The tool MUST expose the dedup/link strategy as an operator-selectable setting and MUST document the compatibility cost of the non-default strategies rather than silently choosing for the operator.
  - source: Cache storage | pnpm nodeLinker=hoisted for React Native / serverless lacking symlink support https://pnpm.io/settings/node-modules | SPEC

**🔴 ESCALATE** — The tool MUST make its per-invocation cache hit and miss counts visible in the default invocation summary, not only via an opt-in profiling flag.
  - source: Observability | Bazel 'Checking your cache hit rate' https://bazel.build/remote/cache-remote | SPEC
  - critic: Cache hit and miss counts are admissible telemetry, but requiring them in the default summary rather than an opt-in view is an operator-experience product decision. The critic should not choose that default.

**✅ CONFIRM** — The tool MUST be able to write a machine-readable execution log of every action's inputs, command line and environment, and MUST ship a comparison utility that normalises action order so two invocations can be diffed to explain a cache miss.
  - source: Observability | Bazel --execution_log_compact_file and //src/tools/execlog:parser https://bazel.build/remote/cache-remote | SPEC

**✅ CONFIRM** — When the tool cannot obtain a required artifact locally it MUST fail with a distinguishable exit code that an automated wrapper can retry on, rather than reusing the generic failure code.
  - source: Build-cache design | Bazel blog BwoB-in-bazel-7, exit code 39 https://blog.bazel.build/2023/10/06/bwob-in-bazel-7.html | REPORT

**✅ CONFIRM** — The tool MUST version each cache bucket independently and MUST refuse to read or write a cache bucket whose format version it does not understand, rather than migrating or reinterpreting it in place.
  - source: Cache storage | uv docs 'Caching' https://docs.astral.sh/uv/concepts/cache/ | SPEC

**✅ CONFIRM** — The tool MUST bound every local cache it creates by a configurable maximum size or entry age and MUST garbage-collect to that bound without operator intervention.
  - source: Build-cache design | Bazel 7.4 --experimental_disk_cache_gc_max_size / _max_age https://bazel.build/remote/caching | SPEC

**✅ CONFIRM** — The tool MUST fail an install when the lockfile is out of sync with the manifest in non-interactive mode, and MUST NOT silently re-resolve to make the install succeed.
  - source: Cold-start/offline | pnpm --frozen-lockfile https://pnpm.io/cli/install | SPEC

**✅ CONFIRM** — The tool MUST support a lockfile-only resolve that writes the lockfile without materialising any project tree, so resolution cost can be paid and measured separately from installation cost.
  - source: Observability | pnpm --lockfile-only https://pnpm.io/cli/install | SPEC

**✅ CONFIRM** — The tool MUST emit a periodic progress report naming the specific in-flight operation (registry host, package, action) at a bounded interval while a resolve or fetch is still running, rather than remaining silent until completion or timeout.
  - source: Registry and mirror performance | Bazel Command Line Reference, --progress_report_interval (default 0 -> 10s, 30s, then per minute) https://bazel.build/reference/command-line-reference | SPEC

**✅ CONFIRM** — The tool MUST store package contents in a single content-addressed store shared across all projects on the machine and MUST materialise project trees by the cheapest available link method, falling back to copying only when the filesystem cannot link.
  - source: Cache storage | pnpm settings packageImportMethod=auto, nodeLinker=isolated https://pnpm.io/settings/node-modules | SPEC

**✅ CONFIRM** — The tool MUST provide an explicit cache-bypass flag that disables cache reads while still permitting cache writes, so an operator can prove a suspected stale or poisoned cache entry without deleting the cache.
  - source: Build-cache design | Bazel 'Debugging Remote Cache Hits', --noremote_accept_cached https://bazel.build/remote/cache-remote | SPEC

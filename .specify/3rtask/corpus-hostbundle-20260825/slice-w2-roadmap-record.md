# SLICE W2 - the ROADMAP RECORD for glpnet: what each candidate work item actually IS

Source of record: the signed roadmap export `.specify/roadmap-sync/exports/ariellas__glpnet__20260824T200131Z.json`
(canonical_version 2, key_id 45d4c0f1a06e3117, 140 heads, 279 dependency rows, 3836 journal rows).
Counts folded from `heads`, never parsed from `buildkit-roadmap status` (that renderer drops rows).

**120 features, 20 epics.** State fold: analyzed=3, captured=1, closed=94, implemented=1, promoted=15, specified=6

NOT-CLOSED = 26.  CLOSED = 94.

## The board work-packet id is derived from the feature slug

Observed rule on this board: `wp_id = 'wp-' + resolved_slot[:60]` (some legacy packets predate the rule and
carry the bare slug, e.g. `wave-2-consolidated-repl-engine-split-spine`). Mapping for every not-closed feature:

| feature slug | state | derived wp_id | spec_path | effort |
|---|---|---|---|---|
| `full-scope-gleam-glp-implementation` | analyzed | `wp-full-scope-gleam-glp-implementation` | specs/059-full-scope-gleam-glp-implementation | marathon (multi-session; 66+ WPs across 5 waves; L items: link primitives, engine sessions, QUIC leaf) |
| `verification-receipts-and-loud-failure-no-check-may-pass-without-proving-it-ran` | analyzed | `wp-verification-receipts-and-loud-failure-no-check-may-pass-wit` | specs/078-verification-receipts | large |
| `wave6-consolidation` | analyzed | `wp-wave6-consolidation` | specs/066-wave6-consolidation | large: 27 not-closed items across 5 story groups (S1-S5) and 3 gates (G1/G2/G3), spanning three hosts and consuming external peer receipts |
| `takt-and-token-persistence-to-ducklake` | captured | `wp-takt-and-token-persistence-to-ducklake` | **(EMPTY)** | midi |
| `qr-link-provisioning` | implemented | `wp-qr-link-provisioning` | specs/067-qr-link-provisioning | medium - QR encode + multi-QR chunking + PDF generation + hub display page on the producer side; decode/assemble in consuming clients |
| `041-cross-runtime-and-two-host-acceptance-completion-t055-parity-sc-009-e2e` | promoted | `wp-041-cross-runtime-and-two-host-acceptance-completion-t055-pa` | **(EMPTY)** | medium (provisioning + verification) |
| `bk-onrestart-per-host-reboot-lane-relaunch` | promoted | `wp-bk-onrestart-per-host-reboot-lane-relaunch` | **(EMPTY)** | medium - the launcher is implemented and hardened over 2 codexreview rounds; remaining work is cross-host install and the auto-installable trigger on the other four hosts |
| `buildkit-coordination-optimisation-gepa-dspy-coop-scheduler-marathon-buildkit-tooling` | promoted | `wp-buildkit-coordination-optimisation-gepa-dspy-coop-scheduler-` | **(EMPTY)** | medium |
| `consolidated-hardening-spine` | promoted | `wp-consolidated-hardening-spine` | **(EMPTY)** | large |
| `crdtmsg-post-mvp-completion-cose-sign1-wrapper-1-14-gated-glp-policy-guard` | promoted | `wp-crdtmsg-post-mvp-completion-cose-sign1-wrapper-1-14-gated-gl` | **(EMPTY)** | small (wrapper) to medium (guard) |
| `distributed-unification-quiescence-protocol-two-runtime-spec-first` | promoted | `wp-distributed-unification-quiescence-protocol-two-runtime-spec` | **(EMPTY)** | large |
| `front-end-goal-term-acceptance-completeness-parser-repl-goal-builders-cross-runtime` | promoted | `wp-front-end-goal-term-acceptance-completeness-parser-repl-goal` | **(EMPTY)** | medium |
| `multi-host-state-discipline-reversible-states-untracked-derived-artifacts-unique-identities` | promoted | `wp-multi-host-state-discipline-reversible-states-untracked-deri` | **(EMPTY)** | large |
| `per-host-toolchain-and-environment-contract-declared-machine-checked-loudly-refused` | promoted | `wp-per-host-toolchain-and-environment-contract-declared-machine` | **(EMPTY)** | medium |
| `product-defect-burn-down-with-regression-proof-no-defect-closed-on-a-fixer-s-own-green-run` | promoted | `wp-product-defect-burn-down-with-regression-proof-no-defect-clo` | **(EMPTY)** | large |
| `scheduler-feature-stream-durable-healing-and-hardening` | promoted | `wp-scheduler-feature-stream-durable-healing-and-hardening` | **(EMPTY)** | large - spans the readiness contract, the allocator emission path, the view derivation and a per-repo standing procedure, across all repos on a host then all hosts |
| `seam-specification-normative-contracts-at-every-trust-lifecycle-and-protocol-boundary` | promoted | `wp-seam-specification-normative-contracts-at-every-trust-lifecy` | **(EMPTY)** | large |
| `single-source-of-truth-one-authority-per-subject-provenance-on-generated-artifacts` | promoted | `wp-single-source-of-truth-one-authority-per-subject-provenance-` | **(EMPTY)** | medium |
| `ynet-human-memorable-decentralized-naming-resolver` | promoted | `wp-ynet-human-memorable-decentralized-naming-resolver` | **(EMPTY)** | large (BUILD-NEW, no drop-in corpus reference) |
| `ynet-mobile-background-battery-budget-scheduling-policy` | promoted | `wp-ynet-mobile-background-battery-budget-scheduling-policy` | **(EMPTY)** | medium — BUILD-NEW; needs a real mobile-P2P energy reference fetched first |
| `bk-onrestart-per-host-configurable-auto-installable-fleet-resume` | specified | `wp-bk-onrestart-per-host-configurable-auto-installable-fleet-re` | specs/085-onrestart-fleet-resume | medium - a per-host config file, register/unregister verbs, an idempotent install/uninstall of the logon trigger, and a verification receipt |
| `coordination-feature-stream-durable-superset-fix` | specified | `wp-coordination-feature-stream-durable-superset-fix` | specs/082-feature-stream-superset | large: spans buildkit scheduler+roadmap+colab subsystems + glpnet consumer; deploy to all hosts |
| `glptutorial-corpus-golden-reconciliation-stale-goldens-drift-guard-vendoring` | specified | `wp-glptutorial-corpus-golden-reconciliation-stale-goldens-drift` | specs/083-glptutorial-corpus-goldens | small |
| `madglp-writer-reader-address-discipline-closure-n-n-1-audit-residuals` | specified | `wp-madglp-writer-reader-address-discipline-closure-n-n-1-audit-` | specs/079-madglp-writer-reader-discipline | small-medium |
| `occurs-checked-substitution-pipeline-compiler-bind-time-occurs-check` | specified | `wp-occurs-checked-substitution-pipeline-compiler-bind-time-occu` | specs/080-occurs-checked-substitution | small-medium: one shared occurs-check helper at ~9 bind sites x 2 duplicate copies |
| `ynet-consolidation` | specified | `wp-ynet-consolidation` | specs/065-ynet-consolidation | large |

## Full record of every NOT-CLOSED feature

### `full-scope-gleam-glp-implementation`  [analyzed]

- title: Full-scope Gleam GLP implementation
- epic: full-gleam
- spec_path: specs/059-full-scope-gleam-glp-implementation
- effort: marathon (multi-session; 66+ WPs across 5 waves; L items: link primitives, engine sessions, QUIC leaf)   risk: Plan NON-FINAL pending engineer waiver-or-resume (10 BLOCKED WPs, 3 missing WPs); 3 open escalations (multiagent-runtime, mesh-ring, UnifyConstant divergence) gate wave-4/5 scope; QUIC leaf carries WSL-only + quicer build risk.   priority_rank: 43
- touched_areas: fe-be-separation, gleam_quic, glp_gleam, yngenios-embed
- problem: Phase-1 3rtask inventory (run 20260719T130005Z-782b, docs/research/fullscope-gleam/gap-inventory-2026-07-19.md): of 154 deduplicated capabilities, 44 delivered in Gleam, 9 partial with named missing parts, 99 gap-class, 2+1 open escalations. Full scope (Dart/C# parity + FE/BE separation + yngenios embeddability) is not deliverable from 050 alone.
- value: One marathon-tracked feature delivering the full-scope Gleam GLP per the Phase-2 outline plan (run 20260719T134320Z-544f, docs/research/fullscope-gleam/feature-outline-plan-2026-07-19.md): waves 1-5 (freeze/guard -> verify/rule -> close -> build FE/BE split + yngenios embeddability -> accept), 66 accepted WPs, 154/154 traceability, frozen-interface register + grow-only suite guards as drift controls.
- notes: SCOPING COMPLETE 2026-07-19 via marathon mrun-8bda036d9e9b: Phase1 gap inventory + Phase2 outline plan committed under docs/research/fullscope-gleam/ (commits ecf92bf9, 45483cc2, 5109b5d3). ENGINEER GATES before /bk-specify: (1) E9 waiver-or-resume for the NON-FINAL plan; (2) rulings on multiagent-runtime, mesh-ring, UnifyConstant escalations; (3) 8 out-of-scope proposals. 3rtask runs indexed: 20260719T130005Z-782b (research), 20260719T134320Z-544f (plan).

### `verification-receipts-and-loud-failure-no-check-may-pass-without-proving-it-ran`  [analyzed]

- title: Verification receipts and loud failure (no check may pass without proving it ran)
- epic: None
- spec_path: specs/078-verification-receipts
- effort: large   risk: medium: touches the review, sync and test-gate seams that everything else depends on; must be fault-injected rather than assumed   priority_rank: 1
- touched_areas: buildkit-3rtask, buildkit-codexreview, codeconv-build-gate, coop-protocol, roadmap-sync, test-harness
- problem: Across ~300 deduplicated fleet defects excavated from gate ledgers, COOP threads and handovers, the single largest and most dangerous class is a mechanism that reports success, zero-findings, or nothing at all while not having run - or having run against the wrong target, revision, host or path. Witnessed independently by all three corpora. Concrete instances: the repo mandatory-reading gate silently false-zeroes non-interactive codex passes so a review reports 0 findings because it never ran (3 recurrences); buildkit brief/record-output silently no-op on an existing role input, invalidating an adjudication round; a roadmap import refused 954 untagged entities and applied 0 lines while replay --verify still reported OK (silent split-brain, 20-line divergence measured); codex omitted its findings block in 5/5 passes yielding findings_count=0 while really finding 5-8 P1/P2s; xUnit skip-guards report an unsupported-platform QUIC link as passed-by-skip; the codeconv build gate is compile-only so a behaviourally-wrong generated file can be promoted; both corpus tools are manual-only so the unified suite gates corpus scope by nothing at all; four separate poll/cursor defects each silently skipped unread mail (one hid a peer ACK for 14.5h, one cost a day of idling); git probes from the wrong directory returned a false clean. This class is WHY the other five root causes survived undetected.
- value: Converts the fleet's most common silent-success paths into loud, attributable failures. Every other feature's acceptance suite is only trustworthy once a check can no longer pass without running - this is the prerequisite for the other five.
- notes: RCA cluster F1 of 6 (3rtask run 20260811T113723Z-1f7c). Invariant: no check may report success/clean/zero-findings without proof it executed against the intended target; every check emits a receipt distinguishing EMPTY / UNREAD / UNSEARCHABLE. Acceptance is fault-injected, not hypothetical. Subsumes 23 inventory ids: PR-15, PR-16/AG-04/RT-35/RT-45, TL-07, RT-24/RT-28/RT-29/RT-16, KI-27, RT-13, RS-11, PR-18, PR-02/04/05/06, CD-03, D8-14, D8-11/12, TK-06, RT-12/RS-35/RS-36, RT-32, RT-27, DI-03. SHIPS FIRST - the other five clusters have an ordering edge from this one.

### `wave6-consolidation`  [analyzed]

- title: Wave6 consolidation
- epic: epic-roadmap-sweep-2026-07-consolidated-waves
- spec_path: specs/066-wave6-consolidation
- effort: large: 27 not-closed items across 5 story groups (S1-S5) and 3 gates (G1/G2/G3), spanning three hosts and consuming external peer receipts   risk: high: the US4/T015 gate is structurally unsatisfiable as written - BC-3 proves the C# target only (Dart unmeasured) and BC-4 means Gleam is not an ANTLR target, so no T016 outcome can ever produce the go that US5 waits on; also depends on peer receipts and 3 open engineer rulings   priority_rank: 49
- touched_areas: (none recorded)
- problem: 18 not-closed roadmap items (snapshot 20260803T150440Z) lack a driven path to terminal disposition; peer ownership (ariellas gap-closure) and open rulings need explicit gating to avoid duplicate/conflicting work
- value: Every not-closed item reaches closed or recorded defer/reject; peer receipts consumed not duplicated; Full-Gleam chain advanced in dependency order
- notes: Gates: G1 064 ship-state, G2 065 completion, G3 open rulings, external ariellas 064-post-wave-gap-closure (carve-out lead-confirmed 153920Z). Story groups S1 quick wins / S2 promoted singletons / S3 ANTLR4 spike / S4 Full-Gleam chain consuming ariellas US1-US2 receipts / S5 captured intake triage

### `takt-and-token-persistence-to-ducklake`  [captured]

- title: Persist takt and per-phase token use to the DuckLake and serve all takt reporting from it
- epic: epic-issue-backlog-root-cause-closure-sweep-2026-08
- spec_path: (EMPTY - no spec dir bound)
- effort: midi   risk: low   priority_rank: None
- touched_areas: buildkit-co, buildkit-marathon
- problem: MEASURED 2026-08-24 on the ariellas glpnet lake (.specify/co-lake, 1202 parquet files back to 2026-06-27): 1200 observations across 6 capabilities (marathon 719, deploy 397, scheduler 67, ship 15, guardian 1, implement 1) contain ZERO rows matching 'takt' and only 8 matching 'token'. Nothing writes takt or per-phase token use to the lake, so the fleet-normative requirement to SERVE takt reporting FROM the lake is structurally unsatisfiable - every takt report is necessarily live-command-sourced.
- value: Makes takt and token spend durable, queryable and comparable across hosts and days instead of recomputed live per session; closes the BK-STD-1 requirement that takt reporting be lake-sourced.
- notes: Engineer ruling 2026-08-24: SPEC IT, do not hand-patch mid-marathon. Until it ships, takt is reported from buildkit-marathon takt with an explicit not-lake-sourced caveat. Broadcast first to check no lane already persists takt - avoid four incompatible schemas. Evidence: DuckDB 1.5.5 census in marathon mrun-f5ef56dba3c1.

### `qr-link-provisioning`  [implemented]

- title: QR-code link + cert provisioning via generated PDF or hub display page
- epic: distributed-glp-connectivity
- spec_path: specs/067-qr-link-provisioning
- effort: medium - QR encode + multi-QR chunking + PDF generation + hub display page on the producer side; decode/assemble in consuming clients   risk: MANDATORY security posture, not optional and NOT waivable by any time-boxing: the shared cert/private-key (pfx) is LONG-LIVED, UNCHANGEABLE TRUNK credential material at the center of the key cluster for public infrastructure. Rendering it as a scannable/printable QR is a PERMANENT credential exposure the moment it is shown - there is NO acceptable 'time-boxed unhardened PoC' carve-out (an earlier note wrongly implied one; corrected 2026-07-08 per Gabi). Hardening is FIRST-CLASS SCOPE: never render the trunk key itself - provision short-lived, per-device, revocable derived material; encrypt the QR payload (one-time passphrase / out-of-band key); never persist secret images (no saved PDF of secrets); full audit of every render + revocation path; printed output forbidden for trunk material.   priority_rank: 17
- touched_areas: cert-trust, glp_quick, hub-display, provisioning
- problem: Joining a device or host to the glp-quick QUIC+WS mesh requires hand-copying the shared cert dir (pem/key/pfx/fingerprint) plus endpoint params out-of-band. 049 US3 hit this twice on real hardware: the cert was absent on the second host, then SMB credential walls blocked both push and pull for a session; phones/tablets have NO copy channel at all, blocking android-quick-link-endpoints (olamnit-assistant repo).
- value: One-scan provisioning: glp-quick renders the link endpoint (addr/port/SPKI pin) and the shared cert+key material as one or more QR codes (chunked to QR capacity with integrity checks), presented either as a generated PDF or as a hub display page; a new endpoint scans to acquire the full trust bundle and joins the mesh - removes the manual cert-copy bottleneck and unblocks device onboarding.
- notes: Motivated by 049 US3 cert-distribution friction (glpnet specs/049-wave1-guard-link-acceptance/evidence/gavri/20-two-host.md). Producer side lives in glp_quick. Pairs with olamnit-assistant android-quick-link-endpoints (consumer). 036 trust model unchanged: manual pin, shared cert. CORRECTION 2026-07-08 (Gabi): the credential here is permanent public-infrastructure trunk key material - it must ALWAYS be treated as a lasting service on unchangeable credentials, never as a disposable time-boxed test; the hardening scope above is a precondition of the feature, not a follow-up.

### `041-cross-runtime-and-two-host-acceptance-completion-t055-parity-sc-009-e2e`  [promoted]

- title: 041 cross-runtime and two-host acceptance completion (T055 parity + SC-009 e2e)
- epic: distributed-glp-connectivity
- spec_path: (EMPTY - no spec dir bound)
- effort: medium (provisioning + verification)   risk: medium - blocked until gavri reachable and quicer NIF built   priority_rank: 33
- touched_areas: csharp/glp_crdtmsg, gleam_quic, glp_quick, test/parity
- problem: 041's Gleam/Dart codec parity run (T055; corpus + vectors ready in test/parity/, C# truth runtime 48/48 green) and the crdtmsg two-host real-QUIC e2e (SC-009; glp_quick_host is a drop-in ILinkTransport side-process per contract C20) are blocked solely by environment: the gavri second LAN endpoint + an MSVC/msquic-built quicer NIF. The intended fold target http3-quic-ws-link-full-acceptance is CLOSED, so these items need their own home.
- value: One host-provisioning effort discharges the last environment-blocked acceptance items; no new protocol design.
- notes: Absorbs: F041-T055, F041-SC-009. Evidence: 3rtask a625 builder-3 (fold proposal F1a/F1b; ESCALATE E4 asked engineer confirmation of the T055 toolchain match - now moot as a fold, live as scope). Overlaps the 067-derived listener/two-host needs (064 durable-listener-service-box; gavriella handshake receipt) - consider dependency-linking at review.

### `bk-onrestart-per-host-reboot-lane-relaunch`  [promoted]

- title: bk-onrestart: per-host reboot lane relaunch with attributed verification
- epic: None
- spec_path: (EMPTY - no spec dir bound)
- effort: medium - the launcher is implemented and hardened over 2 codexreview rounds; remaining work is cross-host install and the auto-installable trigger on the other four hosts   risk: a wrong or stale launcher silently no-ops at logon and the operator does not discover it until the lanes are missing; mitigated by the handshake marker plus exit code 2 on an incomplete run   priority_rank: None
- touched_areas: buildkit-cli, fleet operations, scheduled tasks, windows terminal
- problem: After a reboot every repo lane must be resumed mid-thread, not summarised. Doing it by hand is slow and error-prone, and the known failure mode is silent: Windows Terminal opens N tabs that run NOTHING (measured 12 tabs / 0 claude processes). The prior mechanism verified by counting claude processes started in the last two minutes, which is satisfied by unrelated processes, so it could report VERIFIED while every tab ran nothing. Host layouts also differ - GAVRIELLA and GAVRI want two windows (core + satellite) while OLAMNIT ARIELLAS and SHIRAS want one - and the lane list is per-host, so a single hardcoded script cannot serve the fleet.
- value: One reboot restores all 12 lanes mid-conversation with no manual step, on any host, with per-host layout and a lane list the operator can capture in place via register/unregister. Verification is attributed per lane rather than counted, so a tab that opened and ran nothing is reported as a failure instead of a pass.
- notes: Implemented at scripts/onrestart-launch.ps1 (1349 lines) with regression harness scripts/tests/onrestart-launch.tests.ps1 (187 assertions). Config lives OUTSIDE any repo at ~/.bk-onrestart/config.json so a repo can be deleted or re-cloned without losing the machine layout. layoutByHost maps GAVRIELLA/GAVRI to TwoWindows and OLAMNIT/ARIELLAS/SHIRAS to Tabs; an unlisted host falls back to defaultLayout and SAYS SO rather than guessing. -Install registers the at-logon Scheduled Task with a 45 s delay on ANY host and is idempotent. -Register / -Unregister capture a lane by BEING in it. Verification is per lane and attributed: a generated per-lane launcher writes a handshake marker recording lane key run id resolved cwd and its own PID before exec-ing the ABSOLUTE resolved claude path; a lane is proven only by a live descendant of that PID whose image IS the resolved command, plus an appended transcript record carrying the EXPECTED sessionId. VERIFIED is printed only when every requested lane is proven; accepted exceptions report ACCEPTED-WITH-EXCEPTIONS and an incomplete run exits 2. Shipped in glpnet v2026.08.21.3 and v2026.08.22.1; belongs in buildkit as a first-class tool.

### `buildkit-coordination-optimisation-gepa-dspy-coop-scheduler-marathon-buildkit-tooling`  [promoted]

- title: buildkit coordination optimisation (GEPA/DSPy) — coop, scheduler, marathon, buildkit tooling
- epic: None
- spec_path: (EMPTY - no spec dir bound)
- effort: medium   risk: medium: GEPA/DSPy engine availability varies per host; improvements must be evidence-gated via bk-codify before adoption   priority_rank: 46
- touched_areas: (none recorded)
- problem: Three-host parallel pipeline runs (waves 2/4/5, directive 20260729T193333Z) exercise coop channel + CRDT scheduler + marathon + buildkit together; coordination friction is observed but not systematically optimised or fed back into the tooling
- value: GEPA/DSPy-refined coordination prompts/configs plus codified wins consolidated into one improvement feature covering all affected tooling
- notes: Operator-directed 2026-07-29 alongside the wave-2/4/5 parallel run; wins recorded via /bk-codify during the runs land here; lead ariellas

### `consolidated-hardening-spine`  [promoted]

- title: Consolidated hardening spine: full hardened specify-design-implement-codexreview with durable healing + hardening
- epic: None
- spec_path: (EMPTY - no spec dir bound)
- effort: large   risk: medium   priority_rank: None
- touched_areas: buildkit-co, buildkit-codexreview, buildkit-guardian
- problem: No single wired spine composes shipped hardening capabilities; gate composition, sink ownership, failure-state vocabulary undefined.
- value: One specify->design->implement->codexreview spine wiring 7 shipped capabilities with durable healing + hardening; zero re-implementation.
- notes: Charter=3rtask 20260817T181329Z-d920 (CONVERGED). 4 contracts resolved. WIRE only. See docs/handover + curator_report.md.

### `crdtmsg-post-mvp-completion-cose-sign1-wrapper-1-14-gated-glp-policy-guard`  [promoted]

- title: crdtmsg post-MVP completion (COSE_Sign1 wrapper + 1.14-gated GLP policy guard)
- epic: epic-issue-backlog-root-cause-closure-sweep-2026-08
- spec_path: (EMPTY - no spec dir bound)
- effort: small (wrapper) to medium (guard)   risk: low (wrapper) / medium (guard - approval-gated, touches guard semantics)   priority_rank: 36
- touched_areas: csharp/glp_crdtmsg, programs/crdtmsg
- problem: Two deliberate 041 MVP-boundary carve-offs remain in the crdtmsg subsystem: the COSE_Sign1 CBOR wrapper around the complete, tamper-tested Ed25519 seal core (T040); and the GLP-native policy guard whose signature awaits Gabi's DISCIPLINE 1.14 ruling on programs/crdtmsg/policy-guard-proposal.glp (T053/FR-014; MVP routes via the fixed C# PolicyMatcher meanwhile).
- value: Standards-interoperable seal framing for external COSE consumers; policy expressible in GLP honoring the language-authority process. Degrades to COSE-only if the 1.14 gate refuses.
- notes: Absorbs: F041-T040-COSE, F041-T053-FR014. Evidence: 3rtask a625 builder-3. ESCALATE E3 open: one degradable feature or two singletons - Gabi's call. First gate is the 1.14 ruling.

### `distributed-unification-quiescence-protocol-two-runtime-spec-first`  [promoted]

- title: Distributed unification + quiescence protocol (two-runtime, spec-first)
- epic: distributed-glp-connectivity
- spec_path: (EMPTY - no spec dir bound)
- effort: large   risk: high: new distributed semantics in two runtimes; writer-MGU across the link is semantically deep; any GLP surface need is Section-1.14 gated; requires the ack-path substrate first   priority_rank: 54
- touched_areas: csharp/glp_link, csharp/glp_runtime_net, glp_gleam/src/glp/link
- problem: 3rtask T002 parity checklist (specs/064-post-wave-gap-closure/parity-checklist.md) proved the C# link is ground-relay (050 D-4): no distributed unification, no distributed quiescence oracle, no ack path (inflight_acked unimplementable). 064 Option-B ruling transferred FR-001/FR-002 out of 064 into this feature.
- value: A NEW two-runtime protocol: VAR_EXPORT/DIST_BIND/DIST_SUSPEND/DIST_FAULT + census-based quiescence, designed once and implemented on both runtimes, anchored on the madGLP writer-MGU machinery (payload_serializer GlobalVarId wire form, heap_fcp BindImportedReader seam) with FCP Savannah as tie-breaker. Seed drafts: 064 contracts/dist-unify.md + quiescence.md (must be re-anchored per checklist amendments A-1..A-9). Prereq noted: an ack path (SendWindow wiring) for in-flight accounting.
- notes: Origin: 064 Option-B ruling 2026-08-03. The 059 sweep carries the dependent tasks (T066/T083/parts of T088-T089) as deferrals pointing here.

### `front-end-goal-term-acceptance-completeness-parser-repl-goal-builders-cross-runtime`  [promoted]

- title: Front-end goal-term acceptance completeness (parser + REPL goal builders, cross-runtime)
- epic: epic-issue-backlog-root-cause-closure-sweep-2026-08
- spec_path: (EMPTY - no spec dir bound)
- effort: medium   risk: low   priority_rank: 21
- touched_areas: CLAUDE.md, glp_runtime parser, glp_runtime/lib/engine/glp_engine.dart, out/csharp REPL goal setup
- problem: Input front-ends reject valid GLP terms the runtimes already support: =.. rejected in clause bodies (parser; type-checker registers =../2, prelude.dart:127-129); structs-inside-lists in REPL goals (recorded location STALE - glp_repl.dart is now a thin wrapper, logic moved to GlpEngine); C# REPL _SetupArgument throws on UnderscoreTerm in top-level goals (050).
- value: Removes three documented user-facing footguns in one mechanism-coherent sweep; restores Dart/C# REPL goal parity; the one cross-slice merge both builders flagged independently.
- notes: Absorbs: CLAUDE.md L1 (=.. in bodies), L2 (structs-in-lists, re-verify in GlpEngine first - may already be fixed), F041/050 csharp-repl-underscore. Evidence: 3rtask a625 builder-1 + builder-3 (cross-slice-suspect flags corroborate the merge). Doc fix rides along: CLAUDE.md L2 stale location.

### `multi-host-state-discipline-reversible-states-untracked-derived-artifacts-unique-identities`  [promoted]

- title: Multi-host state discipline (reversible states, untracked derived artifacts, unique identities)
- epic: None
- spec_path: (EMPTY - no spec dir bound)
- effort: large   risk: medium-high: changes roadmap CLI semantics and the ship commit path that all three hosts depend on; needs a coordinated fleet cutover   priority_rank: 30
- touched_areas: buildkit-marathon, buildkit-roadmap, buildkit-ship, calver, roadmap-sync
- problem: Shared fleet state across three hosts is managed without distributed-systems invariants, producing a recurring family of defects. closed is a ONE-WAY DOOR: reachable by one host's advance, reversible by none, in a 3-host CRDT-union journal - it already pinned a live feature wrongly and nearly caused a peer to false-close another. The version-controlled .import-manifest.json is a machine-generated mirror tracked as source, so it conflicts on EVERY multi-host ship (4 occurrences: b5998681, 7e1c055e, 0d2739b1, plus one hit live). The roadmap link scan DOWNGRADES post-implement states (released -> specified) on merge; at least 3 features drifted backwards. add-dependency hangs >2min on the write path so a lineage edge was never recorded. Feature numbers are per-host, giving 3 collisions (064x2, 066x2) and a near-miss where two different '065' features were nearly conflated into a false close. Marathon and pipeline state are machine-local so a peer's run is unreachable and a version skew blocked a discharge. buildkit feature-pointer files conflict on every cross-wave merge. ship's git add -A swept unrelated files into 4 releases. CalVer: stale local tags picked a taken number; -creatordate sort makes a recreated old tag newest; day-roll invalidated announced .N five times.
- value: Makes shared state safe to write concurrently: reversible, single-owner, collision-proof, and free of tracked derived artifacts. Removes the largest source of cross-host coordination cost and the only failure mode the fleet has recorded as unrecoverable.
- notes: RCA cluster F2 of 6 (3rtask run 20260811T113723Z-1f7c). Invariant: shared fleet state is reversible, derived artifacts are never tracked as source, identity namespaces are globally unique, run stores are addressable across hosts. Subsumes 34 inventory ids incl. GL-19/TL-05, GL-20/TL-06/RT-43/CO-14, TL-03, TL-04/HO-46/RT-48/RS-37, CO-03/04/05/RS-09, TL-08/HO-06/HO-07/TL-09/HO-26, RT-42/TL-11/HO-35, RT-11/15/17, CO-12/13, PR-11, PR-17/RT-10/RT-38/RT-49/HO-55, RT-41/RT-52/CO-02/RT-39/HO-36, PR-09/10/12. Fleet is already unanimous on the manifest fix (gitignore the derived mirror; PGlite catalog is authoritative) - only the decision was missing. Ordering edge: after F1.

### `per-host-toolchain-and-environment-contract-declared-machine-checked-loudly-refused`  [promoted]

- title: Per-host toolchain and environment contract (declared, machine-checked, loudly refused)
- epic: None
- spec_path: (EMPTY - no spec dir bound)
- effort: medium   risk: low-medium: additive contract plus per-toolchain build dirs; the CI guard fix is isolated   priority_rank: 24
- touched_areas: buildkit-deploy, ci, glp_gleam, pglite-bridge, test-harness
- problem: Nothing declares what a host must provide, so environment mismatches masquerade as code defects and burn whole sessions. glp_gleam/build is single-OTP: WSL and Windows runs collide and the beam-load failure looks exactly like a regression (cost a full suite re-run, recorded 5 times across corpora). Suites hard-code a SIBLING host's dart path, producing 16/16 false failures. An unquoted export PATH is silently mangled by space-bearing Windows entries so the required OTP is never picked up. Profile-C/quicer/msquic is unbuildable on these hosts, giving permanent failures, a teardown host-process leak, and an unreproducible green claim. The 059 tail is blocked entirely on a Windows OTP-25 install that does not exist here. UPPAAL has been licence-gated since 2026-07-30. DBOS-on-PGLite needs 4 non-default hooks or it fails four different ways; PGLite dies on exFAT and PG16/PG17 data dirs are not forward-compatible. An interrupted pip upgrade uninstalled buildkit-cli and stalled a barrier 22h. buildkit's own CI trunk has been ~200-tests-red since 2026-07-30, dominated by a Windows UNC-path guard misfiring on ordinary Linux paths - the same disease inside CI.
- value: A host either satisfies a declared contract or is refused by name before any suite runs. Eliminates the phantom-regression class and makes a red suite mean something.
- notes: RCA cluster F3 of 6 (3rtask run 20260811T113723Z-1f7c). Invariant: a machine-checkable contract declares every required tool/version/path; a host failing it is named loudly BEFORE any suite runs; per-toolchain artifacts never share a directory. Subsumes 30 inventory ids incl. CL-07/HO-01/RT-47/CD-13, KI-25/CL-03/HO-04, CL-05/CL-06, KI-17/RT-26/HO-05/CD-15/TK-04/TK-11/RS-05, CO-18, TL-16/RT-37/RS-19/E6, KI-08/09/10/11, TL-01, HO-34/CL-16/CL-12/CL-15/DI-04/HO-15/HO-25, HO-49, HO-39/KI-24, RS-14/15/16, CD-14/TL-14/HO-27/RT-19, TL-13. Ordering edge: after F1.

### `product-defect-burn-down-with-regression-proof-no-defect-closed-on-a-fixer-s-own-green-run`  [promoted]

- title: Product-defect burn-down with regression proof (no defect closed on a fixer's own green run)
- epic: None
- spec_path: (EMPTY - no spec dir bound)
- effort: large   risk: medium: breadth across three runtimes; each item is individually small but the adversarial-verification requirement doubles the per-fix cost by design   priority_rank: 59
- touched_areas: csharp, glp_gleam, glp_runtime, multiagent, test-harness
- problem: The residue after the five mechanism clusters: genuine code defects recorded across handovers, known-issues and research dossiers, none of which currently carry a regression test that fails without the fix. Highest-severity members: mesh duplicate-id eviction EVICTS A LIVE SIBLING (routing loss); the Gleam relay silently DROPS DATA above 1 MiB; program_dfa raises an uncaught panic that CRASHES the Gleam REPL where Dart and C# emit a graceful diagnostic; the live REPL process bridge is unbuilt so the --repl flag is inert; the 3270 surface advertises @name routing in help but never implemented it, swallows all exceptions in recv_loop, has a data race on shared lists, and has zero tests importing it; localize() substitutes a writer address where the spec requires a reader so ground() fails definitively instead of suspending and the test passes as a FALSE POSITIVE; the head checker reports spurious mode mismatches so tests carry a filter workaround; AtomVM bignum masking produces false-green parity. Also captured: the fleet's own systematic finding that 2 of 11 review fixes INTRODUCED new defects (a transient econnaborted latched into a permanently dead listener at 100% CPU; a bind-gate that awaited one object and tested another), caught only by a separate post-fix adversarial pass.
- value: Every recorded defect ends in one of two states - closed with a test that fails without the fix, or an explicit dated deferral. Nothing sits in the unproven middle, and no fix is trusted on its author's own green run.
- notes: RCA cluster F6 of 6 (3rtask run 20260811T113723Z-1f7c). Invariant: every recorded product defect is either closed by a regression test that fails on the unfixed tree, or carries an explicit dated deferral. Acceptance includes the fleet's own rule that a fixer's own green run is not evidence a finding closed (recorded as systematic after 2/11 fixes regressed). Subsumes 26 inventory ids incl. KI-01/02/05, RS-01/02/03/04, RS-24, RS-06, KI-26, HO-53/56/57/58/59, RS-21/CR-782b-03, RS-30, HO-30, RT-05, CD-16, TK-07, TK-08, HO-03, RT-21/22/23, RT-50/51. Ordering edge: after F1 (its acceptance depends on tests that cannot pass without running).

### `scheduler-feature-stream-durable-healing-and-hardening`  [promoted]

- title: Scheduler feature-stream durable healing and hardening (the four-break chain)
- epic: epic-issue-backlog-root-cause-closure-sweep-2026-08
- spec_path: (EMPTY - no spec dir bound)
- effort: large - spans the readiness contract, the allocator emission path, the view derivation and a per-repo standing procedure, across all repos on a host then all hosts   risk: medium-high - BREAK 1 is a deliberate advisory-posture contract, so any automatic readiness writer must not violate it; a naive fix mass-promotes an edgeless backlog and manufactures exactly the false green this exists to end   priority_rank: None
- touched_areas: allocator, buildkit-scheduler, coop-board, readiness
- problem: No lane receives a steady stream of features from the scheduler. Measured 2026-08-22 on glpnet: this is NOT one defect but FOUR independent breaks in series, which is why three hosts each fixed a real defect and the stream still did not start. BREAK 0 REFUTED - supply is fine (ingest dry-run: eligible 17 promoted, already_minted 17, would mint 0). BREAK 1 - minted packets land in backlog and no cycle path can EVER write ready; all seven ready occurrences in the engine are reads and only operator verbs write it (R-B1). This is contract not bug: the board is deliberately incapable of self-feeding while the fleet operating model assumed it would self-feed. BREAK 2 - the readiness recommender is vacuous: 23 candidates all unconstrained, 0 confirmed edges, so promoting them is correct arithmetic on the wrong predicate (the module says so itself). BREAK 3 - efforts exceed capacity (e_t_s 288000 and 144000 vs 86400 per day) and the unplaceable proposal is emitted SILENTLY then not billed, so the lane reads idle. BREAK 4 - the allocate VIEW contradicts every durable allocate OP and even proposes a WP already transitioned to done.
- value: Turns the fleet from hand-fed to self-feeding with receipts. Removes the single largest source of false-green coordination reporting: an empty allocation read as an idle lane rather than a stalled pipeline. Prerequisite for the marathon-to-bk-flow migration, which needs a dependable stream to measure takt against.
- notes: Root cause codified as note cn-20260822T201224-c8c4728a. SUPERSET of glpnet 082-feature-stream-superset (which scopes BREAK 3 and BREAK 4 as US2/US3) - 082 should be folded into this or explicitly scoped as its engine half; STILL UNDECIDED, and 082 was merged to develop on 2026-08-20 (TIDY-W03) while this row stayed promoted. Remedy shape: (a) a named per-repo STANDING READINESS PROCEDURE run every cycle (readiness -> confirm) that satisfies the advisory contract by being an explicit agent action rather than a cycle side-effect; (b) unplaceable proposals become a loud refusal not a silent emission; (c) the allocate view derived FROM durable ops rather than recomputed; (d) edge coverage required before mass-confirm. Deploy across all repos on this host first, then all hosts.

CONSOLIDATED 2026-08-23 (marathon mrun-20d9230f767b step mstep-01a0199b-a88c codify-consolidated-hardening-feature). AUTHORITATIVE CONTENT: docs/research/consolidated-hardening-2026-08-23.md - that file WINS over this summary and over any marathon step name. No new roadmap feature was minted; four strands were folded into this one.

STRAND 1 - SCHED-R5 IS DONE and it changed the conclusion for three boards. docs/research/scheduler-lock1-fleet-audit-2026-08-23.md, 14 boards measured read-only under D:/coop/*/sched, folding to the CURRENT addressee per WP rather than counting history. Three boards were reading a FALSE ALL-CLEAR on Lock 1: glpnet 22 of 28 unowned, yngenios-windows 27 of 28, lejepa 30 of 35 - all three have ZERO blanks so the old presence test found nothing while nearly all their work sat unowned. lejepa proves a hard-coded pool vocabulary is insufficient (its pool actor is ariellas-lejepa). buildkit and ospark are HEALTHY and revision 1 of that audit said otherwise - rev 1 figures are WITHDRAWN. yngenios is empty and stays UNMEASURED.

STRAND 2 - SCHED-R6 (new, mini 7): the capability gate is inert and says so. bk-flow poll reports capability_gate_inert - no WP declares a required_capability so the ranking never executed, and missing_capability=0 means UNMEASURED not clear; 50 published capabilities were never compared against anything.

STRAND 3 - SCHED-R7 (new, midi 11): the BINDING gap is a SEPARATE defect from the readiness gap. First bk-flow poll ever run in glpnet: 32 WPs, and binding 1 of 32 packets resolve to a feature - 31 cannot. A WP that reaches ready still has nowhere to go, so fixing BREAK 1 alone moves the stall one hop downstream. PREMISE VERIFIED while establishing this: bk-flow DEPENDS on marathon (open calls _open_marathon_run and persists run_id; takt measures that feature's marathon run) - it does NOT replace it.

STRAND 4 - TOOL-R8 (new, midi 11, engineer/two-repo): the toolchain that reports on all of this has 078's own defect. An editable .pth points at D:\BSTDEV\research\buildkit\src so every bk-* call runs the buildkit WORKING TREE (branch 087-import-untrusted-key-warning), never a deploy-home version, while all 29 targets report active at 2026.08.23.1. Measured honestly: 51 files differ, 48 line-ending-only, exactly 3 real - all in threerole/ and all UNCOMMITTED. So /bk-3rtask here runs code in no release and no commit. pip dist-info says 2026.8.19.1 while the module self-reports 2026.8.23.1.

TOOL-R9 (new, mini 7, gated on R8): those uncommitted threerole files fix a FALSE-CORROBORATION defect - claim identity keys on the claim-text sha1 so contentless records all hash to sha1('') and collapse into ONE identity reported as corroborated by EVERY Builder; measured, 74 records emitted under the unread key claim_text became one row corroborated by all three Builders. THIS FEATURE'S EVIDENCE WAS ADJUDICATED ON 3RTASK CORROBORATION COUNTS, so those counts must be re-checked for any run whose Builders emitted claim_text. The root cause itself was proven BOTH WAYS by direct board manipulation and does not depend on 3rtask, so the conclusion stands while the corroboration FIGURES are pending re-check.

LEDGER after consolidation: R1 maxi 17 pending; R2 midi 11 pending (FALSELY MARKED COMPLETE ONCE - it is NOT done); R3 mini 7 shipped to branch 086-sched-r3-placeholder-addressee NOT merged; R4 midi 11 pending; R5 mini 7 DONE; R6 mini 7 new; R7 midi 11 new; TOOL-R8 midi 11 new; TOOL-R9 mini 7 new. Total 89 pts, 7 delivered, 82 remaining, 18 engineer/two-repo gated.

### `seam-specification-normative-contracts-at-every-trust-lifecycle-and-protocol-boundary`  [promoted]

- title: Seam specification: normative contracts at every trust, lifecycle and protocol boundary
- epic: None
- spec_path: (EMPTY - no spec dir bound)
- effort: large   risk: high: several sub-items are Section-1.14 language-authority gated and need owner rulings before implementation; the trust-boundary default is a security decision   priority_rank: 34
- touched_areas: contracts, csharp/glp_link, glp_gleam, glp_quick, specs
- problem: Where the spec is silent at a seam, each runtime guesses - and the guesses differ. The load-bearing case: glp_quick_host admits unauthenticated plain-TCP peers into the certificate-authenticated QUIC mesh and --bridge-addr binds anywhere, while the Gleam back end enforces the OPPOSITE default on the same seam; FR-004 is silent, so neither is a defect against the spec and the ASYMMETRY is the hazard. More broadly there is no security/trust boundary for the M2 wire at all: untrusted bytecode deserialization with no authn/authz/signing, and a rule that FORBIDS a wire-side SRSW re-check. TCP half-close semantics were guessed independently in three stacked layers because no spec defined the run-termination barrier until D-9 - that produced a shipped ~30% data-loss race. Four normative docs disagree on _activate/2 argument mode; silent-otherwise is normative but unpinned; abort scope diverges per runtime; the named root cause is a MISSING FORCING FUNCTION - no normative semantics grid, so three runtimes drift silently. Seven 064-review rulings remain open (per-link replay exactly-once violations, replay-timing contract drift, an endpoint re-arm requirement silently downgraded, engine idle-break deafness, split-history WAL replay, foreign-dot retry, quickstart sample). The ISA is not frozen or versioned and the Section-15 binary codec exists in no language, with the two obligations mutually blocking. Six backlog issues are the same shape (FR-010 wording, FR-006 bounds, FR-011b negatives, US2 has no trust model at all, DLQ lifecycle, SC-001 preconditions).
- value: Turns 'each runtime behaved defensibly' into one written contract per seam, with a gate that fails when a seam has no normative row and when two runtimes ship different defaults on one seam. Closes the mechanism that generates cross-runtime divergence.
- notes: RCA cluster F4 of 6 (3rtask run 20260811T113723Z-1f7c). Invariant: every trust/lifecycle/protocol seam carries a written normative contract, and no two runtimes may ship different defaults on one seam. Subsumes 33 inventory ids incl. GL-26/EI-03/CD-09/E1, RS-34, RT-34/CD-01/RT-33, the 6 un-legitimated backlog issues A1-A6, CR-bf19-07 (10 coverage gaps), D8-01..08, D8-18, CD-10/11/E2/E3, CD-07, R6-R12, RS-31/32, RS-28/29, HO-08, CD-02/04. Several members are engineer-ruling-gated and stay parked until ruled - see R-GOV residual.

### `single-source-of-truth-one-authority-per-subject-provenance-on-generated-artifacts`  [promoted]

- title: Single source of truth: one authority per subject, provenance on generated artifacts
- epic: None
- spec_path: (EMPTY - no spec dir bound)
- effort: medium   risk: low: mostly doc reconciliation plus a contradiction detector; the codeconv provenance half is the only code work   priority_rank: 35
- touched_areas: AGENTS.md, CLAUDE.md, codeconv, docs, specs
- problem: Multiple artifacts claim authority over the same subject and disagree, actively mis-directing agents. AGENTS.md is a stale Codex-branded fork of an older CLAUDE.md that DIRECTLY CONTRADICTS current policy - it mandates a data-dir that CLAUDE.md marks strictly prohibited and calls the working drive exFAT - a live mis-direction risk for every codex session. Two entries in the CLAUDE.md/AGENTS.md 'Known limitations' list are provably FALSE: the grammar work disproved both '=.. is head-only' and 'structs-in-lists fail'. DISCIPLINE.md Part II is stale for this repo (wrong suite names, counts and Mac paths). A README mislabels which file is the pinned case list; a quickstart flow is stale; an 'INTERNAL_INCONSISTENCY' finding traced to one overloaded phrase in a doc comment. The roadmap cites a PHANTOM spec dir that another feature hard-depends on. 'stdlib' is a misnomer with no directory behind it - the engine derives the real path by string-replacing '/stdlib' with '/self.glp'. out/csharp is codegen-generated but 7 convergence fixes were applied BY HAND, so regenerating returns the bugs; and hand-deleting a generated file desynced the codeconv system of record while the Dart source still on the frontier can regenerate the deleted file.
- value: An agent can trust what it reads. Removes the class where a session follows a stale document into a prohibited or already-disproved action.
- notes: RCA cluster F5 of 6 (3rtask run 20260811T113723Z-1f7c). Invariant: exactly one authoritative artifact per subject; generated artifacts declare provenance; a contradiction between two authority-claiming documents fails a check. Subsumes 18 inventory ids: AG-01/02/03, CL-09/CL-10/HO-52/RS-33, DI-09, KI-12/D8-10/D8-13, RS-07, HO-60/61/62, CO-15, RS-10, HO-43/44, RS-39/40. Note AG-04 (the AGENTS.md gate that false-zeroes codex runs) is deliberately assigned to F1, not here - it is a silent-check defect that happens to live in a doc.

### `ynet-human-memorable-decentralized-naming-resolver`  [promoted]

- title: YNET human-memorable decentralized-naming resolver
- epic: epic-ynet-overlay-deferred-build-new-gaps
- spec_path: (EMPTY - no spec dir bound)
- effort: large (BUILD-NEW, no drop-in corpus reference)   risk: high — no proven decentralized human-naming design exists; may require a novel resolver or a trust-rooted registry   priority_rank: 60
- touched_areas: naming, ynet-transport/Dht
- problem: YNET serves only self-certified key->record resolution (FR-017); human-memorable decentralized naming is unsolved in the entire cycle-2 corpus (GNS citation-only). Callers needing a name->key resolution get 'further resolver required'.
- value: Completes the R8/R9 naming story; ties to the mstack (diana/nato) resolver gap named in cycle-1 §5.
- notes: Deferred from 051 Out-of-Scope. cycle-2 §6 + curator_report_cycle2. Sibling to qhstate 056. Do not fabricate resolutions.

### `ynet-mobile-background-battery-budget-scheduling-policy`  [promoted]

- title: YNET mobile background/battery-budget scheduling policy
- epic: epic-ynet-overlay-deferred-build-new-gaps
- spec_path: (EMPTY - no spec dir bound)
- effort: medium — BUILD-NEW; needs a real mobile-P2P energy reference fetched first   risk: medium — honest evidence gap; design must wait on a real reference to avoid guessing   priority_rank: 57
- touched_areas: leaf-mode, ynet-transport/Relay
- problem: Leaf/edge tier lacks a battery/data-budget scheduling policy. The cycle-2 corpus reference (energy-p2p-mobile.pdf) was a mislabeled file (corpus-integrity defect) and Veilid has zero battery awareness, so the gap could not be closed from evidence.
- value: Makes the constrained-device leaf tier genuinely battery- and data-friendly, not just non-relaying.
- notes: Deferred from 051 Out-of-Scope. cycle-2 §6 corpus-integrity defect. Re-fetch a real mobile-P2P energy paper before design.

### `bk-onrestart-per-host-configurable-auto-installable-fleet-resume`  [specified]

- title: bk-onrestart per-host configurable auto-installable fleet resume
- epic: None
- spec_path: specs/085-onrestart-fleet-resume
- effort: medium - a per-host config file, register/unregister verbs, an idempotent install/uninstall of the logon trigger, and a verification receipt   risk: low-medium - the launcher never edits a repo and never invokes a pipeline stage; the main risk is an install verb that silently no-ops on a host lacking Scheduled Task permission   priority_rank: None
- touched_areas: bk-onrestart, fleet-launcher, host-config
- problem: The post-reboot fleet resume is a host-specific script invocation each host reproduces by hand and gets wrong differently. The known-good mechanism (a logon Scheduled Task with a 45s delay relaunching every repo lane as TABS resumed in place with claude --continue) exists on ONE host. The window layout differs per host - GAVRI/GAVRIELLAS wants two windows with a specific grouping, OLAMNIT and ARIELLAS and SHIRAS want one - and today that difference lives in a human's memory behind a -Layout flag whose DEFAULT IS THE WRONG VALUE. The repo list is a hardcoded array in a launcher. There is no started-versus-requested receipt, so the known failure mode of N tabs opening and running nothing is only caught by counting processes by hand. And if a share is absent after the mount wait, every downstream tool silently reports an EMPTY BOARD at exit 0 rather than I cannot see the board.
- value: Makes fleet resume reproducible and self-maintaining rather than tribal. Removes a recurring class of silent failure (tabs that open and run nothing; an absent mount read as an empty board). Directly protects marathon continuity, which depends on every lane resuming IN PLACE rather than being summarised.
- notes: Codified as note cn-20260822T201322-dd39d00e. Scope: (1) per-host window layout as config - one-window or two-window plus the groupings; (2) the repo list as config not a hardcoded array; (3) bk-onrestart register / unregister run from inside a repo so the list is auto-captured; (4) one idempotent command installs or removes the at-logon trigger on ANY host; (5) a started-versus-requested receipt naming every lane it could not start; (6) mount-wait must refuse LOUDLY - the launcher is the only component that knows the mounts were not ready. Reference implementation to generalise: post-reboot-restart.ps1 -Layout Tabs -WaitForMounts, and the existing /bk-onrestart skill.

### `coordination-feature-stream-durable-superset-fix`  [specified]

- title: Coordination feature-stream durable superset fix — automated sync→reconcile(slug-tolerant)→board-reseed→allocate+commit→deliver loop with receipts
- epic: epic-issue-backlog-root-cause-closure-sweep-2026-08
- spec_path: specs/082-feature-stream-superset
- effort: large: spans buildkit scheduler+roadmap+colab subsystems + glpnet consumer; deploy to all hosts   risk: coordination/protocol authority (fleet-binding); ESCALATE: advisory→committed allocate autonomy boundary + wiring _require_root into the daemon may surface currently-forked boards as loud failures. Adopt: tolerant matcher must handle full slug divergence, not just NNN- prefix (dedupe.py incident).   priority_rank: 13
- touched_areas: buildkit-colab, buildkit-roadmap, buildkit-scheduler, glpnet
- problem: Systemic 'no steady feature stream' defect cluster: scheduler board never re-seeded from roadmap (silent state-forking — _require_root guards read-only cmds but not the mutating daemon); roadmap slug-mismatch (only exact/prefix matchers write state; the tolerant matcher feeds an advisory-only path) so shipped features never advance and next_to_build blocks dependents forever; coop delivery-split (projection write_disk_mirror keyed by box+bucket not to_host); fallback_used misread as root failure (it is hardcoded reader-impl provenance); allocation runs every cycle but is advisory-only, never commits; roadmap add_dependency/reconcile have no write timeout.
- value: An automated, receipted allocation pipeline that actually streams specify-eligible features to hosts. Superset loop: sync(coop merkle primitives)→reconcile(roadmap slug-tolerant, state-writing)→board-reseed(scheduler WP-from-portfolio via gap-to-backlog card)→allocate+commit(scheduler advisory→committed)→deliver(coop inbox/<host>+WP-id receipt), behind a fail-loud root/lock guard; fallback_used demoted to provenance.
- notes: COMBINED A+B remediation (codify cn-20260816T122806). A CLOSED: 6 links+7th=propagation; mechanism=CURATION FAILURE (3 scheduler writers collide on __main__.py/board.py/cycle.py). B DELIVERED (gavri 112500Z): candidate-slices+base-slice, per-candidate rubric, MECHANICAL conflict graph (plain code til fleet #56), judge-panel per candidate-PAIR, graft-not-discard, DECISION BRIEF+engineer canonical-writer decision NEVER merge. olamnit OWNS curation seam (§5.1). 🔴 RULING 20260816: DO NOT MERGE 069 (red suite migration-0032 + recovery.py:216 UNREDACTED secret); cherry-pick 20d78ba4 RETRACTED (would REVERT R2, not an ancestor); ROUTE = REIMPLEMENT the capacity-horizon fix on develop as ONE serial increment {allocate-writer + e_t_s surface + addressing-refusal + reimplemented-horizon + T2 graft}. Candidates are BEHAVIOURS to reimplement, not mergeable branches (f11a432b on no ref). PROPAGABLE constraint. De-dup: #13 proposed survivor vs gavri wp-supply-superset (pending ACK). NEXT: run Programme B over the behaviour list -> decision brief -> engineer canonical-writer ruling. Composes #43/#33/#29/F1 + fleet #56.

### `glptutorial-corpus-golden-reconciliation-stale-goldens-drift-guard-vendoring`  [specified]

- title: glptutorial corpus-golden reconciliation (stale goldens + drift-guard vendoring)
- epic: epic-issue-backlog-root-cause-closure-sweep-2026-08
- spec_path: specs/083-glptutorial-corpus-goldens
- effort: small   risk: low   priority_rank: 3
- touched_areas: codeconv tutorials propose/sync, tutorial corpus goldens
- problem: Three tutorial-corpus truth artefacts diverged from the live runtime: ch04/07 golden asserts a spec-invalid multi-clause guard loads (stale build); ch04/08 flatten golden predates the C# is_list fix; ch07 cssg_modules substrate is not vendored so sync --check cannot guard drift. Outcome comparison and drift guarding assert falsehoods.
- value: Restores the tutorial corpus as a trustworthy regression oracle; unblocks sync --check coverage of ch07. All three route via the existing codeconv tutorials propose flow (LAYOUT_NORMALISE / STALE_ARTEFACT / DRIFT_GAP).
- notes: Absorbs: I10-ch04/07, I10-ch04/08, I10-ch07-drift-gap. Evidence: 3rtask a625 builder-3. ESCALATE E2 open: stale-golden repair vs substrate vendoring are distinct mechanisms sharing one workflow - Gabi may split. Doc fix rides along: Issue 10 headline conflates approved scope with pending repairs.

### `madglp-writer-reader-address-discipline-closure-n-n-1-audit-residuals`  [specified]

- title: madGLP writer-reader address-discipline closure (N/N+1 audit + residuals)
- epic: epic-issue-backlog-root-cause-closure-sweep-2026-08
- spec_path: specs/079-madglp-writer-reader-discipline
- effort: small-medium   risk: low-medium - touches heap addressing; audit-first, behavior-preserving when cross-pointers are intact   priority_rank: 7
- touched_areas: glp_runtime/lib/multiagent, glp_runtime/lib/runtime/heap_fcp.dart, multiagent tests
- problem: Residual reliance on the N/N+1 allocation convention beside the authoritative heap cross-pointer mechanism - the root cause behind the whole fixed Issue-1/2/5/6 address-confusion defect class: heap_fcp.dart pairedReaderAddr retains a writerAddr+1 fallback; the three_agent_pipeline_boot false-positive residual (globalise/send per docs/bug-send-globalise-localise.md) is unverified; GlobalSendSpawn.readerAddr actually holds an onBind writer key but its doc comment still says 'reader to watch' (mad_helpers.dart:61-64).
- value: Closes the recorded audit deferral, removes the last convention-dependent fallback, structurally prevents recurrence of the defect class, and retires the madGLP false-positive test hazard.
- notes: Absorbs: Issue 1 residual, Issue 1b (N+1 audit), Issue 5 residual (field rename). Evidence: 3rtask a625 builder-1. ESCALATE E5 open: confirm bundled scope after inspecting heap_fcp.dart. Doc fix rides along: Issue 1 header 'Open' vs body 'Fixed'.

### `occurs-checked-substitution-pipeline-compiler-bind-time-occurs-check`  [specified]

- title: Occurs-checked substitution pipeline (compiler bind-time occurs-check)
- epic: epic-glp-compiler-robustness-occurs-check-term-traversal-hardening
- spec_path: specs/080-occurs-checked-substitution
- effort: small-medium: one shared occurs-check helper at ~9 bind sites x 2 duplicate copies   risk: 1.14 language-authority: changes what compiles (a cyclic = guard now fails cleanly vs crashes) — propose-first Gabi+Udi on the reject-vs-accept semantics   priority_rank: 4
- touched_areas: out/csharp/lib/compiler/analyzer.cs, out/csharp/lib/compiler/partial_evaluator.cs, out/csharp/lib/compiler/unify_result.cs
- problem: Compiler unification/substitution bind sites in partial_evaluator.cs and analyzer.cs insert var->term with no occurs-check, so a cyclic substitution (e.g. defined guard p(X,s(X)) called as p(Y,Y) -> subst[Y]=s(Y)) is created and ApplySubstitution's guard-less recursion StackOverflows (F-069-1) — an uncatchable crash during compile. analyzer.cs is a self-declared parallel copy with the identical defect.
- value: Turns an uncatchable compiler StackOverflow into a graceful UnifyFail/CompileError; closes F-069-1; lets the SC-003 fuzz run cyclic-= inputs without the non-cyclic-scoping workaround
- notes: Producer-side occurs-check layer. Both /bk-3rtask escalations RESOLVED by Gabi 2026-08-11: (1) defense-in-depth invariant (all walkers cycle-tolerant); (2) dedup NOW. This occurs-check lands ONCE on the consolidated unifier/substitution module -> DEPENDS ON the dedup feature. Closes backlog f-069-1-occurs-check; root cause partial_evaluator.cs:688 + analyzer.cs:1347. 1.14 propose-first Gabi+Udi on reject-vs-accept. Run 20260811T085855Z-8d6f.

### `ynet-consolidation`  [specified]

- title: YNET--consolidation
- epic: None
- spec_path: specs/065-ynet-consolidation
- effort: large   risk: medium-high: YNET items are pre-spec BUILD-NEW (naming resolver crosses addressing/identity; battery policy has no existing energy concept anywhere in the repo); the 051-ynet-transport branch overlap must be reconciled before any YNET spec is written to avoid duplicating unmerged work; 4 method/claim ESCALATEs remain open for the engineer (resolver output shape vs the literal ep(Host,Port) seam; dependency-absence inference; exhaustive-failure-path assertion; atomic-rename-as-precedent for env swaps)   priority_rank: 32
- touched_areas: .specify, codeconv, csharp/glp_link, docs, out/csharp/glp_repl, programs/tests/quic
- problem: Six open roadmap items were audited against the live repo by a 3-role blind team (run 20260803T134739Z-fa8a: 61 attributed claims, 54 CONFIRMED by a cross-provider critic). Findings: (a) both YNET overlay items are PRE-SPECIFICATION - captured records with spec_path=null, no spec dir, no design doc, no energy/duty-cycle concept in any transport spec, no naming/resolution/discovery primitive in the QUIC programs (an adjacent YNET spec dir exists only on the unmerged branch origin/051-ynet-transport - critic REFUTE 3e117a40, must be reconciled not duplicated); (b) durable-listener-service-box is code-complete-unshipped, not missing - 9/9 FRs evidenced, 11/14 tasks done, tag IMPLEMENTED-UNSHIPPED, so it needs finishing (T012-T014) not building; (c) the three toolchain-ops items are unimplemented but sit on shipped machinery - deploy CLI has zero smoke/venv/atomic/rollback surface, roadmap advance is single-id only with no batch form, version dirs carry inconsistent CalVer strings, and the GEPA/DSPy stack optimises only the refine engine, not coop/scheduler/marathon.
- value: One consolidated workstream closes the whole audited gap set in dependency order instead of six disconnected items: finish-and-ship the already-built service box, then the additive low-risk toolchain wins (batch advance + CalVer normalisation FIRST per the builder's sequencing - purely additive, working precedents in buildkit-deploy latest all), then atomic installs (prerequisites venvlock/doctor/husk already shipped; reusable idioms = Section Q AOT smoke harness + prereq-patterns atomic-rename), then the two YNET overlay items which are genuine BUILD-NEW and must be specified first.
- notes: Source: 3rtask run 20260803T134739Z-fa8a (frozen 14-element method, 3 blind builders over pairwise-disjoint slices, cross-provider codex critic; 54 CONFIRM / 3 REFUTE / 4 ESCALATE). Zero corroboration was structural (disjoint slices) - cross-verification was done by the non-blind critic, which is also the open method escalate E9 (slice-bound builders cannot see repo-wide ship markers). Recommended build order from the evidence: 1) 064 finish+ship (T012-T014, restore the replay-idempotence unit test, run the history drill at SC-002 scale N=100, settle the per-link replay question), 2) batch-roadmap-advance + CalVer normalisation (small, additive, precedented), 3) atomic-toolchain-installs (prereqs shipped), 4) YNET naming resolver (spec first; reconcile with origin/051-ynet-transport), 5) YNET mobile battery-budget policy (spec first), 6) buildkit-coordination-optimisation (GEPA/DSPy retarget onto coop/scheduler/marathon). Full attributed evidence + escalations: .specify/3rtask/runs/20260803T134739Z-fa8a/.

## Dependency edges touching not-closed features

```
```

(0 of 279 dependency rows touch a not-closed feature.)

## Spec directories present in the repo working tree

```
001-d2net-scaffold
002-d2net-init
005-d2net-pglite-bridge
006-d2net-init-skill
007-incremental-exclusions
008-remove-exclude
009-scaffold-mirror
010-scaffold-skill
011-prereq-patterns-catalog
012-codeconv-runner
014-package-self-import-resolution
015-codeconv-depgraph
016-codeconv-init-scaffold-langpair
017-conversion-plan-agents
018-codeconv-builder
019-codeconv-codegen
020-trace-equivalence-fidelity
022-glptutorial-list
023-glptutorial-run
024-marathon-stage-harness
025-multi-protocol-link-layer
026-engine-review-dossier
027-refinement-verification-framework
028-evidence-based-constitution
029-il-codec-spike
030-marathon-refinement
031-gleam-port-spike
032-codeconv-gleam-langpair
033-glp-gleam-subtree-scaffold
034-glp-gleam-core-terms-and-heap
035-semantic-tombstone-enrichment
036-glp-gleam-baseline-program
036-http3-quic-ws-link
037-virtual-3270-term
038-result-codec-and-framecodec-ride
039-m2-0-verify-erlang-monitor-atomvm
040-rcopy-file-transfer-service
041-crdtmsg-mvp
042-crdtmsg-verify-harden
043-xsd-schema-language
049-wave1-guard-link-acceptance
050-full-gleam-combined
050-glp-native-quic-link
051-ynet-transport
059-full-scope-gleam-glp-implementation
060-wave3-full-gleam-chain
061-wave-2-consolidated-repl-engine-split-spine
062-wave-4-consolidated-parallel-safe-fillers
063-wave-5-consolidated-captured-triad
064-durable-listener-service-box
064-post-wave-gap-closure
065-glp-runtime-consol
065-ynet-consolidation
066-wave6-consolidation
069-sc-002-il-parity-bridge
076-typechecker-body-atom-moding
077-guarded-term-traversal
078-verification-receipts
079-madglp-writer-reader-discipline
080-occurs-checked-substitution
082-feature-stream-superset
083-glptutorial-corpus-goldens
085-onrestart-fleet-resume
```

(63 spec directories on disk.)

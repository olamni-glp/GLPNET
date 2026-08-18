# Slice A — the PORTFOLIO to allocate (25 open features, post score+promote 2026-08-12)

| state | epic | feature_id | WSJF | RICE | spec |
|---|---|---|---|---|---|
| promoted | Issue-backlog root-cause closure sweep 2026-08 | pglite-pg16-to-pg17-shared-cluster-migration-resolution-verify-premise-execute-or-retire | 14.00 | 1800 | - |
| promoted | (standalone) | verification-receipts-and-loud-failure-no-check-may-pass-without-proving-it-ran | 7.80 | 1173 | - |
| promoted | Issue-backlog root-cause closure sweep 2026-08 | glptutorial-corpus-golden-reconciliation-stale-goldens-drift-guard-vendoring | 6.50 | 1700 | - |
| shipped | (standalone) | atomic-toolchain-installs-venv-swap-post-install-smoke | 5.67 | 160 | - |
| promoted | Issue-backlog root-cause closure sweep 2026-08 | madglp-writer-reader-address-discipline-closure-n-n-1-audit-residuals | 5.33 | 2667 | - |
| promoted | Separation of REPL Front-end from Engine Execution & Scheduler | sc-002-il-parity-bridge-antlr-parse-tree-engine-ast-lowering-adoption-decision | 4.33 | 5333 | - |
| promoted | Issue-backlog root-cause closure sweep 2026-08 | type-checker-body-atom-moding-accept-head-flipped-readers-unblock-2 | 4.20 | 2800 | - |
| specified | Distributed GLP connectivity | qr-link-provisioning | 4.00 | 252 | specs/067-qr-link-provisioning |
| shipped | (standalone) | batch-roadmap-advance-calver-version-dir-normalisation | 3.67 | 80 | - |
| promoted | Issue-backlog root-cause closure sweep 2026-08 | front-end-goal-term-acceptance-completeness-parser-repl-goal-builders-cross-runtime | 3.60 | 3000 | - |
| promoted | (standalone) | per-host-toolchain-and-environment-contract-declared-machine-checked-loudly-refused | 3.60 | 960 | - |
| promoted | (standalone) | multi-host-state-discipline-reversible-states-untracked-derived-artifacts-unique-identities | 3.00 | 680 | - |
| promoted | Distributed GLP connectivity | 041-cross-runtime-and-two-host-acceptance-completion-t055-parity-sc-009-e2e | 2.62 | 625 | - |
| promoted | (standalone) | seam-specification-normative-contracts-at-every-trust-lifecycle-and-protocol-boundary | 2.62 | 578 | - |
| promoted | (standalone) | single-source-of-truth-one-authority-per-subject-provenance-on-generated-artifacts | 2.60 | 540 | - |
| promoted | Issue-backlog root-cause closure sweep 2026-08 | crdtmsg-post-mvp-completion-cose-sign1-wrapper-1-14-gated-glp-policy-guard | 2.40 | 420 | - |
| promoted | (standalone) | buildkit-coordination-optimisation-gepa-dspy-coop-scheduler-marathon-buildkit-tooling | 2.00 | 400 | - |
| promoted | (standalone) | product-defect-burn-down-with-regression-proof-no-defect-closed-on-a-fixer-s-own-green-run | 1.23 | 240 | - |
| promoted | Distributed GLP connectivity | distributed-unification-quiescence-protocol-two-runtime-spec-first | - | - | - |
| specified | Full Gleam implementation | full-scope-gleam-glp-implementation | - | - | specs/059-full-scope-gleam-glp-implementation |
| specified | Roadmap sweep 2026-07 consolidated waves | wave6-consolidation | - | - | specs/066-wave6-consolidation |
| promoted | YNET overlay — deferred BUILD-NEW gaps | ynet-human-memorable-decentralized-naming-resolver | - | - | - |
| promoted | YNET overlay — deferred BUILD-NEW gaps | ynet-mobile-background-battery-budget-scheduling-policy | - | - | - |
| specified | (standalone) | durable-listener-service-box | - | - | specs/064-durable-listener-service-box |
| specified | (standalone) | ynet-consolidation | - | - | specs/065-ynet-consolidation |

## Known sequencing facts (from the roadmap + prior sessions)
- The 6 sweep features (epic Issue-backlog root-cause closure sweep 2026-08) came from 3rtask run a625 clustering known-issues; sweep feature 1 (type-checker) already has spec dir specs/076 and marathon mrun-d086da8a860f at 5/10, gated on an engineer 1.14 language-authority ruling.
- qr-link-provisioning (specs/067) has implement COMPLETE on branch 067b-qr-link-continuation; remaining stages codexreview -> ship -> close; marathon mrun-d15072abb4c4 6/9.
- durable-listener-service-box (specs/064) is being shipped by host gavriella; it was blocked on cert material now re-dropped.
- 041-cross-runtime-and-two-host-acceptance is ENVIRONMENT-BLOCKED: needs a second LAN host (gavri endpoint) reachable AND an MSVC/msquic-built quicer NIF; neither exists on the primary host.
- pglite-pg16-to-pg17 premise is likely VOID (C:/pglite absent, .pgdb already PG17) - it may collapse to a doc/retire task.
- The 6 standalone fleet-RCA features (verification-receipts 7.8, per-host-toolchain 3.6, multi-host-state 3.0, seam-spec 2.62, single-source 2.60, defect-burn-down 1.23) were authored+scored by host gavriella from a fleet-wide RCA and imported via CRDT.
<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# ROADMAP — ALL EPICS & FEATURES **NOT CLOSED**

    host   SHIRAS          lane  shiras.glpnet / olamni-glp/GLPNET
    round  74              generated 2026-09-05T11:05Z
    total  49 not-closed of 138 features · 21 epics

Sorted by WSJF descending — the board's own recommended-order signal.

| # | state | WSJF | RICE | feature | epic |
|---|---|---:|---:|---|---|
| - | `reviewed` | 34.00 | 4800.00 | **stable-federation-identity-persisted-quic-keypair** — Stable federation identity: persisted QUIC keypair so SPKI pins survive restart | — |
| - | `promoted` | 19.50 | 774000.00 | **differential-cross-runtime-acceptance-gate** — Multi-runtime acceptance criteria must be measured differentially, not restated per-runtime | — |
| - | `promoted` | 9.67 | 1425.00 | **atomic-claim-not-check-then-rename** — Exclusivity and durability are different properties: replace every check-then-rename claim with an atomic CreateNew claim | — |
| - | `promoted` | 9.00 | 380.00 | **federation-keystore-key-and-pin-one-atomic-act** — Federation keystore: bind the key and its published pin in ONE atomic act | — |
| - | `promoted` | 8.00 | 1350.00 | **identity-durability-proven-across-a-reboot** — Identity durability proven across a REBOOT, not across processes in one boot | — |
| 1 | `implemented` | 7.80 | 1173.00 | **verification-receipts-and-loud-failure-no-check-may-pass-without-proving-it-ran** — Verification receipts and loud failure (no check may pass without proving it ran) | — |
| - | `specified` | 7.00 | 5400.00 | **bk-onrestart-per-host-configurable-auto-installable-fleet-resume** — bk-onrestart per-host configurable auto-installable fleet resume | — |
| 3 | `specified` | 6.50 | 1700.00 | **glptutorial-corpus-golden-reconciliation-stale-goldens-drift-guard-vendoring** — glptutorial corpus-golden reconciliation (stale goldens + drift-guard vendoring) | issue-backlog-root-cause-closure-sweep-2026-08 |
| 4 | `specified` | 6.00 | 2000.00 | **occurs-checked-substitution-pipeline-compiler-bind-time-occurs-check** — Occurs-checked substitution pipeline (compiler bind-time occurs-check) | glp-compiler-robustness-occurs-check-term-traversal-hardening |
| - | `implemented` | 5.40 | 2880.00 | **quic-federation-transport** — QUIC federation transport for the ynet oracle | — |
| 7 | `implemented` | 5.33 | 2666.67 | **madglp-writer-reader-address-discipline-closure-n-n-1-audit-residuals** — madGLP writer-reader address-discipline closure (N/N+1 audit + residuals) | issue-backlog-root-cause-closure-sweep-2026-08 |
| - | `reviewed` | 5.20 | 810.00 | **ynet-minted-lane-identity-resolve-address-independent** — YNET minted lane identity: address-independent ids, Resolve maps id to address, Refused is a valid answer | — |
| - | `promoted` | 5.20 | 810.00 | **l0-consumer-resolution-no-false-absence-from-a-projection-scoped-search** — L0 consumer resolution: no lane may report an absence it cannot scope, fleet-wide | — |
| - | `promoted` | 4.80 | 3600.00 | **renderers-read-export-fold-not-status** — Renderers must read the signed-export heads fold, never buildkit-roadmap status | issue-backlog-root-cause-closure-sweep-2026-08 |
| - | `promoted` | 4.60 | 540.00 | **measured-not-declared-environment-predicates-slo-gates-and-capability-probes** — Measured-not-declared environment predicates: SLO gates and capability probes must measure the host, never declare it | — |
| - | `promoted` | 4.40 | 1920.00 | **fleet-t24-tactical-action-plan** — Fleet T24 tactical action plan: ratification, adaptation and BEACON realization | — |
| 13 | `specified` | 4.25 | 2625.00 | **coordination-feature-stream-durable-superset-fix** — Coordination feature-stream durable superset fix — automated sync→reconcile(slug-tolerant)→board-reseed→allocate+commit→deliver loop with receipts | issue-backlog-root-cause-closure-sweep-2026-08 |
| - | `promoted` | 3.62 | 3937.50 | **cpm-crdt-cross-ecosystem-package-state-history-chain-points-and-upgrade-proposals** — CPM-CRDT: cross-ecosystem package state, version history, chain points and agreed upgrade proposals - hardened in buildkit | fleet-interconnectivity-observability-hardening |
| 24 | `promoted` | 3.60 | 960.00 | **per-host-toolchain-and-environment-contract-declared-machine-checked-loudly-refused** — Per-host toolchain and environment contract (declared, machine-checked, loudly refused) | — |
| - | `promoted` | 3.38 | 367.50 | **fleet-cpm-crdt-record-bk-cpm-1** — Fleet Central Package Management over a CRDT record (BK-CPM-1) | — |
| - | `promoted` | 3.00 | 3200.00 | **takt-and-token-persistence-to-ducklake** — Persist takt and per-phase token use to the DuckLake and serve all takt reporting from it | issue-backlog-root-cause-closure-sweep-2026-08 |
| - | `promoted` | 3.00 | 3600.00 | **cross-repo-cross-host-era-takt-crdt-hardened-in-buildkit** — Cross-repo cross-host ERA TAKT: CRDT schema, closed repo-slug registry and a lake that can actually be compared - hardened in buildkit | fleet-interconnectivity-observability-hardening |
| 30 | `promoted` | 3.00 | 680.00 | **multi-host-state-discipline-reversible-states-untracked-derived-artifacts-unique-identities** — Multi-host state discipline (reversible states, untracked derived artifacts, unique identities) | — |
| - | `promoted` | 3.00 | 150.00 | **oracle-elastic-lane-pool-launcher-convergence** — Oracle-managed elastic lane pool: converge the two onrestart launchers behind a capacity-gated slot roster | — |
| - | `promoted` | 2.88 | 393.75 | **glp-repl-fmb-split-over-ynet-for-qhsm-terminal** — GLP REPL front/middle/back separation over the ynet transport contract, for the QHSM virtual terminal | — |
| 33 | `promoted` | 2.62 | 625.00 | **041-cross-runtime-and-two-host-acceptance-completion-t055-parity-sc-009-e2e** — 041 cross-runtime and two-host acceptance completion (T055 parity + SC-009 e2e) | distributed-glp-connectivity |
| - | `promoted` | 2.62 | 311.54 | **scheduler-feature-stream-durable-healing-and-hardening** — Scheduler feature-stream durable healing and hardening (the four-break chain) | issue-backlog-root-cause-closure-sweep-2026-08 |
| 32 | `specified` | 2.62 | 900.00 | **ynet-consolidation** — YNET--consolidation | — |
| 34 | `promoted` | 2.62 | 577.50 | **seam-specification-normative-contracts-at-every-trust-lifecycle-and-protocol-boundary** — Seam specification: normative contracts at every trust, lifecycle and protocol boundary | — |
| - | `promoted` | 2.62 | 276.92 | **consolidated-hardening-spine** — Consolidated hardening spine: full hardened specify-design-implement-codexreview with durable healing + hardening | — |
| 35 | `promoted` | 2.60 | 540.00 | **single-source-of-truth-one-authority-per-subject-provenance-on-generated-artifacts** — Single source of truth: one authority per subject, provenance on generated artifacts | — |
| - | `promoted` | 2.46 | 512.31 | **yx-ypm-yngenios-package-manager** — /yx-ypm — the Yngenios Package Manager (uniform cross-language + first-party package management) | — |
| 36 | `promoted` | 2.40 | 420.00 | **crdtmsg-post-mvp-completion-cose-sign1-wrapper-1-14-gated-glp-policy-guard** — crdtmsg post-MVP completion (COSE_Sign1 wrapper + 1.14-gated GLP policy guard) | issue-backlog-root-cause-closure-sweep-2026-08 |
| - | `promoted` | 2.23 | 3000.00 | **glp-repl-front-middle-back-ynet-mailbox** — GLP REPL front/middle/back separation over YNET realtime mailboxes with suspension-preserving session semantics | — |
| 43 | `analyzed` | 2.00 | 738.46 | **full-scope-gleam-glp-implementation** — Full-scope Gleam GLP implementation | full-gleam |
| 46 | `promoted` | 2.00 | 400.00 | **buildkit-coordination-optimisation-gepa-dspy-coop-scheduler-marathon-buildkit-tooling** — buildkit coordination optimisation (GEPA/DSPy) — coop, scheduler, marathon, buildkit tooling | — |
| - | `promoted` | 1.85 | 138.46 | **iroh-tier0-quic-provider-vendored-rust-behind-ynet-transport-seam** — iroh tier-0 QUIC provider: vendored Rust iroh/quinn behind the ynet_transport provider seam, parity-proven before GlpQuick retirement | — |
| 54 | `promoted` | 1.62 | 692.31 | **distributed-unification-quiescence-protocol-two-runtime-spec-first** — Distributed unification + quiescence protocol (two-runtime, spec-first) | distributed-glp-connectivity |
| - | `promoted` | 1.62 | 3500.00 | **port-059-gleam-link-layer-tests** — Port 059's Gleam link-layer tests onto develop's actor-style link_runtime | — |
| 57 | `promoted` | 1.38 | 400.00 | **ynet-mobile-background-battery-budget-scheduling-policy** — YNET mobile background/battery-budget scheduling policy | ynet-overlay-deferred-build-new-gaps |
| 60 | `promoted` | 1.23 | 184.62 | **ynet-human-memorable-decentralized-naming-resolver** — YNET human-memorable decentralized-naming resolver | ynet-overlay-deferred-build-new-gaps |
| 59 | `promoted` | 1.23 | 240.00 | **product-defect-burn-down-with-regression-proof-no-defect-closed-on-a-fixer-s-own-green-run** — Product-defect burn-down with regression proof (no defect closed on a fixer's own green run) | — |
| - | `promoted` | 1.23 | 86.15 | **glp-repl-front-middle-back-separation-yngenios-app-terminal-front-end** — GLP REPL front/middle/back separation with a YNGENIOS-app terminal front end | — |
| 49 | `analyzed` | 0.85 | 62.31 | **wave6-consolidation** — Wave6 consolidation | roadmap-sweep-2026-07-consolidated-waves |
| - | `promoted` | — | — | **pbft-leader-election** — PBFT leader election over the federated board | — |
| - | `promoted` | — | — | **iroh-quic-transport** — iroh QUIC as the ynet transport from L0 up | — |
| - | `promoted` | — | — | **qhsm-virtual-terminals** — QHSM/QMSM-wrapped virtual terminals routed through the oracle | — |
| - | `promoted` | — | — | **declared-unconsumed-guard** — Declared-but-unconsumed capability guard, cross-language | — |
| - | `promoted` | — | — | **csharp-tree-hardening** — csharp tree hardening: bounds, canonical encodings and signature preimages | — |

## Counts by state

| state | count |
|---|---:|
| `implemented` | 3 |
| `analyzed` | 2 |
| `specified` | 5 |
| `promoted` | 37 |
| `reviewed` | 2 |

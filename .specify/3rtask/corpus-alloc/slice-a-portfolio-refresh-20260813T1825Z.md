# Slice A — PORTFOLIO refresh, 2026-08-13T18:25Z (post-sync, authoritative)

**Provenance.** Produced on host `ariellas` from `buildkit-roadmap --json status` immediately
after the sync round that imported 3 files / 177 lines from
`I:/coop/glpnet/roadmap-sync/inbox`, reconciled (no changes), deduped (114 live, 0 duplicate
groups), exported `ariellas__glpnet__20260813T182501Z.json` (20 epics / 114 features / 3444
journal lines), published both legs sha-identical
(`EEB3907BCB209B7081007F424D167D95BE1BB475C267AABC39AC5A987A711508`), and passed
`replay --verify`. Committed `ce99e512`, pushed.

## 🔴 This supersedes the row counts in SUBJECT-20260813.md

SUBJECT-20260813.md states "25 not-closed rows of which only 23 are allocatable (2 are already
shipped and need close-out only)". **That is stale and its allocatable count is wrong in kind,
not just in number.** Current authoritative decomposition:

| Bucket | Count | Meaning for this design |
|---|---|---|
| **Not closed (total)** | **26** | the whole open portfolio |
| **Specify-eligible** (`promoted` 16 + `refined` 1) | **17** | the ONLY rows `/bk-specify` may be allocated for |
| **Close-out-only** (`released` 3 + `shipped` 2) | **5** | need `advance`/`/bk-close`, NOT a pipeline lane |
| **Already specified** (`specified`) | **4** | already in-pipeline; resume mid-chain, do NOT re-specify |

17 + 5 + 4 = 26. Any design that allocates 23 features to `/bk-specify` is allocating work that
does not exist: 5 of those rows are past implement and 4 already hold a spec dir.

## The 17 specify-eligible rows (the allocatable set)

Scored (13):

| feature_id | WSJF | RICE | epic |
|---|---|---|---|
| verification-receipts-and-loud-failure-no-check-may-pass-without-proving-it-ran | 7.80 | 1173.00 | (standalone) |
| glptutorial-corpus-golden-reconciliation-stale-goldens-drift-guard-vendoring | 6.50 | 1700.00 | issue-backlog-sweep |
| madglp-writer-reader-address-discipline-closure-n-n-1-audit-residuals | 5.33 | 2666.67 | issue-backlog-sweep |
| type-checker-body-atom-moding-accept-head-flipped-readers-unblock-2 | 4.20 | 2800.00 | issue-backlog-sweep |
| front-end-goal-term-acceptance-completeness-parser-repl-goal-builders-cross-runtime | 3.60 | 3000.00 | issue-backlog-sweep |
| per-host-toolchain-and-environment-contract-declared-machine-checked-loudly-refused | 3.60 | 960.00 | (standalone) |
| multi-host-state-discipline-reversible-states-untracked-derived-artifacts-unique-identities | 3.00 | 680.00 | (standalone) |
| seam-specification-normative-contracts-at-every-trust-lifecycle-and-protocol-boundary | 2.63 | 577.50 | (standalone) |
| 041-cross-runtime-and-two-host-acceptance-completion-t055-parity-sc-009-e2e | 2.63 | 625.00 | distributed-glp-connectivity |
| single-source-of-truth-one-authority-per-subject-provenance-on-generated-artifacts | 2.60 | 540.00 | (standalone) |
| crdtmsg-post-mvp-completion-cose-sign1-wrapper-1-14-gated-glp-policy-guard | 2.40 | 420.00 | issue-backlog-sweep |
| buildkit-coordination-optimisation-gepa-dspy-coop-scheduler-marathon-buildkit-tooling | 2.00 | 400.00 | (standalone) |
| product-defect-burn-down-with-regression-proof-no-defect-closed-on-a-fixer-s-own-green-run | 1.23 | 240.00 | (standalone) |

**Unscored (4) — no WSJF, no RICE at all.** These carry *no* priority evidence. A design that
slots them into a ranked order is inventing the rank:

- `occurs-checked-substitution-pipeline-compiler-bind-time-occurs-check` — `refined`, WSJF 6.00 / RICE 2000.00 *(scored; the only non-`promoted` eligible row)*
- `ynet-mobile-background-battery-budget-scheduling-policy` — unscored
- `ynet-human-memorable-decentralized-naming-resolver` — unscored
- `distributed-unification-quiescence-protocol-two-runtime-spec-first` — unscored

## The 5 close-out-only rows (NOT lane work)

| feature_id | state | action |
|---|---|---|
| sc-002-il-parity-bridge-antlr-parse-tree-engine-ast-lowering-adoption-decision | released | advance → closed |
| guarded-term-traversal-utilities-cycle-tolerant-compiler-walkers-pe-analyzer-dedup | released | advance → closed |
| durable-listener-service-box | released | advance → closed (spec 064) |
| atomic-toolchain-installs-venv-swap-post-install-smoke | shipped | advance → released → closed |
| batch-roadmap-advance-calver-version-dir-normalisation | shipped | advance → released → closed |

## The 4 already-specified rows (resume, do not re-specify)

| feature_id | spec dir |
|---|---|
| qr-link-provisioning | specs/067-qr-link-provisioning |
| ynet-consolidation | specs/065-ynet-consolidation |
| wave6-consolidation | specs/066-wave6-consolidation |
| full-scope-gleam-glp-implementation | specs/059-full-scope-gleam-glp-implementation |

## Duration evidence

`buildkit-roadmap --json status` carries **no duration field of any kind** — no PERT triple, no
optimistic/likely/pessimistic, no elapsed history, for any of the 26 rows. The only per-feature
duration data known to exist anywhere in the fleet is 2 self-declared marathon-step estimates
from a single host, denominated in steps rather than time. **There is therefore no evidence
base from which an honest P50/P80/P95 or a time-denominated critical path can be computed for
this portfolio.** A CPM/PERT view may legitimately publish *structure* (ordering, dependency
edges, lane parallelism, unit counts) and must *refuse* to publish time.

## Dependency edges

`blocked_by` is populated on **0 of 26** rows; 7 rows carry `advisory_overlap` only. Advisory
overlap is a heuristic textual-similarity edge, **not** a hard ordering constraint — it becomes
one only via `roadmap confirm-dependency`, which has not been run for any of them. Any
serialisation the design asserts between two of these 26 features is therefore **not** sourced
from the roadmap's recorded dependency graph.

## Foreign / refused entities

The import refused **3 untagged entities** (not foreign — untagged): the epic *GLP compiler
robustness (occurs-check & term-traversal hardening)* and its two features
`occurs-checked-substitution-pipeline-compiler-bind-time-occurs-check` and
`guarded-term-traversal-utilities-cycle-tolerant-compiler-walkers-pe-analyzer-dedup`. Both
features are nonetheless present and live in local HEAD (one `refined`, one `released`), so the
refusal is about the *inbound* copies, not about local existence.

# SLICE W1 — the glpnet bk-scheduler board, folded from the DURABLE per-actor op logs

Source of record: `\192.168.0.108\GAVRI_D\coop\glpnet\sched\ops\<actor>\<actor>-ops-NNNNNN.jsonl`
(5 grow-only JSONL op logs: ariellas, ariellas.hatzinor, ariellas.yngenios-windows, gavriella, olamnit).
Folded by R2 total order (timestamp, actor, seq). This is the SUBSTRATE, not the rendered view.

Total ops folded: 116.  Distinct work-packet ids: 32.
Non-WP stream ids excluded from the WP count: epoch, plan, policy, glpnet-host-availability.

## Fold by state

```
backlog        23
done           1
escalated      1
in-progress    4
ready          3
```

## Every work packet

| wp_id | state | owner | distinct claimants | allocate ops | first op | last op |
|---|---|---|---|---|---|---|
| `wave-2-consolidated-repl-engine-split-spine` | in-progress | gavriella | 2 | 1 | 2026-07-29T19:37:45Z | 2026-08-22T19:56:20Z |
| `wave-5-consolidated-captured-triad` | in-progress | gavriella | 1 | 1 | 2026-07-29T19:44:45Z | 2026-08-22T19:56:15Z |
| `wave-4-consolidated-parallel-safe-fillers` | backlog | olamnit | 1 | 0 | 2026-07-29T19:45:20Z | 2026-07-29T19:48:04Z |
| `076-type-checker-body-atom-moding:implement-to-close` | done | ariellas | 1 | 0 | 2026-08-13T21:42:13Z | 2026-08-22T17:58:32Z |
| `067-qr-link-provisioning:codexreview-to-close-SHIP-TOKEN-GATED` | escalated | ariellas | 1 | 0 | 2026-08-13T21:42:13Z | 2026-08-22T16:20:50Z |
| `trust-material-controlled-reproduction:ariellas-only-clean-control-host` | ready | ariellas | 1 | 0 | 2026-08-13T21:42:13Z | 2026-08-22T16:20:28Z |
| `wp-041-cross-runtime-and-two-host-acceptance-completion-t055-pa` | backlog | (none) | 0 | 1 | 2026-08-18T14:32:58Z | 2026-08-18T14:32:58Z |
| `wp-atomic-toolchain-installs-venv-swap-post-install-smoke` | backlog | (none) | 0 | 1 | 2026-08-18T14:32:58Z | 2026-08-18T14:32:58Z |
| `wp-buildkit-coordination-optimisation-gepa-dspy-coop-scheduler-` | backlog | (none) | 0 | 1 | 2026-08-18T14:32:59Z | 2026-08-18T14:32:59Z |
| `wp-coordination-feature-stream-durable-superset-fix` | ready | olamnit | 1 | 2 | 2026-08-18T14:32:59Z | 2026-08-22T18:04:30Z |
| `wp-crdtmsg-post-mvp-completion-cose-sign1-wrapper-1-14-gated-gl` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:00Z | 2026-08-18T14:33:00Z |
| `wp-distributed-unification-quiescence-protocol-two-runtime-spec` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:00Z | 2026-08-18T14:33:00Z |
| `wp-durable-listener-service-box` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:00Z | 2026-08-18T14:33:00Z |
| `wp-front-end-goal-term-acceptance-completeness-parser-repl-goal` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:01Z | 2026-08-18T14:33:01Z |
| `wp-full-scope-gleam-glp-implementation` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:02Z | 2026-08-18T14:33:02Z |
| `wp-glptutorial-corpus-golden-reconciliation-stale-goldens-drift` | in-progress | (none) | 0 | 2 | 2026-08-18T14:33:02Z | 2026-08-22T18:05:35Z |
| `wp-guarded-term-traversal-utilities-cycle-tolerant-compiler-wal` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:02Z | 2026-08-18T14:33:02Z |
| `wp-madglp-writer-reader-address-discipline-closure-n-n-1-audit-` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:03Z | 2026-08-18T14:33:03Z |
| `wp-multi-host-state-discipline-reversible-states-untracked-deri` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:03Z | 2026-08-18T14:33:03Z |
| `wp-occurs-checked-substitution-pipeline-compiler-bind-time-occu` | ready | (none) | 0 | 2 | 2026-08-18T14:33:03Z | 2026-08-19T09:48:03Z |
| `wp-per-host-toolchain-and-environment-contract-declared-machine` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:04Z | 2026-08-18T14:33:04Z |
| `wp-product-defect-burn-down-with-regression-proof-no-defect-clo` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:04Z | 2026-08-18T14:33:04Z |
| `wp-qr-link-provisioning` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:05Z | 2026-08-18T14:33:05Z |
| `wp-sc-002-il-parity-bridge-antlr-parse-tree-engine-ast-lowering` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:05Z | 2026-08-18T14:33:05Z |
| `wp-seam-specification-normative-contracts-at-every-trust-lifecy` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:05Z | 2026-08-18T14:33:05Z |
| `wp-single-source-of-truth-one-authority-per-subject-provenance-` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:06Z | 2026-08-18T14:33:06Z |
| `wp-type-checker-body-atom-moding-accept-head-flipped-readers-un` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:06Z | 2026-08-18T14:33:06Z |
| `wp-verification-receipts-and-loud-failure-no-check-may-pass-wit` | in-progress | gavriella | 1 | 3 | 2026-08-18T14:33:06Z | 2026-08-22T19:56:10Z |
| `wp-wave6-consolidation` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:06Z | 2026-08-18T14:33:06Z |
| `wp-ynet-consolidation` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:07Z | 2026-08-18T14:33:07Z |
| `wp-ynet-human-memorable-decentralized-naming-resolver` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:07Z | 2026-08-18T14:33:07Z |
| `wp-ynet-mobile-background-battery-budget-scheduling-policy` | backlog | (none) | 0 | 1 | 2026-08-18T14:33:07Z | 2026-08-18T14:33:07Z |

## Per-packet op history (verbatim fold)

### `wave-2-consolidated-repl-engine-split-spine`

```
2026-07-29T19:37:45Z claim by ariellas
2026-08-14T13:02:12Z claim by gavriella
2026-08-22T12:53:08Z transition by gavriella backlog -> ready
2026-08-22T19:55:27Z allocate by gavriella -> assignee=None
2026-08-22T19:56:20Z transition by gavriella ready -> in-progress
```

### `wave-5-consolidated-captured-triad`

```
2026-07-29T19:44:45Z claim by gavriella
2026-07-29T20:23:41Z claim by gavriella
2026-08-14T00:17:49Z note by gavriella
2026-08-22T12:53:14Z transition by gavriella backlog -> ready
2026-08-22T19:55:23Z allocate by gavriella -> assignee=None
2026-08-22T19:56:15Z transition by gavriella ready -> in-progress
```

### `wave-4-consolidated-parallel-safe-fillers`

```
2026-07-29T19:45:20Z claim by olamnit
2026-07-29T19:48:04Z claim by olamnit
```

### `076-type-checker-body-atom-moding:implement-to-close`

```
2026-08-13T21:42:13Z claim by ariellas
2026-08-22T16:20:06Z transition by ariellas backlog -> ready
2026-08-22T17:58:32Z transition by ariellas ready -> done
```

### `067-qr-link-provisioning:codexreview-to-close-SHIP-TOKEN-GATED`

```
2026-08-13T21:42:13Z claim by ariellas
2026-08-22T16:20:50Z transition by ariellas backlog -> escalated
```

### `trust-material-controlled-reproduction:ariellas-only-clean-control-host`

```
2026-08-13T21:42:13Z claim by ariellas
2026-08-22T16:20:28Z transition by ariellas backlog -> ready
```

### `wp-041-cross-runtime-and-two-host-acceptance-completion-t055-pa`

```
2026-08-18T14:32:58Z allocate by ariellas -> assignee=None
```

### `wp-atomic-toolchain-installs-venv-swap-post-install-smoke`

```
2026-08-18T14:32:58Z allocate by ariellas -> assignee=None
```

### `wp-buildkit-coordination-optimisation-gepa-dspy-coop-scheduler-`

```
2026-08-18T14:32:59Z allocate by ariellas -> assignee=None
```

### `wp-coordination-feature-stream-durable-superset-fix`

```
2026-08-18T14:32:59Z allocate by ariellas -> assignee=None
2026-08-19T09:48:35Z transition by ariellas backlog -> ready
2026-08-19T09:48:44Z allocate by ariellas -> assignee=None
2026-08-19T11:01:26Z claim by olamnit
2026-08-19T11:02:07Z note by olamnit
2026-08-22T16:27:02Z transition by ariellas ready -> in-progress
2026-08-22T16:28:45Z note by ariellas
2026-08-22T18:04:30Z transition by ariellas in-progress -> ready
```

### `wp-crdtmsg-post-mvp-completion-cose-sign1-wrapper-1-14-gated-gl`

```
2026-08-18T14:33:00Z allocate by ariellas -> assignee=None
```

### `wp-distributed-unification-quiescence-protocol-two-runtime-spec`

```
2026-08-18T14:33:00Z allocate by ariellas -> assignee=None
```

### `wp-durable-listener-service-box`

```
2026-08-18T14:33:00Z allocate by ariellas -> assignee=None
```

### `wp-front-end-goal-term-acceptance-completeness-parser-repl-goal`

```
2026-08-18T14:33:01Z allocate by ariellas -> assignee=None
```

### `wp-full-scope-gleam-glp-implementation`

```
2026-08-18T14:33:02Z allocate by ariellas -> assignee=None
```

### `wp-glptutorial-corpus-golden-reconciliation-stale-goldens-drift`

```
2026-08-18T14:33:02Z allocate by ariellas -> assignee=None
2026-08-18T14:54:31Z transition by ariellas backlog -> ready
2026-08-19T09:47:56Z allocate by ariellas -> assignee=None
2026-08-20T04:26:22Z note by ariellas
2026-08-22T18:05:35Z transition by ariellas ready -> in-progress
```

### `wp-guarded-term-traversal-utilities-cycle-tolerant-compiler-wal`

```
2026-08-18T14:33:02Z allocate by ariellas -> assignee=None
```

### `wp-madglp-writer-reader-address-discipline-closure-n-n-1-audit-`

```
2026-08-18T14:33:03Z allocate by ariellas -> assignee=None
```

### `wp-multi-host-state-discipline-reversible-states-untracked-deri`

```
2026-08-18T14:33:03Z allocate by ariellas -> assignee=None
```

### `wp-occurs-checked-substitution-pipeline-compiler-bind-time-occu`

```
2026-08-18T14:33:03Z allocate by ariellas -> assignee=None
2026-08-18T14:55:00Z transition by ariellas backlog -> ready
2026-08-19T09:48:03Z allocate by ariellas -> assignee=None
```

### `wp-per-host-toolchain-and-environment-contract-declared-machine`

```
2026-08-18T14:33:04Z allocate by ariellas -> assignee=None
```

### `wp-product-defect-burn-down-with-regression-proof-no-defect-clo`

```
2026-08-18T14:33:04Z allocate by ariellas -> assignee=None
```

### `wp-qr-link-provisioning`

```
2026-08-18T14:33:05Z allocate by ariellas -> assignee=None
```

### `wp-sc-002-il-parity-bridge-antlr-parse-tree-engine-ast-lowering`

```
2026-08-18T14:33:05Z allocate by ariellas -> assignee=None
```

### `wp-seam-specification-normative-contracts-at-every-trust-lifecy`

```
2026-08-18T14:33:05Z allocate by ariellas -> assignee=None
```

### `wp-single-source-of-truth-one-authority-per-subject-provenance-`

```
2026-08-18T14:33:06Z allocate by ariellas -> assignee=None
```

### `wp-type-checker-body-atom-moding-accept-head-flipped-readers-un`

```
2026-08-18T14:33:06Z allocate by ariellas -> assignee=None
```

### `wp-verification-receipts-and-loud-failure-no-check-may-pass-wit`

```
2026-08-18T14:33:06Z allocate by ariellas -> assignee=None
2026-08-18T14:54:01Z transition by ariellas backlog -> ready
2026-08-18T15:11:53Z claim by gavriella
2026-08-22T12:55:59Z allocate by gavriella -> assignee=None
2026-08-22T12:56:33Z allocate by gavriella -> assignee=None
2026-08-22T19:56:10Z transition by gavriella ready -> in-progress
```

### `wp-wave6-consolidation`

```
2026-08-18T14:33:06Z allocate by ariellas -> assignee=None
```

### `wp-ynet-consolidation`

```
2026-08-18T14:33:07Z allocate by ariellas -> assignee=None
```

### `wp-ynet-human-memorable-decentralized-naming-resolver`

```
2026-08-18T14:33:07Z allocate by ariellas -> assignee=None
```

### `wp-ynet-mobile-background-battery-budget-scheduling-policy`

```
2026-08-18T14:33:07Z allocate by ariellas -> assignee=None
```

## Allocate ops in full (the assignment record)

```
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 144000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000006", "op_type": "allocate", "payload": {"effort_band": "M", "effort_matched_on": "medium", "effort_source": "derived-from-roadmap-freetext", "effort_text": "medium (provisioning + verification)", "epic": "distributed-glp-connectivity", "proposed_actor": "unassigned", "roadmap_slot": "041-cross-runtime-and-two-host-acceptance-completion-t055-parity-sc-009-e2e", "roadmap_state": "promoted"}, "seq": 6, "timestamp": "2026-08-18T14:32:58Z", "to_state": null, "workstation_id": null, "wp_id": "wp-041-cross-runtime-and-two-host-acceptance-completion-t055-pa"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 288000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000007", "op_type": "allocate", "payload": {"effort_band": "L", "effort_matched_on": "large", "effort_source": "derived-from-roadmap-freetext", "effort_text": "medium-large - touches deploy/install.py, upgrade, and the ship/release reinstall path; Windows junction handling is the fiddly part", "epic": null, "proposed_actor": "unassigned", "roadmap_slot": "atomic-toolchain-installs-venv-swap-post-install-smoke", "roadmap_state": "released"}, "seq": 7, "timestamp": "2026-08-18T14:32:58Z", "to_state": null, "workstation_id": null, "wp_id": "wp-atomic-toolchain-installs-venv-swap-post-install-smoke"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 144000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000008", "op_type": "allocate", "payload": {"effort_band": "M", "effort_matched_on": "medium", "effort_source": "derived-from-roadmap-freetext", "effort_text": "medium", "epic": null, "proposed_actor": "unassigned", "roadmap_slot": "buildkit-coordination-optimisation-gepa-dspy-coop-scheduler-marathon-buildkit-tooling", "roadmap_state": "promoted"}, "seq": 8, "timestamp": "2026-08-18T14:32:59Z", "to_state": null, "workstation_id": null, "wp_id": "wp-buildkit-coordination-optimisation-gepa-dspy-coop-scheduler-"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 288000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000009", "op_type": "allocate", "payload": {"effort_band": "L", "effort_matched_on": "large", "effort_source": "derived-from-roadmap-freetext", "effort_text": "large: spans buildkit scheduler+roadmap+colab subsystems + glpnet consumer; deploy to all hosts", "epic": "epic-issue-backlog-root-cause-closure-sweep-2026-08", "proposed_actor": "unassigned", "roadmap_slot": "coordination-feature-stream-durable-superset-fix", "roadmap_state": "promoted"}, "seq": 9, "timestamp": "2026-08-18T14:32:59Z", "to_state": null, "workstation_id": null, "wp_id": "wp-coordination-feature-stream-durable-superset-fix"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 144000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000010", "op_type": "allocate", "payload": {"effort_band": "M", "effort_matched_on": "medium", "effort_source": "derived-from-roadmap-freetext", "effort_text": "small (wrapper) to medium (guard)", "epic": "epic-issue-backlog-root-cause-closure-sweep-2026-08", "proposed_actor": "unassigned", "roadmap_slot": "crdtmsg-post-mvp-completion-cose-sign1-wrapper-1-14-gated-glp-policy-guard", "roadmap_state": "promoted"}, "seq": 10, "timestamp": "2026-08-18T14:33:00Z", "to_state": null, "workstation_id": null, "wp_id": "wp-crdtmsg-post-mvp-completion-cose-sign1-wrapper-1-14-gated-gl"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 288000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000011", "op_type": "allocate", "payload": {"effort_band": "L", "effort_matched_on": "large", "effort_source": "derived-from-roadmap-freetext", "effort_text": "large", "epic": "distributed-glp-connectivity", "proposed_actor": "unassigned", "roadmap_slot": "distributed-unification-quiescence-protocol-two-runtime-spec-first", "roadmap_state": "promoted"}, "seq": 11, "timestamp": "2026-08-18T14:33:00Z", "to_state": null, "workstation_id": null, "wp_id": "wp-distributed-unification-quiescence-protocol-two-runtime-spec"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 144000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000012", "op_type": "allocate", "payload": {"effort_band": "M", "effort_matched_on": "medium", "effort_source": "derived-from-roadmap-freetext", "effort_text": "medium", "epic": null, "proposed_actor": "unassigned", "roadmap_slot": "durable-listener-service-box", "roadmap_state": "released"}, "seq": 12, "timestamp": "2026-08-18T14:33:00Z", "to_state": null, "workstation_id": null, "wp_id": "wp-durable-listener-service-box"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 144000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000013", "op_type": "allocate", "payload": {"effort_band": "M", "effort_matched_on": "medium", "effort_source": "derived-from-roadmap-freetext", "effort_text": "medium", "epic": "epic-issue-backlog-root-cause-closure-sweep-2026-08", "proposed_actor": "unassigned", "roadmap_slot": "front-end-goal-term-acceptance-completeness-parser-repl-goal-builders-cross-runtime", "roadmap_state": "promoted"}, "seq": 13, "timestamp": "2026-08-18T14:33:01Z", "to_state": null, "workstation_id": null, "wp_id": "wp-front-end-goal-term-acceptance-completeness-parser-repl-goal"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 288000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000014", "op_type": "allocate", "payload": {"effort_band": "L", "effort_matched_on": "marathon", "effort_source": "derived-from-roadmap-freetext", "effort_text": "marathon (multi-session; 66+ WPs across 5 waves; L items: link primitives, engine sessions, QUIC leaf)", "epic": "full-gleam", "proposed_actor": "unassigned", "roadmap_slot": "full-scope-gleam-glp-implementation", "roadmap_state": "specified"}, "seq": 14, "timestamp": "2026-08-18T14:33:02Z", "to_state": null, "workstation_id": null, "wp_id": "wp-full-scope-gleam-glp-implementation"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 28800.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000015", "op_type": "allocate", "payload": {"effort_band": "S", "effort_matched_on": "small", "effort_source": "derived-from-roadmap-freetext", "effort_text": "small", "epic": "epic-issue-backlog-root-cause-closure-sweep-2026-08", "proposed_actor": "unassigned", "roadmap_slot": "glptutorial-corpus-golden-reconciliation-stale-goldens-drift-guard-vendoring", "roadmap_state": "promoted"}, "seq": 15, "timestamp": "2026-08-18T14:33:02Z", "to_state": null, "workstation_id": null, "wp_id": "wp-glptutorial-corpus-golden-reconciliation-stale-goldens-drift"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 144000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000016", "op_type": "allocate", "payload": {"effort_band": "M", "effort_matched_on": "medium", "effort_source": "derived-from-roadmap-freetext", "effort_text": "medium: extract one guarded-traversal util (visited-set/fuel), route ~11 walkers, dedup PE/analyzer copies", "epic": "epic-glp-compiler-robustness-occurs-check-term-traversal-hardening", "proposed_actor": "unassigned", "roadmap_slot": "guarded-term-traversal-utilities-cycle-tolerant-compiler-walkers-pe-analyzer-dedup", "roadmap_state": "released"}, "seq": 16, "timestamp": "2026-08-18T14:33:02Z", "to_state": null, "workstation_id": null, "wp_id": "wp-guarded-term-traversal-utilities-cycle-tolerant-compiler-wal"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 144000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000017", "op_type": "allocate", "payload": {"effort_band": "M", "effort_matched_on": "small-medium", "effort_source": "derived-from-roadmap-freetext", "effort_text": "small-medium", "epic": "epic-issue-backlog-root-cause-closure-sweep-2026-08", "proposed_actor": "unassigned", "roadmap_slot": "madglp-writer-reader-address-discipline-closure-n-n-1-audit-residuals", "roadmap_state": "promoted"}, "seq": 17, "timestamp": "2026-08-18T14:33:03Z", "to_state": null, "workstation_id": null, "wp_id": "wp-madglp-writer-reader-address-discipline-closure-n-n-1-audit-"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 288000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000018", "op_type": "allocate", "payload": {"effort_band": "L", "effort_matched_on": "large", "effort_source": "derived-from-roadmap-freetext", "effort_text": "large", "epic": null, "proposed_actor": "unassigned", "roadmap_slot": "multi-host-state-discipline-reversible-states-untracked-derived-artifacts-unique-identities", "roadmap_state": "promoted"}, "seq": 18, "timestamp": "2026-08-18T14:33:03Z", "to_state": null, "workstation_id": null, "wp_id": "wp-multi-host-state-discipline-reversible-states-untracked-deri"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 144000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000019", "op_type": "allocate", "payload": {"effort_band": "M", "effort_matched_on": "small-medium", "effort_source": "derived-from-roadmap-freetext", "effort_text": "small-medium: one shared occurs-check helper at ~9 bind sites x 2 duplicate copies", "epic": "epic-glp-compiler-robustness-occurs-check-term-traversal-hardening", "proposed_actor": "unassigned", "roadmap_slot": "occurs-checked-substitution-pipeline-compiler-bind-time-occurs-check", "roadmap_state": "promoted"}, "seq": 19, "timestamp": "2026-08-18T14:33:03Z", "to_state": null, "workstation_id": null, "wp_id": "wp-occurs-checked-substitution-pipeline-compiler-bind-time-occu"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 144000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000020", "op_type": "allocate", "payload": {"effort_band": "M", "effort_matched_on": "medium", "effort_source": "derived-from-roadmap-freetext", "effort_text": "medium", "epic": null, "proposed_actor": "unassigned", "roadmap_slot": "per-host-toolchain-and-environment-contract-declared-machine-checked-loudly-refused", "roadmap_state": "promoted"}, "seq": 20, "timestamp": "2026-08-18T14:33:04Z", "to_state": null, "workstation_id": null, "wp_id": "wp-per-host-toolchain-and-environment-contract-declared-machine"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 288000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000021", "op_type": "allocate", "payload": {"effort_band": "L", "effort_matched_on": "large", "effort_source": "derived-from-roadmap-freetext", "effort_text": "large", "epic": null, "proposed_actor": "unassigned", "roadmap_slot": "product-defect-burn-down-with-regression-proof-no-defect-closed-on-a-fixer-s-own-green-run", "roadmap_state": "promoted"}, "seq": 21, "timestamp": "2026-08-18T14:33:04Z", "to_state": null, "workstation_id": null, "wp_id": "wp-product-defect-burn-down-with-regression-proof-no-defect-clo"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 144000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000022", "op_type": "allocate", "payload": {"effort_band": "M", "effort_matched_on": "medium", "effort_source": "derived-from-roadmap-freetext", "effort_text": "medium - QR encode + multi-QR chunking + PDF generation + hub display page on the producer side; decode/assemble in consuming clients", "epic": "distributed-glp-connectivity--01kwjqmg", "proposed_actor": "unassigned", "roadmap_slot": "qr-link-provisioning", "roadmap_state": "specified"}, "seq": 22, "timestamp": "2026-08-18T14:33:05Z", "to_state": null, "workstation_id": null, "wp_id": "wp-qr-link-provisioning"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 144000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000023", "op_type": "allocate", "payload": {"effort_band": "M", "effort_matched_on": "medium", "effort_source": "derived-from-roadmap-freetext", "effort_text": "~250-400 LOC mechanical mapping: ~22 visitor methods (one per grammar rule) ANTLR node -> engine AST node, plus pipeline-invocation glue; no new engine capability. Plus corpus expansion + adversarial fuzzing. Small-to-medium.", "epic": "epic-separation-of-repl-front-end-from-engine-execution-scheduler", "proposed_actor": "unassigned", "roadmap_slot": "sc-002-il-parity-bridge-antlr-parse-tree-engine-ast-lowering-adoption-decision", "roadmap_state": "released"}, "seq": 23, "timestamp": "2026-08-18T14:33:05Z", "to_state": null, "workstation_id": null, "wp_id": "wp-sc-002-il-parity-bridge-antlr-parse-tree-engine-ast-lowering"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 288000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000024", "op_type": "allocate", "payload": {"effort_band": "L", "effort_matched_on": "large", "effort_source": "derived-from-roadmap-freetext", "effort_text": "large", "epic": null, "proposed_actor": "unassigned", "roadmap_slot": "seam-specification-normative-contracts-at-every-trust-lifecycle-and-protocol-boundary", "roadmap_state": "promoted"}, "seq": 24, "timestamp": "2026-08-18T14:33:05Z", "to_state": null, "workstation_id": null, "wp_id": "wp-seam-specification-normative-contracts-at-every-trust-lifecy"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 144000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000025", "op_type": "allocate", "payload": {"effort_band": "M", "effort_matched_on": "medium", "effort_source": "derived-from-roadmap-freetext", "effort_text": "medium", "epic": null, "proposed_actor": "unassigned", "roadmap_slot": "single-source-of-truth-one-authority-per-subject-provenance-on-generated-artifacts", "roadmap_state": "promoted"}, "seq": 25, "timestamp": "2026-08-18T14:33:06Z", "to_state": null, "workstation_id": null, "wp_id": "wp-single-source-of-truth-one-authority-per-subject-provenance-"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 144000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000026", "op_type": "allocate", "payload": {"effort_band": "M", "effort_matched_on": "medium", "effort_source": "derived-from-roadmap-freetext", "effort_text": "medium", "epic": "epic-issue-backlog-root-cause-closure-sweep-2026-08", "proposed_actor": "unassigned", "roadmap_slot": "type-checker-body-atom-moding-accept-head-flipped-readers-unblock-2", "roadmap_state": "released"}, "seq": 26, "timestamp": "2026-08-18T14:33:06Z", "to_state": null, "workstation_id": null, "wp_id": "wp-type-checker-body-atom-moding-accept-head-flipped-readers-un"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 288000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000027", "op_type": "allocate", "payload": {"effort_band": "L", "effort_matched_on": "large", "effort_source": "derived-from-roadmap-freetext", "effort_text": "large", "epic": null, "proposed_actor": "unassigned", "roadmap_slot": "verification-receipts-and-loud-failure-no-check-may-pass-without-proving-it-ran", "roadmap_state": "promoted"}, "seq": 27, "timestamp": "2026-08-18T14:33:06Z", "to_state": null, "workstation_id": null, "wp_id": "wp-verification-receipts-and-loud-failure-no-check-may-pass-wit"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 288000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000028", "op_type": "allocate", "payload": {"effort_band": "L", "effort_matched_on": "large", "effort_source": "derived-from-roadmap-freetext", "effort_text": "large: 27 not-closed items across 5 story groups (S1-S5) and 3 gates (G1/G2/G3), spanning three hosts and consuming external peer receipts", "epic": "epic-roadmap-sweep-2026-07-consolidated-waves", "proposed_actor": "unassigned", "roadmap_slot": "wave6-consolidation", "roadmap_state": "specified"}, "seq": 28, "timestamp": "2026-08-18T14:33:06Z", "to_state": null, "workstation_id": null, "wp_id": "wp-wave6-consolidation"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 288000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000029", "op_type": "allocate", "payload": {"effort_band": "L", "effort_matched_on": "large", "effort_source": "derived-from-roadmap-freetext", "effort_text": "large", "epic": null, "proposed_actor": "unassigned", "roadmap_slot": "ynet-consolidation", "roadmap_state": "specified"}, "seq": 29, "timestamp": "2026-08-18T14:33:07Z", "to_state": null, "workstation_id": null, "wp_id": "wp-ynet-consolidation"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 288000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000030", "op_type": "allocate", "payload": {"effort_band": "L", "effort_matched_on": "large", "effort_source": "derived-from-roadmap-freetext", "effort_text": "large (BUILD-NEW, no drop-in corpus reference)", "epic": "epic-ynet-overlay-deferred-build-new-gaps", "proposed_actor": "unassigned", "roadmap_slot": "ynet-human-memorable-decentralized-naming-resolver", "roadmap_state": "promoted"}, "seq": 30, "timestamp": "2026-08-18T14:33:07Z", "to_state": null, "workstation_id": null, "wp_id": "wp-ynet-human-memorable-decentralized-naming-resolver"}
{"actor": "ariellas", "day": "2026-08-18", "e_t_s": 144000.0, "engineer_id": "unassigned", "from_state": null, "op_id": "ariellas:000031", "op_type": "allocate", "payload": {"effort_band": "M", "effort_matched_on": "medium", "effort_source": "derived-from-roadmap-freetext", "effort_text": "medium \u2014 BUILD-NEW; needs a real mobile-P2P energy reference fetched first", "epic": "epic-ynet-overlay-deferred-build-new-gaps", "proposed_actor": "unassigned", "roadmap_slot": "ynet-mobile-background-battery-budget-scheduling-policy", "roadmap_state": "promoted"}, "seq": 31, "timestamp": "2026-08-18T14:33:07Z", "to_state": null, "workstation_id": null, "wp_id": "wp-ynet-mobile-background-battery-budget-scheduling-policy"}
{"actor": "ariellas", "day": "2026-08-19", "e_t_s": 14400.0, "engineer_id": "ariellas", "from_state": null, "op_id": "ariellas:000035", "op_type": "allocate", "payload": {"proposed_actor": "ariellas"}, "seq": 35, "timestamp": "2026-08-19T09:47:56Z", "to_state": null, "workstation_id": null, "wp_id": "wp-glptutorial-corpus-golden-reconciliation-stale-goldens-drift"}
{"actor": "ariellas", "day": "2026-08-19", "e_t_s": 57600.0, "engineer_id": "ariellas", "from_state": null, "op_id": "ariellas:000036", "op_type": "allocate", "payload": {"proposed_actor": "ariellas"}, "seq": 36, "timestamp": "2026-08-19T09:48:03Z", "to_state": null, "workstation_id": null, "wp_id": "wp-occurs-checked-substitution-pipeline-compiler-bind-time-occu"}
{"actor": "ariellas", "day": "2026-08-19", "e_t_s": 144000.0, "engineer_id": "olamnit", "from_state": null, "op_id": "ariellas:000038", "op_type": "allocate", "payload": {"proposed_actor": "olamnit"}, "seq": 38, "timestamp": "2026-08-19T09:48:44Z", "to_state": null, "workstation_id": null, "wp_id": "wp-coordination-feature-stream-durable-superset-fix"}
{"actor": "gavriella", "day": "2026-08-22", "e_t_s": 43200.0, "engineer_id": "gavriella", "from_state": null, "op_id": "gavriella:000010", "op_type": "allocate", "payload": {"proposed_actor": "gavriella", "repo": "glpnet"}, "seq": 10, "timestamp": "2026-08-22T12:55:59Z", "to_state": "ready", "workstation_id": null, "wp_id": "wp-verification-receipts-and-loud-failure-no-check-may-pass-wit"}
{"actor": "gavriella", "day": "2026-08-22", "e_t_s": 43200.0, "engineer_id": "gavriella", "from_state": null, "op_id": "gavriella:000011", "op_type": "allocate", "payload": {"feature_binding": {"actor": "gavriella", "feature_id": "078-verification-receipts", "repo": "glpnet", "v": 1, "wp_id": "wp-verification-receipts-and-loud-failure-no-check-may-pass-wit"}, "proposed_actor": "gavriella", "repo": "glpnet"}, "seq": 11, "timestamp": "2026-08-22T12:56:33Z", "to_state": "ready", "workstation_id": null, "wp_id": "wp-verification-receipts-and-loud-failure-no-check-may-pass-wit"}
{"actor": "gavriella", "day": "2026-08-22", "e_t_s": 28800.0, "engineer_id": "gavriella", "from_state": null, "op_id": "gavriella:000012", "op_type": "allocate", "payload": {"proposed_actor": "gavriella", "repo": "glpnet"}, "seq": 12, "timestamp": "2026-08-22T19:55:23Z", "to_state": "ready", "workstation_id": null, "wp_id": "wave-5-consolidated-captured-triad"}
{"actor": "gavriella", "day": "2026-08-22", "e_t_s": 28800.0, "engineer_id": "gavriella", "from_state": null, "op_id": "gavriella:000013", "op_type": "allocate", "payload": {"proposed_actor": "gavriella", "repo": "glpnet"}, "seq": 13, "timestamp": "2026-08-22T19:55:27Z", "to_state": "ready", "workstation_id": null, "wp_id": "wave-2-consolidated-repl-engine-split-spine"}
```

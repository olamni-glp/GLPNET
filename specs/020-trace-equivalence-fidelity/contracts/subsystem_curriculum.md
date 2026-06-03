# Contract — Subsystem classification + curriculum order (FR-007, FR-009)

Source of truth: `.codeconv/equiv-manifest/subsystems.yml` (checked in, versioned). `manifest.py` validates against `dart_depgraph` (015, read-only).

## Subsystems + tiers
| Subsystem | Tier | Scope (path prefixes, validated vs depgraph) | Comparison |
|---|---|---|---|
| `heap` | strict | `lib/runtime/heap_fcp*` | total-order trace equality + bytecode-diff |
| `bytecode` | strict | `lib/bytecode/` (runner/dispatch) | total-order + bytecode-diff |
| `compiler` | strict | `lib/compiler/` incl. type-checker, partial-evaluator, SRSW | total-order + bytecode-diff (emits the spine) |
| `runtime-core` | strict | `lib/runtime/` single-computation | total-order trace equality |
| `multiagent` | dynamic | `lib/multiagent/` (isolate_manager, channels, rpc-routing) | causal/partial-order + outcome-equivalence |

## Curriculum order (decision 8, FR-007)
Strict subsystems first, in dependency order from `dart_depgraph` (`heap` → `bytecode` → `compiler` → `runtime-core`), then `multiagent` LAST — attacked from strength with a fully-matured, carried-forward prompt. Within a subsystem, files in topological/SCC order (015). A file converts only when build + the tier's equivalence gate pass, else escalate (FR-008).

## Bootstrapping window (spec edge case)
Before a runnable end-to-end C# runtime exists, strict-tier files fall back to **build + module back-test + bytecode-emission diff** (no full trace). The metric MUST NOT award 1.0 in this window (`trace_captured` is false ⇒ capped at 0.25; once a full trace is captured, the high band opens). The bytecode-emission diff (FR-004, SC-007) is the spine signal available here.

## Strict-tier nondeterminism reclassification (spec edge case)
If a strict-tier source reveals scheduling nondeterminism, it is **reclassified to dynamic** (manifest updated, reviewed) and verified under partial-order — never forced into exact equality. This is a manifest change, recorded with rationale.

## Dynamic-tier verification-mode decision (FR-009, US4 acceptance 3) — DEFERRED
The choice between (a) **pin a canonical verification-schedule** in both runtimes and (b) **accept any causally-valid schedule** is made WHEN the multiagent tier is reached, using observed divergence data, and recorded HERE (this contract) with rationale BEFORE bulk dynamic-tier generation. Until then:
- the dynamic tier is NOT bulk-generated;
- a placeholder section below is filled in at that point.

### Dynamic-tier verification mode — DECISION (to be filled when multiagent tier reached)
> _Not yet decided. Will record: chosen mode (pinned-schedule | accept-any-causal), the divergence data that motivated it, and the implication for `relation.py` DYNAMIC handling._

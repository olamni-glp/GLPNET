# Ch 12 Sources — Constitutional Consensus

**PDF**: `GLP_ART.pdf`, book pp 115–127 (PDF pp 127–139).

## Sections (verified)
- 12.1 Introduction — p 115 (Digital Social Contracts, Technical Requirements, Attestation and Simplification — prose + comparison table)
- 12.2 The Consensus Problem — p 117 (Agreement in Asynchronous Networks, Eventual Synchrony, Constitution Definition 12.1, Why Majority Suffices — prose)
- 12.3 The Blocklace for Consensus — p 118 (Mapping to Interlaced Streams, Block Structure `block(Round, Payload, Pointers)`, Constitutional Genesis Block `genesis(P, Sigma, Delta)`, Depth and Rounds — type sketches)
- 12.4 Wave Structure — p 119 (Three Rounds: Candidates / Endorsements / Ratifications; Finality; Quiescent Waves — prose)
- 12.5 Dual-Mode Operation — p 120 (Low/High-Throughput Modes, Mode Transitions, Timeout Handling — prose + `wait_for_leader/2`)
- 12.6 The Ordering Function — p 121 (Definition of τ, Consistency, Incremental Computation `compute_tau/3`)
- 12.7 Implementation in GLP — p 121 (Agent State, Agent Process, Handling Incoming Blocks, Issuing Blocks, Leader Selection, Finality Detection, Computing Finalized Stream)
- 12.8 A Complete Example — p 124 (Setup, Low-Throughput Scenario, High-Throughput Scenario, Execution Trace)
- 12.9 Correctness Properties — p 126 (Safety, Liveness, Totality, What Attestation Provides — prose)
- 12.10 Exercises — p 127 (OUT OF SCOPE)

## Code-block index — §12.5 Dual-Mode Operation
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 12.5.1 | `wait_for_leader/2` | p 120–121 | 2 clauses (timeout via `wait(Timeout?)` / arrival via `known(Block?)`) | timeout primitive use |

## Code-block index — §12.6 The Ordering Function
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 12.6.1 | `compute_tau/3` | p 121 | 1 clause: `find_new_finalized` + `order_candidates` + `append` | incremental τ computation |

## Code-block index — §12.3 / §12.7 Block Type Definitions
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 12.3.1 | `block(Round, Payload, Pointers)` block-type sketch | p 118 | type signature only | block structure |
| 12.3.2 | `genesis(P, Sigma, Delta)` constitutional genesis block | p 118 | type signature only | initial constitution |

## Code-block index — §12.7 Implementation in GLP
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 12.7.1 | `state(Blocklace, Mode, CurrentRound, Finalized, Pending)` agent state | p 122 | type/structure sketch | agent state record |
| 12.7.2 | `agent/4` event loop | p 122 | 2 clauses (base + recursive `handle_event` + `update_finalized` + recurse) | persistent consensus agent |
| 12.7.3 | `handle_event(block(R, Payload, Ptrs), …)` | p 122 | 1 clause: `add_block` + `check_mode` + `advance_round` + `maybe_issue` + reconstruct state | block-arrival handler |
| 12.7.4 | `maybe_issue/7` — low-throughput, issue (round mod 3 == 1) | p 122–123 | 1 clause; emits `block(Round?, Tx?, Tips?)` | candidate proposal |
| 12.7.5 | `maybe_issue/7` — low-throughput, endorse (round mod 3 == 2) | p 123 | 1 clause; emits `block(Round?, empty, Tips?)` | endorsement |
| 12.7.6 | `maybe_issue/7` — low-throughput, ratify (round mod 3 == 0) | p 123 | 1 clause; emits `block(Round?, empty, Tips?)` | ratification |
| 12.7.7 | `maybe_issue/7` — high-throughput delegate to `issue_high_throughput/6` | p 123 | 1 clause | leader-mode dispatch |
| 12.7.8 | `maybe_issue/7` — no-action default | p 123 | 1 clause | catch-all |
| 12.7.9 | `leader/3` (round-robin formal-leader selection) | p 123 | 1 clause: `length`, `WaveNum := Round//3`, `Index := WaveNum mod N`, `nth` | leader rotation |
| 12.7.10 | `is_finalized/3` | p 123 | 1 clause: endorsements_for + is_majority + ratifications_for + is_majority | finality detection |
| 12.7.11 | `is_majority/2` | p 124 | 1 clause: `M > N // 2` guard | majority threshold |
| 12.7.12 | `update_finalized/3` | p 124 | 1 clause: `find_newly_finalized` + `append` | extend finalized stream |
| 12.7.13 | `find_newly_finalized/3` | p 124 | 1 clause: `all_finalized` + `subtract` | new-finalized diff |

## Code-block index — §12.8 A Complete Example
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 12.8.1 | `genesis([alice, bob, carol], 0.5, 1000)` setup | p 124 | invocation form | 3-participant constitution |
| 12.8.2 | Block-trace examples per round (`block(1, tx_a, [genesis])`, `block(2, empty, [alice_block_1])`, `block(3, empty, [bob_endorse, carol_endorse, alice_endorse])`) | p 124–125 | trace data, not clauses | low-throughput wave |
| 12.8.3 | Block-trace examples for high-throughput conflict (`block(4, tx_a2, ratify_blocks)` vs `block(4, tx_b, ratify_blocks)`) | p 125 | trace data, not clauses | high-throughput wave |

## Tables / Comparisons
- Smart Contract vs Digital Social Contract — p 116.
- Consensus Concept ↔ GLP Implementation — p 118.

## Tutorial mode
multi-actor-distillation. Single use case for the chapter (per charter §1: one project per use case for chs 7–13).

## Use case (suggested per charter)
- **`ch12/constitutional-consensus/`** — full §12.7 agent + handlers (`agent/4`, `handle_event/4`, `maybe_issue/7` × 5, `leader/3`, `is_finalized/3`, `is_majority/2`, `update_finalized/3`, `find_newly_finalized/3`, `compute_tau/3`, `wait_for_leader/2`) + §12.8 3-participant play (`genesis`, low-throughput → high-throughput sequence). One self-contained Flutter project.
- **`ch12/useful-techniques.glp`** — `is_majority`, `wait_for_leader` if shared.

## Companion repo references
- `programs/typed_book/constitutional_consensus/` — typed consensus Programs.
- `programs/Bonds/` — bonds layer may use consensus in some plays (cross-reference at extraction time).
- `glp_multiagent/lib/main_cssg_mad_modules.dart` — Flutter template.
- `../charter.md`

<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# IL-parity results — SC-002 IL-parity bridge (feature 069)

**Toolchain**: dotnet 10.0.301; Antlr4.Runtime.Standard 4.13.1; ANTLR 4.13.2 (gen, -visitor); Java 17 (jdk-17.0.19+10)

## Summary & bounded conditions

Across every sweep below — 7-file representative corpus, the expanded corpus (`programs/tests/typed`
71, `programs/lib` 8, `programs/typed_book` 223), and the 10 000-case bounded fuzz — **un-caused
divergences (genuine grammar/lowering defects, FR-008): 0.** Every logged DIVERGE has an attributed
cause and falls into one bounded class:

- **BC-1 — hand-parser post-parse semantics (decl↔clause well-formedness).** The production hand-parser
  `parser.cs::ParseModule` enforces two SEMANTIC rules the pure-syntactic ANTLR grammar does not:
  (a) a `procedure` declaration must be followed by ≥1 clause unless the name resolves to a runtime-native
  guard or a prelude/imported symbol via the REPL's load context, and (b) a declaration's clauses must
  immediately follow it (no interleaving). Files depending on prelude/imported/native symbols, or that
  interleave decls and clauses, PARSE in ANTLR but are rejected by the ISOLATED hand-parser (which the
  harness runs without the REPL prelude/project-link context). These surface as one-sided rejects
  (`ANTLR=accept, hand=reject: … has no clauses` / `… must be immediately followed by its clauses`) —
  a comparator asymmetry (side A = syntactic parse; side B = syntactic parse + these semantic checks),
  NOT a grammar-fidelity defect. Adopting the ANTLR front-end would require porting these post-parse
  checks (they live in `parser.cs`, not the grammar). Affected: 1/71 `tests/typed` (`satisfiable`, a 049
  native guard), 48/223 `typed_book` (prelude/imported `merge`/`receive`/`new_channel`/`reduce`/… and one
  interleaved-clause file). Every genuinely SELF-CONTAINED file matches (`programs/lib` 8/8).
- **BC-2 — F-069-1 engine occurs-check (fuzz scope, DEC F3).** Cyclic `=` defined-guards
  (`A? = B? * A?`) overflow the production `DefinedGuardEvaluator` (no occurs-check). Such a guard never
  yields IL, so the fuzzer generates only non-cyclic `=`; the defect is filed as its own engine bug
  (see `FINDINGS.md`). Not a parity divergence — both front-ends crash identically.
- **mod-functor (RESOLVED, T016):** `mod(...)` call form now lexes as ATOM in both front-ends
  (Gabi + Udi approved lexer predicate) → byte-identical IL (`mod_functor_call.glp`, MATCH).

"both reject" rows (parse or downstream) are agreements, not divergences: both front-ends reject
identically, so parity holds (no IL is compared for them).

## Representative corpus (7 files) — SC-001

| # | input | verdict | first-diff | cause |
|---|-------|---------|-----------|-------|
| 1 | append_dl.glp | MATCH |  |  |
| 2 | arith_comparison.glp | MATCH |  |  |
| 3 | arith_diseq.glp | MATCH |  |  |
| 4 | arith_guard_ground.glp | MATCH |  |  |
| 5 | abandon_stream.glp | MATCH |  |  |
| 6 | typed_social_agent.glp | MATCH |  |  |
| 7 | abandon_reader_bad.glp | MATCH |  |  |

**Totals**: 7/7 MATCH. Un-caused divergences (defects — FR-008): 0.

## Bounded fuzz — SC-003

**Seed**: 2654435769 · **Budget**: 10000 (deterministic: source = f(index, seed) — F3).

Targets (contract F1): variable-versus-comparison dispatch (IL parity) + deep type-alternative nesting (parse-acceptance parity). Operators are always space-separated, which excludes the documented glued-minus lexer corner; only infix `mod` is generated (the `mod(...)` call form is the §1.14-gated T016 case).

**Verdict**: PASS — full budget completed with **0 un-caused IL divergences** (SC-003 / F2).

- valid IL-parity MATCH: 5623
- both-reject (invalid generated program — not a divergence): 4377

## Expanded corpus (programs/tests/typed) — SC-002

| # | input | verdict | first-diff | cause |
|---|-------|---------|-----------|-------|
| 1 | abandon_reader_bad.glp | MATCH |  |  |
| 2 | abandon_stream.glp | MATCH |  |  |
| 3 | append_dl.glp | MATCH |  |  |
| 4 | arith_comparison.glp | MATCH |  |  |
| 5 | arith_diseq.glp | MATCH |  |  |
| 6 | arith_guard_ground.glp | MATCH |  |  |
| 7 | arithmetic_fixed.glp | MATCH |  |  |
| 8 | assign_reader_test.glp | MATCH |  |  |
| 9 | atom_guard.glp | MATCH |  |  |
| 10 | bb_diff.glp | MATCH |  |  |
| 11 | circular_test.glp | MATCH |  |  |
| 12 | compound_suspend.glp | MATCH |  |  |
| 13 | constant_ground_test.glp | MATCH |  |  |
| 14 | decline_eq_bad.glp | MATCH |  | both reject at parse |
| 15 | decline_neq_bad.glp | MATCH |  | both reject at parse |
| 16 | decline_reader_bad.glp | MATCH |  |  |
| 17 | decline_struct_diseq_bad.glp | MATCH |  | both reject at parse |
| 18 | depth_test.glp | MATCH |  |  |
| 19 | diff_list.glp | MATCH |  |  |
| 20 | fork_1_circular_deref.glp | MATCH |  |  |
| 21 | gap_g1_ground_relax.glp | MATCH |  |  |
| 22 | gap_g2_standardize_apart.glp | MATCH |  |  |
| 23 | gap_g3_fairness.glp | MATCH |  |  |
| 24 | gap_g8_guard_three_valued.glp | MATCH |  |  |
| 25 | gethead_test.glp | MATCH |  |  |
| 26 | guard_reader.glp | MATCH |  |  |
| 27 | hello.glp | MATCH |  |  |
| 28 | merge_standalone.glp | MATCH |  |  |
| 29 | mod_functor_call.glp | MATCH |  |  |
| 30 | module_guard.glp | MATCH |  |  |
| 31 | multi_client_control.glp | MATCH |  |  |
| 32 | multiply.glp | MATCH |  |  |
| 33 | multiply_direct.glp | MATCH |  |  |
| 34 | no_guard.glp | MATCH |  |  |
| 35 | nonground_list.glp | MATCH |  |  |
| 36 | order_guards.glp | MATCH |  |  |
| 37 | otherwise_guard.glp | MATCH |  |  |
| 38 | p.glp | MATCH |  |  |
| 39 | paa.glp | MATCH |  |  |
| 40 | param_arity_mismatch.glp | MATCH |  |  |
| 41 | param_bare_typevar.glp | MATCH |  |  |
| 42 | param_channel.glp | MATCH |  |  |
| 43 | param_procedure_inference.glp | MATCH |  |  |
| 44 | param_stream_integer.glp | MATCH |  |  |
| 45 | policy_guard_formb.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "satisfiable" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 46 | policy_guard_vectors.glp | MATCH |  |  |
| 47 | policy_guard_worked.glp | MATCH |  |  |
| 48 | quoted_body_test.glp | MATCH |  |  |
| 49 | quoted_functor_test.glp | MATCH |  |  |
| 50 | reader_output.glp | MATCH |  |  |
| 51 | run1.glp | MATCH |  |  |
| 52 | send_reader_single.glp | MATCH |  |  |
| 53 | struct_demo.glp | MATCH |  |  |
| 54 | test_arithmetic_kernels.glp | MATCH |  |  |
| 55 | test_befriend_intro_bug.glp | MATCH |  |  |
| 56 | test_bob.glp | MATCH |  |  |
| 57 | test_channel_guards.glp | MATCH |  |  |
| 58 | test_defined_guards.glp | MATCH |  |  |
| 59 | test_defined_guards_all.glp | MATCH |  |  |
| 60 | test_ground_equal.glp | MATCH |  |  |
| 61 | test_guard_negation.glp | MATCH |  |  |
| 62 | test_guard_suspend.glp | MATCH |  |  |
| 63 | test_guards_comprehensive.glp | MATCH |  |  |
| 64 | test_module_boundary.glp | MATCH |  |  |
| 65 | test_nested_suspend.glp | MATCH |  |  |
| 66 | test_time.glp | MATCH |  |  |
| 67 | two_struct_list.glp | MATCH |  |  |
| 68 | typed_social_agent.glp | MATCH |  |  |
| 69 | typed_ui_actors.glp | MATCH |  |  |
| 70 | typed_ui_mediator.glp | MATCH |  |  |
| 71 | with_guard.glp | MATCH |  |  |

**Totals**: 70/71 MATCH. Un-caused divergences (defects — FR-008): 0.

## Expanded corpus (programs/lib) — SC-002

| # | input | verdict | first-diff | cause |
|---|-------|---------|-----------|-------|
| 1 | broadcast.glp | MATCH |  |  |
| 2 | channel_ops.glp | MATCH |  |  |
| 3 | guard_utils.glp | MATCH |  |  |
| 4 | inject.glp | MATCH |  |  |
| 5 | lookup.glp | MATCH |  |  |
| 6 | relay.glp | MATCH |  |  |
| 7 | tag_stream.glp | MATCH |  |  |
| 8 | time_utils.glp | MATCH |  |  |

**Totals**: 8/8 MATCH. Un-caused divergences (defects — FR-008): 0.

## Expanded corpus (programs/typed_book) — SC-002

| # | input | verdict | first-diff | cause |
|---|-------|---------|-----------|-------|
| 1 | abortable_meta.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "reduce" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 2 | ackermann.glp | MATCH |  |  |
| 3 | actors.glp | MATCH |  |  |
| 4 | agent.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "merge" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 5 | agent.glp | MATCH |  |  |
| 6 | agent_full.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "receive" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 7 | agent_simple.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "merge" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 8 | alice.glp | MATCH |  |  |
| 9 | alice.glp | MATCH |  |  |
| 10 | append.glp | MATCH |  |  |
| 11 | attestation_guards.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "handle_verified" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 12 | biased_merge.glp | MATCH |  |  |
| 13 | bidirectional_exchange_boot.glp | MATCH |  |  |
| 14 | binary_tree.glp | MATCH |  |  |
| 15 | bob.glp | MATCH |  |  |
| 16 | bob.glp | MATCH |  |  |
| 17 | boot.glp | MATCH |  |  |
| 18 | bounded_buffer.glp | MATCH |  | both reject downstream |
| 19 | bounded_buffer_original.glp | MATCH |  | both reject downstream |
| 20 | broadcast.glp | MATCH |  |  |
| 21 | bubble_sort.glp | MATCH |  |  |
| 22 | certainty_meta.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "clause" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 23 | channel.glp | MATCH |  |  |
| 24 | channels.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "merge" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 25 | charlie.glp | MATCH |  |  |
| 26 | circuits.glp | MATCH |  |  |
| 27 | cold_call.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "lookup_send" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 28 | cold_call_test_boot.glp | MATCH |  |  |
| 29 | consensus.glp | MATCH |  | both reject downstream |
| 30 | control_meta.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "reduce" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 31 | coop_stream_boot.glp | MATCH |  |  |
| 32 | cooperative.glp | MATCH |  |  |
| 33 | cooperative_producers.glp | MATCH |  | both reject downstream |
| 34 | copy.glp | MATCH |  |  |
| 35 | counter.glp | MATCH |  |  |
| 36 | delete.glp | MATCH |  |  |
| 37 | diana.glp | MATCH |  |  |
| 38 | direct_messaging.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "new_channel" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 39 | distribute.glp | MATCH |  |  |
| 40 | distribute_binary.glp | MATCH |  |  |
| 41 | distribute_boot.glp | MATCH |  |  |
| 42 | distribute_ground.glp | MATCH |  |  |
| 43 | distribute_indexed.glp | MATCH |  |  |
| 44 | distribute_nonground.glp | MATCH |  |  |
| 45 | dl_append.glp | MATCH |  |  |
| 46 | dm_simple.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "new_channel" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 47 | dynamic_merger.glp | MATCH |  | both reject downstream |
| 48 | eve.glp | MATCH |  |  |
| 49 | exp.glp | MATCH |  |  |
| 50 | factorial.glp | MATCH |  |  |
| 51 | failsafe_meta.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "reduce" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 52 | fair_merge.glp | MATCH |  |  |
| 53 | feed.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "create_post" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 54 | feed_server.glp | MATCH |  |  |
| 55 | fibonacci.glp | MATCH |  |  |
| 56 | filter_even.glp | MATCH |  |  |
| 57 | flatten.glp | MATCH |  |  |
| 58 | flatten_original.glp | MATCH |  |  |
| 59 | follower_mgmt.glp | MATCH |  |  |
| 60 | frank.glp | MATCH |  |  |
| 61 | friend_intro_test_boot.glp | MATCH |  |  |
| 62 | friend_introduction.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "lookup_send" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 63 | gates.glp | MATCH |  |  |
| 64 | gates_simple.glp | MATCH |  |  |
| 65 | gc.glp | MATCH |  | both reject downstream |
| 66 | gcd_integer.glp | MATCH |  |  |
| 67 | group_formation.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "create_group_streams" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 68 | group_messaging.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "lookup" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 69 | hanoi.glp | MATCH |  |  |
| 70 | hollow_integers.glp | MATCH |  |  |
| 71 | imported_reader_boot.glp | MATCH |  |  |
| 72 | inner_product.glp | MATCH |  |  |
| 73 | inner_product_iter.glp | MATCH |  |  |
| 74 | insertion_sort.glp | MATCH |  |  |
| 75 | interlaced_streams.glp | MATCH |  |  |
| 76 | is_list.glp | MATCH |  |  |
| 77 | length.glp | MATCH |  |  |
| 78 | lesseq.glp | MATCH |  |  |
| 79 | list_to_bst.glp | MATCH |  |  |
| 80 | main.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "new_channel" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 81 | main_module.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "reduce" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 82 | many_counters.glp | MATCH |  | both reject downstream |
| 83 | map_inc.glp | MATCH |  |  |
| 84 | math_module.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "reduce" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 85 | maxlist.glp | MATCH |  |  |
| 86 | mediator.glp | MATCH |  |  |
| 87 | member.glp | MATCH |  |  |
| 88 | merge_dynamic.glp | MATCH |  |  |
| 89 | merge_ordered.glp | MATCH |  |  |
| 90 | merge_simple.glp | MATCH |  |  |
| 91 | merge_sort.glp | MATCH |  |  |
| 92 | merge_tree.glp | MATCH |  |  |
| 93 | min.glp | MATCH |  |  |
| 94 | minimal_race_boot.glp | MATCH |  |  |
| 95 | monitor.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "monitor" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 96 | monitor_test.glp | MATCH |  |  |
| 97 | mwm.glp | MATCH |  |  |
| 98 | natural_numbers.glp | MATCH |  |  |
| 99 | network.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "receive" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 100 | network2.glp | MATCH |  |  |
| 101 | network3.glp | MATCH |  |  |
| 102 | network4.glp | MATCH |  |  |
| 103 | network_switch.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "send" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 104 | network_switch_3way.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "send" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 105 | nth.glp | MATCH |  |  |
| 106 | observe.glp | MATCH |  |  |
| 107 | observe_minimal.glp | MATCH |  |  |
| 108 | observe_play.glp | MATCH |  |  |
| 109 | observed_monitor.glp | MATCH |  |  |
| 110 | observer.glp | MATCH |  |  |
| 111 | observers.glp | MATCH |  | both reject downstream |
| 112 | parallel_table.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "read" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 113 | ping_pong_test_boot.glp | MATCH |  |  |
| 114 | plain_meta.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "merge" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 115 | play_4agent.glp | MATCH |  | both reject at parse |
| 116 | play_4agents.glp | MATCH |  | both reject at parse |
| 117 | play_absolute.glp | MATCH |  | both reject downstream |
| 118 | play_agents.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "agent" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 119 | play_alice_bob.glp | MATCH |  |  |
| 120 | play_alice_bob_carol.glp | MATCH |  |  |
| 121 | play_alice_bob_full.glp | MATCH |  |  |
| 122 | play_alice_bob_simple.glp | MATCH |  |  |
| 123 | play_alice_bob_typed.glp | MATCH |  | both reject downstream |
| 124 | play_child_safe.glp | MATCH |  |  |
| 125 | play_cold_call.glp | MATCH |  | both reject downstream |
| 126 | play_dglp_boot.glp | MATCH |  |  |
| 127 | play_dglp_boot.glp | MATCH |  |  |
| 128 | play_dglp_boot.glp | MATCH |  |  |
| 129 | play_dm.glp | MATCH |  |  |
| 130 | play_feed.glp | MATCH |  |  |
| 131 | play_group_interlaced.glp | MATCH |  |  |
| 132 | play_group_manager.glp | MATCH |  |  |
| 133 | play_high_throughput.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "tau" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 134 | play_introduction.glp | MATCH |  | both reject at parse |
| 135 | play_low_throughput.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "tau" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 136 | play_madglp_boot.glp | MATCH |  |  |
| 137 | play_mutual_credit.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "agent" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 138 | play_payment.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "agent" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 139 | play_redemption.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "agent" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 140 | play_typed_cold_call.glp | MATCH |  | both reject downstream |
| 141 | play_typed_routed.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Clause for "bob_wait/2" appears between procedure declaration and clauses for "play/0".   Procedure declaration at line 169 must be immediately followed by its clauses. |
| 142 | play_typed_simple.glp | MATCH |  | both reject downstream |
| 143 | play_ui_boot.glp | MATCH |  |  |
| 144 | play_ui_dglp_boot.glp | MATCH |  |  |
| 145 | play_ui_dglp_boot.glp | MATCH |  |  |
| 146 | play_ui_dglp_boot.glp | MATCH |  |  |
| 147 | play_ui_madglp_boot.glp | MATCH |  |  |
| 148 | play_ui_madglp_boot.glp | MATCH |  |  |
| 149 | play_ui_sim_boot.glp | MATCH |  |  |
| 150 | play_ui_sim_boot.glp | MATCH |  |  |
| 151 | play_ui_sim_boot.glp | MATCH |  |  |
| 152 | plus.glp | MATCH |  |  |
| 153 | plus_constraint.glp | MATCH |  |  |
| 154 | polygon_area.glp | MATCH |  |  |
| 155 | prefix.glp | MATCH |  |  |
| 156 | primes.glp | MATCH |  |  |
| 157 | producer_consumer.glp | MATCH |  |  |
| 158 | producer_consumer_countdown.glp | MATCH |  |  |
| 159 | queue_manager.glp | MATCH |  | both reject downstream |
| 160 | quicksort.glp | MATCH |  |  |
| 161 | quicksort_original.glp | MATCH |  |  |
| 162 | replicate.glp | MATCH |  |  |
| 163 | replicate2.glp | MATCH |  |  |
| 164 | replicate3.glp | MATCH |  |  |
| 165 | response_handling.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "tag_stream" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 166 | response_handling_unfolded.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "tag_stream" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 167 | reverse.glp | MATCH |  |  |
| 168 | reverse_naive.glp | MATCH |  |  |
| 169 | reversed_flow_boot.glp | MATCH |  |  |
| 170 | runtime_control_meta.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "merge" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 171 | self.glp | MATCH |  |  |
| 172 | send_reader_boot.glp | MATCH |  |  |
| 173 | shared_variable_boot.glp | MATCH |  |  |
| 174 | snapshot_meta.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "reduce" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 175 | snapshot_meta_cp.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "merge" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 176 | social_agent.glp | MATCH |  |  |
| 177 | social_graph_protocol.glp | MATCH |  |  |
| 178 | social_graph_protocol_v2.glp | MATCH |  | both reject downstream |
| 179 | stream_security.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "produce_batch_a" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 180 | streams.glp | MATCH |  | both reject downstream |
| 181 | substitute.glp | MATCH |  |  |
| 182 | sum_list.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "sum" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 183 | switch2x2.glp | MATCH |  |  |
| 184 | termination_detection_meta.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "reduce" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 185 | termination_meta.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "reduce" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 186 | test_4player.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "social_graph" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 187 | test_agent_init.glp | MATCH |  | both reject downstream |
| 188 | test_balance.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "get_balance" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 189 | test_blocklace.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "blocks_at_round" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 190 | test_bug.glp | MATCH |  |  |
| 191 | test_friend.glp | MATCH |  |  |
| 192 | test_lookup.glp | MATCH |  |  |
| 193 | test_lookup2.glp | MATCH |  |  |
| 194 | test_repayments.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "compute_repayments" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 195 | test_waves.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "wave_of_round" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 196 | three_agent_merge_boot.glp | MATCH |  |  |
| 197 | three_agent_pipeline_boot.glp | MATCH |  |  |
| 198 | times.glp | MATCH |  |  |
| 199 | timestamped_tree_meta.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "reduce" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 200 | tracing_meta.glp | DIVERGE |  | parse-acceptance divergence (ANTLR=accept, hand=reject): Procedure declaration for "reduce" has no clauses.   A procedure declaration must be immediately followed by its clauses. |
| 201 | translate.glp | MATCH |  |  |
| 202 | traversals.glp | MATCH |  |  |
| 203 | tree_sum.glp | MATCH |  |  |
| 204 | two_hop_flow_boot.glp | MATCH |  |  |
| 205 | typed_actors.glp | MATCH |  |  |
| 206 | typed_social_agent.glp | MATCH |  |  |
| 207 | typed_social_agent.glp | MATCH |  |  |
| 208 | typed_social_agent.glp | MATCH |  |  |
| 209 | typed_social_agent.glp | MATCH |  |  |
| 210 | typed_social_agent.glp | MATCH |  |  |
| 211 | typed_ui_actors.glp | MATCH |  |  |
| 212 | typed_ui_actors.glp | MATCH |  |  |
| 213 | typed_ui_actors.glp | MATCH |  |  |
| 214 | typed_ui_actors.glp | MATCH |  |  |
| 215 | typed_ui_actors.glp | MATCH |  |  |
| 216 | typed_ui_mediator.glp | MATCH |  |  |
| 217 | typed_ui_mediator.glp | MATCH |  |  |
| 218 | typed_ui_mediator.glp | MATCH |  |  |
| 219 | typed_ui_mediator.glp | MATCH |  |  |
| 220 | typed_ui_mediator.glp | MATCH |  |  |
| 221 | ui_agent.glp | MATCH |  | both reject downstream |
| 222 | ui_mediator.glp | MATCH |  |  |
| 223 | writer_response_boot.glp | MATCH |  |  |

**Totals**: 175/223 MATCH. Un-caused divergences (defects — FR-008): 0.


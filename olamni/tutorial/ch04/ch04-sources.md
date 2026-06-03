# Ch 4 Sources — Basic Concurrent Programming

**PDF**: `GLP_ART.pdf`, book pp 25–43 (PDF pp 37–55).

## Sections (verified)
- 4.1 Programming with Constants — p 25 (unit clauses, conjunctive goals, multiple clauses, binary unit clauses, logic gates, clauses with bodies, guards for multiple reader occurrences, compound circuits)
- 4.2 Streams — p 30 (producers/consumers, list reversal, stream merging, stream distribution, ripple-carry adder, buffered communication, objects and monitors)
- 4.3 Recursive Programming — p 37 (Peano, integer arithmetic, factorial/fibonacci, flatten, binary trees, insertion sort, merge sort, non-ground distributor, tree substitution)
- 4.4 Metaprogramming — p 41 (programs-as-data, trust-mode / fail-safe / control / tracing metainterpreters)

## Code-block index — §4.1 Programming with Constants
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 4.1.1 | `p(a).` unit clause | p 25 | 1 unit clause | constant pattern |
| 4.1.2 | `q(b). q(a).` multi-clause | p 27 | 2 unit clauses | committed-choice demo |
| 4.1.3 | Logic Gates `and/3`, `or/3`, `not/2`, `xor/3` | p 28 | 14 unit clauses | dataflow / constants |
| 4.1.4 | `nand/3` (body) | p 29 | 1 clause `:- and(A?,B?,W), not(W?,Z)` | first clause-with-body |
| 4.1.5 | `half_adder/4` | p 29–30 | 1 clause + `ground` guards on A?,B? | multi-reader via guards |
| 4.1.6 | `full_adder/5` | p 30 | 1 clause composing two half-adders + or | compound circuit |

### §4.1 Formal boxes
- **Formal 4.1: Produces and Consumes Parameters** — p 29 (head reader = produces, head writer = consumes table).

## Code-block index — §4.2 Streams
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 4.2.1 | `producer/2` (countdown) | p 31 | 2 clauses (base + recursive guarded by `>`) | producer |
| 4.2.2 | `consumer/3` (sum) | p 31 | 2 clauses (base + recursive guarded by `ground`) | consumer w/ accumulator |
| 4.2.3 | `reverse/2` naive | p 31 | 2 clauses + `append/3` | recursive list, O(n²) |
| 4.2.4 | `reverse/2` w/ accumulator + `reverse_acc/3` | p 32 | 1 entry + 2 acc clauses | linear reverse |
| 4.2.5 | `merge/3` simple fair (4 clauses) | p 32 | 4 clauses (two recursive + two early-exit base cases) | stream merge |
| 4.2.6 | `dmerge/3` + `dmerger/3` dynamic merge | p 33 | 7 dmerge + 1 dmerger clauses; handles nested `merge()` messages | dynamic stream tree |
| 4.2.7 | `merge_tree/2` + `merge_layer/2` static balanced tree | p 33 | 2 + 3 clauses | balanced merge tree |
| 4.2.8 | `distribute/3` broadcast | p 33 | 2 clauses, `ground` guard | broadcast distributor |
| 4.2.9 | `distribute_indexed/3` | p 33–34 | 3 clauses w/ `send(N,X)` tags | indexed distributor |
| 4.2.10 | `observer/3` | p 34 | 2 clauses, `ground` guard | non-consuming observer |
| 4.2.11 | `adder/4` ripple-carry | p 34 | 2 clauses chaining `full_adder/5` | n-bit adder over streams |
| 4.2.12 | `bb/0`, `consumer/1`, `producer/2` sliding-window buffer | p 34 | top-level + 1 consumer + 1 producer | buffered communication |
| 4.2.13 | `bb_test/0` terminating buffer variant | p 34–35 | top-level + 2 consumer + 1 producer | bounded buffer |
| 4.2.14 | `counter/1`, `counter_loop/2` | p 35 | 1 entry + 4 loop clauses (clear/add/read/done) | object/monitor pattern |
| 4.2.15 | `accumulator/1`, `acc_loop/2`, `test_acc/0`, `client1/1`, `client2/1` | p 36 | object + 2 clients via `merge` | monitor with multiple clients |

### §4.2 Formal boxes
- **Formal 4.2: SRSW in Continuation Calls** — p 31 (continuation calls pass readers).
- **Formal 4.3: Which Guards Enable Multiple Reader Occurrences?** — p 35–36 (`ground`/`constant`/`number`/`integer` yes; `tuple`/`known` no).

## Code-block index — §4.3 Recursive Programming
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 4.3.1 | Peano arithmetic `plus/3`, `times/3`, `lesseq/2`, `natural_number/1` | p 37 | several clauses each | structure-recursive arithmetic |
| 4.3.2 | Integer arithmetic `double/2`, `average/3`, `abs/2`, `max/3` | p 37 | 1–2 clauses each | guarded integer ops |
| 4.3.3 | `factorial/2` | p 37–38 | 3 clauses (0,1, recursive `>`) | recursive numeric |
| 4.3.4 | `factorial/2` tail-recursive + `fact_acc/3` | p 38 | entry + 2 acc | accumulator tail recursion |
| 4.3.5 | `fib/2` | p 38 | 3 clauses | branching recursion (O(2^N)) |
| 4.3.6 | `fib_linear/2` + `fib_acc/4` | p 38 | entry + 2 acc | linear Fibonacci |
| 4.3.7 | `flatten/2` + `flatten_acc/3` | p 38–39 | entry + 3 acc clauses | nested-list flatten |
| 4.3.8 | `tree_sum/2` | p 39 | 2 clauses (base + concurrent recursive) | binary-tree spawn |
| 4.3.9 | `insertion_sort/2` + `insert/3` | p 39 | 2 + 3 clauses | sort |
| 4.3.10 | `mergesort/2` + `split2/5` + `merge_sorted/3` | p 39–40 | 3 + 4 + 4 clauses | divide-and-conquer sort |
| 4.3.11 | `distribute_ng/3` + `copy/3` + `copy_list/3` | p 40 | non-ground stream distributor with `=..` | copy-on-write distribution |
| 4.3.12 | `substitute/4` + `replace/4` | p 40 | tree substitution | structure-recursive transform |

## Code-block index — §4.4 Metaprogramming
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 4.4.1 | `reduce/2` encoding (programs-as-data) | p 41 | 3 unit clauses encoding the merge program | object-program data form |
| 4.4.2 | Trust-mode meta-interpreter `run/2` | p 41 | 4 clauses (halt/fork/cross-module/reduce) | minimal MI |
| 4.4.3 | Fail-safe meta-interpreter `run/4` | p 41–42 | 5 clauses w/ success-list output | failure-tolerant MI |
| 4.4.4 | Control meta-interpreter `run/5` + `suspended_run/4` | p 42 | 5 + 2 clauses w/ control stream (suspend/resume/abort) | controllable MI |
| 4.4.5 | Tracing meta-interpreter `run/3` + indexed `reduce/3` + `replay/3` | p 42–43 | 3 + 3 + 3 clauses | traced MI w/ deterministic replay |

## Tutorial mode
cohesive-synthesis — per charter §1: section-driven, single .glp per substantial Program. Many code blocks → many small `.glp` files grouped by sub-section. Helpers (e.g., `producer`, `consumer`, `merge`) appear repeatedly and are candidates for `useful-techniques.glp`.

## Companion repo references
- `programs/typed_book/recursive/list_processing/` — list reverse, sort, append.
- `programs/typed_book/recursive/arithmetic_trees/` — factorial, fibonacci, tree_sum.
- `programs/typed_book/recursive/structure_processing/` — flatten, substitute.
- `programs/typed_book/streams/producers_consumers/` — producer/consumer, distributors, observers.
- `programs/typed_book/streams/objects_monitors/` — counter, accumulator.
- `programs/typed_book/streams/buffered_communication/` — bb / bb_test.
- `programs/typed_book/meta/` — `plain/`, `enhanced/`, `debugging/` meta-interpreters.
- `programs/typed_book/constants/` — logic gates, half/full adder.
- `../charter.md`

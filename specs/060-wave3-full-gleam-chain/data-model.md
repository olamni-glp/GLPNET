# Phase 1 Data Model: Wave 3 — Full Gleam chain

**Feature**: `060-wave3-full-gleam-chain` | **Date**: 2026-07-27

Entities as named in `spec.md` → Key Entities, mapped onto the existing Gleam modules that own them. This feature adds no persistent store; every entity below is in-memory or on-disk-as-file.

---

## GLP source module

Named unit of GLP text: type declarations, procedure declarations, clauses; may reference other modules.

| Field | Meaning | Validation |
|---|---|---|
| `name` | module identity | unique within a load set |
| `declarations` | type + procedure declarations | every procedure called must be declared (FR-002) |
| `clauses` | ordered clause list | each clause SRSW-valid (FR-005) |
| `imports` | referenced module names | may resolve after this module loads (FR-008) |

**Owner**: `glp/parser/ast.gleam`
**Lifecycle**: parsed → SRSW-checked → type-checked → compiled → loaded. A failure at any stage yields a structured diagnostic and leaves the runtime usable (FR-003).

## Compiled program

Loaded executable form, addressable by procedure name and arity.

| Field | Meaning | Validation |
|---|---|---|
| `procedures` | name/arity → bytecode entry | no duplicate name/arity within a module |
| `link_table` | resolved cross-module targets | static links resolved at load; dynamic dispatch resolved at call (FR-009) |
| `unresolved` | references awaiting a later module | must resolve before first call, else structured error |

**Owner**: `glp/bytecode/program.gleam`, `glp/compiler/loader.gleam`
**State transitions**: `loaded` → `linked` → `executable`. Re-loading a module replaces its procedures and invalidates link-table entries pointing at the old ones (FR-015) — stale entries must not remain reachable.

## Goal

A query posed against a compiled program.

| Field | Meaning |
|---|---|
| `term` | the goal term |
| `outcome` | `Success(bindings)` \| `Failure` \| `Suspended(readers)` \| `Bounded(steps)` |
| `bindings` | writer bindings produced on success |

**Owner**: `glp/engine/runner.gleam`, `glp/engine/types.gleam`
**Invariants**: suspension is distinct from failure (FR-006); a bounded run reports `Bounded`, never `Failure` (FR-013); only writers are ever bound, never readers, never writer-to-writer (FR-007).

## Runtime instance

One running GLP engine — reference, C#, or Gleam.

| Field | Meaning |
|---|---|
| `identity` | instance name/address used in link negotiation |
| `program` | the loaded compiled program |
| `links` | active links held by this instance |
| `capabilities` | wire-format version + supported feature set |

**Owner**: `glp/engine.gleam` (composition root — currently PARTIAL, gap G6: kernels compiled in, no transport injection seam)

## Link

Established, capability-negotiated connection between two instances.

| Field | Meaning | Validation |
|---|---|---|
| `link_id` | identity of this link | unique per instance |
| `peer` | remote instance identity | — |
| `scheme` | `loopback` \| `tcp` \| (`zmq`, `quic`, `ws` behind the seam) | acceptance requires loopback + tcp only (FR-025) |
| `state` | `negotiating` \| `up` \| `refused(reason)` \| `down(reason)` | refusal always carries a stated reason (FR-022) |
| `capabilities` | negotiated intersection | mismatch ⇒ `refused`, never silent reinterpretation (FR-029) |

**Owner**: `glp/link/seam/{link_id,link_address,link_options,link_scheme,transport,endpoint,link_fault}.gleam`
**State transitions**: `negotiating → up` on capability agreement; `negotiating → refused(reason)` on mismatch; `up → down(reason)` on peer loss, observed within 30 s (SC-007, FR-024). No transition from `refused` — a new link must be established.

## Message

Unit carried over a link.

| Field | Meaning | Validation |
|---|---|---|
| `sequence` | per-link ordering position | strictly increasing; delivery in order (FR-021) |
| `payload` | encoded term | round-trips identically, incl. nested structures and unbound variables (FR-027) |
| `frame` | framing + CRC | a partially-received frame is never delivered as complete |

**Owner**: `glp/codec/term_codec.gleam`, `glp/link/reliability/{frame_codec,crc32}.gleam` (PARTIAL — gap G8, floor only)

## Conformance case

One corpus entry pairing a source program and goal with its expected outcome.

| Field | Meaning | Validation |
|---|---|---|
| `case_id` | stable identity | unique in `corpus.list` |
| `program` | source under test | — |
| `goal` | goal to pose | — |
| `expected` | reference outcome (the golden) | may be **absent** — see below |
| `scope` | `in_scope` \| `out_of_scope(reason)` | absent golden ⇒ `out_of_scope("golden missing — 059 T051 drift")` (FR-018a) |

**Owner**: `test/parity/{corpus.list,expected.list,corpus-manifest.md}`
**Invariant**: a case with no golden is **never** counted as a pass (FR-018a). 44 such cases exist at wave start; SC-010 drives that to 0 or to individually-reasoned exceptions.

## Conformance report

Output of one corpus run.

| Field | Meaning | Validation |
|---|---|---|
| `verdicts` | per-case `pass` \| `fail` \| `out_of_scope(reason)` | one per case, none omitted (FR-017) |
| `counts` | pass / fail / out-of-scope totals | pass + fail + out_of_scope == total (SC-002) |
| `divergences` | case id, expected, observed | one per fail (FR-017) |

**Owner**: `test/parity/run_gleam_corpus.sh`, `test/parity/run_differential.sh`
**Invariant**: deterministic — identical code yields identical verdicts and counts across runs (FR-019, SC-008).

---

## Entity relationships

```text
GLP source module ──compiles to──> Compiled program
                                        │
                                   held by
                                        ▼
Goal ──posed against──────────> Runtime instance ──holds──> Link ──carries──> Message
                                        ▲                     │
                                        └──── peer ───────────┘

Conformance case ──run against──> Runtime instance ──produces──> Conformance report
```

# Metric-Combination Template — the shared per-seed metric table (R8)

The reusable Markdown table every `engine-separation` successor seed (#2–#16)
instantiates at its `/buildkit-specify`, instead of inventing a format. This is the
**FR-003 deliverable** of seed **#1a** (`iterative-refinement-and-verification-framework`).
The binding decision is [`DECISIONS-LOG.md`](DECISIONS-LOG.md) **R8**:

> **Per-seed metric table = shared Markdown template** `name | kind | tool | threshold`. *(Applies to: #1a.)*

The metric model it encodes — **pragmatic + formal, always both** — is
[`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §2; the five Shapiro criteria the host/infra
rule justifies against are §5; the six formal-tooling slots a seed's `formal` rows draw from
are §4. Deferrals these create are anchored in [`DEFERRALS.md`](DEFERRALS.md).

---

## 1. The template

Each seed declares **one** table. Every row is a single metric. The table **must blend
pragmatic and formal** entries ([`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §2, decision
U-M1) — except a host/infra-only seed, which may omit the `formal` tier under the rule in §3
below.

| name | kind (pragmatic\|formal) | tool | threshold |
|---|---|---|---|
| *short metric name* | `pragmatic` \| `formal` | *concrete harness/command/prover — never "manual review"* | *measurable pass/fail bound* |

Column discipline (each is load-bearing — an empty or vague cell fails US1-AC1):

- **name** — what the metric asserts, in a few words.
- **kind** — exactly `pragmatic` or `formal`.
  - **pragmatic** = behavioral/structural gates runnable today: the REPL suite
    (`bash test/run_all_tests.sh`, 384/384), round-trip identity tests, cross-process
    loopback equivalence, capture-coverage tests, `grep` invariants, ResourceSnapshot
    baselines, footprint measurement (massif / VmRSS), and — for any front↔back wire
    protocol — **Promela/SPIN** with named safety+liveness (R14, REQUIRED default).
  - **formal** = mechanized or decidable properties drawn from the six slots
    ([`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §4): ANTLR4 grammar-as-verifier;
    MLIR GLP/FCP IL-dialect round-trip; byte-parity golden-file oracle (FR-060/061);
    Lean 4 prover (Rocq the alternative per §3 of that file); SMT (Z3/CVC5)
    finite-domain decision; Promela/SPIN + the R15 armoury at the model-checked tier.
- **tool** — a *named, runnable* harness or prover with the exact command/file where it
  exists. "A concrete tool" means a reviewer can run it. No prose placeholders.
- **threshold** — a *measurable* bound: a count (`384/384`), an identity
  (`decode(encode(p)) ≡ p`), an SMT verdict (`UNSAT on the negation`), a delta
  (`0 new SRSW violations`), or a named property (`deadlock-freedom + progress`).

**Termination of the refinement loop** ([`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §1) is
*all thresholds met AND owner confirmation* at the interactive `/buildkit-specify` step —
never budget exhaustion alone. The table is the evaluator the loop runs each iteration.

---

## 2. Filled worked example — seed #5 (`result-codec-and-framecodec-ride`)

Instantiating the template end-to-end for the already-reconciled result-envelope codec
([`5-result-codec-and-framecodec-ride.md`](5-result-codec-and-framecodec-ride.md)). #5 is a
**boundary codec** that transforms the engine-internal `ExecutionResult` + `DrainResult` +
`queryVarWriters` + captured output into a self-contained `byte[]` chunk riding
`FrameCodec`/`TcpTransport`. Because it **touches the byte/wire contract**, the table MUST
carry at least one `formal` row (**US1-AC1**) — here it carries four (byte-parity, the SPIN
protocol check, the Lean 4 unbound-sentinel proof, and the SRSW gate). Applicable ratified
decisions: **R1** (var→writer = stable `GlobalVarId`), **R2** (unbound = display-only for MVP),
**R3** (output = length-prefixed UTF-8 blob), **R14** (SPIN required for #5's table).

| name | kind | tool | threshold |
|---|---|---|---|
| Cross-process loopback equivalence | pragmatic | REPL suite split-process test (`test/run_all_tests.sh`): engine in process A, client in process B, assert `RemoteExecutionResult` ≡ in-process `ExecutionResult` over a ground-result goal corpus | identical bindings + status + error across the split for all corpus goals |
| Round-trip identity | pragmatic | Unit test `decode(encode(result))` over all `ExecutionStatus` values + ground bindings + null bindings (unbound → display-only sentinel per **R2**) | 100% of parametrized cases |
| Output-capture completeness | pragmatic | Unit test: set `OutputCallback` to capture lines, run a goal with `_output/1`, assert envelope output field equals captured lines (length-prefixed UTF-8 blob per **R3**) | 100% match on the test corpus |
| Front↔back protocol validation | pragmatic | Promela/SPIN on the request/response handshake (`spin -a` → `pan -a`), named properties = deadlock-freedom + no unspecified receptions + a request-eventually-answered progress property (**R14**) | SPIN reports all three hold, or yields a counterexample trace; result recorded |
| Byte-parity (wire contract) | **formal** | Byte-parity golden-file harness analogous to FR-060/061 (`FrameCodec.cs:31-32` precedent): C# encoder → bytes → C# decoder; record the exact byte layout | exact byte-level `decode(encode(x)) = x` for every field variant; no lossy field |
| Unbound-var sentinel correctness | **formal** | Lean 4 over Lean-LSP-MCP + APOLLO + Lean Copilot (Claude-native, no-API; budget 20 attempts per **R13**): prove `Suspended` + ≥1 `null` binding ⇒ encoder emits the unbound-sentinel tag and decoder reconstructs the display string with no heap access | proposition proved, or `sorry`-isolated + owner-escalated; 100% of `Suspended` cases |
| SRSW preservation | **formal** | In-repo SRSW validator (REPL suite §D) run before/after the codec + its #3 output-capture dependency | 0 new SRSW violations (REPL suite 384/384) |

The blend is satisfied (three pragmatic rows + four formal rows), the formal tier is present
because #5 is a wire/byte-contract seed (US1-AC1), and every row names a concrete runnable
tool with a measurable threshold. This matches the §6 headline combination for #5 in
[`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md): *loopback equiv + round-trip + output (prag) ·
byte-parity golden + Lean 4 unbound-sentinel + SRSW (formal)*.

---

## 3. The host/infra rule — formal tier MAY be omitted, but Shapiro N/A MUST be justified (R9)

[`DECISIONS-LOG.md`](DECISIONS-LOG.md) **R9** (binding on #1a):

> **Shapiro-criteria mapping = mandatory** for language/semantics/wire seeds; **advisory
> (N/A + justification)** for host/infra seeds (#8, #10).

A seed whose work lives **above** the engine library — in the host/exe/composition-root layer,
not the GLP language, execution semantics, or the wire/byte contract — MAY omit the `formal`
tier from its metric table. The exemplars are **#8** (`liveness-crash-restart-host`) and
**#10** (`multi-accept-transport-extension`). When a seed exercises this exemption it **MUST**
instead record an explicit **per-criterion N/A justification**: one line for *each* of the five
Shapiro / embedded-switch criteria ([`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §5),
explaining why that criterion does not apply to this seed (**US1-AC2**). Silence is not
allowed — every criterion must be addressed, as either *enforced-by* (if it does apply) or
*N/A because …* (if it does not).

The five criteria a host/infra seed must justify against (verbatim from §5):

| criterion | enforced by (when it applies) |
|---|---|
| Committed-choice concurrency | post-quiescence result collection; single-owner heap (`heap_fcp.cs:136-141`) |
| SRSW (single-reader/single-writer) | in-repo SRSW validator (REPL suite §D); codec must not alias var indices |
| Suspension correctness | faithful `SuspendedGoals`/`BlockingReaders` (`scheduler.cs:67-73`); re-wire proof |
| Monotone variable binding | snapshot-at-quiescence verbatim Cells; never re-bind |
| Three-valued unification (Success\|Suspend\|Fail) | faithful Status projection (`scheduler.cs:33-43`); no fourth outcome |

### Worked N/A justification — seed #10 (`multi-accept-transport-extension`)

#10 is a pure transport refactor (one-accept → multi-accept on `TcpTransport`); it adds no
GLP language construct, alters no reduction semantics, and changes no byte/wire format. Its
metric table omits the `formal` tier and instead carries this per-criterion N/A block:

| criterion | applies? | justification |
|---|---|---|
| Committed-choice concurrency | **N/A** | The accept loop runs in the host transport layer above the engine; each accepted connection delivers to a single-owner engine instance unchanged. No new committed-choice / backtracking path is introduced into GLP reduction. |
| SRSW (single-reader/single-writer) | **N/A** | Multi-accept adds host-side `LinkId`-keyed connection state; it never touches GLP variable indices or heap Cells. The SRSW property is a GLP-program invariant the transport never observes. *(A pragmatic `SRSW-unchanged` row may still be kept as a regression guard — that is a `pragmatic` REPL-suite check, not a `formal` obligation.)* |
| Suspension correctness | **N/A** | `SuspendedGoals`/`BlockingReaders` are produced by the scheduler at quiescence; the transport carries opaque frames and neither reads nor rewrites them. |
| Monotone variable binding | **N/A** | The transport moves `byte[]` chunks only; it never decodes a binding and so cannot re-bind or down-grade a bound variable. |
| Three-valued unification (Success\|Suspend\|Fail) | **N/A** | The Status projection is encoded once by the result codec (#5) and transmitted verbatim; multi-accept changes which connection carries the frame, never the frame's status field. |

With every criterion explicitly marked **N/A because …**, #10's table satisfies R9 / US1-AC2
even though it carries no `formal` row. (Contrast #5 above, a wire/byte seed for which the
formal tier is mandatory and N/A is not permitted.)

> **Caveat on #8.** R9 names #8 as a host/infra exemplar *eligible* for the exemption.
> #8's own reconciliation memo nonetheless elects to keep two lightweight `formal` rows
> (an FR-057 `csproj` dependency-graph check and a Z3 exception-taxonomy exhaustiveness
> check — [`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §6, slot 5). R9 permits the
> exemption; it does not forbid a host/infra seed from *choosing* to carry formal rows where
> a cheap decidable property exists. The rule is a floor (justify every criterion), not a
> ceiling.

---

## 4. How a seed consumes this template

At `/buildkit-specify` for seed N, `buildkit-roadmap brief <id>` surfaces the PRE-SPECIFY
pointer to [`DECISIONS-LOG.md`](DECISIONS-LOG.md) + [`DEFERRALS.md`](DEFERRALS.md). The
engineer then:

1. Copies the §1 table skeleton into the seed's spec.
2. Proposes the pragmatic+formal blend, drawing `formal` rows from the six slots
   ([`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §4) and pragmatic rows from the §2 gate set.
3. For a **language / execution-semantics / wire-or-byte** seed: includes ≥1 `formal` row
   (US1-AC1) and maps the applicable Shapiro criteria as *enforced-by* (R9 mandatory).
4. For a **host/infra-only** seed: MAY omit the `formal` tier but MUST record the full
   five-criterion Shapiro N/A justification of §3 (R9 advisory / US1-AC2).
5. The owner confirms or amends the table; the accepted table is recorded in the seed's spec
   and becomes the loop's evaluator until all thresholds hold.

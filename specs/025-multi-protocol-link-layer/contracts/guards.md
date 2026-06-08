# Guard Set Contract — Feature 025 Multi-Protocol Link Layer

**Facet:** the approved guard set (FR-032..FR-039 + RULED "G" decisions).
**Status:** every item below is **PROPOSED — pending Gabi's explicit language-authority approval** (CLAUDE.md §Language Authority; DISCIPLINE §1.14). Nothing here is decided. Signatures, arities, modes, and SRSW set memberships are co-design proposals for the plan gate.

## 0. Scope, precedence, and relationship to `docs/guards-reference.md`

Per **FR-032**, `docs/guards-reference.md` is the **single authoritative guard spec**. This contract does **not** duplicate it — it (a) references its sections for already-implemented guards and (b) specifies the *deltas* this feature proposes: one NEW guard family, three FIXes, two/four DECLINEs, one UNTOUCHED guarantee, and one cross-cut conformance obligation. On acceptance, the NEW/FIX entries fold **into** `docs/guards-reference.md` (see §9 "Consolidation"); they are not maintained as a second spec.

**Source precedence applied throughout (binding contract).** Tier-1 local GLP specs (`docs/guards-reference.md`, corpus 00-16) > Shapiro GLP/GLP-typing papers (corpus 10/11/17 where Shapiro) > earlier concurrent-logic papers (FCP/CP/Logix/Oz, corpus 17). The standard-order family `@<`/`@>`/`@=<`/`@>=` and term-identity `==`/`\==`/`\=` are **Tier-3 Prolog/ISO/FCP idioms deliberately absent from the GLP paper kernel** (corpus B2-B3-G §G "Decisive correction"); they are added/declined here only by Gabi's explicit ruling (Clarification 2026-06-06 / FR-037 for `@<`; FR-036 declines for the rest), never because Prolog has them.

**GLP-not-Prolog reminders honored below.** Writer-mode outputs are constructed in clause *heads*, never via `=` in the body; guards are pure three-valued tests (succeed / suspend / fail) in HEAD→GUARD phase; no cut, no if-then-else, no `\=`-as-control. Every clause shown carries `procedure` (and where exported, `exported procedure`) declarations.

### Three-valued ask-semantics (the invariant every guard in this contract obeys)

| Operand state (after deref) | Verdict | Rationale (GLP invariant) |
|---|---|---|
| bound + condition satisfied | **succeed** | committed verdict cannot be falsified later (monotone) |
| bound + condition definitely false | **fail** (try next clause) | definite mismatch |
| **unbound reader** (incl. un-arrived remote value, incl. nested-in-compound) | **suspend** → reactivate **exactly once** on bind | un-arrived remote value behaves as a local unbound reader → SUSPEND, never spurious FAIL (FR-017/FR-050) |
| **unbound writer** | **fail** | SRSW: no paired reader can ever supply the value; not a suspend case (guards-reference §"Guard Arguments: Why Readers?") |

Non-monotone guards (`~(=?=)`, any negation, `otherwise`) carry the extra obligation (FR-039): they are gated **fully-known-across-the-link** before commit, so a late remote bind cannot falsify an already-committed verdict. Across a link this means the operand must be *ground* (every embedded reader arrived), not merely top-level bound.

---

## 1. ADD — standard-order term-ordering family `@<` `@>` `@=<` `@>=`  — status **NEW**

**Authority basis:** FR-037 + Clarification 2026-06-06 (ruling B). Peer-ids MAY be non-numeric compound terms requiring a total order (leader-election / sorted-peer-set use cases in scope). The family is **required, not optional**. Tier-3 origin → added ONLY by this explicit ruling.

### 1.1 PROPOSED signatures (modes)

```prolog
procedure @<(_?, _?).
procedure @>(_?, _?).
procedure @=<(_?, _?).
procedure @>=(_?, _?).
```

Infix surface syntax `X? @< Y?` etc., transformed to prefix `@<(X?, Y?)` (mirrors the existing `<`/`=:=` infix→prefix transform). Both operands are **readers** (`X?`) — same rationale as `<` and `=?=`: a reader suspends patiently; a writer fails (guards-reference §"Guard Arguments: Why Readers?").

### 1.2 PROPOSED semantics

- **Total order over GROUND terms.** Defined only when both operands are ground. The order is the GLP standard order of terms. PROPOSED ordering classes (lowest→highest), pending approval of the exact total order: **Number < String (atom) < compound**; within numbers by numeric value; within strings by code-point lexicographic order; within compounds by **arity, then functor name, then arguments left-to-right** (the conventional standard order; chosen so the order is stable across the Dart↔C# wire — corpus 13 byte-parity, FR-060). `@=<` and `@>=` are the reflexive companions. Equality within the order coincides with `=?=`.
- **Three-valued ask-semantics** (FR-039), over ground terms:
  - both ground → **succeed**/**fail** per the order;
  - either operand an unbound reader (top-level OR nested inside a compound — see §3) → **suspend**, reactivate exactly once on bind;
  - either operand an unbound writer → **fail**.
- **Ground-implying for SRSW.** When `@<`/`@>`/`@=<`/`@>=` succeed, both operands are fully ground (the order is undefined otherwise); therefore both operands' writer+reader may occur multiply in the clause, exactly like the arithmetic comparison guards (guards-reference §"Guards That Imply Groundness"). They join the ground-certifying set.
- **Non-negatable.** Like the arithmetic comparisons (`<` etc.), proposed **non-negatable**: the natural complement of `@<` is `@>=` (and `@>`↔`@=<`), so `~(@<)` is both redundant and would invite a partial-order trap on non-ground operands. (Open question OQ-1 if Gabi prefers negatable.)

### 1.3 Exact edit sites (live code, multi-site core edit)

1. **Lexer** — `glp_runtime/lib/compiler/lexer.dart:61`. Today `case '@':` returns a bare single-char `TokenType.AT` (used by the `Goal@Agent` isolate-spawn operator, token.dart:55). Extend with lookahead, mirroring the `=`/`>` arms (lexer.dart:74-115): `@<` → new `AT_LESS`; `@>` then optional `=` → `AT_GREATER` / `AT_GREATER_EQUAL`; `@=` then required `<` → `AT_LESS_EQUAL`. **Disambiguation constraint:** `@` followed by non-`<`/`>`/`=` must still yield `TokenType.AT` (preserve `Goal@Agent`); `@>=`/`@=<` need two-char lookahead. This is the only place where `@` tokenization changes.
2. **Token enum** — `glp_runtime/lib/compiler/token.dart` (near AT at :55). Add `AT_LESS`, `AT_GREATER`, `AT_LESS_EQUAL`, `AT_GREATER_EQUAL`.
3. **Parser, infix-comparison detection** — `glp_runtime/lib/compiler/parser.dart:687-690`. Add the four new token types to the `_check(...)` disjunction; the existing transform (parser.dart:694-697) reuses `opToken.lexeme` as the functor, so it produces `@<(L,R)` with no further change. `_operatorFunctor` (parser.dart:1142-1182) needs the four cases only if these ops are also reachable through `_parseExpression`'s operator path; for guard position the lexeme path at :696 suffices (verify during impl).
4. **Runner `_evaluateGuard`** — `glp_runtime/lib/bytecode/runner.dart:4330` switch. Add four arms after the arithmetic block (after :4394), each: read the two already-dereferenced ground operands, apply the standard-order comparator, return success/failure. Reuse / extend the cycle-safe `_termsEqual` machinery (runner.dart:4699) into a new `_compareTerms(a, b, cx)` returning `-1|0|1` with the same VarRef-deref + visited-set discipline. Unbound-reader suspension is already handled upstream by the generic gate (runner.dart:3137) **once §3's compound-recursion fix lands** — without §3, a nested unbound reader in a compound peer-id would wrongly FAIL instead of SUSPEND.
5. **SRSW analyzer set memberships** — `glp_runtime/lib/compiler/analyzer.dart`:
   - add `@<`,`@>`,`@=<`,`@>=` to the **ground-implying comparison set** (`comparisonOps`, analyzer.dart:727) so both operands are `markGrounded` on success (SC-006).
   - add them to `_nonNegatableGuards` (analyzer.dart:616) per §1.2 (or `_negatableGuards` if OQ-1 resolves negatable).
6. **Builtin-guard registration** — `glp_runtime/lib/analysis/type_checker/prelude.dart`: add `@</2`,`@>/2`,`@=</2`,`@>=/2` to `builtinProcedures` (prelude.dart:82) and the bare names to `predefinedProcedureNames` (prelude.dart:33) so they are accepted in guard position and protected from redefinition (mirrors `<`/`=:=` at prelude.dart:53-58 and :102-107).
7. **Codegen** — no change required: `_generateGuard` falls through to the generic `bc.Guard(predicate, arity)` opcode (codegen.dart:452-458) for any predicate without a dedicated opcode, which routes to `_evaluateGuard`.

### 1.4 Test plan

- **Section A (runtime), `programs/tests/typed/order_guards.glp`** (NEW) — exported clauses guarded by each of the four, e.g.
  ```prolog
  exported procedure le(_?, _?, _).
  le(X, Y, yes) :- X? @=< Y? | true.
  le(_, _, no)  :- otherwise | true.
  ```
  Goals asserting: `@<` numeric (`le(1,2,_)`→yes), cross-class (`le(1, foo, _)` per Number<compound→yes), compound-by-arity (`le(f(1), g(1,2), _)`→yes), string lexicographic, equal→`@=<` yes / `@<` no. Mirror the `arith_guard_ground.glp` style (programs/tests/typed/arith_guard_ground.glp).
- **Section A suspend case** — a goal whose operand is an unbound reader bound later by a sibling goal; assert it suspends then reactivates exactly once and yields the ground verdict (model on `test_guard_suspend.glp`).
- **Section B (positive type-check)** — `order_guard_srsw.glp` (NEW): a clause that reads a `@<`-grounded var **multiple times** compiles (SC-006 positive).
- **Section C (negative type-check)** — `order_guard_srsw_bad.glp` (NEW): the same clause **without** the `@<` guard on that var is **rejected** by the SRSW analyzer (SC-006 negative). Also a negative for `~(@<)` rejected-as-non-negatable (if §1.2 holds).
- **Wire-stability test** (FR-060 tie-in): the same `@<` verdict on a compound peer-id holds Dart-side and C#-side (cross-runtime parity gate, deferred to the transport facet but the *comparator* is exercised here).

---

## 2. FIX — `atom/1` analyzer↔runner consistency  — status **FIX**

**Authority basis:** FR-033, SC-005. Confirmed live (B2-B3-G §"Verification note").

### 2.1 The divergence (verified)

- **Analyzer accepts + grounds it:** `atom` is in `_negatableGuards` (analyzer.dart:608) and `typeCheckOps` (analyzer.dart:671) — so `atom(X?)` compiles and `markGrounded`s X (relaxes SRSW).
- **Partial evaluator folds it:** `case 'atom': return concreteArg is ConstTerm && concreteArg.value is String;` (partial_evaluator.dart:1008-1009).
- **Runner has NO case:** `_evaluateGuard` (runner.dart:4330 switch) has no `atom` arm → falls to `default:` (runner.dart:4690-4692) → `print('[WARN] Unknown guard predicate')` + `GuardResult.failure`. So any input the analyzer accepts and grounds **fails at runtime** — a direct SC-005 violation.
- It is also **absent** from `builtinProcedures` (prelude.dart:82) and `predefinedProcedureNames` (prelude.dart:33), unlike every other type guard.

### 2.2 PROPOSED fix (consistent — make runtime match the already-grounding analyzer/PE)

The analyzer and PE already define `atom/1` as "non-numeric atomic constant (a String constant)" and treat it as ground-implying. The consistent fix is to **implement the runner arm to match**, not to remove it from the analyzer (removal would lose a real guard the PE already folds, and `atom` is paper-kernel per B2-B3-G §G table). PROPOSED:

```prolog
procedure atom(_?).
% atom(X?): succeed iff X? is a non-numeric atomic constant (string atom);
%           suspend on unbound reader; fail on unbound writer or on number/compound.
```

- **Runner arm** at runner.dart switch (alongside `string`, runner.dart:4416): succeed iff the dereferenced value is a `String`/`ConstTerm(String)` and not `'nil'` (the `[]` representation — `atom([])` should fail, matching `string`'s nil exclusion at runner.dart:4421/4424). This makes `atom` ≡ the runtime `string` arm; PROPOSED that `atom/1` is the **paper-kernel name** and `string/1` the **glpnet name** for the same test (OQ-2: confirm they are intended synonyms, or define `atom` to also accept `[]`/nil — Gabi's call; the corpus does not settle it).
- **Suspension** is already handled upstream by the generic gate (runner.dart:3137) — `atom` is not in the `unknown` exception, so an unbound reader suspends correctly with the arm present.
- **Registration:** add `atom/1` to `builtinProcedures` (prelude.dart:82) and `atom` to `predefinedProcedureNames` (prelude.dart:33), closing the last divergence.

### 2.3 Test plan

- **Section A**, `programs/tests/typed/atom_guard.glp` (NEW): `atom(hello,_)`→succeeds; `atom(42,_)`→fails (number); `atom(f(1),_)`→fails (compound); `atom([],_)`→per OQ-2; suspend-then-bind case.
- **Section B**: a clause that reads an `atom`-grounded var multiply compiles (atom is ground-implying).
- **Section C**: same clause without the guard is SRSW-rejected.
- **Regression guard (SC-005):** add a runtime goal for every analyzer-accepted `atom` shape and assert none hits the `[WARN] Unknown guard predicate` path.

---

## 3. FIX — compound-operand-suspend  — status **FIX**

**Authority basis:** FR-034, SC-009, Edge "Compound / imported-reader suspension". Confirmed live (B2-B3-G §"Interaction with distributed suspension" ⚠).

### 3.1 The bug (verified, exact)

The **generic `Guard` opcode** path collects unbound readers via `_dereferenceWithTracking` (runner.dart:3122). That helper, when the term is a `StructTerm`, **returns it as-is without recursing into its args** (runner.dart:4179-4182). So a compound operand containing a nested unbound reader — e.g. `peer(Region, Id?)` with `Id?` un-arrived from a remote bind — passes the top-level gate (no top-level unbound reader found, runner.dart:3137), reaches `_evaluateGuard`, and `_termsEqual` returns `false` on the unbound inner reader (runner.dart:4722/4757) → the guard **commits a FAIL** where it MUST **SUSPEND**. This is a non-monotone wrong commit: a later remote bind would have satisfied the guard.

Note the **dedicated `GroundEqual` opcode** (runner.dart:3570) does NOT have this bug — its `collectUnbound` recurses into `StructTerm.args` (runner.dart:3630-3633). The defect is specific to the generic-guard path that `@<`, `atom`, type guards, and `=?=`-when-not-both-bare-VarTerms all traverse.

### 3.2 PROPOSED fix

Make the generic-guard unbound-reader collection **recurse into compound args**, matching `GroundEqual.collectUnbound`. Two equivalent edit shapes (PROPOSED — pick one at impl, both are core edits requiring approval):

- **(a) Fix `_dereferenceWithTracking`** (runner.dart:4179) so the `StructTerm` branch maps `dereference` over `args` and unions their tracked readers into `unboundReaders` (cycle-safe via a visited set, like `_termsEqual`). This fixes every generic-guard consumer at once.
- **(b) Replace the per-arg deref+track loop** (runner.dart:3098-3133) with the recursive `collectUnbound` walker already proven in `GroundEqual` (runner.dart:3600-3662), so the generic path's suspend decision uses the same recursion. This also surfaces nested unbound *writers* → FAIL consistently.

PROPOSED: **(a)**, smallest blast radius, single helper, reused by `@<`/`atom`/`=?=`/all type guards. The verdict matrix after the fix: nested unbound **reader** → SUSPEND (reactivate once on bind); nested unbound **writer** → FAIL; fully ground → evaluate. Preserves cycle-safety (visited set) so a cyclic compound terminates (FR-022 tie-in).

### 3.3 Test plan

- **Section A**, `programs/tests/typed/compound_suspend.glp` (NEW): a guard (`=?=`, `@<`, `ground`) over a compound operand `f(a, X?)` where `X?` is initially unbound; assert the goal **suspends** (does not appear in results as failed), then bind `X` from a sibling and assert it **reactivates once** and yields the ground verdict. The same input shape that exercises the link-layer remote-operand case (SC-009).
- **Section A negative-control:** `f(a, W)` with `W` an unbound **writer** → FAIL (definite, SRSW).
- **Regression:** the existing `GroundEqual`-path tests (run_all_tests.sh A13) must stay green — the fix must not change the already-correct dedicated-opcode path.
- **Baseline:** full `bash test/run_all_tests.sh` green before/after (FR-067).

---

## 4. FIX — imported-reader reactivation (assignment-ingress wiring)  — status **FIX**

**Authority basis:** FR-035, SC-009, Edge "Compound / imported-reader suspension". Confirmed live (B2-B3-G §"Verification note" bullet 2).

### 4.1 The hazard (verified, exact)

There is a **live second reader representation**: `allocateImportedReader` (heap_fcp.dart:103) creates a writerless reader cell whose suspensions live in `VariableEntry.suspensions`. `suspendOnReader` routes a suspension on such a reader into `VariableEntry.suspensions` (heap_fcp.dart:493-504). Those suspensions are drained **only** by `bindImportedReader` (heap_fcp.dart:641-664, via `_walkAndActivate(entry.suspensions)` at :653-654). But the assignment ingress `handleMadAssignment` calls **only** `bindVariable` (mad_context.dart:306, :355, :402) — **never** `bindImportedReader`. Result: a guard suspended on a genuinely writerless imported reader **never reactivates** when the value arrives — a permanently un-reactivated goal (FR-051 violation) and a spec/code divergence (madGLP §11.3 "local-pairs only" vs the live `VariableEntry` path).

### 4.2 PROPOSED fix (wire the ingress; do NOT delete the path)

Per CLAUDE.md Preserve-Working-Code, the `VariableEntry`/`bindImportedReader` path is **kept**. Two complementary edits, both PROPOSED:

1. **Wire the ingress** in `mad_context.dart`'s three assignment handlers (`_handleSerializerAssignment` :306, `_handleWriterAssignment` :355, `_handleReaderAssignment` :402). At each bind site, **detect whether the target cell is an imported reader** (a `RoTag` cell whose content is a `VariableEntry`, distinguishable from a local writer) and, if so, route through `bindImportedReader(readerAddr, localizedValue, entry)` instead of `bindVariable(writerAddr, ...)`, then enqueue its returned activations (same `runtime.enqueueReactivatedGoal` loop already present). PROPOSED to introduce one `heap` helper `bindAny(addr, value)` that dispatches writer-vs-imported-reader, so the three handlers call a single seam (parallels `bindVariable`'s compatibility-wrapper role at heap_fcp.dart:671).
2. **OR, alternative ruling (D-B2-3 option):** if Gabi rules the link layer must represent every remote reader as a **local-pair writer** and never `allocateImportedReader`, then instead of (1) we **document that constraint in this contract + the spec** and add a guard/assert that the ingress never receives an imported-reader cell — making the divergence impossible by construction. (This is the "rule it off-limits in writing" arm of D-B2-3.) Either way the path is **not deleted**.

PROPOSED: **(1)** — it is the faithful fix that makes the existing tests of the imported-reader path actually exercisable across the link, and keeps `glink`'s later open-term transport (which mints sub-link readers per hop, corpus 14) sound.

### 4.3 Test plan

- **Section A**, `programs/tests/typed/imported_reader_wake.glp` + a multiagent harness goal (model on the existing `test/multiagent/` isolate tests): a guard (`ground`/`=?=`) suspends on a reader allocated via `allocateImportedReader`; deliver the corresponding assignment through `handleMadAssignment`; assert the suspended goal **reactivates exactly once** and the value is observed (FR-051). Re-deliver the same assignment and assert **no double-reactivation** (ties to FR-021 idempotency, owned by the reliability facet but the wake-once property is asserted here).
- **Regression:** the local-pair (non-imported) ingress path must be unchanged — existing multiagent split tests stay green.
- This test is the precondition for the headline SC-001 split working when a remote reader is represented via the imported-reader path.

---

## 5. DECLINE — `==` `\==` `\=` `reader/1`  — status **DECLINE**

**Authority basis:** FR-036 + RULED "G" decision. These are Tier-3 Prolog/ISO/FCP idioms deliberately absent from the GLP kernel; declined under explicit ruling.

| Declined | Rationale (verbatim contract intent) | Canonical GLP form |
|---|---|---|
| `==` (term identity) | redundant alias of `=?=` over ground terms (guards-reference §"X =?= Y"; B2-B3-G §G table — Tier-3, not GLP kernel) | `X =?= Y` |
| `\==` | redundant alias of `~(=?=)` (guards-reference §"Guard Negation" table includes `~(X =?= Y)`) | `~(X =?= Y)` |
| `\=` (structural disequality) | **declined**; GLP deliberately removed `\=`; ill-defined patiently over partial terms (B2-B3-G §G table) | `~(X =?= Y)` |
| `reader/1` | **non-monotonic** (its truth flips as the store grows — succeeds on an unbound reader, then a later bind makes it false) and therefore **unsound across a link** (a withheld/late remote bind makes a committed verdict wrong); violates the FR-039 monotone-commit invariant | (none — do not introduce) |

**Edit sites:** none added. Active guarantee: these four names MUST NOT be added to `_negatableGuards`/`_nonNegatableGuards`/`comparisonOps` (analyzer.dart:606-623,727), `builtinProcedures`/`predefinedProcedureNames` (prelude.dart), or the runner switch (runner.dart:4330). If a program writes them, the existing "Cannot call X in guard position / Unknown guard predicate" errors are the correct, intended outcome.

**Test plan (negative):** Section C — `decline_guards_bad.glp` (NEW): a clause using `==`, one using `\==`, one using `\=`, one using `reader(X?)` in guard position — each MUST be **rejected at compile time** (analyzer/PE error), documenting the decline as enforced, not merely unimplemented.

---

## 6. UNTOUCHED — `=\=`  — status **(no change; active guarantee)**

**Authority basis:** FR-038, SC-017. `=\=` (arithmetic disequality) is load-bearing and MUST remain exactly as implemented.

- **Live sites preserved:** runner arm `case '=\\=':` (runner.dart:4387-4394); analyzer `comparisonOps` + `_nonNegatableGuards` (analyzer.dart:618,727); registration `=\=/2` (prelude.dart:58,107).
- **Note (correctness, not a change request):** the SC-017 phrasing "the `=\=`-gated division/mod in `self.glp` still loads" — the live `programs/self.glp` does **not** currently contain a `=\=`-gated `:=` division/mod clause (the `:=` arithmetic clauses at self.glp:103-114 are addition etc.; no `=\=` occurrence found in `programs/`). **OQ-3:** confirm whether the SC-017 guarantee targets the *sibling* GLP prelude (which may gate division on `=\=`) or a planned glpnet prelude addition. Regardless, the active guarantee here is: **do not remove or alter the `=\=` guard machinery**, so any `=\=`-gated prelude (current sibling or future glpnet) keeps loading. No edit.
- **Test plan:** the baseline `bash test/run_all_tests.sh` (which loads `self.glp`) green before/after every core-touching change is the standing assertion; if a `=\=`-gated clause is added to the glpnet prelude, add a load-and-run regression to Section A.

---

## 7. CROSS-CUT — FR-039 three-valued + monotone-commit conformance

**Authority basis:** FR-039, SC-004, SC-006, SC-009, FR-067/SC-017.

Every NEW/FIX guard in §1-§4 MUST demonstrate, as **runtime (Section A) AND type-check (Section B positive / Section C negative)** tests, the full three behavioral cases:

1. **succeed** on bound-and-satisfied operands;
2. **suspend** on an unbound reader (top-level **and** nested-in-compound, **and** via the imported-reader path) then **reactivate exactly once** on bind;
3. **fail** on an unbound writer (or definite mismatch).

**Non-monotone gating (the sharp FR-039 clause).** `~(=?=)`, any `~G` negation, and `otherwise` must be gated **fully-known-across-the-link** before commit. Concretely for this facet:
- `~(=?=)` over a remote/compound operand: the operand must be **ground** (every embedded reader arrived) before the negation commits — otherwise a late remote bind could flip the committed verdict. The §3 compound-recursion fix is what makes "ground across the link" actually checked (without it, `~(=?=)` over a compound with an un-arrived inner reader wrongly commits). 
- `otherwise`: documented hazard (B2-B3-G §"Interaction with distributed suspension") — a withheld remote bind keeps a sibling suspended-not-failed, so `otherwise` never fires (a stealth deadlock / attacker-controllable lever, Edge "Byzantine peer"). PROPOSED: no semantic change to `otherwise` here (it stays "all prior clauses *definitively failed*", guards-reference §otherwise); the link layer's monitor-stream fault model (FR-043..FR-046) is what lets a program react to non-arrival rather than relying on `otherwise` firing. Flagged as OQ-4 for the failure-model facet.

**Baseline regression gate (FR-067/SC-017):** `bash test/run_all_tests.sh` MUST be green before AND after every change in §1-§4 (all touch core: parser, runner `_evaluateGuard`, SRSW analyzer, heap/ingress). No core-touching change merges over a red baseline.

---

## 8. Summary of edit sites (consolidated)

| Facet | File:line | Nature |
|---|---|---|
| `@<` family lexer | `compiler/lexer.dart:61` | extend `@` case w/ lookahead |
| `@<` family tokens | `compiler/token.dart:~55` | 4 new token types |
| `@<` family parser | `compiler/parser.dart:687-690` (+:1142-1182 if needed) | infix-detect 4 tokens |
| `@<` family runner | `bytecode/runner.dart:4394` (new arms) + new `_compareTerms` near `_termsEqual` :4699 | 4 guard arms + comparator |
| `@<` family SRSW | `compiler/analyzer.dart:727` (ground-implying), `:616` (non-negatable) | set membership |
| `@<` family registration | `analysis/type_checker/prelude.dart:33,82` | `@<`..`@>=` (+/2) |
| `atom/1` runner | `bytecode/runner.dart:~4416` (new arm) | 1 guard arm |
| `atom/1` registration | `analysis/type_checker/prelude.dart:33,82` | `atom`(/1) |
| compound-suspend fix | `bytecode/runner.dart:4179-4182` (or :3098-3133) | recurse into compound args |
| imported-reader ingress | `multiagent/mad_context.dart:306,355,402` (+ heap_fcp.dart `bindAny` seam) | dispatch writer-vs-imported-reader |
| declines | (none) | enforce absence |
| `=\=` | (none) | preserve |

All edits in this table are **core / core-adjacent (parser, runner guard evaluator, SRSW analyzer, heap ingress)** and per FR-039/FR-067 are made **only under explicit language-authority approval**, keeping the baseline REPL suite green.

## 9. Consolidation into `docs/guards-reference.md` (FR-032 deliverable)

On approval, fold the deltas into the authoritative reference — do not keep this as a second spec:
- **Add** an "@< / @> / @=< / @>= (standard-order term ordering)" subsection under "Comparison Guards", with the three-valued truth table and the ground-implying note added to the "Guards That Imply Groundness" table.
- **Add** an `atom(X?)` subsection under "Type Guards" (clarifying its relationship to `string/1` per OQ-2) and add `atom/1` to the negatable + ground-implying lists.
- **Annotate** the `=?=` / `~(=?=)` entries that `==`/`\==`/`\=`/`reader/1` are **declined** with the canonical forms (a short "Declined guards" note), so future readers do not re-propose them.
- **No change** to the `=\=` entry.
- The existing "Implementation Checklist" already lists the runner/analyzer/parser steps; reference it rather than duplicating.

---

## Open questions (carried to the co-design gate)

- **OQ-1.** `@<` family negatable or non-negatable? PROPOSED non-negatable (natural complement `@>=`); needs Gabi's ruling, affects analyzer set membership.
- **OQ-2.** Is `atom/1` an exact synonym of the runtime `string/1` test (non-numeric string constant, excludes `[]`/nil), or should `atom` also accept `[]`? The corpus does not settle it.
- **OQ-3.** SC-017's "`=\=`-gated division/mod in `self.glp` still loads" — does it target the sibling prelude or a planned glpnet prelude clause? (No `=\=` occurrence exists in `programs/` today.)
- **OQ-4.** `otherwise` across a link can never fire on a withheld remote bind (stealth deadlock / Byzantine lever). Confirm the failure-model facet (monitor stream) is the intended escape, with no `otherwise` semantic change here.
- **OQ-5.** Exact total order for `@<` (the PROPOSED Number<String<compound, then arity/functor/args) — confirm it matches the intended GLP standard order and is stable across the Dart↔C# wire (FR-060).

## Risks

- **Multi-site core edit for `@<`** (lexer + token + parser + runner + analyzer + prelude) is the highest-blast-radius change; lexer `@` disambiguation must not break the `Goal@Agent` isolate-spawn operator (token.dart:55).
- **Compound-suspend fix changes a hot path** (`_dereferenceWithTracking`, every generic guard); a regression here silently turns suspends into fails (the very bug) or vice-versa — guard with the full baseline suite + the new compound/imported tests.
- **Imported-reader ingress** touches the agent runtime / event loop (B2-B3-G "zero core change is partly false"); the alternative D-B2-3 ruling (off-limits-by-construction) changes the fix entirely — needs Gabi's decision before implementation.
- **Cross-runtime parity** for `@<` (standard-order comparator) and the cycle-safe term comparator is a parser-differential risk class on the C# reference (B2-B3-G open risks); the comparator must be byte/behaviour-identical Dart↔C#.
- **`atom` semantics divergence** (OQ-2) — shipping `atom` ≠ `string` without confirming intent would re-introduce an analyzer↔runtime mismatch of a different shape.

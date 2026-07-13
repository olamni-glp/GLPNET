/-
  PI:14 — writer-MGU binds only writers (feature 050, T027).

  Mechanized half of proof obligation PI:14 (contracts/proof-obligations.md, gates M1):

    "the Gleam engine's unification binds only writers — never readers, never
     writer↔writer — under the immutable-heap/value-copy model, for all three-phase
     execution paths (tentative HEAD unification included)."

  The model abstracts `glp_gleam/src/glp/runtime/heap.gleam` (immutable threaded
  binding store) and `glp_gleam/src/glp/runtime/unify.gleam` (three-valued writer-MGU
  unification).  Each definition carries a comment mapping it to the Gleam
  function/case it abstracts.  Core Lean only (no mathlib), sorry-free, checked by a
  single `lake build` — repo convention per csharp/glp_result_codec/lean/ResultTermRoundTrip.

  Modelling decisions (see PROOF.md §Scope for the full honesty list):
  * The heap dict is split: UNBOUND cells (WriterCell / ReaderCell) are represented by
    ABSENCE from the store; the store holds exactly the BOUND cells (ValueCell /
    WriterBound).  "Binds only writers" is then: the store's domain only ever contains
    writer addresses.
  * The FCP writer/reader pairing (WriterCell(reader_addr) / ReaderCell(writer_addr),
    heap.gleam allocate_variable) is modelled by a shared index: reader `Addr.r n` is
    paired with writer `Addr.w n`.  Roles come from the constructor TAG, mirroring
    FR-002 (role read from the cell tag, never address arithmetic).
  * Deref path compression (heap.gleam `compress`) is omitted: it retargets ReaderCells
    but never changes a cell's tag and never adds a binding — internal layout, excluded
    from parity (heap.gleam FR-009).
  * Fuel bounds recursion.  GLP has no occurs-check (unify.gleam R-008), so unification
    on cyclic heap shapes may diverge; fuel exhaustion yields the loud `.error` verdict
    and — crucially — no binding, which is exactly the claim's concern.
-/

namespace WriterMguBindsOnlyWriters

/-- Heap addresses with the FCP role tag baked in (heap.gleam `CellTag` WrtTag/RoTag;
    a `VarRef`'s role is read from the heap tag at its address — terms.gleam header,
    FR-002).  `w n` is a writer cell, `r n` its paired reader cell
    (heap.gleam `allocate_variable`: writer↔reader mutual pointers). -/
inductive Addr where
  | w : Nat → Addr
  | r : Nat → Addr
deriving DecidableEq, Repr

/-- Terms (terms.gleam `Term`).  Constants are abstracted to `Nat` (the four
    `Constant` kinds all carry decidable structural equality — terms.gleam FR-001);
    functors likewise.  `wref`/`rref` are `VarRef(addr)` whose heap cell is
    writer- or reader-tagged: the tag on the model reference stands for the
    `heap.is_writer h addr` lookup in unify.gleam `resolve` (lines 60-63). -/
inductive Term where
  | const  : Nat → Term
  | struct : Nat → List Term → Term
  | wref   : Nat → Term
  | rref   : Nat → Term
deriving Repr

/-- Ground-headed value test: `resolve`'s `RValue` only ever holds a const or struct
    ("A ground value (const or struct; struct args may still hold VarRefs)" —
    unify.gleam `Resolved.RValue`).  A bare variable reference is NOT a value. -/
def Term.isValue : Term → Bool
  | .const _    => true
  | .struct _ _ => true
  | .wref _     => false
  | .rref _     => false

/-- A bound cell's content (heap.gleam `Cell`, bound half):
    `toValue t` = `ValueCell(term)` — written only by `bind_writer`;
    `toVar a`   = `WriterBound(target)` — written only by `bind_writer_to_var`. -/
inductive Binding where
  | toValue : Term → Binding
  | toVar   : Addr → Binding
deriving Repr

abbrev Entry := Addr × Binding

/-- The immutable binding store (heap.gleam `Heap` cells dict, bound cells only).
    Extension is pure cons — every mutating op returns a new store (R-001). -/
abbrev Store := List Entry

/-- The writer-only invariant, per entry:
    * a READER address in the binding domain is forbidden (heap.gleam never replaces a
      ReaderCell: `bind_writer`/`bind_writer_to_var` return `NotAWriter` on a reader,
      heap.gleam:213,242);
    * a writer bound to a bare variable reference is forbidden (`ValueCell` payloads in
      unify-driven flows are ground-headed `RValue`s);
    * a writer→writer `WriterBound` link is forbidden (the WxW rejection:
      unify.gleam:89 and heap.gleam:236-237). -/
def entryOk : Entry → Prop
  | (.w _, .toValue v)    => v.isValue = true
  | (.w _, .toVar (.r _)) => True
  | (.w _, .toVar (.w _)) => False
  | (.r _, _)             => False

/-- A store is writer-only when every bound cell satisfies `entryOk`. -/
def WriterOnly (s : Store) : Prop := ∀ e ∈ s, entryOk e

theorem writerOnly_append {d s : Store}
    (hd : ∀ e ∈ d, entryOk e) (hs : WriterOnly s) : WriterOnly (d ++ s) := by
  intro e he
  rcases List.mem_append.mp he with h | h
  · exact hd e h
  · exact hs e h

theorem writerOnly_suffix {d s : Store} (h : WriterOnly (d ++ s)) : WriterOnly s :=
  fun e he => h e (List.mem_append.mpr (Or.inr he))

/-- Cell fetch (heap.gleam `cell_at` / `dict.get`), restricted to bound cells.
    `none` = the cell is an unbound WriterCell / ReaderCell. -/
def lookup : Store → Addr → Option Binding
  | [], _ => none
  | (a, b) :: rest, x => if a = x then some b else lookup rest x

theorem lookup_mem {s : Store} {a : Addr} {b : Binding}
    (h : lookup s a = some b) : (a, b) ∈ s := by
  induction s with
  | nil => simp [lookup] at h
  | cons e rest ih =>
      obtain ⟨ea, eb⟩ := e
      by_cases hax : ea = a
      · subst hax
        simp [lookup] at h
        subst h
        exact List.mem_cons_self ..
      · simp [lookup, hax] at h
        exact List.mem_cons_of_mem _ (ih h)

/-- Deref result (heap.gleam `DerefResult`): `value t` = `Bound(term)`,
    `unbound w` = `Unbound(writer)`, `stuck` = the loud `HeapError` channel
    (Cycle / WriterToWriter during traversal, heap.gleam FR-003/FR-004) — kept
    distinct from an ordinary unify Fail (R-005). -/
inductive Deref where
  | value   : Term → Deref
  | unbound : Nat → Deref
  | stuck   : Deref
deriving Repr

/-- Dereference starting at writer `w` (heap.gleam `deref` / `deref_walk`):
    * no binding            → `Unbound(w)`      (WriterCell arm, heap.gleam:176-177)
    * `toValue v`           → `Bound(v)`        (ValueCell arm, heap.gleam:178)
    * `toVar (.r n)`, n = w → `Unbound(w)`      (the self-bind→Unbound recognizer:
                                                 WriterBound to its OWN paired reader,
                                                 heap.gleam:165-173)
    * `toVar (.r n)`, n ≠ w → continue at paired writer `n` (WriterBound → ReaderCell →
                                                 writer_addr hop, heap.gleam:156-164,174)
    * `toVar (.w _)`        → `stuck`           (WxW during traversal, heap.gleam:151-153)
    Fuel exhaustion = the visited-set Cycle error (heap.gleam:146-147). -/
def derefW (s : Store) : Nat → Nat → Deref
  | 0, _ => .stuck
  | fuel + 1, w =>
      match lookup s (.w w) with
      | none                 => .unbound w
      | some (.toValue v)    => .value v
      | some (.toVar (.r n)) => if n = w then .unbound w else derefW s fuel n
      | some (.toVar (.w _)) => .stuck

/-- One side after dereferencing (unify.gleam `Resolved`):
    `value` = `RValue`, `writer w` = `RWriter(terminal writer)`,
    `reader n w` = `RReader(original_reader_addr, terminal_paired_writer)`. -/
inductive Resolved where
  | value  : Term → Resolved
  | writer : Nat → Resolved
  | reader : Nat → Nat → Resolved
  | stuck  : Resolved
deriving Repr

/-- unify.gleam `resolve`: a non-var term is `RValue`; a `VarRef` is dereffed and its
    role read from the ORIGINAL address's tag (here: the `wref`/`rref` constructor),
    not from the deref terminal. -/
def resolve (s : Store) (fuel : Nat) : Term → Resolved
  | .const c     => .value (.const c)
  | .struct f as => .value (.struct f as)
  | .wref n =>
      match derefW s fuel n with
      | .value v   => .value v
      | .unbound w => .writer w
      | .stuck     => .stuck
  | .rref n =>
      -- ReaderCell(writer_addr) hop first (deref of a reader starts at its paired
      -- writer), then the role of the ORIGINAL address keeps it an RReader.
      match derefW s fuel n with
      | .value v   => .value v
      | .unbound w => .reader n w
      | .stuck     => .stuck

/-- Three-valued unification outcome (unify.gleam `UnifyOutcome`) plus the loud
    `HeapError` channel (`Result(UnifyOutcome, HeapError)` collapsed into one type).
    `success added`: the ADDED bindings, newest first — the post-store is
    `added ++ pre` (the Gleam threads a whole new immutable heap; the model carries
    the extension explicitly so "every binding ADDED" is directly statable).
    `suspend w`: `Suspend(heap, on)` — `on` = the reader's paired writer; the heap is
    unchanged (no store payload needed without path compression).
    `fail`: ordinary mismatch, kept distinct from `error` (R-005). -/
inductive Outcome where
  | success : Store → Outcome
  | suspend : Nat → Outcome
  | fail    : Outcome
  | error   : Outcome
deriving Repr

/-- heap.gleam `bind_writer`: bind an unbound writer to a ground value → ValueCell.
    An already-bound writer (ValueCell / WriterBound — including a self-bound writer,
    which derefs Unbound but is NOT re-bindable) is `AlreadyBound` → `.error`
    (heap.gleam:208-214). -/
def bindWriterValue (s : Store) (w : Nat) (v : Term) : Outcome :=
  match lookup s (.w w) with
  | some _ => .error
  | none   => .success [(.w w, .toValue v)]

/-- heap.gleam `bind_writer_to_var`: bind an unbound writer onward to another
    variable's READER → WriterBound chain.  A writer-tagged target is the WxW
    violation (heap.gleam:236-237) → `.error`, never a binding.  An already-bound
    source writer is `AlreadyBound` → `.error` (heap.gleam:240-241).
    (Suspension forwarding, heap.gleam `forward_to_terminal`, mutates only the
    suspension list of an UNBOUND WriterCell — never a binding — and is omitted;
    activation lists are the F5 caller's concern, unify.gleam:99-101.) -/
def bindWriterToVar (s : Store) (w : Nat) (target : Addr) : Outcome :=
  match target with
  | .w _  => .error
  | .r n  =>
      match lookup s (.w w) with
      | some _ => .error
      | none   => .success [(.w w, .toVar (.r n))]

/- The unify family mirrors unify.gleam's three mutually recursive functions:
   `unify` (resolve both sides + `unify_resolved` dispatch), `unifyValues`
   (`unify_values`: structural unification of ground-headed values), `unifyArgs`
   (`unify_args`: pairwise argument unification threading the extended store;
   first non-success short-circuits).  Fuel decreases on every hop. -/
mutual

/-- unify.gleam `unify` + `unify_resolved` — the writer-MGU case analysis:
    * value×value    → structural unification            (unify.gleam:76)
    * writer×value   → bind the writer                   (unify.gleam:79-80, bind_writer)
    * writer×reader  → writer→var binding                (unify.gleam:83-86, bind_writer_to_var)
    * writer×writer  → WxW reported loudly, NEVER bound  (unify.gleam:89, SC-004)
    * reader×value / reader×reader → Suspend, never Fail (unify.gleam:93-95)
    * any stuck resolve → the HeapError channel. -/
def unify (s : Store) : Nat → Term → Term → Outcome
  | 0, _, _ => .error
  | fuel + 1, a, b =>
      match resolve s (fuel + 1) a, resolve s (fuel + 1) b with
      | .stuck, _  => .error
      | _, .stuck  => .error
      | .value va, .value vb    => unifyValues s fuel va vb
      | .writer wa, .value vb   => bindWriterValue s wa vb
      | .value va, .writer wb   => bindWriterValue s wb va
      | .writer wa, .reader rb _ => bindWriterToVar s wa (.r rb)
      | .reader ra _, .writer wb => bindWriterToVar s wb (.r ra)
      | .writer _, .writer _    => .error
      | .reader _ wa, .value _  => .suspend wa
      | .value _, .reader _ wb  => .suspend wb
      | .reader _ wa, .reader _ _ => .suspend wa

/-- unify.gleam `unify_values`: constants by structural equality (Fail on mismatch);
    structs need functor + arity agreement upfront, then pairwise args; everything
    else is an ordinary mismatch Fail. -/
def unifyValues (s : Store) : Nat → Term → Term → Outcome
  | 0, _, _ => .error
  | fuel + 1, a, b =>
      match a, b with
      | .const ca, .const cb =>
          if ca = cb then .success [] else .fail
      | .struct fa as, .struct fb bs =>
          if fa = fb then
            if as.length = bs.length then unifyArgs s fuel as bs else .fail
          else .fail
      | _, _ => .fail

/-- unify.gleam `unify_args`: pairwise, threading the extended store (`d1 ++ s`) into
    the tail exactly as the Gleam threads the returned heap; the first non-Success
    short-circuits.  Accumulated additions compose as `d2 ++ d1`. -/
def unifyArgs (s : Store) : Nat → List Term → List Term → Outcome
  | 0, _, _ => .error
  | _ + 1, [], [] => .success []
  | fuel + 1, x :: xr, y :: yr =>
      match unify s fuel x y with
      | .success d1 =>
          match unifyArgs (d1 ++ s) fuel xr yr with
          | .success d2 => .success (d2 ++ d1)
          | o => o
      | o => o
  | _ + 1, _, _ => .fail

end

-- ── Sanity checks: the model is not vacuous ────────────────────────────────────────
-- (every verdict of the three-valued outcome + the error channel is inhabited, and
-- the bindings produced are exactly the writer-keyed ones)

/-- writer × constant binds the writer (bind_writer path). -/
example : unify [] 9 (.wref 0) (.const 42)
    = .success [(.w 0, .toValue (.const 42))] := rfl

/-- writer × unbound reader links writer→reader (bind_writer_to_var path). -/
example : unify [] 9 (.wref 0) (.rref 1)
    = .success [(.w 0, .toVar (.r 1))] := rfl

/-- writer × writer is rejected loudly — no binding (unify.gleam:89). -/
example : unify [] 9 (.wref 0) (.wref 1) = .error := rfl

/-- unbound reader × value SUSPENDS (never fails) on the paired writer. -/
example : unify [] 9 (.rref 0) (.const 1) = .suspend 0 := rfl

/-- constant mismatch is an ordinary Fail. -/
example : unify [] 9 (.const 1) (.const 2) = .fail := rfl

/-- structure decomposition reaches the writer through the args. -/
example : unify [] 9 (.struct 7 [.wref 0]) (.struct 7 [.const 5])
    = .success [(.w 0, .toValue (.const 5))] := rfl

/-- the self-bind→Unbound recognizer (heap.gleam:165-173): a writer bound to its own
    paired reader derefs Unbound... -/
example : derefW [(.w 0, .toVar (.r 0))] 9 0 = .unbound 0 := rfl

/-- ...but re-binding it is AlreadyBound (heap.gleam:212), not a second binding. -/
example : unify [(.w 0, .toVar (.r 0))] 9 (.wref 0) (.const 5) = .error := rfl

/-- the invariant is falsifiable: a reader in the binding domain violates it. -/
example : ¬ WriterOnly [(.r 0, .toValue (.const 1))] := by
  intro h
  exact h (.r 0, .toValue (.const 1)) (List.mem_cons_self ..)

/-- ...as does a writer→writer link. -/
example : ¬ WriterOnly [(.w 0, .toVar (.w 1))] := by
  intro h
  exact h (.w 0, .toVar (.w 1)) (List.mem_cons_self ..)

-- ── Supporting lemmas ──────────────────────────────────────────────────────────────

/-- Under the invariant, a deref that lands on a value yields a ground-headed value:
    `Bound(term)` only ever comes out of a `ValueCell`, whose payload `entryOk`
    constrains. -/
theorem derefW_value_isValue {s : Store} (hs : WriterOnly s) :
    ∀ (fuel w : Nat) (v : Term), derefW s fuel w = .value v → v.isValue = true := by
  intro fuel
  induction fuel with
  | zero => intro w v h; simp [derefW] at h
  | succ fuel ih =>
      intro w v h
      simp only [derefW] at h
      cases hlk : lookup s (.w w) with
      | none => rw [hlk] at h; cases h
      | some b =>
          rw [hlk] at h
          cases b with
          | toValue t =>
              cases h
              have := hs _ (lookup_mem hlk)
              simpa [entryOk] using this
          | toVar a =>
              cases a with
              | w _ => cases h
              | r n =>
                  by_cases hn : n = w
                  · simp [hn] at h
                  · simp only [hn, if_false] at h
                    exact ih n v h

/-- Under the invariant, `resolve` classifies as `RValue` only ground-headed values. -/
theorem resolve_value_isValue {s : Store} (hs : WriterOnly s) {fuel : Nat} :
    ∀ {t v : Term}, resolve s fuel t = .value v → v.isValue = true := by
  intro t v h
  cases t with
  | const c => cases h; rfl
  | struct f as => cases h; rfl
  | wref n =>
      simp only [resolve] at h
      cases hd : derefW s fuel n with
      | value t' => rw [hd] at h; cases h; exact derefW_value_isValue hs fuel n _ hd
      | unbound w => rw [hd] at h; cases h
      | stuck => rw [hd] at h; cases h
  | rref n =>
      simp only [resolve] at h
      cases hd : derefW s fuel n with
      | value t' => rw [hd] at h; cases h; exact derefW_value_isValue hs fuel n _ hd
      | unbound w => rw [hd] at h; cases h
      | stuck => rw [hd] at h; cases h

/-- Every binding `bind_writer` adds is writer-keyed with a ground-headed payload. -/
theorem bindWriterValue_ok {s : Store} {w : Nat} {v : Term} {d : Store}
    (hv : v.isValue = true) (h : bindWriterValue s w v = .success d) :
    ∀ e ∈ d, entryOk e := by
  unfold bindWriterValue at h
  cases hlk : lookup s (.w w) with
  | some _ => rw [hlk] at h; cases h
  | none =>
      rw [hlk] at h
      cases h
      intro e he
      rcases List.mem_singleton.mp he with rfl
      simpa [entryOk] using hv

/-- Every binding `bind_writer_to_var` adds is a writer-keyed writer→READER link
    (a writer-tagged target never gets past the WxW guard). -/
theorem bindWriterToVar_ok {s : Store} {w : Nat} {a : Addr} {d : Store}
    (h : bindWriterToVar s w a = .success d) : ∀ e ∈ d, entryOk e := by
  unfold bindWriterToVar at h
  cases a with
  | w _ => cases h
  | r n =>
      cases hlk : lookup s (.w w) with
      | some _ => rw [hlk] at h; cases h
      | none =>
          rw [hlk] at h
          cases h
          intro e he
          rcases List.mem_singleton.mp he with rfl
          simp [entryOk]

-- ── The invariant theorem, jointly over the unify family ──────────────────────────

/-- Joint induction on fuel over the three mutually recursive functions: every
    successful step adds only `entryOk` bindings. -/
theorem unify_family_ok :
    ∀ fuel : Nat,
      (∀ (s : Store) (a b : Term) (d : Store), WriterOnly s →
          unify s fuel a b = .success d → ∀ e ∈ d, entryOk e)
      ∧ (∀ (s : Store) (a b : Term) (d : Store), WriterOnly s →
          unifyValues s fuel a b = .success d → ∀ e ∈ d, entryOk e)
      ∧ (∀ (s : Store) (xs ys : List Term) (d : Store), WriterOnly s →
          unifyArgs s fuel xs ys = .success d → ∀ e ∈ d, entryOk e) := by
  intro fuel
  induction fuel with
  | zero =>
      refine ⟨?_, ?_, ?_⟩
      · intro s a b d _ h; simp [unify] at h
      · intro s a b d _ h; simp [unifyValues] at h
      · intro s xs ys d _ h; simp [unifyArgs] at h
  | succ fuel ih =>
      obtain ⟨ihU, ihV, ihA⟩ := ih
      refine ⟨?_, ?_, ?_⟩
      -- unify: the writer-MGU dispatch (unify_resolved)
      · intro s a b d hs h
        simp only [unify] at h
        cases hra : resolve s (fuel + 1) a with
        | stuck => rw [hra] at h; cases h
        | value va =>
            rw [hra] at h
            cases hrb : resolve s (fuel + 1) b with
            | stuck => rw [hrb] at h; cases h
            | value vb => rw [hrb] at h; exact ihV s va vb d hs h
            | writer wb =>
                rw [hrb] at h
                exact bindWriterValue_ok (resolve_value_isValue hs hra) h
            | reader rb wb => rw [hrb] at h; cases h
        | writer wa =>
            rw [hra] at h
            cases hrb : resolve s (fuel + 1) b with
            | stuck => rw [hrb] at h; cases h
            | value vb =>
                rw [hrb] at h
                exact bindWriterValue_ok (resolve_value_isValue hs hrb) h
            | writer wb => rw [hrb] at h; cases h
            | reader rb wb => rw [hrb] at h; exact bindWriterToVar_ok h
        | reader ra wa =>
            rw [hra] at h
            cases hrb : resolve s (fuel + 1) b with
            | stuck => rw [hrb] at h; cases h
            | value vb => rw [hrb] at h; cases h
            | writer wb => rw [hrb] at h; exact bindWriterToVar_ok h
            | reader rb wb => rw [hrb] at h; cases h
      -- unifyValues: structural decomposition
      · intro s a b d hs h
        simp only [unifyValues] at h
        cases a with
        | const ca =>
            cases b with
            | const cb =>
                by_cases hc : ca = cb
                · simp only [hc, if_true] at h
                  cases h
                  intro e he; cases he
                · simp [hc] at h
            | struct fb bs => cases h
            | wref _ => cases h
            | rref _ => cases h
        | struct fa as =>
            cases b with
            | const cb => cases h
            | struct fb bs =>
                by_cases hf : fa = fb
                · by_cases hl : as.length = bs.length
                  · simp only [hf, hl, if_true] at h
                    exact ihA s as bs d hs h
                  · simp [hf, hl] at h
                · simp [hf] at h
            | wref _ => cases h
            | rref _ => cases h
        | wref _ => cases h
        | rref _ => cases h
      -- unifyArgs: pairwise threading of the extended store
      · intro s xs ys d hs h
        cases xs with
        | nil =>
            cases ys with
            | nil =>
                simp only [unifyArgs] at h
                cases h
                intro e he; cases he
            | cons y yr => simp [unifyArgs] at h
        | cons x xr =>
            cases ys with
            | nil => simp [unifyArgs] at h
            | cons y yr =>
                simp only [unifyArgs] at h
                cases h1 : unify s fuel x y with
                | success d1 =>
                    simp only [h1] at h
                    have hd1 : ∀ e ∈ d1, entryOk e := ihU s x y d1 hs h1
                    have hs1 : WriterOnly (d1 ++ s) := writerOnly_append hd1 hs
                    cases h2 : unifyArgs (d1 ++ s) fuel xr yr with
                    | success d2 =>
                        rw [h2] at h
                        cases h
                        have hd2 : ∀ e ∈ d2, entryOk e := ihA (d1 ++ s) xr yr d2 hs1 h2
                        intro e he
                        rcases List.mem_append.mp he with h' | h'
                        · exact hd2 e h'
                        · exact hd1 e h'
                    | suspend w => rw [h2] at h; cases h
                    | fail => rw [h2] at h; cases h
                    | error => rw [h2] at h; cases h
                | suspend w => rw [h1] at h; cases h
                | fail => rw [h1] at h; cases h
                | error => rw [h1] at h; cases h

-- ── PI:14 main theorem ─────────────────────────────────────────────────────────────

/-- **PI:14 — writer-MGU binds only writers.**  For every successful unification step
    out of a writer-only store: every binding ADDED is keyed by a WRITER address and
    is either a ground-headed value binding or a writer→READER link (never a reader
    key, never a writer↔writer link); and the writer-only invariant is preserved on
    the post-store `added ++ s`. -/
theorem writer_mgu_binds_only_writers
    (s : Store) (fuel : Nat) (a b : Term) (added : Store)
    (hs : WriterOnly s) (h : unify s fuel a b = .success added) :
    (∀ e ∈ added, entryOk e) ∧ WriterOnly (added ++ s) := by
  have hd := (unify_family_ok fuel).1 s a b added hs h
  exact ⟨hd, writerOnly_append hd hs⟩

/-- Corollary: a READER address never enters the binding domain. -/
theorem readers_never_bound
    (s : Store) (fuel : Nat) (a b : Term) (added : Store) (n : Nat) (bnd : Binding)
    (hs : WriterOnly s) (h : unify s fuel a b = .success added) :
    (Addr.r n, bnd) ∉ added := by
  intro hmem
  exact (writer_mgu_binds_only_writers s fuel a b added hs h).1 _ hmem

/-- Corollary: no added binding links a writer onward to a WRITER
    (writer↔writer is rejected loudly — `.error` — never bound). -/
theorem no_writer_to_writer_binding
    (s : Store) (fuel : Nat) (a b : Term) (added : Store) (m k : Nat)
    (hs : WriterOnly s) (h : unify s fuel a b = .success added) :
    (Addr.w m, Binding.toVar (Addr.w k)) ∉ added := by
  intro hmem
  exact (writer_mgu_binds_only_writers s fuel a b added hs h).1 _ hmem

/-- Corollary: no added binding maps a writer to a BARE variable reference — value
    bindings are always ground-headed (const/struct). -/
theorem writer_value_binding_never_bare_var
    (s : Store) (fuel : Nat) (a b : Term) (added : Store) (m : Nat) (v : Term)
    (hs : WriterOnly s) (h : unify s fuel a b = .success added)
    (hmem : (Addr.w m, Binding.toValue v) ∈ added) : v.isValue = true := by
  have := (writer_mgu_binds_only_writers s fuel a b added hs h).1 _ hmem
  simpa [entryOk] using this

-- ── Tentative HEAD unification (three-phase execution) ─────────────────────────────

/-- The HEAD-phase tentative state: `buf` is σ̂w — the buffered tentative binding set
    HEAD unification accumulates (opcodes.gleam §6.2: writer occurrences "tentatively
    bind in σ̂w"); `base` is the committed heap.  HEAD goals unify against the combined
    `view`; GUARDs are pure; clause commit atomically adopts the buffer (BODY commits),
    clause abandonment discards it.  Because the store is an immutable value, the
    buffer IS the cons-extension prefix — commit is adoption of the extended value,
    discard is keeping the base (heap.gleam R-001: every mutating op returns a NEW
    heap; the old one is untouched). -/
structure Tentative where
  buf  : Store
  base : Store

/-- What HEAD unification sees: tentative bindings extend the committed heap. -/
def Tentative.view (t : Tentative) : Store := t.buf ++ t.base

/-- Atomic clause commit: the buffered σ̂w becomes part of the committed heap in one
    step (no partial interleaving is representable — the value is adopted whole). -/
def Tentative.commit (t : Tentative) : Store := t.buf ++ t.base

/-- Clause abandonment: the buffer is dropped; the committed heap is untouched. -/
def Tentative.discard (t : Tentative) : Store := t.base

/-- One tentative HEAD unification step: run the SAME `unify` (there is no second
    unification algorithm — the HEAD phase differs only in WHERE the additions land)
    against the combined view; successful additions extend the BUFFER only.
    Suspend/fail/error commit nothing through this path. -/
def headUnifyStep (t : Tentative) (fuel : Nat) (a b : Term) : Option Tentative :=
  match unify t.view fuel a b with
  | .success added => some { buf := added ++ t.buf, base := t.base }
  | _ => none

/-- **PI:14, tentative-HEAD path.**  A HEAD-phase tentative unification step out of a
    writer-only view keeps the view writer-only, keeps the ATOMIC COMMIT writer-only,
    and never touches the committed base (so discard trivially preserves the
    invariant too). -/
theorem head_unify_step_preserves_writer_only
    (t t' : Tentative) (fuel : Nat) (a b : Term)
    (hs : WriterOnly t.view) (h : headUnifyStep t fuel a b = some t') :
    WriterOnly t'.view ∧ WriterOnly t'.commit ∧ t'.base = t.base := by
  unfold headUnifyStep at h
  cases hu : unify t.view fuel a b with
  | success added =>
      rw [hu] at h
      cases h
      have hview : WriterOnly ((added ++ t.buf) ++ t.base) := by
        have := (writer_mgu_binds_only_writers t.view fuel a b added hs hu).2
        simpa [Tentative.view, List.append_assoc] using this
      exact ⟨hview, hview, rfl⟩
  | suspend w => rw [hu] at h; cases h
  | fail => rw [hu] at h; cases h
  | error => rw [hu] at h; cases h

/-- Discarding the tentative buffer preserves the invariant on the committed heap. -/
theorem discard_preserves_writer_only
    (t : Tentative) (hs : WriterOnly t.view) : WriterOnly t.discard :=
  writerOnly_suffix (d := t.buf) hs

end WriterMguBindsOnlyWriters

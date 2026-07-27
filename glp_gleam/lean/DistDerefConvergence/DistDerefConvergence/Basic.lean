/-
  PI:17 — distributed-dereference termination + convergence (feature 050, T058 / feature 059 WP T083).

  Mechanized half of proof obligation PI:17 (contracts/proof-obligations.md, gates M2):

    "deref chains crossing instance boundaries terminate and converge to the same binding
     on all participating instances (deferred-local-assignment; globalize/localize on known/1)."

  The model abstracts the distributed-unification surface of the Gleam link layer
  (`glp_gleam/src/glp/link/`, data-model.md §"RemoteVarRef / dist-unify state" and
  contracts/link-parity.md §"Distributed unification"):

    * A `RemoteVarRef` = `(instance_id, writer_id)` pair (data-model.md:49-50) — modelled as `Node`.
    * Two per-instance immutable binding stores + a message channel of in-flight deferred
      assignments (link-parity.md:17 "only the owning instance binds its writer; remote
      requests queue").
    * A cross-instance dereference `derefDist` that hops instance → (link) → instance; every
      hop — local OR message/seam crossing — costs one unit of the fuel measure
      (link-parity.md: "the local-hop + message-hop union").
    * `globalize`/`localize` on the `known/1` boundary (link-parity.md:18 "globalize on export,
      localize on import").

  Core Lean only (no mathlib), sorry-free, checked by a single `lake build` — repo convention
  mirroring `WriterMguBindsOnlyWriters/` (PI:14) and `csharp/glp_result_codec/lean/ResultTermRoundTrip`.

  Modelling decisions (see PROOF.md §Scope for the full honesty list):
  * The per-instance store is split exactly as in PI:14: UNBOUND writers are ABSENT from the
    store; the store holds exactly the BOUND cells (value cell / link cell). Extension is pure
    cons — every delivery returns a new store (immutable-heap R-001).
  * GLP has NO occurs-check, so cyclic cross-instance shapes can exist. A fuel measure bounds
    the traversal; fuel exhaustion yields the loud `.stuck` verdict and — crucially — NO binding,
    exactly as PI:14 handles fuel exhaustion and as the FORK-1 `<circular>` discriminator handles
    a cross-seam cycle (data-model.md:50 "no cycles across the seam — FORK-1 discriminator applies").
  * Convergence is proved robustly: two instances that hold references terminating at the SAME
    owner node read the SAME owner cell, so they agree BEFORE quiescence (both `unbound`) and
    agree AFTER quiescence (both the delivered `value`). The quiescence predicate (GAP-G6) only
    pins WHICH value that is — see `Quiescent` and PROOF.md §Quiescence.
-/

namespace DistDerefConvergence

/-- Instance identity (data-model.md: "instance id"). Two participating instances suffice to
    model a cross-instance seam; the theorems are stated over arbitrary `Node`s, so the two-point
    `Inst` is only the concrete carrier for the seam and the worked examples. -/
inductive Inst where
  | A : Inst
  | B : Inst
deriving DecidableEq, Repr

/-- A `RemoteVarRef`: `(instance_id, writer_id)` (data-model.md:49-50). A node whose `inst`
    equals the reader's own instance is a LOCAL writer; a node whose `inst` differs is a
    GLOBALIZED cross-instance reference — dereferencing it hops the seam (a "message-hop"). -/
structure Node where
  inst : Inst
  wid  : Nat
deriving DecidableEq, Repr

/-- A bound writer cell (the bound half of the immutable per-instance heap):
    * `value v` — a `ValueCell`: the writer is bound to a ground value (`Nat` abstracts the
      ground term, as constants do in PI:14);
    * `link m`  — a forward pointer to another `Node`. A SAME-instance target is an ordinary
      local `WriterBound`; a CROSS-instance target is a globalized `RemoteVarRef`.
    Absence from the store = an UNBOUND writer. -/
inductive Cell where
  | value : Nat  → Cell
  | link  : Node → Cell
deriving Repr

/-- A deferred-local-assignment message in flight on the link (link-parity.md:17
    "remote requests queue"): `assign tgt v` will bind writer `tgt.wid` on instance
    `tgt.inst` to value `v` when the OWNER delivers it. -/
inductive Msg where
  | assign : Node → Nat → Msg
deriving DecidableEq, Repr

/-- The bound-cells store of one instance's immutable heap (bound cells only; absence = unbound).
    Extension is pure cons (R-001, value-copy). -/
abbrev Store := List (Nat × Cell)

/-- A distributed configuration: the two participating instances' stores + the in-flight
    message channel. Pure value — every delivery returns a NEW `Config`. -/
structure Config where
  storeA : Store
  storeB : Store
  chan   : List Msg
deriving Repr

/-- Select the store owning a node's instance. -/
def storeOf (c : Config) : Inst → Store
  | .A => c.storeA
  | .B => c.storeB

/-- Cell fetch within a store (`dict.get`, first match — a later cons shadows an earlier one,
    matching PI:14 `lookup`). `none` = the writer is unbound. -/
def lookup : Store → Nat → Option Cell
  | [], _ => none
  | (k, v) :: rest, x => if k = x then some v else lookup rest x

/-- Cross-instance cell fetch: read a `Node`'s bound cell from its owning instance's store. -/
def cellAt (c : Config) (n : Node) : Option Cell := lookup (storeOf c n.inst) n.wid

-- ── The known/1 globalize / localize boundary (link-parity.md:18) ─────────────────────────────

/-- `globalize`: on export from `self`, a local writer id `w` is tagged with its owner instance,
    becoming the `RemoteVarRef` `(self, w)`. This is the `known/1` export step. -/
def globalize (self : Inst) (w : Nat) : Node := ⟨self, w⟩

/-- `localize`: on import into `self`, a `RemoteVarRef` that points back at `self` is recognised
    as a LOCAL writer (`some wid`); a ref to the OTHER instance stays remote (`none`) and any
    deref must hop the seam to reach it. This is the `known/1` import step. -/
def localize (self : Inst) (n : Node) : Option Nat :=
  if n.inst = self then some n.wid else none

-- ── Distributed dereference ───────────────────────────────────────────────────────────────────

/-- `resolveNode c fuel n` follows link hops from `n` to the TERMINAL node of its chain:
    * no binding      → `some n`      (n is an unbound-writer terminal)
    * `value _`       → `some n`      (n is a value terminal)
    * `link m`, m = n → `some n`      (self-link terminal — the self-bind→Unbound recognizer)
    * `link m`, m ≠ n → recurse at `m` (one hop; crosses the seam iff `m.inst ≠ n.inst`)
    * fuel exhausted  → `none`        (the loud cyclic / FORK-1 verdict — NO terminal, NO binding)
    Every hop (local OR seam-crossing) decrements the SAME fuel: fuel is exactly the
    "local-hop + message-hop union" measure the contract names. -/
def resolveNode (c : Config) : Nat → Node → Option Node
  | 0, _ => none
  | fuel + 1, n =>
      match cellAt c n with
      | none          => some n
      | some (.value _) => some n
      | some (.link m)  => if m = n then some n else resolveNode c fuel m

/-- The distributed deref verdict (mirrors PI:14 `Deref` / heap.gleam `DerefResult` + the loud
    `HeapError`/`<circular>` channel): a ground value, an unbound terminal writer, or the loud
    `stuck` verdict (cyclic / fuel-exhausted — NEVER a silent wrong binding). -/
inductive Deref where
  | value   : Nat  → Deref
  | unbound : Node → Deref
  | stuck   : Deref
deriving DecidableEq, Repr

/-- Read the binding at an already-resolved terminal node. -/
def readCell (c : Config) (t : Node) : Deref :=
  match cellAt c t with
  | none            => .unbound t
  | some (.value v) => .value v
  | some (.link _)  => .unbound t   -- a self-link terminal derefs Unbound (self-bind recognizer)

/-- `derefDist`: resolve `n` to its terminal, then read the terminal's cell. Fuel exhaustion
    (no terminal) is the loud `.stuck` verdict. -/
def derefDist (c : Config) (fuel : Nat) (n : Node) : Deref :=
  match resolveNode c fuel n with
  | none   => .stuck
  | some t => readCell c t

-- ── resolveNode smart constructors (per-arm reduction lemmas) ─────────────────────────────────

theorem resolveNode_none {c : Config} {f : Nat} {n : Node} (h : cellAt c n = none) :
    resolveNode c (f + 1) n = some n := by
  simp [resolveNode, h]

theorem resolveNode_value {c : Config} {f : Nat} {n : Node} {v : Nat}
    (h : cellAt c n = some (.value v)) : resolveNode c (f + 1) n = some n := by
  simp [resolveNode, h]

theorem resolveNode_self {c : Config} {f : Nat} {n : Node} (h : cellAt c n = some (.link n)) :
    resolveNode c (f + 1) n = some n := by
  simp [resolveNode, h]

theorem resolveNode_link {c : Config} {f : Nat} {n m : Node}
    (h : cellAt c n = some (.link m)) (hne : m ≠ n) :
    resolveNode c (f + 1) n = resolveNode c f m := by
  simp [resolveNode, h, hne]

-- ══ THEOREM 1 (termination) ═══════════════════════════════════════════════════════════════════
-- No infinite deref chain: resolution is fuel-monotone (a definite terminal is stable), reaches
-- a GENUINE terminal within a well-founded rank of hops when the chain is acyclic, and yields the
-- loud `.stuck` verdict — never a binding — on a cross-instance cycle.

/-- Fuel monotonicity (one step): once resolution finds a terminal, more fuel keeps it. -/
theorem resolveNode_mono (c : Config) :
    ∀ (f : Nat) (n t : Node), resolveNode c f n = some t → resolveNode c (f + 1) n = some t := by
  intro f
  induction f with
  | zero => intro n t h; simp [resolveNode] at h
  | succ f ih =>
      intro n t h
      cases hlk : cellAt c n with
      | none =>
          rw [resolveNode_none hlk] at h
          rw [resolveNode_none hlk]; exact h
      | some cell =>
          cases cell with
          | value v =>
              rw [resolveNode_value hlk] at h
              rw [resolveNode_value hlk]; exact h
          | link m =>
              by_cases hmn : m = n
              · subst hmn
                rw [resolveNode_self hlk] at h
                rw [resolveNode_self hlk]; exact h
              · rw [resolveNode_link hlk hmn] at h
                rw [resolveNode_link hlk hmn]
                exact ih m t h

/-- Fuel monotonicity (add `k`). -/
theorem resolveNode_add (c : Config) :
    ∀ (k f : Nat) (n t : Node), resolveNode c f n = some t → resolveNode c (f + k) n = some t := by
  intro k
  induction k with
  | zero => intro f n t h; simpa using h
  | succ k ih =>
      intro f n t h
      have h1 : resolveNode c (f + k) n = some t := ih f n t h
      have h2 : resolveNode c (f + k + 1) n = some t := resolveNode_mono c (f + k) n t h1
      exact h2

/-- Fuel monotonicity (≤). -/
theorem resolveNode_mono_le (c : Config) {f g : Nat} {n t : Node}
    (hfg : f ≤ g) (h : resolveNode c f n = some t) : resolveNode c g n = some t := by
  obtain ⟨k, rfl⟩ := Nat.le.dest hfg
  exact resolveNode_add c k f n t h

/-- Terminal well-formedness: a resolved terminal is GENUINELY terminal — its cell is unbound,
    a value, or a self-link (never a proper link onward to a different node). Resolution never
    stops mid-chain. -/
theorem resolveNode_terminal (c : Config) :
    ∀ (f : Nat) (n t : Node), resolveNode c f n = some t →
      cellAt c t = none ∨ (∃ v, cellAt c t = some (.value v)) ∨ cellAt c t = some (.link t) := by
  intro f
  induction f with
  | zero => intro n t h; simp [resolveNode] at h
  | succ f ih =>
      intro n t h
      cases hlk : cellAt c n with
      | none =>
          rw [resolveNode_none hlk] at h
          cases h; exact Or.inl hlk
      | some cell =>
          cases cell with
          | value v =>
              rw [resolveNode_value hlk] at h
              cases h; exact Or.inr (Or.inl ⟨v, hlk⟩)
          | link m =>
              by_cases hmn : m = n
              · subst hmn
                rw [resolveNode_self hlk] at h
                cases h; exact Or.inr (Or.inr hlk)
              · rw [resolveNode_link hlk hmn] at h
                exact ih m t h

/-- **Bounded termination (well-founded).** If the configuration carries an acyclicity
    certificate — a `rank : Node → Nat` that STRICTLY DECREASES across every link hop
    (local or seam-crossing) — then resolution reaches a terminal within `rank n + 1` hops:
    NO fuel exhaustion, NO `.stuck`. This is the strictly-decreasing well-founded measure over
    the local-hop + message-hop union that the contract names. -/
theorem resolveNode_terminates (c : Config) (rank : Node → Nat)
    (hrank : ∀ n m, cellAt c n = some (.link m) → m ≠ n → rank m < rank n) :
    ∀ (k : Nat) (n : Node), rank n ≤ k → ∃ t, resolveNode c (k + 1) n = some t := by
  intro k
  induction k with
  | zero =>
      intro n hr
      cases hlk : cellAt c n with
      | none => exact ⟨n, resolveNode_none hlk⟩
      | some cell =>
          cases cell with
          | value v => exact ⟨n, resolveNode_value hlk⟩
          | link m =>
              by_cases hmn : m = n
              · rw [hmn] at hlk; exact ⟨n, resolveNode_self hlk⟩
              · exfalso
                have hlt := hrank n m hlk hmn
                have : rank n = 0 := Nat.le_zero.mp hr
                omega
  | succ k ih =>
      intro n hr
      cases hlk : cellAt c n with
      | none => exact ⟨n, resolveNode_none hlk⟩
      | some cell =>
          cases cell with
          | value v => exact ⟨n, resolveNode_value hlk⟩
          | link m =>
              by_cases hmn : m = n
              · rw [hmn] at hlk; exact ⟨n, resolveNode_self hlk⟩
              · have hlt := hrank n m hlk hmn
                have hmk : rank m ≤ k := by omega
                obtain ⟨t, ht⟩ := ih m hmk
                exact ⟨t, by rw [resolveNode_link hlk hmn]; exact ht⟩

/-- **Theorem 1 (termination), headline.** Under an acyclicity certificate `rank`, `derefDist`
    reaches a definite terminal in `rank n + 1` hops and returns that terminal's binding —
    never the loud `.stuck` fuel-exhaustion verdict. -/
theorem derefDist_terminates (c : Config) (rank : Node → Nat)
    (hrank : ∀ n m, cellAt c n = some (.link m) → m ≠ n → rank m < rank n) (n : Node) :
    ∃ t : Node, resolveNode c (rank n + 1) n = some t ∧ derefDist c (rank n + 1) n = readCell c t := by
  obtain ⟨t, ht⟩ := resolveNode_terminates c rank hrank (rank n) n (Nat.le_refl _)
  exact ⟨t, ht, by simp [derefDist, ht]⟩

/-- Corollary: under an acyclicity certificate, `derefDist` never gets stuck. -/
theorem derefDist_not_stuck (c : Config) (rank : Node → Nat)
    (hrank : ∀ n m, cellAt c n = some (.link m) → m ≠ n → rank m < rank n) (n : Node) :
    derefDist c (rank n + 1) n ≠ .stuck := by
  obtain ⟨t, _, hd⟩ := derefDist_terminates c rank hrank n
  rw [hd]
  simp only [readCell]
  cases cellAt c t with
  | none => simp
  | some cell => cases cell with
      | value v => simp
      | link m => simp

-- ── FORK-1: a cross-instance cycle yields the loud stuck verdict (no binding) ──────────────────

/-- The canonical FORK-1 shape: instance A's writer 0 is a globalized ref to instance B's
    writer 0, and vice-versa — a cross-seam cycle with NO occurs-check to catch it. -/
def cyc : Config where
  storeA := [(0, .link ⟨.B, 0⟩)]
  storeB := [(0, .link ⟨.A, 0⟩)]
  chan   := []

/-- The cycle never resolves to a terminal, at ANY fuel (both directions). The single-step
    induction goes through because one hop crosses the seam A→B (and B→A), swapping the pair. -/
theorem cyc_never (f : Nat) :
    resolveNode cyc f ⟨.A, 0⟩ = none ∧ resolveNode cyc f ⟨.B, 0⟩ = none := by
  induction f with
  | zero => exact ⟨rfl, rfl⟩
  | succ f ih =>
      obtain ⟨iha, ihb⟩ := ih
      refine ⟨?_, ?_⟩
      · rw [resolveNode_link (show cellAt cyc ⟨.A, 0⟩ = some (.link ⟨.B, 0⟩) from rfl) (by decide)]
        exact ihb
      · rw [resolveNode_link (show cellAt cyc ⟨.B, 0⟩ = some (.link ⟨.A, 0⟩) from rfl) (by decide)]
        exact iha

/-- **Theorem 1 (termination), FORK-1 dual.** The cross-instance cycle produces the loud
    `.stuck` verdict — and hence NO binding — at every fuel. Exactly the `<circular>` /
    "no cycles across the seam" discriminator. -/
theorem cyc_stuck (f : Nat) : derefDist cyc f ⟨.A, 0⟩ = .stuck := by
  simp [derefDist, (cyc_never f).1]

-- ══ THEOREM 2 (convergence) ═══════════════════════════════════════════════════════════════════
-- Eventual agreement: two references that resolve to the SAME owner terminal read the SAME owner
-- cell, so their deref verdicts are equal. This holds before quiescence (both `unbound`) and
-- after quiescence (both the delivered `value`); quiescence pins WHICH value.

/-- **Shared-terminal agreement (core convergence lemma).** Two nodes that resolve to the same
    terminal `t` deref to the same verdict — they read the one owner cell. -/
theorem derefDist_eq_of_terminal (c : Config) {f : Nat} {n1 n2 t : Node}
    (h1 : resolveNode c f n1 = some t) (h2 : resolveNode c f n2 = some t) :
    derefDist c f n1 = derefDist c f n2 := by
  simp [derefDist, h1, h2]

/-- Fuel-agnostic form: nodes resolving to the same terminal at (possibly different) fuels
    `fa`, `fb` agree at the common fuel `fa + fb`. -/
theorem derefDist_converges (c : Config) {fa fb : Nat} {n1 n2 t : Node}
    (h1 : resolveNode c fa n1 = some t) (h2 : resolveNode c fb n2 = some t) :
    derefDist c (fa + fb) n1 = derefDist c (fa + fb) n2 := by
  have h1' := resolveNode_mono_le c (Nat.le_add_right fa fb) h1
  have h2' := resolveNode_mono_le c (Nat.le_add_left fb fa) h2
  exact derefDist_eq_of_terminal c h1' h2'

/-- A ref on instance A and a ref on instance B, each linking to the SAME owner writer `⟨.B, w⟩`,
    always resolve to that owner terminal (owner instance differs from A, so the A-hop crosses the
    seam; B's handle links locally). -/
theorem handles_resolve_to_owner (c : Config) {w ha hb : Nat}
    (hAlink : cellAt c ⟨.A, ha⟩ = some (.link ⟨.B, w⟩))
    (hBlink : cellAt c ⟨.B, hb⟩ = some (.link ⟨.B, w⟩))
    (hbne : (⟨.B, w⟩ : Node) ≠ ⟨.B, hb⟩) :
    resolveNode c 2 ⟨.A, ha⟩ = resolveNode c 1 ⟨.B, w⟩ ∧
    resolveNode c 2 ⟨.B, hb⟩ = resolveNode c 1 ⟨.B, w⟩ := by
  have hAne : (⟨.B, w⟩ : Node) ≠ ⟨.A, ha⟩ := by intro h; simp at h
  refine ⟨?_, ?_⟩
  · exact resolveNode_link hAlink hAne
  · exact resolveNode_link hBlink hbne

/-- **Theorem 2 (convergence), headline — a globalized writer converges on all instances.**
    Given a value settled on the owner `⟨.B, w⟩` (delivered — see `Quiescent`), instance A's
    globalized reference and instance B's local handle both deref to that one value: the two
    instances AGREE, and on the delivered value `v`. -/
theorem dist_deref_converges (c : Config) {w ha hb v : Nat}
    (hAlink : cellAt c ⟨.A, ha⟩ = some (.link ⟨.B, w⟩))
    (hBlink : cellAt c ⟨.B, hb⟩ = some (.link ⟨.B, w⟩))
    (hbne : (⟨.B, w⟩ : Node) ≠ ⟨.B, hb⟩)
    (hOwner : cellAt c ⟨.B, w⟩ = some (.value v)) :
    derefDist c 2 ⟨.A, ha⟩ = derefDist c 2 ⟨.B, hb⟩ ∧
    derefDist c 2 ⟨.A, ha⟩ = .value v := by
  have rOwner : resolveNode c 1 ⟨.B, w⟩ = some ⟨.B, w⟩ := resolveNode_value hOwner
  obtain ⟨rA, rB⟩ := handles_resolve_to_owner c hAlink hBlink hbne
  rw [rOwner] at rA rB
  refine ⟨derefDist_eq_of_terminal c rA rB, ?_⟩
  simp [derefDist, rA, readCell, hOwner]

/-- **Convergence is robust to non-quiescence.** Even with the assignment still IN FLIGHT
    (owner unbound), the two instances agree — both `unbound` on the owner. Quiescence is not
    needed for AGREEMENT; it is needed only to pin which value they agree on. -/
theorem dist_deref_agrees_pre_quiescence (c : Config) {w ha hb : Nat}
    (hAlink : cellAt c ⟨.A, ha⟩ = some (.link ⟨.B, w⟩))
    (hBlink : cellAt c ⟨.B, hb⟩ = some (.link ⟨.B, w⟩))
    (hbne : (⟨.B, w⟩ : Node) ≠ ⟨.B, hb⟩)
    (hOwner : cellAt c ⟨.B, w⟩ = none) :
    derefDist c 2 ⟨.A, ha⟩ = derefDist c 2 ⟨.B, hb⟩ ∧
    derefDist c 2 ⟨.A, ha⟩ = .unbound ⟨.B, w⟩ := by
  have rOwner : resolveNode c 1 ⟨.B, w⟩ = some ⟨.B, w⟩ := resolveNode_none hOwner
  obtain ⟨rA, rB⟩ := handles_resolve_to_owner c hAlink hBlink hbne
  rw [rOwner] at rA rB
  refine ⟨derefDist_eq_of_terminal c rA rB, ?_⟩
  simp [derefDist, rA, readCell, hOwner]

-- ── Deferred-local-assignment: delivery, drain, and quiescence (GAP-G6) ───────────────────────

/-- The quiescence predicate (GAP-G6 oracle, data-model.md:66-67): no in-flight frames.
    The distributed-run termination detector reports quiescent when the channel is empty. -/
def Quiescent (c : Config) : Prop := c.chan = []

/-- Bind a writer to a value by consing onto its instance's store (immutable extension). -/
def bind (s : Store) (w v : Nat) : Store := (w, .value v) :: s

/-- Deliver ONE deferred-local-assignment: the OWNING instance (and only it) binds its writer,
    then the message leaves the channel (link-parity.md:17). -/
def deliver (c : Config) : Config :=
  match c.chan with
  | [] => c
  | Msg.assign ⟨.A, w⟩ v :: rest => { c with storeA := bind c.storeA w v, chan := rest }
  | Msg.assign ⟨.B, w⟩ v :: rest => { c with storeB := bind c.storeB w v, chan := rest }

/-- Delivering a non-empty channel removes exactly the head message. -/
theorem deliver_chan {c : Config} {m : Msg} {rest : List Msg} (h : c.chan = m :: rest) :
    (deliver c).chan = rest := by
  cases m with
  | assign n v =>
      cases n with
      | mk inst w =>
          cases inst with
          | A => simp [deliver, h]
          | B => simp [deliver, h]

/-- Delivering an assignment to owner `⟨.B, w⟩` settles that owner cell to the delivered value. -/
theorem deliver_binds_owner {c : Config} {w v : Nat} {rest : List Msg}
    (h : c.chan = Msg.assign ⟨.B, w⟩ v :: rest) :
    cellAt (deliver c) ⟨.B, w⟩ = some (.value v) := by
  simp [deliver, h, cellAt, storeOf, bind, lookup]

/-- Drain up to `fuel` in-flight assignments (owner-side delivery, one per step). -/
def drainN : Nat → Config → Config
  | 0, c => c
  | fuel + 1, c =>
      match c.chan with
      | [] => c
      | _ :: _ => drainN fuel (deliver c)

/-- Draining with fuel ≥ the channel length reaches quiescence (all in-flight assignments
    delivered). This is the reachability side of the GAP-G6 oracle: a run with a finite backlog
    quiesces. -/
theorem drainN_quiescent : ∀ (fuel : Nat) (c : Config), c.chan.length ≤ fuel → Quiescent (drainN fuel c) := by
  intro fuel
  induction fuel with
  | zero =>
      intro c h
      have : c.chan.length = 0 := Nat.le_zero.mp h
      have hnil : c.chan = [] := List.length_eq_zero_iff.mp this
      simp [drainN, Quiescent, hnil]
  | succ fuel ih =>
      intro c h
      cases hchan : c.chan with
      | nil => simp [drainN, Quiescent, hchan]
      | cons m rest =>
          have hlen : (deliver c).chan.length ≤ fuel := by
            have hd : (deliver c).chan = rest := deliver_chan hchan
            have : c.chan.length = rest.length + 1 := by simp [hchan]
            rw [hd]; omega
          have : drainN (fuel + 1) c = drainN fuel (deliver c) := by
            simp [drainN, hchan]
          rw [this]
          exact ih (deliver c) hlen

-- ══ Non-vacuity: the full deferred-assignment → quiescence → convergence pipeline, executable ══
-- (every verdict is inhabited and the end-to-end story is pinned by `rfl`)

/-- A worked two-instance configuration: instance A holds a globalized reference (writer 0 →
    `⟨.B, 7⟩`), instance B holds a local handle (writer 1 → `⟨.B, 7⟩`), and one deferred
    assignment binding the owner `⟨.B, 7⟩ := 99` is in flight. -/
def demo : Config where
  storeA := [(0, .link ⟨.B, 7⟩)]
  storeB := [(1, .link ⟨.B, 7⟩)]
  chan   := [Msg.assign ⟨.B, 7⟩ 99]

-- BEFORE quiescence: the owner is unbound; BOTH instances agree, deref = unbound ⟨.B,7⟩.
example : derefDist demo 3 ⟨.A, 0⟩ = .unbound ⟨.B, 7⟩ := rfl
example : derefDist demo 3 ⟨.B, 1⟩ = .unbound ⟨.B, 7⟩ := rfl
example : ¬ Quiescent demo := by simp [Quiescent, demo]

-- Deliver the one in-flight assignment → the run quiesces.
example : Quiescent (drainN 1 demo) := rfl

-- AFTER quiescence: BOTH instances converge to the delivered value 99.
example : derefDist (drainN 1 demo) 3 ⟨.A, 0⟩ = .value 99 := rfl
example : derefDist (drainN 1 demo) 3 ⟨.B, 1⟩ = .value 99 := rfl
example : derefDist (drainN 1 demo) 3 ⟨.A, 0⟩ = derefDist (drainN 1 demo) 3 ⟨.B, 1⟩ := rfl

-- The FORK-1 cross-seam cycle is loudly stuck (no binding), never a wrong answer.
example : derefDist cyc 10 ⟨.A, 0⟩ = .stuck := cyc_stuck 10

-- The globalize / localize known/1 boundary.
example : localize .B (globalize .B 7) = some 7 := rfl   -- owner localizes its export back to writer 7
example : localize .A (globalize .B 7) = none := rfl      -- the far instance keeps it a remote ref

-- Every deref verdict is inhabited.
example : derefDist demo 3 ⟨.A, 0⟩ = .unbound ⟨.B, 7⟩ := rfl
example : derefDist (drainN 1 demo) 3 ⟨.A, 0⟩ = .value 99 := rfl
example : derefDist cyc 3 ⟨.A, 0⟩ = .stuck := cyc_stuck 3

end DistDerefConvergence

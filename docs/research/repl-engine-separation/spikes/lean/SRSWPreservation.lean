/-
  SRSWPreservation.lean — PROP-1 for the Lean 4 tactic-loop spike (US3, 027 / R13-R14).

  Property (research.md §1, PROP-1): **SRSW preservation on a toy GLP clause.**
  SRSW (single-reader / single-writer, docs/glp-cheat-sheet.md §3): in one clause each
  reader `X?` occurs at most once AND each writer `X` occurs at most once — modulo the
  constant-type relaxation (a constant-type variable is exempt; cheat-sheet §3 / line 273).

  This file STATES PROP-1 and leaves the proof body as `sorry`. The single `sorry` below is
  the spike target: the bounded Lean tactic loop discharges it at T014/T015 (sorry-isolation +
  owner-escalation per FR-031/FR-035). The file MUST elaborate in Lean 4.30.0 with no errors
  other than this deliberate `sorry`.

  Minimal by design: one toy clause model, one toy transformation (an injective variable
  renaming), one preservation theorem. Cross-refs:
    - ../tool-versions.txt              (Lean 4.30.0 — target toolchain)
    - ../../../reconciliation/REFINEMENT-METHOD.md  (§3 "clean inductive fact over finite types")
    - ../RESULT.md                      (acceptance artifact, SC-006/009)
-/

namespace SRSWSpike

/-- A variable occurrence carries a kind: a reader (`X?`) or a writer (`X`). -/
inductive Role where
  | reader
  | writer
deriving DecidableEq, BEq, Repr

/-- A single occurrence of a variable inside a clause: which variable (by index) and its role. -/
structure Occ where
  var  : Nat
  role : Role
deriving DecidableEq, Repr

/--
  A toy GLP clause: the flat list of its variable occurrences, plus a predicate marking which
  variable indices have *constant type* (and are therefore exempt from the SRSW count — the
  constant-type relaxation, cheat-sheet §3).
-/
structure Clause where
  occs  : List Occ
  /-- `isConst v = true` ⇒ variable `v` is constant-type and exempt from the SRSW bound. -/
  isConst : Nat → Bool

/-- Number of occurrences of variable `v` playing role `r` in clause `c`. -/
def roleCount (c : Clause) (v : Nat) (r : Role) : Nat :=
  (c.occs.filter (fun o => (o.var == v) && (o.role == r))).length

/--
  SRSW-validity for one variable: unless it is constant-type, it has at most one reader
  occurrence AND at most one writer occurrence.
-/
def varSRSW (c : Clause) (v : Nat) : Prop :=
  c.isConst v = true ∨ (roleCount c v Role.reader ≤ 1 ∧ roleCount c v Role.writer ≤ 1)

/-- SRSW-valid clause: every variable occurring in it is SRSW-valid. -/
def SRSWValid (c : Clause) : Prop :=
  ∀ v ∈ (c.occs.map Occ.var), varSRSW c v

/--
  The toy transformation: rename every variable index through an injective function `ρ`,
  carrying the constant-type marking along the renaming. This models a structural
  alpha-renaming / reindex stage of the engine — it must not introduce SRSW violations.
-/
def renameOcc (ρ : Nat → Nat) (o : Occ) : Occ :=
  { o with var := ρ o.var }

def rename (ρ : Nat → Nat) (c : Clause) : Clause :=
  { occs := c.occs.map (renameOcc ρ),
    -- A renamed index `w` is constant-type iff some original occurrence whose variable
    -- renames to `w` was itself constant-type. Decidable: the search ranges over the
    -- finite occurrence list, not an unbounded `∃ v : Nat`.
    isConst := fun w => c.occs.any (fun o => (ρ o.var == w) && c.isConst o.var) }

/--
  PROP-1 (spike target): an **injective** renaming preserves SRSW-validity of the toy clause.
  The `sorry` is the obligation the bounded tactic loop discharges (T014/T015).
-/
theorem rename_preserves_SRSW
    (ρ : Nat → Nat) (hρ : Function.Injective ρ)
    (c : Clause) (h : SRSWValid c) :
    SRSWValid (rename ρ c) := by
  sorry

end SRSWSpike

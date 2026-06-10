-- proof.lean — the tactic block the bounded loop DISCOVERED for `rename_preserves_SRSW`
-- (US3 spike, T015). It is spliced in place of the `sorry` in SRSWPreservation.lean by
-- `harness.py` (verify/attempt). Recorded so `run.sh` can deterministically reproduce the
-- PROVED outcome. Found in 5 attempts / budget 20, against real Lean 4.30.0 (RESULT.md).
--
-- Core Lean only (no Mathlib in the spike toolchain): the key lemma `maplen` shows an
-- injective renaming preserves per-(variable,role) occurrence counts (induction on the occ
-- list + `beq_eq_false_iff_ne` + `Function.Injective.ne`); `hcount` lifts it to roleCount;
-- the constant-flag case is discharged by an existential witness into `List.any`.
intro w hw
have maplen : ∀ (l : List Occ) (u : Nat) (rr : Role),
    (List.filter (fun o => o.var == ρ u && o.role == rr) (l.map (renameOcc ρ))).length
      = (List.filter (fun o => o.var == u && o.role == rr) l).length := by
  intro l u rr
  induction l with
  | nil => rfl
  | cons a as ih =>
    have hbeq : (ρ a.var == ρ u) = (a.var == u) := by
      by_cases hb : a.var = u
      · subst hb; simp
      · rw [beq_eq_false_iff_ne.mpr (hρ.ne hb), beq_eq_false_iff_ne.mpr hb]
    simp only [List.map_cons, renameOcc, List.filter_cons, hbeq]
    by_cases hg : (a.var == u && a.role == rr) = true
    · simp [hg, ih]
    · simp [hg, ih]
have hcount : ∀ (u : Nat) (rr : Role), roleCount (rename ρ c) (ρ u) rr = roleCount c u rr := by
  intro u rr
  simp only [roleCount, rename]
  exact maplen c.occs u rr
simp only [rename, List.map_map] at hw
rw [List.mem_map] at hw
obtain ⟨o, ho, rfl⟩ := hw
show varSRSW (rename ρ c) (ρ o.var)
have hv : varSRSW c o.var := h o.var (List.mem_map.mpr ⟨o, ho, rfl⟩)
simp only [varSRSW] at hv ⊢
rcases hv with hconst | ⟨hr, hwr⟩
· left
  simp only [rename]
  exact List.any_eq_true.mpr ⟨o, ho, by simp [hconst]⟩
· right
  rw [hcount o.var Role.reader, hcount o.var Role.writer]
  exact ⟨hr, hwr⟩

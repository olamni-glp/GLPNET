import Lake
open Lake DSL

-- Feature 050, T058 — mechanized half of proof obligation PI:17 (RISK-PROOF-distDeref):
-- cross-instance dereference chains terminate and converge under deferred-local-assignment
-- with globalize/localize on known/1 (gates M2; contracts/proof-obligations.md).
-- Recorded deviation: the P4 harness planned SPIN here; Lean is owner-directed (2026-07-10).
-- Convention mirrors 029/038: NO mathlib, core-Lean structural proofs, single fast
-- `lake build`. No external LM API.
package «DistDerefConvergence» where

@[default_target]
lean_lib «DistDerefConvergence» where

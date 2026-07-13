import Lake
open Lake DSL

-- Feature 050, T027 — mechanized half of proof obligation PI:14 (RISK-PROOF-writerMGU):
-- the engine's binding step binds only writers — never readers, never writer↔writer —
-- under the immutable-heap/value-copy model (gates M1; contracts/proof-obligations.md).
-- Convention mirrors 029 IlCodecRoundTrip / 038 ResultTermRoundTrip: NO mathlib, core-Lean
-- structural proofs only, single fast `lake build`. No external LM API.
package «WriterMguBindsOnlyWriters» where

@[default_target]
lean_lib «WriterMguBindsOnlyWriters» where

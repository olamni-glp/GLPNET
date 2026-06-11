import Lake
open Lake DSL

-- Feature 029 — formal gate. A self-contained Lean 4 proof of `decode ∘ encode = id`
-- over a simplified model of the IL codec (v1-opcode subset + ground constants).
-- NO mathlib dependency: the round-trip is a structural induction that core Lean proves,
-- so the proof checks in a single fast `lake build` (SC-007). No external LM API (A6).
package «IlCodecRoundTrip» where

@[default_target]
lean_lib «IlCodecRoundTrip» where

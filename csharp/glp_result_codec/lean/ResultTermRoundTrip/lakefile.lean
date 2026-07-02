import Lake
open Lake DSL

-- Feature 038, T042 (OPTIONAL) — formal gate. A self-contained Lean 4 proof of
-- `decode ∘ encode = id` over a simplified flat ground-term model of the result-envelope
-- term sub-codec (int/string/atom/var→writer). Mirrors feature 029's IlCodecRoundTrip.
-- NO mathlib dependency: the round-trip is a structural induction that core Lean proves,
-- so it checks in a single fast `lake build`. No external LM API.
package «ResultTermRoundTrip» where

@[default_target]
lean_lib «ResultTermRoundTrip» where

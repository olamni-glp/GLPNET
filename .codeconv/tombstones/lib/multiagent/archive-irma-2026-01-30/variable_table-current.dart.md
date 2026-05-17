---
path: lib/multiagent/archive-irma-2026-01-30/variable_table-current.dart
name: variable_table-current.dart
purpose: "Variable Table (V_p) for irmaGLP\n\nTracks variables whose paired counterparts are non-local.\n\nCore Invariant: V_p contains exactly those variables whose paired \ncounterparts are non-local (i.e., in a remote agent's resolvent).\n\nPer spec, V_p ⊆ \U0001D4B1 × Π × (\U0001D4AF ∪ Π ∪ {⊥}) × \U0001D4AE* where \U0001D4B1 includes both X (writers)\nand X? (readers) as distinct elements. The fourth component Σ is the\nsuspension list for goals waiting on this variable. For imported readers,\nV_p serves as the \"virtual writer\" that holds suspensions since there is\nno local writer cell.\n\nSpecification: /docs/ma/irmaGLP-spec.md Section 3.1.2\n"
key_idea: "Variable Table (V_p) for irmaGLP\n\nTracks variables whose paired counterparts are non-local.\n\nCore Invariant: V_p contains exactly those variables whose paired \ncounterparts are non-local (i.e., in a remote agent's resolvent).\n\nPer spec, V_p ⊆ \U0001D4B1 × Π × (\U0001D4AF ∪ Π ∪ {⊥}) × \U0001D4AE* where \U0001D4B1 includes both X (writers)\nand X? (readers) as distinct elements. The fourth component Σ is the\nsuspension list for goals waiting on this variable. For imported readers,\nV_p serves as the \"virtual writer\" that holds suspensions since there is\nno local writer cell.\n\nSpecification: /docs/ma/irmaGLP-spec.md Section 3.1.2\n"
dependencies:
- lib/multiagent/runtime/suspension.dart
- lib/multiagent/runtime/terms.dart
callers: []
mtime: '2026-05-17T10:36:35.288Z'
sha256: b52af792cae28dc7656836e7eebd77831e788ac6f6f961efca94d3ff0f238102
target_path: lib/multiagent/archive-irma-2026-01-30/variable_table-current.cs
---

Variable Table (V_p) for irmaGLP

Tracks variables whose paired counterparts are non-local.

Core Invariant: V_p contains exactly those variables whose paired 
counterparts are non-local (i.e., in a remote agent's resolvent).

Per spec, V_p ⊆ 𝒱 × Π × (𝒯 ∪ Π ∪ {⊥}) × 𝒮* where 𝒱 includes both X (writers)
and X? (readers) as distinct elements. The fourth component Σ is the
suspension list for goals waiting on this variable. For imported readers,
V_p serves as the "virtual writer" that holds suspensions since there is
no local writer cell.

Specification: /docs/ma/irmaGLP-spec.md Section 3.1.2

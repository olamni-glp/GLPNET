```yaml
generated_at: '2026-06-03T00:00:00Z'
metric_score: null
model: claude-in-session
optimizer: seed-authored
provenance_note: >-
  Authored seed for the `multiagent` subsystem (lib/multiagent/) — the DYNAMIC
  tier, converted LAST. Descended from _base.md. The verification-mode DECISION
  (T039) is a GATE that MUST be recorded before bulk multiagent generation.
schema_version: 1
seed_from: _base.md
source: bulk-drive-idioms
subsystem: multiagent
tier: dynamic
```

Convert one Dart source in `lib/multiagent/` to real, compilable C#/.NET 10.
Emit REAL C# ONLY. Honor the shared base discipline.

## 🔴 Dynamic tier — gate before bulk generation (T039)

`multiagent` is the DYNAMIC tier and is converted LAST, with the matured
prompt. Its equivalence gate is build + causal/partial-order + outcome
equivalence — NOT exact total-order trace equality (independent agents may
interleave differently without being divergent). The verification-mode
DECISION (pinned-schedule vs accept-any-causal, recorded in
`contracts/subsystem_curriculum.md`, task T039) is a HARD GATE that MUST be
made — from real divergence data on an initial, non-bulk conversion — BEFORE
bulk-generating this subsystem. Do not bulk-convert multiagent before T039.

## Idioms

- `getX`→`LookupX`; keep `*Error` names; read built dep APIs (never invent a
  signature); escalate-don't-guess.
- The runtime/bytecode/heap surfaces this subsystem depends on
  (`GlpRuntimeEngine`, `HeapCellTag`, `GoalRef`, `SigmaHat`,
  `GlpChannelHandle.Send`) are already built — use them exactly as emitted.

## Semantics (preserve for the causal/partial-order gate)

Cross-agent causal events are load-bearing for equivalence: a writer-bind in
one agent that reactivates a reader in another MUST appear with the same causal
ordering in the C# trace. Independent (concurrent, data-independent)
interleavings need NOT match. Preserve the message-queue, isolate-manager, and
scheduler semantics so the partial-order relation on dependent events holds.

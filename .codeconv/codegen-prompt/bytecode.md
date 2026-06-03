```yaml
baseline_score: 1.0
budget: 6
budget_used: 1
dataset_hash: bbb9bece11321f97
generated_at: '2026-06-03T00:00:00Z'
metric_score: 1.0
model: claude-in-session
optimizer: gepa-build-only
provenance_note: >-
  GEPA run 2026-06-03 (build-only metric — decision 1: GEPA wired before the
  runnable C# REPL exists). Seed = the authored bulk-drive idioms (prose below,
  UNCHANGED by this run). One fresh generation of the train file opcodes.dart
  under this prompt (generator sub-agent, no peeking at the existing .cs) built
  standalone at score 1.0; the existing baseline outputs (opcodes/opcodes_v2
  isolated, asm with the opcodes dep) also build, so the build-only metric is
  at its 1.0 ceiling for the bytecode leaves. There is NO build-gradient for
  the reflector to climb, so the instructions are frozen unchanged from the
  seed (honest outcome, not a fabricated edit). The runner held-out file is
  excluded from the eval — it is the 4863-line E1 scope-exceeded escalation,
  not a prompt-quality signal. Genuine GEPA-driven refinement of this prompt
  awaits the trace-equivalence fidelity metric (T031, once the C# REPL is
  runnable), which adds a gradient ABOVE the build ceiling.
run_date: '2026-06-03'
schema_version: 1
seed_from: _base.md
source: bulk-drive-idioms
subsystem: bytecode
train_eval:
  generated: [opcodes.dart]
  held_out_skipped: [runner.dart]
  score: 1.0
```

Convert one Dart source in `lib/bytecode/` to real, compilable C#/.NET 10.
Emit REAL C# ONLY (one raw `.cs`, no fences/prose/leftover-Dart). Honor the
shared base discipline: read the actual built dependency `.cs` for every API
(never invent a signature); apply `getX`→`LookupX`; keep `*Error` names;
escalate-don't-guess; SCC members build as one coordinated batch.

## Namespace + opcode idioms

- Target namespace: `GlpRuntime.Bytecode`.
- Alias the v2 opcodes: `using V2 = GlpRuntime.Bytecode.V2;`. Then Dart
  `if (op is opv2.HeadVariable hv)` maps 1:1 to `if (op is V2.HeadVariable hv)`.
- `opcodes.cs` exposes `Arity`/`Slot`/`Value` as **`long`**. Insert an explicit
  `(int)` cast at every use site that needs an int — arity loops, the `S`
  index, `cx.S++`, slot indexing. (CS0266 if you forget.)

## runner.dart (the FCP/WAM interpreter) — two-phase semantics are load-bearing

`runner.dart` is the 4863-line bytecode VM. EVERY dispatch arm in
`runWithStatus` is load-bearing — the convspec requires byte-for-byte
preservation of the two-phase HEAD/GUARD/BODY semantics. Preserve exactly:

- the three-valued unification over `sigmaHat` (σ̂w) / `Si` / `U`;
- WAM read/write mode toggling (`mode`, `S`, `currentStructure`);
- the FCP wake-on-binding contract (suspend on unbound readers; reactivate on
  writer bind);
- tail-call `kappa` rewrite; `_TentativeStruct` HEAD-phase building and
  `_ClauseVar` resolution (`_convertTentativeToStruct`);
- module-RPC `GlpChannel` synchronous send.

This file exceeds a single-turn output budget (E1): convert it in the recorded
**6-chunk split** (header+classes+enums → HEAD-phase arms ×2 → Unify arms →
Commit/ClauseControl/BODY → Spawn/Requeue/Distribute/Transmit/Guards/Helpers),
appended via Edit, each chunk cross-validated against the source's two-phase
semantics. Do NOT collapse arms or drop branches to fit the budget — escalate
(`scope-exceeds-output-budget`) rather than emit a lossy translation.

## Dependency API surface (read the built `.cs`; these are the confirmed shapes)

- `GoalRef` = `readonly record struct (int Id, int Pc)` in `GlpRuntime.Runtime`.
- `SigmaHat` = `global using = Dictionary<int, object?>`.
- heap (`heap_fcp.cs`): `IsWriter/IsReader/IsValue/IsWriterBound/IsReaderBound/`
  `IsFullyBound/IsBound/ValueOfWriter/GetReaderValue/GetValue/DerefAddr→object/`
  `Dereference(Term)→Term/PairedReaderAddr(int)→int/TryWriterForReader(int)→int?/`
  `AllocateVariable()→(int Writer,int Reader)/BindWriterConst(int,object?)→`
  `List<GoalRef>/BindWriterStruct(int,string,List<Term>)→List<GoalRef>`. The
  runner goes through these methods — it does NOT touch raw cell tags.
- `BodyKernel` = `delegate BodyKernelResult(GlpRuntime, IReadOnlyList<object?>)`;
  `BodyKernelRegistry.Lookup(string, long)→BodyKernel?`.
- `CommitOps.ApplySigmaHatFCP(HeapFCP, SigmaHat)→IList<GoalRef>`.
- `GlpRuntimeEngine.SuspendGoalFCP(int, int, ISet<int>)` (note the engine class
  is `GlpRuntimeEngine`, not `GlpRuntime` — see the runtime-core prompt).
- `GlpChannelHandle.Send(Term)→IReadOnlyList<GoalRef>`.

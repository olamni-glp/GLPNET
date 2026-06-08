---
title: "# cross-module dynamic dispatch (serve/2 + _activate + _select) vs. madGLP isolate boundaries — do they meet today, and what distributing # requires"
authors: "GLP/glpnet maintainers (Gabi / vonwenm); madGLP spec authored by Claude per CGLP paper §7 (E. Shapiro et al.); FCP precedent: E. Shapiro et al."
year: "2026"
source_url: "local: D:/bstdev/research/glp/glpnet/glp_runtime/lib/runtime/glp_activation.dart ; .../glp_runtime/lib/bytecode/runner.dart (Distribute/Transmit opcodes) ; .../glp_runtime/lib/runtime/body_kernels.dart (activateKernel) ; .../glp_runtime/lib/runtime/runtime.dart (rt.glpChannels, rt.runners) ; .../glp_runtime/lib/compiler/project_linker.dart (static link) ; .../docs/modules/dynamic-dispatch-implementation-plan.md ; .../docs/ma/isolate-boot-spec.md ; .../glp_runtime/lib/multiagent/mad_context.dart"
retrieved: 2026-06-06
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — How does the runtime # cross-module dynamic dispatch (channel + serve/2 + _activate + _select/2) interact with madGLP isolate boundaries — can a # call target a procedure in a DIFFERENT isolate/instance today, or only within one isolate? What would distributing # require?"
precedence_class: glp-current
access: full-text
---

# `#` dynamic dispatch vs. madGLP isolate boundaries

## Direct answer (the question, resolved)

**Today, a `M # goal` cross-module call targets a procedure in the SAME runtime
instance / SAME isolate / SAME FCP heap as the caller — never a different
isolate.** The `#` dispatch mechanism (the `serve/2` service loop + the
`'_activate'` body kernel + the compiler-generated `_select/1` table, routed
through a GLP channel) and the madGLP cross-isolate variable mechanism
(`global_send` + global writers table `W_p` + globalize/localize + the Dart
`IsolateManager`) are **two entirely separate, non-interoperating subsystems**.
They share no code, no addressing scheme, and no transport. The `#` "channel" is
an in-heap stream tail bound by `heap.bindVariable` and drained by the same
single scheduler; it has **no notion of a remote agent, isolate, or instance.**

Distributing `#` so a call can transparently reach a procedure in another
instance requires **unifying these two mechanisms**: replacing the in-heap GLP
channel that `#` routes over with a madGLP-style *global link* (so the goal term
— and crucially the shared result variable inside it — crosses the boundary via
globalize/localize + assignment messages), or equivalently teaching the `#`
router to address a *remote* `serve/2` loop and carry the goal's writer/reader
variables across the wire with madGLP's polarity-correct globalize/localize.
Neither bridge exists today.

---

## SOURCE 1 (precedence: glp-current, authoritative) — the `#` path is single-runtime, single-heap

### 1a. `rt.glpChannels` is keyed by module NAME, not by agent/isolate/instance

Path: `glp_runtime/lib/runtime/runtime.dart`. The service directory the `#`
router consults is a plain `Map<String, GlpChannelHandle>` on **one** runtime
object:

```dart
/// GLP channel handles: module name → GlpChannelHandle.
/// Registered by activateModule() (Phase 4). Used by the runner's
/// Distribute/Transmit opcodes to route RPCs via GLP channels
/// instead of Dart-level dispatch.
final Map<String, GlpChannelHandle> glpChannels = {};
```

**Load-bearing:** the key space is *module names* (strings), with no agent id,
isolate id, or endpoint address. There is no slot in which a "remote instance"
could be named. Routing is name → in-heap channel-writer, all within `rt`.

### 1b. `GlpChannelHandle.send` is a local `heap.bindVariable` — no wire, no isolate

Path: `glp_runtime/lib/runtime/glp_activation.dart`. The channel "send" is the
writer-MGU on a stream tail in the **same heap** (`final HeapFCP _heap;`):

```dart
List<GoalRef> send(Term goal) {
  final (tailWriterAddr, _) = _heap.allocateVariable();
  final consCell = StructTerm('.', [goal, VarRef(tailWriterAddr)]);
  final activations = _heap.bindVariable(_writerAddr, consCell);
  _writerAddr = tailWriterAddr;
  return activations;
}
```

`activateModule(...)` constructs the channel writer/reader pair with
`rt.heap.allocateVariable()`, stores the `ModuleTerm` with `rt.heap
.storeTermOnHeap(...)`, enqueues the `serve` goal on `rt.gq`, and registers
`rt.glpChannels[moduleName] = channel`. **Every artifact lives on the one
`rt`/`rt.heap`/`rt.gq`.** The activation comment is explicit: *"The caller must
drain the scheduler to execute the spawned serve goal."* — i.e. the cross-context
dataflow is the **single in-process scheduler**, the very assumption the link
layer is told to relax.

### 1c. The `Distribute` / `Transmit` opcodes route inside `cx.rt`

Path: `glp_runtime/lib/bytecode/runner.dart` (the runtime compilation of
`RemoteGoal`/`M # goal`). Both opcodes look up the channel on `cx.rt`,
`send` on the same heap, and re-enqueue woken goals on the same scheduler:

```dart
// Distribute (static import index):
final glpChannel = cx.rt.glpChannels[target.name];
if (glpChannel != null) {
  final goalTerm = StructTerm(op.functor, args);
  final activations = glpChannel.send(goalTerm);
  for (final act in activations) {
    cx.rt.enqueueReactivatedGoal(act);
  }
} else {
  print('ERROR: Distribute: module ${target.name} not activated ...');
}
```

```dart
// Transmit (dynamic, module name resolved from a variable at runtime):
final glpChannel = cx.rt.glpChannels[moduleName];
if (glpChannel != null) {
  final goalTerm = StructTerm(op.functor, args);
  final activations = glpChannel.send(goalTerm);
  for (final act in activations) {
    cx.rt.enqueueReactivatedGoal(act);
  }
} else {
  print('ERROR: Transmit: module $moduleName not activated ...');
}
```

**Load-bearing:** the goal's arguments (`args`) are heap `VarRef`s on `cx.rt
.heap`; the result variable inside the goal term is the *same heap cell* the
caller holds. `send` just splices that goal onto the target's in-heap stream
tail. The error path is "module not activated (no GLP channel)" — a missing
*local* registry entry, never a "remote dial" path. **An unreachable instance is
not even a representable concept here.**

### 1d. `'_activate'` spawns the target procedure on the SAME runtime

Path: `glp_runtime/lib/runtime/body_kernels.dart`, `activateKernel`. It extracts
the `ModuleTerm`'s `BytecodeProgram`, finds the procedure label, and:

```dart
final newGoalId = rt.nextGoalId++;
final env = CallEnv(args: argSlots);          // argSlots are VarRefs on rt.heap
rt.setGoalEnv(newGoalId, env);
rt.setGoalProgram(newGoalId, bytecode);
if (!rt.runners.containsKey(bytecode)) {
  rt.runners[bytecode] = BytecodeRunner(bytecode);
}
rt.gq.enqueue(GoalRef(newGoalId, entryPc));    // same scheduler
```

The kernel's own doc comment states the fidelity-critical property that makes the
result variable shared rather than copied: *"This direct dispatch preserves
argument polarity (writer/reader), which is essential for output parameters … For
VarRef args (e.g., unbound writers for output params), storeTermOnHeap returns
the existing heap address, preserving writer/reader polarity."* This is exactly
why `#` cannot today cross an isolate: the output parameter is a **heap address
in `rt.heap`**, meaningless in another isolate's heap.

### 1e. Code-level proof of disjointness (negative evidence)

- `runner.dart` (the `#` Distribute/Transmit path): **0** occurrences of
  `MadContext`, `isolate`/`Isolate`, `onMessageReady`, `global_send`,
  `handleMadAssignment`, `agentId`.
- `glp_activation.dart` (the `#` activation path): **0** occurrences of
  `MadContext`, `isolate`/`Isolate`, `onMessageReady`, `global_send`.
- `mad_context.dart` (the madGLP cross-isolate path): **0** occurrences of
  `glpChannels`, `Distribute`, `Transmit`, `serve`, `_activate`,
  `GlpChannelHandle`.

The two subsystems do not reference each other anywhere. They are independent.

---

## SOURCE 2 (precedence: glp-current, authoritative spec) — madGLP isolate boundary is a DIFFERENT, Dart-mediated mechanism, and remote instances are explicitly OUT OF SCOPE

Path: `docs/ma/isolate-boot-spec.md` (v0.6, "Updated for madGLP").

### 2a. Inter-isolate routing is Dart-level by agent id — not GLP `#`

> **Key design principle**: The Dart runtime handles all inter-isolate routing.
> There is no GLP-level network switch — messages are routed by Dart based on the
> destination agent ID. (§1)

> The `network3` procedure (GLP-level network switch) is not needed — Dart
> handles all inter-isolate routing via cold-calls. (§10 note)

Routing across isolates is done by `IsolateManager.routeMessage(from, to, msg)`
over Dart `SendPort`s (§3.2), keyed by **agent id**, carrying serialized
`NetworkMsg` frames — a transport completely distinct from the in-heap GLP
channel the `#` router uses.

### 2b. Remote / network-distributed agents are explicitly unsupported

> ## 11. Future Extensions (Out of Scope for v0.5)
> The following are explicitly **not supported** in this version:
> 1. **Dynamic spawning**: Using `@` at runtime (not just boot)
> …
> 4. **Remote isolates**: Network-distributed agents

So even the madGLP isolate mechanism is, today, **in-process Dart isolates only**
(same OS process, `dart:isolate`), not across machines/REPL instances. The `#`
dispatch is one level more local still — same isolate, same heap.

### 2c. The boundary that *does* cross isolates is the global link, not `#`

Per `docs/ma/madGLP-spec.md` v5.3 (corpus entry 00) and `mad_context.dart`
(corpus entry 07): a maGLP shared pair `(X, X?)` spanning agents is split into
two fully-local FCP pairs joined by a **global link** = a `global_send` goal at
the writer side + a `W_p` table entry at the reader side, carried by serialized
**assignment messages** (`_w(p,i):=T↑` / `_r(p,i):=T↑`). The transport seam is
the `MadContext` `onMessageReady` callback (outbound) and `handleMadAssignment`
(inbound). **The `#` router does not produce or consume any of these.** A `#`
call never globalizes a variable, never allocates a global name, never touches
`W_p`.

---

## SOURCE 3 (precedence: glp-current) — the third option in the design space: static linking erases `#` entirely

Path: `glp_runtime/lib/compiler/project_linker.dart`. The static-link alternative
to runtime `#` dispatch flattens all modules into **one Program AST at compile
time**, resolving inter-module calls to renamed local procedures:

> Given a project root directory, discovers all modules, type-checks each
> independently, then produces a single flat Program AST where all inter-module
> calls are resolved to renamed local procedures.

This is the opposite extreme: no channels, no `serve`, no isolates — a single
monolithic program. It is irrelevant to distribution (it removes the boundary
rather than crossing it), but it bounds the design space: today GLP offers
(a) static link = one program, no boundary; (b) `#` dynamic dispatch = one
runtime/heap, in-heap channel boundary; (c) madGLP isolate links = cross-isolate
boundary via global links + Dart routing. **Only (c) crosses an isolate, and it
is NOT `#`.**

---

## Synthesis — what distributing `#` requires (B2 implications)

The `#` mechanism and the madGLP cross-isolate mechanism are **structurally the
same idea at two different scopes**: both route a *goal term* over a channel into
a *service loop* in another context, and both rely on a *shared variable inside
the goal term* to return the result (`F` in `factorial(5, F)`). The difference is
the channel's reach:

| | `#` dynamic dispatch (today) | madGLP global link (today) | distributed `#` (required) |
|---|---|---|---|
| Boundary crossed | none — same `rt.heap` | Dart isolate (same process) | runtime/REPL instance (any host) |
| Channel transport | in-heap `heap.bindVariable` on stream tail | `SendPort` (Dart), `onMessageReady` callback | a multi-protocol link primitive (MQTT/AMQP/WS/BLE/…) |
| Result variable | same heap cell (polarity preserved by `_activate`) | split local pairs + global link (globalize/localize) | split local pairs + global link over the wire |
| Address key | module **name** (`rt.glpChannels`) | agent **id** (`IsolateManager`) | instance/endpoint **address** |
| Suspension/wake | `enqueueReactivatedGoal` on same scheduler | `bindVariable` + `enqueueReactivatedGoal` on *local* scheduler after receive | local writer-MGU on receive, per-side scheduler |

**To make `M # goal` transparently target a remote instance, the link layer must
bridge (b) onto (c):**

1. **Replace the `#` channel transport.** `GlpChannelHandle.send` (an in-heap
   `bindVariable`) must become a transport-backed send when the target is remote.
   `rt.glpChannels` must gain a remote-addressed value kind (transport-backed
   handle) and the key space must admit instance/endpoint addresses, not just
   module names.

2. **Carry the goal's variables, not just the goal.** Today `_activate`'s
   correctness rests on `storeTermOnHeap` returning the *existing heap address* so
   the caller's reader `F?` and the callee's writer `F` are one cell. Across
   instances that is impossible — the goal term's variables must be **globalized**
   on send (polarity-correct: writer→`W_p` entry at globalizer; reader→
   `global_send` goal at globalizer, per madGLP §5.1 v5.3) and **localized** on
   receive, so the remote `serve`/`_select` sees fresh local pairs joined back by
   global links. In effect, the `#` router must invoke the madGLP globalize/
   localize machinery on the goal term before/after the wire — *unifying the two
   subsystems that are disjoint today.*

3. **Run a remote `serve/2` loop.** The far side needs a `serve(Module, In?)`
   loop reading the link's reader end (the spec-§3.4 service loop), with the link
   delivering localized goal terms to it — i.e. the link's reader end is wired to
   that `serve`'s input stream exactly as `activateModule` wires the local one,
   but fed by the transport's receive path instead of an in-heap writer.

4. **Preserve the fidelity invariants (B2).** Per corpus 00/07: the **heap cell
   model, writer-MGU, suspension/reactivation, SRSW are unchanged**; remote-ness
   lives only in `W_p` + `global_send` + serialized global names; the remote side
   performs a **purely local writer-MGU** (`heap.bindVariable`) and the wire
   carries only globalized terms, never the binding act. Correctness needs
   **per-peer FIFO + monotonicity** (corpus 00 §13). A distributed `#` must obey
   these too — it cannot RPC-copy the result back (that breaks writer-MGU and
   three-valued unification).

**Bottom line for the feature.** The `#` dispatch is the GLP-native RPC seam
(corpus 08) but is in-heap/in-isolate only; the madGLP global link is the
GLP-native *cross-boundary* seam (corpus 00/07) but is invoked by `@`-boot agents
and Dart isolate routing, **not** by `#`. The multi-protocol link layer's job, to
"split a writer X and reader X? across two REPL instances," is precisely to make
`#` (or a new link primitive with `#`-like ergonomics) route over the madGLP
global-link semantics carried by a chosen transport. The bridge between the two
subsystems is the unbuilt work; both halves' semantics already exist and must be
preserved exactly.

## Caveats / drift to respect

- The `#` activation plan (`dynamic-dispatch-implementation-plan.md`) is staged
  Phases 1–5; the authoritative routing semantics are in `docs/type
  system/dynamic-module-dispatch.md` (spec §3.4/§3.6/§6, see corpus 08), which
  supersede the plan on `serve/2`'s `ground(Module?)` guard and the bare-goal
  (no `export(...)` wrapper) message form.
- `isolate-boot-spec.md` is v0.6 DRAFT and pins "Remote isolates" as future work;
  the madGLP spec (corpus 00) is v5.3 DRAFT. Both are `glp-current` (HIGHEST
  precedence) for present implementation truth; neither is overridden by external
  Shapiro/FCP papers, which are mechanism inspiration only.
- No external source was fetched: the question is about the *local* implementation
  state (which two local subsystems exist and whether they meet). The
  authoritative answer is in the local code + specs above; an external paper would
  describe the ideal, not glpnet's current bridge (which is the point of the
  question — the bridge does not yet exist).

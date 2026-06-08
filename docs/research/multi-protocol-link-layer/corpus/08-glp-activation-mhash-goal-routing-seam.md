---
title: "M#goal Runtime Routing Seam — glp_activation.dart + Dynamic-Dispatch Implementation Plan (routing) + Dynamic Module Dispatch spec"
authors: "GLP/glpnet maintainers (Gabi / vonwenm); FCP precedent: E. Shapiro et al."
year: "2026"
source_url: "local: D:/bstdev/research/glp/glpnet/glp_runtime/lib/runtime/glp_activation.dart ; D:/bstdev/research/glp/glpnet/docs/modules/dynamic-dispatch-implementation-plan.md ; D:/bstdev/research/glp/glpnet/docs/type system/dynamic-module-dispatch.md"
retrieved: 2026-06-06
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Fetch, preserve & extract source: glp_activation.dart + dynamic-dispatch-implementation-plan.md (# routing)"
precedence_class: glp-current
access: full-text
---

# M#goal Runtime Routing Seam — the GLP-native RPC mechanism to extend across instances

## Why this matters for the link layer (B2 fidelity)

The multi-protocol link layer must split a program where a **writer `X`** and a
**reader `X?`** communicate through one shared logic variable inside a single
instance, and carry the binding across two (eventually N) runtime/REPL
instances. GLP **already has** a runtime mechanism that moves goals between two
execution contexts through a GLP channel and binds results back across that
boundary: the `M # goal` cross-module dispatch. It routes a goal term over a
**standard GLP stream** (writer/reader channel) to a target module's `serve/2`
service loop, where the goal executes in the target's context and the writer
arguments of the goal are bound back to the caller's readers via the **shared
logic variable that travels inside the goal term**.

This is the existing GLP-native RPC seam to **extend across instances**: today
the two contexts are two modules in one runtime; the link layer generalises the
*channel transport* from an in-heap stream to a remote transport, while
preserving the channel/writer-MGU/suspension semantics exactly. The crucial
fidelity point for B2: in the current design the result variable (`F` in
`factorial(5, F)`) is **not copied** — the caller holds reader `F?` and the
callee binds writer `F`; they are the *same* heap variable. Any distributed
scheme must reproduce this single-binding-once semantics across the wire (it
cannot just RPC-copy the arg back, or it breaks writer-MGU and three-valued
unification).

---

## SOURCE 1 (precedence: glp-current, authoritative) — `glp_activation.dart`

Path: `glp_runtime/lib/runtime/glp_activation.dart`. This is Phase 4 of the
dynamic-dispatch plan: GLP-level module activation. It creates a GLP channel,
spawns `serve(Module, ChannelReader?)`, and returns a handle whose writer end is
used by Phase-5 RPC routing.

### Channel handle = the writer end of a GLP stream (verbatim)

```dart
/// Handle for a GLP module channel.
///
/// Holds the writer end of the channel for sending goal terms.
/// Each [send] call extends the stream: [goal | newTail].
class GlpChannelHandle {
  final HeapFCP _heap;
  int _writerAddr;

  GlpChannelHandle(this._heap, this._writerAddr);
  ...
  /// Send a goal term on the channel.
  ///
  /// Binds current writer to [goal | newTail], advances writer to newTail.
  /// Returns goals woken up by the injection (must be enqueued by caller).
  List<GoalRef> send(Term goal) {
    final (tailWriterAddr, _) = _heap.allocateVariable();
    final consCell = StructTerm('.', [goal, VarRef(tailWriterAddr)]);
    final activations = _heap.bindVariable(_writerAddr, consCell);
    _writerAddr = tailWriterAddr;
    return activations;
  }

  /// Close the channel (bind writer to nil / empty list).
  List<GoalRef> close() {
    return _heap.bindVariable(_writerAddr, ConstTerm('nil'));
  }
}
```

**Load-bearing extraction (for B2):**
- A channel is just a **logic-variable writer cell** (`_writerAddr`) over a heap
  stream. `send(goal)` performs `bindVariable(writer, [goal | newTail])` and
  advances the writer to `newTail` — i.e. it is the **writer-MGU on the stream
  tail**, exactly the atomic writer/reader pair, not a side channel.
- `bindVariable` returns the list of **goals woken up by the injection**
  (`List<GoalRef> activations`) — this is the **suspension/reactivation**
  mechanism surfacing at the channel boundary: a `serve` loop suspended on the
  unbound stream-tail reader reactivates when the writer binds. The caller "must
  enqueue" these. A remote transport must reproduce this wake-on-bind, not just
  deliver bytes.
- `close()` binds the writer to `nil` — stream termination is an ordinary
  variable binding (`ConstTerm('nil')`), consumed by `serve(_, [])`.

### Activation sequence (verbatim, numbered as in source)

```dart
GlpChannelHandle activateModule({
  required GlpRuntime rt,
  required BytecodeProgram serveBytecode,
  required BytecodeProgram moduleBytecode,
  required String moduleName,
}) {
  // 1. Create GLP channel (writer/reader pair)
  final (writerAddr, readerAddr) = rt.heap.allocateVariable();

  // 2. Construct ModuleTerm and store on heap
  final moduleTerm = ModuleTerm(moduleBytecode, name: moduleName);
  final moduleAddr = rt.heap.storeTermOnHeap(moduleTerm);

  // 3. Spawn serve(Module, ChannelReader?)
  final goalId = rt.nextGoalId++;
  final env = CallEnv(args: {0: VarRef(moduleAddr), 1: VarRef(readerAddr)});
  rt.setGoalEnv(goalId, env);
  rt.setGoalProgram(goalId, serveBytecode);
  final servePc = serveBytecode.labels['serve/2']!;
  rt.gq.enqueue(GoalRef(goalId, servePc));

  // 4. Tag as infrastructure goal (spec §3.4, §3.5)
  rt.infrastructureGoalIds.add(goalId);

  // 5. Register serve runner if not already present
  if (!rt.runners.containsKey(serveBytecode)) {
    rt.runners[serveBytecode] = BytecodeRunner(serveBytecode);
  }

  // 6. Register channel handle in rt.glpChannels for RPC routing (Phase 5)
  final channel = GlpChannelHandle(rt.heap, writerAddr);
  rt.glpChannels[moduleName] = channel;

  // 7. Return channel handle (writer end)
  return channel;
}
```

**Load-bearing extraction:**
- The **writer/reader pair** (`allocateVariable()`) is the atomic unit: the
  caller-side keeps `writerAddr` (in the `GlpChannelHandle`); the callee-side
  `serve` goal is spawned reading `readerAddr` (`args: {1: VarRef(readerAddr)}`,
  i.e. `ChannelReader?`). **One writer, one reader — SRSW preserved at the
  channel seam.**
- The target context is identified by a `ModuleTerm` (compiled bytecode wrapped
  as a `Module` constant) stored on the heap and passed as arg 0 of `serve`.
- `rt.glpChannels[moduleName] = channel` is the **service directory**: the
  registry keyed by name that Phase-5 RPC routing uses to find the writer end.
  This is the natural attach-point where a remote endpoint registry would hook a
  *transport-backed* channel handle keyed by remote module/instance address.
- The `serve` goal is enqueued on the scheduler (`rt.gq.enqueue`); the comment
  on `send` ("must be enqueued by caller") confirms the cross-context dataflow
  is driven by the **scheduler**, which is exactly the single-thread assumption
  the link layer is told to relax.

---

## SOURCE 2 (precedence: glp-current) — dynamic-dispatch implementation plan, routing sections (Phase 4 + Phase 5)

Path: `docs/modules/dynamic-dispatch-implementation-plan.md`. The `# routing`
content is Phase 4 (activation) and Phase 5 (RPC routing via GLP channels).

### Phase 4 — Module activation via GLP (verbatim activation sequence)

> **What:** When a module is loaded, the runtime activates it by creating a GLP
> channel and spawning `serve(Module, In?)` on the read end. This replaces the
> Dart `Dispatcher`.
>
> **Activation sequence:**
> 1. Load and compile the module binary.
> 2. Create a GLP channel: `new_channel(Ch)`, yielding a writer/reader pair.
> 3. Obtain the module handle (a runtime reference to the compiled module).
> 4. Spawn: `serve(ModuleHandle, ChannelReader?)`.
> 5. Register the channel writer in the module registry, keyed by module path.
> 6. For monitor modules (`-monitor(Name)`), spawn the programmer-written
>    monitor procedure instead of `serve`.

### Phase 5 — RPC routing via GLP channels (verbatim)

> **What:** When a module executes `M # goal(...)`, the runtime wraps the goal
> as `export(goal(...), L, R)` and sends it on `M`'s GLP channel, rather than
> through Dart streams.
>
> **RPC compilation:** Currently `RemoteGoal` nodes are compiled to Dart-level
> dispatch. Change to:
> 1. Build the goal term from the `RemoteGoal`'s inner goal.
> 2. Send the goal term on the target module's channel writer:
>    `send(GoalTerm, ChannelWriter)`.
>
> **RPC resolution:** When the target module is not yet loaded, the runtime
> loads and activates it (Phase 4) before sending the message. The channel
> writer is obtained from the module registry.
>
> **Detail — the send:** This uses GLP's existing `send` defined guard (in body
> position) to write to the channel. The message flows on a standard GLP stream,
> consumed by the target module's `serve` loop.

**Load-bearing extraction:**
- `M # goal` compiles to **`send(GoalTerm, ChannelWriter)`** on the target's
  channel writer obtained from the registry. There is no special RPC wire format
  at the GLP level — it is **an ordinary stream send** of the goal term. (The
  plan's `export(goal, L, R)` wrapper is the older FCP-flavoured form; the
  shipped spec §3.6 drops the wrapper and sends the bare goal — see Source 3.)
- **Lazy activation:** unknown target → load+activate (Phase 4) → then send.
  This is the model for "connect-on-first-use" to a remote instance.
- **Routing is dynamic dispatch** at runtime, contrasted explicitly with static
  linking ("This replaces the current Dart-level `Dispatcher` with GLP-level
  dispatch"). The plan's §1 Goal: "the compiler generates a `_select/2` dispatch
  table from exported procedures, the `'_activate'` body kernel resolves goals
  against a module's dispatch table, and a GLP system predicate `serve/2`
  provides the stream-consuming loop. This replaces the current Dart-level
  `Dispatcher` with GLP-level dispatch, following FCP's architecture."

### The service loop `serve/2` (verbatim, Phase 3 of the plan)

```glp
-mode(system).

procedure serve(_, Stream?).

serve(Module, [Goal | In]) :-
    true |
    '_activate'(Module?, Goal),
    serve(Module, In?).

serve(_, []) :-
    true |
    true.
```

> **Detail — message format:** Each message on the module's input stream is a
> goal term (e.g., `factorial(5, F)`) — the remote procedure call sent directly,
> with no wrapper.

---

## SOURCE 3 (precedence: glp-current, authoritative spec) — Dynamic Module Dispatch spec §3.4, §3.6, §6

Path: `docs/type system/dynamic-module-dispatch.md` (v1.2). This is the
*specification* the plan implements; it is the single source of truth for the
routing semantics and supersedes the plan where they differ (e.g. bare goal vs
`export(...)` wrapper; `ground(Module?)` guard).

### §3.4 The Module Service Loop (verbatim — the authoritative `serve/2`)

```glp
serve(Module, [Goal | In]) :-
    ground(Module?) |
    '_activate'(Module?, Goal?),
    serve(Module?, In?).

serve(_, []) :-
    otherwise |
    true.
```

> This is a system predicate — it uses the `'_activate'` body kernel and is not
> written by the programmer. The `ground(Module?)` guard is required by SRSW:
> `Module` is read twice in the body (by `'_activate'` and the recursive `serve`
> call), so a grounding guard must establish that it is a constant value. The
> core pattern is: read, dispatch, recurse.

### §3.6 Remote Procedure Call Routing (verbatim)

> When a module executes a cross-module call `M # p(X?, Y)`:
> 1. The runtime looks up `M`'s channel in the domain's service directory.
> 2. If `M` is not yet loaded, the runtime loads and activates it (Section 3.5).
> 3. The goal term `p(X?, Y)` is sent on `M`'s channel.
> 4. `M`'s service loop reads the goal and dispatches via `_select/1`.
> 5. The exported procedure `p` executes within `M`'s context.

### §6 The Complete Dispatch Chain (verbatim — the end-to-end trace, B2-critical)

> 1. **Caller** executes `M # factorial(5, F)`.
> 2. **RPC routing.** The runtime looks up `M`'s channel and sends the goal term
>    `factorial(5, F)` on it.
> 3. **Service loop.** `M`'s `serve` (or programmer-written monitor) reads
>    `factorial(5, F)` from the input stream.
> 4. **Dispatch.** The loop calls `'_activate'(Module?, factorial(5, F))`.
> 5. **`_select` resolution.** The runtime resolves `factorial(5, F)` against
>    the compiled `_select/1` table:
>    ```glp
>    _select(factorial(N, F)) :- factorial(N?, F).
>    ```
> 6. **Procedure execution.** `factorial(5, F)` executes within the module's
>    context. **The `F` argument connects the caller and callee — the caller
>    holds a reader `F?` that will be bound when the callee unifies `F`.**
> 7. **Recursion.** The service loop recurses on the tail of the input stream,
>    ready for the next call.

**Load-bearing extraction (B2 fidelity yardstick):**
- **Step 6 is the whole point for the link layer.** The result is carried by a
  *shared logic variable inside the transmitted goal term*: caller has reader
  `F?`, callee binds writer `F`. The "RPC" is not request/response copying — it
  is a one-shot **writer-MGU on a variable that spans both contexts**. The
  caller's goal **suspends on `F?`** until the callee binds it (three-valued
  unification: Suspend → Success). Across instances, the link primitive must
  transport not just the goal but the **identity of `F`** so that a remote bind
  reactivates the suspended caller — this is exactly distributed unification
  (B2), and the in-process channel mechanism is its fidelity reference.
- Routing is **name → channel-writer** via the service directory (§3.6.1); the
  link layer generalises this to **name/address → transport-backed
  channel-writer**.
- `_select/1` is **pure head matching, no guards** (spec §3.2, v1.2 note):
  "Each clause matches on the goal term's functor and arity, then calls the
  corresponding exported procedure. No guards are needed — the dispatch is
  entirely in the head unification." Dispatch is a unification, consistent with
  the rest of the model.

### §3.2 note — GLP omits FCP's Controls argument (verbatim)

> **Note:** FCP's `_select/2` carried a second `Controls` argument for the
> termination circuit (`procedures(L, R, V)`). GLP omits this — the termination
> circuit is an FCP-specific mechanism that GLP does not use. If a control or
> signaling mechanism is needed in the future, it can be added as an additional
> argument at that time.

---

## FCP precedent recorded by the spec (precedence: earlier-cl-paper / mechanism inspiration only)

The dispatch spec §2 documents the FCP architecture the GLP design follows
(three layers; quoted here for traceability, NOT overriding GLP semantics):

- **Compiler layer** (`control/self.cp`): generates `_select/2` with one clause
  per export, e.g. `_select(export(p(A,B)), Controls) :- Controls = procedures(L, R, V) | p(A, B, L, R, V).`
- **Runtime layer** (`reserved_text.cp`): the `activate/3` primitive resolves a
  goal against a module binary; `module(Module)` ask-guard verifies a valid
  compiled binary.
- **Service layer** (`domain_server.cp`): `in_server` is the stream-consuming
  loop; `layer_goals` calls `activate(Module, export(Goals), procedures(...))`
  and recurses via `self`.

GLP's adaptations (spec §3.1, verbatim): "**Body kernels replace tell kernels.**
… **Typed channels replace untyped streams.** … **The service loop is regular
GLP.** … **Load-time type verification.**"

---

## Synthesis — implications for the multi-protocol link layer (B2)

1. **The seam already exists and is GLP-native.** `M # goal` is dynamic dispatch
   over a GLP channel (writer/reader stream) into a `serve/2` loop. To go
   multi-instance, replace the *transport* of that channel (today: in-heap
   `bindVariable` on a stream tail) with a remote transport, while keeping
   `serve/2`, `_activate`, `_select/1`, and the writer/reader discipline intact.

2. **The result travels as a shared variable, not a copy.** The fidelity
   yardstick: caller reader `F?` and callee writer `F` are the same variable;
   binding one reactivates the suspended other. A new link primitive must
   distribute *this* atomic writer/reader pair (the prompt's core transform),
   transporting variable identity + the bind event + the wake, not request/reply
   payloads. This is precisely B2 (distributed unification).

3. **Registry = attach point for remote endpoints.** `rt.glpChannels[name]`
   (Dart) / "domain's service directory" (spec) maps a name to a channel writer.
   The link layer extends the key space to remote instance/endpoint addresses
   and the value to transport-backed channel handles, leaving routing logic
   unchanged.

4. **Suspension/reactivation is surfaced at the channel boundary.**
   `bindVariable` returns the woken goals (`activations`) that the caller must
   enqueue; a remote transport must reproduce wake-on-bind across the wire, or
   suspended goals will never reactivate.

5. **Lazy activation models connect-on-first-use.** Phase 5 / spec §3.6.2:
   unknown target → load+activate → send. The remote analogue is
   dial/handshake-on-first-link.

6. **`serve/2` is the per-instance service loop.** Splitting one program across
   two instances (writer-node vs reader-node) maps onto: the reader-node runs a
   `serve`-like loop on the link's reader end; the writer-node holds the link's
   writer end and `send`s. The "one program parameterized by a per-instance
   role goal" framing aligns with choosing which end of each link a given
   instance owns at boot — exactly how `activateModule` assigns writer vs reader
   to caller vs spawned `serve`.

## Caveat / spec-vs-plan drift to respect

- Authoritative `serve/2` uses `ground(Module?)` guard (spec §3.4); the plan's
  draft used `true |`. Spec wins.
- Authoritative message is the **bare goal term** with no wrapper (spec §3.6,
  Phase-3 detail); the plan's Phase-5 `export(goal, L, R)` wrapper is the older
  FCP-flavoured form and is superseded.
- `_select` is **`/1`** with pure head matching in current GLP (spec v1.1/v1.2),
  not FCP's `/2`-with-Controls.

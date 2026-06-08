# Investigation — Persistent, Embeddable GLP Engine (REPL ↔ Engine Separation over a Binary Wire Protocol)

**Epic:** `epic-separation-of-repl-front-end-from-engine-execution-scheduler`
**Feature:** `repl-engine-split-mvp-binary-wire-format-intermediate-language-c`
**Authored:** 2026-06-08. READ-ONLY design + feasibility synthesis of six multi-agent facet reports (frontend, engine, IL, interchange+wire, control/clients/liveness/mailbox, persistence/state). Marathon step 1 deliverable per `feature-definition.md` §8.

This report answers the feature in `feature-definition.md`. It is the input to marathon step 2 (the refactoring design) and step 3 (fleshing out the buildkit feature(s)). All claims are grounded in `out/csharp` (C#-first reference) and `csharp/glp_link` (feature-025 link layer), cross-checked against `glp_runtime` (Dart source-of-conversion).

---

## 0. Executive summary

The current C# implementation is **closer to the target than the prose suggests**, but in a way that reframes three requirements:

1. **The split is already half-done structurally.** `GlpEngine` (`out/csharp/lib/engine/glp_engine.cs:127`) is an extracted, embeddable execution core; its own docstring (lines 5-17) calls it "the ONE way to run GLP programs," used by the REPL, the IsolateManager, and tests. The REPL is a thin console loop (`out/csharp/bin/glp_repl.cs:130-371`). **But the seam is not a process boundary and not a wire — it is in-process C# method calls sharing one `Term`/`BytecodeProgram`/heap object graph.** The whole job is to turn that facade into a wire.

2. **Two requirement premises do not match the code as-built and force an explicit design decision:**
   - The requirements say "the **parser lives in the front-end**" and "client → engine = **compiled IL**." In the code, the **Lexer/Parser/TypeChecker/Compiler are entirely engine-internal** — `GlpEngine.RunGoalAsync` takes a **raw goal string** and compiles it itself (`glp_engine.cs:349`, `:487-493`); `LoadSource` takes **source text** (`glp_engine.cs:251`). So "client sends compiled IL" is a **real refactor of where the compiler lives**, not an existing contract to wire up. §2a (ANTLR4, factor out the compiler) is the lever that resolves this.
   - The requirements name "engine generates new IL at runtime." Repo-wide, **no bytecode is synthesized at runtime** — every `.Compile`/`GenerateWithMetadata` call site is inside `glp_engine.cs` and always takes a **source string** (IL report finding (d)). What looks like runtime IL generation is **runtime goal-term assembly + dispatch against pre-compiled bytecode** via `_activate` (`body_kernels.cs:1015`) against a `ModuleTerm`-wrapped `BytecodeProgram` stored on the heap (`glp_activation.cs:88-89`, `terms.cs:146`). This is load-bearing for persistence: **compiled programs circulate as runtime heap data**, so any state snapshot must serialize `BytecodeProgram` instances, not treat code as a static side table.

3. **Three capabilities are entirely net-new — there is ZERO of them today:** (a) any IL/bytecode wire codec; (b) any OS-liveness / crash-signal / watchdog machinery; (c) any engine-state serialization or persistence path. The repo *does* have the right reusable substrates for each: feature-025's `FrameCodec`/`TcpTransport`/`LinkPump` (transport+framing), `PayloadSerializer` (a term-tree codec), and the codeconv `MarathonStore` (PGLite-primary + JSON-fallback behind one API, monotonic `sequence_no`) — but none is wired to the engine for these purposes.

**Recommended first MVP slice:** a one-engine / one-REPL-client process split over feature-025 `TcpTransport`+`FrameCodec` (loopback), where the engine compiles (the compiler stays engine-side for the MVP), the wire carries **source text client→engine** and a **new structured result envelope engine→client**, the engine bootstraps from `self.glp`, and persistence is a **snapshot-at-quiescence** of the heap + goal queue + suspension index + loaded `BytecodeProgram`s behind a `MarathonStore`-shaped API. Defer multi-client, compiled-IL-on-the-wire (and thus ANTLR4/C++/LLVM), and the in-flight-request replay to follow-up features. Rationale in §8.

---

## 1. THE SEAM — front-end/client vs embeddable engine+scheduler

### 1.1 What the front-end is (and owns)

The front-end is two files: the 38-line composition-root shim `out/csharp/glp_repl/Program.cs` (the only place allowed to reference both `glp_runtime_net` and `GlpLink`, `Program.cs:30-35`) and the converted REPL `out/csharp/bin/glp_repl.cs` (`Program.Main`, lines 98-371). It owns: console I/O + banner; the read-trim-dispatch loop (`glp_repl.cs:130-133`); the colon-command set (`:trace/:debug/:strict/:limit/:clear/:activate/:bytecode/:boot/:help/:quit`, lines 148-259); and **all result display** — `FormatTerm` (lines 432-584) renders terms/lists/doubles with heap-deref + cycle detection, and `PrintStatus` (379-388) maps status to `→ succeeds/failed/suspended`.

It owns **no** parsing, compilation, execution, scheduling, or suspension logic. There is **no pause state in the front-end** — a suspended goal is merely the status word `→ suspended`; the real suspend/re-drain machinery (feature-025 InboundPump loop) lives in the engine (`glp_engine.cs:551-569`, `705-724`). (Frontend report findings; "no pausing state in front-end".)

### 1.2 What the engine is (and owns)

`GlpEngine` (`glp_engine.cs:127`) is the embeddable core. The complete front-end→engine contract is the public surface of `GlpEngine` + `ExecutionResult` + `ExecutionStatus`:

- ctor `new GlpEngine(string rootSelfGlpPath)` (`glp_engine.cs:202`) — registers standard predicates, loads `programs/self.glp`, compiles the embedded `serve/2`.
- `Task<ExecutionResult> RunGoalAsync(string goalText)` (`:349`) — **the primary crossing**; takes a goal **string**, returns `ExecutionResult`.
- `bool LoadFile(string)` (`:238`) / `LoadSource(string,string?)` (`:251`) / `bool LoadProject(string,string?)` (`:328`).
- `void ActivateDynamicModule(string)` (`:373`); `void Clear()` (`:225`); scalar prop setters `MaxCycles/DebugTrace/DebugOutput/StrictTypes` (`:159-168`); `InboundPumpWait` (`:181`); getter `LoadedPrograms` (`:189`); static hook `AfterEngineCreated` (`:47`).

`ExecutionResult` (`glp_engine.cs:51-80`) has exactly **three** fields: `Status` (`ExecutionStatus`: Succeeded|Failed|Suspended, `scheduler.cs:33-43`), `Bindings` (`IReadOnlyDictionary<string,Term?>`, var-name→deref'd heap term, null=unbound), `Error` (`string?`).

### 1.3 What crosses each direction — and the leaks

**Client → engine (today):** a raw **goal string** (`RunGoalAsync`), or file paths/source text (`LoadFile/LoadSource/LoadProject`). **Not** compiled IL — the engine owns the full Lexer→Parser→TypeChecker→PartialEvaluator→Compiler pipeline internally (`glp_engine.cs:487-493`, `625-627`, `256-259`).

**Engine → client (today):** an `ExecutionResult` object whose `Bindings` values are **live heap `VarRef`s**, only meaningful relative to `engine.Runtime.Heap`. The front-end re-dereferences them during display (`glp_repl.cs:479,483,508,514,561` via `engine.Runtime.Heap.Dereference`/`IsReader`). **This is the seam's biggest leak:** the result is not self-contained; it is a pointer into engine-owned heap. A process split *requires* the engine to fully resolve/serialize result terms server-side (the engine already has the deep resolver `_ResolveDeepForTrace` at `glp_engine.cs:607-619` for feature-020 — the reusable basis for wire-result encoding).

**Components the engine computes but DROPS at the `ExecutionResult` boundary** (must be promoted to wire-encode engine→client per §3 requirements):
- **var-name → writer-id mapping** — `queryVarWriters` is built in `_SetupArgument` (`glp_engine.cs:515,988`) and handed to the scheduler for display (`:540`) but is **not** a field of `ExecutionResult`.
- **suspended-goal detail** — `DrainResult` (`scheduler.cs:58-91`) carries `SuspendedGoals` (formatted strings) and `BlockingReaders` (heap reader addrs, spec §8.4); the engine propagates **only `Status`**, dropping both.
- **captured/streamed output** — there is **no output field**; output is `Console.WriteLine` side effects (`glp_repl.cs`, `system_predicates_impl.cs:73,82`, `body_kernels.cs:933`) and out-of-band stream observation via `external_io.cs` `OutputObserver` (`:171-261`). Across a process boundary every `Console.WriteLine` is a leak; output must route through `GlpRuntimeEngine.OutputCallback` (`runtime.cs:135`) and `Scheduler.TraceSink` (`scheduler.cs:138`) and be framed onto the wire.

### 1.4 The natural cut point + a second seam to reckon with

The cleanest architectural cut is the **compiler→runner boundary**: `BytecodeProgram` is the sole artifact crossing from `GlpCompiler`/`CodeGenerator` into `BytecodeRunner`/`Scheduler` (`glp_engine.cs:534`, `runner.cs:452`). Everything downstream of `BytecodeProgram` is execution. This is exactly where §2a ("factor out the compiler") and §3 ("client sends compiled IL") want the split.

There is a **second front-end→engine seam** that bypasses `GlpEngine`: `:boot` → `RunBoot` (`glp_repl.cs:596-671`) drives `BootLoader` + `IsolateManager` directly (in-process isolates, `out/csharp/lib/multiagent/`). The multi-process/control-program model (§4) must decide whether it subsumes or replaces `IsolateManager` — out of MVP scope, but a known parallel mailbox/relay substrate (§6).

---

## 2. THE BINARY WIRE PROTOCOL

### 2.1 The IL is an in-memory object graph, not a byte array

`BytecodeProgram` (`runner.cs:41-123`) = `IReadOnlyList<object> Instructions` (heterogeneous **by design**, comment `runner.cs:39`) + `Dictionary<string,int> Labels` (derived at construction by `IndexLabels`, `:61-73`). Two opcode families coexist in one list: **v1 `IOp`** (`opcodes.cs`, ~50 classes: HEAD/GUARD/BODY/structure-traversal/control/scheduler/module-RPC) and **v2 `IOpV2`** (`opcodes_v2.cs`: unified reader/writer instructions with an `IsReader` bool; codegen emits v2 directly, `:209`), plus `Label` markers. Operands are primitives: `long` register/arg indices, `string` functors + label names, `bool` flags, and **`object?` constant Values** that can be **recursive** — codegen embeds a runtime `Rt.StructTerm` as a `UnifyConstant.Value` for ground lists (`codegen.cs:737-759`), so a constant encoder must be a recursive term sub-encoder, not scalar-only.

**There is NO IL/bytecode wire codec anywhere.** `opcodes.cs`/`opcodes_v2.cs`/`runner.cs` have zero `Serialize/Encode/ToBytes`. `ToDisassembly()` (`runner.cs:88`) is human-readable, not a format. `asm.cs` is a construction surface, not a (de)serializer. (IL report finding "NO IL/BYTECODE WIRE CODEC EXISTS"; interchange report same.)

### 2.2 What an IL codec must capture (both for wire client→engine AND for DB persistence)

(1) the ordered `Instructions` list with each opcode's discriminant + typed operands; (2) **both** opcode families in one stream; (3) the `Label` markers (or an equivalent label→index table — note `CombinedProgram` *mutates* the Labels dict for module-boundary filtering, `glp_engine.cs:455-460`, so the filtered set is engine state distinct from the raw program); (4) the companion `CompilationResult.VariableMap` (name→register, `result.cs:6`) — the basis for the engine→client var-name↔writer mapping §3 demands; (5) for persistence specifically: `ModuleTerm`-embedded `BytecodeProgram`s reachable from the heap, plus the `_loadedPrograms`/export/import-index registry.

### 2.3 Result encoding (engine → client) is a NEW envelope

`PayloadSerializer` (`out/csharp/lib/multiagent/payload_serializer.cs`) is a tag-byte term-tree codec (TagConstant=1/Variable=2/Struct=3/List=4; constant subtypes nil/int64/double/string/bool; var-length length prefix). It serializes runtime `Term` graphs **but**: it is keyed by `GlobalVarId(creator:localId)` for inter-agent messages, and **it THROWS on any unbound `VarRef`** (the feature-025 send path `LinkEgress.cs:29-36` rejects non-ground payloads; `MutualRefTerm`/`ModuleTerm` hit `NotSupportedException`, `payload_serializer.cs:447`).

So `PayloadSerializer` covers **only the bindings-VALUE sub-problem for ground terms**. It does **not** cover: `ExecutionStatus`, error strings, suspended-goal lists, output streams, the var-name→writer map, **unbound writers/readers in suspended results**, or opcodes. The **result envelope is net-new** and must define encodings for all of those, including how to represent a SUSPENDED result containing unbound variables (a first-class status that the only existing term codec cannot encode).

### 2.4 Reuse decision — feature-025 transport+framing YES, payload codec NO

**Reuse for transport + framing:** `FrameCodec` (`csharp/glp_link/reliability/FrameCodec.cs`) gives a version byte (0x01), a 22-byte big-endian header (Version/Kind/MessageId/TotalLen/FragIndex/FragCount/ChunkCrc/ChunkLen), CRC-32 per chunk, MTU fragmentation/reassembly, and a 64 MiB payload guard — it wraps an **opaque** payload and knows nothing about terms or opcodes, so it can frame an IL blob or a result envelope **unchanged**. `ILinkTransport`/`ILinkEndpoint` (`csharp/glp_link/seam/`) is the byte seam (`SendBytesAsync`/`RecvBytesAsync`); `TcpTransport` (`csharp/glp_link/transports/TcpTransport.cs`) is a working cross-process loopback transport (4-byte length-prefixed frames). **This stack is exactly what a REPL↔engine OS-level link needs and should be reused as-is.**

**Do NOT reuse `PayloadSerializer` as the wire payload codec.** It is ground-only and term-only. **Recommendation: a dedicated codec with two payload kinds** — (A) an **IL/bytecode codec** for the compiled-IL direction (a new opcode-discriminant + operand format, reusing a *recursive constant-term sub-encoder* that MAY share `PayloadSerializer`'s constant tag scheme), and (B) a **result-envelope codec** (status + bindings + var→writer map + suspended detail + output + errors). Both ride `FrameCodec` (distinguish by the frame `Kind` byte). Why dedicated rather than extending `PayloadSerializer`: opcodes are not terms; suspended results need unbound-variable encoding `PayloadSerializer` forbids by design (it would break the feature-025 ground-relay invariant if changed in place).

### 2.5 How runtime-generated IL crosses

Since the engine does not synthesize bytecode (§0.2), "engine-generated IL" on the wire means: a `ModuleTerm`-wrapped `BytecodeProgram` that lives on the heap (`terms.cs:146`, `glp_activation.cs:88`) may appear in a binding or be routed over a channel. `PayloadSerializer` cannot serialize a `ModuleTerm`. Therefore the **same IL codec (§2.4-A) must round-trip in BOTH directions** — the engine→client result encoder must be able to emit a serialized `BytecodeProgram` when a result term contains one. For the MVP, ground results dominate and `ModuleTerm`-in-binding can be excluded/erroed; the full bidirectional IL-on-wire is a follow-up (§8).

### 2.6 Cross-runtime parity caveat

`FrameCodec`/`PayloadSerializer` are stated byte-identical across Dart↔C# (FrameCodec remark FR-060/061); the Dart `ExecutionResult` is structurally identical (`glp_runtime/lib/engine/glp_engine.dart:34-36`). If the Dart mirror is kept (feature-definition §2a lists Dart as a target), the new IL + result codecs must be specified to the same byte-parity standard — a constraint the opcode v1/v2 split complicates.

---

## 3. CONTROL-PROGRAM STARTUP + CLIENT MODEL

### 3.1 The startup seam exists and is the right insertion point

`AfterEngineCreated` (`Action<GlpEngine>?`, `glp_repl.cs:47`) is invoked once right after `new GlpEngine(...)` (`:126`). `out/csharp/glp_repl/Program.cs:30-35` is the **sole composition root** that may reference both the engine library and `GlpLink` — it sets the hook to `LinkKernels.Install(engine.Runtime)` + register `TcpTransport`/`LoopbackTransport`. A pre-compiled control program (a GLP boot file) is loaded/run at exactly this seam. **Dependency rule FR-057 is load-bearing:** `glp_link → out/csharp` only; the engine library NEVER references the link layer (cycle avoidance). Any C# control-program/listener therefore lives in the exe/host, not the engine library.

### 3.2 Listen/accept is real; multi-accept is the blocker

GLP-level link primitives already exist: `LinkKernels` (`csharp/glp_link/primitives/LinkKernels.cs:59`) registers `_link_setup/_link_send/_link_request/_link_listen/_link_accept/_link_monitor/_link_close`. GLP wrappers in `programs/self.glp`: `request_listener(rendezvous(tcp,ep(127.0.0.1,Port)), Requests?)` (`:513-516`), `accept_link/4` (`:526`). `LinkListenKernel.cs:32-81` binds a rendezvous, parks the accepted endpoint in `LinkRuntime.Pending`, surfaces a `request(LinkId,FromPeer)` token; `LinkAcceptKernel.cs:26-49` adopts it. **KEY GAP:** `TcpTransport.ListenAsync` (`csharp/glp_link/transports/TcpTransport.cs:32-50`) accepts **exactly ONE** connection then `listener.Stop()` (comment line 47: "ONE link per listen for the base MVP ... a multi-accept loop is a transport-leaf concern, Phase 6"). **A one-engine/N-clients control program needs the deferred Phase-6 multi-accept loop first** (changing `ListenAsync` to yield many endpoints — `IAsyncEnumerable<ILinkEndpoint>` or a callback).

### 3.3 The control program/mailbox CAN be GLP — and the loop shape already exists

`serve/2` (`glp_engine.cs:135-136`: `serve(Module,[Goal|In]) :- ground(Module?) | '_activate'(Module?,Goal?), serve(Module?,In?). serve(_,[])`) is the **exact stream-dispatch control-loop shape** a GLP-written listener uses to read a request stream and activate each incoming goal. Combined with `request_listener` (accept stream), `Link(In,Out)` channels as per-client mailboxes (`self.glp:456`), `link_send`/`link_recv` (`self.glp:536,548`), and `mwm` multiway-merge for many-clients→one-engine fan-in (`self.glp:380-425`), **a GLP-written control program + per-client mailbox is concretely feasible on the link layer** — as the requirements hypothesize. It depends on (a) the multi-accept transport extension and (b) the wire carrying compiled IL (not just ground terms).

### 3.4 Multiple clients + Option-C

The engine is single-threaded, single-owner-heap by construction (`heap_fcp.cs:136-141`). **Multi-link is already solved by Option-B+Option-C:** `IInboundPump` (`inbound_pump.cs:20-37`) + `LinkPump` (`csharp/glp_link/primitives/LinkPump.cs`) run a **per-link background recv loop** that only touches a thread-safe inbox (never the heap); the runner thread applies frames via `TryApplyNext` (`LinkPump.cs:86-125`). So **N clients → N links → N background recv-loops → ONE shared inbox → ONE heap** is the already-ratified model. Heap safety is solved; multi-client at the transport level only awaits the multi-accept extension. The driver loops already service the pump (`glp_engine.cs:555-569`, `709-724`).

### 3.5 Recommendation (control program / client model)

For the **MVP**: a **C# host control program** (a `BackgroundService` that owns a one-accept `TcpTransport` listener) is simpler to ship and decouples the milestone from the multi-accept + compiled-IL-on-wire dependencies. The **target** is a **GLP-written control program** (serve/2-loop over `request_listener`, Link-channel mailboxes, `mwm` fan-in), enabled once multi-accept lands. The REPL is one client; a programmatic OS client is another process running `client_connector` + `link_send`/`link_recv` (precedent: `programs/tests/link/pc.glp` splits producer/consumer across two processes over 127.0.0.1:9100). Multi-client is feasible architecturally but should be a **follow-up feature**, not MVP.

---

## 4. OS-LIVENESS, CRASH SIGNALING & RESTART

**NONE exists today.** Grep across `out/csharp` and `csharp/glp_link` finds no heartbeat/watchdog/sd_notify/IHostedService/BackgroundService/Environment.Exit/systemd machinery; the only "liveness" hits are per-link timeout knobs (`LinkOptions.TempFailAfter=5s`, `ConnectTimeout`, `seam/LinkOptions.cs:39`) — link-fault knobs, not OS liveness. Transport faults become GLP lattice terms `ok/tempFail/permFail/closed` (`self.glp:451`) but never escalate to the OS. This whole capability is **net-new C# host work**.

**Recommended shape:**
- Wrap the engine in a `Microsoft.Extensions.Hosting` `BackgroundService`/`IHostedService` (the host layer, alongside the composition root, above the engine library per FR-057).
- **Liveness:** on Linux, `sd_notify` `READY=1` + periodic `WATCHDOG=1`; on Windows, run as a Windows Service reporting status to the SCM; cross-platform fallback = a heartbeat file/socket the supervisor polls.
- **Crash signal:** a distinguished non-zero exit code on unrecoverable state so the supervisor (systemd `Restart=on-failure` / Windows SC failure actions) restarts it. Recoverable faults stay GLP terms; only "cannot-live-with" state exits.
- **Heartbeat tick placement:** the engine's only steady-state loop is the pump driver (`glp_engine.cs:555-569`/`709-724`, `InboundPumpWait=30s` default, `:181`) — a watchdog ping belongs at the top of each iteration. **But** an idle engine with no active goal has no tick, so the **host BackgroundService timer** (independent of the GLP scheduler) is the robust source; an engine-internal "prove the core computes" liveness goal (a self-directed no-op that must reduce within a bound) is the deeper signal that distinguishes a live process from a live *computation* (analogous to the bridge-daemon-coordination "end-to-end SQL roundtrip" liveness signal).
- On restart, the host restores from persisted state (§5) then re-establishes ephemeral links.

---

## 5. PERSISTENCE — DB-backed full-state store behind an API

### 5.1 Where full state lives (none serializable today)

Engine live state is concentrated in `GlpRuntimeEngine` (`runtime.cs:22-152`): `Heap` (`HeapFCP`, `:27`), `Gq` (`GoalQueue`, `:30`), `Suspended` (reader-varId→blocked-goals, `:104`), per-goal tables `_goalEnvs/_goalPrograms/_goalModuleContexts/_budgets` (`:57-60`), `GlpChannels` (`:53`), `InfrastructureGoalIds` (`:112`), `NextGoalId` (`:78`), `_pendingTimers` (`:82`), file/FFI handle tables (`:64,:69`). Plus `GlpEngine._loadedPrograms`/`_loadedModules`/`_serveBytecode` (`glp_engine.cs:150-154`). **None is serializable; all CLR object graphs are lost on exit. No snapshot/persist path exists in the engine** (persistence report).

The heap detail: `HeapFCP.Cells` (`List<HeapCell>`) + `Hp` (`heap_fcp.cs:148,154`); cells hold `null|Pointer|SuspensionListNode|Term|VariableEntry|WriterContent` (`:36-59`). All refs are heap-address **ints** (`Pointer.TargetAddr`, `:69-80`) — **self-consistent only if the whole `Cells` array + `Hp` snapshot together**, which is exactly why a snapshot is atomic and tractable. `_bindCallbacks` (`:157`) are C# delegates — ephemeral, re-armed on resume via `LinkEstablish.ArmEgress`.

### 5.2 Persistent-vs-ephemeral classification (the central distinction)

| Component | file:line | Class | Resume handling |
|---|---|---|---|
| Heap `Cells` values + `Hp` | `heap_fcp.cs:148,154` | **PERSISTENT** | snapshot whole array atomically at quiescence |
| On-heap suspension chains (`WriterContent/VariableEntry.Suspensions`→`SuspensionListNode`) | `heap_fcp.cs:94-117,622-680` | **PERSISTENT** (addr-coupled) | restored with heap; reactivate on writer bind |
| `Gq` GoalQueue (`GoalRef{Id,Pc}`) | `runtime.cs:46`, `machine_state.cs:31` | **PERSISTENT** | reload queue |
| `Suspended` index (reader-varId→GoalRefs) | `runtime.cs:104` | **PERSISTENT** (ints) | reload |
| per-goal tables (`_goalEnvs/_goalPrograms/_goalModuleContexts/_budgets`, `_waitReaders`) | `runtime.cs:57-60` | **PERSISTENT** | reload |
| `NextGoalId` | `runtime.cs:78` | **PERSISTENT** | reload (avoid id collision) |
| `_loadedPrograms`/`_loadedModules`/`_serveBytecode` (compiled IL) | `glp_engine.cs:150-154`, `runner.cs:41` | **PERSISTENT** (the owner's prime persistent code) | reload IL **or** recompile from source at bootstrap |
| `ModuleTerm`-embedded `BytecodeProgram` (IL as heap term) | `terms.cs:146`, `glp_activation.cs:88` | **PERSISTENT** (lives in heap snapshot) | serialized with heap (intersects §2.5) |
| SystemPredicates/BodyKernels/LinkKernels registries (C# delegates) | `runtime.cs:33-36`, `body_kernels.cs:42` | **EPHEMERAL objects / PERSISTENT definition** | re-registered deterministically at boot (the SET is a definition) |
| `Scheduler` + `RunnerContext` | `scheduler.cs:106`, `runner.cs:232` | **EPHEMERAL** (rebuilt per run/drain-step) | reconstructed from persistent goal+heap state |
| `LinkId(Scheme,LinkAddress,LinkNonce)` (ground, never-reused) | `csharp/glp_link/seam/LinkId.cs:53-56` | **PERSISTENT** (the link DEFINITION) | reload; key for re-establish |
| listen rendezvous `rendezvous(Scheme,Endpoint)` | `LinkListenKernel.cs:32` | **PERSISTENT** (always-listening definition) | re-bind at boot |
| `LinkHandle.Endpoint` (live socket) + Sequencer/Window/Reassembler/Ordering | `LinkHandle.cs:14,17` | **EPHEMERAL** (instance) | re-open fresh socket from `LinkId` via `LinkRegistry.GetOrEstablish` |
| `LinkRuntime.Pending` / live `ILinkEndpoint`s | `LinkRuntime.cs:27,50` | **EPHEMERAL** | dropped; re-accepted |
| `MessageQueue._queuesByDestination` (pending outbound bytes, in-flight) | `message_queue.cs:78` | **EPHEMERAL** | sender resends |
| OS file/FFI handles (`_fileHandles` FileStream, `_libraries` IntPtr) | `runtime.cs:64,69` | **EPHEMERAL** (no re-establish path, no definition) | **OPEN PROBLEM** — see §8 risks |

The persistent-definition vs ephemeral-instance line is **already drawn** in feature-025: `LinkId` (definition) vs `LinkHandle.Endpoint` (instance), with `LinkRegistry.GetOrEstablish(id, establish)` (`LinkRegistry.cs:25-34`) the **reuse-or-rebuild seam** and `LinkEstablish.WireEstablishedLink` (`LinkEstablish.cs:29`) the single establish-and-wire core (open endpoint, idempotent register, wire 3 cursors, ArmEgress, start pump). Replaying it from a persisted `LinkId` yields an indistinguishable fresh link. `ResourceSnapshot`/`IResourceProbe` (`ResourceSnapshot.cs:17-38`) is a ready resume-verification checklist (per-link resources must return to baseline).

### 5.3 DB + API shape — mirror the codeconv MarathonStore

`codeconv/src/codeconv/marathon/store.py:96` is the proven template for "DB underneath, API hides detail" (§6): ONE logical interface over **PGLite-primary** (schema `marathon`, Alembic `0010`) + **JSON-fallback**, reconciled by a strict-monotonic marathon-wide `sequence_no` (`store.py:1-27`); `active_store()` degrades to fallback on outage (`:139`); `resume()` reads ONLY durable `max(seq)` state, never a summary (`checkpoint.py:68-74`). DBOS-on-PGLite (`durable/__init__.py`) gives idempotent dedup if boot/resume is modeled as a durable workflow.

**Recommended engine persistence API (same shape):**
- `SaveSnapshot(engineId, seq, blob)` at quiescence, where `blob` = {heap `Cells` + `Hp`; `Suspended` index; `Gq`; per-goal tables; `NextGoalId`; `programIL[]`; `linkDefs[]` (LinkIds); `listenDefs[]` (rendezvous)}.
- `LoadLatestSnapshot(engineId)`.
- `SaveDefinition(kind, id, blob)` append-only for new clause/channel/link-def.
- Strict-monotonic `seq`; primary + fallback; no SQL exposed.

**Granularity:** snapshot **at quiescence / between reductions only** — never mid-reduction. The heap is single-owner/single-thread and reductions are atomic 3-phase, so the consistency point is the quiescence boundary in `DrainAsyncWithStatus` (`glp_engine.cs:545`). The MarathonStore `sequence_no` model suggests a hybrid: **append-only definition log + periodic full-heap checkpoint** (full-snapshot at every quiescence may be costly for a long-lived large heap — §8 open question).

### 5.4 Bootstrap + restore-and-resume

**COLD start** already runs a predefined bootstrap: the `GlpEngine` ctor loads `self.glp`, registers predicates, compiles `serve/2` (`glp_engine.cs:202-217`); the composition root installs link kernels + transports (`Program.cs:30-35`, `glp_repl.cs:126`).

**WARM restart:** reload IL into `_loadedPrograms` (or recompile from source); re-register kernels (re-instantiate delegate objects); rebuild the transport registry; restore heap + goal-queue + suspension snapshot; re-establish links from persisted `LinkId`/listen defs via `GetOrEstablish`/`LinkEstablish` (fresh sockets, fresh cursors re-wired to restored heap addrs); resume the drain — suspended goals reactivate when `LinkPump.TryApplyNext` re-extends the In-stream (`LinkPump.cs:104-124`). Corpus 06 (`docs/research/multi-protocol-link-layer/corpus/06-heap-fcp-live-implementation.md:255-274`) confirms the imported-variable path is the live remote seam and that resume must rebuild suspension chains so reactivations are identical.

**Where the resume driver lives (FR-057):** the engine may own heap+goal snapshot; **link re-establishment must be above it** (in `glp_link` or the composition root), OR the engine gains a new **resume-hook injection seam** analogous to `rt.InboundPump` (`runtime.cs:129`). This is a design decision (§8 open question).

---

## 6. MAILBOX — OS-level vs in-GLP

Both substrates exist in-repo.

- **OS-level mailbox:** `TcpTransport` (`csharp/glp_link/transports/TcpTransport.cs:99-218`) is a working cross-process relay (4-byte big-endian length-prefixed self-delimiting frames over a duplex `NetworkStream`). `ILinkTransport`/`ILinkEndpoint` (`csharp/glp_link/seam/`) is the per-scheme seam where a **named-pipe / unix-domain-socket** leaf slots in for a same-host mailbox (the natural same-host choice; TCP-127.0.0.1 works today).
- **GLP-language mailbox:** the multiagent relay IS a mailbox programmed above GLP — `IsolateManager` (`out/csharp/lib/multiagent/isolate_manager.cs:234`) routes `IsolateMessage` over per-agent unbounded `Channels` (Option-C, `:224-232`), each agent `await foreach(reader.ReadAllAsync())` (`:564`). `ILinkTransport.cs:14-16` explicitly frames the link seam as "a leaf replaces the in-process IsolateManager's SendPort routing with real bytes." The concrete GLP constructs that could BE the mailbox: `Link(In,Out)` channel (`self.glp:456`), `link_send` appends a ground term to Out (`:536`), `link_recv` reads next off In (`:548`), `mwm` fans many client streams into one (`:380-425`). A per-client mailbox = one `Link` channel; many-clients→one-engine fan-in = `mwm`.

**Trade-offs / recommendation.** OS-level (TCP-loopback now, NamedPipe/UDS as the same-host leaf): simplest, already working cross-process, byte-exact, no language-level complexity — **use for the MVP transport/mailbox**. In-GLP (serve/2 + Link channels + mwm): the more elegant long-term target, lets the control program + per-client mailbox be GLP code (§3.3), and is the only option that makes the mailbox itself a *persistent GLP construct*; but it depends on multi-accept + compiled-IL-on-wire and the ground-only-relay restriction being lifted for IL. **Recommendation: OS-level (TCP loopback) mailbox for the MVP; in-GLP mailbox as the post-MVP target once multi-accept and the IL wire land.**

---

## 7. EPIC FEATURE BREAKDOWN

The owner wants the epic SPLIT into preparatory features, verification experiments, and an MVP. Ordered breakdown below (each: scope · kind · why · depends-on). Kinds: **PREP** (refactor/foundation that unblocks), **EXPERIMENT** (verification spike, throwaway-or-keep, de-risks an unknown), **MVP** (ships the milestone), **FOLLOW-UP** (post-MVP).

1. **engine-review-and-design-dossier** — PREP — Land this investigation + the refactoring design (the seam contract, wire shapes, persistence model, mailbox decision) as the authoritative dossier; resolve the requirement/code premise mismatches (§0.2). Why prep: every later feature cites it. depends-on: (none).
2. **result-envelope-and-deep-resolve** — PREP — Promote the dropped result components (var→writer map, suspended-goal detail, captured output) into `ExecutionResult` and add a server-side deep-resolver (reuse `_ResolveDeepForTrace`) so a result is self-contained (no client-side heap deref). Why prep: closes the §1.3 leak; prerequisite for ANY wire result. depends-on: 1.
3. **structured-output-capture-seam** — PREP — Route all `Console.WriteLine`/trace through `OutputCallback`/`TraceSink` so output is capturable/streamable. Why prep: §1.3 output leak must be plugged before a process split. depends-on: 1.
4. **il-codec-spike** — EXPERIMENT — Prove a `BytecodeProgram`↔bytes round-trip codec (both opcode families + recursive constant terms + labels + VariableMap), tested by compile→encode→decode→execute-equivalence. Why experiment: de-risks the single hardest unknown (no codec exists; v1/v2 split + recursive constants). depends-on: 1. (Throwaway-or-keep.)
5. **result-codec-and-framecodec-ride** — PREP — A dedicated result-envelope codec (status+bindings+var→writer+suspended+output+errors, incl. unbound-var encoding) framed over `FrameCodec`/`TcpTransport`. Why prep: the engine→client wire half. depends-on: 2,3.
6. **repl-engine-process-split-mvp** — MVP — Two processes (REPL client + engine host) over TCP-loopback `FrameCodec`: client sends **source text** (compiler stays engine-side for MVP), engine returns the structured result envelope; one engine, one client; C# host control program (one-accept listener); bootstrap from `self.glp`. Why MVP: the smallest end-to-end split that satisfies §2/§4 success themes without the compiled-IL/multi-accept/persistence dependencies. depends-on: 5.
7. **engine-state-snapshot-and-persistence-api** — PREP/MVP — Heap+`Gq`+`Suspended`+per-goal-tables+`NextGoalId`+loaded-IL snapshot at quiescence behind a `MarathonStore`-shaped API (PGLite-primary + JSON-fallback, monotonic seq). Why: the §6 persistence requirement; can land alongside or just after 6. depends-on: 1 (heap-serialize design), 6 (engine host process to own it).
8. **liveness-crash-restart-host** — MVP — `BackgroundService`/Windows-Service/systemd host: liveness ping (timer + optional self-prove goal), crash exit code, supervised restart that calls restore-and-resume. Why MVP: §5 success theme. depends-on: 7 (resume needs the snapshot).
9. **restore-and-resume-with-link-reestablish** — MVP — On restart reload persistent constructs, re-establish ephemeral links from `LinkId`/listen definitions via `GetOrEstablish`, re-wire cursors, resume the drain; pass a kill-and-restart correctness test. Why MVP: the §5/§6a central distinction proven end-to-end. depends-on: 7,8, and the feature-025 link-establish core.
10. **multi-accept-transport-extension** — FOLLOW-UP/PREP — Phase-6 multi-accept loop in `TcpTransport.ListenAsync` (yield many endpoints). Why: unblocks N-clients + a GLP control program. depends-on: 6.
11. **compiled-il-on-the-wire + factor-out-compiler** — FOLLOW-UP — Move the compiler to the front-end / a standalone component; wire carries compiled IL (codec from 4) both directions incl. `ModuleTerm` IL. Why follow-up: large refactor (compiler relocation, serve/2 + mad-predicate compilation currently engine-side); enables §2a. depends-on: 4,6.
12. **antlr4-shared-grammar-spike** — EXPERIMENT — Define the GLP grammar once in ANTLR4, generate C# (and trial C++/Dart) parser front-ends, confirm they produce the same IL. Why experiment: §2a/§2b prior-art-heavy, single-sources the language across C#/C++/Dart; verification spike before committing. depends-on: 11.
13. **multi-client-control-program-in-glp** — FOLLOW-UP — GLP-written control program (serve/2-loop + `request_listener` + Link mailboxes + `mwm`), N clients, Option-C funneled through the one-inbox pump. Why follow-up: the elegant target; needs multi-accept + IL wire. depends-on: 10,11.
14. **cpp-engine-feasibility** — EXPERIMENT — Scope + spike a C++ engine+scheduler+compiler-front-end consuming the same ANTLR4 grammar + IL, for footprint/perf/portability (§2b, §7a). Why experiment: prior-art-heavy, decisive for the many-instance goal. depends-on: 4,12.
15. **many-instances-shared-static-memory + cooperative-scheduling** — EXPERIMENT/FOLLOW-UP — Two-tier memory (instance vs shared-static construct-wrappers), minimal footprint, safe preempt/resume, cooperative run-to-completion of reduction CHAINS returning control to the OS (§7a). Why experiment: deep design unknown; informs C++ + persistence. depends-on: 7,14.
16. **research-programme + llvm-feasibility (staged)** — EXPERIMENT — The §7b internet-research programme (FCP/WAM/KL1-KLIC/BinProlog IL prior art) + staged LLVM scout→deepen→spike. Why experiment: informs IL design + optimization; runs in parallel, gates nothing critical. depends-on: 1 (can start immediately, mostly parallel).

---

## 8. FEASIBILITY, RISKS, OPEN QUESTIONS, FIRST MVP SLICE

### 8.1 Feasibility verdict

**High-feasibility, well-substrated.** The hard infrastructure mostly exists: an embeddable engine (`GlpEngine`), a single-owner-heap + multi-link pump model already ratified (Option-B/C), a working cross-process transport + frame codec (feature-025), a term codec for ground values (`PayloadSerializer`), and a proven durable-store pattern (`MarathonStore`). The three genuinely net-new pieces (IL/result codecs, OS-liveness host, heap serialization) are each tractable: heap serialization is tractable because the heap is single-owner, int-addressed, and snapshotted atomically at quiescence; the codecs ride `FrameCodec` unchanged; the liveness host is standard .NET hosting. The single biggest unknown is the **IL codec** (no precedent, dual opcode families, recursive constant terms) — hence it is an early EXPERIMENT (feature 4).

### 8.2 Top risks

1. **Premise mismatch (compiler location).** The requirements assume parser-in-frontend / compiled-IL-on-wire, but the code compiles engine-side from source. Shipping compiled-IL-on-wire is a large refactor (relocating Lexer/Parser/TypeChecker/Compiler + serve/2 + mad-predicate compilation). Mitigation: MVP keeps the compiler engine-side and carries source text; compiled-IL is a deliberate follow-up (features 11-12).
2. **Heap snapshot scale/cost.** Full-heap snapshot at every quiescence may be expensive for a long-lived large heap. Mitigation: definition-log + periodic full checkpoint hybrid (§5.3); design granularity early.
3. **Ephemeral OS file/FFI handles have no definition + no re-establish path** (`runtime.cs:64,69`). A resumed goal holding a stale handle int dereferences a dead handle. Mitigation: decide per-construct — persist open-intent as a definition, or treat open-file goals as non-resumable / re-derived (§8.3 open question).
4. **Suspended/partial results contain unbound variables that the only existing term codec rejects** (`LinkEgress.cs:29`). The result codec must encode unbound `VarRef` (and decide on `MutualRefTerm`/`ModuleTerm`). Net-new encoding.
5. **Heap-address stability across resume.** Internal refs are self-consistent ints, but any EXTERNAL reference (a client holding a writer address, an out-of-band cursor) breaks if addresses shift. Mitigation: snapshot `Cells` verbatim (int self-consistency permits exact-address resume) or introduce a stable logical-id layer.
6. **Multi-accept is a hard dependency for N-clients AND a GLP control program** (`TcpTransport.cs:47`), currently deferred Phase-6. Sequencing risk if multi-client is pulled into MVP.
7. **Cross-runtime byte-parity** for the new codecs if the Dart mirror is kept — the v1/v2 opcode split complicates a stable cross-runtime opcode format.

### 8.3 Open questions (for design/specify to resolve)

- Does the client compile to IL (relocate the compiler) or does the engine stay the compiler (carry source text)? Determines whether the wire codec is opcode-level or source+result-level. (MVP: engine stays compiler.)
- How is captured/streamed output modeled — incremental streaming frames vs a single terminal envelope (goals can suspend and produce output incrementally)?
- How are unbound `VarRef`, `MutualRefTerm`, and `ModuleTerm` encoded in engine→client bindings? Do `SuspendedGoals`/`BlockingReaders` (heap-addr-keyed) round-trip to a remote client (display-only vs resume-support)?
- Should var-name→writer-id use the stable `GlobalVarId(creator:localId)` scheme (`PayloadSerializer`) rather than local heap ints, for cross-process/restart stability?
- Which DB underlies the engine store (mirror `MarathonStore`'s PGLite-primary + JSON-fallback?) and exactly what is "full current state" vs rebuilt-from-code?
- Snapshot granularity (full at every quiescence vs definition-log + periodic checkpoint) and consistency point (only at quiescence — confirmed by single-owner/atomic-reduction).
- Where does the snapshot/resume driver live given FR-057 (engine owns heap snapshot; link re-establish above it — new resume-hook seam vs top-level supervisor)?
- Is the persistent store source-of-truth for CODE (persist IL, skip recompile) or is `.glp` re-loaded and the store holds only runtime state (recompile-on-boot avoids IL/source drift but risks divergence)?
- In-flight request loss on crash: replay from a persisted request log (idempotent) or accept loss? (No request persistence today; `LinkListenKernel`.)
- §2a/§2b/§7a/§7b dimensions (ANTLR4 shared grammar, C++ engine, two-tier shared/instance memory, cooperative run-to-completion, LLVM) are deferred to EXPERIMENT features (12,14,15,16); they do not gate the MVP but must be on the roadmap.

### 8.4 Recommended FIRST MVP slice

**A one-engine / one-REPL-client process split**, in order: (1) close the result leak — promote var→writer map + suspended detail + captured output into a self-contained, server-resolved `ExecutionResult` (features 2,3); (2) a dedicated result-envelope codec over feature-025 `FrameCodec`/`TcpTransport` loopback (feature 5); (3) split the REPL into a client process that sends **source text** and renders the wire result, and an engine host process that compiles + executes + frames results (feature 6); (4) bootstrap from `self.glp`. This satisfies the "separate process instances, engine embeddable, communicate only over the documented binary wire protocol, front-end is just one client" success theme with **no** dependency on the IL codec, multi-accept, compiled-IL relocation, or persistence — those layer on next (features 7-9 persistence/liveness/resume, then 10-16 the follow-ups/experiments). The IL codec spike (feature 4) runs in parallel as the early risk-burn-down.

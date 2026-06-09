# Design Dossier — Persistent, Embeddable GLP Engine (REPL ↔ Engine Separation over a Binary Wire)

**Epic:** `epic-separation-of-repl-front-end-from-engine-execution-scheduler`
**Status:** AUTHORED 2026-06-09 — awaiting owner decision at the marathon approval gate.
**Authoritative for:** the refactoring design of the REPL/front-end ↔ engine split. Successor features cite this document by section anchor (§N) as their source of truth.
**Supersedes (design content only):** the design/recommendation prose of `investigation.md` §1–§8. `investigation.md` remains the read-only **step-1 review of record**; this dossier consolidates its design, **re-verifies every `file:line` against current `HEAD`**, records reality where the code diverged, and presents every genuine fork as an **owner decision** (this dossier recommends but never settles a fork — FR-011, FR-018).

---

## §0. Executive summary + how to cite this dossier

### 0.1 The one-paragraph picture

The C# reference is **closer to the target than the prose suggests**, but the seam that exists today is **in-process C# method calls over one shared `Term`/`BytecodeProgram`/heap object graph — not a process boundary and not a wire**. `GlpEngine` (`out/csharp/lib/engine/glp_engine.cs:127`) is already an extracted, embeddable execution core (its own docstring at `:5-17` calls it "the ONE way to run GLP programs"); the REPL is a thin console loop (`out/csharp/bin/glp_repl.cs:98-371`). The whole job is to turn that in-process facade into a **documented binary wire** between separate OS processes, with the front-end as just one client. Three capabilities are **entirely net-new** (zero in the repo today): an IL/result wire codec, OS-liveness/crash/restart host machinery, and engine-state serialization/persistence. Each has a proven reusable substrate already in-repo (feature-025 `FrameCodec`/`TcpTransport`; `PayloadSerializer` for ground terms; the codeconv `MarathonStore`). Two requirement premises do **not** match the as-built code and force explicit decisions (§9). Recommended MVP: a one-engine/one-REPL-client loopback split carrying **source text** client→engine and a **net-new structured result envelope** engine→client, deferring compiled-IL-on-wire, multi-accept, and persistence to layered follow-ups.

### 0.2 How to cite this dossier (for successor-feature authors)

- Every design area is a numbered section **§1–§8**; premise reconciliations are **§9**; open forks are **§10**; the feature breakdown is **§11**; risks are **§12**.
- Each design area carries a **classification tag** — `reuse` / `refactor` / `net-new` (INV-2) — and **≥1 `file:line` citation** (SC-008). Jump straight to the cited code; you do not need to re-derive it from source (US1).
- A fork in **§10** is presented as **2–5 mutually-exclusive options** with consequences + trade-off + evidence. The dossier may mark one option **advisory-recommended**; the **owner's recorded decision** (made at the approval gate) is what you implement (FR-011, INV-4).
- The shared **classification table (§0.4)** is the at-a-glance map of what is reused, refactored, or built net-new across the whole epic.

### 0.3 Source inputs (read-only — never modified by this feature, FR-015)

| Input | Role |
|---|---|
| `docs/research/repl-engine-separation/investigation.md` | Step-1 multi-agent engine review of record; design draft this dossier consolidates + re-verifies |
| `docs/research/repl-engine-separation/requirements.md` | Owner requirements for the epic |
| `docs/research/repl-engine-separation/feature-definition.md` | Marathon framing (§8 steps 1–3) |
| `docs/research/repl-engine-separation/llvm-feasibility.md` | Feeds the staged-LLVM experiment entry (§11 #16) |
| `docs/research/repl-engine-separation/research-programme.md` | Feeds the internet-research-programme entry (§11 #16) |
| `out/csharp/` | **C#-first** engine + REPL reference (primary subject) |
| `csharp/glp_link/` | Feature-025 transport / framing / payload / link-establish layer |
| `codeconv/src/codeconv/marathon/` + `codeconv/src/codeconv/durable/` | Durable-store + DBOS template the persistence API mirrors |
| `glp_runtime/` | Dart source-of-conversion (parity cross-check) |
| `programs/self.glp` | Bootstrap prelude + GLP link wrappers |

### 0.4 Consolidated reuse / refactor / net-new classification (the shared table, FR-014 · SC-008 · INV-2)

This is the table §1–§8 reference. Every row is re-verified against current `HEAD`.

| Capability | Class | Status in code today | `file:line` anchor | Substrate to reuse / build on |
|---|---|---|---|---|
| Transport + framing (REPL↔engine link) | **reuse** | working cross-process loopback | `csharp/glp_link/transports/TcpTransport.cs:99-126`; `reliability/FrameCodec.cs:42,45,52,56-62` | feature-025 `FrameCodec` (0x01 ver byte, 22-byte header, CRC-32, MTU frag, 64 MiB guard) + `TcpTransport` + `ILinkTransport`/`ILinkEndpoint` byte seam, **as-is** |
| Ground-term value codec (bindings VALUES) | **reuse (partial)** | ground-only term-tree codec | `out/csharp/lib/multiagent/payload_serializer.cs:85-88` (tags), `:511` (throws on unbound VarRef), `:447` (`MutualRefTerm`/`ModuleTerm` → `NotSupportedException`) | `PayloadSerializer` tag scheme as a *recursive constant sub-encoder*; **cannot** encode unbound/suspended results |
| Server-side deep result resolve | **reuse** | exists for feature-020 trace | `out/csharp/lib/engine/glp_engine.cs:607-619` (`_ResolveDeepForTrace`) | basis for the engine-side "make the result self-contained" resolver |
| Result envelope codec (engine→client) | **net-new** | zero | rides `FrameCodec` (distinguish by a **payload-type prefix byte inside the chunk**; `FrameCodec.cs:64` `OffKind` is fragmentation-only — `Whole`/`Fragment` — not a payload-type slot) | new envelope: status + bindings + var→writer map + suspended detail + output + errors + **unbound-var encoding** |
| IL / bytecode wire codec | **net-new** | zero — no `Serialize/Encode/ToBytes` in `opcodes*.cs`/`runner.cs` (confirmed) | `runner.cs:41-73` (`BytecodeProgram`), `opcodes.cs`/`opcodes_v2.cs`, `codegen.cs:737-759` | new opcode-discriminant + operand format spanning **both** v1 `IOp` and v2 `IOpV2` families + recursive constant terms + labels + `VariableMap` |
| Multi-accept listener (N clients) | **refactor** (Phase-6 deferred) | one-accept-then-`Stop()` | `csharp/glp_link/transports/TcpTransport.cs:46-48` (comment: "ONE link per listen … Phase 6") | extend `ListenAsync` to yield many endpoints (`IAsyncEnumerable<ILinkEndpoint>` / callback) |
| Compiler relocation (front-end IL) | **refactor** (large) | Lexer/Parser/TypeChecker/Compiler engine-internal | `glp_engine.cs:487-493` (compiles goal string), `:251` (`LoadSource` takes text) | move compiler to front-end / standalone; the §9 premise reconciliation |
| OS-liveness / crash-signal / watchdog | **net-new** | zero (no `IHostedService`/`BackgroundService`/`sd_notify`/`Environment.Exit` anywhere in `out/csharp`, confirmed) | — | `Microsoft.Extensions.Hosting` `BackgroundService`; sd_notify (Linux) / Windows Service (SCM); distinguished crash exit code |
| Engine-state serialization / persistence | **net-new** | zero — no snapshot/persist path; all CLR graphs lost on exit | live state at `out/csharp/lib/runtime/runtime.cs:22-152`, heap `heap_fcp.cs:148,154` | `MarathonStore`-shaped API (PGLite-primary + JSON-fallback, monotonic `sequence_no`): `codeconv/src/codeconv/marathon/store.py:96` |
| Persistent-vs-ephemeral *definition/instance* seam | **reuse** (pattern) | drawn in feature-025 | `csharp/glp_link/primitives/LinkRegistry.cs:25-34` (`GetOrEstablish`), `LinkEstablish.cs:29` (`WireEstablishedLink`) | reuse-or-rebuild seam: replay a persisted `LinkId` → indistinguishable fresh link |
| Control program / per-client mailbox | **net-new** (host) / **reuse** (GLP loop) | host listener absent; GLP loop shape exists | `glp_engine.cs:135-136` (`serve/2`), `self.glp:387-422` (`mwm`), `self.glp:456` (`Link`), `:536`/`:548` (`link_send`/`link_recv`) | MVP: C# host `BackgroundService` one-accept listener; target: GLP `serve/2`-loop control program |

**Note on tags:** `reuse` = used unchanged; `refactor` = an existing component is moved/extended; `net-new` = no implementation exists today (substrate may be reusable).

---

## §1. The seam — front-end/client vs embeddable engine+scheduler  ·  *classification: reuse (engine core exists) + refactor (turn facade into wire)*

> **→ Successor seeds:** #2 result-envelope-and-deep-resolve, #3 structured-output-capture-seam — see Appendix B.

### 1.1 What the front-end owns

The front-end is the 38-line composition-root shim `out/csharp/glp_repl/Program.cs` (the **sole** place allowed to reference both the engine library and `GlpLink`) plus the converted REPL `out/csharp/bin/glp_repl.cs` (`Main` at `:98-371`). It owns console I/O + banner; the read-trim-dispatch loop (`glp_repl.cs:130-144`); the colon-command set (`:148-259`); and **all result display** — `FormatTerm` (`:432-584`) renders terms with heap-deref + cycle detection, `PrintStatus` (`:379-388`) maps status to `→ succeeds/failed/suspended`. It owns **no** parsing/compilation/execution/scheduling/suspension. There is **no pause state in the front-end** — a suspended goal is merely the status word; the real suspend/re-drain machinery lives in the engine pump driver (`glp_engine.cs:555-569`, `709-724`).

### 1.2 What the engine owns — the public contract

`GlpEngine` (`glp_engine.cs:127`) is the embeddable core. The front-end→engine contract is the public surface of `GlpEngine` + `ExecutionResult` + `ExecutionStatus`:

- ctor `GlpEngine(string rootSelfGlpPath)` (`:202`) — registers standard predicates, loads `self.glp`, compiles the embedded `serve/2` (body `:202-217`).
- `Task<ExecutionResult> RunGoalAsync(string goalText)` (`:349`) — **the primary crossing**; takes a goal **string**, compiles it itself (`:487-493`).
- `bool LoadFile(string)` (`:238`) / `bool LoadSource(string, string?)` (`:251`) / `bool LoadProject(string, string?)` (`:328`).
- scalar setters `MaxCycles/DebugTrace/DebugOutput/StrictTypes` (`:159-168`); `InboundPumpWait` (`:181`); `LoadedPrograms` getter (`:189`); `Clear()` (`:225`); `ActivateDynamicModule` (`:373`).
- **host hook** `static Action<GlpEngine>? AfterEngineCreated` — **lives in `glp_repl.cs:47`, invoked at `glp_repl.cs:126`** *(INV-3 divergence: step-1 §1.2 implied `glp_engine.cs:47`; it is actually in the REPL/host file. This reinforces FR-057 — the **host**, not the engine library, owns the seam where link kernels/transports are installed).*

`ExecutionResult` (`glp_engine.cs:51-80`) has **exactly three** fields: `Status` (`ExecutionStatus`: Succeeded|Failed|Suspended, `scheduler.cs:33-43`), `Bindings` (`IReadOnlyDictionary<string, RtTerm?>` at `:57`, var-name→**shallow**-deref'd live heap term via `Heap.Dereference` (`glp_engine.cs:578`) — **not** deep-resolved; the deep resolver `_ResolveDeepForTrace` (`:607-619`) runs only for EquivTrace, which is exactly why §1.3's leak exists; null=unbound), `Error` (`string?` at `:60`).

### 1.3 What crosses each direction — and the leaks to promote onto the wire

**Client → engine today:** a raw **goal string** (`RunGoalAsync`), or file paths / source text (`LoadFile/LoadSource/LoadProject`). **Not** compiled IL (see §9).

**Engine → client today:** an `ExecutionResult` whose `Bindings` values are **live heap `VarRef`s**, meaningful only against `engine.Runtime.Heap`. The front-end re-dereferences them during display (`glp_repl.cs:479,483,508,514,561`). **This is the seam's biggest leak** — the result is a pointer into engine-owned heap, not self-contained. A process split *requires* the engine to fully resolve/serialize result terms **server-side** (the deep resolver `_ResolveDeepForTrace` at `glp_engine.cs:607-619` is the reusable basis).

**Components the engine computes but DROPS at the `ExecutionResult` boundary** — these must be **promoted onto the wire envelope** (§2):

| Dropped component | Computed at | Why it matters |
|---|---|---|
| var-name → writer-id map (`queryVarWriters`) | built `glp_engine.cs:515`, handed to scheduler `:539` (`SetQueryVarNames`) — **not** an `ExecutionResult` field | the client needs writer identity to display/resume bindings without heap access |
| suspended-goal detail (`SuspendedGoals` + `BlockingReaders`) | `DrainResult`, `scheduler.cs:58-91` — engine propagates **only `Status`** | a remote client cannot otherwise see *why* a goal suspended |
| captured/streamed output | **no output field**; output is `Console.WriteLine` side-effects (`body_kernels.cs:959` `OutputKernel`, `glp_repl.cs`) + out-of-band `external_io.cs` observation | across a process boundary every `Console.WriteLine` is a leak; output must route via `GlpRuntimeEngine.OutputCallback` (`runtime.cs:135`) + `Scheduler.TraceSink` (`scheduler.cs:138`) onto the wire |

### 1.4 The natural cut point + the second seam

The cleanest architectural cut is the **compiler→runner boundary**: `BytecodeProgram` (defined `runner.cs:41`) is the sole artifact crossing from `GlpCompiler`/`CodeGenerator` into `BytecodeRunner`, constructed at `glp_engine.cs:534` *(INV-3: step-1's `runner.cs:452` is stale — `BytecodeProgram` is at `runner.cs:41`)*. Everything downstream of `BytecodeProgram` is execution — exactly where §9's compiler relocation wants the split.

A **second** front-end→engine seam bypasses `GlpEngine`: `:boot` → `RunBoot` (`glp_repl.cs:596-671`) drives `BootLoader` + `IsolateManager` directly (in-process isolates). The control-program model (§4) must decide whether it subsumes or replaces `IsolateManager` — out of MVP scope, a known parallel mailbox substrate (§7).

---

## §2. Binary wire shapes — client→engine payload + the net-new engine→client result envelope  ·  *classification: net-new (envelope) over reuse (framing)*

> **→ Successor seeds:** #2 result-envelope-and-deep-resolve, #4 il-codec-spike, #5 result-codec-and-framecodec-ride, #11 compiled-il-on-the-wire-and-factor-out-compiler — see Appendix B.

### 2.1 The IL is an in-memory object graph, not a byte array

`BytecodeProgram` (`runner.cs:41-73`) = `IReadOnlyList<object> Instructions` (heterogeneous **by design**, `:44`) + `Dictionary<string,int> Labels` (derived by `IndexLabels`, `:61-73`). **Two opcode families coexist in one list:** v1 `IOp` (`opcodes.cs`, ~50 classes) and v2 `IOpV2` (`opcodes_v2.cs`, unified reader/writer with an `IsReader` bool; codegen emits v2 directly, `codegen.cs:209`), plus `Label` markers. Operands are primitives (`long` indices, `string` functors/labels, `bool` flags) and **`object?` constant Values that can be recursive** — codegen embeds a runtime `Rt.StructTerm` as a `UnifyConstant.Value` for ground lists (`codegen.cs:737-759`), so a constant encoder must be a **recursive term sub-encoder**, not scalar-only.

**There is NO IL/bytecode wire codec anywhere** — `opcodes.cs`/`opcodes_v2.cs`/`runner.cs` have zero `Serialize/Encode/ToBytes` (re-confirmed by grep). `ToDisassembly()` (`runner.cs:88`) is human-readable, not a format.

### 2.2 What an IL codec must capture (for the wire AND for DB persistence)

(1) the ordered `Instructions` list — each opcode's discriminant + typed operands; (2) **both** opcode families in one stream; (3) the `Label` markers (note `CombinedProgram` *mutates* the Labels dict for module-boundary filtering at `glp_engine.cs:455-460`, so the filtered set is engine state distinct from the raw program); (4) the companion `CompilationResult.VariableMap` (name→register, `result.cs:9`) — the basis for the engine→client var↔writer mapping; (5) for persistence: `ModuleTerm`-embedded `BytecodeProgram`s reachable from the heap (`terms.cs:146`, `glp_activation.cs:88`) + the `_loadedPrograms`/export-import registry (`glp_engine.cs:150-154`).

### 2.3 The result envelope (engine → client) is NET-NEW

`PayloadSerializer` (`out/csharp/lib/multiagent/payload_serializer.cs`) is a tag-byte term-tree codec (`TagConstant=1/Variable=2/Struct=3/List=4` at `:85-88`; constant subtypes nil/int64/double/string/bool; length-prefixed) — **but** it is keyed by `GlobalVarId(creator:localId)` for inter-agent messages, **throws on unbound `VarRef`** (`:511`), and routes `MutualRefTerm`/`ModuleTerm` to the default `NotSupportedException` (`:447`). So it covers **only the bindings-VALUE sub-problem for ground terms**.

The **result envelope is net-new** and must define a complete field set:

| Field | Encoding obligation |
|---|---|
| `status` | Succeeded \| Failed \| Suspended (`scheduler.cs:33-43`) |
| `bindings` | var-name → value; ground values may reuse the `PayloadSerializer` tag scheme as a recursive sub-encoder |
| `var-name → writer-id map` | promote `queryVarWriters` (§1.3); identity scheme is an **open fork** (§10.4) |
| `suspended-goal detail` | `SuspendedGoals` (formatted) + `BlockingReaders` (heap-addr-keyed) from `DrainResult` (`scheduler.cs:58-91`); round-trip vs display-only is an **open fork** (§10.3) |
| `captured/streamed output` | framed from `OutputCallback`/`TraceSink`; streaming-vs-terminal is an **open fork** (§10.2) |
| `errors` | the `Error` string (`glp_engine.cs:60`) |
| **unbound-variable encoding** | **the first-class hard case**: a Suspended result *contains unbound vars* that the only existing term codec (`PayloadSerializer`) rejects by design (`:511`). The envelope MUST define an unbound-`VarRef` encoding (and decide `MutualRefTerm`/`ModuleTerm`, §10.3). (US1 scenario 1.) |

### 2.4 How runtime "generated IL" crosses

The engine does **not** synthesize bytecode (§9.2). "Engine-generated IL on the wire" means a `ModuleTerm`-wrapped `BytecodeProgram` on the heap (`terms.cs:146`, `glp_activation.cs:88`) appearing in a binding/channel. `PayloadSerializer` cannot serialize a `ModuleTerm` (`:447`). Therefore the **same IL codec (§3) must round-trip in BOTH directions** — the result encoder must emit a serialized `BytecodeProgram` when a result term contains one. For the MVP, ground results dominate; `ModuleTerm`-in-binding may be excluded/errored, with full bidirectional IL-on-wire a follow-up (§11 #11).

### 2.5 Cross-runtime parity caveat

`FrameCodec`/`Crc32` carry explicit byte-parity remarks ("Dart mirror is byte-identical (FR-060/061)", `FrameCodec.cs:31-32`, `Crc32.cs:7-8`); the Dart `ExecutionResult` is structurally identical (`glp_runtime/lib/engine/glp_engine.dart:34-37`). If the Dart mirror is kept (feature-definition §2a), the new IL + result codecs must meet the same byte-parity standard — a constraint the v1/v2 opcode split complicates (§12 risk 7).

---

## §3. Wire reuse decision — transport/framing reused, payload codecs net-new  ·  *classification: reuse (transport) + net-new (codecs)*

> **→ Successor seeds:** #4 il-codec-spike, #5 result-codec-and-framecodec-ride — see Appendix B.

**Reuse, as-is:** `FrameCodec` (`csharp/glp_link/reliability/FrameCodec.cs:42,45,52,56-62`) gives a 0x01 version byte, a 22-byte big-endian header (Version/Kind/MessageId/TotalLen/FragIndex/FragCount/ChunkCrc/ChunkLen), per-chunk CRC-32, MTU fragmentation/reassembly, and a 64 MiB payload guard — it wraps an **opaque** payload and knows nothing about terms or opcodes, so it frames an IL blob or a result envelope **unchanged**. *(Correction (reconciliation D3): payloads are distinguished by a **payload-type prefix byte inside the chunk**, NOT the header — `FrameCodec.cs:64` `OffKind` carries only `FrameKind.Whole/Fragment` for fragmentation, and `ParseFrame` (`FrameCodec.cs:132-143`) throws on any other value; there is no payload-type slot in the 22-byte header. Keeping the type byte inside the chunk preserves the feature-025 byte-parity contract.)* `ILinkTransport`/`ILinkEndpoint` (`csharp/glp_link/seam/`, `SendBytesAsync`/`RecvBytesAsync`) is the byte seam; `TcpTransport` (`transports/TcpTransport.cs:99-126`) is a working cross-process loopback (4-byte length-prefixed frames). **This stack is exactly what a REPL↔engine OS link needs — reuse it.**

**Do NOT reuse `PayloadSerializer` as the wire payload codec** — it is ground-only (`:511`) and term-only (no opcodes). Two **dedicated net-new codecs**, both riding `FrameCodec` (distinguished by a **payload-type prefix byte inside the chunk** — see the D3 correction above; the header `Kind` byte is fragmentation-only):

- **(A) IL/bytecode codec** — opcode discriminant + operand format spanning both v1/v2 families + recursive constant terms + labels + `VariableMap`. MAY reuse `PayloadSerializer`'s constant **tag scheme** as a sub-encoder.
- **(B) result-envelope codec** — the §2.3 field set, including unbound-var encoding.

**Why dedicated rather than extending `PayloadSerializer`:** opcodes are not terms; suspended results need unbound-variable encoding that `PayloadSerializer` forbids by design — changing it in place would break the feature-025 ground-relay invariant (`LinkEgress.cs:26` ships ground only, throws at `:68-69`).

---

## §4. Control-program startup + client model  ·  *classification: net-new (host) + refactor (multi-accept) + reuse (GLP loop)*

> **→ Successor seeds:** #6 repl-engine-process-split-mvp, #10 multi-accept-transport-extension, #13 multi-client-control-program-in-glp — see Appendix B.

### 4.1 The startup seam is the right insertion point

`AfterEngineCreated` (`glp_repl.cs:47`, invoked `:126`) fires once right after `new GlpEngine(...)`. `out/csharp/glp_repl/Program.cs:30-35` is the **sole composition root** referencing both the engine library and `GlpLink` — it sets the hook to `LinkKernels.Install(...)` + registers `TcpTransport`/`LoopbackTransport`. A pre-compiled control program is loaded/run at exactly this seam. **FR-057 is load-bearing:** `glp_link → out/csharp` only; the engine library NEVER references the link layer. Any C# control-program/listener therefore lives in the **exe/host**, not the engine library.

### 4.2 Listen/accept is real; multi-accept is the blocker

GLP link primitives exist: `LinkKernels.Install` (`csharp/glp_link/primitives/LinkKernels.cs:59-85`) registers `_link_setup/_link_send/_link_request/_link_listen/_link_accept/_link_monitor/_link_close`. GLP wrappers in `self.glp`: `request_listener(rendezvous(Scheme,Endpoint), Requests?)` (`:513-516`, the concrete `tcp,ep(127.0.0.1,Port)` is illustrative — the clause is generic over `Scheme`/`Endpoint`), `accept_link/4` (`:523-526`). `LinkListenKernel.cs:32-81` binds a rendezvous, parks the accepted endpoint in `LinkRuntime.Pending`, surfaces a `request(LinkId,FromPeer)` token; `LinkAcceptKernel.cs:26-49` adopts it via the shared `WireEstablishedLink` core.

**KEY GAP:** `TcpTransport.ListenAsync` (`transports/TcpTransport.cs:32-50`) accepts **exactly ONE** connection then `listener.Stop()` (`:48`; comment `:46-47`: "ONE link per listen … multi-accept … Phase 6"). **A one-engine/N-clients control program needs the deferred multi-accept loop first** (yield many endpoints — `IAsyncEnumerable<ILinkEndpoint>` or callback).

### 4.3 The control loop CAN be GLP — the shape already exists

`serve/2` (embedded const `_ServeSource` at `glp_engine.cs:135-136`; Dart mirror `glp_engine.dart:71-82`):
`serve(Module,[Goal|In]) :- ground(Module?) | '_activate'(Module?,Goal?), serve(Module?,In?). serve(_,[])` — the **exact stream-dispatch control-loop shape** a GLP listener uses to read a request stream and activate each goal. Combined with `request_listener` (accept stream), `Link(In,Out)` channels as per-client mailboxes (`self.glp:456`), `link_send`/`link_recv` (`self.glp:536,548`), and `mwm` multiway-merge fan-in (`self.glp:387-422`), **a GLP-written control program + per-client mailbox is concretely feasible** — depending on (a) the multi-accept extension and (b) the wire carrying compiled IL.

### 4.4 Multiple clients are already heap-safe (Option-B/C)

The engine is single-threaded, single-owner-heap by construction (`heap_fcp.cs:136-141`). `IInboundPump` (`inbound_pump.cs:20-37`) + `LinkPump` run a **per-link background recv loop** touching only a thread-safe inbox; the runner thread applies frames via `TryApplyNext` (`LinkPump.cs:86-125`). So **N clients → N links → N recv-loops → ONE inbox → ONE heap** is already ratified. Multi-client at the transport level only awaits multi-accept (§4.2).

### 4.5 Recommendation (advisory)

For the **MVP**: a **C# host control program** (a `BackgroundService` owning a one-accept `TcpTransport` listener) — simpler to ship, decouples the milestone from the multi-accept + compiled-IL dependencies. The **target**: a **GLP-written control program** (`serve/2`-loop over `request_listener`, `Link`-channel mailboxes, `mwm` fan-in), enabled once multi-accept lands. The REPL is one client; a programmatic OS client is another process running `link_send`/`link_recv` (precedent: `programs/tests/link/pc.glp` splits producer/consumer across two processes). Multi-client is a **follow-up** (§11 #13), not MVP. *(Owner decides at the gate — §10.)*

---

## §5. Liveness / crash-signal / restart model  ·  *classification: net-new (host layer)*

> **→ Successor seed:** #8 liveness-crash-restart-host — see Appendix B.

**NONE exists today** — grep across `out/csharp` and `csharp/glp_link` finds no heartbeat/watchdog/sd_notify/`IHostedService`/`BackgroundService`/`Environment.Exit` (re-confirmed). The only "liveness" hits are per-link timeout knobs (`LinkOptions.TempFailAfter=5s`, `seam/LinkOptions.cs:41`; `ConnectTimeout` `:51`) — link-fault knobs, not OS liveness. Transport faults become GLP lattice terms `ok/closed/tempFail/permFail` (`self.glp:451`, `Fault ::= ok ; closed(...) ; tempFail(...) ; permFail(...)`) but never escalate to the OS. This whole capability is **net-new C# host work**, placed in the host layer (alongside the composition root, **above** the engine library per FR-057).

**Recommended shape (advisory):**

- Wrap the engine in a `Microsoft.Extensions.Hosting` `BackgroundService`/`IHostedService`.
- **Liveness:** Linux — `sd_notify` `READY=1` + periodic `WATCHDOG=1`; Windows — run as a Windows Service reporting to the SCM; cross-platform fallback — a heartbeat file/socket the supervisor polls.
- **Crash signal:** a distinguished non-zero **exit code** on unrecoverable state so the supervisor (systemd `Restart=on-failure` / Windows SC failure actions) restarts it. Recoverable faults stay GLP terms; only "cannot-live-with" state exits.
- **Heartbeat tick placement:** the engine's only steady-state loop is the pump driver (`glp_engine.cs:555-569`/`709-724`, `InboundPumpWait=30s` default `:181`). **But** an idle engine has no tick, so the **host `BackgroundService` timer** (independent of the GLP scheduler) is the robust source. A deeper signal — an engine-internal "prove the core computes" liveness goal (a self-directed no-op that must reduce within a bound) — distinguishes a live *process* from a live *computation* (analogous to the bridge-daemon-coordination "end-to-end SQL roundtrip" liveness signal).
- On restart, the host **restores from persisted state (§6) then re-establishes ephemeral links**.

---

## §6. Persistent-vs-ephemeral state model (+ DB-abstraction + bootstrap + resume)  ·  *classification: net-new (serialization) over reuse (MarathonStore + LinkRegistry patterns)*

> **→ Successor seeds:** #7 engine-state-snapshot-and-persistence-api, #9 restore-and-resume-with-link-reestablish — see Appendix B.

### 6.1 Where full state lives (none serializable today)

Engine live state concentrates in `GlpRuntimeEngine` (`runtime.cs:22-152`): `Heap` (`HeapFCP`, `:27`), `Gq` (`GoalQueue`, `:30`), `Suspended` (reader-varId→blocked-goals, `:104`), per-goal tables `_budgets/_goalEnvs/_goalPrograms/_goalModuleContexts` (`:57-60`), `GlpChannels` (`:53`), `InfrastructureGoalIds` (`:112`), `NextGoalId` (`:78`), `_pendingTimers` (`:82`), file/FFI handle tables (`_fileHandles` FileStream `:64`, `_libraries` IntPtr `:69`). Plus `GlpEngine._loadedPrograms/_loadedModules/_serveBytecode` (`glp_engine.cs:150-154`). **None is serializable; all CLR graphs are lost on exit. No snapshot/persist path exists.**

Heap detail: `HeapFCP.Cells` (`List<HeapCell>` `:148`) + `Hp` (`:154`); a cell's `Content` holds `null|Pointer|SuspensionListNode|Term|VariableEntry|WriterContent` (`:36-59`); all refs are heap-address **ints** (`Pointer.TargetAddr` `:72`) — **self-consistent only if the whole `Cells` array + `Hp` are snapshot together**, which is exactly why a snapshot is atomic and tractable. `_bindCallbacks` (C# delegates `:157`) are ephemeral — re-armed on resume via `LinkEstablish.ArmEgress`.

### 6.2 The persistent-vs-ephemeral classification (the central distinction)

| Component | `file:line` | Class | Resume handling |
|---|---|---|---|
| Heap `Cells` values + `Hp` | `heap_fcp.cs:148,154` | **PERSISTENT** | snapshot whole array atomically at quiescence |
| On-heap suspension chains (`WriterContent.Suspensions`→`SuspensionListNode`) | `heap_fcp.cs:103`, `suspension.cs:35`, walk/activate `heap_fcp.cs:730-742` | **PERSISTENT** (addr-coupled) | restored with heap; reactivate on writer bind |
| `Gq` GoalQueue (`GoalRef{Id,Pc}`) | `runtime.cs:30`, `machine_state.cs:31` | **PERSISTENT** | reload queue |
| `Suspended` index (reader-varId→GoalRefs) | `runtime.cs:104` | **PERSISTENT** (ints) | reload |
| per-goal tables (`_goalEnvs/_goalPrograms/_goalModuleContexts/_budgets`) | `runtime.cs:57-60` | **PERSISTENT** | reload |
| `NextGoalId` | `runtime.cs:78` | **PERSISTENT** | reload (avoid id collision) |
| `_loadedPrograms`/`_loadedModules`/`_serveBytecode` (compiled IL) | `glp_engine.cs:150-154`, `runner.cs:41` | **PERSISTENT** (owner's prime persistent code) | reload IL **or** recompile from source at bootstrap (§10.8) |
| `ModuleTerm`-embedded `BytecodeProgram` (IL as heap term) | `terms.cs:146`, `glp_activation.cs:88` | **PERSISTENT** (in heap snapshot) | serialized with heap (intersects §2.4) |
| SystemPredicates/BodyKernels/LinkKernels registries (C# delegates) | `runtime.cs:33-36`, `body_kernels.cs` | **EPHEMERAL objects / PERSISTENT definition** | re-registered deterministically at boot (the SET is a definition) |
| `Scheduler` + `RunnerContext` | `scheduler.cs`, `runner.cs:232` | **EPHEMERAL** (rebuilt per drain-step) | reconstructed from persistent goal+heap state |
| `LinkId(Scheme,LinkAddress,LinkNonce)` (ground, never-reused) | `csharp/glp_link/seam/LinkId.cs:53-56` | **PERSISTENT** (the link DEFINITION) | reload; key for re-establish |
| listen rendezvous `rendezvous(Scheme,Endpoint)` | `LinkListenKernel.cs:32` | **PERSISTENT** (always-listening definition) | re-bind at boot |
| `LinkHandle.Endpoint` (live socket) + Sequencer/Window/Reassembler/Ordering | `LinkHandle.cs:17,21-30` | **EPHEMERAL** (instance) | re-open fresh socket from `LinkId` via `LinkRegistry.GetOrEstablish` |
| `LinkRuntime.Pending` / live `ILinkEndpoint`s | `LinkRuntime.cs:50` | **EPHEMERAL** | dropped; re-accepted |
| `MessageQueue._queuesByDestination` (in-flight outbound) | `message_queue.cs:78` | **EPHEMERAL** | sender resends |
| OS file/FFI handles (`_fileHandles`/`_libraries`) | `runtime.cs:64,69` | **EPHEMERAL** (no re-establish path, no definition) | **OPEN PROBLEM** — §12 risk 3 / §10.x |

The persistent-**definition** vs ephemeral-**instance** line is **already drawn** in feature-025: `LinkId` (definition) vs `LinkHandle.Endpoint` (instance), with `LinkRegistry.GetOrEstablish(id, establish)` (`LinkRegistry.cs:25-34`) the **reuse-or-rebuild seam** and `LinkEstablish.WireEstablishedLink` (`LinkEstablish.cs:29`) the single establish-and-wire core. Replaying it from a persisted `LinkId` yields an indistinguishable fresh link. `ResourceSnapshot`/`IResourceProbe` (`ResourceSnapshot.cs:17-38`) is a ready resume-verification checklist (per-link resources must return to baseline).

### 6.3 DB + API shape — mirror the codeconv MarathonStore

`codeconv/src/codeconv/marathon/store.py:96` is the proven template for "DB underneath, API hides detail": ONE logical interface over **PGLite-primary** (schema `marathon`, Alembic migration `0010_marathon_schema.py`, 8 tables) + **JSON-fallback**, reconciled by a strict-monotonic marathon-wide `sequence_no` (`store.py:1-27`); `active_store()` degrades to fallback on outage (`:139`); `resume()` reads ONLY durable `max(seq)` state, never a summary (`checkpoint.py:68-74`). DBOS-on-PGLite (`codeconv/src/codeconv/durable/__init__.py`) gives idempotent dedup if boot/resume is modeled as a durable workflow.

**Recommended engine persistence API (same shape, advisory):**

- `SaveSnapshot(engineId, seq, blob)` at quiescence, `blob` = {heap `Cells`+`Hp`; `Suspended`; `Gq`; per-goal tables; `NextGoalId`; `programIL[]`; `linkDefs[]` (LinkIds); `listenDefs[]` (rendezvous)}.
- `LoadLatestSnapshot(engineId)`.
- `SaveDefinition(kind, id, blob)` append-only for new clause/channel/link-def.
- Strict-monotonic `seq`; primary + fallback; no SQL exposed.

**Granularity:** snapshot **at quiescence / between reductions only** — never mid-reduction. The heap is single-owner/single-thread and reductions are atomic 3-phase, so the consistency point is the quiescence boundary in `DrainAsyncWithStatus` (`glp_engine.cs:545`). Full-snapshot-at-every-quiescence may be costly for a long-lived large heap, suggesting a **definition-log + periodic full-checkpoint hybrid** (§10.6).

### 6.4 Bootstrap + restore-and-resume

**COLD start** already runs a predefined bootstrap: the ctor loads `self.glp`, registers predicates, compiles `serve/2` (`glp_engine.cs:202-217`); the composition root installs link kernels + transports (`Program.cs:30-35`, `glp_repl.cs:126`).

**WARM restart:** reload IL into `_loadedPrograms` (or recompile from source); re-register kernels; rebuild the transport registry; restore heap + goal-queue + suspension snapshot; re-establish links from persisted `LinkId`/listen defs via `GetOrEstablish`/`WireEstablishedLink` (fresh sockets, fresh cursors re-wired to restored heap addrs); resume the drain — suspended goals reactivate when `LinkPump.TryApplyNext` re-extends the In-stream (`LinkPump.cs:104-124`). Corpus 06 (`docs/research/multi-protocol-link-layer/corpus/06-heap-fcp-live-implementation.md:255-274`) confirms the imported-variable path is the live remote seam and that resume must rebuild suspension chains so reactivations are identical.

**Where the resume driver lives (FR-057):** the engine may own heap+goal snapshot; **link re-establishment must be above it** (in `glp_link` or the composition root), OR the engine gains a new **resume-hook injection seam** analogous to `rt.InboundPump` (`runtime.cs:129`). A design fork — §10.7.

---

## §7. Mailbox decision — OS-level vs in-GLP  ·  *classification: reuse (OS-level, MVP) + net-new-in-GLP (target)*

> **→ Successor seed:** #13 multi-client-control-program-in-glp — see Appendix B.

Both substrates exist in-repo.

- **OS-level mailbox:** `TcpTransport` (`transports/TcpTransport.cs:99-126`) is a working cross-process relay (4-byte big-endian length-prefixed self-delimiting frames over a duplex `NetworkStream`). `ILinkTransport`/`ILinkEndpoint` (`csharp/glp_link/seam/`) is the per-scheme seam where a **named-pipe / unix-domain-socket** leaf slots in for a same-host mailbox (TCP-127.0.0.1 works today).
- **GLP-language mailbox:** the multiagent relay IS a mailbox programmed above GLP — `IsolateManager` routes `IsolateMessage` over per-agent unbounded `Channels` (Option-C); `ILinkTransport.cs:14-16` explicitly frames the link seam as "a leaf replaces the in-process IsolateManager's SendPort routing with real bytes." The concrete GLP constructs that could BE the mailbox: `Link(In,Out)` channel (`self.glp:456`), `link_send` (appends a ground term to Out, `:536`), `link_recv` (reads next off In, `:548`), `mwm` (fans many client streams into one, `:387-422`). A per-client mailbox = one `Link` channel; many-clients→one-engine fan-in = `mwm`.

**Trade-off / recommendation (advisory).** OS-level (TCP-loopback now; NamedPipe/UDS as the same-host leaf): simplest, already working cross-process, byte-exact, no language-level complexity — **MVP transport/mailbox**. In-GLP (`serve/2` + `Link` channels + `mwm`): the elegant long-term target, lets the control program + per-client mailbox be GLP code (§4.3), and is the only option that makes the mailbox itself a *persistent GLP construct*; depends on multi-accept + the IL wire + lifting the ground-only relay restriction. **Recommendation: OS-level (TCP loopback) for MVP; in-GLP mailbox as the post-MVP target.** *(Owner decides — §10.)*

---

## §8. MVP slice(s)  ·  *classification: composition of the above*

Each slice names the **net-new** capabilities it depends on and what it **defers** (SC-007).

> **→ Successor seed:** #6 repl-engine-process-split-mvp (and its prerequisites #2, #3, #5) — see Appendix B.

### 8.1 Slice A — one-engine / one-REPL-client process split (advisory-recommended MVP)

**Net-new deps it needs:** (1) the **result-envelope codec** (§2.3, §3-B) over `FrameCodec`/`TcpTransport` loopback; (2) the **result-leak closures** — promote var→writer map + suspended detail + captured output into a server-resolved, self-contained `ExecutionResult` (§1.3), reusing `_ResolveDeepForTrace` (`glp_engine.cs:607-619`) + routing output via `OutputCallback`/`TraceSink`.
**Reuses unchanged:** `FrameCodec`/`TcpTransport`/`ILinkTransport` (§3); the engine's existing compile+execute path (compiler stays engine-side); `self.glp` bootstrap.
**Defers:** the IL codec (§3-A); compiled-IL-on-wire + compiler relocation (§9.1); multi-accept (§4.2); persistence/liveness/resume (§5/§6); `ModuleTerm`-in-binding (§2.4).
**Why:** the smallest end-to-end split satisfying "separate processes, embeddable engine, communicate only over the documented binary wire, front-end is one client" — with **no** dependency on the IL codec, multi-accept, compiler relocation, or persistence.

### 8.2 Slice B — Slice A + snapshot persistence (alternative, larger MVP)

**Adds net-new deps:** the heap+goal **snapshot/persistence API** (§6.3) and the **liveness/crash/restart host** (§5) + **restore-and-resume** (§6.4).
**Defers:** same wire-side deferrals as Slice A.
**Why:** delivers the durability success-theme in the first milestone, at the cost of pulling §5/§6 net-new work forward.

**Advisory recommendation:** ship **Slice A** first (decouples the milestone from the three net-new heavy items), then layer persistence/liveness/resume as §11 #7–#9. *(Owner decides the MVP boundary at the gate — §10.)*

---

## §9. Premise reconciliations  ·  *(SC-002 — each: assumption · as-built + `file:line` · decision · downstream consequence)*

> **→ Successor seeds:** #6 repl-engine-process-split-mvp (source-text MVP), #11 compiled-il-on-the-wire-and-factor-out-compiler (the relocation follow-up) — see Appendix B.

### 9.1 Premise: "the parser lives in the front-end; client→engine carries compiled IL"

- **Requirement assumption:** the parser/compiler live in the front-end; the wire carries **compiled IL** client→engine.
- **As-built reality:** the **Lexer/Parser/TypeChecker/PartialEvaluator/Compiler are entirely engine-internal**. `RunGoalAsync` takes a **raw goal string** and compiles it itself (`glp_engine.cs:349`, `:487-493`); `LoadSource` takes **source text** (`:251`). The compiler→runner artifact `BytecodeProgram` is constructed at `glp_engine.cs:534` (defined `runner.cs:41`).
- **Resolving decision (owner, §10.1):** "client sends compiled IL" is a **real refactor of where the compiler lives**, not an existing contract to wire up. **MVP carries source text** (compiler stays engine-side); compiler relocation is a deliberate follow-up.
- **Downstream consequence:** splits the epic — the MVP (§11 #6) needs **only** the result-envelope codec; **compiled-IL-on-wire + factor-out-compiler** (§11 #11) and the **ANTLR4 shared-grammar spike** (§11 #12) become a distinct, later track. Determines whether the wire codec is opcode-level or source+result-level.

### 9.2 Premise: "the engine generates new IL at runtime"

- **Requirement assumption:** the engine **synthesizes new bytecode at runtime**.
- **As-built reality:** repo-wide, **no bytecode is synthesized at runtime** — every `.Compile`/`GenerateWithMetadata` call site is inside `glp_engine.cs` and always takes a **source string**. What looks like runtime IL generation is **runtime goal-term assembly + dispatch against pre-compiled bytecode** via `_activate` (`body_kernels.cs:1015`) against a `ModuleTerm`-wrapped `BytecodeProgram` stored on the heap (`glp_activation.cs:88`, `terms.cs:146`).
- **Resolving decision:** no compiler relocation is required to support "runtime IL" — there is none to support. But the finding is **load-bearing for persistence**.
- **Downstream consequence:** because **compiled programs circulate as runtime heap data** (`ModuleTerm`), any state snapshot must **serialize `BytecodeProgram` instances inside the heap** (§2.4, §6.2), not treat code as a static side table. This couples the IL codec (§3-A) to the persistence design (§11 #7) — the result/IL codec must round-trip a `BytecodeProgram` found in a binding.

---

## §10. Open-question option sets  ·  *(FR-011 · FR-018 · SC-003 · SC-009 — every step-1 §8.3 fork; none recorded settled; recommendations advisory)*

> **Authoritative open-question set:** `investigation.md` §8.3. Each fork below is rendered as mutually-exclusive options with consequences + trade-off + evidence. `settled = false` for all.

### 10.1 Compiler location (cross-linked to §9.1)
- **Opt 1 — engine stays the compiler; wire carries source text.** Consequence: MVP needs only the result codec; no compiler refactor. Trade-off: not the requirements' "compiled-IL-on-wire" end state. Evidence: `glp_engine.cs:487-493,251`.
- **Opt 2 — relocate compiler to front-end/standalone; wire carries compiled IL.** Consequence: enables thin clients + the §2a ANTLR4 single-grammar vision. Trade-off: large refactor (Lexer/Parser/TypeChecker/Compiler + `serve/2` + mad-predicate compilation are engine-side). Evidence: `glp_engine.cs:534`, `runner.cs:41`.
- *Advisory:* Opt 1 for MVP, Opt 2 as follow-up (§11 #11).

### 10.2 Output model — streaming vs terminal envelope
- **Opt 1 — single terminal envelope.** Consequence: simplest codec; one frame per goal. Trade-off: a goal that suspends and emits output incrementally cannot stream. Evidence: `OutputKernel` `body_kernels.cs:959`, `OutputCallback` `runtime.cs:135`.
- **Opt 2 — incremental streaming frames** (distinct **payload-type prefix byte**) + a terminal status frame. Consequence: supports long/suspending computations. Trade-off: ordering/flush + client reassembly complexity. Evidence: `TraceSink` `scheduler.cs:138`; the type byte rides inside the chunk (`FrameCodec.cs:64` `OffKind` is fragmentation-only — D3).
- *Advisory:* Opt 1 for the MVP (ground results dominate); Opt 2 when interactive long-runs land.

### 10.3 Encoding of unbound `VarRef` / `MutualRefTerm` / `ModuleTerm`; do `SuspendedGoals`/`BlockingReaders` round-trip?
- **Opt 1 — display-only suspended detail; exclude/erro `MutualRefTerm`/`ModuleTerm`.** Consequence: ships the MVP; matches ground-only relay. Trade-off: no remote resume from a suspended result. Evidence: `PayloadSerializer` throws `:511`, NotSupported `:447`; `LinkEgress.cs:68-69`.
- **Opt 2 — full round-trip** (encode unbound `VarRef` + heap-addr `BlockingReaders` + `ModuleTerm` IL via §3-A). Consequence: enables remote resume/inspection. Trade-off: the hardest net-new encoding; couples result codec to IL codec. Evidence: `scheduler.cs:58-91` (`DrainResult`), `terms.cs:146`.
- *Advisory:* Opt 1 for MVP; Opt 2 with persistence/resume.

### 10.4 var-name→writer identity scheme
- **Opt 1 — stable `GlobalVarId(creator:localId)`** (`PayloadSerializer` scheme). Consequence: cross-process/restart stable. Trade-off: must map heap ints↔GlobalVarId at the boundary. Evidence: `payload_serializer.cs:85-88`.
- **Opt 2 — local heap ints** (`queryVarWriters`). Consequence: zero mapping. Trade-off: meaningless across processes/restart (the §1.3 leak persists). Evidence: `glp_engine.cs:515`.
- *Advisory:* Opt 1 — the wire must be heap-independent (INV-5).

### 10.5 Which DB underlies the store; what is "full current state" vs rebuilt-from-code
- **Opt 1 — mirror `MarathonStore`: PGLite-primary + JSON-fallback, monotonic seq.** Evidence: `store.py:96,139`, migration `0010_marathon_schema.py`. Trade-off: a PGLite dependency for the engine host.
- **Opt 2 — JSON/file snapshot only (no PGLite).** Consequence: simplest host. Trade-off: loses query/dedup/DBOS idempotency. Evidence: `durable/__init__.py`.
- *Advisory:* Opt 1 (proven in-repo); "full state" = the §6.3 blob; code is rebuilt-from-source unless §10.8 Opt 1.

### 10.6 Snapshot granularity + consistency point
- **Opt 1 — full heap snapshot at every quiescence.** Consequence: simplest correctness. Trade-off: costly for a long-lived large heap. Evidence: `glp_engine.cs:545`.
- **Opt 2 — definition-log + periodic full checkpoint.** Consequence: cheap steady-state. Trade-off: replay complexity. Evidence: `store.py:1-27` (seq model).
- *Advisory:* consistency point is **only at quiescence** (single-owner/atomic-reduction — settled by construction, `heap_fcp.cs:136-141`); granularity is Opt 1 for MVP, Opt 2 when heap size demands.

### 10.7 Where the snapshot/resume driver lives (FR-057)
- **Opt 1 — top-level supervisor/composition root drives resume** (engine exposes heap-snapshot only; link re-establish above it). Evidence: `Program.cs:30-35`, FR-057.
- **Opt 2 — new engine resume-hook seam** analogous to `rt.InboundPump` (`runtime.cs:129`). Trade-off: adds engine surface but co-locates resume with heap.
- *Advisory:* Opt 1 (respects FR-057 cleanly).

### 10.8 Store as source-of-truth for CODE vs reload `.glp`
- **Opt 1 — persist IL; skip recompile on boot.** Consequence: fast warm start; exact code. Trade-off: IL/source drift if `.glp` changes underneath. Evidence: `glp_engine.cs:150-154`.
- **Opt 2 — reload/recompile `.glp` on boot; store holds only runtime state.** Consequence: no drift. Trade-off: slower boot; risks behavioral divergence if source changed. Evidence: ctor `:202-217`.
- *Advisory:* owner-dependent; Opt 2 simpler for MVP, Opt 1 for a code-authoritative engine.

### 10.9 In-flight request loss on crash — replay vs accept loss
- **Opt 1 — accept loss.** Consequence: no request log; simplest. Trade-off: a crash mid-request drops it. Evidence: no request persistence today; `LinkListenKernel.cs:32-81`.
- **Opt 2 — persisted idempotent request log + replay.** Consequence: at-least-once with dedup (DBOS). Trade-off: log + idempotency-key design. Evidence: `durable/__init__.py`.
- *Advisory:* Opt 1 for MVP; Opt 2 when durability is a hard requirement.

### 10.10 Deferred research dimensions (non-gating)
§2a ANTLR4 shared grammar, §2b C++ engine, §7a two-tier shared/instance memory + cooperative run-to-completion, §7b LLVM staging — deferred to EXPERIMENT features (§11 #12,#14,#15,#16). They **do not gate the MVP** but must be on the roadmap; framed there, not as forks here.

---

## §11. Epic feature breakdown  ·  *(FR-012 · FR-013 · SC-004 — ordered, topologically valid; each: kind · scope · why · depends_on · §ref)*

Kinds: **PREP** (unblocking refactor/foundation) · **EXPERIMENT** (verification spike, de-risks an unknown) · **MVP** (ships the milestone) · **FOLLOW-UP** (post-MVP). Entry **1 is this feature** (not re-seeded). Dependencies reference only **earlier** entries (zero forward deps).

| # | Name | Kind | Scope (one line) | Why | depends_on | §ref |
|---|---|---|---|---|---|---|
| 1 | engine-review-and-design-dossier | PREP | This dossier: seam/wire/persistence/mailbox design + premise reconciliations | every later feature cites it | — | §0–§12 |
| 1a | iterative-refinement-and-verification-framework | PREP | Shared GEPA/DSPy refinement loop (Claude-run, no API) + dual **formal** (ANTLR4 grammar-as-verifier, type/SRSW, mechanized GLP semantics via model-agnostic agentic Lean/Rocq, MLIR IL-dialect) + **pragmatic** (testing policy + pluggable per-domain strategies, Shapiro-criteria preservation) verification strategy that every later feature instantiates as its metric combination | every successor's refinement + metrics plan depends on it; avoids per-feature reinvention | 1 | §0.4, Appendix B + `reconciliation/SEED-RECONCILIATION-BRIEF.md` |
| 2 | result-envelope-and-deep-resolve | PREP | Promote var→writer map + suspended detail + captured output into `ExecutionResult`; server-side deep-resolver (reuse `_ResolveDeepForTrace`) | closes the §1.3 leak; prerequisite for ANY wire result | 1 | §1.3, §2.3 |
| 3 | structured-output-capture-seam | PREP | Route all `Console.WriteLine`/trace through `OutputCallback`/`TraceSink` | output must be capturable before a process split | 1 | §1.3 |
| 4 | il-codec-spike | EXPERIMENT | Prove `BytecodeProgram`↔bytes round-trip (both opcode families + recursive constants + labels + VariableMap) via compile→encode→decode→execute-equivalence | de-risks the single hardest unknown (no codec; v1/v2 split) | 1 | §2.1, §2.2, §3 |
| 5 | result-codec-and-framecodec-ride | PREP | Dedicated result-envelope codec (status+bindings+var→writer+suspended+output+errors, incl. unbound-var) over `FrameCodec`/`TcpTransport` | the engine→client wire half | 2, 3 | §2.3, §3 |
| 6 | repl-engine-process-split-mvp | MVP | Two processes over TCP-loopback `FrameCodec`: client sends **source text**, engine returns the structured envelope; one engine/one client; C# host one-accept listener; bootstrap from `self.glp` | smallest end-to-end split (§8.1) | 5 | §4, §8.1, §9.1 |
| 7 | engine-state-snapshot-and-persistence-api | PREP/MVP | Heap+`Gq`+`Suspended`+per-goal-tables+`NextGoalId`+loaded-IL snapshot at quiescence behind a `MarathonStore`-shaped API | the §6 persistence requirement | 1, 6 | §6.2, §6.3 |
| 8 | liveness-crash-restart-host | MVP | `BackgroundService`/Windows-Service/systemd host: liveness ping, crash exit code, supervised restart→restore-and-resume | the §5 success theme | 7 | §5 |
| 9 | restore-and-resume-with-link-reestablish | MVP | On restart reload persistent constructs, re-establish links from `LinkId`/listen defs via `GetOrEstablish`, re-wire cursors, resume the drain; kill-and-restart correctness test | proves the §6 distinction end-to-end | 7, 8 | §6.4 |
| 10 | multi-accept-transport-extension | PREP/FOLLOW-UP | Multi-accept loop in `TcpTransport.ListenAsync` (yield many endpoints) | unblocks N-clients + a GLP control program | 6 | §4.2 |
| 11 | compiled-il-on-the-wire + factor-out-compiler | FOLLOW-UP | Move the compiler to front-end/standalone; wire carries compiled IL (codec from #4) both directions incl. `ModuleTerm` IL | enables §9.1 Opt 2 / §2a | 4, 6 | §9.1, §2.4 |
| 12 | antlr4-shared-grammar-spike | EXPERIMENT | Define the GLP grammar once in ANTLR4; generate C# (trial C++/Dart) front-ends; confirm identical IL | single-sources the language; verification before commitment | 11 | §10.10 |
| 13 | multi-client-control-program-in-glp | FOLLOW-UP | GLP-written control program (`serve/2`-loop + `request_listener` + `Link` mailboxes + `mwm`), N clients via the one-inbox pump | the elegant target | 10, 11 | §4.3, §7 |
| 14 | cpp-engine-feasibility | EXPERIMENT | Scope + spike a C++ engine+scheduler+compiler-front-end on the same grammar/IL (footprint/perf/portability) | decisive for the many-instance goal | 4, 12 | §10.10 |
| 15 | many-instances-shared-static-memory + cooperative-scheduling | EXPERIMENT/FOLLOW-UP | Two-tier memory (instance vs shared-static), minimal footprint, safe preempt/resume, cooperative run-to-completion of reduction chains | deep design unknown; informs C++ + persistence | 7, 14 | §10.10 |
| 16 | research-programme + llvm-feasibility (staged) | EXPERIMENT | The internet-research programme (FCP/WAM/KL1-KLIC/BinProlog IL prior art) + staged LLVM scout→deepen→spike | informs IL design + optimization; runs parallel, gates nothing critical | 1 | §10.10 |

**Topological check:** every `depends_on` references a strictly smaller number — no forward dependency (SC-004). Every entry cites a motivating §ref (FR-013). MVP entries (#6,#7,#8,#9) enumerate net-new deps + defers in §8 (SC-007).

---

## §12. Risk register  ·  *(FR-017 — each risk: a mitigation reflected in the design or the breakdown ordering)*

| # | Risk | `file:line` | Mitigation (in design / ordering) |
|---|---|---|---|
| 1 | **Premise mismatch (compiler location)** — compiled-IL-on-wire is a large refactor | `glp_engine.cs:487-493` | MVP keeps the compiler engine-side, carries source text (§9.1 Opt 1); compiled-IL deferred to #11–#12 |
| 2 | **Heap snapshot scale/cost** at every quiescence | `heap_fcp.cs:148` | definition-log + periodic checkpoint hybrid (§10.6 Opt 2); design granularity early in #7 |
| 3 | **Ephemeral OS file/FFI handles have no definition + no re-establish path** — a resumed goal holds a stale handle int | `runtime.cs:64,69` | decide per-construct in #9: persist open-intent as a definition, or treat open-file goals as non-resumable/re-derived (§6.2 OPEN PROBLEM row) |
| 4 | **Suspended/partial results contain unbound vars the only term codec rejects** | `payload_serializer.cs:511`, `LinkEgress.cs:68-69` | the result codec (#5) defines a net-new unbound-`VarRef` encoding (§2.3, §10.3) |
| 5 | **Heap-address stability across resume** — external refs (a client holding a writer addr) break if addresses shift | `heap_fcp.cs:72` (`Pointer.TargetAddr`) | snapshot `Cells` verbatim (int self-consistency permits exact-address resume) or add a stable logical-id layer (§10.4 Opt 1); #7/#9 |
| 6 | **Multi-accept is a hard dep for N-clients AND a GLP control program**, currently deferred | `TcpTransport.cs:46-48` | sequenced as #10 **after** the MVP (#6); multi-client kept out of MVP (§4.5) |
| 7 | **Cross-runtime byte-parity** for new codecs if the Dart mirror is kept — the v1/v2 opcode split complicates a stable format | `FrameCodec.cs:31-32`, `opcodes_v2.cs` | specify codecs to the FR-060/061 byte-parity standard in #4/#5; the ANTLR4 spike (#12) single-sources the grammar |

---

## Appendix — cross-cutting invariants (apply to every section)

- **INV-1 (read-only):** the dossier + the post-approval roadmap-seed are the only artifacts produced; no engine/runtime/REPL code changed (FR-015, SC-006).
- **INV-2 (classification + citation):** every design area is tagged `reuse`/`refactor`/`net-new` and cites ≥1 `file:line` (FR-014, SC-008) — see the per-section tags + §0.4.
- **INV-3 (re-verified reality):** where as-built code contradicted a step-1 claim, current reality is recorded — `AfterEngineCreated` in `glp_repl.cs:47` (not the engine library); `BytecodeProgram` at `runner.cs:41` (step-1's `:452` stale); `serve/2` const at `glp_engine.cs:135-136` + Dart `glp_engine.dart:71-82`; `mwm` proc at `self.glp:387-422`; `PayloadSerializer` unbound throw at `:511` / `NotSupported` at `:447`; durable layer at `codeconv/src/codeconv/durable/`; migration `0010_marathon_schema.py` (FR-016).
- **INV-4 (present-options):** no genuine fork (§10) recorded as settled; recommendations are advisory; the owner decides at the marathon gate (FR-011).
- **INV-5 (self-contained):** the wire-crossing design (§2) is locatable from this dossier alone — a reviewer needs no engine source to find the result-envelope field set (SC-005).

---

## Appendix B — Successor Seed Registry (two-way traceability, FR-013)

Each successor feature seeded in `buildkit-roadmap` (state `captured`) maps to the dossier section(s) that motivate it (inverse of §11's per-entry `§ref`). Per-seed reconciliation memos live under `reconciliation/<num>-<id>.md` (authored by the seed-reconciliation pass; methodology in `reconciliation/SEED-RECONCILIATION-BRIEF.md`). In-situ `→ Successor seeds` markers appear at the head of §1–§9.

| # | Seed (feature_id) | Kind | Dossier §-anchors | Reconciliation memo |
|---|---|---|---|---|
| 1a | iterative-refinement-and-verification-framework *(early-stage methodology feature)* | PREP | §0.4, §3-metrics (via brief) — prerequisite-for-refinement of #2–#16 | `reconciliation/SEED-RECONCILIATION-BRIEF.md` (de-facto spec) |
| 2 | result-envelope-and-deep-resolve | PREP | §1.3, §2.3, §0.4 (result-envelope codec; deep-resolve) | `reconciliation/2-result-envelope-and-deep-resolve.md` |
| 3 | structured-output-capture-seam | PREP | §1.3 | `reconciliation/3-structured-output-capture-seam.md` |
| 4 | il-codec-spike | EXPERIMENT | §2.1, §2.2, §3, §0.4 (IL/bytecode wire codec) | `reconciliation/4-il-codec-spike.md` |
| 5 | result-codec-and-framecodec-ride | PREP | §2.3, §3 | `reconciliation/5-result-codec-and-framecodec-ride.md` |
| 6 | repl-engine-process-split-mvp | MVP | §4, §8.1, §9.1 | `reconciliation/6-repl-engine-process-split-mvp.md` |
| 7 | engine-state-snapshot-and-persistence-api | PREP/MVP | §6.2, §6.3, §0.4 (engine-state serialization) | `reconciliation/7-engine-state-snapshot-and-persistence-api.md` |
| 8 | liveness-crash-restart-host | MVP | §5, §0.4 (OS-liveness/crash/watchdog) | `reconciliation/8-liveness-crash-restart-host.md` |
| 9 | restore-and-resume-with-link-reestablish | MVP | §6.4, §6.2 (definition/instance seam) | `reconciliation/9-restore-and-resume-with-link-reestablish.md` |
| 10 | multi-accept-transport-extension | PREP/FOLLOW-UP | §4.2, §0.4 (multi-accept listener) | `reconciliation/10-multi-accept-transport-extension.md` |
| 11 | compiled-il-on-the-wire-and-factor-out-compiler | FOLLOW-UP | §9.1, §2.4, §0.4 (compiler relocation) | `reconciliation/11-compiled-il-on-the-wire-and-factor-out-compiler.md` |
| 12 | antlr4-shared-grammar-spike | EXPERIMENT | §10.10 (deferred dimensions) | `reconciliation/12-antlr4-shared-grammar-spike.md` |
| 13 | multi-client-control-program-in-glp | FOLLOW-UP | §4.3, §7 | `reconciliation/13-multi-client-control-program-in-glp.md` |
| 14 | cpp-engine-feasibility | EXPERIMENT | §10.10 | `reconciliation/14-cpp-engine-feasibility.md` |
| 15 | many-instances-shared-static-memory-cooperative-scheduling | EXPERIMENT/FOLLOW-UP | §10.10 | `reconciliation/15-many-instances-shared-static-memory-cooperative-scheduling.md` |
| 16 | research-programme-and-llvm-feasibility | EXPERIMENT | §10.10 | `reconciliation/16-research-programme-and-llvm-feasibility.md` |
| 1.5 | repl-engine-split-mvp-binary-wire-format-intermediate-language-c *(pre-decomposition monolith)* | UMBRELLA / supersession case | §8, §9, §11 (whole) | `reconciliation/1_5-repl-engine-split-mvp-binary-wire-format-intermediate-language-c.md` |

**Refinement methodology (all seeds):** each seed is shaped for iterative GEPA/DSPy refinement (Claude-run, no API) against a per-step **pragmatic + formal** metric combination, settled interactively at `/buildkit-specify`. Formal verification spans the GLP language (ANTLR4 grammar-as-example-verifier; type-checker/SRSW; mechanized semantics in Lean/Coq building on the Shapiro/FCP + WAM-verification line), its implementation, and the IL (TWAM/Vellvm-style verified-IL; MLIR verification dialect as the higher-level layer). Full methodology: `reconciliation/SEED-RECONCILIATION-BRIEF.md` + `reconciliation/REFINEMENT-METHOD.md`.

---

*End of dossier. Owner decision pending at the marathon approval gate. Roadmap seeding of successor features 2–16 (FR-019, `contracts/roadmap-candidate.md`) is **owner-gated** and occurs only after approval — see tasks.md T027.*

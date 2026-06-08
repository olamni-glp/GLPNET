# Research Programme — Persistent, Embeddable GLP Engine (REPL/Engine Separation over a Binary Wire IL)

**Epic:** `epic-separation-of-repl-front-end-from-engine-execution-scheduler`
**Feature:** `repl-engine-split-mvp-binary-wire-format-intermediate-language-c`
**Authored:** 2026-06-08 — synthesis of four internet-research reports
(logic-IL binary formats; shared/instance memory; cooperative scheduling +
preempt/resume; orthogonal persistence) into one research plan targeting the
requirements in [`feature-definition.md`](feature-definition.md) (esp. §3 wire-IL,
§4 control-program, §5 liveness/restart, §6 persistence, §7a many-instances /
shared-memory / cooperative-scheduling, §7b research programme).

GLP is in the **flat-concurrent-logic / committed-choice** lineage (Ehud Shapiro's
Concurrent Prolog → Flat Concurrent Prolog → the glpnet GLP). That lineage decides
almost everything below: the closest precedents are the **FCP sequential abstract
machine** and **KL1/KLIC**, *not* sequential Prolog — and the committed-choice model
removes backtracking/choice-points/trail, which is a major simplification for both
the binary IL and the persistence layer.

---

## 1. State of the Art (per axis: most relevant prior art + what to borrow)

### Axis 1 — Logic-language intermediate languages compilable to a binary format

The single most relevant precedent is the **FCP sequential abstract machine**
(Houri & Shapiro, *A Sequential Abstract Machine for Flat Concurrent Prolog*,
Weizmann CS86-20, 1986; reprinted as ch.38 of Shapiro's *Concurrent Prolog:
Collected Papers*, MIT Press) — the direct ancestor of GLP: a committed-choice
flat-concurrent-logic IL with a compiler (Logix) and a C emulator. Its claim — *"a
process-oriented language need not be less efficient than a procedure-oriented
language, even on a uniprocessor"* — validates the single-threaded engine over a
compiled IL.
(https://www.sciencedirect.com/science/article/pii/0743106689900113 ;
http://www.nongnu.org/efcp/references)

**Borrow:**
- **WAM category skeleton** (`put_*` / `get_*` / `unify_*` / control / choice /
  indexing) as the opcode taxonomy — the glpnet GLP bytecode is already WAM-shaped,
  so the wire format is a serialization of an ISA the engine already executes.
  (Aït-Kaci, *WAM: A Tutorial Reconstruction*, Appendix B —
  https://cliplab.org/~logalg/slides/8_wam_AitKaci_book.pdf)
- **A tiny opcode set** — CARMEL-2 realised the FCP machine in **29 instructions**;
  BinProlog's BinWAM is **~123 instructions / ~4500 LOC C emulator** — proof a
  flat-concurrent-logic IL reduces to a small, serializable ISA.
  (https://link.springer.com/article/10.1007/BF03037206 ;
  https://arxiv.org/abs/1102.1178)
- **One binary format for BOTH wire and persistence** — SICStus `.po` (Prolog
  object) files use *exactly the same binary format as saved states*, loadable by
  `load_files`; GNU Prolog has a two-tier `.wbc` (portable byte-code, loadable via
  `load/1`) vs `.ma`→native split, plus `write_pl_state_file/1` for state capture.
  (https://sicstus.sics.se/sicstus/docs/4.4.0/html/sicstus/Saving.html ;
  http://www.gprolog.org/manual/html_node/gprolog009.html)
- **Push complexity into a source-to-source pass, keep the runtime ISA minimal**
  (BinProlog binarization → "WAM-RISC"; Neumerkel's success criteria: *"small
  instruction set, small implicit state, simple meta-interpreter, compact emulator"*).
  (https://www.complang.tuwien.ac.at/ulrich/papers/PDF/binwam-nov93.pdf)
- **Mode-as-bit-field, not opcode-pair** — the glpnet V2 ISA already folds
  reader/writer opcode pairs into one opcode + `isReader` flag
  (`GetVariable(varIndex, argSlot, isReader)`); a compact binary IL carries mode in
  the operand. (glpnet `docs/glp-bytecode-v216-complete.md` v2.16.2 history)
- **Module(persistent) vs Computation(ephemeral)** split from Logix maps onto §6a:
  persist compiled modules/clauses; rebuild live processes/links. (http://www.nongnu.org/efcp/)

### Axis 2 — Many lightweight instances: shared static + per-instance dynamic memory

The dominant technique everywhere is identical: factor into an **immutable,
position-independent, read-only code/constant region** (shared across instances)
plus a **per-instance mutable heap/stack**. V8 states the cost model explicitly —
from `c*(1+n)` to `c*1` — by placing builtins in the binary's read-only `.text`
section, which *"the operating system automatically shares across processes"*.
(https://v8.dev/blog/embedded-builtins)

**Borrow:**
- **Isolate-independent code via a root register** (V8): builtins reference no
  absolute per-isolate addresses; constants load root-relative
  (`movq rax,[kRootRegister + <offset>]`). This is the exact discipline behind the
  glpnet `runner.dart` heap-address-vs-register-index fix (commit `8af18c3a`) — the
  codebase is partway there. (https://v8.dev/blog/embedded-builtins)
- **BEAM per-process model** as the footprint target and isolation contract: a
  spawned process is **327 words (~2.6 KB)**, private heap/stack/mailbox; the
  module/literal/atom areas are shared and **literals are sent by pointer, never
  copied**. This is the right model for GLP's "many single-threaded instances,
  per-instance heap, shared static wrappers".
  (https://www.erlang.org/doc/system/eff_guide_processes.html ;
  https://medium.com/@jlouis666/an-erlang-otp-20-0-optimization-efde8b20cba7)
- **JVM CDS / AppCDS**: memory-map ONE read-only archive (`classes.jsa`) of
  pre-parsed metadata into every process — the model for a memory-mapped shared
  construct-wrapper archive. (https://openjdk.org/jeps/310 ;
  https://docs.oracle.com/en/java/javase/17/vm/class-data-sharing.html)
- **Wasmtime pooling allocator + CoW + slot-affinity**: pre-allocate a pool sized to
  max concurrent instances; instantiate by grabbing a module-affine slot + CoW-map a
  bootstrap image; reset on teardown with a single `madvise`. The most directly
  transplantable engine-lifecycle design for many same-codebase instances.
  (https://docs.wasmtime.dev/examples-fast-instantiation.html)
- **WebAssembly Module/Instance** spec-level split (stateless shareable code +
  per-instance memory) — and its **honest cost**: sharing code means all
  instance-specific data is a memory load with no per-instance specialization. We
  accept this indirection tax. (https://developer.mozilla.org/en-US/docs/WebAssembly/Reference/JavaScript_interface/Module)
- **Akka is the anti-pattern**: shares bytecode but NOT per-actor heap (one GC heap),
  coupling GC/preempt/crash. GLP must follow BEAM — per-instance heap — so one
  instance can be preempted/restarted without touching siblings.
  (https://doc.akka.io/libraries/akka/snapshot/general/jmm.html)
- **KL1/PIM** is the concurrent-LOGIC data point (same committed-choice lineage):
  the "flat global name space over shared/hierarchical memory" tension is precisely
  ours; KLIC compiles KL1→C on stock Unix — precedent for an embeddable many-instance
  deployment on stock OS primitives rather than special hardware.
  (https://link.springer.com/chapter/10.1007/3-540-58402-1_4)

### Axis 3 — Cooperative run-to-completion scheduling + safe preempt/resume

The closest scheduler precedent is **BEAM's reduction budget**: a counter `FCALLS`
initialised to `CONTEXT_REDS` (4000 since OTP-20), decremented per call; at 0 the
process yields and the scheduler picks the next; progress is recorded as
`p->reds += (CONTEXT_REDS - p->fcalls)`. Critically, *"a process can only be
suspended at certain points... such as at a receive or a function call"* — yield
points are **inserted at safe boundaries**, never arbitrary instructions.
(https://github.com/happi/theBeamBook/blob/master/chapters/scheduling.asciidoc)

**Borrow:**
- **Yield boundary = end of one atomic reduction CHAIN** (per §7a, not per-reduction).
  GLP's coarser unit is *cheaper* than BEAM's because the engine call-stack is EMPTY
  between chains — the engine is effectively **stackless** at the boundary, so a
  "preempt" is just "stop scheduling new chains" and "resume" is "re-enter the loop".
  (GraalVM Espresso safe-point discipline: suspend only at well-defined points —
  https://www.graalvm.org/latest/reference-manual/espresso/continuations/)
- **Wasmtime epoch interruption** as the bound: a host sets a flag the loop checks
  between chains; epoch is **2-3× faster than fuel** (reads a rarely-changing global
  counter). Add an optional **fuel-style** deterministic per-chain reduction cap to
  catch a single non-terminating chain, and for replayable/record-replay debugging.
  (https://docs.wasmtime.dev/examples-interrupting-wasm.html ;
  https://docs.wasmtime.dev/examples-deterministic-wasm-execution.html)
- **FCP/GLP-native scheduling**: the resolvent is a FIFO goal queue (breadth-first);
  a per-unbound-variable suspension list suspends a goal reading an unbound var and
  reactivates it when a writer binds — this IS the engine's `si`/`U` +
  suspend/reactivate model (KL1/KLIC offers a per-PE LIFO alternative as a design knob).
  (Mierowsky/Taylor/Shapiro/Levy/Safra, *Design and implementation of FCP*;
  https://ieeexplore.ieee.org/document/153282/)
- **GraalVM Espresso continuations** prove a single-threaded interpreter can be
  snapshot+resumed (possibly in a different VM) **if** suspension is confined to safe
  points — exactly the inter-chain discipline. (https://www.graalvm.org/jdk24/reference-manual/espresso/continuations/serialization/)
- **OTP supervisor model** for §5 liveness/crash/restart: heartbeat + "restart me"
  signal + restart strategy + a **MaxR/MaxT restart-intensity cap** to stop a poison
  goal from crash-looping. (https://www.erlang.org/doc/system/sup_princ.html)
- **Prefer application-level checkpoint over CRIU**: CRIU would freeze the live socket
  (an ephemeral construct §6a wants rebuilt) and ties us to Linux; checkpoint the
  persistent constructs at a chain boundary and re-establish sockets on resume.
  (https://criu.org/Main_Page)

### Axis 4 — DB-backed orthogonal persistence + bootstrap + restore-and-resume

**Orthogonal persistence** (Atkinson & Morrison) gives the API contract for §6:
(1) Persistence Independence, (2) Data Type Orthogonality, (3) Persistence
Identification — persistence is a property of constructs reached by **reachability
from a persistent root**, not a per-object save call. Napier88's stable heap
*"ensures all reachable objects from a stable root are saved atomically"* via
shadow-paging copy-on-write at a stop-the-world checkpoint.
(https://arxiv.org/pdf/1006.3448 ; https://archive.cs.st-andrews.ac.uk/papers/download/RHB+90.pdf)

**Borrow:**
- **Persist by reachability from a root**, where the root = the program clause DB +
  the live goal/suspension set; the §6 hiding API is the orthogonal-persistence
  runtime layer.
- **Image-based persistence's honest caveat**: SBCL `SAVE-LISP-AND-DIE` preserves
  *only global state — the stack is unwound* and resume re-enters via a `:toplevel`
  bootstrap; Smalltalk reloads an image and re-opens streams. GLP must persist the
  **logical state** (heap terms, suspended-goal set, goal queue) and re-enter via the
  §6 bootstrap script — never resurrect a native C# call stack.
  (https://www.sbcl.org/manual/ ; https://github.com/devhawala/ST80)
- **BinProlog binarization** makes the **continuation a first-class serializable
  term** (`p(X):-q(X,Y),r(Y)` → `p(X,C):-q(X,Y,r(Y,C))`); a packed live continuation
  `r(Y,s(Z,true))` is *"suspended, packed, sent over the network and resumed at a
  different place"*. Insight: if suspended state is heap terms, **one serializer
  covers wire + persistence** and runtime-generated IL persists for free.
  (https://ar5iv.labs.arxiv.org/html/1102.1178)
- **DBOS-on-PGLite schema + recovery protocol** — already in-repo (marathon harness):
  `dbos.workflow_status` (status, `recovery_attempts`, `MAX_RECOVERY_ATTEMPTS_EXCEEDED`,
  `application_version`, ...) + `dbos.operation_outputs` (`function_id` monotonic,
  output, serialization); step checkpoint piggybacked in the same DB transaction;
  on restart a background thread *"resumes all incomplete workflows from the last
  completed step"*. Reuse the bridge + durable-execution machinery for §6/§9.
  (https://docs.dbos.dev/explanations/system-tables ; https://docs.dbos.dev/architecture)
- **Checkpoint + WAL hybrid**: periodic CoW heap snapshot (bounds replay length) +
  a redo-only WAL of per-chain committed mutations. Committed-choice GLP has **no
  backtracking trail**, so a **redo-only** log (no undo) suffices — a major
  simplification vs Prolog. (https://dotnet.github.io/dotNext/features/cluster/wal.html)
- **Ephemeral-vs-persistent (§6a)** is the textbook image-persistence problem:
  persist the **definition/intent** (channel, "always-listening" link) and
  **re-establish a fresh resource on restore** — exactly SBCL `:toplevel` /
  Smalltalk stream-reopen.

---

## 2. The Binary IL Scan (focused comparison)

GLP is committed-choice flat-concurrent-logic, so the **FCP and KL1 abstract
machines are the closest precedents**; WAM/BinProlog/GNU-Prolog/SICStus contribute
the *binary-encoding and persistence* engineering that the FCP literature does not
document in modern, reusable form.

| IL / machine | What it captures | Binary / serializable form | Doc quality | What GLP should adopt |
|---|---|---|---|---|
| **FCP sequential abstract machine** (Houri & Shapiro; CARMEL-2) | Committed-choice reduction; two-cell writer/reader vars; suspension lists; FIFO resolvent; `commit` | C-emulator instruction set; CARMEL-2 = **29 instructions** | Paper-grade; no modern open binary container spec | The reduction/commit/suspension model GLP already implements; proof the ISA is tiny |
| **WAM** (Warren; Aït-Kaci) | Term unification, structure-copying heap (REF/STR/CON/LIS tags), env/choice-point stacks, trail | Bytecode (p-code style); tagged heap cells | **Definitive open spec** (Appendix B full ISA) | The `put/get/unify/control/indexing` **opcode taxonomy**; tagged-cell heap model |
| **Neumerkel's binary-WAM family survey** (PLM/ZIP/WAM/VAM/BinWAM) | Comparative *instruction formats* (operand counts, decode style, control-transfer position) | n/a — design-knob taxonomy | Excellent comparative spec | The explicit **wire-IL design knobs** + success criteria (small ISA / small implicit state / compact emulator); the code-size-vs-decode-speed trade |
| **BinProlog / BinWAM** (Tarau) | Binarized continuation-passing clauses → "WAM-RISC"; first-class logic engines; continuation = term | **Saves to disk as WAM / binary application files**; continuation is a serializable term | Good (paper + open source) | **Continuation-as-term ⇒ one serializer for wire + persistence**; source-to-source binarization to shrink the runtime ISA |
| **KL1 / KLIC** (ICOT FGCS) | Flat Guarded Horn Clauses (committed-choice, GHC); goal-as-process; per-PE ready stacks | **KL1 → portable C** (native-compiled), not a bytecode image | Production-grade; papers | The **compile-IL-through-C** alternative substrate (parallels codeconv Dart→C#); per-PE LIFO scheduling as a knob |
| **GNU Prolog** (Diaz) | Full Prolog | **`.pl`→`.wam`→`.ma`→`.s`→`.o`**; `.wbc` portable byte-code loadable via `load/1`; `write_pl_state_file/1` | **Cleanest open documented pipeline** | The **two-tier model**: portable loadable byte-code (`.wbc`) vs native (`.ma`); explicit state-file capture |
| **SICStus** | Full Prolog (mature WAM emulator) | **`.po` == saved-state binary**, loadable by `load_files`; `save_files/save_modules/save_predicates` | Good vendor docs | **ONE binary format for partial code-shipping (wire) AND full image (persistence)** |
| **glpnet GLP bytecode v2.16.3** (NORMATIVE, in-repo) | WAM categories + FCP `Commit`/`ClauseTry`; two-phase HEAD match (σ̂w + S, then S′); two-cell var model; heap-only `VarRef(heapAddress)`; `isReader`-flag folding | In-memory now; **the actual ISA the wire/persistence codec must encode** | Internal NORMATIVE spec | **This is the baseline**: the wire format is a faithful serialization of it, not a new ISA |

**Net recommendation for GLP's binary wire+persistence IL:**
1. **Serialize the existing v2.16.3 ISA** — do not invent a new ISA. The codec is a
   faithful encoding of `glp-bytecode-v216-complete.md`.
2. **One codec for wire AND persistence** (SICStus `.po` model; BinProlog
   continuation-as-term). Engine-generated IL (§3) is indistinguishable on the wire
   from client-supplied IL and persists for free.
3. **Self-describing versioned container**: magic + version + section table; reuse
   feature-025's `FrameCodec` (version + CRC + fragmentation) to carry serialized-
   bytecode payloads as framed messages.
4. **Mode/variants as bit-fields** (the V2 `isReader` folding) to keep the opcode
   space and the image small.
5. **Keep the runtime ISA minimal; push optimization into a source-to-source / compile
   pass** (BinProlog "WAM-RISC"; Neumerkel "best: source-to-source optimization") —
   important for the minimal-footprint many-instances goal and cheap snapshot cost.
6. **Content-hash / dedup shared static IL** (BinProlog binary application files) so
   shared static code is shipped/stored ONCE across the many shared-memory instances
   of §7a.

---

## 3. Recommended Design Directions (mapped to requirements)

### (a) Binary IL wire + persistence format (§3, §6)
- **Single codec, two uses.** A `GlpImageCodec` serializing the v2.16.3 ISA serves
  both the §3 wire payload and the §6 persisted store (SICStus `.po`-style). Carry
  payloads in **feature-025 `FrameCodec` frames** (version + CRC + fragmentation).
- **Module vs computation sections** (Logix). The image has a *persistent* section
  (compiled clauses/goal definitions/link definitions) and an *ephemeral/live*
  section (goal queue, suspension tables, heap) — the same seam as §6a.
- **Heap terms = the universal payload** (BinProlog continuation-as-term). Encode
  the two-cell writer/reader vars, suspension records, and partial structures
  uniformly so the same encoder handles wire results (bindings + var-name→writer map
  + status) and persisted snapshots.
- **Engine→client result encoding** (open §11): bindings as serialized heap terms +
  the variable-name→writer mapping + a status enum (Success/Suspend/Fail) + streamed
  output + errors — all framed.

### (b) Shared-static + per-instance-dynamic memory, many single-threaded instances (§7a)
- **Two-tier memory.** Construct-wrapper code/dispatch tables + constant pool =
  immutable, position-independent, read-only **shared** segment; heap + goal queue +
  suspension tables + scheduler cursor = per-instance **dynamic** memory (BEAM model).
- **Instance-relative addressing** (V8 root register / the `8af18c3a` heap-addr vs
  register-index discipline) so one copy of wrapper code serves all instances. Accept
  the WebAssembly indirection tax.
- **Deployment choice to decide** (drill-down): **many OS processes COW-sharing the
  R2R/AOT `.text`** (independent OS-level liveness/restart per instance, matches §5)
  vs **one process hosting N in-process isolates** (cheapest sharing). Lean toward
  many-processes for the §5 supervised-restart story.
- **Lifecycle** = Wasmtime pooling + CoW: pre-allocate a pool; instantiate by
  CoW-mapping the bootstrap image (the §6 bootstrap result IS the CoW initial image);
  reset on teardown.
- **Inter-instance cooperation / mailbox** (§7): pass pointers/handles into the
  shared segment without copying where instances are in-process (BEAM literal model);
  serialize-and-copy across OS-process boundaries.

### (c) Cooperative run-to-completion scheduling + safe preempt/resume (§7a)
- **Scheduler loop:** `while (has_runnable_goals && !preempt_requested) { run_one_reduction_chain(); }` then **return control to the OS/control-program**.
- **Yield only at the inter-chain boundary** (BEAM safe-point + Espresso safe-point):
  the boundary must be **quiescent** — no in-flight HEAD-phase `_TentativeStruct` /
  `_ClauseVar`, no partial unification. At the boundary the engine is stackless ⇒
  preempt = stop scheduling, resume = re-enter the loop.
- **Bound a chain** with an epoch-style preempt flag checked between chains (fast) +
  an optional per-chain fuel cap for a single non-terminating chain (and for
  deterministic record-replay).
- **Persist at the boundary** — the safe-preempt boundary is also the safe-persist
  boundary (Napier88 stop-the-world checkpoint is cheap here because we are already
  stopped).
- **No CRIU** — application-level checkpoint at the chain boundary, not process-image
  freeze (CRIU captures the ephemeral socket §6a wants rebuilt).

### (d) DB-backed orthogonal persistence + bootstrap + restore-and-resume (§5, §6, §6a)
- **What "full current state" is** (§11): for committed-choice GLP it is **heap terms
  (reachable from root) + suspended-goal set (keyed by unbound readers) + active goal
  queue + compiled IL** — and **NOT** a native call stack and **NOT** choice-points /
  a backtracking trail (GLP has none ⇒ redo-only WAL, no undo).
- **Schema (DBOS-shaped, reuses the in-repo PGLite bridge)** — a `glpengine` schema:
  - `glpengine.engine_instance(instance_id, status, application_version,
    bootstrap_script_id, last_heartbeat_epoch_ms, recovery_attempts, ...)`
  - `glpengine.program(clause_id, predicate_name, arity, il_blob, source_hash, ...)`
    — persistent compiled code, incl. runtime-generated IL.
  - `glpengine.goal(goal_id, instance_id, il_blob, state ACTIVE|SUSPENDED|DONE, seq_no)`
    — the persistent goal queue.
  - `glpengine.suspension(susp_id, goal_id, waiting_on_var_id, reactivation_il)`
    — suspended set keyed by the unbound reader.
  - `glpengine.heap_snapshot(snapshot_id, instance_id, root_ref, heap_blob,
    taken_at_chain_boundary)` — periodic CoW checkpoint.
  - `glpengine.wal(lsn, instance_id, chain_id, mutation_blob, committed_at)` —
    redo-only per-chain log replayed past the last snapshot.
  - `glpengine.persistent_link_def(link_id, kind, listen_spec, status, instance_id)`
    — the §6a definition that re-establishes a fresh ephemeral link on restore.
- **Recovery sequence** (DBOS + Napier88 + SBCL): bootstrap script runs → load
  `program` (compiled IL) → restore latest `heap_snapshot` from root → replay `wal`
  with `lsn > snapshot` → rebuild `goal` queue + `suspension` set → re-establish
  ephemeral resources from `persistent_link_def` (open FRESH sockets) → resume
  scheduling at the next chain boundary.
- **Liveness/crash/restart** = OTP supervisor: heartbeat to host; "restart me" on
  unrecoverable error; **MaxR/MaxT** intensity cap (encoded by `recovery_attempts` /
  `MAX_RECOVERY_ATTEMPTS_EXCEEDED`) to quarantine a poison goal instead of looping.
- **Control program + mailbox** (§4, §7): decide OS-level (named pipe / domain socket
  / feature-025 `TcpTransport` / file mailbox) vs GLP-programmed (channels/streams +
  link In/Out). Lean toward a **GLP-programmed control program on the link layer**
  for single-sourcing, with an OS-level transport underneath.

---

## 4. The Research Programme Proper

**Overall plan.** Three phases, gated by the marathon sequence (§8): (I) a read-only
*engine review* that pins the actual C# ISA encoding, heap layout, and the
REPL↔engine seam against the four axes; (II) *design spikes* that de-risk the hardest
choices (one codec for wire+persistence; the chain-boundary quiescence guarantee;
shared-static addressing in .NET; the DBOS-shaped schema); (III) feed the validated
design into the buildkit pipeline. Every spike below states the source/experiment to
consult.

### Area A — Binary IL (wire + persistence)
1. **Pin the current encoding.** Read the C# bytecode emitter/reader in `out/csharp`
   (+ `glp_runtime`): exact opcode width, operand layout, constant pool, label/PC
   representation. *Source: in-repo code + `glp-bytecode-v216-complete.md`.*
2. **Confirm one-codec-for-both is faithful.** Verify the engine→client result set
   (bindings + var-name→writer map + status) and the persisted snapshot are encodable
   by the SAME term/IL serializer. *Source: SICStus `.po`==saved-state; BinProlog
   continuation-as-term.*
3. **Pick the container.** Does the IL need a versioned magic + section table, and can
   feature-025 `FrameCodec` wrap serialized-bytecode payloads directly? *Source:
   feature-025 `FrameCodec`; GNU-Prolog `.wbc`.*
4. **Code-size vs decode-speed knob.** Decide fully-unfolded head-unification opcodes
   vs a more compact (VAM-style merged head+goal, or partly data-driven) encoding for
   a persistence-conscious image. *Source: Neumerkel ("full head-unification doubles
   code").*
5. **Dedup shared static IL.** Content-hash scheme so shared code is shipped/stored
   once across §7a instances. *Source: BinProlog binary application files.*

### Area B — Shared-static / per-instance memory (§7a)
1. **The .NET sharing mechanism.** What is the CLR analogue of V8 embedded-builtins /
   JVM CDS — ReadyToRun/R2R AOT images, single-file publish, `MemoryMappedFiles` with
   read-only/CoW — and does the OS share R2R `.text` pages COW across processes? *Source:
   .NET R2R/AOT docs; `System.IO.MemoryMappedFiles`.*
2. **Static/dynamic boundary under runtime-generated IL (§3).** Confirm
   runtime-generated wrappers land in per-instance dynamic memory (BEAM literal model)
   and never mutate the shared segment. *Source: BEAM literal-copy-on-unload.*
3. **Process-per-instance vs isolates-in-process.** Quantify against §5 (OS liveness
   per instance) and §7a (cheapest sharing). *Source: V8 IsolateGroup vs prefork/Wasmtime
   pool.*
4. **Instance-relative addressing in managed C#.** Can wrapper code/data be addressed
   via an instance-relative base (root-register analogue)? *Source: V8 root register;
   glpnet `8af18c3a`.*

### Area C — Scheduling + preempt/resume (§7a)
1. **Quiescence proof.** Verify (against `runner.dart` / `heap_fcp.dart` and the C#
   mirror) that the inter-chain boundary has NO live `_TentativeStruct` / `_ClauseVar`
   and that {heap σ̂w + runnable goal queue + suspension lists + scheduler cursor}
   fully determines resumability. *Source: in-repo runner; FCP suspension/activation
   tables.*
2. **Fuel vs epoch.** Decide whether deterministic preemption (replayable persistence /
   record-replay) is required (⇒ fuel) or an epoch flag between bounded chains suffices.
   *Source: Wasmtime fuel vs epoch.*
3. **Where the scheduler lives.** Host-C# run-to-completion primitive vs a GLP-level
   meta-scheduler observing "chain complete" events (ties to §7 GLP-programmed control
   program). *Source: §7; FCP/KL1 schedulers.*
4. **Long-BIF / foreign-call fencing.** Any operation that cannot reach a chain boundary
   (analogue of BEAM trapping BIFs / dirty schedulers) must be fenced so it never
   straddles a checkpoint. *Source: BEAM BIF trapping.*

### Area D — Persistence + bootstrap + restore (§5, §6, §6a)
1. **Snapshot+WAL cadence.** Every N chains vs time-based, given PGLite cold-init ~7s
   and per-chain commit cost. *Experiment: benchmark on the in-repo PGLite bridge.*
2. **Heap-term serialization format** shared with the §3 wire IL: encode unbound
   readers/writers, suspensions (reactivation continuations), partial structures.
   *Source: inspect C# heap layout; BinProlog.*
3. **Persistent-root identification.** Is the root the bootstrap goal, the clause DB,
   the live top-level goals, or all three — and what is reachable-but-garbage that must
   NOT persist? *Source: Napier88 reachability; tabling early-reset literature.*
4. **Re-point in-flight goals at re-established links (§6a).** A stable logical link-id
   the heap holds vs the ephemeral fd, so goals referencing the old dead link bind to
   the fresh one on restore. *Design spike.*
5. **Multi-instance persistence isolation.** Per-instance `heap_snapshot`/`wal`
   partition (instance_id FK); shared static IL persisted ONCE and mmap'd read-only.
   *Source: DBOS schema; JVM CDS.*
6. **Idempotent replay.** GLP analogue of DBOS `function_id` ordering so a mid-chain
   crash + WAL replay does not double-apply a mutation or re-emit client output.
   *Source: DBOS operation_outputs.*
7. **Redo-only WAL confirmation.** Confirm committed-choice (no backtracking) ⇒ no
   undo log needed. *Source: FCP/GLP reduction model.*

### Staged side-investigations (from §7b)
- **LLVM/MLIR scout** (staged: scout → conditional deeper → optional spike) for
  generating/optimizing the IL — output to [`llvm-feasibility.md`](llvm-feasibility.md).
- **ANTLR4 shared-grammar** (§2a/§2b): one grammar → C#/.NET + Dart + C++ compiler
  front-ends producing the same binary IL (§3). Separate investigation; not on the
  persistence-MVP critical path.

---

## 5. Open Questions + Highest-Leverage Code-Level Spikes

**Open questions (must be resolved by the engine review / design):**
1. Engine→client result wire encoding (bindings, var-name→writer map, status, output,
   errors) — concrete shape.
2. How runtime-generated IL is represented + transmitted both directions, and where it
   lives in the static/dynamic memory split.
3. Which DB underlies the store (lean: reuse the in-repo DBOS-on-PGLite) and the shape
   of the hiding API.
4. Control program + mailbox in C# vs programmed in GLP on the link layer (§4/§7).
5. Multi-client feasibility for the MVP vs follow-up (single-owner-heap, Option-C).
6. What precisely constitutes "full current state" — provisionally: heap + goal queue +
   suspension set + compiled IL; NOT native stack, NOT choice-points/trail. **Verify.**
7. .NET mechanism for OS-shared read-only construct-wrapper pages (R2R COW vs CDS-style
   mmap archive) — does the model even hold on .NET?

**Highest-leverage code-level spikes (in priority order):**
- **SPIKE-1 (highest):** *Chain-boundary quiescence.* Instrument the C# runner (mirror
  of `runner.dart`/`heap_fcp.dart`) to assert that at the inter-chain boundary there
  are zero live `_TentativeStruct`/`_ClauseVar` and snapshot {heap, goal queue,
  suspension lists, scheduler cursor}, then deserialize into a fresh engine and resume
  to the same result. This simultaneously validates the §7a preempt point AND the §6
  "full current state" definition. *Decides Q6 and the whole persist/preempt design.*
- **SPIKE-2:** *One codec, two uses.* Serialize a compiled GLP program + a live
  suspended-goal set with one term/IL serializer; round-trip it (a) as a §3 wire
  payload in a feature-025 `FrameCodec` frame and (b) as a §6 `heap_snapshot` blob.
  *Decides whether wire and persistence truly share a codec.*
- **SPIKE-3:** *DBOS-shaped `glpengine` schema on the in-repo PGLite bridge.* Stand up
  `program`/`goal`/`suspension`/`heap_snapshot`/`wal`, write a checkpoint at a chain
  boundary inside one transaction, kill the process, and replay-resume. Benchmark
  snapshot+WAL cadence against PGLite cold-init ~7s. *Decides the §6 store + recovery.*
- **SPIKE-4:** *.NET shared-static feasibility.* Build the construct-wrapper code as a
  ReadyToRun/AOT image or a read-only mmap archive; spawn N engine processes; measure
  whether the OS shares the `.text`/archive pages COW and the per-instance dynamic
  footprint (target BEAM's ~2.6 KB order of magnitude). *Decides §7a memory architecture
  and the process-vs-isolate deployment.*
- **SPIKE-5:** *Restore-and-rebuild a §6a link.* Persist a `persistent_link_def`
  ("always listening on socket S"), kill the engine, restart, confirm a FRESH socket is
  opened from the definition and in-flight goals re-point to it via a stable logical
  link-id. *Decides the persistent-vs-ephemeral seam.*

---

## Sources (consolidated)

**Axis 1 — Logic IL:** FCP sequential abstract machine (Houri & Shapiro)
https://www.sciencedirect.com/science/article/pii/0743106689900113 · EFCP
http://www.nongnu.org/efcp/ · FCP references http://www.nongnu.org/efcp/references ·
CARMEL-2 https://link.springer.com/article/10.1007/BF03037206 · WAM (Wikipedia)
https://en.wikipedia.org/wiki/Warren_Abstract_Machine · Aït-Kaci WAM tutorial
https://cliplab.org/~logalg/slides/8_wam_AitKaci_book.pdf · Neumerkel binary-WAM
https://www.complang.tuwien.ac.at/ulrich/papers/PDF/binwam-nov93.pdf · BinProlog
https://arxiv.org/abs/1102.1178 · BinProlog source https://github.com/ptarau/binprolog ·
wamcc https://github.com/thezerobit/wamcc · KLIC http://ftp.sai.msu.su/sal/C/1/KLIC.html ·
FGCS retrospective https://link.springer.com/chapter/10.1007/978-3-319-29604-3_1 ·
GNU Prolog http://www.gprolog.org/manual/html_node/gprolog009.html ·
GNU Prolog mirror https://www.cse.iitb.ac.in/~cs206/Html/manual008.html ·
SICStus Saving https://sicstus.sics.se/sicstus/docs/4.4.0/html/sicstus/Saving.html ·
SICStus all-in-one https://sicstus.sics.se/sicstus/docs/4.10.1/html/sicstus/All_002din_002done-Executables.html ·
glpnet GLP bytecode v2.16.3 (NORMATIVE) D:/bstdev/research/glp/glpnet/docs/glp-bytecode-v216-complete.md

**Axis 2 — Shared/instance memory:** V8 embedded builtins https://v8.dev/blog/embedded-builtins ·
V8 custom startup snapshots https://v8.dev/blog/custom-startup-snapshots · BEAM processes
https://www.erlang.org/doc/system/eff_guide_processes.html · BEAM literal-by-pointer
https://medium.com/@jlouis666/an-erlang-otp-20-0-optimization-efde8b20cba7 · JVM CDS
https://docs.oracle.com/en/java/javase/17/vm/class-data-sharing.html · JEP 310
https://openjdk.org/jeps/310 · WebAssembly Module
https://developer.mozilla.org/en-US/docs/WebAssembly/Reference/JavaScript_interface/Module ·
Wasm compilation model https://github.com/WebAssembly/design/issues/1375 · Wasmtime fast
instantiation https://docs.wasmtime.dev/examples-fast-instantiation.html · Wasmtime
PoolingAllocationConfig https://docs.wasmtime.dev/api/wasmtime/struct.PoolingAllocationConfig.html ·
Linux CoW fork https://web.eecs.utk.edu/~huangj/cs360/360/notes/Fork/lecture.html ·
CoW why-fast https://medium.com/@Ibraheemcisse/copy-on-write-why-linux-process-creation-is-lightning-fast-90cf08644504 ·
KLIC portable impl https://link.springer.com/chapter/10.1007/3-540-58402-1_4 ·
FGCS parallel logic https://www.sciencedirect.com/science/article/abs/pii/S0167819199000757 ·
Akka JMM https://doc.akka.io/libraries/akka/snapshot/general/jmm.html ·
HN Akka heap https://news.ycombinator.com/item?id=7928030

**Axis 3 — Scheduling/preempt/resume:** theBeamBook scheduling
https://github.com/happi/theBeamBook/blob/master/chapters/scheduling.asciidoc · Erlang
scheduler details https://hamidreza-s.github.io/erlang/scheduling/real-time/preemptive/migration/2016/02/09/erlang-scheduler-details.html ·
AppSignal scheduler https://blog.appsignal.com/2024/04/23/deep-diving-into-the-erlang-scheduler.html ·
Run-to-completion https://handwiki.org/wiki/Run_to_completion_scheduling · Actor R&R
https://arxiv.org/pdf/1805.06267 · libcppa https://arxiv.org/pdf/1301.0748 · Wasmtime
interrupting https://docs.wasmtime.dev/examples-interrupting-wasm.html · Wasmtime Config
https://docs.wasmtime.dev/api/wasmtime/struct.Config.html · Wasmtime deterministic
https://docs.wasmtime.dev/examples-deterministic-wasm-execution.html · Stackless coroutines
https://goyalkavya.medium.com/stackless-execution-of-coroutines-9fdfcfffe6ce · Coroutine
https://en.wikipedia.org/wiki/Coroutine · N4402 resumable functions
https://isocpp.org/files/papers/N4402.pdf · CRIU https://criu.org/Main_Page · CRIU GitHub
https://github.com/checkpoint-restore/criu · CRIU for simulations https://arxiv.org/abs/2402.05244 ·
GraalVM Espresso continuations https://www.graalvm.org/latest/reference-manual/espresso/continuations/ ·
GraalVM Espresso serialization https://www.graalvm.org/jdk24/reference-manual/espresso/continuations/serialization/ ·
BRICS RS-99-51 https://www.brics.dk/RS/99/51/BRICS-RS-99-51.pdf · stackhack
https://www2.ccs.neu.edu/racket/pubs/stackhack4.html · KL1 https://grokipedia.com/page/kl1 ·
FCP(:,?) https://link.springer.com/article/10.1007/BF03037201 · Goal management in FCP
https://ieeexplore.ieee.org/document/153282/ · US4775934 https://patents.google.com/patent/US4775934 ·
Learn You Some Erlang supervisors https://learnyousomeerlang.com/supervisors · OTP supervisor
https://softwarepatternslexicon.com/erlang/creational-design-patterns-in-erlang/the-supervisor-pattern-in-otp/ ·
let-it-crash https://www.mgasch.com/2019/03/crash/

**Axis 4 — Persistence:** PS-algol https://en.wikipedia.org/wiki/PS-algol · Persistence
https://en.wikipedia.org/wiki/Persistence_(computer_science) · Orthogonal persistence revisited
https://arxiv.org/pdf/1006.3448 · OPJ spec https://dl.acm.org/doi/pdf/10.5555/974998 · Napier88
stable heap https://archive.cs.st-andrews.ac.uk/papers/download/RHB+90.pdf · Napier88 layered
https://www.researchgate.net/publication/2239814_A_Layered_Persistent_Architecture_for_Napier88 ·
SBCL manual https://www.sbcl.org/manual/ · save-lisp-and-die
https://koji-kojiro.github.io/sb-docs/build/html/sb-ext/function/SAVE-LISP-AND-DIE.html ·
ST80 https://github.com/devhawala/ST80 · BinProlog (ar5iv) https://ar5iv.labs.arxiv.org/html/1102.1178 ·
WAM action rules https://lirias.kuleuven.be/server/api/core/bitstreams/55869294-371d-4799-800e-138b40e2a9bb/content ·
Heap mgmt in tabling https://scholar.lib.vt.edu/ejournals/JFLP/jflp-mirror/articles/2001/S01-02/JFLP-A01-09.pdf ·
DBOS architecture https://docs.dbos.dev/architecture · DBOS system tables
https://docs.dbos.dev/explanations/system-tables · Why DBOS https://docs.dbos.dev/why-dbos ·
Postgres durable https://www.dbos.dev/blog/why-postgres-durable-execution · Erlang supervisor
https://www.erlang.org/doc/system/sup_princ.html · .NEXT WAL
https://dotnet.github.io/dotNext/features/cluster/wal.html · Event sourcing
https://dataopsschool.com/blog/event-sourcing/ · Event sourcing storage
https://softwaremill.com/things-i-wish-i-knew-when-i-started-with-event-sourcing-part-3-storage/

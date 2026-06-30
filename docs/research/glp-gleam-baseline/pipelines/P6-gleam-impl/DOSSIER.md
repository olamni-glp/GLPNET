# P6 — Gleam-Impl Dossier: how the GLP engine maps onto Gleam/AtomVM

| Field | Value |
|---|---|
| Feature | 036-glp-gleam-baseline-program |
| Pipeline | P6 (gleam-impl) |
| Run | mrun-5611c436ba95 |
| Task | T009 |
| Date | 2026-06-29 |
| Contract | `docs/research/glp-gleam-baseline/contracts/pipeline-contract.md` — every material claim cited (file:line / page / URL); judged on P4 parity-bar faithfulness · Gleam/AtomVM fit · maintainability; NO fastest-path rubric; FIXED inputs honored: ED-1 (bytecode-on-wire seam; M2 term-level link), ED-2 (v2.16.3 ISA the engine runs), and the AtomVM reality (no `gleam_otp`; raw `erlang:spawn` + `gleam_erlang` Subjects) established by F1. |

## 0. Scope & method

This dossier synthesizes how the dGLP execution model (the M1 single-instance parity bar) maps onto a Gleam implementation that runs on AtomVM. It is grounded against four read corpora and one adversarial cross-critique:

- **Bytecode / execution model:** `docs/glp-bytecode-v216-complete.md` (key `bc`); `D:/bstdev/research/glp/GLP/GLP_IMPLEMENTATION.pdf` (key `PDF`); `docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PARITY-BAR.md` (key `PB`).
- **Existing Gleam kernel (F4, feature 034):** `glp_gleam/src/glp/runtime/{terms,heap,unify,suspension}.gleam` (keys `heap.gleam` etc.); `specs/034-glp-gleam-core-terms-and-heap/parity-evidence.md` (key `parity-evidence.md`).
- **Gleam/AtomVM toolchain (F1, feature 031):** `docs/research/gleam-atomvm/{dossier.md,toolchain-inventory.md}`; `docs/research/gleam-atomvm/hello-glp-term/README.md`.
- **AtomVM v0.6.x primary docs:** the three `atomvm.net` pages cited inline.

Faithfulness is judged by **outcome-equivalence** (deref-result · unify-verdict · activation-set · committed-binding · top-level status), NOT transition-by-transition match — step-by-step match is impossible because GLP has nondeterministic goal selection vs dGLP's FIFO (`PDF` p.10 Thm 3.34 + Remark 3.35; `PB`:9). Internal heap layout is explicitly EXCLUDED from the bar (`PB`:9; `heap.gleam:69-71`), so Gleam may use any heap encoding provided the observable tuple matches.

---

## 1. Concurrency mapping — GLP suspension/reactivation ↔ BEAM (FB-M1-34..39)

### 1.1 The M1 recommendation: a single scheduler-actor, goals-as-data, ZERO spawns

**The dGLP machine is deterministic-FIFO with exactly one enabled transition per non-terminal configuration** (`PDF` p.8 Remark 3.26; goal selection = FIFO from active queue Q, clause selection = first applicable). The configuration is a triple `(Q, S, F)`: Q the active queue, S the suspended set (each goal paired with its blocking-reader set W), F the failed set (`PDF` p.8 Def 3.23). The transition (`PDF` p.8 Def 3.25) on head goal A of `Q = A·Qr` is exactly one of Reduce / Suspend / Fail. **There is therefore no intra-instance concurrency to exploit at M1** — a WAM-style bytecode interpreter is "plain sequential BEAM code… fine for AtomVM" and only the *spawn* primitive ever needs the raw form (`dossier.md:128-141`).

Consequently the recommended M1 engine is **one BEAM process threading `#(heap, Q, S, F)`**, with goals carried as plain data values `#(goal_id, κ, call_env)` in a FIFO Q, and **zero spawns** on the reduction path. This is *forced*, not chosen: F4's `Heap` is an `opaque Dict(Int, Cell)` threaded through pure functions where every mutator returns a new `Heap` (`heap.gleam:69-71`); a value-threaded heap is inherently single-owner and cannot back a variables-as-processes model. Every M1 suspension criterion FB-M1-34..40 reduces to a pure function over the heap value (`suspend_on_writer` `heap.gleam:287-297`; `activate` `heap.gleam:302-306`; `forward_to_terminal` `heap.gleam:251-278`), so `gleam_otp`/`proc_lib` — forbidden on AtomVM (`README.md:18-23`) — never touch the M1 path. **AtomVM-feasible: yes, zero spawns.**

BEAM **processes + `gleam_erlang` Subjects enter only at the M2 inter-instance seam (ED-1)**, where no shared heap value can cross (no disterl on AtomVM — see §3.5): these are the exact primitives F1 proved on AtomVM — raw `erlang:spawn` external + `self()`+`make_ref()` + `!` + selective `receive`, NOT `gleam_otp` (`README.md:13-23`). FCP-style single-assignment / SRSW (one writer binds, readers observe) is a natural fit for BEAM send/receive — the epic's strongest tailwind (`dossier.md:117-126`) — but that fit is M2's, not M1's.

### 1.2 Suspension formation (FB-M1-28/29/33/34)

A goal suspends only **after all clauses are tried** and U is non-empty at `no_more_clauses`: U≠∅ ⇒ suspend(U); U=∅ ⇒ fail (`bc`:190-196, `bc`:1438; `PB`:FB-M1-33). Suspension is on **readers, never writers** (`bc`:18; `PDF` p.5 Def 3.8, Reader×Term = suspend; `PB`:FB-M1-29). A HEAD match needing an unbound reader **SUSPENDS, never FAILS** (`PDF` p.7 Ex 3.22; `PB`:FB-M1-28); F4 already enforces this — `unify` reports `RReader×RValue → Suspend` and `RReader×RReader → Suspend`, never `Fail` (`unify.gleam:93-95`), and writer×writer as `Error(WriterToWriter)` never `Fail` (`unify.gleam:89`; SC-004). The suspension set is **minimal** — the least set of readers whose instantiation could enable a writer mgu (`PDF` p.7 Def 3.21; minimality flagged as possible implementation-latitude, `PB`:GAP-G7).

**FB-M1-34 — one shared record per goal.** When a goal suspends on N readers, ONE shared `SuspensionRecord{goalId, κ, next}` attaches to *each* blocking reader's (writer) cell suspension list (`bc`:62-73, `bc`:1380-1382; `PB`:FB-M1-34). In F4 the storage primitive is `suspend_on_writer`, which prepends a record to an unbound writer (`heap.gleam:287-297`); the record is `Suspension(goal_id, resume_pc, armed)` (`suspension.gleam:11-18`).

### 1.3 Single-fire / disarm (FB-M1-35) — the most pinned criterion, with a load-bearing correction

A goal suspended on N readers fires **EXACTLY once**: the first blocking reader to bind walks the list, enqueues the goal, and disarms the record (goalId←null); a disarmed record yields NO activation (`bc`:67-73, `bc`:225-233, `bc`:1385-1386; `PB`:FB-M1-36). F4 implements the **intra-cell** guard correctly — `activate` filters `armed` and emits one `GoalRef` per armed record (`heap.gleam:302-306`), and the caller `bind_writer` replaces the cell with `ValueCell` so that cell's records cannot re-fire (`heap.gleam:209-210`).

The **cross-cell gap is real and must be closed by F5**: a goal armed on writer-A and writer-B holds **two independent `Suspension` value-copies**; when A binds it fires and A→`ValueCell`, but B's copy is untouched and still armed, so B's later bind fires the same goal **again** — nothing in `bind_writer`/`activate`/`forward_*` references other cells' records (`heap.gleam:203-328`). Because Gleam/BEAM is immutable value-copy (no shared mutable cell), the single-shared-record aliasing the Dart engine relies on does not exist; the bar pins the obligation verbatim: *"the value-copy port MUST dedupe activations by goal_id"* (`PB`:FB-M1-35; `PARITY-BAR.md:89`). This is the 034 review finding "immutable value-copy doesn't preserve the cross-writer single-fire guard."

🔴 **Correction (load-bearing, from the cross-critique).** "Dedupe by `goal_id`" — as literally written in `PARITY-BAR.md:89` — is **under-specified and, taken literally, would BREAK FB-M1-38.** A goal lawfully wakes at κ, re-tries all clauses, fails to commit, and **re-suspends** (`bc`:85, `bc`:1442-1445; FB-M1-38, §1.5). Naive lifetime-wide `goal_id` dedup would drop the legitimate *second-episode* activation. The correct invariant is "fires once **per suspension episode**", i.e. the dedup key is **`(goal_id, suspension_generation)`**, not bare `goal_id`. **F5 must implement generation-scoped activation dedup.** This is the single sharpest F5 faithfulness obligation.

### 1.4 Reactivation is disjunctive (FB-M1-37)

Any ONE blocking reader binding reactivates the goal; exactly one `GoalRef(goal_id, κ)` is produced per armed record (`PDF` p.8 Def 3.24; `bc`:718-727; `PB`:FB-M1-37). Def 3.24: `reactivate(S, σ̂?) = {G : (G,W) ∈ S ∧ ∃X? ∈ W. X?σ̂? ≠ X?}` — goals whose suspension set contains a reader the new readers-substitution instantiated. In dGLP these reactivated goals rejoin the active queue at its tail (`PDF` p.8 Def 3.25 Reduce: `Q' = (Qr·B·R)σ̂σ̂?`; `bc`:1413, `bc`:1447-1451 — reactivations append to the GQ tail, never inline). F4 maps `activate` over **armed** suspensions → one `GoalRef` each, skipping disarmed (the double-activation guard) (`heap.gleam:302-306`; `parity-evidence.md` scenario #8/#9).

### 1.5 Wake-and-retry at κ (FB-M1-38)

Resume PC = κ = procedure entry / clause 1, **not** the suspension point; the goal re-attempts ALL clauses from the beginning on reactivation (`bc`:85, `bc`:1442-1445; `PB`:FB-M1-38). κ is the clause-selection entry PC of the current procedure (`bc`:22, `bc`:85), stored per goal and updated by `requeue` on tail-call (`bc`:662-693, worked example `bc`:677-693). In the scheduler-actor this is trivial: a goal value carries its κ; reactivation re-enqueues `#(goal_id, κ, call_env)` and the loop re-enters the procedure at κ. **This is exactly why generation-scoped (not bare-goal_id) dedup is required (§1.3):** wake-and-retry-and-re-suspend is a lawful second episode.

### 1.6 Bind-to-variable forwards suspensions (FB-M1-39) + never-drop-on-bound (FB-M1-40)

**FB-M1-39:** binding a writer onward to another unbound variable **forwards** the suspension list to the target and returns `[]` activations now; the suspensions fire on the *target* writer's later bind (`bc`:238-239; `PB`:FB-M1-39). F4: `bind_writer_to_var` produces the `WriterBound` chain and forwards armed suspensions via `forward_suspensions`/`forward_to_terminal` (`heap.gleam:221-278`; `forward_suspensions` `heap.gleam:310-328`).

**FB-M1-40 (FR-008):** forwarding onto an already-`WriterBound` target must NOT drop the suspension — forward to the **terminal** unbound writer (deref to terminal first). F4 implements `forward_to_terminal` (`heap.gleam:251-278`), which derefs the chain to its terminal unbound writer, handles the bound-target and no-armed branches (`heap.gleam:257-258,274-275`), and returns `[]` activations. This was a real suspension-DROP bug fixed in 034. ⚠️ **The Dart source-of-truth line this mirrors is NOT pinned** — `PB` flags RISK-CITE-1: *"the HEAP.forward_to_terminal line is not pinned"* (`PARITY-BAR.md:94,150`). The Gleam code is internally self-consistent, but its *parity* with Dart is not yet provable (see §5).

### 1.7 Scheduling contract the actor reproduces

The engine must emit a deterministic-FIFO schedule (`PDF` p.7 Remark 3.26 / Def 3.25): Reduce appends body+reactivated goals to the queue tail under the combined substitution; Suspend moves A into S paired with W; Fail moves A into F. Reader substitutions are applied **immediately** during reduction (sound because all variables are local — `PDF` p.8 Remark 3.27; `bc`:718-727). The single scheduler-actor is a direct image of `(Q,S,F)`, making it trivially outcome-equivalent to dGLP under Thm 3.34. Top-level `ExecutionStatus ∈ {succeeded, failed, suspended}` (`PB`:FB-M1-41); infrastructure/serve goals excluded from the verdict, `blockingReaders = suspended.keys` when suspended (`PB`:FB-M1-42); a terminated goal = success iff ≥1 reduction occurred (`PB`:FB-M1-43).

---

## 2. Heap model + term representation (ED-2, two-cell)

### 2.1 Term representation — keep F4 as-is

`Term = ConstTerm(value) | StructTerm(functor, args) | VarRef(addr)` (`terms.gleam:26-30`); reader/writer role is NOT in the term — read from the heap cell tag only (`terms.gleam:4-6`). `Constant` = the four ground kinds `ConstAtom/ConstInt/ConstReal/ConstString` (`terms.gleam:13-18`), with derived structural equality giving free comparability. Lists are not a variant: `nil()` = `ConstTerm(ConstAtom("nil"))` (`terms.gleam:33-35`), `cons(h,t)` = `StructTerm(".", [h,t])` (`terms.gleam:38-40`). This is the heap-only representation the ISA mandates (variables are `VarRef(heapAddress)`; CallEnv holds only `VarRef` — `bc`:24-41, `bc`:129-157). Names are Gleam binaries, **not** BEAM atoms, so the AtomVM 255-byte atom ceiling is N/A; `ConstInt` at 64-bit is the *faithful* bound vs Dart `int` / C# `long`, not a loss (see §3.2).

### 2.2 ED-2 two-cell object model — already faithfully encoded in F4

Every variable = **two heap cells** sharing one identity (`bc`:42-75): a writer cell (pointer to the reader cell when unbound, or the bound value; never updated after binding — `bc`:48-53) and a reader cell whose content is exactly ONE of {back-pointer to writer / suspension-list head / bound value} — **replaced, not extended** (`bc`:54-61). F4's `Cell` typed-union IS the tag: `WriterCell(reader_addr, suspensions)` (unbound writer holding paired reader + attached suspensions), `WriterBound(target)` (FCP `Pointer` chain, still writer-tagged), `ReaderCell(writer_addr)`, `ValueCell(term)` (`heap.gleam:23-28`); `tag/1` derives `WrtTag/RoTag/ValueTag` from the constructor, never stored (`heap.gleam:33-47`). `allocate_variable` returns `#(heap', writer_addr, reader_addr)` with writer↔reader bidirectional; the `+1` adjacency is allocation convenience only, role read from the tag (`heap.gleam:82-90`). The internal layout (an `opaque Dict(Int, Cell)` + `hp`) is EXCLUDED from parity (`heap.gleam:69-71`).

### 2.3 Deref, writer-MGU, and unify — pure functions, faithful to the term-matching table

`deref` walks reader→writer→(value|unbound) with **path compression threaded into the returned heap** (`heap.gleam:134-194`); a genuine multi-hop pointer cycle → `Error(Cycle)` (`heap.gleam:146-147`); WxW during traversal → `Error(WriterToWriter)` (`heap.gleam:151-153`); a `WriterBound` pointing to its OWN paired reader yields `Unbound(current)` not a cycle — the Dart bidirectional recognizer (`heap.gleam:165-175`, mirrors `heap_fcp.dart:312-323`). `bind_writer` → `ValueCell`, single-assignment (`heap.gleam:203-215`); `bind_writer_to_var` → the `WriterBound` chain with WxW guard (`heap.gleam:221-278`). Three-valued `unify` = `Success(heap) | Suspend(heap, on:) | Fail` (`unify.gleam:19-23`,40-97); `resolve` records the role from the tag at the ORIGINAL address (`unify.gleam:28-67`); only `RWriter` is bound, readers never (`unify.gleam:79-86`); struct unification recurses pairwise, no occurs-check (`unify.gleam:113-145`). This matches the term-matching table — Writer×Term ⇒ assign, Reader×Term ⇒ suspend, WxW ⇒ fail-as-loud-error, f1≠f2 ⇒ fail else recurse (`PDF` p.5 Def 3.8; `bc`:1371-1375; `PB`:FB-M1-10/14/15). 11 scenarios are cross-validated against the Dart heap unit suites (`parity-evidence.md:21-31,38-50`).

### 2.4 Why immutable-threaded over the alternatives (heap layout is free, so this is fit + faithfulness, not perf)

The immutable threaded store is single-owner and serializes as a *value* — the one mechanism that serves BOTH M1's deterministic heap and the M2 ED-1 seam (serialize the heap value). Process-per-variable is rejected on a **faithfulness** ground, not merely memory: a concurrent process-cell heap injects message-ordering nondeterminism into the heap itself, turning FB-M1-35 single-fire into a distributed-disarm race — directly hazarding the most-pinned criterion. ETS is OUT because it is absent from AtomVM v0.6.x and is not a value (cannot cross the ED-1 seam). See §4 FORK-B.

---

## 3. Persistence + AtomVM constraints (ED-6 codec)

### 3.1 Process / scheduling substrate

`proc_lib` is **absent** from AtomVM's subset → `gleam_otp` and `gleam_erlang`'s own `process.spawn`/`spawn_unlinked` do not run; a `gleam_otp` actor build crashes with `module proc_lib cannot be resolved` (`README.md:18-23`; `dossier.md:87,130-134`). The empirically pinned escape is a raw `erlang:spawn` external + `gleam_erlang` Subjects (`README.md:13-16`). This is the boundary the M1 scheduler-actor sidesteps entirely (§1.1) and the only primitive M2's seam may use (§3.5). AtomVM is BEAM-style **pre-emptive** and supports **SMP** ([Welcome v0.6.5](https://www.atomvm.net/doc/v0.6.5/welcome-to-atomvm.html)); on **plain BEAM** — the `gleam test` runtime — `gleam_otp` is fine (`dossier.md:138-139`).

### 3.2 Integer / atom / memory limits — directly constrain term+heap representation

🔴 **Integers restricted to 64-bit; bignums NOT supported** ([Memory Management v0.6.2](https://www.atomvm.net/doc/v0.6.2/memory-management.html); [Programmers Guide v0.6.5](https://www.atomvm.net/doc/v0.6.5/programmers-guide.html)). **Atoms > 255 bytes unsupported** (same). Per-process heap+stack starts at **8 words**, shares one region growing toward each other; copying GC on allocation when free < requested + ~16 words; growth strategy is a spawn option (`bounded_free` default, `minimum`, `fibonacci`) — relevant for tuning the one large scheduler-actor heap ([Memory Management v0.6.2](https://www.atomvm.net/doc/v0.6.2/memory-management.html)). Footprint floor ~512K RAM; **no stated max process count** ([Welcome v0.6.5](https://www.atomvm.net/doc/v0.6.5/welcome-to-atomvm.html)). Derived: keep all heap/term integers ≤64-bit (cell ids, var addresses, arithmetic) and term names as binaries; `ConstInt` 64-bit is the faithful bound (§2.1).

### 3.3 ED-6 wire format — byte-faithful port of the tracked C# codec

The Gleam ED-6 codec must be a **byte-identical** port of the C# wire format so the Lean-proven `decode∘encode = id` and cross-runtime parity carry over unchanged. The C# source: doubles store the raw IEEE bit pattern — `WriteDouble` = `BitConverter.DoubleToInt64Bits(d)` → `BinaryWriter.Write(long)` (8-byte LE), `ReadDouble` = `Int64BitsToDouble(ReadInt64())` (`ByteIo.cs:54-56`); int64 constant = tag + `w.Write(l)` (`ConstantCodec.cs:44-47`); varint = unsigned LEB128 **capped at 64 bits** (`ByteIo.cs:13-36`, the `shift >= 64` guard at `:33`); strings = varint-len + UTF-8 (`ByteIo.cs:38-52`). **Every field is byte-granular — no sub-byte field anywhere.**

### 3.4 Bit-syntax rules the codec must obey on AtomVM

AtomVM bit-syntax is hard-limited ([Programmers Guide v0.6.5](https://www.atomvm.net/doc/v0.6.5/programmers-guide.html)): restricted to **8-bit boundaries**; signed/little-endian insertion+extraction only for **8/16/32-bit**; only **unsigned** big/little-endian **64-bit** values; **no arbitrary (sub-byte) bit-length binaries**. Because the wire format is wholly byte-aligned (§3.3), the "8-bit boundary / no arbitrary-bit-length" rule is auto-satisfied — a big tailwind. **Two corrections, however:**

1. The 8-byte int/double fields on the wire are **signed LE** (C# `long`/`BinaryWriter`), but AtomVM permits LE-64 only **unsigned**. So the Gleam decoder must read `:64-unsigned-little` and **reinterpret two's-complement in pure Gleam** — bytes on the wire unchanged, preserving `decode∘encode=id`. This sign-reinterpretation is needed **only for `ConstInt`**; operands (var-indices, arities, reg ids, slots) are non-negative and bottom out cleanly at the 64-bit ceiling.
2. The varint 64-bit-cap guard (`ByteIo.cs:33`) must be replicated in the Gleam reader.

### 3.5 Persistence + M2 distribution

Host (generic_unix — the pinned build): plain **filesystem** access; AVM/BEAM loaded from filesystem paths (`code:load_abs/1`) — no NVS/flash (ESP32-only) ([Programmers Guide v0.6.5](https://www.atomvm.net/doc/v0.6.5/programmers-guide.html)). Ship as a packed **`.avm`** via `packbeam`/`atomvm_rebar3_plugin` (`README.md:111-117`; `toolchain-inventory.md:101-103`). 🔴 **`epmd`/`disterl` are unsupported** — native BEAM distribution is NOT available ([Programmers Guide v0.6.5](https://www.atomvm.net/doc/v0.6.5/programmers-guide.html)); the M2 cross-instance link must therefore ride the explicit bytecode-on-wire / term-level seam (ED-1), NOT disterl — exactly the raw `erlang:spawn` + `gleam_erlang` Subjects F1 proved (`README.md:13-23`). The GLP REPL itself cannot run on AtomVM (no REPL in the subset — same source); for M1 packaging the REPL stays on Dart/BEAM-host while AtomVM runs the engine.

---

## 4. Genuine forks — OWNER OPTIONS (owner-gated; READ-ONLY on live roadmap/specs/code until the migration gate, FR-010/FR-011)

These are presented as options with consequences; nothing on the live roadmap moves until the owner ratifies at the migration gate. Recommendations are advisory.

### FORK-A — Concurrency granularity → RECOMMEND scheduler-actor

- **Option A1 — Scheduler-actor (one process, goals-as-data) [RECOMMENDED].** Direct image of dGLP `(Q,S,F)` (`PDF` p.8 Def 3.23-3.25); reuses the F4 immutable heap unchanged (`heap.gleam:69-71`); **zero spawn ⇒ zero `proc_lib`/`gleam_otp` exposure** (best AtomVM fit, `README.md:18-23`); single-place generation-scoped dedup; deterministic ⇒ trivially outcome-equivalent to dGLP (Thm 3.34). CON: hand-written scheduler; one large process heap (mitigated by AtomVM copying GC + `fibonacci` growth option, §3.2).
- **Option A2 — Process-per-goal/variable.** BEAM-native suspension via `receive`. CONs: requires raw `erlang:spawn` per cell (`gleam_otp` forbidden, §3.1); thousands of tiny processes against the ~512K-RAM floor (§3.2); **makes FB-M1-35 single-fire a distributed race** (faithfulness hazard, §2.4); `receive` resumes where blocked, but GLP demands resume-at-κ (FB-M1-38, §1.5), so the native continuation is unusable. **Higher faithfulness risk, no M1 benefit.**

### FORK-B — Binding store → RECOMMEND immutable threaded store (already built, 54 tests green)

- **Option B1 — Immutable threaded store [RECOMMENDED].** Faithful (layout excluded from parity, `PB`:9; `heap.gleam:69-71`), AtomVM-fit (plain `maps`, no spawn), and the one mechanism serving both snapshot/persistence and the M2 ED-1 seam (serialize the heap value).
- **Option B2 — ETS.** OUT: absent from AtomVM v0.6.x and not a value (cannot serialize onto the ED-1 seam). NB this ruling rests on web docs / inference-from-absence, not an observed-failure spike (F1 never attempted ETS on v0.6.6) — sound but weaker evidence than the empirically-pinned `proc_lib` finding.
- **Option B3 — Process-cells.** OUT — the FORK-A2 CON. Revisit B-class only if profiling later shows AtomVM map-update cost dominates — a *performance* matter, since internal layout is excluded from parity.

### FORK-1 — Circular-term deref discriminator → OWNER-GATED, do NOT invent

Cross-goal-communication-formed circular terms `X=f(f(X?))` must be handled *gracefully* (core:166) yet the live Dart deref raises a loud SRSW error on a revisited address (HEAP:265-266). F4's `deref` already distinguishes a genuine multi-hop `Error(Cycle)` (`heap.gleam:146-147`) from a writer↔own-paired-reader self-bind → `Unbound(current)` (the Dart bidirectional recognizer, `heap.gleam:165-175`), giving a defensible default — **but the cross-goal-formed case must follow the owner-ratified discriminator** (`PB`:180-183, GAP-G5), not a Claude-chosen one.

---

## 5. Open risks / ED-6 obligations

1. 🔴 **ED-6 float-decode on AtomVM — the one unverified codec item.** The double is stored as its raw IEEE bit pattern to round-trip NaN/±0 (`ByteIo.cs:54-56`). Decoding on Gleam/AtomVM needs a bits→`Float` reinterpretation; whether AtomVM honors `/float` bit-syntax extraction (or needs arithmetic reconstruction) is **NOT grounded**. **Must spike before committing the Gleam codec** — do not assume it works. Everything else in the wire format is byte-aligned and AtomVM-legal.
2. **ED-6 signed-LE-64 extraction technique.** Required because AtomVM forbids signed/LE-64 bit-syntax (§3.4) while the C# fields are signed-LE `long`. Low risk — read unsigned-LE-64 + Gleam two's-complement, bytes unchanged — but a real porting step that must be coded and tested for negative `ConstInt`.
3. **FB-M1-40 / RISK-CITE-1.** The Dart `forward_to_terminal` reference line is unpinned (`PARITY-BAR.md:150`); the Gleam impl is self-consistent (`heap.gleam:251-278`) but its *parity* is not yet provable. Pin the `heap_fcp.dart` line before declaring FB-M1-40 verified.
4. **Integer-overflow parity masked on the test runtime.** M1 tests run on plain BEAM, where Gleam `Int` is **bignum** — so a parity test could pass on BEAM with a value Dart/AtomVM would wrap at 64-bit (§3.2). Overflow-edge parity must be exercised on **AtomVM** (or with explicit 64-bit wrapping), not only plain BEAM.
5. **F5 generation-scoped dedup.** The bar's literal "dedupe by `goal_id`" is insufficient; F5 needs episode/generation scoping (`(goal_id, suspension_generation)`) or it will drop lawful re-activations (FB-M1-38, §1.3/§1.5). Flag in the roadmap as a named F5 design constraint.
6. **Reader-side suspension routing + imported readers.** F4's writer-side `suspend_on_writer` (`heap.gleam:287-297`) is observable-equivalent for M1 but defers the reader-routing + imported-reader branch to F9 (`parity-evidence.md` faithful-divergences). This is precisely the **M2 linked-parity** heap seam and must be named in the roadmap, not assumed free.

---

## 6. Recommended synthesized strategy (survives the adversarial critique)

1. **M1 engine = a single scheduler-actor** threading `#(heap, Q, S, F)`: one BEAM process, goals as `#(goal_id, κ, call_env)` data values in a FIFO Q, **zero spawns**; the whole reduction loop is sequential BEAM code so `gleam_otp`/`proc_lib` never touch the path (§1.1, §3.1).
2. **Keep the F4 immutable threaded store and ED-2 two-cell model unchanged** (`heap.gleam`); faithful (layout excluded, `PB`:9), AtomVM-fit (plain `maps`, no spawn), and the one mechanism serving both persistence and the M2 ED-1 seam (§2.4).
3. **F5 implements generation-scoped activation dedup** (key = `goal_id` + suspension-generation), NOT bare `goal_id` — satisfies FB-M1-35 single-fire without breaking FB-M1-38 wake-and-retry-and-re-suspend (§1.3).
4. **ED-6 Gleam codec = byte-faithful port of the C# wire format** (`ByteIo.cs`, `ConstantCodec.cs`): read every 64-bit field as `:64-unsigned-little` and reinterpret sign in Gleam (only `ConstInt` needs it); replicate the varint 64-bit-cap guard (`ByteIo.cs:33`). Bytes identical ⇒ `decode∘encode=id` and cross-runtime parity preserved — **after** the float-decode spike (risk #1) clears (§3.3-3.4).
5. **Term representation stays as F4** — names are Gleam binaries not BEAM atoms (255-byte limit N/A); `ConstInt` 64-bit is the faithful bound vs Dart `int` / C# `long`, not a loss (§2.1, §3.2).
6. **BEAM processes + `gleam_erlang` Subjects enter only at the M2 inter-instance seam (ED-1)** — never disterl (unsupported, §3.5) — the exact primitives F1 proved on AtomVM.

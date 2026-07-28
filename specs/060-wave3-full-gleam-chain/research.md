# Phase 0 Research: Wave 3 — Full Gleam chain

**Feature**: `060-wave3-full-gleam-chain` | **Date**: 2026-07-27

All `NEEDS CLARIFICATION` markers from the spec were resolved at `/bk-clarify` (see `spec.md` → Clarifications → Session 2026-07-27). This document records those decisions plus the source-level gap analysis that the plan rests on.

---

## D1. Shared grammar — out of scope

- **Decision**: The Gleam runtime keeps its own front end (`glp/parser/{lexer,parser,ast}.gleam`). No ANTLR4 or other shared grammar artifact is produced in wave 3.
- **Rationale**: Feature 059's compiler verification recorded the ANTLR path as **superseded under the G5 ruling**, and recorded `parser-recursive-descent`, `compile-mode`, and `strict-gate` as DELIVERED. Introducing a generator now would be rework against a working front end, and would put a code-generation step between the language definition and three runtimes.
- **Alternatives considered**: (a) Generate all three front ends from one grammar — rejected: 059 already ruled it superseded, and two of the three front ends are already written and passing. (b) Treat the Dart parser as a normative reference implementation and port it literally — rejected: couples Gleam's structure to Dart's, and conformance already gives the guarantee without the coupling.
- **Consequence**: Cross-runtime syntax agreement becomes an *observable* (FR-002, proven by the corpus) rather than a *construction* (proven by shared generation). Any accept/reject divergence is a named conformance failure.

## D2. Acceptance transports — loopback + TCP only

- **Decision**: Wave 3 acceptance runs over loopback and TCP. QUIC/WebSocket and ZMQ remain reachable behind the seam but are not proven.
- **Rationale**: The seam (`glp/link/seam/*`) and both acceptance transports already exist. 059 recorded engine-side QUIC-WS as **ABSENT** (T055, no `quic_ws.gleam`), and Profile-C QUIC acceptance as **ENV-BLOCKED, not code-absent** — the WSL `quicer 0.2.15` build hook fails and there is no MSVC on this Windows host. Making a known-blocked dependency an acceptance gate would block the wave on a toolchain problem unrelated to its goal.
- **Alternatives considered**: (a) Include ZMQ, since the leaf landed in 059 — rejected: its runtime is WSL-provisioned via `profile_zmq/`, so it imports the same environment fragility for no acceptance value. (b) Include QUIC/WS — rejected: engine side does not exist yet; that is its own feature.
- **Consequence**: FR-025 requires the seam to stay open — the other transports must remain selectable without link-layer code changes, so the deferred work stays cheap.

## D3. Execution target — BEAM, AtomVM deferred

- **Decision**: Full BEAM is the wave-3 acceptance target. AtomVM is deferred to a follow-on, with FR-032 forbidding constructs known to be unavailable there.
- **Rationale**: AtomVM toolchain instability is the wave's **primary recorded risk**. The prerequisite `m2-0-verify-erlang-monitor-atomvm` is delivered and 059 recorded `platform-atomvm` as DELIVERED-by-construction, so the path is not lost by deferring. Gating a seven-item consolidation on the least stable dependency inverts the risk ordering.
- **Alternatives considered**: (a) AtomVM as a hard gate — rejected: dominates the schedule for a capability none of the five user stories needs. (b) Drop AtomVM compatibility entirely — rejected: would silently foreclose the deferred work; hence FR-032 keeps the constraint without the gate.
- **Consequence**: `atomvm_gated_probe.gleam` stays as the compatibility canary. Any deliberate BEAM-only construct must be recorded with its reason.

## D4. Corpus goldens — the 44 are out-of-scope, regeneration is in-scope

- **Decision**: The 44 corpus cases whose reference goldens are missing are declared out-of-scope with the reason `golden missing — 059 T051 drift`; they may not be counted as passes. Regenerating them is wave-3 work (FR-018b, SC-010).
- **Rationale**: Feature 059 commit `c7d65a13` recorded T051 parity as **HALT/ESCALATE — corpus rc=44, 44 missing goldens = evidence-reproducibility drift, engineer decision needed**. Wave 3's entire correctness claim (SC-001) rests on the corpus, so inheriting an unexplained 44-case hole would make the claim meaningless. Counting them as passes would be exactly the "robustness as workaround" Principle II forbids.
- **Alternatives considered**: (a) Count them as passes pending investigation — rejected: violates Principle II and would corrupt SC-001. (b) Delete them from the corpus — rejected: destroys evidence of a known drift. (c) Block the wave until regenerated — rejected: the other four user stories do not depend on them.
- **Consequence**: The corpus report must carry an explicit out-of-scope bucket with reasons (FR-017, FR-018), and `record_dart_goldens.sh` becomes a wave-3 instrument, not just a maintenance script.

---

## Source-level gap analysis

Verified against `glp_gleam/src/` on 2026-07-27. "Exists" means the module is present; the gap is what 059 verification recorded as ABSENT or PARTIAL inside it.

| # | Module | State | Gap to close | Story |
|---|---|---|---|---|
| G1 | `glp/compiler/loader.gleam` | exists | module **static linking** and **dynamic dispatch** — 059: `Unimplemented distribute` | US1 |
| G2 | `glp/compiler/partial_eval.gleam` | exists | `reduce` metainterpreter PARTIAL — blocked on a missing `_copy/2` | US1 |
| G3 | `glp/lint.gleam` | placeholder | bytecode lint ABSENT | US1 |
| G4 | `glp/repl/commands.gleam` | exists | `:boot` and `:bytecode` ABSENT (`:trace`, `:limit` DELIVERED) | US2 |
| G5 | `test/parity/*.sh` | exists | 44 missing goldens; runner must emit explicit out-of-scope bucket | US3 |
| G6 | `glp/engine.gleam` | PARTIAL | composition root has kernels compiled in, **no transport injection seam** | US4 |
| G7 | `glp/link/*` | PARTIAL | inbound pump, link acceptance, capability gate, instance network join ABSENT (T050–T058) | US4 |
| G8 | `glp/link/reliability/frame_codec.gleam` | PARTIAL | FrameCodec + CRC floor only; ordering/rehydration guarantees not established | US4 |
| G9 | `glp/mad/*.gleam` | ABSENT | multiagent boot loader empty; named reference plays malformed on **both** runtimes (`|` type-alt) | US4/US5 |
| G10 | cross-runtime suite | absent | no C#↔Gleam distributed suite exists yet | US5 |

**Note on G9**: 059 recorded the malformed named-reference plays as failing on *both* runtimes. That is a shared defect, not a Gleam gap — under Principle II it must be reported and specified before being "fixed" on one side.

## G1 design dossier — module dispatch is a subsystem port, not a loader patch (2026-07-27)

Read against the Dart reference before starting T009–T011. The `Unimplemented distribute` surface
is in `runner.gleam:391` (catch-all), but the machinery behind it spans four modules:

| Dart reference | What it does | Gleam locus (to create) |
|---|---|---|
| `runtime/glp_activation.dart` `activateModule` | allocate channel var; store a **ModuleTerm** (compiled bytecode as a first-class heap term); spawn GLP-level `serve(Module, ChannelReader?)`; tag it an infrastructure goal; register `rt.glpChannels[name]` | scheduler state + a new `terms` variant |
| `GlpChannelHandle.send` | bind stream writer to `[goal \| NewTail]`, advance writer, return wakes | small — mirrors `_stream_append` |
| `body_kernels.dart:820` `activateKernel` (`_activate/2`) | kernel entry to activation | `engine/kernels.gleam` (currently unregistered) |
| `runner.dart:3375-3479` `Distribute`/`Transmit` | build goal struct from arg slots; look up channel by import-index (static) or deref'd module var (dynamic); `channel.send`; enqueue wakes; hard error if module not activated | `engine/runner.gleam` two new opcode cases |
| `serve/2` GLP infrastructure + `ModuleTerm` | the dispatch loop itself, reading goals off the channel and applying them **inside the target module's bytecode** | needs `ModuleTerm` in `runtime/terms.gleam` — blast radius: heap, unify, codec, output |

**Consequences for the plan**: tasks.md places T009–T011 in `compiler/loader.gleam` — the wrong
locus. The real work is runner+scheduler+kernels+terms, dominated by the `ModuleTerm` variant and
the `serve/2` loop. The REPL suite's Sections F and L (CSSG modules, dynamic dispatch) are the
reference oracle. Sequencing note: T012 (re-load replacement), T013 (lint), T014–T018a, and all of
US2 (REPL commands) are independent of this dossier and can land first.

### Concrete Gleam mapping (decided 2026-07-27, after full reference read)

1. **No `Term` variant.** A module value is the ground sentinel struct `'$module'(<idx:Int>)`
   indexing an engine/scheduler-level module registry — the SAME mapping the port already uses for
   Dart's `MutualRefTerm` (`'$mutual_ref'(addr)`, kernels.gleam:48-54). Zero blast radius in
   terms/heap/unify/codec.
2. **Per-goal programs.** Dart goals each carry a program (`rt.setGoalProgram`,
   `rt.runners[program]` — activateKernel:867-878). The Gleam scheduler is single-program; it
   gains a module registry (`idx → BytecodeProgram`) and a per-goal program reference (default:
   the main program). This is the structural change.
3. **`_activate/2` is NOT a plain kernel.** Gleam kernels are heap-only by contract
   (kernels.gleam module doc) — they cannot enqueue goals. Follow the `_output` precedent:
   thread the effect out as DATA. `KernelOutcome.KSuccess` gains a `spawns` field
   (list of `#(module_idx, label, args)`), which the runner surfaces and the scheduler applies —
   exactly how captured output already flows kernel → runner → scheduler.
4. **`serve/2`** is embedded GLP (Dart `_serveSource`, glp_engine.dart:71-82) — a 2-clause loop:
   `serve(Module, [Goal|In]) :- ground(Module?) | '_activate'(Module?, Goal?), serve(Module?, In?).`
   Compiled once at engine init; spawned per activation as an infrastructure goal (excluded from
   run-status derivation, Dart scheduler.dart:319-329).
5. **Activation** (`activateModule`, glp_activation.dart): allocate channel writer/reader; register
   module in the registry; spawn `serve('$module'(idx), Reader?)`; record the channel writer by
   module name; auto-activate on load iff the module has exports (glp_engine.dart:306-317).
6. **`Distribute`/`Transmit` runner cases** (runner.dart:3375-3479): build `StructTerm(functor,
   collected args)`; resolve module name (static import table / deref'd var); look up the channel;
   `send` = bind stream writer to `[goal|NewTail]`, advance writer, enqueue wakes; hard error
   (terminate) when the module is not activated.
7. **Channel send** mirrors `_stream_append`'s existing walk-and-bind machinery.

Implementation order: scheduler registry + per-goal program → `'$module'` + `_activate` via
extended `KernelOutcome` → activation + embedded serve → `Distribute`/`Transmit` → Section F/L
oracle cases as gleeunit tests. Commit after each green slice.

## Non-regression baseline

- Gleam: **465 green** (recorded in 059 as the floor; raised from 463).
- Repo REPL suite: `bash test/run_all_tests.sh` must stay green (SC-009).
- Both must be captured *before* the first wave-3 code change, per Principle VII.

## Baseline captured — 2026-07-27 (T001–T003, OLAMNIT host)

Toolchain was **absent on this host** and freshly installed (all portable, user-profile, no admin):
Gleam **1.17.0** (`~\.local\bin`, SHA256-verified) · Erlang/**OTP 29** (erts 17.0.4, `~\erlang-otp-29\`)
· Dart **3.12.2** stable (`~\dart-sdk\`). Erlang + Dart bins persisted to the user PATH.

| Task | Suite | Result | Note |
|---|---|---|---|
| T001 | `gleam test` (glp_gleam) | **508 passed, no failures** | the documented 465/463 floor is stale — tree grew since 059; **508 is the wave-3 floor** |
| T002 | `bash test/run_all_tests.sh` | **532/532 passed** | first run failed en masse (`Invalid kernel binary format version (expected 130, found 125)`) — the documented stale-`repl.dill` failure mode after the Dart SDK change; deleted `glp_runtime/.dart_tool/repl.dill`, `dart pub get`, re-ran → all green |
| T003 | `bash test/parity/run_gleam_corpus.sh` | pre-fix: **agree=162 · diverge=44 · exit 44** (reproduced 059 T051 rc=44 exactly, deterministic) → post-ruling fix: **agree=206 · diverge=0 · exit 0 · 100% agreement · 10x bound PASS** | see finding below; the corpus baseline for the wave is **206/0** |

### T003 finding — the "44 missing goldens" are a CRLF harness artifact (Bug Protocol, ruling pending)

All 44 divergences are `MISSING golden` lines; **zero** behavioural DIVERGE lines. But all 44 golden
files **exist** in `test/parity/goldens/runtime/` — git-tracked, LF-clean, names exactly matching the
corpus block ids (`a1…a30`, `gap_g1/g2/g3/g8`, `fork_1`). Hex probe (od -c, 2026-07-27, OLAMNIT):
`corpus.list` is CRLF in a `core.autocrlf` checkout → the runner's block-id parse yields `a1\r` →
`[ -f goldens/runtime/a1\r.golden ]` fails → every block reports MISSING. The script strips `\r`
from REPL transcripts (line 74) but not from `corpus.list`/`expected.list` input lines.

**Ruling (owner, 2026-07-27): root cause confirmed; durable fix directed and applied** — CR-strip in
all three harness read loops (`run_gleam_corpus.sh` ×2, `record_dart_goldens.sh`) + `.gitattributes`
LF pin for `test/parity` (commit `10d66d84`); FR-018a/FR-018b/SC-010 and T027/T028a/T029 revised
spec-first (commit `efac2f19`); marathon item `mitem-019fa481` resolved.

**Verified clean re-run (stable tree, nothing concurrent): agree=206 · diverge=0 · exit 0 —
100% agreement on in-scope cases, 10x wall-clock bound PASS.** All 44 formerly-MISSING blocks AGREE
against their existing goldens. (An interim post-fix run showed 8 apparent divergences — measurement
artifact: it overlapped `gleam test` rebuilds in the same build dir; each such block AGREEs solo and
in the clean run.) Note for the record: 059's Windows corpus runs never actually compared these 44
blocks — today's run is the first true block-level parity evidence on this host, and it is green.

Environment caveat: these artifacts were previously built 2026-07-22 with an unknown (likely peer-host
GAVRI) toolchain; today's toolchain is newer. Everything compiles and passes, but wave-3 results are
attributed to **this** environment, not 059's.

## T009 dossier — project static linking (Section F's mechanism; owner-directed 2026-07-28)

Section F (CSSG modules) runs through Dart's PROJECT loader, not the channel-dispatch subsystem
B1–B4 delivered: `loadProject` (glp_engine.dart:331) = discover → type-check-each → detect top →
link into ONE flat program → compile. Reference spec: `docs/modules/glp-project-compilation-spec.md`.

| Dart reference | What it does | Gleam locus |
|---|---|---|
| `discoverProject` (project_linker.dart:57) | recursive `.glp` listing (skip `boot_direct.glp`, `mad_boot.glp`, `mad_boot/`); parse each; module name = `-module(M)` ?? (self.glp → parent-DIR name : filename base); per-file ancestor chain + scope | discovery I/O in the ENGINE facade (it owns disk, FR-009 precedent: `read_file` external); parse via existing lexer/parser |
| `discoverSelfChain` (module_hierarchy.dart:32) | walk the file's dir up to project root collecting `self.glp`s, ROOT-FIRST; a self.glp's own chain starts at its grandparent | pure path walk over the discovered file set — no extra I/O needed |
| `_buildAncestorScope` (project_linker.dart:442) | prelude env (root self.glp included) + chain-layered self.glp envs; parameterized templates extracted before expansion and threaded to descendants; children shadow parents | ALL seams exist: `teb.build_prelude_environment`, `build_environment_from_module`, `type_ast.merge`, `param_expansion.expand_parameterized_types` |
| `typeCheckProject` (project_linker.dart:121) | per module with own (non-imported) decls: PE → `checkModule(ancestorScope:)`; first error throws | `type_checker.check_module` already takes `ancestor_scope: Option(TypeEnvironment)` |
| `_detectTopModule` (glp_engine.dart:358) | module with imported decls (the orchestrator), else most procedures | pure |
| `linkProject` (project_linker.dart:158) | rename every proc `p/n` → `M:p`; resolve body goals local → ancestor-self chain (innermost wins) → leave (prelude/kernel); STATIC `RemoteGoal` → direct `M':p` call (no channels); dynamic RemoteGoal left as-is; `SpawnGoal` resolves inner; entry aliases (top = ALL procs, others = EXPORTED only, first wins) with MODE-AWARE body args from the ProcDecl (input→reader, output→writer; no-decl fallback all-reader); renamed non-imported ProcDecls returned | new `compiler/project_linker.gleam` — pure AST transformation; `ast.Goal`/`RemoteGoal`/`SpawnGoal` all modeled |
| `compileProgram` (compiler.dart:142) | analyzer `compileMode: system`, **`skipGlobalSRSW: true`** (modules were checked individually — a REFERENCE option, not an invented skip), procDeclarations for relaxation → codegen; stored as `_loadedPrograms['__project__']` | `loader.compile_linked`: PE → codegen in system mode, eliding the SRSW + per-module type-check stages exactly as the reference does; `__project__` enters the facade's loaded registry (participates in `rebuild_program`, re-load replaces) |

Notes: colon-renamed procedures (`M:p`) never pass through the lexer — they are constructed AST,
compiled to labels `"M:p/n"` (labels are plain strings). Dart's `generateReduce: true` concerns the
reduce metainterpreter source generation the Gleam codegen does not have anywhere — parity with the
rest of the port. Oracle: gleeunit over `programs/cssg_modules` — `load_project`, `play1`–`play7` →
Success|Suspended; `fplay1` captured output contains the Section F tagged lines.

Implementation order: dir-listing externals + discovery → ancestor scope → type-check-project →
linker (pure) → `compile_linked` → facade `load_project` → Section F oracle tests → gates
(`gleam test` ≥ 537, corpus 206/0). Risk surfaced loudly, never worked around: the Gleam parser has
not yet seen the CSSG sources (parameterized self.glp types, `ui/` subdir modules) — a parse/check
rejection there is a REPORTABLE gap, not a thing to patch silently.

## US4 dossier — link layer (T032–T042; owner-directed 2026-07-28) — ⛔ SPEC CONFLICT, ruling required

**Reference**: C# `csharp/glp_link/` (the wire format's owner per the contract) + Dart
`glp_runtime/lib/link/` (39 files — primitives / reliability / seam / transports), both carrying the
frozen feature-025 semantics.

**Already in Gleam (feature 050)**: the seam is COMPLETE and faithful — `link_scheme`
(loopback/tcp/quic/zmq), `link_address`, `link_id`, `link_options` (backpressure window, TLS,
temp/perm-fail ms, connect timeout), `link_fault` (Closed/Transient/Permanent), `Endpoint`
record-of-functions vtable (send/recv/close + out-of-band fault `Subject`), `Transport`
(listen/connect); `reliability/crc32` + `frame_codec` (version byte 0x01 with `UnsupportedVersion`
rejection, fragmentation header, CRC — parse/encode both ways); transports `loopback`/`tcp`/`zmq`
with green tests (`frame_codec_test`, `loopback_test`, `tcp_test`).

**Missing in Gleam (the actual T032–T039 surface)**: the PRIMITIVES layer — `LinkEstablish` (the
one canonical wire-and-register core), `LinkPump` (per-link background recv loops + single inbox +
runner-thread `tryApplyNext` extending the `In` stream), `LinkHandle`/`LinkRegistry`/`LinkRuntime`,
egress drainers (heap `onBind` observers on the `Out` writer chain), `LinkFaults`/monitor fan-out,
teardown + distributed-GC hooks, the seven `_link_*` kernels, `inbound_ordering` +
`frame_reassembler` + `fencing_registry` + `cycle_guard`, and the T039 multiagent boot loader.
Engine-integration gaps that make this bigger than the dispatch port: the pure Gleam engine has NO
heap bind-observer hook and NO inbound-pump/run-to-quiescence-await seam — both need new,
carefully-mapped seams (likely the data-threading discipline again + `gleam_erlang` processes for
the genuinely-async recv loops).

### ⛔ The conflict (STOP — Constitution II / spec-first)

`contracts/link-handshake.md` specifies a wire message sequence
`Hello{version, capabilities, identity} → Accept{...} | Refuse{reason}` and rests on the assumption
*"The wire format is the one already established for the C# runtime; Gleam conforms to it."*
**Neither reference implements any such message exchange.** What both references actually have:

1. **Version negotiation** = the frame codec's version byte (0x01); any other byte → the frame is
   REJECTED (`UnsupportedVersion` / FR-022 "bad-version rejected") — never misinterpreted. Already
   ported to Gleam.
2. **Capability enforcement** = the `ICapabilityGate` verify-before-act seam (macaroon-backed gate
   injected per scheme from `glp_crdtmsg`; allow-all default for loopback/tcp) — a LOCAL fail-closed
   gate that records refusals, not a wire negotiation of a capability intersection.
3. **"Handshake"** = the path-B establishment rendezvous: `_link_request` (connector ships a token)
   / `_link_listen` + `_link_accept` (adopt the pending connection).
4. **Refusal surface** = gate refusal / transport fault on the `Faults` monitor with a reason term —
   not a `Refuse` wire message.

Implementing the contract's `Hello/Accept/Refuse` literally would INVENT a wire protocol the C# peer
does not speak — breaking US5's C#↔Gleam interop, the very thing the contract serves — and would be
a cross-runtime protocol addition needing Language-Authority-level approval plus C#-side work.

**Recommended disposition**: amend `contracts/link-handshake.md` to the reference's actual
mechanisms, onto which every rule 1–7 already maps (either side initiates ✓ listen+pump; version
mismatch never best-effort ✓ frame-version rejection; explicit capability refusal ✓ fail-closed
gate; per-link ordering ✓ inbound_ordering; partial frames never delivered ✓ CRC+reassembly; peer
loss bounded ✓ temp/perm-fail ms ≤ 30 s default; refused terminal ✓). Then port T032–T042 against
the amended contract. Awaiting the owner's ruling before any US4 code.

## SC-001 / SC-008 record — 2026-07-28 (T030/T031, OLAMNIT host)

- **In-scope pass rate (SC-001)**: **100% (206/206)** — zero exceptions to name. The ≥95% gate is
  now enforced in `run_gleam_corpus.sh` itself (`in_scope_pass_rate:` line; below 95% prints
  `SC-001 GATE FAIL` and the run cannot exit 0).
- **Determinism (SC-008, T030)**: two consecutive full corpus runs over the unchanged tree produced
  **identical verdict sets (206 `verdict:` lines) and identical aggregate counts**
  (total 206 / pass 206 / fail 0 / out_of_scope 0), both exit 0.
- Out-of-scope at wave end: **0** (SC-010 target met — the 44 "golden missing" cases were the T028a
  CRLF harness defect, fixed durably; nothing remains individually reasoned).

# Gleam/AtomVM GLP Baseline — Cross-Runtime Contract-Reuse Proposal

**Thesis:** The dossier seam shape, the result envelope (§2/§3), the FrameCodec framing (025), and the madGLP TLV link format (A3) are **language-agnostic contracts** with a working C# reference and a shipped codec (029). They are the interop spine. Every Gleam feature is re-cast as *"implement this contract in Gleam,"* so a Gleam back-end and a C# back-end are wire-compatible by construction and the #5 cross-runtime gate passes. BEAM/OTP supplies the operational layers (supervision, mailbox, liveness) the C# epic builds by hand — so those engine-separation features are **superseded, but their contracts are kept**.

## A. End-State Architecture

**M1 — single combined Gleam instance (front-end + back-end).**
- **Back-end (engine):** a supervised BEAM process owning F4's immutable threaded heap + **F5 runner/scheduler** (three-phase HEAD/GUARD/BODY, suspend/reactivate, **goal_id activation-dedupe**) + **F6 compiler/loader**. Logic-var cells spawn via **raw `erlang:spawn` externals + `gleam_erlang` Subjects** — AtomVM-safe, **no `gleam_otp`**.
- **Front-end (REPL client):** thin Gleam client (R7 contract) — console I/O, read-dispatch loop, colon-commands, **all result display**; owns no parse/compile/execute.
- **Seam = the result-envelope contract (#11):** engine returns `status / bindings / var-name→GlobalVarId map / suspended-goal detail / output-blob / errors`, with **server-side deep-resolve** so no live heap addr crosses the seam (the §1 "biggest leak" — equally fatal for Gleam's immutable heap addrs). **Output-capture seam (#10)** routes trace/output into the envelope's blob (R3). Within one BEAM/AtomVM node the seam is a **typed Gleam value over a Subject**; the envelope is specified as a contract so it serializes later (FrameCodec/#15) for an OS-process or cross-runtime split **without touching the engine**.
- **Supervision/liveness:** OTP supervision tree on plain BEAM; on AtomVM a hand-rolled supervisor (raw spawn + `monitor`, no `proc_lib`). This **replaces** the C# liveness/crash/restart/restore host.
- **Persistence (deferred, off M1 path):** the persistent-vs-ephemeral **classification contract** (A1 §5) and store API shape port directly; snapshot-at-quiescence; BEAM restart + replay is the MVP.

**M2 — multiple linked Gleam instances.**
- Each instance = one M1 back-end + a **link layer (#36)** implementing the A3 contracts: **globalize/localize** on the `known/1` seam (per-hop, per-embedded-variable link minting + global-writers routing); **distributed unification = deferred local assignment** (writer-MGU/three-valued/suspend run **locally** each side); **per-link FIFO**; **fault-as-data** on an `ok/tempFail/permFail` monitor stream; **epoch/fencing + global-name idempotency**.
- **Wire = the A3 big-endian recursive TLV term format, byte-for-byte** (tags 1/2/3, const subtypes, the per-variable polarity byte + paired-reader-localId, varint codec, GlobalName/message envelopes, index-0 cold-call sentinel) — a **third byte-parity implementation** against the same adversarial corpus.
- **Framing = the same FrameCodec contract** (025: version byte, 22-byte header, CRC-32, MTU frag, 64 MiB guard), so Gleam frames == C# frames.
- **Transport:** one BEAM process per link (BEAM gives FIFO + monitor mechanism free). Loopback/in-process transport first → real TCP → cross-runtime. BEAM `monitor` is used **only to mint a bound `tempFail`/`permFail` term**, never as auto-propagating `EXIT` (FR-044).
- **#5 capstone:** C#↔Gleam round-trip over the shared FrameCodec+TLV spine, **identical verdicts** on the adversarial corpus.

## B. Alignment Rubric

For each feature ask, in order:
1. **Does it define a contract the Gleam baseline must honor for M1/M2 faithfulness or #5 interop?** → **aligned** (build in Gleam) or **fold-into-gleam** (the contract is absorbed into an existing Gleam feature, no separate feature).
2. **Is its mechanism an operational layer BEAM/OTP provides natively** (supervision, multi-accept, mailbox, crash/restart, cooperative scheduling)? → **supersede-by-beam** (keep the *concept/classification*, drop the C# mechanism).
3. **Is it a C# reference/codec we validate Gleam against rather than port now?** → **keep-cross-runtime**.
4. **Does it constrain the C# seam to stay wire-compatible but need no near-term Gleam work?** → **realign** (re-scope to "contract spec; Gleam impl deferred").
5. **Otherwise** (orthogonal substrate, research, non-Gleam target) → **drop**.

## C. Per-Feature Disposition

- **#2 engine-review-and-design-dossier => keep-cross-runtime (mark RELEASED)** — source of the reused contracts.
- **#11 result-envelope-and-deep-resolve => fold-into-gleam (#6)** — the M1 seam contract Gleam must emit.
- **#10 structured-output-capture-seam => fold-into-gleam (#6)** — output-blob seam feeds the envelope.
- **#4 il-codec-spike => keep-cross-runtime (mark RELEASED)** — IL-codec contract; Gleam IL-on-wire deferred.
- **#15 result-codec-and-framecodec-ride => realign** — envelope wire-codec; needed only at process/runtime boundary.
- **#13 repl-engine-process-split-mvp => supersede-by-beam** — BEAM processes give the split natively.
- **#20 engine-state-snapshot-and-persistence-api => realign** — classification contract ports; Gleam store deferred.
- **#30 liveness-crash-restart-host => supersede-by-beam** — OTP/AtomVM supervision replaces the .NET host.
- **#18 restore-and-resume-with-link-reestablish => supersede-by-beam** — OTP restart + link re-establish native.
- **#21 multi-accept-transport-extension => supersede-by-beam** — process-per-connection is natively multi-accept.
- **#23 compiled-il-on-the-wire-and-factor-out-compiler => drop (defer)** — Gleam keeps source-text seam, compiler engine-side.
- **#17 antlr4-shared-grammar-spike => drop** — Gleam has its own parser; not parity-load-bearing.
- **#29 multi-client-control-program-in-glp => realign (post-M2)** — GLP `serve/2` control-loop reusable for >2-instance server.
- **#28 cpp-engine-feasibility => drop** — orthogonal to the Gleam baseline.
- **#33 many-instances-shared-static-memory-cooperative-scheduling => supersede-by-beam** — BEAM process model replaces it.
- **#16 research-programme-and-llvm-feasibility => drop** — non-gating MLIR/LLVM research.
- **#26 glp-gleam-bytecode-runner => aligned (CRITICAL, M1)** — the runner gate; promote refined→specified; goal_id dedupe.
- **#27 glp-gleam-compiler-and-loader => aligned (M1)** — pipeline to runnable; reuse C#/Dart golden bytecode.
- **#6 glp-gleam-repl => aligned (M1 milestone)** — front-end+back-end seam; absorbs #10/#11.
- **#8 glp-test-corpus-port-and-runner => aligned (M1 GATE)** — golden-output parity vs Dart/C#, green on BEAM.
- **#36 glp-gleam-link-layer => aligned (CRITICAL, M2)** — A3 TLV + globalize/localize + FrameCodec ride + fault-as-data.
- **#5 cross-runtime-csharp-gleam-distributed-tests => keep-cross-runtime (M2 CAPSTONE)** — validates the shared contract spine.
- **#030 marathon-refinement => keep (mark RELEASED)** — harness for the heavy F5/F6/#36 marathons.

## D. Fastest Ordered Sequence

**Critical path is the spine; everything else is parallel/non-blocking.**

**To M1 (critical path):**
1. **Promote #26** refined→specified (readiness unblock; roadmap drift fix).
2. **#26 glp-gleam-bytecode-runner** ⚑CRITICAL — three-phase runner + scheduler over F4's heap; carry the 034 review fixes (suspension-drop, self-bind⇒Unbound) and **enforce goal_id activation-dedupe** (PC-13).
3. **#27 glp-gleam-compiler-and-loader** ⚑CRITICAL — SRSW→PE→typecheck→compile→load; reuse C#/Dart **golden bytecode** for byte-parity.
4. **#6 glp-gleam-repl** ⚑CRITICAL (M1 milestone) — front-end client + back-end engine over the BEAM-process seam, **implementing the #11 envelope + #10 output-capture contracts** (deep-resolve, no heap addrs cross).
5. **#8 glp-test-corpus-port-and-runner** ⚑CRITICAL (M1 GATE) — A4 parity corpus, 100% agreement with Dart outcomes, green on BEAM, **no `gleam_otp`**. → **M1 ACHIEVED.**

**To M2 (critical path continues):**
6. **#36 glp-gleam-link-layer** ⚑CRITICAL — A3 TLV byte-parity codec + globalize/localize + per-link FIFO + fault-as-data monitor + epoch/fencing, **riding the 025 FrameCodec contract**; **loopback transport first to hit SC-001 (byte-identical split)**, then TCP.
7. **#5 cross-runtime-csharp-gleam-distributed-tests** ⚑CRITICAL (M2 CAPSTONE) — one pair-side on C#, one on Gleam, over the shared FrameCodec+TLV spine; **identical verdicts on the adversarial corpus**. → **M2 ACHIEVED.**

**Parallel / non-blocking (run under #030 marathon harness, off critical path):**
- **#20** persistence — Gleam snapshot of the persistent set, after #6; not M1/M2-gating.
- **#29** GLP `serve/2` multi-client control program — after #36, for >2-instance servers.
- **#15** envelope wire-codec — only if an OS-process or cross-runtime REPL seam is wanted; serializes the already-defined #11 envelope.

**No Gleam feature (native BEAM/AtomVM supervision covers them):** #13, #30, #18, #21, #33.
**Dropped/deferred:** #23 (defer IL-on-wire), #17, #28, #16.
**Released contract references (oracles for byte-parity):** #2, #4, #030.

**Roadmap drift to correct:** mark #2, #4, #030 **released**; promote #26 to **specified**.
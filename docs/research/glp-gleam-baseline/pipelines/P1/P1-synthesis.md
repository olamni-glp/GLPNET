# Gleam/AtomVM Baseline — Realignment Review

## 1. Executive answer

The single path is **parity-first, in-process M1 then linked M2 over a byte-parity wire**. Build four Gleam features in order — **#26 bytecode-runner → #27 compiler+loader → #6 REPL → #8 corpus** — to reach a single combined Gleam/AtomVM instance whose ported corpus is 100% green on BEAM with no `gleam_otp` (M1 LOCKED). Then run one gating spike (**M2-0: verify `erlang:monitor` on AtomVM**) and two features — **#36 link-layer → #5 cross-runtime C#↔Gleam** — to reach linked parity (M2). The **biggest scope change vs the current roadmap**: M1 is **in-process** — the REPL/engine OS-process split (#13) and its whole C# operational stack (#20/#30/#21/#33/#18) is dropped or superseded-by-BEAM, and every C# spike (#4/#23/#17/#28/#16) leaves the path. Thirteen features collapse into six Gleam features plus one spike; **parity evidence, not feature count, defines the baseline.**

## 2. Alignment verdict

**ALIGNED — carry forward (build in Gleam as-is):**
- **#26 glp-gleam-bytecode-runner** — the F5 three-phase HEAD/GUARD/BODY runner + scheduler; gates PC-2/3/11/14, the only M1 semantics not already in shipped F4.
- **#27 glp-gleam-compiler-and-loader** — SRSW→PE→typecheck→compile→load; without it no `.glp` loads, M1 unreachable; dossier rates it "largely unchanged."
- **#6 glp-gleam-repl** — the single combined instance; reaching it = M1.
- **#8 glp-test-corpus-port-and-runner** — the machine-checkable M1 LOCK gate vs recorded Dart outcomes.
- **#36 glp-gleam-link-layer** — the entire M2 feature: TLV wire, distributed-unify-as-local-assignment, globalize/localize, fault-as-data.
- **#5 cross-runtime-csharp-gleam-distributed-tests** — M2 capstone; independent C# oracle catches wire-parity defects a Gleam-only test masks.

**REALIGN / FOLD (keep but re-target to Gleam or merge):**
- **#11 result-envelope-and-deep-resolve** — fold into #6 as the engine's typed return value + deep-resolve so no immutable heap address escapes.
- **#10 structured-output-capture-seam** — fold into #6 as the output/trace blob field (near-zero work; no mutable Console exists in Gleam).
- **#15 result-codec-and-framecodec-ride** — fold its **frame-envelope byte-parity** into #36; the envelope-on-wire codec is realign(defer).
- **#29 multi-client-control-program-in-glp** — fold `serve/2`+`mwm` into #36 step 5c for N-client topologies.
- **#2 engine-review-and-design-dossier** — keep as the cross-runtime contract/parity oracle; released; harvest its contracts into the Gleam specs.
- **#20 engine-state-snapshot-and-persistence-api** — realign(defer) Gleam-native after #6; immutable heap erases its C# atomicity/GC hazards.
- **#18 restore-and-resume-with-link-reestablish** — realign(defer) after #20+#36; restart itself is BEAM-native.
- **#030 marathon-refinement** — released harness; re-point it to drive the heavy #26/#36 marathons (workload-agnostic, zero internal change).

**NO-LONGER-ALIGNED (supersede-by-BEAM or drop):**
- **#13 repl-engine-process-split-mvp** — supersede-by-BEAM; M1 stays in-process.
- **#30 liveness-crash-restart-host** — supersede-by-BEAM (OTP supervision/restart).
- **#21 multi-accept-transport-extension** — supersede-by-BEAM (`gen_tcp` accept loop, process-per-link).
- **#33 many-instances-shared-static-memory** — supersede-by-BEAM (shared module code/literals + reduction-counted scheduler).
- **#4 il-codec-spike** — drop; shipped (029) as reference; M1 has no wire, M2 carries source/terms not IL.
- **#23 compiled-il-on-the-wire + factor-out-compiler** — drop; compiler stays engine-side, source rides the wire.
- **#17 antlr4-shared-grammar-spike** — drop; ANTLR4 has no Gleam target; parser hand-ports in #27.
- **#28 cpp-engine-feasibility** — drop; alternative substrate; its footprint thesis is exactly what BEAM answers.
- **#16 research-programme + llvm-feasibility** — drop; non-gating research orthogonal to the baseline.

## 3. Critical milestones

**M1 — single-instance Gleam parity (the in-process combined instance).** Backbone, in order:
1. **#26 (critical)** — ports the WAM runner over F4's immutable heap; owns the scheduler that *consumes* activations; enforces PC-2 (HEAD purity), PC-3 (head two-phase), PC-11 (reactivate-from-kappa), PC-14 (committed-choice/no-trail), and the carried 034 obligations: PC-8 self-bind⇒Unbound, forward-to-terminal suspension-preservation, and **PC-13 `goal_id` dedupe** (immutable value-copy loses the cross-writer single-fire guard).
2. **#27 (critical)** — produces the bytecode #26 executes; it is the *producer* side of the faithfulness contract; targets #26's instruction set (hence sequenced after it).
3. **#6 (critical)** — the front-end driver; **reaching it = M1 single combined instance**; absorbs #11/#10 so results/output are faithful and observable.
4. **#8 (critical)** — converts "identical observable semantics vs Dart" from claim to green/red gate; **corpus-green = M1 LOCKED.**

**M2 — multi-instance linked parity.** Backbone:
- **M2-0 spike (critical, gating)** — verify `erlang:monitor` on AtomVM v0.6.6 before committing the fault model (unverified by ground docs).
- **#36 (critical)** — delivers the whole linked layer: byte-for-byte TLV term codec + FrameCodec envelope, distributed-unify-as-deferred-local-assignment (deref/WxW/compression stay local), globalize/localize on `known/1`, reliability sublayer, fault-as-data lattice, loopback-then-`gen_tcp` transport; absorbs #15 frame-envelope and #29 serve/2.
- **#5 (critical, capstone)** — one role-parameterized program split across a shipped C# instance and a Gleam instance; identical adversarial-corpus verdicts = **M2 cross-runtime confidence locked.**

## 4. Refinements

- **#11 → #6:** make the envelope field-set (`status` / `bindings` / var-name→`GlobalVarId` map / suspended-goal detail / captured-output / errors) the engine's **return type**; run deep-resolve inside the result producer. Built ready, un-wired on M1.
- **#10 → #6:** drop the C# Console-inventory/TraceSink-gap work; thread an output accumulator through the immutable runner state into the blob; route `:trace` into it. Merge with #11 into one engine-result-contract sub-spec.
- **#15 → #36:** implement a Gleam-native FrameCodec (version byte, 22-byte big-endian header, CRC-32, MTU frag, 64 MiB guard) validated against the shipped C# FrameCodec as oracle; discard OffKind/Dart-mirror corrections. Defer the result-envelope-on-wire codec post-baseline (only if a cross-process REPL seam is wanted).
- **#29 → #36:** promote `serve/2` from the C# embedded const into `self.glp` so it is engine-neutral; drop the dead #10-multi-accept and #11-IL deps; resolve the real gap — per-client result-routing back over the originating `Link Out`.
- **#2:** mark released; harvest S2.3 envelope + S1.3 deep-resolve into #6, S3 frame-envelope + PayloadSerializer tag scheme into #36/#5; add a head-note that its OS-process-split MVP thesis is superseded (M1 in-process).
- **#20:** re-express Gleam-native after #6 — persist the engine value at quiescence (immutable tree, no GC/atomicity hazard); retain only the persistent-vs-ephemeral classification. Merge with #18 into one post-baseline persistence feature.
- **#18:** drop C# mutable-heap plumbing (RewireHandle, `_bindCallbacks` re-arm, address remapping); keep the reload-constructs + re-establish-links-from-global-name + resume-drain contract and the kill-and-restart test, retargeted to a single Gleam node; sequence after #20+#36.
- **#030:** register #26 and #36 (incl. 5a/5b/5c sub-stages and M2-0) as marathon runs; fix the stale `specified`/`Draft` metadata; keep its emergent-work mini-pipeline as intake for mid-port discoveries.

## 5. Fastest ordered sequence

Today's state: F4 shipped (terms/heap/writer-MGU unify/suspension storage, 54 green on BEAM); F3 subtree green; toolchain proven (Gleam 1.17.0/OTP 25.3.2.8, WSL).

**Step 0 — roadmap hygiene (minutes, non-gating):** mark #2/#4/#030 released; promote #26 `refined`→`specified`; fold #11+#10 into #6's spec; fold #15 frame-envelope + #29 into #36's spec; record #13/#21/#30/#33 superseded-by-BEAM; park #20/#18 realign-deferred; drop #23/#17/#28/#16.

**Critical path to M1:**
1. **#26 bytecode-runner** (heavy/marathon under #030) — gates PC-2/3/11/14 + 034 carries.
2. **#27 compiler+loader** — reuse Dart/C# golden bytecode (`dump_bytecode`) as the parity acceptance gate; loader spawn via raw `erlang:spawn` + Subjects, never `gleam_otp`/`proc_lib`.
3. **#6 REPL** — in-process, engine called as a typed Gleam value; absorbs #11/#10. ⇒ **M1 single combined instance reached.**
4. **#8 corpus** — extend the shipped 11-scenario F4 parity seed forward to the F5 PCs + the program-level `.glp` corpus; 100% agreement with recorded Dart, green on BEAM, no `gleam_otp`; add a secondary AtomVM-host gate. ⇒ **M1 LOCKED.**

**Critical path to M2:**
5. **M2-0 spike** (early/gating) — `erlang:monitor` on AtomVM; fallback = raw-spawn fault-monitor.
6. **#36 link-layer** (heavy/marathon under #030), in evidence order: **5a** TLV codec + FrameCodec envelope + globalize/localize on `known/1` + global-writers routing; **5b** loopback transport + distributed-bind-as-local-assignment ⇒ hit **SC-001 byte-identical split first**, then `gen_tcp`; **5c** reliability sublayer + fault-as-data monitor + epoch/fencing + fold-in #29.
7. **#5 cross-runtime C#↔Gleam** (capstone) — reuse 025's adversarial corpus + harness verbatim; validate Gleam vs **both** Dart (cheap) and the shipped C# oracle; add only a Gleam endpoint adapter + one transport leg. ⇒ **M2 capstone reached.**

**Parallelizable side-tracks (non-blocking, off critical path):**
- M2-0 spike can run in parallel with #26/#27 (no dependency).
- Gleam-native FrameCodec (5a) byte-parity work can be prototyped against the shipped C# FrameCodec while M1 finishes.
- #20 (after #6) and #18 (after #20+#36) run post-baseline under #030.

**Reused existing artifacts:** shipped **F4 runtime kernel** (local writer-MGU/suspend, reused unchanged across the wire boundary); **025 FrameCodec/TcpTransport + `csharp/glp_link` (40 files)** as the M2 byte-parity oracle and #5 endpoint; **025 adversarial corpus + test-matrix** reused verbatim for #5; **C# reference at `out/csharp`** + the **Dart `glp_runtime/lib/link` (39 files)** as port source-of-truth and parity oracles; **029 il-codec** kept as released reference only (not on path); **#2 dossier** contracts harvested into specs. Each linked instance is a complete M1 engine.

## 6. Roadmap drift & housekeeping

Stale rows to correct:
- **#4 il-codec** shows `captured`/`specified` but is **shipped+released as 029** (`v2026.06.11.1`) → mark **released**.
- **#2 engine-review-and-design-dossier** delivered+ratified 2026-06-09 (026) → mark **released**; its headline OS-process-split thesis no longer matches the in-process M1 goal — annotate as superseded.
- **#030 marathon-refinement** shows `specified`/`Draft` but is **shipped+released** (`v2026.06.12.1` + `v2026.06.19.1`, all 8 phases) → mark **released**; fix `spec.md:5` "Draft" header.
- **#26 bytecode-runner** is `refined` but must be `specified` before the pipeline can start — a readiness lag that **blocks the M1 critical path**; promote first.

Blocked-by chains to rewire:
- **#8** roadmap lists only `blocked-by #6`, understating the real chain **#26→#27→#6→#8**; correct it and re-order #8 from the roadmap tail to M1-LOCK position 4 (carve its cross-runtime adversarial duty to #5).
- **#6** global build-order routes through **#15 result-codec then #13 process-split** — both off the in-process M1 path; remove that detour, leave #6 blocked-by #27 only.
- **#29** blockers #10-multi-accept and #11-IL-on-wire dissolve (multi-accept is BEAM-native; IL dropped) → collapse to **blocked-by #36**.
- **#20→#30→#18** C# chain rewires: restart is BEAM-native, so #18 depends on Gleam-native #20 + #36, not on #30.
- **#36** add the **M2-0 `erlang:monitor` spike** as an explicit gating dependency.

## 7. Recommended roadmap actions (ADVISORY — not executed)

1. **Promote** #26 `refined`→`specified` (unblocks the entire M1 critical path).
2. **Mark released:** #2, #4, #030 (clear shipped-but-stale rows); fix the #030 "Draft" spec header.
3. **Fold + re-spec:** #11+#10 into #6; #15 frame-envelope + #29 into #36 (and move `serve/2` into `self.glp`).
4. **Add a new gating spike** M2-0 (`erlang:monitor` on AtomVM v0.6.6) ahead of #36's fault-model work; runnable in parallel with M1.
5. **Record superseded-by-BEAM:** #13, #21, #30, #33 (operational layers only; retain any classification notes).
6. **Park realign-deferred:** #20 (after #6), #18 (after #20+#36); merge them into one post-baseline Gleam persistence feature.
7. **Drop:** #23, #17, #28, #16 (off-path; revisit only post-baseline if a hard non-BEAM requirement materializes).
8. **Re-order to the single critical path** #26→#27→#6→#8 (M1) then M2-0→#36→#5 (M2); re-point #030 to drive #26 and #36 as marathon runs.
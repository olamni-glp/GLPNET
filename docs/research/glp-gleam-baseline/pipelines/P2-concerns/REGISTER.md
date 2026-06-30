# P2 — Concerns Register

- **Feature:** 036-glp-gleam-baseline-program
- **Marathon run:** mrun-5611c436ba95
- **Task:** T011 (P2 concerns pipeline — exhaustive, loop-until-dry)
- **Date:** 2026-06-29
- **Contract honored:** `pipelines/contracts/pipeline-contract.md` — every concern carries cited evidence (file:line / page / URL), a severity, and the affected feature(s); grounded in verified artifacts only; read-only; no invention; no "fastest-path" rubric.

## Cite key

PB = `pipelines/P4-faithfulness/PARITY-BAR.md` · PROOFS = `pipelines/P4-faithfulness/PROOFS/INDEX.md` · P6 = `pipelines/P6-gleam-impl/DOSSIER.md` · ANTLR = `pipelines/ANTLR-integration/DOSSIER.md` · DEC = `pipelines/P5-il-machine-language/DECISIONS.md` · P5D = `pipelines/P5-il-machine-language/DOSSIER.md` · CA = `pipelines/P1/P1-chosen-approach.md` · CRIT = `pipelines/P1/P1-critique.md` · IDX = `pipelines/INDEX.md` · CORPUS = `CORPUS-INDEX.md` · DISP = `pipelines/P1b-realignment/DISPOSITIONS.md` · DM = `data-model.md` · SPEC = `spec.md`. Code/term cites (HEAP, RUN, SCHED, OPS2, heap.gleam, ByteIo.cs, mad_context.dart …) and PDF/core/cat book cites are reproduced as the source angle pinned them. Severity labels are preserved verbatim per row; the manifest 3-bucket tally rounds **med-high→high** and **low-med→med** (conservative round-to-nearest-named-tier).

## Dedup note

Within each angle, round-1 and round-2 concerns are merged with no intra-angle repeats (round-2 mining emitted *new only*; critic emitted *new risk classes only*). Original sub-IDs (FG-/AL-/BLK-/A-/B-/C-/D-/E-/ST2-/PR-/MISS-) are preserved in-row for traceability. **Cross-angle recurrence is expected** (each angle is a different lens) and is intentionally retained because each lens cites distinct evidence; the genuinely-shared root issues that surface in ≥3 angles are listed under "Cross-angle duplicate clusters" at the end.

---

## A. Faithfulness-gaps (semantic parity / proof obligations)

| id | description | severity | evidence | affected_features | status |
|----|-------------|----------|----------|-------------------|--------|
| C-01 | [FG-01] Writer-MGU soundness UNPROVEN (binds only writers, never reader/writer↔writer, incl. self-bind→Unbound recognizer); only code-cite + 034 test seed, no Lean theorem | high | PROOFS:14 (open, RISK-PROOF-writerMGU); PB:68-69,146; HEAP:455-457/364-367/378-379/416-441/312-323 | F5, P4, both output epics | open |
| C-02 | [FG-02] Distributed-deref M2 faithfulness UNPROVEN; only a minimal single-message SPIN handshake passes, no Promela model of multi-message bidirectional suspend/reactivate with no-lost/no-dup safety + liveness | high | PROOFS:17 (open, RISK-PROOF-distDeref, 5 steps); PB:111,113,147 | F9, P4, Full-Gleam | open |
| C-03 | [FG-03] 42 of 45 M1 criteria are "cite only"; only 3 proof obligations discharged | med | PB:51,55-99,101; PROOFS:22-25 | P4, P8 | open |
| C-04 | [FG-04] Exec-equivalence proofs cover merge/3 CLAUSE 1 ONLY; committed-choice, guard purity, multi-clause first-success unexercised | high | PROOFS:15-16; DEC:67-68; ANTLR:30,112 | F5, P4 | open |
| C-05 | [FG-05] SRSW-preservation proof = one-clause/one-transform core-Lean feasibility spike (5/20 tactics, Mathlib absent), not the full suite | med | PROOFS:13 (RESULT.md:30,57-59) | P4, Full-Gleam | open |
| C-06 | [FG-06] `forward_to_terminal` Dart line NOT pinned (FB-M1-40/FR-008); Gleam side internally consistent but unanchored to Dart | med | PB:94,150 (RISK-CITE-1); P6:61,141 | F5, P4 | open |
| C-07 | [FG-07] FB-M2-04 rests on a contested `mad_context` snapshot; needs re-verify gate recurses into compound args + imported reader reactivates | med | PB:113,151 (RISK-M2-CONTEST) | F9, P4 | open |
| C-08 | [FG-08] FB-M2-08 original-creator-id holds at serializer layer ONLY; live `_lookupVariableForSerialization` returns relayer id | med | PB:125,155; mad_context.dart:180-184 | F9, P4 | open |
| C-09 | [FG-09] M2 yardstick Thm 5.7 (+lemmas C.41/C.45) truncated in arXiv HTML; no validated whole-system M2 oracle until re-grounded | high | PB:143 (RISK-RUBRIC-M2) | P4, F9, Full-Gleam | open |
| C-10 | [FG-10] Outcome-equivalence is the M1 judging method (Thm 3.34/Rem 3.35), not a per-run testable point; mis-listing hazard | low | PB:9,142 (RISK-RUBRIC-M1) | P4, P8 | accepted |
| C-11 | [FG-11] GAP-G1: `ground/1` SRSW relaxation absent from both lenses; Gleam SRSW checker would wrongly reject valid programs | high | PB:165 (core:139) | F6, ANTLR front-end, P4 | open |
| C-12 | [FG-12] GAP-G2: clause-head standardize-apart per reduction + recursion non-aliasing absent; no criterion/test | high | PB:166 (PDF p.5 Def 3.10; core:118) | F5, P4 | open |
| C-13 | [FG-13] GAP-G3: fairness/liveness (perpetually-reducible goal eventually reduced) unspecified; scheduler-actor obligation unpinned | high | PB:167 (core:112); P6 §1.7 | F5, P4 | open |
| C-14 | [FG-14] GAP-G4: computed-answer soundness (logical consequence) unproven; P4 proof obligation not authored | med | PB:168 (core:153-157) | P4, Full-Gleam | open |
| C-15 | [FG-15] GAP-G5: communication-formed circular terms must be graceful but live deref raises loud SRSW error (HEAP:265-266); CONFLICTS FB-M1-23 | high | PB:169,77-79 (core:166); P6:131-133 (FORK-1) | F5, P4, owner gate | open |
| C-16 | [FG-16] GAP-G6: distributed top-level status/quiescence oracle UNDEFINED; M2 linked parity not testable until defined | high | PB:170 | F9, P4 (M2 bar), Full-Gleam | open |
| C-17 | [FG-17] GAP-G7: suspension-set MINIMALITY (Def 3.21) unspecified as parity requirement vs latitude | low | PB:171 (PDF p.7 Def 3.21); P6 §1.2 | F5, P4 | open |
| C-18 | [FG-18] GAP-G8: guard three-valued coverage incomplete (`=:=`,`<`,`==`/`\==`,type tests,`known` unspecified) | med | PB:172 (cheat §guards) | F5/F6, P4 | open |
| C-19 | [FG-19] FORK-1: circular-term deref discriminator boundary is an OPEN owner decision (Option A loud-all vs Option B structural-vs-cycle); must NOT be Claude-chosen | high | PB:178-185; P6:131-133 | F5, P4, owner gate | open |
| C-20 | [FG-20] FB-M2-R2: epoch/fencing token for conflicting double-bind is a recommendation, NOT in code; FB-M2-20 monotonicity emergent | med | PB:160,157,130 | F9, Optional epic, owner | open |
| C-21 | [FG-21] FB-M2-R3: M2 wire-framing UNSETTLED (version byte/CRC/fragmentation/dual polarity/in-band sentinel) | med | PB:161 | F9, ED-6 codec, Full-Gleam | open |
| C-22 | [FG-22] FB-M2-R4: long forwarding-chain (A→…→N) end-to-end parity has no theorem; rests on implicit unary-hop assumption | med | PB:162 | F9, P4 | open |
| C-23 | [FG-23] FB-M2-09: cross-instance RPC is NOT a single unified mechanism (intra = bytecode RPC; cross = global_send/handleMadAssignment) | low | PB:126,156 | F9, P4 | open |
| C-24 | [FG-24] ED-6 float-decode on AtomVM UNVERIFIED (double stored as raw IEEE bits; `/float` bit-syntax support not grounded) | high | P6:139 (ByteIo.cs:54-56); §3.4 | ED-6 codec, F5/codec, Full-Gleam | open |
| C-25 | [FG-25] Signed-LE-64 extraction must be hand-coded (AtomVM LE-64 unsigned-only); reinterpret two's-complement for negative ConstInt + varint cap | med | P6:107-108,140 | ED-6 codec, F5 | open |
| C-26 | [FG-26] Integer-overflow parity MASKED on BEAM (Gleam Int = bignum) vs AtomVM/Dart 64-bit wrap; must exercise on AtomVM | med | P6:142; §3.2 | F5, P4 (M1 tests) | open |
| C-27 | [FG-27] Section-15 bytecode binary codec DOES NOT EXIST for Dart/Gleam path; 029 C# IlCodec must NOT be cited as proof | high | DEC:44-46; CORPUS §E | ED-6 codec, ANTLR, F5, Full-Gleam | open |
| C-28 | [FG-28] IL op-verifiers (phase-order/SRSW/writer-MGU) only a 4-op MLIR round-trip smoke; full suite unbuilt | med | DEC:50-51 | F6/front-end IL, P4 | open |
| C-29 | [FG-29] v2.16.3 ISA NOT frozen/versioned; v1/v2 opcode split unresolved before crossing any boundary | high | DEC:7-8,49; PB FB-M1-06/13; OPS2 isReader | ED-6 codec, all runtimes, P4/P8 | open |
| C-30 | [FG-30] "M2 parity ≠ ISA-identity" conflation hazard (bytecode-on-wire M1 vs term-level M2) | low | DEC:51; PB:11 | P4, F9, P8 | open |
| C-31 | [FG-31] FB-M1-35 cross-cell single-fire gap; literal "dedupe by goal_id" drops a lawful 2nd-episode activation (breaks FB-M1-38); correct key = (goal_id, suspension_generation) | high | P6:43-47,143,152; PB:89; 034 review | F5, P4 | open |
| C-32 | [FG-32] Reader-side suspension routing + imported readers DEFERRED to F9 (the M2 linked-parity heap seam); must be named, not assumed free | med | P6:144 | F9, P4 (M2 heap seam) | open |
| C-33 | [FG-33] A clean ANTLR grammar silently CHANGES accepted language (`=..` uniform head+body; structs-in-lists); must be explicit owner decisions | med | ANTLR:128; CLAUDE.md known-issues | ANTLR front-end, F6, owner | open |
| C-34 | [FG-34] ANTLR spike proves the SPINE ONLY (merge/3 cl.1); infix heads, `::=`, module envelope, ~25-op lexer, guard disjunction, Module target unexercised | med | ANTLR:110-124,30 | ANTLR front-end, F6, P4 | open |
| C-35 | [FG-35] Cross-target (Dart↔C#) byte-identical-AST parity NEVER shown for GLP; antlr4 4.13.1 vs 4.13.2 skew | med | ANTLR:126,67 | ANTLR front-end, P4 | open |
| C-36 | [FG-36] ETS-OUT ruling rests on inference-from-absence, not an observed-failure spike (F1 never attempted ETS on v0.6.6) | low | P6:128 (FORK-B B2) | F5 binding-store fork, owner | open |
| C-37 | [FG-37] `MSTACK/docs/diana` DOES NOT EXIST; P7 grounding missing | med | CORPUS:101-105 | P7 (QHSM/YngeniOS), P8 | open |
| C-38 | [FG-38] `qhstate-Yngenios` cited as a STUB; P7 grounding thin | low | CORPUS:106-108 | P7, P8 | open |
| C-39 | [FG-39] Sibling-GLP-repo duplication → canonical-tree ambiguity for faithfulness cites | low | CORPUS:109-110 | P4, P8 (citation hygiene) | open |
| C-40 | [FG-40] deref NON-recursion into struct args (FB-M1-19) is an unflagged observable-parity invariant; eager resolve of embedded VarRefs diverges | med | PB:73 (HEAP:331-333; BPT:19,71,98); P6:81 | F5, P4 | open |
| C-41 | [FG-41] Circular-term discriminator is THREE-way (FB-M1-22→Unbound vs FB-M1-24→Bound) beyond the two-way FORK-1; finer unpinned boundary | med | PB:76,78; P6:81,133 | F5, P4, owner gate | open |
| C-42 | [FG-42] Suspension FORMATION (FB-M1-30/31/32): goal-level U accumulation + two-phase commit-time Si resolution (spurious-suspension prevention) cite-only, exercised only on merge/3 cl.1 | med | PB:84-86 (RUN:282-287/2277-2296; cat:352-369) | F5, P4 | open |
| C-43 | [FG-43] M1 top-level STATUS ORACLE (FB-M1-42/43/44) cite-only with non-obvious rules (exclude infra/serve goals; success iff ≥1 reduction); M1 analogue of GAP-G6 | med | PB:96-98 (SCHED:322-323/300-314); P6:65 | F5 (verdict), P4 | open |
| C-44 | [FG-44] index-never-reused dedup chain (FB-M2-14→18→20) collides with AtomVM 64-bit/no-bignum: counter wrap silently turns dedup NO-OP into destructive double-bind | med | PB:117,120,130,157; P6:97 | F9, P4 (M2), Optional epic | open |
| C-45 | [FG-45] Recovered-11 M2 criteria grounded in `docs/ma/madGLP-spec.md` whose fidelity to the truncated arXiv normative source is itself unverified (transitive grounding gap) | med | PB:143,107,153-154 | P4 M2 bar, F9, P8 | open |
| C-46 | [FG-46] FB-M2-16 mandates NO wire-side SRSW re-check × unsettled framing (FB-M2-R3) ⇒ malformed/corrupt frame silently absorbable; only the framing spec can close it (defensive recheck would itself be unfaithful) | low-med | PB:118,161 | F9, P4 | open |
| C-47 | [FG-47] Loud-cycle error (FB-M1-23) is observable, but its trigger (revisited-address visited-set) depends on path-compression/addressing state the bar EXPLICITLY EXCLUDES from parity | med | PB:9,77; P6:81 (heap.gleam:134-194,146-147) | F5, P4 | open |
| C-48 | [FG-48] FB-M2-19/21: Communicate decomposed into 3 local unary transitions (Reduce→Send→Receive) with send-atomicity-within-one-Reduce; Dart-only-cited, no Gleam M2 code, folded under OPEN distDeref proof | med | PB:129,131; PROOFS:17 | F9, P4, M2 | open |
| C-49 | [FG-49] Single non-PDF (book/appendix) cite for load-bearing M1 invariants: conjunction outcome-priority (FB-M1-09 core:75), two-phase collection (FB-M1-32 cat-only), monotonicity (FB-M1-45 core-only) | low | PB:63,86,99 | F5, P4, P8 | open |

---

## B. AtomVM hard limits

| id | description | severity | evidence | affected_features | status |
|----|-------------|----------|----------|-------------------|--------|
| C-50 | [AL-1] `gleam_otp`/`proc_lib` absent from AtomVM; OTP/gen_server designs crash; only raw `erlang:spawn` + Subjects proven | high | P6:91-93,130-134; README:18-23; atomvm.net pg v0.6.5 | F5, F6, F9, M1 scheduler | open |
| C-51 | [AL-2] Integers 64-bit, no bignums; all heap/term ints must stay ≤64-bit | high | P6:95-97,73; atomvm.net mem v0.6.2 | F4/F5 term+heap, ED-6 codec | open |
| C-52 | [AL-3] Integer-overflow parity MASKED on BEAM bignum (false-green) vs AtomVM/Dart 64-bit wrap | high | P6:142 | P4 test harness, F5 | open |
| C-53 | [AL-4] ED-6 float-decode on AtomVM UNVERIFIED (raw IEEE bit pattern; `/float` bit-syntax support unknown); "must spike before committing the Gleam codec" | high | P6:139,103-108; DEC:44-46; atomvm.net pg v0.6.5 | ED-6 codec, bytecode-on-wire seam, Full-Gleam | open |
| C-54 | [AL-5] AtomVM bit-syntax limits: 8-bit boundaries, no sub-byte binaries (byte-aligned format auto-satisfies these) | med | P6:103-108; atomvm.net pg v0.6.5 | ED-6 codec, F5 | open |
| C-55 | [AL-6] Signed-LE-64 unsupported → must read `:64-unsigned-little` + reinterpret two's-complement in pure Gleam (negative ConstInt) | med | P6:107,140 | ED-6 codec (ConstInt) | open |
| C-56 | [AL-7] varint 64-bit-cap guard (`shift>=64`, ByteIo.cs:33) must be replicated in the Gleam reader | low-med | P6:108,101 | ED-6 codec | open |
| C-57 | [AL-8] Atoms >255 bytes unsupported (mitigated: names kept as Gleam binaries, not atoms) | low | P6:97,73; atomvm.net mem v0.6.2 | F4 term representation | mitigated |
| C-58 | [AL-9] Per-process heap+stack model / ~512K RAM floor concentrated in one scheduler-actor; all GC pressure in one process | med | P6:97,122,29-33; atomvm.net mem/welcome | M1 scheduler-actor, F5 | open |
| C-59 | [AL-10] `epmd`/`disterl` unsupported → no native BEAM distribution; M2 must ride the explicit seam | high | P6:112; atomvm.net pg v0.6.5 | M2, F9, maGLP term-link | open |
| C-60 | [AL-11] No REPL in AtomVM subset; REPL must stay on Dart/BEAM-host while AtomVM runs only the engine | med | P6:112; atomvm.net pg v0.6.5 | REPL/engine-sep epic, M1 packaging | open |
| C-61 | [AL-12] ETS absent from AtomVM v0.6.x and not a value → forecloses an ETS binding store (caveat: inference-from-absence) | med | P6:85,128 | F5 binding-store design | open |
| C-62 | [AL-13] Process-per-variable model unsafe on AtomVM (single-fire race + RAM floor + receive-resumes-where-blocked ≠ resume-at-κ); pushes design away from BEAM-native | med-high | P6:123,85; PB:89,92 | F5 concurrency model, FB-M1-35/38 | open |
| C-63 | [AL-14] Host build: filesystem-only, no NVS/flash; must pack to `.avm` via packbeam | low-med | P6:112; README:111-117; toolchain-inventory:101-103 | persistence, packaging (F2/F3) | open |
| C-64 | [AL-15] AtomVM viability proven only on HOST build, never embedded HW; all limit evidence is host-build | med | gleam-atomvm/dossier.md:84-89; toolchain-inventory:19 | AtomVM verdict, P7 edge, embedded deploy | open |
| C-65 | [AL-16] AtomVM bit-syntax decode spike is an OPEN undischarged obligation; no discharged proof for Dart/Gleam codec target (only 4-op MLIR smoke) | high | DEC:42-50; P6:139 | ED-6 codec, seam, Full-Gleam | open |
| C-66 | [AL-17] Dynamic AtomVM build needs `libmbedtls.so.10` (absent on Ubuntu noble) → static-mbedtls asset mandatory | low | toolchain-inventory:72-79 | toolchain/packaging (F2/F3) | open |
| C-67 | [AL-18] AtomVM SMP/pre-emption UNUSED by one-big-actor M1 design; single-heap is the bottleneck (scaling note, not correctness) | low | P6:93,122; atomvm.net welcome | M1 scheduler-actor perf, F5 | accepted |
| C-68 | [AL2-1] M2 cross-instance TRANSPORT unverified; raw spawn+Subjects are same-NODE only; `gen_tcp` on AtomVM v0.6.6 host never opened — "raw spawn satisfies M2" is a category error | high | CA:32,140; P6:112; README:25-26 | F9, M2, ED-1 transport, Full-Gleam | open |
| C-69 | [AL2-2] `erlang:monitor` on AtomVM v0.6.6 UNVERIFIED; M2 fault-as-data mints tempFail/permFail from a monitor stream; needs early gating spike (M2-0) | med-high | CRIT:11,62; CA:31,45,137 | F9, M2 fault model, Full-Gleam | open |
| C-70 | [AL2-3] AtomVM timer/timeout primitives (`send_after`/`after`/`timer`) UNVERIFIED; needed by reliability sublayer + epoch/fencing + escrow-timer plays | med | CA:31; README:13-16,25-26; P6:91-93 | F9, M2 reliability/fencing, plays | open |
| C-71 | [AL2-4] Heap+stack share ONE region growing toward each other → deep/cyclic non-tail recursion (deref path-compression, struct-unify, long WriterBound chains) collides with the single large actor heap | med | P6:97,31,81,134 | F5, M1 scheduler-actor, FORK-1 deref | open |
| C-72 | [AL2-5] Single-big-heap mitigation (`fibonacci`/`bounded_free` heap-growth spawn-option) UNEXERCISED; may not be reachable via raw spawn on AtomVM | low-med | P6:97,122; README:11-16,27-28 | F5, M1 scheduler-actor scaling | open |
| C-73 | [AL2-6] AtomVM viability = ONE trivial atom-bind over 2-3 processes; no numeric/float-math/list/deep-struct term, no unification, no suspension/reactivation ever ran on AtomVM | med | README:8-16,25-28; gleam-atomvm/dossier.md:87; P6:139 | F5, P4 (AtomVM target), SC-004, Full-Gleam | open |

---

## C. Architecture-blockers (seam / ISA / IL / codec)

| id | description | severity | evidence | affected_features | status |
|----|-------------|----------|----------|-------------------|--------|
| C-74 | [BLK-CODEC-01] No wire format exists — Section-15 "Instruction Encoding" NOT IMPLEMENTED in any language | high | P5D:65 (bytecode-v216:1350-1352); DEC:44 | c1 seam, F5, Full-Gleam | open |
| C-75 | [BLK-CODEC-02] Opcode operands embed in-process `Object?`/`StructTerm`; codec cannot serialize until de-embedded | high | P5D:65 (opcodes.dart:99,116,133; codegen.dart:652-653); DEC:44-46 | c1 seam, ED-6 | open |
| C-76 | [BLK-CODEC-03] Heap-independent result envelope not built; Bindings leak live `VarRef`s; codegen drops 3 envelope components | med | P5D:23 (§1.3/INV-5) | c1 seam, ED-6 | open |
| C-77 | [BLK-PARITY-01] 029 C# IlCodec explicitly NOT proof for the Dart/Gleam bytecode path | high | DEC:46; P5D:64 | ED-6, F5, Full-Gleam | open |
| C-78 | [BLK-PARITY-02] `glp_il_codec` is C#-only; Gleam codec is a fresh port with no Dart mirror to diff against | med-high | gleam-atomvm/dossier.md:159-161,55 | ED-6, F5/Full-Gleam | open |
| C-79 | [BLK-PARITY-03] Byte-parity REFERENCE for the bytecode seam is conflated with the IL codec (which ED-1 says must never cross the wire); reference exists in NO language | high | P6:99-101,153 vs DEC:17-19,46; P5D:64-65 | ED-6, c1/ED-1 seam | open |
| C-80 | [BLK-ISA-01] v2.16.3 ISA not frozen/versioned; v1/v2 opcode split (opcodes.dart vs opcodes_v2.dart) unresolved before crossing | high | DEC:49; P5D:68; CORPUS:28,39; PB:25-26,67 | ED-2, ED-6, all M1 | open |
| C-81 | [BLK-ISA-02] Two-cell writer-MGU extension (what makes v2.16.3 ≠ FCP) has NO constructed proof | med | PROOFS:14; PB:68-69; DEC:9-11 | ED-2 ISA semantics, M1 | open |
| C-82 | [BLK-ATOM-01] AtomVM binary/bit-syntax decode of the ML codec is UNSPIKED (F1 proved spawn+exec, not on-device decode) | high | P5D:64; DEC:46,69 | ED-6, c1, Full-Gleam | open |
| C-83 | [BLK-ATOM-02] Float decode on AtomVM is the one unverified codec item (raw IEEE bits; `/float` extraction ungrounded) | high | P6:139 (ByteIo.cs:54-56) | ED-6 Gleam codec | open |
| C-84 | [BLK-ATOM-03] Signed/LE-64 bit-syntax forbidden on AtomVM; ConstInt needs sign reinterpretation in pure Gleam | med | P6:105-107,140 (ByteIo.cs) | ED-6 Gleam codec | open |
| C-85 | [BLK-ATOM-04] Bignums unsupported (64-bit ceiling); varint 64-bit-cap guard must be re-implemented in Gleam | med | P6:97,108,140 (ByteIo.cs:33) | ED-6 Gleam codec | open |
| C-86 | [BLK-ATOM-05] 64-bit overflow parity MASKED on plain-BEAM test runtime | med | P6:142; §3.2 | ED-6, M1 parity validation | open |
| C-87 | [BLK-IL-01] IL op-verifiers (phase-order/SRSW/writer-MGU) = the #11 obligation; only 4-op MLIR smoke + single-clause firing, full suite unbuilt | med | P5D:66 (MLIR-GLP:46-48; RESULT.md); DEC:50-51,60-64 | b2 IL, Full-Gleam | open |
| C-88 | [BLK-IL-02] `suspend_reactivate` primitive emits NO opcode (HEAD three-valued + trailing NoMoreClauses) → IL↔bytecode map non-total; un-stress-tested asymmetry | med | DEC:67-69; PROOFS:15 | b2 IL, ED-6 faithfulness | open |
| C-89 | [BLK-SEAM-01] M2 parity ≠ ISA-identity must stay distinct (two seams, different payloads) | med | DEC:50; P5D:17; PB:11 | M2/ED-1 | open |
| C-90 | [BLK-SEAM-02] M2 term-level codec (FrameCodec/Crc32, FR-060/061) has no Gleam implementation | med | P5D:17; PB:121-131 | M2/ED-1, Full-Gleam | open |
| C-91 | [BLK-SEAM-03] M2 wire-framing unsettled (version byte/CRC/fragmentation/dual polarity-byte/in-band sentinel) | med | PB:161,124 | M2 codec, ED-6-adjacent | open |
| C-92 | [BLK-SEAM-04] Multi-hop imported-var identity holds only at serializer layer (relayer-id substitution); breaks end-to-end | med | PB:125,155 (mad_context.dart:180-184; payload_serializer.dart:424-434) | M2/ED-1 codec correctness | open |
| C-93 | [BLK-SCOPE-01] Ratified seam spike-verified ONLY in-process, single-clause, Dart, no codec; AtomVM round-trip is a distinct not-run spike gating Fork-C | high | DEC:69; P5D:60; ANTLR:30,112 | ED-6, c1, Phase-B gating | open |
| C-94 | [BLK-SCOPE-02] Codec byte-parity explicitly OUTSIDE the SPIN distributed-deref proof, and that proof is OPEN → codec has zero formal coverage | med | PROOFS:17 | ED-6, M2 | open |
| C-95 | [BLK-SCOPE-03] FB-M1-40 `forward_to_terminal` Dart parity line UNPINNED (RISK-CITE-1) | med | PB:94,150; P6:61,141 (heap.gleam:251-278) | ED-6 faithfulness, F4/F5 | open |
| C-96 | [BLK-ANTLR-01] ANTLR has no BEAM/Gleam target → bytecode-on-wire is the only engine path; cross-target parser parity is load-bearing (FB-M1-06) | med | ANTLR:36,87; DEC:27-29; PB:60 | ED-4, seam | open |
| C-97 | [BLK-ANTLR-02] No GLP grammar built on EITHER target; cross-target byte-identical-AST parity never shown; 4.13.1 vs 4.13.2 skew | med | ANTLR:126 (Qhxm.Regen.csproj:27; SPIKE-RESULT.md:31),69-74 | ED-4, seam byte-parity | open |
| C-98 | [BLK-ANTLR-03] Spike built `Program` not `Module`/`TypeDef` envelope; hardcoded `(1,0)` positions | low | ANTLR:116,130 (antlr_adapter.dart:31-32; ast.dart:14-19) | ED-4 | open |
| C-99 | [BLK-CORPUS-01] `MSTACK/docs/diana` missing; `qhstate-Yngenios` a stub → seam decisions resting on YngeniOS context provisional | low | CORPUS:101-108 | P7, host-edge seam decisions | open |
| C-100 | [BLK2-CODEC-04] `decode∘encode=id` is intra-runtime self-round-trip; cross-runtime byte-parity needs ENCODER agreement (the 6 cross pairs), which no proof/test covers | med-high | DEC:46; P6:99,153 | ED-6, Dart↔C#↔Gleam, F5 | open |
| C-101 | [BLK2-CODEC-05] No encoder-CANONICALIZATION spec; "byte-parity reference" is a C# implementation, not a normative contract (field order/varint-min/default-omission/var→writer-id map order) | med | P6:99-101; P5D:23 | ED-6, ISA freeze, F5 | open |
| C-102 | [BLK2-PARITY-04] Spike byte-identity is an artifact of REUSING the production analyzer/register table; the codec/seam itself carries no parity guarantee (dissolves under a multi-target front-end) | med-high | ANTLR:21,26 (antlr_adapter.dart:7; SPIKE-RESULT.md:146-148,230-232); PB:60 (ANZ:823-831) | ED-6 byte-parity, F6, F5 | open |
| C-103 | [BLK2-SEAM-05] De-embedding `StructTerm` operands forces the M1 bytecode codec to EMBED a term serializer — collapsing the ratified "two seams, two payloads" separation in practice | med-high | P5D:65; DEC:32,50 | ED-6, ED-1, F5 | open |
| C-104 | [BLK2-SEAM-06] Ratified "identical in-process and over-the-wire" premise is currently FALSE; making it true needs changing the working in-process forward path + envelope, not just adding a codec | med-high | DEC:20-24; P5D:23,65 | ED-6, c1 seam, in-process engine, F5 | open |
| C-105 | [BLK2-CODEC-06] Heap-independent envelope needs DEEP-RESOLVE of partially-bound terms, contradicting shallow-deref (FB-M1-19); embedded still-unbound readers have no heap-independent representation | med | P5D:23; PB:73 (HEAP:331-333) | ED-6 envelope, F6 REPL fold-in, F5 | open |
| C-106 | [BLK2-CODEC-07] Envelope's `var-name→writer-id map` is named heap-INDEPENDENT yet a writer-id IS a heap-local address — internal contradiction; cross-boundary id namespace undesigned | med | P5D:23,18 (bc:24-41) | ED-6 envelope, c1 over-the-wire | open |
| C-107 | [BLK2-ISA-03] Per-instruction `isReader` mode bit is load-bearing with NO codec slot in any codec; 029 encodes IL ops (7 IOpV2), not v2.16.3 ISA opcodes | med | PB:67 (OPS2:24-94); P5D:19 (opcodes_v2.dart:29,47,65); DEC:46 | ED-6, ISA freeze, F5 | open |
| C-108 | [BLK2-ISA-04] Freezing toward the v2 family can RETROACTIVELY invalidate the spike's byte-identity anchor (disassembled vs today's stock CodeGenerator v1 family) | med | DEC:59,49; P5D:68; PB:25-26,67 | ED-6 #3, FR-005 anchor, P5/P8 | open |
| C-109 | [BLK2-ISA-05] Obligation-1 (codec) and Obligation-3 (freeze) are MUTUALLY blocking: ISA can't be "frozen before it crosses" when its normative encoding section doesn't exist — freeze must first AUTHOR Section-15 | med | P5D:65; DEC:44,49 | ED-6 #1+#3, ISA freeze | open |
| C-110 | [BLK2-PARITY-05] Byte-parity is clause-FORM-sensitive; the DOSSIER's worked merge/3 illustration uses a NON-faithful body-construction form emitting different bytecode than the verified head-construction form | med | DEC:70-71,58; P5D:32,38 | ED-6 byte-parity, F5, P5 spike scope | open |
| C-111 | [BLK2-ANTLR-04] Cross-target divergence is two different code-GENERATION toolchains (Antlr4BuildTasks/dotnet vs antlr4-tools/Java-17), not just runtime skew; drift gate guards only C# bytes, none for Dart/cross-target | low-med | ANTLR:56,28,126 | ED-4, seam byte-identical-AST, F6 | open |
| C-112 | [BLK2-CODEC-08] Label table is "derivable" and RE-DERIVED engine-side, not serialized; identical instruction bytes don't guarantee equivalent execution unless Gleam derives labels identically to Dart | med | P5D:18,23,38 (runner.dart:50-64) | ED-6 codec, c1 seam, F5 engine | open |

---

## D. Scope-traps (mis-sizing across the 3 epics)

| id | description | severity | evidence | affected_features | status |
|----|-------------|----------|----------|-------------------|--------|
| C-113 | [A1] ANTLR FR-005 "verified base" is one clause (no guards/multi-clause/nested-struct end-to-end); green stamp gives false full-grammar confidence | high | ANTLR:30,110-112; SPIKE-RESULT.md:226 | T008, #17 | open |
| C-114 | [A2] Infix-operator heads/goals (`Result?:=N`, `=..`, guards) need a left-recursive term/expr rule + precedence the spike never touched ("HIGHEST") | high | ANTLR:114; self.glp:87,113,374 | T008, #27 | open |
| C-115 | [A3] No `::=` rule; spike built `Program` not `Module`; entire TypeDef/TypeExpr envelope unbuilt; `QUESTION` context-overloaded | high | ANTLR:116; antlr_adapter.dart:31-32 | T008, #27 | open |
| C-116 | [A4] Module envelope (`-module`/`-mode` directives, compilationUnit, clause→Procedure regroup) net-new vs spike's Program | med | ANTLR:118; parser.dart:59, ast.dart:250-285 | T008 | open |
| C-117 | [A5] Spike tokenizes 9 operators; production needs ~25 shared-prefix; maximal-munch/`.`-overloading load-bearing, untested at scale — the axis the A-vs-B fork bites | med→high | ANTLR:120; token.dart | T008 | open |
| C-118 | [A6] Dual-role BAR proven only on no-guard clause 1; `;`-disjunction/`otherwise`/an actual-guard clause never run (Shapiro criterion #2) | med | ANTLR:122; self.glp:289,301 | T008 | open |
| C-119 | [A7] Spike uses BailErrorStrategy; production wants located diagnostics; negative corpus must be ACCEPTED then rejected by type/SRSW checker — too-tight grammar wrongly rejects | med | ANTLR:124; antlr_adapter.dart:20 | T008, #8 | open |
| C-120 | [A8] No GLP grammar built on EITHER target; cross-target byte-identical-AST parity never shown; 4.13.1 vs 4.13.2 skew | high | ANTLR:126; Qhxm.Regen.csproj:27; SPIKE-RESULT.md:31 | T008, cross-runtime, #5 | open |
| C-121 | [A9] Packaging symmetric gap: Option A built-Dart/unbuilt-C#, Option B built-C#/unbuilt-Dart; recommended B needs the split→Dart gap closed first | med | ANTLR:69-74,80-82,94,98-104 | T008 | open |
| C-122 | [A10] Clean grammar silently accepts `=..` uniform + structs-in-lists — language-surface changes must be explicit owner decisions | med | ANTLR:128; CLAUDE.md known-issues | T008, language authority | open |
| C-123 | [A11] Spike hardcodes `(1,0)`; production must thread real line/column corpus-wide | low | ANTLR:130; ast.dart:14-19 | T008 | open |
| C-124 | [A12] ANTLR has no BEAM target → full-grammar front-end can never run on AtomVM; "single combined instance" is structurally a split (parse off-device, engine on-device) | med | ANTLR:36,45,87; DEC:28,66 | M1 packaging, #6, #27, P7 | open |
| C-125 | [B1] Distributed-deref/unify M2 faithfulness OPEN (only minimal handshake passes SPIN) | high | PROOFS:17; PB:147 | #36, M2 | open |
| C-126 | [B2] GAP-G6: no quiescence oracle for a linked N-instance run → "M2 linked parity" currently untestable | high | PB:170 | #36, #5, M2 done-def | open |
| C-127 | [B3] `erlang:monitor` on AtomVM v0.6.6 assumed by all 3 approaches but unverified; M2-0 gating spike not yet run | high | CA:45,137; CRIT:62 | #36 fault-as-data, M2 | open |
| C-128 | [B4] No Section-15 bytecode binary codec; de-embed operands + Dart↔C#↔Gleam byte-parity + AtomVM decode spike owed; don't cite 029 | high | DEC:44-46; P6:99-101 | #36, ED-6, Full-Gleam | open |
| C-129 | [B5] AtomVM `/float` bit-syntax extraction NOT grounded; IEEE-bit double round-trip is the one unverified codec item | high | P6:139; §3.4 | #36, ED-6 | open |
| C-130 | [B6] AtomVM LE-64 unsigned-only vs signed-LE long; Gleam decoder must reinterpret two's-complement for ConstInt | med | P6:140; §3.4 | #36 codec | open |
| C-131 | [B7] `epmd`/`disterl` unsupported → M2 must ride the explicit on-wire seam, not disterl; constrains every transport choice | med | P6:112 | #36, M2 transport | open |
| C-132 | [B8] FB-M2-08 creator-id holds at serializer layer only; live lookup returns relayer id → multi-hop unproven | med | PB:125,155; mad_context.dart:180-184 | #36, M2 | open |
| C-133 | [B9] FB-M2-R2 epoch/fencing token is a recommendation, not in code; M2 enumerates features with no reference impl to be faithful to | med | PB:160; CA:31,78 | #36, M2 | open |
| C-134 | [B10] FB-M2-R3 wire-framing unsettled; needs an M2 codec framing spec before parity is defined | med | PB:161 | #15, #36, #5 | open |
| C-135 | [B11] Approach-3 DROPPED #15/frame-envelope parity + underspecified M2 frame envelope; critique: re-instate as #36 sub-req "else #5 fails late" | high | CRIT:30,35,60; CA:28,102 | #15, #36, #5 | open |
| C-136 | [B12] FB-M2-R4: no enumerated-chain/local N-instance correctness theorem; rests on unproven unary-hop assumption | med | PB:162 | #36, M2 | open |
| C-137 | [B13] M2 governing Thm 5.7 lemmas C.41/C.45 truncated in arXiv HTML; M2 bar foundation ungrounded | med | PB:143 | M2 bar, #36, #5 | open |
| C-138 | [B14] FB-M2-04 rests on a contested `mad_context` snapshot; re-verify gate-recursion + imported-reader reactivation | med | PB:151 | #36 | open |
| C-139 | [B15] FB-M2-20 monotonicity EMERGENT (no retract + dedup no-op), epoch/fence future; relying on emergent not designed guarantees | low-med | PB:130,157 | #36 | open |
| C-140 | [B16] GLP REPL cannot run on AtomVM; "combined instance" deliverable is split across runtimes | med | P6:112 | #6, M1 packaging, P7 | open |
| C-141 | [C1] Ratified port source is Dart but C# is the #5 oracle and "Dart↔C# aren't at parity" — the reference contract is itself contested | high | CRIT:19,59; CA:3 | #5, #2 | open |
| C-142 | [C2] glp_runtime/ parity SoT must stay byte-converged with sibling upstream; drift moves every M1/M2 criterion's baseline | med | CORPUS:47-49 | all parity criteria, P4 | open |
| C-143 | [C3] Integer-overflow false-green baked into test substrate (BEAM bignum vs 64-bit) | high | P6:142; §3.2 | #8, cross-runtime | open |
| C-144 | [C4] Cross-runtime codec byte-parity asserted but unbuilt for Dart/Gleam; 029 may NOT be cited; each runtime owes its own proof + AtomVM spike | high | DEC:46; P6:99-101 | #36, ED-6, #5 | open |
| C-145 | [C5] Faithfulness judged by outcome-equivalence not transition-by-transition; over-asserting stepwise is wrong, outcome-only may mask scheduling divergence | med | PB:9; P6:21 | #8, #5 | open |
| C-146 | [C6] Writer-MGU binds only writers has NO constructed proof (OPEN) | high | PROOFS:14; PB:146 | #26, all M1 | open |
| C-147 | [C7] RISK-CITE-1: Dart `forward_to_terminal` line unpinned; Gleam impl self-consistent but parity unprovable | med | PB:150; P6:141 | #26, #8 | open |
| C-148 | [C8] Literal "dedupe by goal_id" (FB-M1-35) BREAKS FB-M1-38; correct key (goal_id, suspension_generation); can't build a faithful runtime from the bar literally | high | P6:47,143; PB:89 | #26 (F5) | open |
| C-149 | [D1] P1 (the only concrete disposition list) FAILED its own bar ("read for why it failed, not conclusions"); P8/hurried reader could absorb discredited verdicts | high | IDX:22 | P1b, #17/#4/#23/#16/#28, P8 | open |
| C-150 | [D2] Dispositions predate the P4 bar + P1b; IDX mandates P1b re-judge on ED-1…ED-6 + the bar; any on-record disposition is provisional | high | IDX:24 | P1b, P8 | open |
| C-151 | [D3] P1 DROPS #17 antlr4-shared-grammar-spike yet ANTLR is the owner-ratified ED-4 front-end (T008, FR-005) — direct internal contradiction | high | CA:109; DEC:28; ANTLR:36 | #17, T008, P1b | open |
| C-152 | [D4] P1 DROPS #4 il-codec-spike + #23 compiled-il-on-wire yet DEC creates a Section-15 codec OBLIGATION on the M2 critical path | high | CA:101,108; DEC:44-46 | #4, #23, #36, ED-6, P1b | open |
| C-153 | [D5] M1 ruled IN-PROCESS (supersedes #13); engine-separation is one of the 3 compressed epics — deferring its central deliverable implicitly guts it | med-high | CA:11,103; IDX | engine-sep epic, #13, P8 | open |
| C-154 | [D6] `supersede-by-beam` ("never applies to GLP semantics") applied to #13/#21/#30/#33/#18 — the operational-vs-semantics boundary is where mis-sizing hides | med | CA:103,106,107,112,89 | #13/#18/#20/#21/#30/#33, engine-sep | open |
| C-155 | [D7] Approach-3 hard-dropped persistence (#20/#18); critique prefers realign-defer ("matters for long-running linked nodes") | med | CRIT:33,61; CA:104,106 | #20, #18, M2 | open |
| C-156 | [D8] Fold-ins push real scope into host features uncounted (#6 absorbs envelope/deep-resolve/output-capture; #36 absorbs frame-envelope/serve/2) | med | CA:99-102,116,118,132,141 | #6, #36, P8 scoring | open |
| C-157 | [D9] #28 cpp + #16 llvm-feasibility dropped as "alternative substrate" while DEC defers real-MLIR "revisit if LLVM/C++ greenlit" — latent re-scoping | low | CA:111,113; DEC:36-38 | #28, #16 | open |
| C-158 | [E1] "SHORTEST VERIFIED" tension: 45 M1 + 21 M2 criteria, 2 open proofs, 8 corpus GAPs, ≥3 unverified spikes pull against "shortest" | med | PB:101,133,165-172; PROOFS:24 | P8, both output epics | open |
| C-159 | [E2] GAP-G1…G8 are load-bearing invariants ABSENT from both lenses → unscoped parity work the M1/M2 done-defs omit | high | PB:165-172 | #8, #26, #36, P8 | open |
| C-160 | [E3] FORK-1 (GAP-G5 vs FB-M1-23) is an unresolved owner fork that must be decided + corpus'd before either runtime is "faithful" | med | PB:176-183; P6 §4 | #26, #8, both runtimes | open |
| C-161 | [E4] F4 deferred reader-side routing + imported-reader branch to F9 — the M2 linked-parity heap seam, "must be named not assumed free" | med | P6:144 | #36 (F9), M2 | open |
| C-162 | [E5] P7 grounding partly missing (`MSTACK/docs/diana` absent; `qhstate-Yngenios` a stub) → integration design provisional | med | CORPUS:101-108 | P7 (T010) | open |
| C-163 | [E6] ED-1…ED-6 stamped "spike-verified" but spike is single-clause merge/3, no guards/multi-clause/codec/Gleam/AtomVM — narrow evidence base vs scope | high | DEC:67-69; SPIKE-RESULT.md:226; ANTLR:30 | all EDs, P8 | open |
| C-164 | [E7] Marathon (#030) contributes no port features (pure infra); framing as "compressing 3 epics" overstates feature consolidation | low | CA:120 | P8 framing, roadmap | accepted |
| C-165 | [E8] Full-Gleam epic risks being scored smaller/faster than evidence supports (6 features w/ folded contracts, 2 heavy marathons, 2 open proofs, ≥3 unverified spikes) | med | IDX:30; CA §5 | P8, Full-Gleam epic | open |
| C-166 | [E9] Entire Gleam/AtomVM toolchain runs WSL-Ubuntu-only; AtomVM viability "host" only — narrows where any "verified" result reproduces | low | CA:41; P6 §3.1 | all Gleam features, reproducibility | open |
| C-167 | [ST2-01] ANTLR Option B's separability/reach advantage is self-warned INAPPLICABLE to GLP's Module-vs-REPL two-entry case, yet B is recommended | med | ANTLR:66,44,98-102 | T008, #27, FR-005/SC-005 | open |
| C-168 | [ST2-02] Reusing qhstate drift-gate is false parity coverage (guards C# committed bytes, not cross-target AST); adopting B silently adds an uncounted GLP regen/drift-gate tooling build | med | ANTLR:56,60,127; PB:60 | T008, cross-target parity, P8 | open |
| C-169 | [ST2-03] Chosen-approach "Step 0 roadmap hygiene (non-gating)" prescribes live-roadmap MUTATIONS that violate read-only-until-T014 | med | CA:126; IDX:49-54; CORPUS:12-15 | P8, T014/T015, FR-010/011 | open |
| C-170 | [ST2-04] SUPERSEDED P1's chosen-approach declares itself "Single source of truth for all downstream per-feature analysts" → un-regrounded grafts steer P6/P7/ANTLR before P1b corrects | med-high | CA:3; IDX:22,24,40-43 | T008-T010, P1b, P8 | open |
| C-171 | [ST2-05] SC-001 (byte-identical split) satisfiable on in-process/loopback transport, deferring real-socket/MTU/CRC/`gen_tcp` parity — false-green on the M2 transport seam | med-high | CA:32,79,140; P6:112 | #36 (F9), SC-001, M2, P4 | open |
| C-172 | [ST2-06] `serve/2`+`mwm` (#29) is GLP control-program SOURCE the N-client M2 depends on, absent from the M1 PC-1…PC-15 corpus gate | med | CA:33,118,141,69,133 | #36, #5, M2, #8 | open |
| C-173 | [ST2-07] "M1 done = 100% green" rests on a 15-scenario kernel micro-corpus (034 11-seed), not the repo's real suite (run_all_tests 384 + book + plays + bonds) | high | CA:69,117,133; CORPUS:56; CLAUDE.md test inventory | #8 (F8), M1 done-def, P8, SC-004 | open |
| C-174 | [ST2-08] #27 plans "reuse Dart/C# golden bytecode for parity" but no cross-target GLP bytecode oracle has ever been shown; Dart↔C# GLP bytecode parity unestablished | med | CA:131; ANTLR:126; CRIT:19 | #27 (F6), P4 M1, P8 | open |
| C-175 | [ST2-09] Per-link FIFO (Lemma 5.7 precondition) claimed "free from BEAM ordering" but holds only on loopback/Subjects; real-TCP must reconstruct FIFO via the unbuilt reliability sublayer | med-high | CA:26,31,32 | #36 (F9), M2 correctness, P4 M2 | open |
| C-176 | [ST2-10] "Faithful = observable, NOT internal layout" used to treat immutable-heap divergence as free, yet it already forced 2 unbudgeted F5 obligations (forward-to-terminal, goal_id dedup) → systematically under-sizes F5 | med | CA:51,65,66 | #26 (F5), P4 M1, P8 scoring | open |
| C-177 | [ST2-11] Six-disposition rubric is "first match wins" → operationally-superseded features strand their semantic obligations (e.g. #33 scheduling → supersede-by-beam, leaving GAP-G3 fairness ownerless) | med | CA:85,89,112; PB:167 | #33/#18/#21, F5, P1b, P8 | open |

---

## E. Process / methodology risks

| id | description | severity | evidence | affected_features | status |
|----|-------------|----------|----------|-------------------|--------|
| C-178 | [PR-01] `diana` grounding (`MSTACK/docs/diana`) named in US4/SC-006 does not exist; verified no `MSTACK/` repo — a named acceptance source unsatisfiable as written | high | SPEC:99; CORPUS:101-105; verified `ls /d/bstdev/research/MSTACK` → absent | P7 (T010), FR-007, SC-006, US4 | open |
| C-179 | [PR-02] CORPUS-INDEX mischaracterizes `qhstate-Yngenios` as a "stub"; direct inspection shows a substantial repo (Csharp/docs/codeconv/specs, mod 06-27) → P7 grounding plan built on stale corpus characterization | high | CORPUS:95,106-108 vs verified `ls /d/bstdev/research/qhstate-Yngenios/` | P7 (T010), FR-007, SC-006 | open |
| C-180 | [PR-03] `diana` material exists only as scattered NATO-DIANA coordination notes under `mstack-coop/` (9 hits, archive/inbox/task), not a `docs/diana` spec | med | verified `find mstack-coop -iname '*diana*'`; CORPUS:104-105 | P7, FR-007 | open |
| C-181 | [PR-04] The one pipeline with known-broken grounding (P7) is still entirely PENDING while P8 fans in T010 → gap unmeasured at synthesis-input assembly | med | IDX:27,30,43 | P7, P8 (T007), FR-008 | open |
| C-182 | [PR-05] Documented marathon-row staleness pattern (025/030 STALE; 027 mis-driven) threatens SC-008 restart-safety; resume silently degrades to reading on-disk artifacts | high | MEMORY (025/030/027); SPEC:230-231,184-185 | whole program (FR-012, SC-008) | open |
| C-183 | [PR-06] No committed pipeline script; durable progress = the marathon run alone; a crash mid-Workflow (pre-extraction) loses un-extracted output with no re-derivable script | med-high | IDX:8-10 | FR-012, SC-008, P2–P8 | open |
| C-184 | [PR-07] Divergent locators for P1 work: MEMORY says workflow `wf_fb9f56eb-0ca` "running as head-start" while INDEX marks P1 SUPERSEDED → live workflow attached to abandoned premise | med | MEMORY (036 entry); IDX:22 | P1b (T006), P8 | open |
| C-185 | [PR-08] Multi-participant marathon foundation + repo history of parallel-session divergence (030 reconcile; 035 `git add -A` sweep) → concurrent session could mutate shared state or sweep 036 artifacts | low-med | bk-marathon skill desc; MEMORY (030/035) | FR-012, 036 artifacts, durability | open |
| C-186 | [PR-09] Single discharge gate + read-only window vs a live moving roadmap (Gleam epic keeps shipping) → SC-001's "100% of not-completed features" can be stale by gate time | high | IDX:49-54; pipeline-contract:31-36; SPEC:176-181,216-217; MEMORY (F4 shipped) | SC-001, FR-001/009, P1b, P8 | open |
| C-187 | [PR-10] Owner-gated forks (FORK-1, ANTLR A/B, FORK-A/B) deferred to owner block a complete scored topological synthesis (FR-008) if open at synthesis | med-high | PB:176-185; ANTLR:96-106; P6:116-133; SPEC:170-173 | P8 (T007), FR-008, SC-002 | open |
| C-188 | [PR-11] P8 fan-in concentrates every weak input into one gated artifact; only as sound as its weakest feeder; owner told to decide "from synthesis artifacts alone" | med | IDX:30,43; SPEC:232-233 | P8, FR-008, output epics | open |
| C-189 | [PR-12] Two load-bearing invariants OPEN (writer-MGU FB-M1-14/15; distributed-deref FB-M2-01/04) — M1/M2 "faithful" rests on un-discharged obligations | high | PROOFS:14,17,24; PB:146-147 | F5, F9/F10, FR-004, SC-004, P8 | open |
| C-190 | [PR-13] The 3 "proved" obligations prove the METHOD on a toy, not the property on full GLP (SRSW one toy clause; three-valued/suspension merge/3 cl.1) | med-high | PROOFS:8,13,16 | FR-004, SC-004, all M1, P8 | open |
| C-191 | [PR-14] RISK-CITE-1: FB-M1-40 parity to Dart not provable (unpinned source line); a "verified" claim downstream would be ungrounded | med | PB:94,150; P6:61,141 | F5, FB-M1-40, P4 | open |
| C-192 | [PR-15] M2 criteria rest on a CONTESTED/possibly-stale `mad_context` snapshot; re-verification still owed → may encode superseded behavior | med-high | PB:149-151 | M2 features, P4 M2, P8 | open |
| C-193 | [PR-16] 11 M2 criteria REFUTE-as-cited then re-grounded the same day (2026-06-29) by swapping draft cites for live multiagent/*.dart, with residual caveats → young/fragile M2 bar | med | PB:107,133,153-157 | M2 bar, P8 | open |
| C-194 | [PR-17] M2 yardstick Thm 5.7 lemmas C.41/C.45 truncated in the only available source; bar judges M2 per-invariant meanwhile | med-high | PB:143; DEC:50 | M2 epic, P4 M2, P8 | open |
| C-195 | [PR-18] GAP-G1…G8 absent from BOTH lenses; G6 makes a linked N-instance run's overall status undefined → "M2 linked parity" not testable | high | PB:164-172 | M2 epic, FR-003, SC-003, P8, all M1 | open |
| C-196 | [PR-19] FORK-1 is a genuine semantic conflict (book graceful core:166 vs live loud HEAP:265-266); F4 (shipped 06-25) carries a possibly-unfaithful default | med-high | PB:169,176-185; P6:131-133 | F4 (shipped), F5, M1 bar, FR-003 | open |
| C-197 | [PR-20] Bar text itself carries a known-defective instruction ("dedupe by goal_id", PB:89); a verbatim F5 impl would be unfaithful (breaks FB-M1-38) | med-high | P6:47,143; PB:89 | F5, FB-M1-35/38, P4 bar text, P8 | open |
| C-198 | [PR-21] "vs Dart/C#" oracle assumes a Dart↔C# agreement that doesn't exist for M2 (link 39 vs 40 files separate async impl; il_codec C#-only); critique: "reference contract is itself contested" | high | gleam-atomvm/dossier.md:46,57-60; CRIT:19,59 | M2 bar, F9/F10, FR-003, P4 | open |
| C-199 | [PR-22] Parity SoT (in-repo Dart) has un-propagated runner deref-conflation drift from its named upstream authority → sibling re-sync could shift the M1 baseline | med | MEMORY (runner fix outstanding); CORPUS:47-49 | M1 bar, P4, F5 | open |
| C-200 | [PR-23] Owner-ratified ED-1…ED-6 architecture verified on a SINGLE clause; 9 enumerated scaling risks net-new; P1b/P8 lean on "the verified architecture" as a fixed input that may not survive full GLP | med-high | ANTLR:30,110-130; DEC:68 | ED-1…ED-6, P1b, P8, ANTLR/IL | open |
| C-201 | [PR-24] ANTLR FR-005 anchor asymmetric; the RECOMMENDED option (B/split) is unbuilt on the anchor target (Dart) → risks sending the front-end down an unverified path | med | ANTLR:69-74,80-82,94-106 | ANTLR decision, Full-Gleam front-end, FR-005, SC-005 | open |
| C-202 | [PR-25] ED-6 float-decode on AtomVM UNVERIFIED and gates the M2 byte-parity codec; if `/float` fails the codec is blocked, invalidating the M2 byte-parity decision | med-high | P6:139; DEC:44-46 | ED-6, M2 codec, Full-Gleam | open |
| C-203 | [PR-26] M1 parity "green" can be MASKED by the test runtime (BEAM bignum vs 64-bit) → corpus-green is not a valid AtomVM-faithfulness signal for 64-bit edges | med | P6:142 | M1 corpus gate, AtomVM features, SC-004 | open |
| C-204 | [PR-27] AtomVM-substrate rulings rest on docs/inference not observed-failure spikes (ETS-OUT inference; `erlang:monitor` assumed) → fault-model/binding-store forks on un-spiked premises | med | P6:128; CRIT:11,62 | P6 forks, M2 fault model, Full-Gleam | open |
| C-205 | [PR-28] The program's core methodology already FAILED once (original P1 "fastest-path" rubric); contract now bans it but the failure was on the load-bearing realignment | med-high | IDX:22; pipeline-contract:18-19 | P1b (T006), FR-001, all dispositions | open |
| C-206 | [PR-29] Each pipeline self-certifies contract compliance; no independent gate before P8/owner; a mis-grounded pipeline (as P1 showed) caught only by a later pass or the owner | med | pipeline-contract:28-29; IDX:22; SPEC:232-233 | all pipelines, P8 | open |
| C-207 | [PR-30] P2/P3 exhaustiveness ("loop-until-dry, consecutive empty rounds") is self-certified with no external oracle; a missed concern goes undetected | low-med | SPEC:117-120,152-153 | P2/P3, FR-002, dispositions, P8 | open |
| C-208 | [PR-31] 036's own trackers ALREADY STALE vs disk: INDEX marks P4/P1b PENDING but their artifacts exist + post-date INDEX (verified mtimes); tasks.md all `[ ]` though T001-T006 outputs exist; entire tree untracked `??` → resume re-runs completed pipelines (SC-008 breach) | high | IDX:23-24 + verified mtimes; tasks.md:15-57; quickstart.md:9-15; SPEC:230-231; `git status` untracked | FR-012, SC-008, restart-safety, P8 | open |
| C-209 | [PR-32] ≥3 features carry BACKWARD roadmap drift (#4 shipped→still `specified`; #2 →`reviewed`; #030 →`specified`) that P1b had to catch by hand → FR-001/SC-001 denominator wrong unless every drift spotted | med-high | DISP:26,39,40,42; SPEC:149,216-217 | FR-001/009, SC-001, P1b, P8 | open |
| C-210 | [PR-33] P1b's emitted disposition records violate the declared `data-model.md` schema (out-of-enum classifications ADD-NEW/SHIPPED/CLOSED/KEEP-and-CLOSE/FOLD-into-#6/REALIGN-DEFER; out-of-enum milestones M1/M2; crit "TRUE (seam)") → P8 consumes vocabulary it doesn't define | med | DM:7-12; DISP:23,26,28-40 | P8 (T007), SC-002, FR-008, data-model | open |
| C-211 | [PR-34] Realignment CREATES net-new scope (invents feature M2-0, makes it a hard dependency of #36/#30/#21) inside a read-only dispositioning program scoped to the existing not-completed set | med | DISP:23,42,67; DM:7-8; SPEC:149,216-217 | P8 (T007), SC-002, FR-008, T014, #36 | open |

---

## F. Critic additions — missing risk classes

| id | description | severity | evidence | affected_features | status |
|----|-------------|----------|----------|-------------------|--------|
| C-212 | [MISS-01] SECURITY / trust-boundary of the M2 wire seam is completely unscanned: zero security content in the corpus, no NFR/security FR; M2 deserializes executable bytecode + terms from a peer with no authn/authz/signing/integrity, and FB-M2-16 FORBIDS wire-side SRSW re-check — a textbook untrusted-deserialization/RCE boundary with no threat model | high | SPEC:149-232 (no security FR); PB:11,118,161; DEC:20-24; verified grep (no security terms) | F9, M2, ED-1 seam, Full-Gleam, owner gate | open |
| C-213 | [MISS-02] No PERFORMANCE bar exists; "faithful = observable, not layout" lets an arbitrarily-slower Gleam engine "pass"; immutable threaded store on a single copying-GC actor can be super-linear, and the only mitigation (B3 process-cells) is explicitly punted as "performance, out of parity scope" | med-high | PB:7-9; SPEC:216-232 (no perf SC); P6:97,122,129 | F5, M1/M2 viability, P8, owner | open |
| C-214 | [MISS-03] AtomVM 0.6.6 is a pinned PRE-1.0 supply-chain/longevity dependency (reachable only via one static-mbedtls asset; engine-critical primitives unverified on exactly that point release); no concern treats release-cadence/breaking-change/abandonment/pin-policy risk | med | toolchain-inventory:72-79; gleam-atomvm/dossier.md:84-89; AL-4/AL2-1/2/3 | all AtomVM features, F5, reproducibility, Full-Gleam, owner | open |
| C-215 | [MISS-04] No cross-runtime DIFFERENTIAL-TEST HARNESS is scoped as a build obligation: the proof harness is Lean/SPIN/MLIR + one runner oracle — nothing runs the same program on Dart+C#+Gleam and diffs; #5/#8 assume it; bytecode-golden variant separately known-absent → P8 topo order incomplete | med | SPEC:222; PROOF-HARNESS.md:24-26; ST2-08 | #5, #8, P8 scoring/ordering, P4 | open |
| C-216 | [MISS-05] DEBUGGABILITY/observability of the AtomVM engine and any distributed M2 run is unaddressed beyond "no REPL"; no tracing/structured-logging/post-mortem on-device, no shared observability across instances; compounds the BEAM-masks-AtomVM hazard | med | P6:112 (no REPL/disterl); AL-3/PR-26 | F5, F9, P4 test/diagnosis loop, M2 operability, Full-Gleam | open |
| C-217 | [MISS-06] Owner GATE is a single high-cognitive-load decision over an enormous fork-stacked synthesis (45+21 criteria, 2 open proofs, ~24 dispositions incl. M2-0, multiple stacked owner-forks, 218+ concerns) with no mechanism to split/sequence the independent fork decisions | low-med | SPEC:232-233; PB:176-185; ANTLR:96-106; P6:116-133; DISP | T014 owner gate, SC-009, P8 packaging | open |
| C-218 | [MISS-07] Post-gate migration (T015/FR-011) has no reversibility/rollback path if the synthesis is later invalidated (2 open proofs + ≥3 unverified spikes); no FR/SC defines un-migration; compounds day-one tracker staleness (C-208) | low-med | SPEC:176-181,228,179-181 | T015, FR-011, live roadmap integrity, owner | open |

---

## Cross-angle duplicate clusters (root issues recurring across ≥3 lenses, retained for per-lens evidence)

- **Writer-MGU proof OPEN** → C-01 (faithfulness) · C-81 (architecture, two-cell) · C-146 (scope) · C-189 (process).
- **Distributed-deref M2 proof OPEN** → C-02 · C-94 (codec carve-out) · C-125 (scope) · C-189 (process).
- **ED-6 float-decode on AtomVM UNVERIFIED** → C-24 · C-53 · C-83 · C-129 · C-202.
- **Section-15 bytecode codec does NOT exist / 029 not a proof** → C-27 · C-74/C-77 · C-128 · C-144.
- **goal_id-dedup defective bar text (breaks FB-M1-38)** → C-31 · C-148 · C-197.
- **BEAM-bignum masks 64-bit overflow (false-green)** → C-26 · C-52/C-86 · C-143 · C-203.
- **GAP-G6 distributed quiescence oracle → M2 untestable** → C-16 · C-126 · C-195.
- **FORK-1 circular-term discriminator (owner-gated)** → C-15/C-19/C-41 · C-160 · C-196.
- **Seam spike-verified single-clause-Dart-only over-generalized** → C-93 · C-163 · C-200.
- **`MSTACK/docs/diana` missing + `qhstate-Yngenios` characterization** → C-37/C-38 · C-99 · C-162 · C-178/C-179.
- **Dart↔C# reference oracle contested (not at parity)** → C-78 · C-141 · C-198.

---

## Loop-until-dry record

- **Round 1 count:** 167 concerns (faithfulness 39 · AtomVM 18 · architecture 26 · scope 54 · process 30).
- **Round 2 NEW count:** 44 concerns (faithfulness 10 · AtomVM 6 · architecture 13 · scope 11 · process 4). [The architecture-angle round-2 summary line self-counted "12"; the angle text actually emits 13 distinct BLK2-* IDs — its MED breakdown omitted BLK2-PARITY-05 — so 13 are registered.]
- **Critic additions:** 7 genuinely-new risk classes (MISS-01..07): security/trust-boundary, performance bar, AtomVM pre-1.0 supply-chain, cross-runtime differential-test harness, debuggability/observability, owner-gate decision throughput, post-gate migration rollback.
- **Registered total:** 167 + 44 + 7 = **218**.
- **Saturation:** **NOT reached.** Round 2 was high-yield (44 new, several MED-HIGH structural blockers), and the first dedicated breadth scan surfaced ENTIRE zero-coverage risk classes (security, performance, supply-chain longevity, differential-test infra, observability). The critic recommends one more round scoped to those classes before declaring the register dry. Closest-to-dry angles: **AtomVM-limits** and **process-risks** (round-2 added only 6 and 4, mostly concrete manifestations of round-1 themes) — plausibly one round from saturation. **Faithfulness, architecture, scope-traps** are still actively producing distinct edge cases. No consecutive empty rounds have been recorded; per FR-002/US5 the register is NOT yet certified exhaustive.

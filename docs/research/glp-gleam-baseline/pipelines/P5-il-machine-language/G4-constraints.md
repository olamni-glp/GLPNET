# G4 — Faithfulness, Separation, and Gleam/AtomVM Constraints on the Seam

## (1) The M1 faithfulness bar the executed machine language must satisfy

M1 (single-instance faithfulness) is already pinned by the shipped F4 kernel and its spec, so whatever the back-end executes must reproduce these **observable** behaviours — not an internal layout:

- **Three-valued writer-MGU**, never two-valued: every term×term step yields exactly one of `{success, suspend, fail}` (`specs/034-…/spec.md` FR-007, line 105; impl `glp_gleam/src/glp/runtime/unify.gleam:19-23,40`). A needed unbound **reader** must **suspend, not fail** — the spec calls this "the most common GLP correctness error" (spec line 89; `unify.gleam:93-95`).
- **Writer-only binding**: binds writers only, never readers, never writer-to-writer; WxW is reported **loudly** as a structural error kept distinct from `Fail` (FR-004/FR-007, lines 102/105; `heap.gleam:59-64,152,236`, `unify.gleam:89`).
- **Role from cell tag, never address arithmetic** ("no `reader == writer + 1`", FR-002 line 100; `heap.gleam:40-47,99-112`).
- **Deref with path-compression** that is read-only-safe, and the FCP **self-bind recognizer**: a writer bound onto its own paired reader derefs to `Unbound`, *not* `Cycle` — to match Dart `heap_fcp.dart:312-323` (`heap.gleam:165-175`; this exact parity bug was caught at review, per MEMORY 034).
- **Suspension storage + activation-list production** (no scheduler): bind-to-value emits armed activations; bind-to-variable **forwards** suspensions to the *terminal* unbound writer (FR-008 line 106; `heap.gleam:251-278` — dropping them on an already-`WriterBound` target was the other live review bug). No occurs-check, recorded as deliberate non-behaviour (spec line 90).

Crucially, **parity is observable-outcomes-only** — dereferenced result, three-valued verdict, activation set — and **explicitly NOT internal heap representation** (cells/tags/layout), which "legitimately differs once the heap is re-expressed for Gleam" (spec Clarification 2026-06-25, line 27; FR-009/SC-005, lines 107/131). This is the load-bearing fact for the seam: **faithfulness is defined on what crosses the boundary, not on how the heap is built** — which is exactly what lets an immutable threaded store (`heap.gleam:69-71`) be faithful to a mutable WAM heap.

## (2) What "clean front/back separation" requires of the seam

The reference C# split (design-dossier §1) shows the shape: the front-end owns console I/O, the read-dispatch loop, colon-commands, and **all result display**, but **"no parsing/compilation/execution/scheduling/suspension"** (§1.1, line 66) — a 38-line shim plus a display-only REPL. The engine's entire public contract is `GlpEngine` + `ExecutionResult` (three fields: `Status`, `Bindings`, `Error`) + `ExecutionStatus` (§1.2, lines 70-78).

The decisive separation requirement is that **the same contract must hold in-process and over-the-wire**. Today it does *not*: `Bindings` values are **live `VarRef`s into engine-owned heap**, re-dereferenced by the front-end at display time — "the seam's biggest leak" (§1.3, line 84). A clean seam demands the engine **resolve/serialize results server-side** into a **self-contained, heap-independent** envelope (INV-5, line 416), so an in-process call and a framed wire message carry *identical* payloads. Three computed-but-dropped components must be promoted onto that envelope: var-name→writer-id map, suspended-goal detail, and captured output (§1.3 table, lines 88-92).

The natural cut is the **compiler→runner boundary** — `BytecodeProgram` is the sole artifact crossing into execution (§1.4, line 96). This is where the owner's fork lives (§9.1/§10.1) — **present, not self-decided**:

- **Opt 1 — source-on-wire, compiler engine-side.** Wire carries source text; only a result codec is net-new; no compiler refactor (line 319). Consequence: the *seam is not the machine language* — the back-end still parses/compiles, so a thin heterogeneous front-end and an ANTLR front-end are **not** enabled.
- **Opt 2 — compiled-ML-on-wire, compiler in front-end.** Enables **thin clients + the single ANTLR grammar** (line 320). Consequence: the machine language itself becomes the seam; larger refactor (lexer/parser/typechecker/compiler relocate). This is the only option consistent with the owner's "thin/heterogeneous front-end + ANTLR-defined grammar" goal in Q4.

## (3) AtomVM/Gleam constraints on representing + executing a machine language

The F1 dossier localizes the boundary precisely and favourably:

- **A WAM-style bytecode interpreter "is plain sequential BEAM code (fine for AtomVM); only the spawn primitive needs the raw form"** (dossier line 134). So *executing* the v2.16.3 ISA as a Gleam interpreter is viable on both BEAM and AtomVM.
- **No `gleam_otp` / no `proc_lib`** on AtomVM; spawn process-cells via raw `erlang:spawn` + `gleam_erlang` Subjects (dossier lines 87, 126, 134; spec FR-010 forbids the `gleam_otp` dep, line 108). The hot path and any process-cell heap must avoid `gen_*`.
- The heap mechanism is a plan-time choice (immutable threaded store **or** process-cells, both F1-smoke-proven; dossier §4.1). The shipped kernel chose the **immutable threaded store** (`heap.gleam:1-9,69-71`), which is pure sequential code — the most AtomVM-portable option.

**Verification gap (flagged, not asserted):** representing the ML *as a binary* on the wire is the idiomatic BEAM form, and feature 029's il-codec proved `BytecodeProgram`↔bytes round-trips (dossier line 160). But **AtomVM binary/bit-syntax support for the codec was not spiked in F1** — the dossier evidences spawn and sequential execution, not binary decoding on AtomVM. If Opt 2 (ML-on-wire) is chosen with AtomVM as a real target, the ML byte-codec on AtomVM needs its own spike before reliance.

## (4) What M2 (linked parity vs Dart/C#) needs from the seam

M2 is where the seam's *byte-level* definition matters. The dossier's cross-runtime caveat (§2.5, lines 136-138) is explicit: `FrameCodec`/`Crc32` are **byte-identical Dart↔C# (FR-060/061)** and the Dart `ExecutionResult` is structurally identical to C#'s. Therefore the new **ML codec + result envelope must meet the same byte-parity standard** — and the v1/v2 opcode split complicates it (risk 7, line 406).

This yields the cleanest G4 conclusion: **M1 and M2 demand parity at *different* layers, and the seam is exactly the layer where they meet.** M1 = observable outcomes, heap-internal layout free to differ across Dart/C#/Gleam (spec line 27). M2 = **byte-identical wire** for the ML and the envelope. They compose without conflict **only if the seam is the machine language + result envelope, not the heap** — heap re-expression stays a private back-end concern (satisfying Gleam immutability) while the cross-runtime contract is the byte format three runtimes agree on. Whether that byte format crosses *as compiled ML* (Opt 2) or merely as *source + result envelope* (Opt 1) is the owner's open fork (§10.1); Opt 1 makes M2 a result-envelope-parity problem only, Opt 2 additionally makes the ML codec a cross-runtime byte-parity obligation.
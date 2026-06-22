# Decision Dossier — Gleam Port: Source & Toolchain / AtomVM Feasibility Spike

**Feature**: 031-gleam-port-spike (epic `gleam-atomvm`, F1) · **Status**: verdict-set (engineer ratifies)
**Date**: 2026-06-22 · **Contract**: `specs/031-gleam-port-spike/contracts/dossier-outline.md`.

> **Evidence convention (FR-009)**: every feasibility/"it works" claim below carries a
> **command + observed output** (recorded in `toolchain-inventory.md` and
> `hello-glp-term/README.md`) or an **authoritative citation**. Nothing rests on assertion.
> **Authority (FR-011)**: this dossier *recommends*; the engineer *ratifies* the source
> decision and any roadmap edits.

---

## 1. Executive summary & verdict

**Recommended source basis (one sentence):** Port from the **Dart source `glp_runtime/`** —
the single coherent, current, authoritative runtime tree whose sealed-term / immutable-field
idioms map most directly onto Gleam's ADTs — **not** the C# source, whose surface is
fragmented (a regenerable mirror that is itself generated *from* the Dart, plus hand-written
feature modules, plus a gitignored snapshot).

**Verdict: GO — with revisions.** The Gleam + Erlang/OTP toolchain is proven end-to-end
(a Gleam module constructs a GLP term, performs one unbound→bound bind, compiles to BEAM, and
runs on Erlang **and on AtomVM** with reproducible observed output). Plain **BEAM is viable** as
the test runtime; **AtomVM is viable on the host build** — the *full* Gleam smoke (term + the
process/state-holder unbound→bound bind over real BEAM processes) runs on AtomVM when the cell is
spawned via a raw `erlang:spawn` external plus `gleam_erlang` Subjects; only `gleam_otp`'s
`proc_lib`-based actors are outside AtomVM's subset; **JavaScript is partially viable** (pure
functional code ports; the BEAM-process concurrency model does not). The revisions (enumerated in
§7) re-scope the heap-mutation strategy (F5) and the link layer (F9), and fix the AtomVM
concurrency substrate to raw `erlang:spawn` (proven); the compiler/loader (F6) is confirmed
largely unchanged. None of these is a blocker — they change *scope*, not viability.

---

## 2. Source-language decision

### 2.1 Criteria table  *(FR-001; US1 acceptance #1)*

Rows = the three candidates; columns = the four required criteria. Every cell is evidenced
(counts via `git ls-files`, dates via `git log -1 --format=%cs`, structure via file reads).

| Candidate | Source health & currency | Structural fit to Gleam | Conversion effort | Divergence between the two sources |
|---|---|---|---|---|
| **Dart** (`glp_runtime/`) | **Strongest.** Single coherent tree: **151 `.dart`** under `lib/{analysis,bytecode,compiler,engine,link,lint,multiagent,runtime}` (all 8 subsystems); last commit **2026-06-08**, **18 commits/90d**; ships REPL `bin/glp_repl.dart` + prebuilt `glp_repl.exe`. The authoritative reference. | **Best.** `abstract class Term` + sealed subclasses `ConstTerm`/`StructTerm`/`VarRef` with immutable `final` fields → near-direct Gleam custom-type mapping (`terms.dart:1-46`). Functional DI (function-typed compiler fields, `compiler.dart:33-50`) → Gleam first-class functions. Null-safety → `Option`. | **Tractable.** Largest surface (151 files) but well-factored; the existing `codeconv` Dart→C# pipeline already proves Dart is a clean conversion *input*. The one real cost is re-expressing the mutable heap (see §4.1) — independent of source language. | **Is the baseline.** The C# is generated from Dart; divergence is measured *against* Dart. |
| **C#** (multi-rooted — see §2.2) | **Fragmented.** No single authoritative hand-maintained C# runtime tree: `out/csharp/lib` (**79 `.cs`**, tracked but **regenerable**, generated from Dart via feature 020 — header: *"source: glp_runtime/lib/bytecode/runner.dart"*) has **no `link` source**; `csharp/` (**79 `.cs`**, hand-written feature modules) last commit **2026-06-11**; `glp_runtime_net/` is **gitignored/regenerable** (`.gitignore:39`). | **Good-but-noisier.** Same ADT potential as Dart, but each type carries C# `IEquatable`/`GetHashCode` boilerplate Gleam doesn't need (`out/csharp/lib/runtime/terms.cs:5-78`). `csharp/glp_link` uses `async/await` + `IAsyncEnumerable` — **furthest** from Gleam (no async/await). | **Higher / circular.** Porting from `out/csharp` is **circular** (it is generated from the Dart); the `csharp/` modules are separate hand-port tasks; no single root to point a langpair at. | **The key signal** — see §2.2. Link layer and il_codec diverge between the two sources. |
| **File-by-file replication of both** | Inherits both surfaces — but they are **not at parity** (link layer + il_codec diverge), so "replicate both" means reconciling a divergence, not copying a doubled-but-identical corpus. | No better than Dart-alone for the shared core; *adds* the C#-only async idioms as extra friction. | **Highest, worst cost/benefit.** ~2× the surface plus a hand reconciliation of every Dart↔C# divergence. | Would have to *resolve* the divergence (which source wins per subsystem) — re-introducing exactly the question a single-source choice settles. |

### 2.2 The C# candidate is multi-rooted — and divergent  *(Edge case; research R5)*

Treating "the C# source" as one thing is the trap the spec warned about. The three C# roots:

- **`glp_runtime_net/`** — the nominal hand-port *with its own REPL* (per the roadmap/spec), but in this repo it is **gitignored and regenerable** (`.gitignore:39`; `git ls-files glp_runtime_net` → empty) and lacks a `link/` subsystem.
- **`out/csharp/`** — the **tracked** C# artifact (79 `.cs`), but it is the **`codeconv scaffold` mirror generated *from* the Dart** (feature 020); porting a Gleam runtime from it would be porting from a machine translation of the real source.
- **`csharp/`** — hand-written, **feature-specific** modules only: `glp_il_codec` (45 files, feature 029) and `glp_link` (40 files, feature 025) — not a runtime.

**Dart↔C# divergence, surfaced explicitly as a criterion (not assumed parity):**
- **Link layer diverges**: Dart `glp_runtime/lib/link/` = **39** files; `out/csharp` has **no link source**; `csharp/glp_link/` = **40** files is a *separate parallel implementation* (async/await), not a mirror.
- **`glp_il_codec` is C#-only** — feature 029, **no Dart equivalent**. (Implication for §5.)
- **What *is* synced**: the runner deref-conflation fix (commit **`8af18c3a`**, 2026-06-08) is present in **both** Dart `runner.dart` and `out/csharp/lib/bytecode/runner.cs` — confirming the generated core stays mirrored, while the *hand-written features* are where the two sources part.

Net: whichever C# root you pick, you do not get a single authoritative, hand-maintained C#
runtime tree. The Dart tree is that single source.

### 2.3 Recommendation + rationale  *(FR-001, SC-001)*

**Recommendation: port from the Dart source `glp_runtime/`.**
**One-sentence rationale:** Dart is the single coherent, current, authoritative runtime whose
sealed-term/immutable-field idioms map most cleanly onto Gleam's ADTs, while the C# surface is
either generated *from* that Dart (circular to port) or fragmented across hand-written feature
modules and a gitignored snapshot.

**This overturns the roadmap's initial C#-lean** — on evidence: the only *tracked* C# runtime
(`out/csharp`) is a generated mirror of the Dart, and the hand-written C# (`csharp/`) is
feature-scoped, not a runtime. The roadmap's lean (presumably toward C#'s static typing /
the existing codeconv C# target) does not survive the repo reality. Aligning the Gleam port's
source with the codeconv pipeline's *input* (Dart) is also the natural fit for the downstream
`codeconv-gleam-langpair` feature.

---

## 3. Build-target matrix  *(FR-002; US4; contract build-target-matrix.schema.md)*

| target | verdict | evidence | constraints | host_vs_hardware |
|---|---|---|---|---|
| **Erlang/BEAM** | **viable** | `gleam run --target erlang` → full observed output (term `pair(label, _G0)`; bind `_G0 := bound_atom`; reader observes `bound_atom`; resolved `pair(label, bound_atom)`); `gleam test` → **4 passed**; clean `rm -rf build && gleam run` reproduces identically. (`hello-glp-term/README.md` §BEAM) | None for the spike's scope; `gleam_otp`/`gleam_erlang` fully available. The **test runtime**. | host build |
| **AtomVM** | **viable** (host build) | The **full Gleam smoke** (term + the process/state-holder unbound→bound bind over real BEAM processes) runs on the AtomVM host build → byte-identical observed output to Erlang + `Return value: nil` (README §AtomVM). Host sanity: the release's `hello_world.avm` → `Return value: ok`. | **Subset constraint:** spawn the cell via a raw `erlang:spawn` external (+ `gleam_erlang` Subjects), NOT `gleam_otp` — AtomVM omits **`proc_lib`**, which `gleam_otp` (and `gleam_erlang`'s own `process.spawn`) route through (a `gleam_otp` actor build crashes: `module proc_lib cannot be resolved`). | host build (`AtomVM-linux-x86_64-static-mbedtls-v0.6.6`; no embedded HW) |
| **JavaScript** | **partially viable** | Functional subset (term + immutable bind, `gleam_stdlib` only) compiles + runs on **node v18.19.1** → `pair(label, _G0)` / `bound_atom`. Full smoke `gleam build --target javascript` → `error: Unsupported target … no implementation for the JavaScript target` at `process.send` / `process.receive`. (README §JavaScript; the JS-targetable functional subset is the committed `js-probe/` project) | **JS fallback cost vs BEAM:** pure compute + types port for free; but **`gleam_erlang` is BEAM-only**, so GLP's process/message-passing concurrency must be *replaced* (event loop / web workers) — a major rewrite. Viable for the *pure* compiler/type-checker, not the concurrent engine. | N/A (host runtime, node) |

No cell is "unknown". Every row has a verdict + ≥1 evidence item (SC-003).

---

## 4. Architectural-fit assessment  *(FR-006, FR-007, SC-006; US1 acceptance #2)*

### 4.1 Mutable heap / WAM-style cells vs Gleam immutability — **smoke-backed**

GLP's runtime binds logic-variable cells **in place** (the Dart runner mutates `sigmaHat` /
`clauseVars` and heap cells — e.g. `runner.dart:4123-4145`). Gleam has **no mutable
variables**. The smoke demonstrates — *with running evidence, not analysis* — that this maps
two ways:

- **Process/state-holder ("variable = BEAM process")**: a cell process holds `Option(Term)`;
  the writer process binds it; the reader process observes it. Observed output:
  `cell before bind … : unbound` → `cell after bind … : bound_atom`. The mutable cell's
  *identity* persists across the transition; the "mutation" is a message.
- **Functional sibling (immutable threaded state)**: binding produces a **new** value; the
  old cell is never mutated. Observed: `heap1 … : bound_atom` while `heap0 re-read … :
  unbound`.

**Bearing on the recommendation:** the WAM mutable heap **cannot be transliterated** into
Gleam; it must be re-expressed (immutable threaded store, or process-cells). This is a real
re-design cost (it lands on F5/F6, §5) but is **demonstrably feasible on BEAM** and does **not**
change the source choice — Dart's *term ADTs* port cleanly regardless; only the *heap-mutation
mechanism* is re-designed.

### 4.2 FCP concurrency / SRSW & suspension-reactivation vs BEAM processes — **top opportunity**

The smoke's process model maps an FCP-style **single-assignment** variable onto a BEAM
process: exactly one writer binds (the cell handler binds only while unbound), readers
observe. BEAM's process + message-passing substrate is a *natural* fit for FCP/SRSW
concurrency and suspension-on-readers. This is the epic's strongest tailwind.

**Bearing:** raises confidence in GO. The concurrency engine can be re-expressed idiomatically
on BEAM — and the **full Gleam bind demo runs on AtomVM** (raw `erlang:spawn` + `gleam_erlang`
Subjects, §3), so the concurrency model is the most portable part of the port, not a risk.

### 4.3 WAM-style bytecode execution & custom heap vs AtomVM's BEAM/OTP subset

AtomVM runs a **subset** of BEAM/OTP. The spike's evidence localizes the boundary precisely:
**`proc_lib` (hence `gleam_otp`/`gen_*`, and `gleam_erlang`'s own `process.spawn`) is unavailable,
but raw `erlang:spawn` + message passing + `gleam_erlang` Subjects work — the full Gleam smoke runs
on AtomVM that way (§3).** A WAM-style bytecode interpreter is plain sequential BEAM code (fine for
AtomVM); only the *spawn* primitive needs the raw form.

**Bearing:** the heavy features (bytecode runner F5, link layer F9) must, **if AtomVM is a
real target**, spawn via raw `erlang:spawn` (or a thin AtomVM-compatible actor) rather than
`gleam_otp`/`gen_*` — a proven, low-cost constraint, not a blocker. On **plain BEAM** (the test
runtime) `gleam_otp` is fine.

---

## 5. Downstream re-scope notes  *(FR-007, SC-005)* — roadmap-actionable

- **F5 — bytecode runner: RE-SCOPE.** The most affected feature. The WAM mutable heap
  (`sigmaHat`/`clauseVars`/in-place cell mutation in `runner.dart`) cannot be a literal port;
  re-scope to an **immutable threaded binding store** or a **process-cell** heap (both shown
  feasible by the smoke, §4.1). Additionally, if AtomVM is a target, keep the runner's hot
  path on **raw processes**, not `gleam_otp` (§4.3).
- **F6 — compiler/loader: CONFIRMED LARGELY UNCHANGED.** The compiler pipeline
  (lexer→parser→analyzer→codegen, `compiler.dart:33-50`) is pure data transformation that maps
  cleanly to Gleam ADTs + pattern matching. *Minor* re-scope: the loader / module-activation
  that spawns service loops should use the chosen concurrency substrate (BEAM `gleam_otp`, or
  raw processes for AtomVM).
- **F9 — link layer: RE-SCOPE.** The Dart link layer (39 files, async streams) and the C#
  `glp_link` (async/await + `IAsyncEnumerable`) both use async idioms **with no Gleam
  equivalent**; re-express on Gleam processes / `gleam_otp` (BEAM) and on **raw processes** for
  AtomVM (no `proc_lib`). A real re-design — flag it.
- **Also affected — `glp_il_codec` (feature 029): C#-ONLY.** It has **no Dart source**. If the
  Gleam port needs IL-codec functionality, that is a **separate port from the C# `csharp/glp_il_codec`**,
  not from Dart — name it explicitly so the roadmap accounts for it.

---

## 6. Downstream handoff (for F2/F3)  *(FR-008, SC-004)*

- **Chosen source basis:** **Dart `glp_runtime/`** (authoritative; the tracked C# is generated
  from it). `codeconv-gleam-langpair` (F2) targets the **Dart→Gleam** direction, mirroring the
  existing Dart→C# langpair's input.
- **Assumed `glp_gleam/` project layout & conventions** (proven by the smoke):
  a standard Gleam project — `gleam.toml` + committed `manifest.toml` + `src/` + `test/`
  (gleeunit). Mirror Dart's `lib/` subsystems as Gleam modules under `src/glp/` (e.g.
  `glp/runtime`, `glp/bytecode`, `glp/compiler`, `glp/engine`, `glp/link`, `glp/analysis`).
  Build `gleam build --target erlang`; test `gleam test`. Place `glp_gleam/` as a repo-root
  subtree, sibling to `glp_runtime/` and `glp_runtime_net/`. **(F3 creates this subtree — not
  this spike.)**
- **Toolchain versions to build against:** Gleam **1.17.0** · Erlang/OTP **25.3.2.8** ·
  rebar3 **3.19.0** · deps `gleam_stdlib` 1.0.3 / `gleam_erlang` 1.3.0 / `gleeunit` 1.11.0
  (**NOT `gleam_otp`** — its `proc_lib` use is outside AtomVM's subset; spawn raw) · node
  **v18.19.1** (only if the JS backend is exercised). **Environment:**
  Linux / **WSL Ubuntu** (see toolchain-inventory.md environment finding).

---

## 7. Conclusion — single verdict + required revisions  *(FR-010, SC-005; US1 acceptance #3)*

**Verdict: GO, with revisions.** The Gleam port of GLP is feasible; proceed on the epic with
**plain BEAM as the test runtime** and the **Dart source** as the port basis. The required
roadmap revisions:

1. **Ratify source = Dart** (`glp_runtime/`), overturning the initial C#-lean. *(§2)*
2. **Re-scope F5 (bytecode runner):** replace the WAM in-place mutable heap with an immutable
   threaded store or process-cell heap; keep AtomVM-targeted concurrency on raw BEAM
   processes. *(§4.1, §4.3, §5)*
3. **Re-scope F9 (link layer):** replace async/await idioms with the Gleam process model;
   AtomVM-safe variant uses raw processes (no `proc_lib`). *(§5)*
4. **F6 (compiler/loader): confirmed largely unchanged** — pure pipeline ports cleanly; only
   the loader's process-spawning follows the concurrency-substrate decision. *(§5)*
5. **AtomVM is viable on the host build** — the *full* Gleam smoke (term + process-cell bind)
   runs on AtomVM. The only constraint: spawn the cell via a raw `erlang:spawn` external (+
   `gleam_erlang` Subjects), **not** `gleam_otp` (`proc_lib` is out of AtomVM's subset). The epic
   can target AtomVM (P2) with confidence; plain BEAM remains the test runtime. *(§3, §4.3)*
6. **Account for `glp_il_codec` (C#-only):** if needed, a separate port from C#, not Dart. *(§5)*
7. **Standardize the dev environment** on Linux/WSL with the pinned versions; native-Windows is
   viable for a developer with admin rights but was not exercised in this spike. *(§6;
   toolchain-inventory.md)*

The dossier is self-sufficient: source basis, criteria, matrix, architectural-fit (with the
smoke-backed mutable-heap finding), downstream re-scopes, and the single go/no-go verdict are
all here — a reviewer can ratify, reject, or request revisions from this document alone.

---

## Appendix — Acceptance self-check (contract dossier-outline.md)

- [x] Exactly one recommended source basis (**Dart**). *(SC-001)*
- [x] Criteria table present with all four criteria, every cell evidenced. *(FR-001)*
- [x] Dart↔C# divergence surfaced as a criterion (link layer; il_codec), not assumed parity. *(Edge case)*
- [x] Architectural-fit names the two required findings; mutable-heap finding cites the running smoke. *(SC-006)*
- [x] Every heavy downstream feature named with re-scope or "unchanged" (F5 re-scope, F6 unchanged, F9 re-scope). *(SC-005)*
- [x] Exactly one go/no-go/go-with-revisions verdict (**go-with-revisions**), revisions enumerated. *(FR-010, SC-005)*
- [x] Every "it works"/feasibility claim has command+output or citation. *(FR-009)*
- [x] Reviewer can act using only this document. *(SC-001)*

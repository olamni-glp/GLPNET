---
title: "Imported-variable heap model — madGLP local-pairs (authoritative) vs. heap_fcp.dart VariableEntry-on-RoTag (dead irmaGLP residue)"
authors: "glpnet local specs + live runtime code (madGLP-spec by Claude; heap_fcp.dart/mad_context.dart/variable_table.dart project code); model derived from Shapiro CGLP §7 madGLP"
year: "2026"
source_url: "local: D:/bstdev/research/glp/glpnet — docs/ma/madGLP-spec.md §11.3; glp_runtime/lib/runtime/heap_fcp.dart; glp_runtime/lib/multiagent/{mad_context.dart,variable_table.dart,payload_serializer.dart}; glp_runtime/lib/multiagent/archive-irma-2026-01-30/"
retrieved: "2026-06-06"
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Two heap models for imported variables coexist in code: madGLP (variable_table.dart says 'all variables are local pairs; W_p holds routing; no special imported representation') vs heap_fcp.dart's live VariableEntry-on-RoTag path (bindImportedReader, isImportedReader) inherited from irmaGLP. Which is authoritative for new distributed work, and is the VariableEntry path dead or still exercised?"
precedence_class: glp-current
access: full-text
---

# Imported-variable heap model: which is authoritative, is VariableEntry dead?

## TL;DR

**madGLP — "all variables are local pairs; the global writers table (W_p) holds routing; no special imported representation" — is authoritative for new distributed work.** It is the model spelled out in the authoritative subsystem spec (`docs/ma/madGLP-spec.md` §11.3), and it is the model the *live* runtime (`MadContext`) actually implements via `globalize`/`localize`/`global_send` + `GlobalWritersTable`.

**The `heap_fcp.dart` `VariableEntry`-on-`RoTag` path is dead in live code.** Its sole non-archive callers were the irmaGLP `IrmaContext`, which now lives under `glp_runtime/lib/multiagent/archive-irma-2026-01-30/` (superseded). In the live tree, `MadContext` never calls `allocateImportedReader/Writer`, never attaches a `VariableEntry`, and never calls `bindImportedReader`. The live multiagent test suite (`glp_runtime/test/multiagent/`) contains **zero** references to `bindImportedReader`, `allocateImportedReader`, `isImportedReader`, or `VariableEntry`. The methods survive in `heap_fcp.dart` only as unexercised compatibility code — but they are NOT dead-strippable without care, because the protocol in CLAUDE.md ("Preserve Working Code — NEVER remove `_ClauseVar`, fallback cases, or any code you don't fully understand without explicit approval") covers them.

A distributed link layer (blocker B2) MUST adopt the madGLP local-pairs + global-writers-table model and MUST NOT build on the `VariableEntry` representation.

---

## 1. The authoritative spec passage (highest precedence: glp-current)

`docs/ma/madGLP-spec.md` §11.3 "Heap Representation" (verbatim):

> Local variable pairs use standard two-cell allocation:
>
> - Writer cell: WrtTag, content is null (unbound), SuspensionListNode (waiting), or Pointer (bound)
> - Reader cell: RoTag, content is Pointer to writer cell
>
> No special representation is needed for "imported" variables—all variables are local pairs. The global writers table provides routing information separately from the heap representation.

This is unambiguous and is the single source of truth for the subsystem. The spec's References section explicitly flags the older model as superseded:

> - **Previous Spec**: `archive/irmaGLP-spec-v3.1-2026-01-30.md` (request-based model, now superseded)

So at the spec layer, madGLP wins by construction; irmaGLP is "now superseded."

---

## 2. What the madGLP model is (the routing layer that replaces the shared variable)

Per madGLP-spec §1.1, §3, §5, §11.3 — the model that the link layer must preserve:

- A maGLP shared pair `(X, X?)` split across agents is realized as **two fully local pairs** — `(X_p, X_p?)` at p and `(X_q, X_q?)` at q — **connected by a global link**, not by a special heap cell.
- The "global link" = a `global_send` goal at one end + a **Global Writers Table (W_p)** entry at the other. The table maps a global name (`_w(p,i)` / `_r(p,i)`) to the local writer that will be assigned when a remote assignment message arrives (§3).
- **Globalize** (export): for an exported writer Y → create W_p entry `(Y,q)` at index i, substitute `_w(p,i)`; for an exported reader Y? → spawn `global_send(Y?,_r(p,i),q)`, substitute `_r(p,i)` (§5.1).
- **Localize** (import): for incoming `_w(p,i)` → **create a fresh local pair**, put the writer in the term, spawn `global_send`; for incoming `_r(p,i)` → **create a fresh local pair**, add a W_q LocalizeEntry `(Z_q,p,i)`, put the reader in the term (§5.2).
- Imported variables are therefore **ordinary local pairs** (`heap.allocateVariable()`); the cross-instance identity lives entirely in W_p + the spawned `global_send` goals, **outside the heap cell**.

Mechanism inspiration (precedence: earlier-cl-paper / glp-paper, NEVER overriding the above): the local-pairs-plus-routing decomposition is the CGLP §7 "Multiagent Deterministic GLP (madGLP)" model (madGLP-spec §References cites `~/Grassroots/CGLP` §7). The two-cell FCP heap it sits on (WrtTag/RoTag, reader→writer pointer, suspensions on the writer) is the Flat Concurrent Prolog cell model.

---

## 3. The dead path: heap_fcp.dart VariableEntry-on-RoTag (irmaGLP residue)

`glp_runtime/lib/runtime/heap_fcp.dart` still carries a *separate*, *competing* representation for imported variables:

- `allocateImportedReader()` / `allocateImportedWriter()` — allocate a *single* cell (no paired writer/reader), content to be set to a `VariableEntry` by the caller. Docstrings literally say "Per irmaGLP spec, imported readers have no local paired writer." (lines 99–117)
- A reader cell whose `content is VariableEntry` is the "unbound imported reader"; suspensions for it are stashed in `VariableEntry.suspensions` (`suspendOnReader`, lines 493–514; `SuspendOps._suspendOnVariable`, suspend_ops.dart:40).
- `bindImportedReader(readerAddr, value, entry)` — binds it by allocating a `ValueTag` cell and re-pointing the reader to it; drains `entry.suspensions` (lines 641–664).
- `isImportedReader()` / `getReaderValue()` / `isReaderBound()` branch on this `VariableEntry`/`Pointer→ValueTag` shape (lines 719–801).
- `derefAddr()` returns a `VariableEntry` for an unbound imported var (lines 281–306).

`VariableEntry` itself (`glp_runtime/lib/multiagent/variable_table.dart`) is now a stripped stub. Its own header records the supersession (verbatim):

> Provides VariableEntry for tracking suspensions on imported readers.
> The full VariableTable (V_p) has been replaced by GlobalWritersTable (W_p)
> for madGLP. This file provides only the entry type needed by the core
> runtime for suspension management.

So the *table* (V_p) is already gone; only the *entry type* lingers because `heap_fcp.dart` still type-references it.

---

## 4. Evidence the VariableEntry path is NOT exercised in live code

### 4.1 Live MadContext uses madGLP exclusively
`glp_runtime/lib/multiagent/mad_context.dart`:
- Holds `final GlobalWritersTable wp;` and `final GlobalSendRegistry globalSendRegistry;` (lines 33, 39, 61–63). No `VariableTable`, no `VariableEntry` field.
- Imports variables via `localize(...)` with `freshAddrAllocator: () => runtime.heap.allocateVariable()` (lines 282–286, 343–347, 390–394, 428–432) — i.e. **standard local pairs**, exactly per §11.3.
- Binds incoming assignments with `runtime.heap.bindVariable(writerAddr, localizedValue)` (lines 355, 402) — the ordinary local-writer bind, **not** `bindImportedReader`.
- Never calls `allocateImportedReader`, `allocateImportedWriter`, or attaches a `VariableEntry`.

### 4.2 The VariableEntry path's only caller is the archived IrmaContext
The full-tree grep for `bindImportedReader` / `allocateImportedReader` / `attachImportedVariableEntry` returns hits in exactly two places:
1. `heap_fcp.dart` (the definitions themselves), and
2. `glp_runtime/lib/multiagent/archive-irma-2026-01-30/` (the superseded `IrmaContext` at `irma_context.dart:683,690,704,905,1057` plus its archived tests).

`payload_serializer.dart`'s `onVariableImported` callback (lines 530–533, 642–643) is the seam IrmaContext used to attach a `VariableEntry`; the live MadContext does not pass that callback for variable import — it localizes instead.

### 4.3 Live tests touch none of it
`glp_runtime/test/multiagent/` (the live suite — `globalize_test.dart`, `localize_test.dart`, `global_send_test.dart`, `global_writers_table_test.dart`, `mad_transactions_test.dart`, `mad_scenarios_test.dart`, isolate/cold-call tests, bonds/cssn isolate tests, etc.): grep for `bindImportedReader|allocateImportedReader|isImportedReader|VariableEntry` → **No matches found.** Every `allocateImportedReader`/`VariableEntry` test usage is under `archive-irma-2026-01-30/tests/`.

**Conclusion on "dead or exercised":** dead in live execution (no live caller, no live test). The `VariableEntry` type and the `heap_fcp.dart` imported-reader methods compile and are reachable by API, but no live runtime path invokes them; they are unexercised compatibility scaffolding from irmaGLP.

---

## 5. Ruling for blocker B2 (distributed unification) / link-layer design

- **Adopt:** madGLP local-pairs + Global Writers Table + `global_send`/`globalize`/`localize`. This is what every new remote link must preserve: each end is an ordinary local writer/reader pair; the wire carries global names `_w(p,i)`/`_r(p,i)`; routing/suspension-bridging lives in W_p and spawned `global_send` goals, NOT in the heap cell. This keeps SRSW intact per-instance, keeps writer-MGU "binds only local writers," and preserves three-valued unification because an unbound imported value is just an unbound *local* writer (suspends normally) until an assignment message arrives and `bindVariable` fires reactivations.
- **Do NOT build on:** `VariableEntry`/`bindImportedReader`/`allocateImported*`. Designing the link layer against that path would resurrect the superseded irmaGLP request-based model and contradict madGLP-spec §11.3.
- **Do NOT unilaterally delete** the dead path: CLAUDE.md "Preserve Working Code" forbids removing fallback/edge code without explicit approval. The clean contradiction ("two models coexist") is real but is resolved in madGLP's favor at both the spec layer and the live-code layer; if the engineer wants the residue removed, that is a separate, approval-gated cleanup (candidate: drop `allocateImportedReader/Writer`, `bindImportedReader`, `isImportedReader`, the `VariableEntry` branches in `derefAddr`/`suspendOnReader`/`suspend_ops.dart`, and the stub `variable_table.dart`).

---

## 6. Source precedence applied

No external Shapiro paper was needed or fetched: the question is answered entirely by **glp-current** sources (the authoritative subsystem spec + the live runtime code), which are the highest authority and which any earlier CL/FCP paper could not override. The model the spec mandates (madGLP local-pairs + W_p routing) is itself the CGLP §7 madGLP design; the FCP two-cell heap underneath is mechanism inspiration only. There is no conflict to resolve across precedence classes — the spec and the live code agree, and the contradiction is purely between live code and *archived* code.

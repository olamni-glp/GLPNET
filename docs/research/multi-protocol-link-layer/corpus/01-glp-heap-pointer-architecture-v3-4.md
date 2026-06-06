---
title: "GLP Heap Storage Specification — Pointer Architecture v3.4"
authors: "GLP project (glpnet); design follows the original FCP implementation (Shapiro et al.)"
year: "2026"
source_url: "file:///D:/bstdev/research/glp/glpnet/docs/heap/heap-pointer-architecture-spec.md"
retrieved: "2026-06-06"
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Fetch, preserve & extract source: GLP Heap Storage Specification — Pointer Architecture v3.4"
precedence_class: glp-current
access: full-text
---

# Extraction: GLP Heap Storage Specification — Pointer Architecture v3.4

## Provenance and precedence

This is a **local GLP spec** (`docs/heap/heap-pointer-architecture-spec.md`), version **3.4**, dated **2026-01-31**, status `DRAFT - FCP bidirectional pointers`, branch `pointer-architecture`. Per SOURCE PRECEDENCE it is **glp-current** = highest authority (current implementation truth). It explicitly grounds itself in the original FCP implementation (FCP source at `/Users/udi/Dropbox/Concurrent Prolog/FCP/Merged EMULATOR/`; cites `kernels.c:522-526` and `emulate.c:114-117`). This document supersedes the arithmetic-based v2.18 scheme.

This file is the **authoritative description of the atomic writer/reader cell** that any remote/distributed link-layer cell must preserve to keep distributed unification (blocker B2) faithful to local GLP semantics.

---

## 1. The atomic unit: a bidirectional writer/reader cell PAIR

A fresh logic variable is **two heap cells that point at each other** (the FCP bidirectional pointer pattern). This is the single most load-bearing fact for B2: the "atomic pair" is not one cell — it is a writer cell and a reader cell mutually linked, and the link is an explicit pointer, not address arithmetic.

> **FCP Evidence** (§1.2): "In `kernels.c:522-526` and `emulate.c:114-117`, FCP allocates variable pairs with bidirectional pointers:"
> ```c
> *HP = Var_Word((HP+1), WrtTag);   // Writer points to reader
> HP++;
> *HP = Var_Word((HP-1), RoTag);    // Reader points to writer
> ```

Allocation in the local runtime (§3.1) mirrors this exactly:

> ```dart
> (int, int) allocateVariable() {
>   final writerAddr = HP;
>   final readerAddr = HP + 1;
>   HP += 2;
>   // Writer cell: points to its reader (FCP pattern)
>   cells.add(HeapCell(Pointer(readerAddr), CellTag.WrtTag));
>   // Reader cell: points to its writer
>   cells.add(HeapCell(Pointer(writerAddr), CellTag.RoTag));
>   return (writerAddr, readerAddr);
> }
> ```

> **Key point (FCP pattern)** (§3.1): "Both cells point to each other: Reader points TO the writer (for dereferencing to find value); Writer points TO the reader (for finding paired reader without arithmetic). This enables `readerForWriter(writerAddr)` to simply follow the pointer, eliminating all `+1` arithmetic."

**B2 implication:** when splitting a writer X and reader X? across two REPL instances, the link primitive replaces the *mutual pointer* between the two cells. Locally the writer-cell pointer and reader-cell pointer are co-resident on one heap; remotely they live on two heaps and the transport must carry the binding that the writer cell would have stored. The model to preserve is "two cells, each able to find the other" — but a remote reader has **no local writer** (see §10 below), so the cross-instance case is already anticipated by the imported-variable design.

---

## 2. Cell structure: tags and content rules

### Tags (§2.1)
```dart
enum CellTag {
  WrtTag,    // Writer cell (unbound or bound)
  RoTag,     // Reader cell (read-only view)
  ValueTag,  // Bound to ground value (optimization)
}
```

### Content rules by tag (§2.3) — verbatim

**WrtTag (Writer Cell)**:
- `Pointer(readerAddr)` — unbound, no suspensions, points to paired reader (FCP pattern)
- `WriterContent(Pointer(readerAddr), SuspensionListNode)` — unbound with suspensions, preserves reader pointer
- `Pointer(valueAddr)` — bound to value at addr (or transitively to another variable via reader)

**RoTag (Reader Cell)**:
- `Pointer(writerAddr)` — points to paired writer (always)
- `Pointer(valueAddr)` — after path compression, may point directly to value

**ValueTag (Ground Value)**:
- `Term` — the bound ground term (ConstTerm or StructTerm)

> **FCP Pattern** (§2.3): "Both cells point to each other. This enables navigation in both directions without address arithmetic. When suspensions are added to an unbound writer, the reader pointer is preserved in a compound `WriterContent` structure."

A **VarRef** is "simply a heap address. The cell's tag determines whether it's a reader or writer." (§3.2). `isWriter(addr)` ⟺ tag==WrtTag; `isReader(addr)` ⟺ tag==RoTag.

---

## 3. Dereferencing + path compression (deref is mutating, by design)

> **Definition** (§4.1): "Dereferencing is the act of following a chain of references until reaching the final object which is not a reference. As part of dereferencing, the initial reference is updated to point directly to the final object (path compression). **This is integral to the design, not an optional optimization.**"

`derefAddr(startAddr)` returns one of three things (§4.2): `Term` (bound) | `VarRef` (unbound local writer) | `VariableEntry` (unbound imported reader). The algorithm (§4.2):
- **RoTag** with a `Pointer` → follow it; with a `VariableEntry` → return it (imported reader, no local writer to follow; caller treats as unbound).
- **WrtTag** with a `Pointer` → if the target is the paired reader pointing back to `current`, this is **unbound** → return `VarRef(current)`; otherwise it is bound to another cell → follow. With `WriterContent` → unbound (has suspensions) → return `VarRef(current)`.
- **ValueTag** → the content `Term` is the final value.

**Path-compression semantics (§4.3):** updates references to point directly to the final target so repeated derefs are O(1) after the first. "Compression is applied to the starting cell only." Full-chain compression is an allowed-but-not-required further optimization. **Readers are compressed during read-only deref; writers are not** (§4.2 Phase 2: "Writers are not updated during read-only dereference / Writer compression happens during binding").

**Staging (§4.4):** Stage 1 = pointer-following without compression (read-only); Stage 2 = add compression. "The final implementation must include path compression as specified."

---

## 4. WxW (writer-to-writer) detection — the SRSW invariant in the heap

This is the heap-level enforcement of SRSW / writer-MGU "never binds writer to writer."

> **Invariant (§4.5):** "During dereferencing, if we follow a pointer and land on a writer, the previous cell MUST have been a reader. This is because writer-to-writer bindings are forbidden."
> ```dart
> if (cells[current].tag == CellTag.WrtTag && previousTag == CellTag.WrtTag) {
>   throw StateError('SRSW violation: writer points to writer');
> }
> ```

> "**Implementation requirement**: The deref operation MUST check this invariant and throw if violated... This provides defense-in-depth: even if a bug allows WxW binding to occur, deref will detect and report it."

v3.4's sole change clarified this check is **mandatory, not debug-only** (may be moved behind a debug flag only "once the implementation is mature and well-tested"; "During development, it remains mandatory").

The forbidden binding is also blocked at bind time (§5.2):
> ```dart
> if (value is VarRef && isWriter(value.addr)) {
>   throw StateError('WxW violation: cannot bind writer to writer');
> }
> ```

**B2 implication:** a distributed bind must preserve "writers bind only to readers/values, never to writers." A remote unification protocol that could bind two writers across instances would violate the core invariant; the wire protocol must designate exactly one writer side per shared variable.

---

## 5. Binding (writer-MGU at the heap level)

`bindWriter(writerAddr, value)` (§5.1):
1. Save/activate suspension list if present (`_walkAndActivate`).
2. If `value is VarRef`: store `Pointer(value.addr)`; **tag stays WrtTag** (bound to a variable, not ground).
3. Else (ground value): store the `value`; **tag becomes ValueTag**. Returns the list of goals to reactivate.

**Only writers can be bound** — `assert(cell.tag == CellTag.WrtTag, 'Can only bind writers')`. This is the heap encoding of "Writer MGU binds ONLY writers, never readers."

**Writer→reader binding (variable-to-variable, §5.3):** when writer W is bound to reader R: "(1) W's content becomes `Pointer(R.addr)`; (2) Any suspensions on W are forwarded to R's writer." `bindWriterToReader` forwards W's suspensions to `findWriter(readerAddr)` and returns no activations yet — "goals wait for target."

---

## 6. Suspension storage and reactivation (the heart of three-valued unification)

A goal that meets an unbound reader **suspends**; the suspension is stored **on the reader's writer**, and is reactivated when that writer is bound. This is the runtime realization of the Suspend value of three-valued unification.

**Adding a suspension (§6.1):** `suspendOnReader(readerAddr, record)` follows the reader's pointer to the writer, then:
- If the writer already holds `WriterContent` → push the new `SuspensionListNode` onto its suspension list.
- If the writer holds a bare `Pointer` (first suspension) → convert to `WriterContent(readerPtr, node)`, **preserving the reader pointer**.

> **Note (§6.1):** "The writer must preserve its pointer to the paired reader even when suspensions are added. This enables `readerForWriter()` to work at any time."

**Suspension records (§6.2):**
```dart
class SuspensionRecord { int? goalId; final int resumePC; void disarm(); bool get armed; }
class SuspensionListNode { final SuspensionRecord record; SuspensionListNode? next; }
```
A record is **disarmed** by nulling `goalId`; `armed` ⟺ `goalId != null`. Suspensions form a singly-linked list off the writer.

**Activation on bind (§6.3):** `_walkAndActivate` walks the list; for each armed record it appends `GoalRef(goalId, resumePC)` to activations and disarms the record. (Called from `bindWriter` when binding to a ground value.)

**Forwarding on variable-to-variable bind (§6.4):** when W1 is bound through reader R2, W1's armed suspensions are re-linked onto R2's writer W2 (new nodes **sharing the same record**), so the waiting goals follow the variable they now depend on.

**B2 implication:** the cross-instance reader must be able to (a) register a suspension locally and (b) be reactivated when the remote writer binds — i.e. the link primitive must carry a "value-arrived" event back to the importing instance to drive the local equivalent of `_walkAndActivate`. The local model already factors this out via VariableEntry (next section).

---

## 7. Finding the paired cell (no arithmetic, ever)

- **Reader → Writer (§7.1):** `writerForReader(addr)` follows the reader's `Pointer` (asserts the reader is *local* — content is a Pointer). `tryWriterForReader(addr)` returns `null` for **imported readers** (whose content is a `VariableEntry`, not a Pointer). Guidance: use the strict form for known-local readers (e.g. just-allocated pairs); use `try…` when the reader might be imported (e.g. OutputObserver callbacks).
- **Writer → Reader (§7.2, FCP pattern):** `readerForWriter(addr)` follows the writer's pointer; if unbound-with-suspensions, reads `WriterContent.readerPointer`. Bound writers may not give direct reader access. "No address arithmetic (`+1`) is ever needed."

§9.2 makes the prohibition explicit and **CRITICAL**: "Code must NEVER use address arithmetic to navigate between writer and reader." All `writerAddr+1` / `readerAddr-1` are replaced by `writerForReader` / `readerForWriter`.

---

## 8. Heap diagrams (state shapes to replicate across the wire)

- **Unbound (§8.1):** `[WrtTag|Ptr(1)]` + `[RoTag|Ptr(0)]` — mutual pointers; "to check if unbound: follow writer's pointer, verify target is reader that points back."
- **Unbound + suspension (§8.2):** `[WrtTag|(Ptr(1),SusQ)]` + `[RoTag|Ptr(0)]` — writer keeps the reader pointer plus a suspension queue.
- **Bound to ground value (§8.3):** `[ValueTag|42]` + `[RoTag|Ptr(0)]` — the former writer now holds the value; the reader still points at addr 0 and derefs through it.
- **Bound to another variable (§8.4):** writer X holds `Ptr(reader-Y)`; deref of X walks X-writer → Y-reader → Y-writer and (if Y unbound) returns `VarRef(Y-writer)`.

---

## 9. Imported / cross-instance variables (multiagent) — the SEED for distributed unification

**This section is the most directly relevant to the multi-protocol link layer.** The local model already supports a reader **with no local writer**, which is exactly the situation when a writer/reader pair is split across instances.

> **§10 (verbatim):** "For multiagent GLP, imported readers have no local writer. The reader cell contains a VariableEntry (virtual writer) instead of a Pointer:"
> ```
> +-------+-------------+
> | RoTag | VarEntry    |  ← Imported reader: contains V_p entry
> +-------+-------------+
> ```
> "The VariableEntry serves as the 'virtual writer' and holds: Creator agent ID; Creator's local address; Suspension queue (for local goals waiting); Received value (after assignment arrives). When `derefAddr` encounters an imported reader (cell content is VariableEntry), it returns the VariableEntry directly. Callers should treat this as 'unbound' and suspend the goal, similar to encountering an unbound local writer."

**B2 implications (fidelity yardstick):**
1. The "virtual writer" (`VariableEntry`) is the local stand-in for a *remote* writer. A distributed link primitive maps the remote writer onto a `VariableEntry`-like object: it must carry **creator agent/instance ID**, the **creator's local address** (the remote cell identity), a **local suspension queue**, and a slot for the **received value**.
2. `derefAddr` returning the `VariableEntry` and the caller suspending is precisely the **Suspend** arm of three-valued unification across instances — no special-case path is needed beyond treating "imported, unbound" like "local writer, unbound."
3. Reactivation must be driven by an external "assignment arrives" event (the transport delivering the remote bind), which populates the `VariableEntry`'s received value and walks its suspension queue — analogous to `_walkAndActivate`.
4. The split is single-direction per variable: the side holding the real writer can bind; the side holding the imported reader can only read/suspend. This matches "one writer / one reader" SRSW and forbids the cross-instance WxW that §4.5/§5.2 prohibit locally.

---

## 10. Migration / affected code (where this model is enforced)

- `heap_fcp.dart` — core heap operations
- `suspend_ops.dart` — suspension management
- `runner.dart` — bytecode execution
- `irma_context.dart` — multiagent variable handling
- `variable_table.dart` — V_p management

Old `VarRef{varId, isReader}` → new `VarRef{addr}` (tag-determined role). All `±1` arithmetic removed in favor of explicit pointer following.

---

## 11. Document history (versions)

| Version | Date | Changes |
|---|---|---|
| 3.0 | 2026-01-20 | Pointer architecture (replaces arithmetic v2.18) |
| 3.1 | 2026-01-20 | `VariableEntry` added as `derefAddr` return for imported readers (§4.2, §10) |
| 3.2 | 2026-01-31 | FCP bidirectional pointers: writer points to reader; eliminates all `+1` arithmetic |
| 3.3 | 2026-01-31 | `tryWriterForReader()` for imported readers (§7.1) |
| 3.4 | 2026-01-31 | WxW detection during deref clarified as **mandatory** (§4.5) |

---

## 12. Related local artifacts (same directory)

- `docs/heap/fcp-bidirectional-pointers-plan.md` — the plan for the §3.2 FCP-pointer change.
- `docs/heap/implementation-plan.md` — staged implementation plan.
- FCP source of record: `/Users/udi/Dropbox/Concurrent Prolog/FCP/Merged EMULATOR/` (`kernels.c`, `emulate.c`) — earlier-CL-paper precedence (mechanism inspiration; does not override this glp-current spec).

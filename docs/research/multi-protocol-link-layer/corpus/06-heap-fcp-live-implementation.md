---
title: "heap_fcp.dart — FCP Two-Cell Heap with Pointer Architecture (live implementation)"
authors: "GLP runtime team (glpnet); byte-identical with sibling GLP repo"
year: 2026
source_url: "D:/bstdev/research/glp/glpnet/glp_runtime/lib/runtime/heap_fcp.dart"
retrieved: 2026-06-06
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Fetch, preserve & extract source: heap_fcp.dart (live heap implementation)"
precedence_class: glp-current
access: full-text
---

# heap_fcp.dart — Live Heap Implementation (ground truth)

This is the authoritative, executing heap module for GLP's variable representation.
It implements `docs/heap/heap-pointer-architecture-spec.md` (v3.4, FCP bidirectional
pointers). Because it is **glp-current** (live code), it is the HIGHEST source-precedence
artifact: where the spec and this file diverge, this file is what actually runs and is the
fidelity yardstick for any distributed link-layer scheme (blocker B2, distributed
unification). The class is `HeapFCP` (file 889 lines).

**Relationship to the spec:** The spec cites FCP origins directly — `kernels.c:522-526` /
`emulate.c:114-117` allocate variable pairs with bidirectional pointers
(`*HP = Var_Word((HP+1), WrtTag); HP++; *HP = Var_Word((HP-1), RoTag);`). `heap_fcp.dart`
is the Dart realization of exactly that scheme.

---

## 1. Cell representation (the atomic writer/reader pair)

Three tags (`CellTag`), matching FCP:

```dart
enum CellTag {
  WrtTag,   // Writer cell
  RoTag,    // Read-only (reader) cell
  ValueTag, // Bound to ground value
}
```

A `HeapCell` is `{ dynamic content; CellTag tag; }` where `content` is one of:
`null | Pointer | SuspensionListNode | Term | VariableEntry` (and `WriterContent`).

`Pointer` is a heap address wrapper: `class Pointer { final int targetAddr; }`.

`WriterContent` is the compound carried by an UNBOUND writer once it accrues suspensions —
it preserves the paired-reader pointer while holding the suspension list (so
`readerForWriter()` still works under suspension):

```dart
class WriterContent {
  final int readerAddr;       // Pointer to paired reader (preserved)
  SuspensionListNode? suspensions;
}
```

### Allocation — the bidirectional FCP pair (load-bearing)

`allocateVariable()` returns the `(writerAddr, readerAddr)` tuple; writer and reader point
AT EACH OTHER (no address arithmetic — explicit bidirectional pointers):

```dart
(int, int) allocateVariable() {
  final writerAddr = HP;
  final readerAddr = HP + 1;
  HP += 2;
  // Writer cell: points TO reader (FCP pattern)
  cells.add(HeapCell(Pointer(readerAddr), CellTag.WrtTag));
  // Reader cell: points TO writer
  cells.add(HeapCell(Pointer(writerAddr), CellTag.RoTag));
  return (writerAddr, readerAddr);
}
```

`pairedReaderAddr()` tries the FCP bidirectional pointer first, falling back to
`writerAddr + 1` by allocation convention.

---

## 2. The still-present IMPORTED-VARIABLE path (load-bearing for remote representation)

This is the single most important section for the distributed link layer: the heap ALREADY
models a variable whose other half lives in another agent/isolate. The remote-variable
representation the link layer needs should reuse / extend this, not invent a parallel one.

**Imported half-cells (no local paired writer/reader):**

```dart
int allocateImportedReader() {              // imported reader: no local paired writer
  final readerAddr = HP++;
  cells.add(HeapCell(null, CellTag.RoTag));
  return readerAddr;
}
int allocateImportedWriter() {              // imported writer: no local paired reader
  final writerAddr = HP++;
  cells.add(HeapCell(null, CellTag.WrtTag));
  return writerAddr;
}
```

The cell content is then set by the caller to a `VariableEntry`
(`lib/multiagent/variable_table.dart`). `VariableEntry` records the cross-agent identity and
holds the suspension list that a local-paired writer would otherwise hold:

```dart
class VariableEntry {
  final int varId;                          // local heap ID
  final bool isReader;                       // reader view vs writer view
  final String creator;                      // agent who created this variable
  final int creatorLocalId;                  // creator's original local ID
  Term? boundValue;                          // cached bound value
  int? pairedReaderCreatorLocalId;           // imported writer: creator's paired-reader ID
  SuspensionListNode? suspensions;           // goals waiting on this variable
}
```

**Distinguishing imported from local at the cell level** (`isImportedReader`):

| State            | cell.content   | Target cell        |
|------------------|----------------|--------------------|
| Unbound imported | VariableEntry  | N/A                |
| Bound imported   | Pointer        | ValueTag           |
| Local (any)      | Pointer        | WrtTag (writer)    |

So after binding, an imported reader points DIRECTLY to a freshly allocated `ValueTag`
cell — unlike a local reader which points to its paired writer. This structural marker
(Pointer→ValueTag) lets `derefAddr()` retrieve the bound value without any `V_p` lookup.

**Binding an imported reader to a value received from the creator** — the heap-transform
analogue of "the binding arrived over the wire":

```dart
List<GoalRef> bindImportedReader(int readerAddr, Term value, VariableEntry entry) {
  // ... tag/content guards ...
  final activations = <GoalRef>[];
  if (entry.suspensions != null) {
    _walkAndActivate(entry.suspensions!, activations);  // resume waiters
  }
  final valueCellAddr = HP++;                            // allocate value cell
  cells.add(HeapCell(value, CellTag.ValueTag));
  cell.content = Pointer(valueCellAddr);                 // reader now → ValueTag
  return activations;
}
```

Comment in source (spec §5.3 imported-reader case): "For imported readers, V_p serves as the
'virtual writer' that holds suspensions. When an assignment arrives, goals are resumed from
VariableEntry.suspensions." This is precisely the seam a remote link primitive plugs into:
the "assignment arrives" event is currently an in-process call; the link layer makes it a
transport-delivered event that calls `bindImportedReader`.

---

## 3. Dereference (three-valued-aware, cycle-guarded, WxW-detecting)

`derefAddr(int startAddr) -> Object` returns one of:
- `Term` (bound to ground / structure),
- `VarRef` (unbound LOCAL writer),
- `VariableEntry` (unbound IMPORTED writer/reader).

Key load-bearing behaviours:
- **Cycle guard / SRSW:** visited-set; `throw StateError('Cycle detected ... SRSW violation!')`.
- **WxW detection during deref:** if a pointer is followed FROM a writer and lands ON another
  writer → `throw StateError('SRSW violation: writer at X points to writer at Y')`.
- **Reader cell (`RoTag`):** if content is `VariableEntry` → imported; return its `boundValue`
  if cached else the entry. If content is `Pointer` → follow to writer.
- **Writer cell (`WrtTag`):** `VariableEntry` → imported (cached value or entry);
  `WriterContent` → unbound-with-suspensions → return `VarRef(current)`; `Pointer` → if it is
  the bidirectional pair (target reader points back) → unbound → `VarRef(current)`, else
  follow the chain.
- **Value cell (`ValueTag`):** return the `Term`.

---

## 4. Binding — writer-MGU realized (binds writers only; WxW forbidden)

This module enforces the writer-MGU invariant directly: writers may be bound to a ground
value or forwarded to another variable's READER; binding a writer to a writer THROWS.

**Bind writer to ground value** (`bindWriter` → `bindWriterWithCallbackControl`):
- guard `tag == WrtTag` else `throw 'bindWriter called on non-writer cell'`;
- if content is `WriterContent`, `_walkAndActivate(wc.suspensions, activations)` BEFORE
  overwriting (reactivation);
- set `cell.content = value; cell.tag = ValueTag;`
- optionally fire a registered `_bindCallbacks[writerAddr]` (Phase-0 external observation /
  I/O seam — also load-bearing for a link: a writer-bind can notify a remote peer).
- `bindWriterNoCallback` defers callbacks so nested `VarRef`s in a structure deref correctly
  (used by `applySigmaHatFCP`); `firePendingCallback` flushes deferred ones.

**Bind writer to (another variable's) reader** — variable chaining + suspension forwarding:

```dart
List<GoalRef> bindWriterToReader(int writerAddr, int readerAddr) {
  // guards: writerAddr is WrtTag; readerAddr is RoTag
  final targetWriterAddr = tryWriterForReader(readerAddr);
  if (targetWriterAddr == null) {
    throw StateError('bindWriterToReader target ... is an imported reader (no local writer)');
  }
  if (writerCell.content is WriterContent) {
    _forwardSuspensions(wc.suspensions, targetWriterAddr);   // forward waiters
  }
  writerCell.content = Pointer(readerAddr);                  // chain; tag stays WrtTag
  // forward external callback to the target writer too
  return activations;
}
```

**WxW (writer-to-writer) — FORBIDDEN, throws** (spec §5.2):

```dart
void bindWriterToWriter(int w1, int w2) {
  throw StateError('WxW violation: cannot bind writer $w1 to writer $w2');
}
```

`bindVariable` dispatches: a `VarRef` target that is a reader → `bindWriterToReader`; a
writer target → `bindWriterToWriter` (throws); otherwise → `bindWriter` (ground value).

---

## 5. Suspension & reactivation

- `suspendOnWriter(writerAddr, record)`: wraps `record` in a `SuspensionListNode`; converts
  the writer's `Pointer` into a `WriterContent(readerAddr, node)` on the FIRST suspension (so
  the paired-reader pointer is preserved), or prepends to the existing `WriterContent` list.
- `suspendOnReader(readerAddr, record)`: if the reader holds a `VariableEntry` (IMPORTED), the
  suspension is stored in `entry.suspensions` (the "virtual writer"); otherwise it follows the
  reader's `Pointer` to the writer and calls `suspendOnWriter`.
- `_forwardSuspensions(list, targetWriterAddr)`: copies ARMED suspension records onto the
  target writer (creating/extending its `WriterContent`); skips disarmed ones and bound targets.
- `_walkAndActivate(list, activations)`: for each ARMED node, append
  `GoalRef(goalId, resumePC)` and `record.disarm()`.

The reactivation product is a `List<GoalRef>` returned from every binding call — the scheduler
consumes these to re-run suspended goals. For a distributed link, the remote "writer bound"
event must produce the same `GoalRef` reactivations locally.

---

## 6. Reader/value accessors & high-level API

- `isFullyBound(writerAddr)`: `derefAddr` result is neither `VarRef` nor `VariableEntry`.
- `getValue` / `valueOfWriter`: dereferenced `Term?` (null if unbound).
- `dereference(Term)`: dereferences a `VarRef`; returns original for imported-unbound.
- `isReaderBound` / `getReaderValue`: work for LOCAL (paired-writer fully bound) AND IMPORTED
  (Pointer→ValueTag) readers.
- `onBind` / `removeBindCallback`: the external-observer (I/O) hook keyed by `writerAddr`.
- `storeTermOnHeap(Term)`: heap-only argument-register helper (spec v2.16.3) — recursively
  heap-allocates `ConstTerm` / `StructTerm` (args become `VarRef`s), passes through `VarRef`,
  and stores `MutualRefTerm` / `ModuleTerm` as opaque `ValueTag` values.

---

## 7. Why this matters for the link layer (B2 fidelity notes)

1. **The split target is the writer/reader pair.** The atomic unit a link primitive must
   distribute is exactly the `(writerAddr, readerAddr)` cell pair. A remote link replaces the
   in-heap `Pointer` between the two halves with a transport.
2. **The imported-variable path is the existing remote seam.** `allocateImportedReader` /
   `allocateImportedWriter` + `VariableEntry` + `bindImportedReader` already model "the other
   half lives elsewhere; suspensions accrue locally; an arriving assignment resumes them."
   A multi-protocol link should drive these existing entry points, not parallel ones — this is
   what keeps the split program behaving like the original (transparency goal).
3. **Writer-MGU & WxW are enforced in code, not just spec.** `bindWriterToWriter` throws; deref
   throws on writer→writer. Any distributed unification scheme MUST preserve "bind writers
   only, never writer-to-writer" end to end; a remote binding that could connect two writers
   violates the model.
4. **Reactivation is a `GoalRef` list, locally consumed.** Cross-instance binding delivery must
   reconstruct the same `_walkAndActivate` reactivations on the receiving instance.
5. **The callback hook (`_bindCallbacks`) is a natural notify-on-bind transport seam** for the
   writer→peer direction (its comment calls it "external observation (Phase 0 I/O)").
6. **Caveat — current imports are point-to-point and single-binding.** `bindImportedReader`
   allocates ONE value cell and points the reader at it; there is no multi-reader fan-out. This
   aligns with SRSW and is directly relevant to open sub-question T2 (BLE LE-Audio BIS one-to-many
   broadcast vs SRSW single-reader) — the current model has no native multi-reader path.

---

## Verbatim load-bearing quotes

- WxW guard: `throw StateError('WxW violation: cannot bind writer $w1 to writer $w2');`
- Deref WxW: `throw StateError('SRSW violation: writer at ... points to writer at $current');`
- Cycle guard: `throw StateError('Cycle detected at address $current - SRSW violation!');`
- Allocation: `cells.add(HeapCell(Pointer(readerAddr), CellTag.WrtTag));` /
  `cells.add(HeapCell(Pointer(writerAddr), CellTag.RoTag));`
- Imported-reader semantics (source comment): "For imported readers, V_p serves as the
  'virtual writer' that holds suspensions. When an assignment arrives, goals are resumed from
  VariableEntry.suspensions."
- `bindImportedReader` transform (source comment):
  `cells[readerAddr] = HeapCell(Pointer(valueCellAddr), CellTag.RoTag)` /
  `cells[valueCellAddr] = HeapCell(value, CellTag.ValueTag)`.

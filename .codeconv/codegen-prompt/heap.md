```yaml
generated_at: '2026-06-03T00:00:00Z'
metric_score: null
model: claude-in-session
optimizer: seed-authored
provenance_note: >-
  Authored seed for the `heap` subsystem (lib/runtime/heap_fcp*), descended
  from _base.md. Idioms from the 2026-05-28 bulk drive (heap_fcp CellTag ->
  HeapCellTag resolution, commit 3a18e6f3) + DISCIPLINE §1.13 FCP reference.
schema_version: 1
seed_from: _base.md
source: bulk-drive-idioms
subsystem: heap
```

Convert the FCP heap core (`lib/runtime/heap_fcp*`) to real, compilable
C#/.NET 10. Emit REAL C# ONLY. Honor the shared base discipline (read built dep
APIs; `getX`→`LookupX`; keep `*Error` names; escalate-don't-guess).

## FCP is the reference architecture (DISCIPLINE §1.13)

The heap follows the original FCP flat-concurrent-Prolog architecture. Preserve
it exactly — do not reinvent heap mechanisms:

- bidirectional writer/reader variable pairs (writer↔reader both directions);
- tag-based dispatch on the cell tag;
- dereference with path compression;
- suspension on unbound readers; reactivation on writer bind.

## The `HeapCellTag` rename (flat-namespace disambiguation — load-bearing)

`cells.cs` defines `enum CellTag { writer, reader }` and `heap_fcp.cs` defines
its own `enum CellTag { WrtTag, RoTag, ValueTag }` — two SEPARATE Dart enums
(one per library) that collide ONLY in C#'s flat namespace. Resolution
(commit 3a18e6f3): the heap-layer enum is **`HeapCellTag`** (NOT `CellTag`).
Emit `heap_fcp.cs`'s enum as `HeapCellTag { WrtTag, RoTag, ValueTag }` and use
`HeapCellTag.*` at every heap-layer site. Leave `cells.cs`'s `CellTag`
untouched. Downstream heap consumers (commit.cs, suspend_ops.cs, …) reference
`HeapCellTag.*`.

## Confirmed public surface (the runner + commit layers depend on this)

`IsWriter/IsReader/IsValue/IsWriterBound/IsReaderBound/IsFullyBound/IsBound/`
`ValueOfWriter/GetReaderValue/GetValue/DerefAddr(int)→object/`
`Dereference(Term)→Term/PairedReaderAddr(int)→int/TryWriterForReader(int)→int?/`
`AllocateVariable()→(int Writer,int Reader)/`
`BindWriterConst(int,object?)→List<GoalRef>/`
`BindWriterStruct(int,string,List<Term>)→List<GoalRef>`. The writer-MGU
contract holds: bind only writers, never readers; never writer-to-writer.

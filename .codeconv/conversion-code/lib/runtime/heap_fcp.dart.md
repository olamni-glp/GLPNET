### E1: Conflicting CellTag definitions in cells.cs vs heap_fcp.cs

- **Kind**: undecidable
- **File(s)**: `out/csharp/lib/runtime/cells.cs` (already built, defines `CellTag { writer, reader }`), `out/csharp/lib/runtime/heap_fcp.cs` (defines `CellTag { WrtTag, RoTag, ValueTag }`, used ~30 times throughout the file)
- **Detail**: `cells.cs` was generated with a 2-member lowercase `CellTag`; `heap_fcp.cs` requires a 3-member PascalCase `CellTag` (`WrtTag`, `RoTag`, `ValueTag`) that matches the heap-pointer-architecture-spec. The two enums are structurally incompatible — neither is a superset of the other, and `heap_fcp.cs` does not use the `writer`/`reader` member names at all.
- **Needs**: Decision on which `CellTag` is authoritative for the namespace. Options: (A) fix `cells.cs` to use the 3-member spec-aligned enum and replace `writer`/`reader` usages there with `WrtTag`/`RoTag`; (B) rename one enum (e.g. `CellTag2` in `cells.cs`) to avoid the collision and keep both; (C) delete `cells.cs`'s `CellTag` and update its `WriterCell`/`ReaderCell` to use `heap_fcp.cs`'s 3-member enum. Option A is recommended — `heap_fcp.cs`'s enum matches the spec and is used by the load-bearing heap implementation.
- **Status**: open

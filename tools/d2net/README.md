# d2net — Dart-to-.NET Conversion Toolkit

`d2net-scaffold` is the bootstrap step of the `d2net` toolkit. It walks a Dart
source tree (e.g. `glp_runtime`) and produces a parallel target tree (e.g.
`glp_runtime_net`) that is ready for porting to .NET, with one preserved
`.dart.src` per Dart file plus nine companion stub files (`.cs`, `.ana`,
`.tst`, `.con`, `.dep`, `.cgn`, `.iss`, `.sta`, `.ver`) and a
`d2net-tracker.json` inventory at the target root.

See [specs/001-d2net-scaffold/](../../specs/001-d2net-scaffold/) for the full
specification, plan, contracts, and tasks.

## Build

```powershell
dotnet build D:\BSTDEV\RESEARCH\glp\glpnet\tools\d2net\D2Net.sln
```

## Run a fresh scaffold

```powershell
dotnet run --project D:\BSTDEV\RESEARCH\glp\glpnet\tools\d2net\src\D2Net.Scaffold -- `
    D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime `
    D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net
```

## Refresh after editing source

```powershell
dotnet run --project D:\BSTDEV\RESEARCH\glp\glpnet\tools\d2net\src\D2Net.Scaffold -- `
    D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime `
    D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net `
    --refresh
```

`--refresh` rewrites `.dart.src` and non-Dart files from the current source,
preserves all companion files (`.cs`, `.ana`, …) and the tracker, and reports
any newly-discovered Dart files for manual tracker updates.

## Test

```powershell
dotnet test D:\BSTDEV\RESEARCH\glp\glpnet\tools\d2net\D2Net.sln
```

## Observed scaffold counts (T039 baseline, 2026-04-30)

First end-to-end run against `glp_runtime`: 52 directories, 127 non-Dart files,
193 `.dart.src` files, 1737 companion stubs (= 193 × 9), 193 tracker records.
Wall-clock 11.6 s — well under the 30 s SC-007 budget.

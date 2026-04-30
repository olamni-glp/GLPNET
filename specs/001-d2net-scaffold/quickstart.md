# Quickstart — d2net-scaffold

This is the operator's eye view: the smallest set of commands needed to scaffold `glp_runtime_net` from `glp_runtime` and to verify the result.

## Prerequisites

- .NET 8 SDK installed (`dotnet --version` returns `8.x`).
- Repository cloned at `D:\BSTDEV\RESEARCH\glp\glpnet`.
- `glp_runtime` exists at `D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime`.
- `glp_runtime_net` does NOT exist yet (or you are running with `--refresh`).

## Build the toolkit

```powershell
dotnet build D:\BSTDEV\RESEARCH\glp\glpnet\tools\d2net\D2Net.sln
```

## Run a fresh scaffold

```powershell
dotnet run --project D:\BSTDEV\RESEARCH\glp\glpnet\tools\d2net\src\D2Net.Scaffold -- `
    D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime `
    D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net
```

Expected outcome (a 5-second smoke check):

1. Exit code 0.
2. Stdout summary block (see `contracts/cli-contract.md`).
3. `D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net` exists.
4. `D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net\d2net-tracker.json` parses as JSON, top level is an array, length matches the count of `.dart` files outside pruned directories.

Sanity check from PowerShell:

```powershell
$count = (Get-ChildItem D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime -Filter *.dart -Recurse -File `
    | Where-Object { $_.FullName -notmatch '\\(\.dart_tool|build|\.git|\.idea|\.vscode)\\' }).Count

$tracker = Get-Content D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net\d2net-tracker.json -Raw `
    | ConvertFrom-Json

"Dart files in source: $count, tracker records: $($tracker.Count)"
```

The two counts must match.

## Run tests

```powershell
dotnet test D:\BSTDEV\RESEARCH\glp\glpnet\tools\d2net\D2Net.sln
```

All xUnit tests should pass. The integration tests build their own throwaway source trees in `Path.GetTempPath()` and assert on the produced target tree, so they do not require `glp_runtime` to be in any particular state.

## Clean and re-run (default mode)

If you want to redo a fresh scaffold:

```powershell
Remove-Item -Recurse -Force D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net
dotnet run --project D:\BSTDEV\RESEARCH\glp\glpnet\tools\d2net\src\D2Net.Scaffold -- `
    D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime `
    D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net
```

Without the delete, the second run will exit with code 3 ("target already exists") — by design.

## Refresh after editing source

When you edit `glp_runtime` `.dart` files and want the `.dart.src` mirrors and any added non-Dart files refreshed without losing your in-progress `.cs` work or your tracker progress:

```powershell
dotnet run --project D:\BSTDEV\RESEARCH\glp\glpnet\tools\d2net\src\D2Net.Scaffold -- `
    D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime `
    D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net `
    --refresh
```

The summary will list any newly-discovered Dart files for which companion stubs were freshly created but no tracker entry was added — update `d2net-tracker.json` manually for those.

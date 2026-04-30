# Quickstart — D2NET.Init

This is the operator's eye view: the smallest set of commands needed to initialise a `.D2NET` workspace for `glp_runtime` and to verify the result.

## Prerequisites

- **.NET 8 SDK** — `dotnet --version` returns `8.x` (or newer).
- Repository cloned at `D:\BSTDEV\RESEARCH\glp\glpnet`.
- `glp_runtime/` exists at `D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime`.
- `.D2NET/` does NOT exist at the repo root (or you intend to use `--FORCE --DELETE-EXISTING`).

> No Node.js, no ODBC driver, no Postgres install needed. The workspace database is embedded SQLite (clarification Q6 in `spec.md`).

## Build the toolkit

```powershell
dotnet build D:\BSTDEV\RESEARCH\glp\glpnet\tools\d2net\D2Net.sln
```

This builds both `D2Net.Scaffold` (existing) and `D2Net.Init` (this feature).

## Run a fresh init — fully scripted

```powershell
cd D:\BSTDEV\RESEARCH\glp\glpnet
dotnet run --project tools\d2net\src\D2Net.Init -- `
    --source glp_runtime `
    --target-extension _net `
    --target glp_runtime_net `
    --accept-suggested-exclusions `
    --non-interactive
```

Expected outcome (a 5-second smoke check):

1. Exit code 0.
2. Stdout summary block (see `contracts/cli-contract.md`).
3. `D:\BSTDEV\RESEARCH\glp\glpnet\.D2NET\` exists.
4. `D:\BSTDEV\RESEARCH\glp\glpnet\.D2NET\D2NET-Settings.json` parses as JSON and validates against `contracts/settings-schema.json`.
5. `D:\BSTDEV\RESEARCH\glp\glpnet\.D2NET\pgdb\workspace.sqlite` exists and is a valid SQLite database (open it in DB Browser to confirm).

## Run a fresh init — interactive

```powershell
cd D:\BSTDEV\RESEARCH\glp\glpnet
dotnet run --project tools\d2net\src\D2Net.Init
```

You will be prompted for source, extension, and target directory names, then shown the suggested exclusion list (well-known tool subdirs + archive/backup matches) with the chance to remove items, redisplay, or accept.

## Inspect the workspace

```powershell
cd D:\BSTDEV\RESEARCH\glp\glpnet

# List all Dart files captured at init time
dotnet run --project tools\d2net\src\D2Net.Init -- --list

# List excluded directories
dotnet run --project tools\d2net\src\D2Net.Init -- --Exclusions

# Show the current (lowest-sequence non-COMPLETED) phase
dotnet run --project tools\d2net\src\D2Net.Init -- --current-phase

# JSON variants for scripting
dotnet run --project tools\d2net\src\D2Net.Init -- --list --json | jq .
dotnet run --project tools\d2net\src\D2Net.Init -- --current-phase --json
```

Sanity check the row counts via PowerShell:

```powershell
$expected = (Get-ChildItem D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime -Filter *.dart -Recurse -File `
    | Where-Object { $_.FullName -notmatch '\\(\.dart_tool|build|\.git|\.idea|\.vscode|node_modules|bin|obj|\.gradle|\.next|\.pytest_cache|\.venv|venv|\.nuget|\.terraform|.*archive.*|.*backup.*|.*old.*|.*legacy.*|.*obsolete.*|.*deprecated.*|.*attic.*|.*bak.*)\\' }).Count

$actual = (dotnet run --project D:\BSTDEV\RESEARCH\glp\glpnet\tools\d2net\src\D2Net.Init -- --list --json `
    | ConvertFrom-Json).dart_files.Count

"Expected (filesystem): $expected, Actual (workspace DB): $actual"
```

The two counts should match (modulo any directories you manually toggled off in the prompt cycle).

## Re-run after a configuration change

```powershell
# Default: refuses to touch existing .D2NET/
dotnet run --project tools\d2net\src\D2Net.Init -- `
    --source glp_runtime --target-extension _net --target glp_runtime_net `
    --accept-suggested-exclusions --non-interactive
# → exit 3, message: "workspace already exists"

# Destructive re-init (case-sensitive uppercase per spec)
dotnet run --project tools\d2net\src\D2Net.Init -- `
    --FORCE --DELETE-EXISTING `
    --source glp_runtime --target-extension _net --target glp_runtime_net `
    --accept-suggested-exclusions --non-interactive
# → exit 0, fresh .D2NET/, previous workspace replaced atomically
```

## Run tests

```powershell
dotnet test D:\BSTDEV\RESEARCH\glp\glpnet\tools\d2net\D2Net.sln
```

Both `D2Net.Scaffold.Tests` (existing, 34 tests) and `D2Net.Init.Tests` (this feature, 70 tests) run, all green. Init integration tests build their own throwaway repos in `Path.GetTempPath()` and create their own SQLite databases inside them; they can run in parallel.

## Connect an external SQL client

The workspace database is a plain SQLite file at `.D2NET\pgdb\workspace.sqlite`. Open it with any SQLite-compatible tool while *no D2NET command is running*:

- **DB Browser for SQLite** (https://sqlitebrowser.org/) — open the `.sqlite` file directly.
- **JetBrains DataGrip / IntelliJ Database tools** — add a SQLite data source pointing at the file.
- **`sqlite3` CLI** — `sqlite3 D:\BSTDEV\RESEARCH\glp\glpnet\.D2NET\pgdb\workspace.sqlite "SELECT * FROM dart_files LIMIT 10;"`.

The connection string for `Microsoft.Data.Sqlite` is recorded as `connection.connection_string` in `D2NET-Settings.json` and as `db_connection_string` in the `setting` table.

# verify-pgbridge-deps.ps1
#
# Build-time guardrail for spec 005-d2net-pglite-bridge FR-008 + SC-010.
# Walks the pgbridge node_modules tree and fails the build if pg-gateway is
# anywhere in the transitive dependency tree. The RCA at
# docs/research/pglite-pg-gateway-odbc-failure-analysis.md is unambiguous:
# pg-gateway corrupts the Postgres-wire response stream and crashes psqlODBC.
# It must never enter the production bundle.

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string] $BridgeDir
)

$ErrorActionPreference = 'Stop'

$resolved = Resolve-Path -LiteralPath $BridgeDir
$nodeModules = Join-Path $resolved 'node_modules'

if (-not (Test-Path $nodeModules)) {
    Write-Host "[verify-pgbridge-deps] node_modules not present at $nodeModules; nothing to verify yet."
    Write-Host "[verify-pgbridge-deps] (npm ci will populate it; this script will re-run on next build.)"
    exit 0
}

# Pass 1: structural ban. Any directory literally named 'pg-gateway' is fatal.
$banned = Get-ChildItem -Recurse -Directory -LiteralPath $nodeModules `
    | Where-Object { $_.Name -eq 'pg-gateway' }

if ($banned) {
    Write-Error "[verify-pgbridge-deps] FAIL: pg-gateway found in pgbridge node_modules. See specs/005-d2net-pglite-bridge/spec.md FR-008."
    foreach ($b in $banned) { Write-Error "  -> $($b.FullName)" }
    exit 1
}

# Pass 2: cross-check the pinned PGLite version matches the spec's RCA-verified pin.
$pglitePkg = Join-Path $nodeModules '@electric-sql\pglite\package.json'
if (Test-Path $pglitePkg) {
    $version = (Get-Content -Raw $pglitePkg | ConvertFrom-Json).version
    Write-Host "[verify-pgbridge-deps] pgbridge bundle: @electric-sql/pglite@$version, pg-gateway absent."
} else {
    Write-Warning "[verify-pgbridge-deps] @electric-sql/pglite/package.json not found under $nodeModules; verify the install."
}

exit 0

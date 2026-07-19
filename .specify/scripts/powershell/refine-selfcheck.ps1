#!/usr/bin/env pwsh

# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT
# buildkit-file-id: 10c1f1c6-c6c2-4efe-afec-c6a3ffa6b999

# Refinement self-check (spec-007 FR-007/SC-004). Thin wrapper around the
# module CLI so it works the moment the wheel is installed — no dependency
# on the `buildkit` console script being on PATH.
#
# Resolve the project root from THIS script's location (an installer may
# invoke it from an arbitrary cwd) so the check reflects the intended repo.
#
# Exit: 0 ready, 0 degraded (baseline-only, still functional), 2 DB down.

[CmdletBinding()]
param([switch]$Help)

$ErrorActionPreference = 'Stop'

if ($Help) {
    Write-Output "Usage: ./refine-selfcheck.ps1   # prints 'refine: ready' | 'refine: degraded (...)'"
    exit 0
}

$dir = $PSScriptRoot
while ($dir -and -not (Test-Path (Join-Path $dir '.specify'))) {
    $parent = Split-Path $dir -Parent
    if ($parent -eq $dir) { $dir = $null; break }
    $dir = $parent
}
if ($dir) { $env:BUILDKIT_PROJECT_ROOT = $dir }

python -m buildkit_cli.refine selfcheck
exit $LASTEXITCODE

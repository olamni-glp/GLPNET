#!/usr/bin/env pwsh

# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT
# buildkit-file-id: 24cad5bc-a710-4847-8aa0-148d47e9b0dd

# Opt-in scheduled refinement trigger (spec-007 FR-018c). OFF by default:
# this is a no-op unless `.specify/refine.json` sets
# triggers.scheduled = true. Single-flight + fail-safe are enforced in the
# runner, so it is safe to wire into a Task Scheduler / cron entry.
#
# Task Scheduler / cron invoke this with an arbitrary working directory, so
# resolve the project root from THIS script's location (walk up to the dir
# containing `.specify`) and pin BUILDKIT_PROJECT_ROOT — otherwise the
# module would resolve the project from cwd and usually no-op as disabled
# (or target the wrong repo).
#
# Exit: 0 ran/disabled, 1 failure, 2 DB down.

[CmdletBinding()]
param([switch]$Help)

$ErrorActionPreference = 'Stop'

if ($Help) {
    Write-Output "Usage: ./refine-scheduled.ps1   # opt-in; off unless triggers.scheduled=true"
    exit 0
}

$dir = $PSScriptRoot
while ($dir -and -not (Test-Path (Join-Path $dir '.specify'))) {
    $parent = Split-Path $dir -Parent
    if ($parent -eq $dir) { $dir = $null; break }
    $dir = $parent
}
if ($dir) { $env:BUILDKIT_PROJECT_ROOT = $dir }

python -m buildkit_cli.refine scheduled
exit $LASTEXITCODE

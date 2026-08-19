# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT

<#
.SYNOPSIS
  Post-reboot lane relaunch for host GAVRIELLA — two Windows Terminal windows, 12 repo-lane tabs.

.DESCRIPTION
  Interim, hand-rolled stand-in for the NOT-YET-EXISTING /bk-onrestart capability
  (verified absent 2026-08-19: no skill among the 62 installed, no CLI on PATH).
  The durable replacement is roadmap feature `bk-onrestart` — this script is the
  executable specification for it, not the permanent answer.

  Window 1 (7 tabs): ospark, tefl, hatzinor, olamnit, buildkit, qhstate, yngenios
  Window 2 (5 tabs): crucible, glpnet, lejepa, mstack, yngwin

  Every repo path below was VERIFIED to exist and to be a git repo on 2026-08-19,
  and each ambiguous name was disambiguated by last-commit recency, not by guessing:
    ospark  -> D:\BSTDEV\db\ospark          (3 candidates; this one committed 2026-08-19 10:59)
    yngwin  -> D:\yngenios\yngenios-windows (3 candidates; this one committed 2026-08-19 12:30)
    yngenios-> D:\BSTDEV\research\yngenios  (3 candidates; this one committed 2026-08-19 12:17)

.PARAMETER StartClaude
  Also start `claude` in each tab. Default OFF: 12 concurrent agent sessions is a
  deliberate act, not a side effect of rebooting.

.EXAMPLE
  pwsh -File scripts\onrestart-launch.ps1
  pwsh -File scripts\onrestart-launch.ps1 -StartClaude
#>
[CmdletBinding()]
param(
    [switch]$StartClaude,
    [switch]$WhatIfOnly
)

$ErrorActionPreference = 'Stop'

# The GAVRIELLA shell prelude. Nothing dev-related is on the persisted PATH on this
# host, so every lane must prepend it or node/git/dart/dotnet are simply absent.
$Prelude = @'
$env:PATH = "$env:USERPROFILE\.dotnet;$env:USERPROFILE\.local\bin;$env:USERPROFILE\erlang-otp-29\bin;$env:USERPROFILE\dart-sdk\bin;C:\Program Files\nodejs;C:\Program Files\Git\cmd;C:\Program Files\Git\bin;$env:PATH"
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
$env:PYTHONUTF8 = 1
'@

$Window1 = [ordered]@{
    'ospark'   = 'D:\BSTDEV\db\ospark'
    'tefl'     = 'D:\BSTDEV\lang\TEFL'
    'hatzinor' = 'D:\BSTDEV\lang\hatzinor'
    'olamnit'  = 'D:\BSTDEV\research\olamnit'
    'buildkit' = 'D:\BSTDEV\research\buildkit'
    'qhstate'  = 'D:\BSTDEV\research\qhstate'
    'yngenios' = 'D:\BSTDEV\research\yngenios'
}

$Window2 = [ordered]@{
    'crucible' = 'D:\BSTDEV\research\crucible'
    'glpnet'   = 'D:\BSTDEV\research\GLP\GLPNET'
    'lejepa'   = 'D:\BSTDEV\research\LeJEPA'
    'mstack'   = 'D:\BSTDEV\tools\MSTACK'
    'yngwin'   = 'D:\yngenios\yngenios-windows'
}

function Test-Lanes {
    param([System.Collections.Specialized.OrderedDictionary]$Lanes, [string]$Label)
    $bad = @()
    foreach ($name in $Lanes.Keys) {
        $p = $Lanes[$name]
        if (-not (Test-Path -LiteralPath (Join-Path $p '.git'))) { $bad += "$name -> $p" }
    }
    if ($bad.Count) {
        # Refuse loudly rather than open half a window of broken tabs. A launcher that
        # silently skips a missing lane is the same silent-success defect this fleet
        # is trying to eliminate.
        throw "$Label : not a git repo: $($bad -join '; ')"
    }
    Write-Host ("  {0}: {1}/{1} lanes verified" -f $Label, $Lanes.Count) -ForegroundColor Green
}

function Get-TabCommand {
    param([string]$Path)
    $body = $Prelude
    if ($StartClaude) { $body += "`nclaude" }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($body))
    return @('pwsh.exe', '-NoExit', '-EncodedCommand', $encoded)
}

function Start-LaneWindow {
    param([System.Collections.Specialized.OrderedDictionary]$Lanes, [string]$WindowId)
    $args = @('-w', $WindowId)
    $first = $true
    foreach ($name in $Lanes.Keys) {
        if (-not $first) { $args += ';' }
        $args += @('new-tab', '--title', $name, '-d', $Lanes[$name])
        $args += (Get-TabCommand -Path $Lanes[$name])
        $first = $false
    }
    if ($WhatIfOnly) {
        Write-Host "wt $($args -join ' ')" -ForegroundColor DarkGray
        return
    }
    & wt.exe @args
}

Write-Host "onrestart-launch — host $env:COMPUTERNAME" -ForegroundColor Cyan
if ($env:COMPUTERNAME -ne 'Gavriella') {
    Write-Warning "These paths were verified on GAVRIELLA. On $env:COMPUTERNAME they are unverified — stop and re-resolve before trusting them."
}

Test-Lanes -Lanes $Window1 -Label 'window-1'
Test-Lanes -Lanes $Window2 -Label 'window-2'

Start-LaneWindow -Lanes $Window1 -WindowId 'lanes-1'
Start-Sleep -Milliseconds 900   # let the first window claim its name before the second is addressed
Start-LaneWindow -Lanes $Window2 -WindowId 'lanes-2'

Write-Host "launched: 2 windows, $($Window1.Count + $Window2.Count) tabs (StartClaude=$StartClaude)" -ForegroundColor Cyan

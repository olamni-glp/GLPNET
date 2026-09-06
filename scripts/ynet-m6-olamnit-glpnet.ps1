# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
#
# M6 receiver launcher for the olamnit-glpnet lane.
#
# THIS IS CONFIGURATION, NOT A CLIENT. The QHSM/QMSM YNET receiver is
# YngeniOS.Ynet.Client, shipped by ariellas.qhstate and ruled CANONICAL by Q-glpnetshiras-50.
# This lane is a CONTRIBUTOR. This file supplies only the three lane-specific facts the canonical
# client asks for -- lane id, node id, carrier root -- and starts it. If you find yourself adding
# behaviour here, you are writing the client the fleet has three times been told not to write.
#
# WHY THE BINARY LIVES OUTSIDE EVERY REPO
#     Engineer ruling, 2026-09-06. Running it from a session scratchpad meets M6 for one session
#     and loses it at the reboot -- which is measured instances 5, 7 and 8 of feature 108: a
#     completion signal that a restart undoes. Installing it into qhstate's own tree is the
#     "patched binary nobody else has" that R-C already refused, and the next rebuild there
#     reverts it. A versioned directory under the user's local app data is neither.
#
# TO REGISTER WITH bk-onrestart
#     This script is the entry point to call. It is deliberately NOT self-installing: creating a
#     scheduled task is a host-level change and bk-onrestart is mstack's mechanism, not this
#     lane's. Ask @mstack to add this line to the OLAMNIT resume sequence.

[CmdletBinding()]
param(
    [string]$Lane    = 'olamnit-glpnet',
    [string]$Node    = 'olamnit',
    [string]$Coop    = 'D:/coop',
    [string]$Repo    = 'D:\BSTDEV\research\glp\GLPNET',
    # Pin the version explicitly. An unpinned "latest" would change the running client under the
    # lane without anyone deciding to, and a client nobody chose is a client nobody can vouch for.
    [string]$Version = 'eea87e02',
    [switch]$Status
)

$ErrorActionPreference = 'Stop'
$home_ = Join-Path $env:LOCALAPPDATA 'yngenios\ynet-client'
$cli   = Join-Path $home_ "$Version\ynet-client.exe"

if (-not (Test-Path $cli)) {
    Write-Error @"
ynet-m6: canonical client not installed at $cli

Build and install it (nothing here is generated -- it is qhstate's source, built as-is):

  git -C D:\BSTDEV\research\qhstate fetch origin
  git -C D:\BSTDEV\research\qhstate worktree add --detach <tmp> origin/develop
  dotnet build -c Release <tmp>\Csharp\yngenios\YngeniOS.Ynet.Client.Cli\YngeniOS.Ynet.Client.Cli.csproj
  Copy-Item <tmp>\Csharp\yngenios\YngeniOS.Ynet.Client.Cli\bin\Release\net11.0\* '$home_\<version>\'

Do NOT build into qhstate's working tree: it sits on a feature branch with a live peer's WIP.
"@
    exit 1
}

if ($Status) {
    # Bare, never piped. Piping replaces `$?` with the pipe's status -- which is how a caller
    # loses an exit code, and the client's own banner says so.
    & $cli doctor --lane $Lane --node $Node --coop $Coop --json
    exit $LASTEXITCODE
}

$logDir = Join-Path $Repo '.specify\ynet'
New-Item -ItemType Directory -Force -Path (Join-Path $logDir $Lane) | Out-Null
$log = Join-Path $logDir "$Lane\daemon.log"

# LIVENESS IS ASKED FOR, NEVER INFERRED FROM PROCESS EXISTENCE.
#
# The first version of this guard used `Get-Process -Name ynet-client`, and it was WRONG in the
# way this feature exists to name. Measured on OLAMNIT 2026-09-06: two processes named
# `ynet-client` were running while `doctor` reported this lane `listening:false`, `m6_met:false`,
# `receiver_pid:null`. The guard would have refused to start the lane's receiver on the strength
# of somebody else's process. "A process with the right name exists" is not "my receiver is
# listening" -- that conflation is measured instance 2, and the guard committed it.
& $cli doctor --lane $Lane --node $Node --coop $Coop --json > "$env:TEMP\ynet-m6-precheck.json" 2>&1
if ($LASTEXITCODE -eq 0) {
    # Do NOT restart a healthy receiver. On this build a restart re-raises retained WAL entries
    # and CLOBBERS acknowledgements (feature 108, measured instance 8).
    Write-Host "ynet-m6: this lane's receiver is already listening (doctor exit 0). Not restarting."
    Write-Host "         A restart currently undoes acks on this build -- see docs/known-issues.md."
    exit 0
}

$p = Start-Process -FilePath $cli `
    -ArgumentList 'run', '--lane', $Lane, '--node', $Node, '--coop', $Coop `
    -WorkingDirectory $Repo `
    -RedirectStandardOutput $log -RedirectStandardError "$log.err" `
    -WindowStyle Hidden -PassThru

Start-Sleep -Seconds 2
if ($p.HasExited) {
    Write-Error "ynet-m6: client exited immediately (code $($p.ExitCode)). See $log.err"
    exit 1
}

# Liveness is PROVEN by asking the running process, never by the fact that Start-Process returned.
# "The process exists" is not "the receiver is listening" -- that conflation is measured instance 2.
& $cli doctor --lane $Lane --node $Node --coop $Coop --json > "$log.doctor.json"
$rc = $LASTEXITCODE
if ($rc -ne 0) {
    Write-Error "ynet-m6: started pid $($p.Id) but doctor reports NOT MET (exit $rc). See $log.doctor.json"
    exit $rc
}
Write-Host "ynet-m6: listening -- lane=$Lane node=$Node pid=$($p.Id) coop=$Coop version=$Version"
exit 0

# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT

<#
.SYNOPSIS
  Post-reboot lane relaunch — resumes every repo lane mid-thread, and installs itself to
  fire automatically 45 s after logon. Executable spec for the buildkit `bk-onrestart` feature.

.DESCRIPTION
  Mechanism copied from the mstack lane's reference implementation
  (`D:\BSTDEV\tools\MSTACK\scripts\fleet\post-reboot-restart.ps1`, ariellas/gavriella lane),
  which is the prototype this feature generalises. What is copied, and WHY each part exists:

  1. RESUME, DO NOT SUMMARISE.  `claude --continue` picks up the most recent conversation in
     the cwd mid-thread. `--autocompact 1000000` pushes auto-compaction — the only thing that
     would summarise a resumed session — as far out as the CLI allows. `--fork-session` is
     deliberately NEVER passed: it mints a new session id and continues a COPY, which is not
     continuing.

  2. THE SILENT-NEW-SESSION TRAP.  `--continue` does NOT error when there is no stored session
     for the cwd — it silently starts a BRAND NEW EMPTY one. After a reboot that is
     indistinguishable from a successful resume until you notice the context is gone. So every
     lane's session store is checked FIRST and a lane with no store, an empty store, or a store
     whose transcripts are all unreadable is REFUSED rather than resumed into nothing. AFTER
     launch the store is re-read: a transcript file that did not exist before launch is a
     silent new session and is reported as a FAILURE, never as a resume.

  3. THE BARE-SEMICOLON TRAP.  wt's command separator must reach wt as a BARE ';' token. A
     backtick-escaped '`;' arrives at wt literally and silently produces tabs that open and run
     NOTHING. Measured in the reference lane: 12 tabs, 0 claude processes. Every other token is
     quoted with Windows argv rules so lane names and paths containing spaces or quotes cannot
     be misparsed into the wt command sequence; the ';' separators are the only bare tokens.

  4. TWO DIFFERENT MOUNT WAITS.  Repo paths (local D:) are REQUIRED — wait, then refuse what is
     still missing. Network shares (I:, H:) are OPTIONAL — wait briefly, then LAUNCH ANYWAY.
     The shares live on \\192.168.0.108\GAVRI_D served by gavriella; in a fleet-wide reboot
     gavriella is down too, so blocking on them means the restart never runs on the hosts that
     are actually up. Sessions do not need the shares — every repo is local.

  5. ABSENT SHARE MEANS "I CANNOT SEE THE BOARD" — never "the board is empty", and never a
     finding about any host. Several buildkit tools silently fall back to a stale local root
     when the share is missing, and that husk answers every query with plausible wrong data.
     Three separate 2026-08 incidents came from that fallback.

  6. VERIFICATION IS PER LANE AND ATTRIBUTED — never a process count over a time window.
     A count of `claude` processes started "in the last two minutes" is satisfied by processes
     this invocation did not create, so it can report success while every requested tab ran
     nothing. Instead each lane is launched through a generated per-lane launcher that writes a
     HANDSHAKE MARKER recording its own PID before it execs claude, and verification requires,
     for every requested lane: the marker (proves the tab actually ran its command), a live
     claude process that is a DESCENDANT of that marker's PID and was created after launch
     (proves the process belongs to THIS lane and THIS invocation), and — unless -Fresh —
     resume evidence read from the lane's own session store. Lanes that were refused before
     launch stay in the denominator and are enumerated as failures.

  MEASURED AND REFUTED — do not "fix" path case in the lane table: Claude Code mangles the cwd
  into a store name case-preservingly, but Windows resolves the lookup case-insensitively, so
  case cannot split a lane's history. Tested and refuted in the reference lane.

.PARAMETER Install
  Register the at-logon Scheduled Task (45 s delay) for the CURRENT user on ANY host, then exit.
  Idempotent — re-running replaces the existing task.

.PARAMETER Uninstall
  Remove the Scheduled Task and exit.

.PARAMETER Layout
  TwoWindows (default) = window 1 gets the first group, window 2 the second.
  Tabs = one window, all lanes.  Windows = one window per lane.

.PARAMETER Fresh
  Start NEW sessions instead of resuming. Opt-in only, and it defeats the point.
  Resume evidence is not checked under -Fresh, because a new transcript is the intended result.

.PARAMETER AllowPartial
  Exit 0 even when one or more selected lanes were REFUSED before launch. Without it a refused
  lane fails the whole run, because "resume every lane" did not happen.

.PARAMETER AllowUnconfirmedResume
  Exit 0 for a lane whose claude process is attributed and live but whose session store showed
  no write within -VerifyTimeoutSec. Such a lane is always reported as UNCONFIRMED, never as
  resumed; this switch only decides whether it fails the run.

.EXAMPLE
  pwsh -File scripts\onrestart-launch.ps1 -Install      # arm it once; fires 45 s after every logon
  pwsh -File scripts\onrestart-launch.ps1 -DryRun       # validate, launch nothing
  pwsh -File scripts\onrestart-launch.ps1 -WaitForMounts
#>
[CmdletBinding()]
param(
    [switch]$Install,
    [switch]$Uninstall,
    [switch]$Register,
    [switch]$Unregister,
    [switch]$ShowConfig,
    [string]$Name,
    [int]$Group = 0,
    [string]$ConfigPath,
    [switch]$DryRun,
    [ValidateSet('TwoWindows', 'Tabs', 'Windows')][string]$Layout,
    [string[]]$Only,
    [ValidateSet('max', 'auto')][string]$AutoCompact = 'max',
    [switch]$Fresh,
    [switch]$WaitForMounts,
    [switch]$AllowPartial,
    [switch]$AllowUnconfirmedResume,
    [int]$DelaySeconds = 45,
    [int]$RepoWaitSec = 120,
    [int]$ShareWaitSec = 60,
    [int]$VerifyTimeoutSec = 45,
    [string[]]$OptionalMount = @('I:\coop', 'H:\coop', 'G:\coop')
)

$ErrorActionPreference = 'Stop'
$TaskName = 'BK-OnRestart'

# Exit codes — distinct so Scheduled Task history distinguishes the failure modes.
$EXIT_OK           = 0
$EXIT_NOTHING      = 1
$EXIT_BADARGS      = 2
$EXIT_NO_WT        = 3
$EXIT_NOT_LAUNCHED = 4   # a lane produced no attributable claude process
$EXIT_UNCONFIRMED  = 5   # launched and attributed, but no resume evidence
$EXIT_REFUSED      = 6   # a selected lane was refused before launch
$EXIT_SILENT_NEW   = 7   # a lane started a NEW session instead of continuing
$EXIT_NO_ATTRIB    = 8   # the process table is unavailable: cannot attribute, so cannot pass

# --- Configuration ---------------------------------------------------------------
# Host config lives OUTSIDE any repo, so a repo can be deleted or re-cloned without
# losing the machine's lane layout. Seeded on first run, then hand-editable, and
# extendable in place with -Register / -Unregister.
#
# layoutByHost maps a hostname to its window layout — the operator ruling is:
#   GAVRIELLA / GAVRI  -> TwoWindows (group 1 in window 1, group 2 in window 2)
#   OLAMNIT / ARIELLAS / SHIRAS -> Tabs (all lanes, one window)
# An unlisted host falls back to defaultLayout, and says so rather than guessing silently.
if (-not $ConfigPath) { $ConfigPath = Join-Path $env:USERPROFILE '.bk-onrestart\config.json' }

$SeedConfig = [ordered]@{
    schema_version = '1'
    defaultLayout  = 'Tabs'
    layoutByHost   = [ordered]@{
        'GAVRIELLA' = 'TwoWindows'; 'GAVRI' = 'TwoWindows'
        'OLAMNIT'   = 'Tabs'; 'ARIELLAS' = 'Tabs'; 'SHIRAS' = 'Tabs'
    }
    lanes = @(
        [ordered]@{ name='ospark';     group=1; path='D:\bstdev\db\ospark' }
        [ordered]@{ name='tefl';       group=1; path='D:\BSTDEV\LANG\tefl' }
        [ordered]@{ name='hatzinor';   group=1; path='D:\BSTDEV\LANG\hatzinor' }
        [ordered]@{ name='olamnit';    group=1; path='D:\BSTDEV\research\olamnit' }
        [ordered]@{ name='buildkit';   group=1; path='D:\BSTDEV\research\buildkit' }
        [ordered]@{ name='qhstate';    group=1; path='D:\BSTDEV\research\qhstate' }
        [ordered]@{ name='yngraw';     group=1; path='D:\bstdev\research\yngenios' }
        [ordered]@{ name='crucible';   group=2; path='D:\bstdev\research\crucible' }
        [ordered]@{ name='glpnet';     group=2; path='D:\bstdev\research\glp\glpnet' }
        [ordered]@{ name='lejepa';     group=2; path='D:\bstdev\research\lejepa' }
        [ordered]@{ name='mstack';     group=2; path='D:\bstdev\tools\mstack' }
        [ordered]@{ name='yngwin';     group=2; path='D:\YNGENIOS\yngenios-windows' }
    )
}

function Save-Config($cfg) {
    $dir = Split-Path -Parent $ConfigPath
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    ($cfg | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $ConfigPath -Encoding utf8
}

function Get-Config {
    if (-not (Test-Path -LiteralPath $ConfigPath)) {
        Save-Config $SeedConfig
        Write-Host "seeded lane config -> $ConfigPath" -ForegroundColor Green
    }
    Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
}

$Config = Get-Config
$Repos  = @($Config.lanes | ForEach-Object {
    [pscustomobject]@{ Name = $_.name; Group = [int]$_.group; Path = $_.path }
})

# Layout precedence: explicit -Layout > this host's entry > defaultLayout.
if (-not $Layout) {
    $hostKey = $env:COMPUTERNAME.ToUpperInvariant()
    $mapped  = $Config.layoutByHost.$hostKey
    if ($mapped) { $Layout = $mapped }
    else {
        $Layout = $Config.defaultLayout
        Write-Host "host '$hostKey' is not in layoutByHost - falling back to defaultLayout '$Layout'" -ForegroundColor Yellow
    }
}

$ProjectsRoot = Join-Path $env:USERPROFILE '.claude\projects'

# --- Native-argument quoting ------------------------------------------------------
# Start-Process joins an ArgumentList with spaces and does NOT preserve argument
# boundaries, so a lane name or path containing a space or a quote is re-parsed by
# wt.exe into extra tokens — and a crafted one can inject its own wt subcommands.
# Every token is quoted here with the CommandLineToArgvW rules and the command line is
# handed to Start-Process as ONE pre-quoted string. The ';' tab separators are the only
# tokens deliberately left bare (see .DESCRIPTION 3).
function ConvertTo-NativeArg([string]$Value) {
    if ($null -eq $Value) { $Value = '' }
    if ($Value -match '[\x00-\x1f]') { throw "control character in argument: '$Value'" }
    if ($Value -ne '' -and $Value -notmatch '[\s"]') { return $Value }
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.Append('"')
    $slashes = 0
    foreach ($ch in $Value.ToCharArray()) {
        if ($ch -eq '\') { $slashes++; continue }
        if ($ch -eq '"') {
            [void]$sb.Append('\' * ($slashes * 2 + 1)); [void]$sb.Append('"'); $slashes = 0; continue
        }
        if ($slashes -gt 0) { [void]$sb.Append('\' * $slashes); $slashes = 0 }
        [void]$sb.Append($ch)
    }
    if ($slashes -gt 0) { [void]$sb.Append('\' * ($slashes * 2)) }
    [void]$sb.Append('"')
    return $sb.ToString()
}

function Assert-SafeLaneValue([string]$Value, [string]$What, [string]$Lane) {
    if ([string]::IsNullOrWhiteSpace($Value)) { throw "lane '$Lane': $What is empty" }
    if ($Value -match '[\x00-\x1f]')          { throw "lane '$Lane': $What contains a control character" }
    if ($Value -match '"')                    { throw "lane '$Lane': $What contains a double quote" }
    if ($Value -match ';')                    { throw "lane '$Lane': $What contains ';', which is wt's tab separator" }
}

# Claude Code's cwd -> session-store mangle. Case-preserving by design; harmless (see .DESCRIPTION).
function ConvertTo-SessionDirName([string]$Path) { ($Path.TrimEnd('\') -replace '[:\\]', '-') }

# A transcript file is only evidence of a resumable conversation if it actually holds one.
# A zero-byte, truncated or unparseable *.jsonl walks straight into the silent-new-session
# trap, so the mere filename must never count as a resumable session.
function Test-ResumableSessionFile([System.IO.FileInfo]$File) {
    if (-not $File -or $File.Length -le 0) { return $false }
    try {
        $reader = [System.IO.StreamReader]::new($File.FullName)
        try {
            while (-not $reader.EndOfStream) {
                $line = $reader.ReadLine()
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                $null = $line | ConvertFrom-Json -ErrorAction Stop
                return $true
            }
        } finally { $reader.Dispose() }
    } catch { return $false }
    return $false
}

function Get-RepoState($Repo) {
    $dirName = ConvertTo-SessionDirName $Repo.Path
    $store   = Join-Path $ProjectsRoot $dirName
    $files   = @()
    if (Test-Path -LiteralPath $store) {
        $files = @(Get-ChildItem -LiteralPath $store -Filter '*.jsonl' -ErrorAction SilentlyContinue)
    }
    $usable  = @($files | Where-Object { Test-ResumableSessionFile $_ })
    $latest  = $usable | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    [pscustomobject]@{
        Name = $Repo.Name; Group = $Repo.Group; Path = $Repo.Path; SessionDir = $dirName
        Store = $store
        PathExists = (Test-Path -LiteralPath $Repo.Path)
        StoreExists = (Test-Path -LiteralPath $store)
        Sessions = $files.Count
        Usable = $usable.Count
        Latest = if ($latest) { $latest.LastWriteTime } else { $null }
        # Pre-launch fingerprint of the store, used after launch to prove resumption.
        FileNames = @($files | ForEach-Object { $_.Name })
        LatestPath = if ($latest) { $latest.FullName } else { $null }
        LatestLength = if ($latest) { $latest.Length } else { 0 }
        LatestWriteUtc = if ($latest) { $latest.LastWriteTimeUtc } else { [datetime]::MinValue }
    }
}

function Wait-ForPaths {
    param([string[]]$Paths, [int]$TimeoutSec, [string]$Label)
    if (-not $Paths) { return @() }
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $pending  = @($Paths | Where-Object { -not (Test-Path -LiteralPath $_) })
    while ($pending.Count -gt 0 -and (Get-Date) -lt $deadline) {
        Write-Host ("  waiting for {0} {1}: {2}" -f $pending.Count, $Label, ($pending -join ', ')) -ForegroundColor DarkGray
        Start-Sleep -Seconds 5
        $pending = @($pending | Where-Object { -not (Test-Path -LiteralPath $_) })
    }
    return , @($pending)
}

# --- Process attribution ----------------------------------------------------------
# The only honest way to say "this lane is running" is to point at a process THIS
# invocation created for THAT lane. Win32_Process supplies the parent link needed to walk
# from a lane's handshake PID down to its claude process. If it is unavailable we cannot
# attribute anything, and the run fails loudly rather than falling back to a count.
function Get-ProcTable {
    $table = @{}
    foreach ($p in Get-CimInstance -ClassName Win32_Process -ErrorAction Stop) {
        $table[[int]$p.ProcessId] = [pscustomobject]@{
            ProcId = [int]$p.ProcessId; ParentId = [int]$p.ParentProcessId
            Name = $p.Name; CommandLine = $p.CommandLine; Created = $p.CreationDate
        }
    }
    return $table
}

function Get-DescendantProcs($Table, [int]$RootPid) {
    $byParent = @{}
    foreach ($p in $Table.Values) {
        if (-not $byParent.ContainsKey($p.ParentId)) { $byParent[$p.ParentId] = [System.Collections.Generic.List[object]]::new() }
        $byParent[$p.ParentId].Add($p)
    }
    $out   = [System.Collections.Generic.List[object]]::new()
    $seen  = [System.Collections.Generic.HashSet[int]]::new()
    $queue = [System.Collections.Generic.Queue[int]]::new()
    $queue.Enqueue($RootPid)
    while ($queue.Count -gt 0) {
        $cur = $queue.Dequeue()
        if (-not $seen.Add($cur)) { continue }
        if (-not $byParent.ContainsKey($cur)) { continue }
        foreach ($child in $byParent[$cur]) {
            $out.Add($child); $queue.Enqueue($child.ProcId)
        }
    }
    return $out
}

function Test-IsClaudeProc($Proc) {
    if ($Proc.Name -and $Proc.Name -match '^claude') { return $true }
    if ($Proc.CommandLine -and $Proc.CommandLine -match '(?i)(^|[\\/"\s])claude(\.(exe|cmd|ps1|js))?([\\/"\s]|$)') { return $true }
    return $false
}

# --- Install / uninstall the at-logon trigger (portable to any host) -------------
function Install-Trigger {
    $self = $PSCommandPath
    if (-not $self) { throw "cannot resolve own path; run via 'pwsh -File <script> -Install'" }
    $pwsh = (Get-Process -Id $PID).Path        # the pwsh actually running us — portable
    $action  = New-ScheduledTaskAction -Execute $pwsh `
               -Argument ('-NoProfile -WindowStyle Hidden -File "{0}" -WaitForMounts' -f $self)
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
    $trigger.Delay = [System.Xml.XmlConvert]::ToString([TimeSpan]::FromSeconds($DelaySeconds))  # ISO-8601, e.g. PT45S
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
                -StartWhenAvailable -ExecutionTimeLimit ([TimeSpan]::FromMinutes(30))
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings `
        -Description "Resume all Claude Code repo lanes $DelaySeconds s after logon (buildkit bk-onrestart prototype)" `
        -Force | Out-Null
    Write-Host "installed scheduled task '$TaskName' on $env:COMPUTERNAME for $env:USERNAME" -ForegroundColor Green
    Write-Host "  fires: at logon + $DelaySeconds s delay" -ForegroundColor Green
    Write-Host "  runs : $pwsh -NoProfile -WindowStyle Hidden -File `"$self`" -WaitForMounts" -ForegroundColor DarkGray
    Write-Host "  verify: Get-ScheduledTask -TaskName $TaskName" -ForegroundColor DarkGray
}

function Uninstall-Trigger {
    if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
        Write-Host "removed scheduled task '$TaskName'" -ForegroundColor Green
    } else { Write-Host "no scheduled task '$TaskName' to remove" -ForegroundColor Yellow }
}

# --- register / unregister a lane from the window you are standing in -------------
# The point of -Register is that you capture a lane by BEING in it: cd into the repo,
# run it, and the lane is added with the correct path and session store already proven.
function Register-Lane {
    $path = (Get-Location).Path
    if (-not (Test-Path -LiteralPath (Join-Path $path '.git'))) {
        Write-Error "not a git repo: $path — refusing to register a lane that is not a repo."; exit $EXIT_BADARGS
    }
    $laneName = if ($Name) { $Name } else { Split-Path -Leaf $path }
    $laneGroup = if ($Group -gt 0) { $Group } else { 1 }
    # Refuse at registration what the launcher would otherwise have to refuse 45 s after logon.
    Assert-SafeLaneValue $laneName 'lane name' $laneName
    Assert-SafeLaneValue $path     'lane path' $laneName

    # Prove the lane is resumable NOW rather than discovering it at 45 s after next logon.
    $store = Join-Path $ProjectsRoot (ConvertTo-SessionDirName $path)
    $sess  = 0
    if (Test-Path -LiteralPath $store) {
        $sess = @(Get-ChildItem -LiteralPath $store -Filter '*.jsonl' -ErrorAction SilentlyContinue |
                  Where-Object { Test-ResumableSessionFile $_ }).Count
    }
    if ($sess -eq 0) { Write-Host "  warning: no usable stored session for this directory yet — it will be REFUSED at resume until Claude has run here." -ForegroundColor Yellow }

    $cfg = Get-Config
    $lanes = @($cfg.lanes | Where-Object { $_.name -ne $laneName -and $_.path -ne $path })
    if (@($cfg.lanes).Count -ne $lanes.Count) { Write-Host "  replacing existing entry for '$laneName'" -ForegroundColor DarkGray }
    $lanes += [pscustomobject]@{ name = $laneName; group = $laneGroup; path = $path }
    $cfg.lanes = $lanes
    Save-Config $cfg
    Write-Host "registered lane '$laneName' (group $laneGroup, $sess usable session(s)) -> $path" -ForegroundColor Green
    Write-Host "  config: $ConfigPath  |  lanes now: $(@($lanes).Count)" -ForegroundColor DarkGray
}

function Unregister-Lane {
    $laneName = if ($Name) { $Name } else { Split-Path -Leaf (Get-Location).Path }
    $cfg = Get-Config
    $before = @($cfg.lanes).Count
    $cfg.lanes = @($cfg.lanes | Where-Object { $_.name -ne $laneName })
    if (@($cfg.lanes).Count -eq $before) { Write-Host "no lane named '$laneName' in config — nothing removed." -ForegroundColor Yellow; exit $EXIT_NOTHING }
    Save-Config $cfg
    Write-Host "unregistered lane '$laneName'  |  lanes now: $(@($cfg.lanes).Count)" -ForegroundColor Green
}

function Show-Config {
    Write-Host "config : $ConfigPath" -ForegroundColor Cyan
    Write-Host "host   : $env:COMPUTERNAME  ->  layout '$Layout'" -ForegroundColor Cyan
    Write-Host "layoutByHost:" -ForegroundColor Cyan
    $Config.layoutByHost.PSObject.Properties | ForEach-Object { "    {0,-12} {1}" -f $_.Name, $_.Value } | Write-Host
    Write-Host "lanes ($(@($Repos).Count)):" -ForegroundColor Cyan
    $Repos | ForEach-Object { "    {0,-10} win {1}  {2}" -f $_.Name, $_.Group, $_.Path } | Write-Host
}

if ($Register)   { Register-Lane;     exit $EXIT_OK }
if ($Unregister) { Unregister-Lane;   exit $EXIT_OK }
if ($ShowConfig) { Show-Config;       exit $EXIT_OK }
if ($Install)    { Install-Trigger;   exit $EXIT_OK }
if ($Uninstall)  { Uninstall-Trigger; exit $EXIT_OK }

# --- Resolve + validate ---------------------------------------------------------
# Under `pwsh -File`, "-Only a,b" arrives as ONE string, so a comma list silently matches
# nothing. Split explicitly so -File and -Command behave alike.
if ($Only) { $Only = @($Only -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
$selected = @(if ($Only) { $Repos | Where-Object { $Only -contains $_.Name } } else { $Repos })
if (-not $selected) { Write-Error "No lanes matched -Only. Known: $($Repos.Name -join ', ')"; exit $EXIT_BADARGS }

foreach ($r in $selected) {
    try { Assert-SafeLaneValue $r.Name 'lane name' $r.Name; Assert-SafeLaneValue $r.Path 'lane path' $r.Name }
    catch { Write-Error "$_  (fix it in $ConfigPath)"; exit $EXIT_BADARGS }
}

Write-Host "bk-onrestart (prototype) — host $env:COMPUTERNAME" -ForegroundColor Cyan
if ($env:COMPUTERNAME -ne 'Gavriella') {
    Write-Warning "Lane paths were verified on GAVRIELLA. On $env:COMPUTERNAME they are unverified — re-resolve before trusting them."
}

$shareMissing = @()
if ($WaitForMounts) {
    Write-Host ''; Write-Host 'Waiting for mounts...' -ForegroundColor Cyan
    $repoMissing = Wait-ForPaths -Paths @($selected.Path) -TimeoutSec $RepoWaitSec -Label 'repo path(s)'
    if ($repoMissing.Count -gt 0) { Write-Host ("  {0} repo path(s) never appeared: {1}" -f $repoMissing.Count, ($repoMissing -join ', ')) -ForegroundColor Yellow }
    else { Write-Host '  all repo paths present.' -ForegroundColor Green }
    $shareMissing = Wait-ForPaths -Paths $OptionalMount -TimeoutSec $ShareWaitSec -Label 'network share(s)'
    if ($shareMissing.Count -eq 0) { Write-Host '  all network shares present.' -ForegroundColor Green }
}

$state = @($selected | ForEach-Object { Get-RepoState $_ })

Write-Host ''; Write-Host 'Lane resume plan' -ForegroundColor Cyan
Write-Host ('-' * 104)
'{0,-10} {1,-6} {2,-34} {3,-6} {4,-7} {5,-9} {6}' -f 'LANE','WINDOW','PATH','SESS','USABLE','RESUME?','LATEST SESSION' | Write-Host
Write-Host ('-' * 104)
foreach ($s in $state) {
    $verdict = if (-not $s.PathExists) { 'NO-PATH' } elseif (-not $s.StoreExists) { 'NO-STORE' }
               elseif ($s.Sessions -eq 0) { 'EMPTY' }
               elseif ($s.Usable -eq 0) { 'UNUSABLE' } else { 'yes' }
    $colour = if ($verdict -eq 'yes') { 'Green' } else { 'Red' }
    $when = if ($s.Latest) { $s.Latest.ToString('yyyy-MM-dd HH:mm') } else { '-' }
    '{0,-10} {1,-6} {2,-34} {3,-6} {4,-7} {5,-9} {6}' -f $s.Name,$s.Group,$s.Path,$s.Sessions,$s.Usable,$verdict,$when |
        Write-Host -ForegroundColor $colour
}
Write-Host ('-' * 104)

# A lane is launchable only if it has a session that can actually be continued. A *.jsonl
# that is zero-byte or unparseable is not one — counting it walks into the very trap this
# precheck exists to prevent.
$launchable = @($state | Where-Object { $_.PathExists -and $_.StoreExists -and $_.Usable -gt 0 })
$blocked    = @($state | Where-Object { -not ($_.PathExists -and $_.StoreExists -and $_.Usable -gt 0) })

if ($blocked.Count -gt 0) {
    Write-Host ''; Write-Host "REFUSING to resume $($blocked.Count) lane(s):" -ForegroundColor Yellow
    foreach ($b in $blocked) {
        $why = if (-not $b.PathExists) { 'path does not exist' }
               elseif (-not $b.StoreExists) { "no session store at ...\$($b.SessionDir)  <- Claude has never run in this directory" }
               elseif ($b.Sessions -eq 0) { 'session store is empty' }
               else { "all $($b.Sessions) transcript(s) are empty or unreadable — none can be continued" }
        Write-Host "  $($b.Name): $why" -ForegroundColor Yellow
    }
    Write-Host '  Resuming anyway would silently start a NEW empty session, which looks like success.' -ForegroundColor Yellow
    if (-not $AllowPartial) {
        Write-Host '  They stay in the denominator: the run FAILS unless -AllowPartial is given.' -ForegroundColor Yellow
    }
}

if ($Fresh) {
    Write-Host ''; Write-Host '-Fresh set: starting NEW sessions. This does NOT continue anything.' -ForegroundColor Yellow
    $launchable = @($state | Where-Object { $_.PathExists })
    $blocked    = @($state | Where-Object { -not $_.PathExists })
}

$claudeArgs = @()
if (-not $Fresh) { $claudeArgs += '--continue' }
if ($AutoCompact -eq 'max') { $claudeArgs += @('--autocompact', '1000000') }
$claudeCmd = (@('claude') + $claudeArgs) -join ' '

if ($shareMissing.Count -gt 0) {
    Write-Host ''
    Write-Host '################################################################' -ForegroundColor Red
    Write-Host ' NETWORK SHARE(S) NOT PRESENT - LAUNCHING ANYWAY (this is normal)' -ForegroundColor Red
    Write-Host '################################################################' -ForegroundColor Red
    foreach ($m in $shareMissing) { Write-Host "  absent: $m" -ForegroundColor Red }
    Write-Host ''
    Write-Host '  ABSENT SHARE MEANS "I CANNOT SEE THE BOARD".' -ForegroundColor Yellow
    Write-Host '  It does NOT mean the board is empty, and it is NOT a finding about any host.' -ForegroundColor Yellow
    Write-Host '  DO NOT let any tool fall back to a default/local sched root - that husk' -ForegroundColor Yellow
    Write-Host '  answers every query with plausible STALE data. Three 2026-08 incidents.' -ForegroundColor Yellow
    Write-Host '  Remap when gavriella is back:  net use I: \\192.168.0.108\GAVRI_D /persistent:yes' -ForegroundColor Cyan
    Write-Host ''
}

Write-Host ''
Write-Host "Requested   : $($selected.Count)"
Write-Host "Will launch : $($launchable.Count)"
Write-Host "Refused     : $($blocked.Count)"
Write-Host "Layout      : $Layout"
Write-Host "Command     : $claudeCmd"

if ($DryRun) { Write-Host ''; Write-Host 'DRY RUN - nothing launched.' -ForegroundColor Cyan; exit $EXIT_OK }
if ($launchable.Count -eq 0) { Write-Host 'Nothing to launch.' -ForegroundColor Yellow; exit $EXIT_NOTHING }

$wt = 'wt.exe'
if (-not (Get-Command $wt -ErrorAction SilentlyContinue)) {
    Write-Error "Windows Terminal (wt.exe) not found on PATH."; exit $EXIT_NO_WT
}

# Attribution must be possible BEFORE anything is launched — otherwise the run could only
# end in a count, which is exactly the check this script refuses to make.
try { $null = Get-ProcTable }
catch {
    Write-Error "cannot read the process table (Win32_Process): $_`nWithout it a launch cannot be attributed to a lane, and an unattributed count is not verification. Refusing to launch."
    exit $EXIT_NO_ATTRIB
}

# --- Per-lane launcher + handshake marker ----------------------------------------
# Each tab runs a generated script instead of an inline command, for two reasons:
#   * it writes a MARKER recording its own PID before exec'ing claude, which is what makes
#     the later verification attributable to THIS lane and THIS invocation;
#   * an inline command would need ';' to sequence, and ';' is wt's tab separator.
$runId  = '{0}-{1}' -f (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'), ([guid]::NewGuid().ToString('N').Substring(0, 6))
$RunDir = Join-Path $env:LOCALAPPDATA ('bk-onrestart\runs\' + $runId)
New-Item -ItemType Directory -Path $RunDir -Force | Out-Null

function New-LaneLauncher {
    param($Lane)
    $marker   = Join-Path $RunDir ('marker-{0}.json' -f $Lane.Name)
    $launcher = Join-Path $RunDir ('lane-{0}.ps1'    -f $Lane.Name)
    $qPath    = "'" + ($Lane.Path -replace "'", "''") + "'"
    $qMarker  = "'" + ($marker    -replace "'", "''") + "'"
    $qName    = "'" + ($Lane.Name -replace "'", "''") + "'"
    $body = @"
# generated by bk-onrestart run $runId — regenerated every launch; do not edit
`$ErrorActionPreference = 'Continue'
Set-Location -LiteralPath $qPath
try {
    [pscustomobject]@{
        lane       = $qName
        pwshPid    = `$PID
        path       = (Get-Location).Path
        runId      = '$runId'
        startedUtc = (Get-Date).ToUniversalTime().ToString('o')
    } | ConvertTo-Json -Compress | Set-Content -LiteralPath $qMarker -Encoding utf8
} catch {
    Write-Warning "bk-onrestart: could not write the handshake marker; this lane will report NOT-LAUNCHED even if claude starts. `$_"
}
& claude $($claudeArgs -join ' ')
"@
    Set-Content -LiteralPath $launcher -Value $body -Encoding utf8
    return [pscustomobject]@{
        Name = $Lane.Name; Path = $Lane.Path; Group = $Lane.Group
        LauncherPath = $launcher; MarkerPath = $marker; State = $Lane
    }
}

$plan = @($launchable | ForEach-Object { New-LaneLauncher $_ })

function Get-WtCommandLine {
    param([object[]]$Lanes, [string]$WindowId)
    $parts = [System.Collections.Generic.List[string]]::new()
    $parts.Add((ConvertTo-NativeArg '-w')); $parts.Add((ConvertTo-NativeArg $WindowId))
    $first = $true
    foreach ($l in $Lanes) {
        # BARE ';' — a quoted or backtick-escaped separator reaches wt literally and yields
        # tabs that open and run nothing. It is the ONLY token left unquoted.
        if (-not $first) { $parts.Add(';') }
        foreach ($t in @('new-tab', '--title', $l.Name, '-d', $l.Path, 'pwsh', '-NoExit', '-File', $l.LauncherPath)) {
            $parts.Add((ConvertTo-NativeArg $t))
        }
        $first = $false
    }
    return ($parts -join ' ')
}

function Start-LaneWindow {
    param([object[]]$Lanes, [string]$WindowId)
    Start-Process -FilePath $wt -ArgumentList (Get-WtCommandLine -Lanes $Lanes -WindowId $WindowId)
}

$launchStart = Get-Date

switch ($Layout) {
    'TwoWindows' {
        $g1 = @($plan | Where-Object { $_.Group -eq 1 })
        $g2 = @($plan | Where-Object { $_.Group -eq 2 })
        if ($g1.Count) { Start-LaneWindow -Lanes $g1 -WindowId 'lanes-1'; Start-Sleep -Milliseconds 900 }
        if ($g2.Count) { Start-LaneWindow -Lanes $g2 -WindowId 'lanes-2' }
        Write-Host "Launched 2 windows: $($g1.Count) + $($g2.Count) tabs." -ForegroundColor Green
    }
    'Tabs' { Start-LaneWindow -Lanes $plan -WindowId '0'; Write-Host "Launched 1 window with $($plan.Count) tabs." -ForegroundColor Green }
    'Windows' {
        foreach ($l in $plan) {
            Start-LaneWindow -Lanes @($l) -WindowId '-1'
            Start-Sleep -Milliseconds 400
        }
        Write-Host "Launched $($plan.Count) windows." -ForegroundColor Green
    }
}

# --- Verify PER LANE, by attribution — never by a count over a time window --------
# For every REQUESTED lane (refused ones included, so they stay in the denominator):
#   marker present   -> the tab actually ran its command (kills the 12-tabs-0-claude mode)
#   claude process   -> a live descendant of the marker's PID, created after launch
#   resume evidence  -> the store's pre-launch latest transcript grew, and NO transcript
#                       appeared that was absent before (a new one is a silent new session)
Write-Host ''
Write-Host "Verifying per lane (attributed, not counted) — up to $VerifyTimeoutSec s..." -ForegroundColor Cyan

$results = @{}
foreach ($s in $selected) {
    $results[$s.Name] = [pscustomobject]@{
        Name = $s.Name; Status = 'REFUSED'; Detail = 'refused before launch'; ClaudePid = $null
    }
}
foreach ($l in $plan) {
    $results[$l.Name] = [pscustomobject]@{
        Name = $l.Name; Status = 'NOT-LAUNCHED'
        Detail = 'no handshake marker: the tab opened but never ran its command'; ClaudePid = $null
    }
}

$deadline = (Get-Date).AddSeconds($VerifyTimeoutSec)
$pendingLanes = @($plan)
while ($pendingLanes.Count -gt 0 -and (Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3
    $table = Get-ProcTable
    $stillPending = [System.Collections.Generic.List[object]]::new()
    foreach ($l in $pendingLanes) {
        $r = $results[$l.Name]

        if (-not (Test-Path -LiteralPath $l.MarkerPath)) { $stillPending.Add($l); continue }
        $marker = $null
        try { $marker = Get-Content -LiteralPath $l.MarkerPath -Raw | ConvertFrom-Json } catch { $stillPending.Add($l); continue }
        $lanePid = [int]$marker.pwshPid

        # PID-reuse guard: the handshake PID must still be live AND have been created by us.
        $laneProc = $table[$lanePid]
        if (-not $laneProc -or ($laneProc.Created -and $laneProc.Created -lt $launchStart)) {
            $r.Status = 'NOT-LAUNCHED'
            $r.Detail = "handshake PID $lanePid is not a live process created by this launch"
            $stillPending.Add($l); continue
        }

        $claude = @(Get-DescendantProcs $table $lanePid | Where-Object {
            (Test-IsClaudeProc $_) -and (-not $_.Created -or $_.Created -ge $launchStart)
        })
        if ($claude.Count -eq 0) {
            $r.Status = 'NO-CLAUDE'
            $r.Detail = "tab ran (handshake PID $lanePid) but no claude process descends from it yet"
            $stillPending.Add($l); continue
        }

        $r.ClaudePid = $claude[0].ProcId

        if ($Fresh) {
            $r.Status = 'STARTED'
            $r.Detail = "new session; claude pid $($r.ClaudePid) attributed to handshake PID $lanePid"
            continue
        }

        # Resume evidence, read from the lane's own store.
        $now = Get-RepoState $l.State
        $new = @($now.FileNames | Where-Object { $l.State.FileNames -notcontains $_ })
        if ($new.Count -gt 0) {
            $r.Status = 'SILENT-NEW'
            $r.Detail = "claude pid $($r.ClaudePid) created a NEW transcript ($($new -join ', ')) instead of continuing"
            continue   # terminal: the outcome is already determined, stop polling this lane
        }
        $grew = $false
        if ($l.State.LatestPath -and (Test-Path -LiteralPath $l.State.LatestPath)) {
            $f = Get-Item -LiteralPath $l.State.LatestPath
            $grew = ($f.LastWriteTimeUtc -gt $l.State.LatestWriteUtc) -or ($f.Length -gt $l.State.LatestLength)
        }
        if ($grew) {
            $r.Status = 'RESUMED'
            $r.Detail = "claude pid $($r.ClaudePid) wrote to $(Split-Path -Leaf $l.State.LatestPath)"
            continue
        }
        $r.Status = 'UNCONFIRMED'
        $r.Detail = "claude pid $($r.ClaudePid) is live, but the store has not been written yet — resume NOT proven"
        $stillPending.Add($l)
    }
    $pendingLanes = @($stillPending)
}

Write-Host ''
Write-Host ('{0,-12} {1,-13} {2}' -f 'LANE','STATUS','EVIDENCE')
Write-Host ('-' * 104)
foreach ($s in $selected) {
    $r = $results[$s.Name]
    $colour = switch ($r.Status) {
        'RESUMED'     { 'Green' }
        'STARTED'     { 'Green' }
        'UNCONFIRMED' { 'Yellow' }
        default       { 'Red' }
    }
    Write-Host ('{0,-12} {1,-13} {2}' -f $r.Name, $r.Status, $r.Detail) -ForegroundColor $colour
}
Write-Host ('-' * 104)

$ok          = @($results.Values | Where-Object { $_.Status -in @('RESUMED','STARTED') })
$unconfirmed = @($results.Values | Where-Object { $_.Status -eq 'UNCONFIRMED' })
$silentNew   = @($results.Values | Where-Object { $_.Status -eq 'SILENT-NEW' })
$notLaunched = @($results.Values | Where-Object { $_.Status -in @('NOT-LAUNCHED','NO-CLAUDE') })
$refused     = @($results.Values | Where-Object { $_.Status -eq 'REFUSED' })

Write-Host ("requested {0} | proven {1} | unconfirmed {2} | silent-new {3} | not-launched {4} | refused {5}" -f `
    $selected.Count, $ok.Count, $unconfirmed.Count, $silentNew.Count, $notLaunched.Count, $refused.Count)
Write-Host "  run artifacts (markers + per-lane launchers): $RunDir" -ForegroundColor DarkGray

if ($silentNew.Count -gt 0) {
    Write-Host "FAILED: $($silentNew.Count) lane(s) started a NEW session instead of continuing." -ForegroundColor Red
    exit $EXIT_SILENT_NEW
}
if ($notLaunched.Count -gt 0) {
    Write-Host "FAILED: $($notLaunched.Count) lane(s) produced no attributable claude process." -ForegroundColor Red
    Write-Host "  Tabs may have opened without running anything. Re-check the wt separator, then inspect $RunDir." -ForegroundColor Red
    exit $EXIT_NOT_LAUNCHED
}
if ($refused.Count -gt 0 -and -not $AllowPartial) {
    Write-Host "FAILED: $($refused.Count) selected lane(s) were refused before launch (pass -AllowPartial to accept a partial run)." -ForegroundColor Red
    exit $EXIT_REFUSED
}
if ($unconfirmed.Count -gt 0 -and -not $AllowUnconfirmedResume) {
    Write-Host "UNPROVEN: $($unconfirmed.Count) lane(s) are running but their resume is not proven within $VerifyTimeoutSec s." -ForegroundColor Yellow
    Write-Host "  Raise -VerifyTimeoutSec, or pass -AllowUnconfirmedResume to accept a live-but-unproven lane." -ForegroundColor Yellow
    exit $EXIT_UNCONFIRMED
}
Write-Host "VERIFIED: $($ok.Count) of $($selected.Count) requested lane(s) proven, each attributed to its own launch." -ForegroundColor Green
exit $EXIT_OK

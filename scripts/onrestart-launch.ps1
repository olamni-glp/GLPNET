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
     lane's session store is checked FIRST, and a transcript only counts when its first record
     is a JSON object carrying a non-empty `sessionId` — the shape every real Claude Code
     transcript starts with. A zero-byte, unparseable, or structurally-valid-but-not-a-session
     file (`{}`) is NOT a resumable session and its lane is REFUSED. AFTER launch the store is
     re-read: a transcript that did not exist before launch, or an appended record carrying a
     DIFFERENT sessionId, is a new conversation and is reported as a FAILURE, never a resume.

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

  6. VERIFICATION IS PER LANE AND ATTRIBUTED — never a process count over a time window, and
     never a name. A count of `claude` processes started "in the last two minutes" is satisfied
     by processes this invocation did not create; a process merely NAMED claude.exe is satisfied
     by anything renamed. So each lane is launched through a generated per-lane launcher that
     writes a HANDSHAKE MARKER — lane key, run id, resolved cwd, own PID — before it execs
     claude, and a lane is proven only when ALL of these hold:
       * a marker whose lane, run and cwd match what was asked for;
       * a live process descending from that marker's PID, created after launch, whose IMAGE is
         the claude command this run resolved from PATH (or whose command line invokes it), and
         whose command line carries the arguments we asked for;
       * unless -Fresh, an APPENDED, complete, parseable record past the transcript's pre-launch
         length, carrying the expected sessionId. A timestamp change is not evidence — any
         process can touch a file.
     Lanes refused before launch stay in the denominator and are enumerated as failures.

  7. THE WORD "VERIFIED" IS EARNED, NOT DEFAULTED.  It is printed only when every requested lane
     is proven. -AllowPartial / -AllowUnconfirmedResume let the OPERATOR accept named exceptions,
     and that outcome reports as ACCEPTED-WITH-EXCEPTIONS listing each unproven lane — never as
     VERIFIED. No switch can make a run with zero proven lanes succeed.

  MEASURED AND REFUTED — do not "fix" path case in the lane table: Claude Code mangles the cwd
  into a store name case-preservingly, but Windows resolves the lookup case-insensitively, so
  case cannot split a lane's history. Tested and refuted in the reference lane.

  Regression cover: scripts\tests\onrestart-launch.tests.ps1 (fault injections, not absences).

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

.PARAMETER DeepValidate
  Validate every transcript in full instead of its last 64 KB. Slower, and only needed when you
  suspect corruption in the middle of a large transcript; the bounded check is stated as bounded
  rather than presented as a whole-file guarantee.

.PARAMETER AllowPartial
  Accept lanes that were REFUSED before launch. They are still listed as unproven and the run
  reports ACCEPTED-WITH-EXCEPTIONS, never VERIFIED.

.PARAMETER AllowUnconfirmedResume
  Accept a lane whose claude process is attributed and live but whose transcript showed no
  appended record within -VerifyTimeoutSec. Such a lane is always reported as UNCONFIRMED,
  never as resumed; this switch only decides whether it fails the run.

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
    [int]$KeepRuns = 20,
    [switch]$DeepValidate,
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
$EXIT_UNCONFIRMED  = 5   # launched and attributed, but no appended-record evidence
$EXIT_REFUSED      = 6   # a selected lane was refused before launch
$EXIT_SILENT_NEW   = 7   # a lane started a NEW session instead of continuing
$EXIT_NO_ATTRIB    = 8   # cannot attribute (no process table, or claude not resolvable)
$EXIT_NONE_PROVEN  = 9   # nothing was proven; no switch may call that success

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

function ConvertTo-ComparablePath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return '' }
    return ($Path.TrimEnd('\', '/')).ToLowerInvariant()
}

# Two lanes sharing a name or a path would share marker files, launcher files and result
# slots, so one attributed process could stand in for both and the run would still claim
# to have proven every lane. Identity must be unique before anything is launched.
function Get-LaneIdentityConflicts($Lanes) {
    $msgs = @()
    foreach ($g in @($Lanes | Group-Object -Property Name)) {
        if ($g.Count -gt 1) { $msgs += "duplicate lane name '$($g.Name)' ($($g.Count) entries)" }
    }
    foreach ($g in @($Lanes | Group-Object -Property { ConvertTo-ComparablePath $_.Path })) {
        if ($g.Count -gt 1) { $msgs += "duplicate lane path '$($g.Name)' ($($g.Count) entries: $(($g.Group.Name) -join ', '))" }
    }
    return , @($msgs)
}

# Claude Code's cwd -> session-store mangle. Case-preserving by design; harmless (see .DESCRIPTION).
function ConvertTo-SessionDirName([string]$Path) { ($Path.TrimEnd('\') -replace '[:\\]', '-') }

# FileStream.Read may return fewer bytes than asked for, and a transcript claude is actively
# writing changes length between the size check and the read. A single short read would decode
# a truncated record and report corruption that is not there, so the range is pinned first and
# read to completion; anything that cannot be read whole returns $null, which callers treat as
# "not yet known" rather than as a verdict.
function Read-FileRange([string]$Path, [long]$Offset, [long]$Count) {
    if ($Count -le 0) { return $null }
    try {
        $fs = [System.IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
        try {
            if ($Offset -gt 0) { $null = $fs.Seek($Offset, 'Begin') }
            $buf = New-Object byte[] $Count
            $got = 0
            while ($got -lt $Count) {
                $n = $fs.Read($buf, $got, $Count - $got)
                if ($n -le 0) { break }
                $got += $n
            }
            if ($got -lt $Count) { return $null }   # truncated under us: retryable, not a verdict
            return $buf
        } finally { $fs.Dispose() }
    } catch { return $null }
}

# Split a decoded byte range into lines, reporting whether the final line is unterminated.
function Split-TranscriptLines([byte[]]$Bytes, [int]$SkipBytes = 0) {
    if ($null -eq $Bytes) { return $null }
    $len = $Bytes.Length - $SkipBytes
    if ($len -lt 0) { return $null }
    $text = [System.Text.Encoding]::UTF8.GetString($Bytes, $SkipBytes, $len)
    $endsWithNewline = $text.EndsWith("`n")
    $parts = @($text -split "`n" | ForEach-Object { $_.TrimEnd("`r") })
    if ($endsWithNewline -and $parts.Count -gt 0) { $parts = @($parts | Select-Object -SkipLast 1) }
    return [pscustomobject]@{ Lines = @($parts); LastIsTerminated = $endsWithNewline }
}

function Test-JsonObjectLine([string]$Line) {
    if ([string]::IsNullOrWhiteSpace($Line)) { return $null }
    try { $o = $Line | ConvertFrom-Json -ErrorAction Stop } catch { return $null }
    if ($o -isnot [pscustomobject]) { return $null }
    return $o
}

# The session-id line every real Claude Code transcript opens with. Measured across 43
# transcripts in 40 stores on 2026-08-21: all 43 first records were JSON objects carrying a
# non-empty `sessionId`. A file without one cannot be continued, so it must not count —
# a filename, a zero-byte file, and a bare `{}` are all NOT resumable sessions.
# The byte just past the last complete record. A transcript can be captured mid-write, and the
# bytes appended afterwards then COMPLETE a record that began before launch. Decoding from the
# raw pre-launch length would split that record and silently drop it — including any foreign
# sessionId inside it — so scanning starts from the last record boundary instead.
function Get-LastRecordBoundary([System.IO.FileInfo]$File, [int]$TailBytes = 65536) {
    if ($TailBytes -lt 4096) { $TailBytes = 4096 }     # a zero window would never terminate
    try { $len = (Get-Item -LiteralPath $File.FullName).Length } catch { return -1 }
    if ($len -le 0) { return 0 }
    # Walk BACKWARDS in windows until a newline is found or the file start is reached. Returning
    # the window start when no newline was seen would hand back a position in the MIDDLE of a
    # record - and a record longer than one window (they exist: a large tool result) would then be
    # decoded from its middle, hiding whatever sessionId it carries. Offset 0 is the only position
    # that is a record boundary without a newline in front of it.
    $end = $len
    while ($end -gt 0) {
        $start = [Math]::Max(0L, $end - $TailBytes)
        $bytes = Read-FileRange $File.FullName $start ($end - $start)
        if ($null -eq $bytes) { return -1 }        # unreadable: unknown, never a guess
        for ($i = $bytes.Length - 1; $i -ge 0; $i--) {
            if ($bytes[$i] -eq 10) { return $start + $i + 1 }
        }
        if ($start -eq 0) { return 0 }             # no newline anywhere: the file starts a record
        $end = $start
    }
    return 0
}

# A content fingerprint of the transcript's opening bytes. Appending never changes them, so this
# is stable for the life of a session — and unlike a creation timestamp it cannot be inherited by
# a same-name replacement (NTFS file-system tunneling re-uses the creation time of a file deleted
# moments earlier, so a timestamp alone is not identity).
function Get-FileHeadHash([System.IO.FileInfo]$File, [long]$HeadBytes) {
    try { $len = (Get-Item -LiteralPath $File.FullName).Length } catch { return $null }
    if ($len -le 0 -or $HeadBytes -le 0) { return $null }
    if ($len -lt $HeadBytes) { return $null }   # shorter than when snapshotted: truncated/replaced
    $bytes = Read-FileRange $File.FullName 0 $HeadBytes
    if ($null -eq $bytes) { return $null }
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return [System.BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '') }
    finally { $sha.Dispose() }
}

function Get-SessionFileId([System.IO.FileInfo]$File) {
    if (-not $File -or $File.Length -le 0) { return $null }
    try {
        $reader = [System.IO.StreamReader]::new($File.FullName)
        try {
            while (-not $reader.EndOfStream) {
                $line = $reader.ReadLine()
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                $rec = Test-JsonObjectLine $line
                if ($null -eq $rec) { return $null }
                $id = $rec.sessionId
                if ($id -is [string] -and -not [string]::IsNullOrWhiteSpace($id)) { return $id }
                return $null
            }
        } finally { $reader.Dispose() }
    } catch { return $null }
    return $null
}

# Corruption check. EVERY complete line examined must parse; only the final line may be
# malformed, and only when the file does not end in a newline — that is a crash mid-write,
# which is normal and which Claude Code itself tolerates.
#
# STATED BOUND, so this check does not overclaim: by default only the last $TailBytes are
# examined, so corruption between the header and that window is NOT detected. That is a
# deliberate trade — a full scan of twelve multi-megabyte transcripts at logon costs more
# than it is worth — and -DeepValidate scans the whole file for anyone who wants it.
# A window that begins exactly on a record boundary keeps its first line; one that begins
# inside a record drops the fragment, and that is decided from the byte before the window,
# never assumed.
function Test-SessionTailIntact([System.IO.FileInfo]$File, [int]$TailBytes = 65536, [bool]$Deep = $false) {
    try { $len = (Get-Item -LiteralPath $File.FullName).Length } catch { return $false }
    if ($len -le 0) { return $false }
    if ($TailBytes -lt 4096) { $TailBytes = 4096 }     # a zero window would never terminate
    $win = if ($Deep) { [Math]::Max($len, 1L) } else { [long]$TailBytes }
    while ($true) {
        $start = [Math]::Max(0L, $len - $win)
        $probe = if ($start -gt 0) { $start - 1 } else { 0 }
        $bytes = Read-FileRange $File.FullName $probe ($len - $probe)
        if ($null -eq $bytes) { return $false }
        $startsOnBoundary = $true
        $skip = 0
        if ($start -gt 0) {
            $startsOnBoundary = ($bytes.Length -gt 0 -and $bytes[0] -eq 10)   # 10 = LF
            $skip = 1
        }
        $split = Split-TranscriptLines $bytes $skip
        if ($null -eq $split) { return $false }
        $lines = @($split.Lines)
        if (-not $startsOnBoundary -and $lines.Count -gt 0) { $lines = @($lines | Select-Object -Skip 1) }
        $complete = @($lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        if ($complete.Count -eq 0 -and $start -gt 0) {
            # The window landed entirely inside ONE record. A record CAN be larger than the
            # window (a big tool result), so widen rather than call a healthy transcript corrupt.
            $win = $win * 4
            continue
        }
        if ($complete.Count -eq 0) { return $false }
        $tornTailAllowed = -not $split.LastIsTerminated
        $lastIndex = $complete.Count - 1
        $parsedAny = $false
        for ($i = 0; $i -lt $complete.Count; $i++) {
            if ($null -ne (Test-JsonObjectLine $complete[$i])) { $parsedAny = $true; continue }
            if ($i -eq $lastIndex -and $tornTailAllowed) { continue }   # one unterminated record
            return $false                                              # a complete line that does not parse is corruption
        }
        return $parsedAny
    }
}
function Test-ResumableSessionFile([System.IO.FileInfo]$File, [bool]$Deep = $false) {
    if (-not $File -or $File.Length -le 0) { return $false }
    if (-not (Get-SessionFileId $File)) { return $false }
    return (Test-SessionTailIntact $File 65536 $Deep)
}

# Resume evidence must be CONTENT the launched claude appended, never a timestamp: any process
# can touch a file. Three things are required, and each closes a specific hole:
#
#  * scanning starts at the last COMPLETE RECORD BOUNDARY, not the raw pre-launch length, so a
#    record that straddles the launch is parsed whole instead of being split into an
#    unparseable fragment that hides whatever sessionId it carries;
#  * EVERY complete record in that range is examined for a foreign sessionId, which wins over
#    anything else in the range - a later foreign id cannot be masked by an earlier record;
#  * proof of resumption is a complete record that BEGINS at or after the pre-launch length and
#    carries the EXPECTED sessionId. A record without one proves only that the file changed, and
#    any process with access to the file could have done that, so it is not accepted as proof.
#    (Measured 2026-08-21 across 29 stores: 97.9% of non-header records carry sessionId and 28 of
#    29 transcripts have at least one, so requiring it does not starve honest lanes.)
function Get-ResumeEvidence($State) {
    if (-not $State.LatestPath -or -not (Test-Path -LiteralPath $State.LatestPath)) { return $null }
    try { $len = (Get-Item -LiteralPath $State.LatestPath).Length } catch { return $null }
    if ($len -le $State.LatestLength) { return $null }             # no append: a touch is not evidence
    # A transcript can be DELETED and re-created under the same name, in which case length and
    # offsets from the old file describe a file that no longer exists. Creation time and the
    # header session id together identify the file we actually snapshotted.
    try {
        $fi = Get-Item -LiteralPath $State.LatestPath
        $leafName = Split-Path -Leaf $State.LatestPath
        if ($fi.CreationTimeUtc -ne $State.LatestCreatedUtc) {
            return [pscustomobject]@{ Kind = 'WRONG-SESSION'; Strength = 'file-identity'
                Detail = "the transcript $leafName was replaced, not continued" }
        }
        # The opening bytes are the real identity. A replacement that inherits the creation time
        # through file-system tunneling still has to reproduce them exactly.
        $nowHash = Get-FileHeadHash $fi ([long]$State.LatestHeadLen)
        if ($State.LatestHeadHash) {
            if (-not $nowHash) {
                # Either unreadable, or now SHORTER than when snapshotted. A transcript never
                # shrinks while being appended to, so a short file is a replacement.
                try { $curLen = (Get-Item -LiteralPath $State.LatestPath).Length } catch { return $null }
                if ($curLen -lt [long]$State.LatestHeadLen) {
                    return [pscustomobject]@{ Kind = 'WRONG-SESSION'; Strength = 'file-identity'
                        Detail = "$leafName is shorter than when it was snapshotted - it was replaced, not appended to" }
                }
                return $null
            }
            if ($nowHash -cne $State.LatestHeadHash) {
                return [pscustomobject]@{ Kind = 'WRONG-SESSION'; Strength = 'file-identity'
                    Detail = "the opening bytes of $leafName changed - it was replaced, not appended to" }
            }
        }
        $nowId = Get-SessionFileId $fi
        if ($State.LatestSessionId) {
            if (-not $nowId) { return $null }                   # header unreadable: cannot judge
            if ($nowId -cne $State.LatestSessionId) {
                return [pscustomobject]@{ Kind = 'WRONG-SESSION'; Strength = 'file-identity'
                    Detail = "the transcript now opens with sessionId '$nowId', expected '$($State.LatestSessionId)'" }
            }
        }
    } catch { return $null }
    $from = [long]$State.LatestCompleteOffset
    if ($from -lt 0) { return $null }              # boundary unknown: cannot judge, so do not
    if ($from -gt [long]$State.LatestLength) { $from = [long]$State.LatestLength }
    $bytes = Read-FileRange $State.LatestPath $from ($len - $from)
    if ($null -eq $bytes) { return $null }

    $leaf  = Split-Path -Leaf $State.LatestPath
    $added = $len - $State.LatestLength
    $foreign = $null; $proof = $null; $sawRecord = $false
    $lineStart = 0
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -ne 10) { continue }                        # only terminated records count
        $absStart = $from + $lineStart
        $line = [System.Text.Encoding]::UTF8.GetString($bytes, $lineStart, $i - $lineStart).TrimEnd("`r")
        $lineStart = $i + 1
        $rec = Test-JsonObjectLine $line
        if ($null -eq $rec) { continue }
        if ($absStart -ge [long]$State.LatestLength) { $sawRecord = $true }
        $sid = $rec.sessionId
        if ($sid -isnot [string] -or [string]::IsNullOrWhiteSpace($sid)) { continue }
        if ($State.LatestSessionId -and $sid -cne $State.LatestSessionId) { $foreign = $sid; continue }
        if ($State.LatestSessionId -and $sid -ceq $State.LatestSessionId -and $absStart -ge [long]$State.LatestLength) {
            $proof = $absStart
        }
    }
    if ($foreign) {
        return [pscustomobject]@{ Kind = 'WRONG-SESSION'; Strength = 'session-id'
            Detail = "appended a record carrying sessionId '$foreign', expected '$($State.LatestSessionId)'" }
    }
    if ($null -ne $proof) {
        return [pscustomobject]@{ Kind = 'RESUMED'; Strength = 'expected-session-id'
            Detail = "appended $added byte(s) to $leaf including a record at offset $proof carrying sessionId '$($State.LatestSessionId)'" }
    }
    if ($sawRecord) {
        # Something wrote complete records and none of them named a session. That is not proof of
        # resumption - any process with access to the file could have written them - but it is a
        # materially different situation from silence, so it is reported as its own thing.
        return [pscustomobject]@{ Kind = 'UNIDENTIFIED'; Strength = 'none'
            Detail = "appended $added byte(s) to $leaf but no record named sessionId '$($State.LatestSessionId)'" }
    }
    return $null
}

function Get-RepoState($Repo) {
    $dirName = ConvertTo-SessionDirName $Repo.Path
    $store   = Join-Path $ProjectsRoot $dirName
    $files   = @()
    if (Test-Path -LiteralPath $store) {
        $files = @(Get-ChildItem -LiteralPath $store -Filter '*.jsonl' -ErrorAction SilentlyContinue)
    }
    $deep    = [bool]$DeepValidate
    $usable  = @($files | Where-Object { Test-ResumableSessionFile $_ $deep })
    $latest  = $usable | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    [pscustomobject]@{
        Name = $Repo.Name; Group = $Repo.Group; Path = $Repo.Path; SessionDir = $dirName
        Key = $Repo.Key
        Store = $store
        PathExists = (Test-Path -LiteralPath $Repo.Path)
        StoreExists = (Test-Path -LiteralPath $store)
        Sessions = $files.Count
        Usable = $usable.Count
        Latest = if ($latest) { $latest.LastWriteTime } else { $null }
        # Pre-launch fingerprint of the store, used after launch to prove resumption.
        FileNames = @($files | ForEach-Object { $_.Name })
        LatestPath = if ($latest) { $latest.FullName } else { $null }
        LatestCreatedUtc = if ($latest) { $latest.CreationTimeUtc } else { [datetime]::MinValue }
        LatestHeadLen = if ($latest) { [Math]::Min(4096L, $latest.Length) } else { 0 }
        LatestHeadHash = if ($latest) { Get-FileHeadHash $latest ([Math]::Min(4096L, $latest.Length)) } else { $null }
        LatestLength = if ($latest) { $latest.Length } else { 0 }
        LatestCompleteOffset = if ($latest) { Get-LastRecordBoundary $latest } else { 0 }
        LatestSessionId = if ($latest) { Get-SessionFileId $latest } else { $null }
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
            ExecutablePath = $p.ExecutablePath
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
        if (-not $seen.Add($cur)) { continue }          # a parent cycle cannot spin the walk
        if (-not $byParent.ContainsKey($cur)) { continue }
        foreach ($child in $byParent[$cur]) {
            $out.Add($child); $queue.Enqueue($child.ProcId)
        }
    }
    return $out
}

# Windows argv tokenization (CommandLineToArgvW rules). Substring tests over a raw command line
# are forgeable: any process can carry another program's path, or an expected flag, as DATA inside
# one of its own arguments. Splitting into real tokens is what makes "this process was invoked as
# X with argument Y" a statement about the invocation rather than about the bytes.
function ConvertFrom-NativeCommandLine([string]$CommandLine) {
    $out = [System.Collections.Generic.List[string]]::new()
    if ([string]::IsNullOrWhiteSpace($CommandLine)) { return @() }

    # argv[0] is NOT parsed by the escape state machine. CommandLineToArgvW reads it as:
    #   starts with a quote -> everything up to the NEXT quote, quotes stripped, no escapes;
    #   otherwise           -> everything up to the first whitespace, VERBATIM, quotes and all.
    # Running the ordinary rules over it mis-splits paths like C:\dir\"prog.exe". Verified
    # against the platform parser by the harness's differential test.
    $i = 0
    if ($CommandLine[0] -eq '"') {
        $close = $CommandLine.IndexOf('"', 1)
        if ($close -lt 0) { $out.Add($CommandLine.Substring(1)); return $out.ToArray() }
        $out.Add($CommandLine.Substring(1, $close - 1))
        $i = $close + 1
    } else {
        $e = 0
        while ($e -lt $CommandLine.Length -and $CommandLine[$e] -ne ' ' -and $CommandLine[$e] -ne "`t") { $e++ }
        $out.Add($CommandLine.Substring(0, $e))
        $i = $e
    }

    # Everything after argv[0] uses the ordinary backslash/quote rules.
    $sb = [System.Text.StringBuilder]::new()
    $inQuotes = $false; $slashes = 0; $started = $false
    for (; $i -lt $CommandLine.Length; $i++) {
        $ch = $CommandLine[$i]
        if ($ch -eq '\') { $slashes++; $started = $true; continue }
        if ($ch -eq '"') {
            [void]$sb.Append('\' * [int]($slashes / 2))
            if ($slashes % 2 -eq 1) { [void]$sb.Append('"') } else { $inQuotes = -not $inQuotes }
            $slashes = 0; $started = $true; continue
        }
        if ($slashes -gt 0) { [void]$sb.Append('\' * $slashes); $slashes = 0 }
        if (-not $inQuotes -and ($ch -eq ' ' -or $ch -eq "`t")) {
            if ($started) { $out.Add($sb.ToString()); [void]$sb.Clear(); $started = $false }
            continue
        }
        [void]$sb.Append($ch); $started = $true
    }
    if ($slashes -gt 0) { [void]$sb.Append('\' * $slashes); $started = $true }
    if ($started) { $out.Add($sb.ToString()) }
    return $out.ToArray()
}

# A NAME proves nothing: any executable can be copied to claude.exe. Nor does an arbitrary
# OCCURRENCE of the resolved path in a command line, which any process can carry as data.
# A candidate counts only when the INVOCATION identifies it:
#
#   * its image path IS the resolved claude command; or
#   * it is the system command processor running that exact path as its /c target — the shape a
#     .cmd or .bat shim takes, matched on a real argv token, not on a substring; or
#   * it is a JS runtime whose argv names the Claude Code CLI entry point FILE.
#
# and in every case its argv must carry the arguments we asked for, as whole tokens, with any
# flag/value pair adjacent.
function Test-IsClaudeProc($Proc, [string]$ClaudePath, [string[]]$ExpectedArgs) {
    if ([string]::IsNullOrWhiteSpace($ClaudePath)) { return $false }
    $target = $ClaudePath.ToLowerInvariant()
    $cl = $Proc.CommandLine
    $argv = @(ConvertFrom-NativeCommandLine $cl)
    $lower = @($argv | ForEach-Object { $_.ToLowerInvariant() })

    $identified = $false
    if ($Proc.ExecutablePath -and $Proc.ExecutablePath.ToLowerInvariant() -eq $target) { $identified = $true }
    if (-not $identified -and $Proc.Name -and $Proc.Name.ToLowerInvariant() -eq 'cmd.exe') {
        # The command processor. The resolved path must be what cmd was TOLD TO RUN — the head of
        # its /c (or /k) command string — not merely a token appearing somewhere in it, which
        # "cmd /c echo <path> --continue" would satisfy.
        for ($n = 0; $n -lt $argv.Count; $n++) {
            if ($lower[$n] -ne '/c' -and $lower[$n] -ne '/k') { continue }
            if ($n + 1 -ge $argv.Count) { break }
            $head = $lower[$n + 1]
            if ($head -eq $target) { $identified = $true; break }
            $inner = @(ConvertFrom-NativeCommandLine $argv[$n + 1])
            if ($inner.Count -gt 0 -and $inner[0].ToLowerInvariant() -eq $target) { $identified = $true }
            break
        }
    }
    if (-not $identified) {
        # A JS runtime running the Claude Code entry point FILE. The runtime is named explicitly:
        # without it, any executable carrying that path as an argument would be attributed.
        $img = ''
        if ($Proc.ExecutablePath) { $img = (Split-Path -Leaf $Proc.ExecutablePath).ToLowerInvariant() }
        elseif ($Proc.Name) { $img = $Proc.Name.ToLowerInvariant() }
        if ($img -in @('node.exe', 'bun.exe', 'deno.exe', 'node', 'bun', 'deno')) {
            foreach ($tok in $lower) {
                if ($tok -match '(?i)[\\/]claude-code[\\/]cli\.(js|mjs|cjs)$') { $identified = $true; break }
            }
        }
    }
    if (-not $identified) { return $false }

    # Arguments as whole argv tokens. cmd.exe hands its target one packed string, so tokens are
    # searched one level deep as well - still tokens, never substrings.
    $flat = [System.Collections.Generic.List[string]]::new()
    $isCmd = ($Proc.Name -and $Proc.Name.ToLowerInvariant() -eq 'cmd.exe')
    foreach ($tok in $argv) {
        $flat.Add($tok)
        # ONLY cmd.exe hands its target one packed string. Expanding any process's arguments a
        # level deep would let a single quoted argument such as "payload --continue 1000000"
        # masquerade as three real argv tokens.
        if ($isCmd) { foreach ($inner in @(ConvertFrom-NativeCommandLine $tok)) { $flat.Add($inner) } }
    }
    $flatLower = @($flat | ForEach-Object { $_.ToLowerInvariant() })
    $expected = @($ExpectedArgs | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    for ($i = 0; $i -lt $expected.Count; $i++) {
        $a = $expected[$i].ToLowerInvariant()
        $idx = [array]::IndexOf($flatLower, $a)
        if ($idx -lt 0) { return $false }
        # A flag's value must FOLLOW it, so "--autocompact 1000000" cannot be satisfied by the
        # two tokens appearing independently somewhere in the command line.
        if ($i + 1 -lt $expected.Count -and -not $expected[$i + 1].StartsWith('-')) {
            $v = $expected[$i + 1].ToLowerInvariant()
            if ($idx + 1 -ge $flatLower.Count -or $flatLower[$idx + 1] -ne $v) { return $false }
            $i++
        }
    }
    return $true
}

# Attribution for one lane. There is exactly ONE tier: a live descendant that IDENTIFIES itself
# as the claude command per Test-IsClaudeProc above. "Some descendant of the launcher is alive"
# is deliberately NOT accepted — a descendant proves the launcher's command ran, not that what
# survives is claude. The launcher runs with -NoProfile and invokes the resolved absolute path
# and nothing else, so a healthy lane has a descendant that identifies; if none does, the honest
# answer is NO-CLAUDE.
function Get-LaneClaudeEvidence {
    param($Table, [int]$LanePid, [string]$ClaudeExe, [string[]]$ClaudeArgs, [datetime]$LaunchStart)
    $fresh = @(Get-DescendantProcs $Table $LanePid | Where-Object { -not $_.Created -or $_.Created -ge $LaunchStart })
    if ($fresh.Count -eq 0) { return $null }
    $strong = @($fresh | Where-Object { Test-IsClaudeProc $_ $ClaudeExe $ClaudeArgs })
    if ($strong.Count -eq 0) { return $null }
    return [pscustomobject]@{ Proc = $strong[0]; Strength = 'image'
        Detail = "claude pid $($strong[0].ProcId) ($($strong[0].Name)) matches the resolved command" }
}

# A marker proves a tab ran ITS OWN command. Accepting one without checking which lane,
# which run and which directory it came from would let a stale or misdirected launcher
# stand in for the lane that was actually requested.
function Test-LaneMarker($Marker, [string]$ExpectedKey, [string]$ExpectedRunId, [string]$ExpectedPath) {
    if ($null -eq $Marker) { return 'marker is empty or unparseable' }
    if ([string]$Marker.key   -cne $ExpectedKey)   { return "marker is for lane '$($Marker.key)', expected '$ExpectedKey'" }
    if ([string]$Marker.runId -cne $ExpectedRunId) { return "marker is from run '$($Marker.runId)', expected '$ExpectedRunId'" }
    $got  = ConvertTo-ComparablePath ([string]$Marker.path)
    $want = ConvertTo-ComparablePath $ExpectedPath
    if ($got -ne $want) { return "marker cwd is '$($Marker.path)', expected '$ExpectedPath'" }
    $markerPid = 0
    if (-not [int]::TryParse([string]$Marker.pwshPid, [ref]$markerPid) -or $markerPid -le 0) { return 'marker carries no usable PID' }
    return $null   # $null == valid
}

# One lane's state transition, isolated from the polling loop so every branch is testable.
# Terminal means the outcome cannot change by waiting; anything else is retried until the
# deadline. Never returns a proven status without the evidence that status claims.
function Get-LaneVerification {
    param($Lane, $Table, [datetime]$LaunchStart, [string]$RunId, [string]$ClaudeExe,
          [string[]]$ClaudeArgs, [bool]$Fresh)
    $mk = { param($s, $d, $t, $p = $null)
            [pscustomobject]@{ Status = $s; Detail = $d; Terminal = $t; ClaudePid = $p } }

    if (-not (Test-Path -LiteralPath $Lane.MarkerPath)) {
        return & $mk 'PENDING' 'no handshake marker yet' $false
    }
    $marker = $null
    try { $marker = Get-Content -LiteralPath $Lane.MarkerPath -Raw | ConvertFrom-Json } catch {
        return & $mk 'PENDING' 'handshake marker not readable yet' $false
    }
    $bad = Test-LaneMarker -Marker $marker -ExpectedKey $Lane.Key -ExpectedRunId $RunId -ExpectedPath $Lane.Path
    if ($bad) { return & $mk 'NOT-LAUNCHED' "handshake rejected: $bad" $true }

    $lanePid  = [int]$marker.pwshPid
    $laneProc = $Table[$lanePid]
    if (-not $laneProc -or ($laneProc.Created -and $laneProc.Created -lt $LaunchStart)) {
        return & $mk 'NOT-LAUNCHED' "handshake PID $lanePid is not a live process created by this launch" $false
    }
    $ev = Get-LaneClaudeEvidence -Table $Table -LanePid $lanePid -ClaudeExe $ClaudeExe -ClaudeArgs $ClaudeArgs -LaunchStart $LaunchStart
    if ($null -eq $ev) {
        return & $mk 'NO-CLAUDE' "tab ran (handshake PID $lanePid) but nothing it started is still running" $false
    }
    $cpid = $ev.Proc.ProcId
    if ($Fresh) {
        return & $mk 'STARTED' "new session in $($Lane.Path); $($ev.Detail) [$($ev.Strength)]" $true $cpid
    }

    $now = Get-RepoState $Lane.State
    $new = @($now.FileNames | Where-Object { $Lane.State.FileNames -notcontains $_ })
    if ($new.Count -gt 0) {
        return & $mk 'SILENT-NEW' "$($ev.Detail) but a NEW transcript appeared ($($new -join ', ')) instead of a continuation" $true $cpid
    }
    $rev = Get-ResumeEvidence $Lane.State
    if ($rev -and $rev.Kind -eq 'WRONG-SESSION') {
        return & $mk 'SILENT-NEW' "$($ev.Detail); $($rev.Detail)" $true $cpid
    }
    if ($rev -and $rev.Kind -eq 'RESUMED') {
        return & $mk 'RESUMED' "$($ev.Detail) [$($ev.Strength)]; $($rev.Detail) [$($rev.Strength)]" $true $cpid
    }
    if ($rev -and $rev.Kind -eq 'UNIDENTIFIED') {
        return & $mk 'UNCONFIRMED' "$($ev.Detail) [$($ev.Strength)]; $($rev.Detail) — resume NOT proven" $false $cpid
    }
    return & $mk 'UNCONFIRMED' "$($ev.Detail) [$($ev.Strength)], but nothing has been appended to the transcript — resume NOT proven" $false $cpid
}

# Retention with an ownership check. Deleting by age alone can take the marker directory out
# from under a concurrent invocation that is still polling, which would turn its lanes into
# NOT-LAUNCHED. Two independent guards: the owner holds active.lock OPEN for its whole life,
# so Windows refuses the delete outright; and the recorded start time must match the live
# process EXACTLY, so a reused PID cannot inherit another run's claim.
function Remove-StaleRunDirs {
    param([string]$RunsRoot, [int]$KeepRuns, [string]$CurrentRunDir)
    $removed = @(); $skipped = @()
    if (-not (Test-Path -LiteralPath $RunsRoot)) {
        return [pscustomobject]@{ Removed = @(); Skipped = @() }
    }
    $dirs = @(Get-ChildItem -LiteralPath $RunsRoot -Directory -ErrorAction SilentlyContinue |
              Sort-Object CreationTimeUtc -Descending)
    foreach ($d in @($dirs | Select-Object -Skip ([Math]::Max(1, $KeepRuns)))) {
        if ($CurrentRunDir -and (ConvertTo-ComparablePath $d.FullName) -eq (ConvertTo-ComparablePath $CurrentRunDir)) {
            $skipped += $d.FullName; continue
        }
        $lock = Join-Path $d.FullName 'active.lock'
        if (Test-Path -LiteralPath $lock) {
            $live = $true    # an unreadable lock is treated as live: never delete on a guess
            try {
                $o = Get-Content -LiteralPath $lock -Raw | ConvertFrom-Json
                $owner = Get-Process -Id ([int]$o.pid) -ErrorAction SilentlyContinue
                $live = $false
                if ($owner -and $o.startedTicks) {
                    $ticks = 0L
                    try { $ticks = $owner.StartTime.ToUniversalTime().Ticks } catch { $ticks = -1 }
                    # Exact identity, not a window: a reused PID has a different start instant.
                    if ($ticks -eq -1 -or $ticks -eq [long]$o.startedTicks) { $live = $true }
                } elseif ($owner) { $live = $true }   # pre-tick lock format: assume live
            } catch { $live = $true }
            if ($live) { $skipped += $d.FullName; continue }
        }
        try { Remove-Item -LiteralPath $d.FullName -Recurse -Force -ErrorAction Stop; $removed += $d.FullName }
        catch { $skipped += $d.FullName }   # an open handle in a live run also lands here
    }
    return [pscustomobject]@{ Removed = @($removed); Skipped = @($skipped) }
}

# The final decision, isolated so it can be tested exhaustively. Precedence is by damage:
# a wrong session beats a missing one, a missing one beats an unproven one, and no switch
# can turn zero proven lanes into success.
function Get-RunOutcome {
    param([object[]]$Results, [int]$RequestedCount, [bool]$AllowPartial, [bool]$AllowUnconfirmed)
    $proven      = @($Results | Where-Object { $_.Status -in @('RESUMED','STARTED') })
    $unconfirmed = @($Results | Where-Object { $_.Status -eq 'UNCONFIRMED' })
    $silentNew   = @($Results | Where-Object { $_.Status -eq 'SILENT-NEW' })
    $notLaunched = @($Results | Where-Object { $_.Status -in @('NOT-LAUNCHED','NO-CLAUDE','PENDING') })
    $refused     = @($Results | Where-Object { $_.Status -eq 'REFUSED' })
    $mk = {
        param($status, $code, $msg)
        [pscustomobject]@{
            Status = $status; ExitCode = $code; Message = $msg
            Proven = $proven.Count; Unconfirmed = $unconfirmed.Count; SilentNew = $silentNew.Count
            NotLaunched = $notLaunched.Count; Refused = $refused.Count; Requested = $RequestedCount
        }
    }
    if ($silentNew.Count -gt 0) {
        return & $mk 'FAILED' 7 "$($silentNew.Count) lane(s) started a NEW session instead of continuing."
    }
    if ($notLaunched.Count -gt 0) {
        return & $mk 'FAILED' 4 "$($notLaunched.Count) lane(s) produced no attributable claude process."
    }
    if ($refused.Count -gt 0 -and -not $AllowPartial) {
        return & $mk 'FAILED' 6 "$($refused.Count) selected lane(s) were refused before launch (pass -AllowPartial to accept a partial run)."
    }
    if ($unconfirmed.Count -gt 0 -and -not $AllowUnconfirmed) {
        return & $mk 'UNPROVEN' 5 "$($unconfirmed.Count) lane(s) are running but their resume is not proven."
    }
    if ($proven.Count -eq 0) {
        return & $mk 'FAILED' 9 'no lane was proven; -AllowPartial / -AllowUnconfirmedResume cannot make that a success.'
    }
    if ($proven.Count -eq $RequestedCount) {
        return & $mk 'VERIFIED' 0 "$($proven.Count) of $RequestedCount requested lane(s) proven, each attributed to its own launch."
    }
    return & $mk 'ACCEPTED-WITH-EXCEPTIONS' 0 ("$($proven.Count) of $RequestedCount lane(s) proven; " +
        "$($refused.Count) refused and $($unconfirmed.Count) unconfirmed were accepted by switch. This run is NOT verified.")
}

# The launcher invokes the ABSOLUTE path this run resolved for claude, not the bare word, so
# what runs in the tab is exactly what attribution was resolved against — a PATH change
# between here and the tab cannot substitute a different program.
function New-LaneLauncher {
    param($Lane, [string]$RunDir, [string]$RunId, [string]$ClaudePath, [string[]]$ClaudeArgs)
    $marker   = Join-Path $RunDir ('marker-{0}.json' -f $Lane.Key)
    $launcher = Join-Path $RunDir ('lane-{0}.ps1'    -f $Lane.Key)
    $q = { param([string]$s) "'" + ($s -replace "'", "''") + "'" }
    $qPath    = & $q $Lane.Path
    $qMarker  = & $q $marker
    $qKey     = & $q $Lane.Key
    $qRun     = & $q $RunId
    $qClaude  = & $q $ClaudePath
    $body = @"
# generated by bk-onrestart run $RunId — regenerated every launch; do not edit
`$ErrorActionPreference = 'Stop'
try {
    Set-Location -LiteralPath $qPath
    [pscustomobject]@{
        key        = $qKey
        runId      = $qRun
        pwshPid    = `$PID
        path       = (Get-Location).Path
        claude     = $qClaude
        startedUtc = (Get-Date).ToUniversalTime().ToString('o')
    } | ConvertTo-Json -Compress | Set-Content -LiteralPath $qMarker -Encoding utf8
} catch {
    Write-Warning "bk-onrestart: could not enter the lane directory or record the handshake: `$_"
    Write-Warning "bk-onrestart: NOT starting claude - it would resume the wrong conversation."
    exit 9
}
& $qClaude $($ClaudeArgs -join ' ')
"@
    Set-Content -LiteralPath $launcher -Value $body -Encoding utf8
    return [pscustomobject]@{
        Name = $Lane.Name; Key = $Lane.Key; Path = $Lane.Path; Group = $Lane.Group
        LauncherPath = $launcher; MarkerPath = $marker; State = $Lane
    }
}

function Get-WtCommandLine {
    param([object[]]$Lanes, [string]$WindowId)
    $parts = [System.Collections.Generic.List[string]]::new()
    $parts.Add((ConvertTo-NativeArg '-w')); $parts.Add((ConvertTo-NativeArg $WindowId))
    $first = $true
    foreach ($l in $Lanes) {
        # BARE ';' — a quoted or backtick-escaped separator reaches wt literally and yields
        # tabs that open and run nothing. It is the ONLY token left unquoted.
        if (-not $first) { $parts.Add(';') }
        # -NoProfile: a user profile could start long-lived children of the lane's pwsh that
        # attribution would then have to tell apart from claude. Not loading it removes the
        # whole class, and makes the logon path deterministic across hosts.
        foreach ($t in @('new-tab', '--title', $l.Name, '-d', $l.Path, 'pwsh', '-NoProfile', '-NoExit', '-File', $l.LauncherPath)) {
            $parts.Add((ConvertTo-NativeArg $t))
        }
        $first = $false
    }
    return ($parts -join ' ')
}
# --- Install / uninstall the at-logon trigger (portable to any host) -------------
function Install-Trigger {
    $self = $PSCommandPath
    if (-not $self) { throw "cannot resolve own path; run via 'pwsh -File <script> -Install'" }
    $pwsh = (Get-Process -Id $PID).Path        # the pwsh actually running us — portable
    # -AllowUnconfirmedResume on the UNATTENDED path only. A lane whose claude is attributed and
    # live, but whose transcript has not yet named the session within the verification window, is
    # a normal outcome at logon before anyone types anything. The task accepts it — and it still
    # reports ACCEPTED-WITH-EXCEPTIONS naming that lane, never VERIFIED. Run the script by hand
    # without the switch to hold the strict default.
    $action  = New-ScheduledTaskAction -Execute $pwsh `
               -Argument ('-NoProfile -WindowStyle Hidden -File "{0}" -WaitForMounts -AllowUnconfirmedResume' -f $self)
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
    $trigger.Delay = [System.Xml.XmlConvert]::ToString([TimeSpan]::FromSeconds($DelaySeconds))  # ISO-8601, e.g. PT45S
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
                -StartWhenAvailable -ExecutionTimeLimit ([TimeSpan]::FromMinutes(30))
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings `
        -Description "Resume all Claude Code repo lanes $DelaySeconds s after logon (buildkit bk-onrestart prototype)" `
        -Force | Out-Null
    Write-Host "installed scheduled task '$TaskName' on $env:COMPUTERNAME for $env:USERNAME" -ForegroundColor Green
    Write-Host "  fires: at logon + $DelaySeconds s delay" -ForegroundColor Green
    Write-Host "  runs : $pwsh -NoProfile -WindowStyle Hidden -File `"$self`" -WaitForMounts -AllowUnconfirmedResume" -ForegroundColor DarkGray
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
    $lanes = @($cfg.lanes | Where-Object { $_.name -ne $laneName -and (ConvertTo-ComparablePath $_.path) -ne (ConvertTo-ComparablePath $path) })
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
$conflicts = Get-LaneIdentityConflicts $selected
if ($conflicts.Count -gt 0) {
    Write-Error ("lane identities are not unique, so a launch could not be attributed per lane:`n  " +
                 ($conflicts -join "`n  ") + "`nFix them in $ConfigPath")
    exit $EXIT_BADARGS
}
# Immutable per-run key: filenames, markers and result slots are keyed by this, never by a
# display name that configuration could repeat.
for ($i = 0; $i -lt $selected.Count; $i++) {
    $selected[$i] | Add-Member -NotePropertyName Key -NotePropertyValue ('{0:d2}-{1}' -f $i, $selected[$i].Name) -Force
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

# A lane is launchable only if it has a session that can actually be continued.
$launchable = @($state | Where-Object { $_.PathExists -and $_.StoreExists -and $_.Usable -gt 0 })
$blocked    = @($state | Where-Object { -not ($_.PathExists -and $_.StoreExists -and $_.Usable -gt 0) })

if ($blocked.Count -gt 0) {
    Write-Host ''; Write-Host "REFUSING to resume $($blocked.Count) lane(s):" -ForegroundColor Yellow
    foreach ($b in $blocked) {
        $why = if (-not $b.PathExists) { 'path does not exist' }
               elseif (-not $b.StoreExists) { "no session store at ...\$($b.SessionDir)  <- Claude has never run in this directory" }
               elseif ($b.Sessions -eq 0) { 'session store is empty' }
               else { "all $($b.Sessions) transcript(s) lack a session record or are corrupt — none can be continued" }
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
$ClaudeExe = (Get-Command 'claude' -ErrorAction SilentlyContinue | Select-Object -First 1).Source

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
Write-Host "claude exe  : $(if ($ClaudeExe) { $ClaudeExe } else { '<NOT ON PATH>' })"
Write-Host "validation  : $(if ($DeepValidate) { 'deep (whole transcript)' } else { 'bounded (last 64 KB of each transcript)' })"

if ($DryRun) { Write-Host ''; Write-Host 'DRY RUN - nothing launched.' -ForegroundColor Cyan; exit $EXIT_OK }
if ($launchable.Count -eq 0) { Write-Host 'Nothing to launch.' -ForegroundColor Yellow; exit $EXIT_NOTHING }

$wt = 'wt.exe'
if (-not (Get-Command $wt -ErrorAction SilentlyContinue)) {
    Write-Error "Windows Terminal (wt.exe) not found on PATH."; exit $EXIT_NO_WT
}

# Attribution must be possible BEFORE anything is launched — otherwise the run could only end
# in a count or a name match, which are exactly the checks this script refuses to make.
if ([string]::IsNullOrWhiteSpace($ClaudeExe)) {
    Write-Error "'claude' does not resolve on PATH. Without the real command path a launched process cannot be told apart from anything renamed claude.exe, so no lane could be honestly proven. Refusing to launch."
    exit $EXIT_NO_ATTRIB
}
try { $null = Get-ProcTable }
catch {
    Write-Error "cannot read the process table (Win32_Process): $_`nWithout it a launch cannot be attributed to a lane, and an unattributed count is not verification. Refusing to launch."
    exit $EXIT_NO_ATTRIB
}

# --- Per-lane launcher + handshake marker ----------------------------------------
# Each tab runs a generated script instead of an inline command, for two reasons:
#   * it writes a MARKER identifying lane, run and cwd before exec'ing claude, which is what
#     makes the later verification attributable to THIS lane and THIS invocation;
#   * an inline command would need ';' to sequence, and ';' is wt's tab separator.
# The launcher stops on error and does NOT start claude if it could not enter the lane
# directory: a claude started in the wrong cwd would resume the wrong conversation.
$runId  = '{0}-{1}' -f (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'), ([guid]::NewGuid().ToString('N').Substring(0, 6))
$RunsRoot = Join-Path $env:LOCALAPPDATA 'bk-onrestart\runs'
$RunDir = Join-Path $RunsRoot $runId
New-Item -ItemType Directory -Path $RunDir -Force | Out-Null
# Claim this run dir so a concurrent invocation's retention pass cannot delete it while we poll.
# The handle stays OPEN for this process's whole life, so Windows itself refuses to delete
# this directory while we are still polling; the exact start ticks defeat PID reuse.
$RunLockHandle = $null
try {
    $lockPath = Join-Path $RunDir 'active.lock'
    $me = Get-Process -Id $PID
    $lockJson = [pscustomobject]@{
        pid = $PID; startedTicks = $me.StartTime.ToUniversalTime().Ticks
        startedUtc = $me.StartTime.ToUniversalTime().ToString('o'); image = $me.Path
    } | ConvertTo-Json -Compress
    $RunLockHandle = [System.IO.File]::Open($lockPath, 'Create', 'Write', 'Read')
    $lockBytes = [System.Text.Encoding]::UTF8.GetBytes($lockJson)
    $RunLockHandle.Write($lockBytes, 0, $lockBytes.Length)
    $RunLockHandle.Flush()
} catch { Write-Host "  (could not claim the run dir: $_)" -ForegroundColor DarkGray }

$prune = Remove-StaleRunDirs -RunsRoot $RunsRoot -KeepRuns $KeepRuns -CurrentRunDir $RunDir
if ($prune.Removed.Count -gt 0) { Write-Host "  pruned $($prune.Removed.Count) completed run dir(s) under $RunsRoot" -ForegroundColor DarkGray }
if ($prune.Skipped.Count -gt 0) { Write-Host "  kept $($prune.Skipped.Count) run dir(s) still claimed by a live process" -ForegroundColor DarkGray }

$plan = @($launchable | ForEach-Object { New-LaneLauncher -Lane $_ -RunDir $RunDir -RunId $runId -ClaudePath $ClaudeExe -ClaudeArgs $claudeArgs })

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

# --- Verify PER LANE, by attribution ---------------------------------------------
Write-Host ''
Write-Host "Verifying per lane (attributed, not counted) — up to $VerifyTimeoutSec s..." -ForegroundColor Cyan

$results = @{}
foreach ($s in $selected) {
    $results[$s.Key] = [pscustomobject]@{
        Name = $s.Name; Key = $s.Key; Status = 'REFUSED'; Detail = 'refused before launch'; ClaudePid = $null
    }
}
foreach ($l in $plan) {
    $results[$l.Key] = [pscustomobject]@{
        Name = $l.Name; Key = $l.Key; Status = 'NOT-LAUNCHED'
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
        $v = Get-LaneVerification -Lane $l -Table $table -LaunchStart $launchStart -RunId $runId `
                                  -ClaudeExe $ClaudeExe -ClaudeArgs $claudeArgs -Fresh ([bool]$Fresh)
        if ($v.Status -ne 'PENDING') {
            $r = $results[$l.Key]
            $r.Status = $v.Status; $r.Detail = $v.Detail; $r.ClaudePid = $v.ClaudePid
        }
        if (-not $v.Terminal) { $stillPending.Add($l) }
    }
    $pendingLanes = @($stillPending)
}

Write-Host ''
Write-Host ('{0,-12} {1,-13} {2}' -f 'LANE','STATUS','EVIDENCE')
Write-Host ('-' * 104)
foreach ($s in $selected) {
    $r = $results[$s.Key]
    $colour = switch ($r.Status) {
        'RESUMED'     { 'Green' }
        'STARTED'     { 'Green' }
        'UNCONFIRMED' { 'Yellow' }
        default       { 'Red' }
    }
    Write-Host ('{0,-12} {1,-13} {2}' -f $r.Name, $r.Status, $r.Detail) -ForegroundColor $colour
}
Write-Host ('-' * 104)

$outcome = Get-RunOutcome -Results @($results.Values) -RequestedCount $selected.Count `
                          -AllowPartial ([bool]$AllowPartial) -AllowUnconfirmed ([bool]$AllowUnconfirmedResume)

Write-Host ("requested {0} | proven {1} | unconfirmed {2} | silent-new {3} | not-launched {4} | refused {5}" -f `
    $outcome.Requested, $outcome.Proven, $outcome.Unconfirmed, $outcome.SilentNew, $outcome.NotLaunched, $outcome.Refused)
Write-Host "  run artifacts (markers + per-lane launchers): $RunDir" -ForegroundColor DarkGray

$unproven = @($results.Values | Where-Object { $_.Status -notin @('RESUMED','STARTED') })
if ($outcome.Status -notin @('VERIFIED', 'ACCEPTED-WITH-EXCEPTIONS')) {
    foreach ($u in $unproven) { Write-Host ("  unproven: {0} [{1}] {2}" -f $u.Name, $u.Status, $u.Detail) -ForegroundColor Red }
    Write-Host ("{0}: {1}" -f $outcome.Status, $outcome.Message) -ForegroundColor Red
    exit $outcome.ExitCode
}
if ($outcome.Status -eq 'ACCEPTED-WITH-EXCEPTIONS') {
    foreach ($u in $unproven) { Write-Host ("  unproven (accepted): {0} [{1}] {2}" -f $u.Name, $u.Status, $u.Detail) -ForegroundColor Yellow }
    Write-Host ("ACCEPTED-WITH-EXCEPTIONS: {0}" -f $outcome.Message) -ForegroundColor Yellow
    exit $outcome.ExitCode
}
Write-Host ("VERIFIED: {0}" -f $outcome.Message) -ForegroundColor Green
exit $outcome.ExitCode

# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT

<#
.SYNOPSIS
  Fault-injection harness for scripts/onrestart-launch.ps1.

.DESCRIPTION
  Regression cover for two rounds of codexreview findings:

    round 1 (run 20260821T101500Z-rel)
      F1 critical  verification could pass on claude processes this launch did not create
      F2 high      lanes refused before launch left the denominator, so a partial run exited 0
      F3 high      any *.jsonl filename counted as a resumable session
      F4 medium    Start-Process argument serialization could misparse names and paths

    round 2 (run 20260821T123119Z-rel2, against the round-1 fix)
      N1 critical  -AllowPartial / -AllowUnconfirmedResume reached a "VERIFIED" claim
      N2 critical  duplicate lane names collapsed two lanes into one verification identity
      N3 critical  the launcher could start claude in the wrong cwd; markers went unvalidated
      N4 medium    a '^claude' prefix test accepted claudette.exe and claude-malware.exe
      N5 low       per-run launcher/marker directories accumulated without bound

  Every negative case is a FAULT INJECTION, not an absence. F4 reproduces the pre-fix
  corruption on the same argv it then round-trips correctly; N1 drives the real decision
  function through every switch combination including the false-green it used to produce;
  N3 runs the REAL generated launcher against a non-existent directory and asserts it
  refuses to start claude; N4 launches real processes named claude.exe and claudette.exe
  and asserts only the first is attributed.

  Function bodies are extracted from the shipped file via the PowerShell AST, so the tests
  exercise the code that ships, not a copy of it.

.EXAMPLE
  pwsh -NoProfile -File scripts\tests\onrestart-launch.tests.ps1
#>
$ErrorActionPreference = 'Stop'
$src = Join-Path (Split-Path -Parent $PSScriptRoot) 'onrestart-launch.ps1'
if (-not (Test-Path -LiteralPath $src)) { throw "cannot find the script under test at $src" }

$errs = $null; $toks = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($src, [ref]$toks, [ref]$errs)
if ($errs) { throw "parse errors in $src" }
$want = @('ConvertTo-NativeArg','Assert-SafeLaneValue','ConvertTo-ComparablePath','Get-LaneIdentityConflicts',
          'ConvertTo-SessionDirName','Get-SessionFileId','Test-SessionTailIntact','Test-ResumableSessionFile',
          'Get-ProcTable','Get-DescendantProcs','Test-IsClaudeProc','Test-LaneMarker','Get-RunOutcome',
          'New-LaneLauncher','Get-WtCommandLine')
$found = @()
foreach ($f in $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
    if ($want -contains $f.Name) { Invoke-Expression $f.Extent.Text; $found += $f.Name }
}
$missing = @($want | Where-Object { $found -notcontains $_ })
if ($missing) { throw "did not find in $src : $($missing -join ', ')" }

$pass = 0; $fail = 0
function Check([string]$Name, [bool]$Cond, [string]$Got = '') {
    if ($Cond) { $script:pass++; Write-Host ("  PASS  {0}" -f $Name) -ForegroundColor Green }
    else       { $script:fail++; Write-Host ("  FAIL  {0}   got: {1}" -f $Name, $Got) -ForegroundColor Red }
}
$tmp = Join-Path $env:TEMP ('onrestart-test-' + [guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
function Res([string]$Status) { [pscustomobject]@{ Name = $Status; Key = $Status; Status = $Status } }

Write-Host ''
Write-Host 'F4 - native argument quoting' -ForegroundColor Cyan
Check 'plain token is not quoted'        ((ConvertTo-NativeArg 'new-tab') -ceq 'new-tab')            (ConvertTo-NativeArg 'new-tab')
Check 'token with space is quoted'       ((ConvertTo-NativeArg 'my lane') -ceq '"my lane"')          (ConvertTo-NativeArg 'my lane')
Check 'embedded quote is escaped'        ((ConvertTo-NativeArg 'a"b c') -ceq '"a\"b c"')             (ConvertTo-NativeArg 'a"b c')
Check 'trailing backslash is doubled'    ((ConvertTo-NativeArg 'C:\x y\') -ceq '"C:\x y\\"')         (ConvertTo-NativeArg 'C:\x y\')
Check 'interior backslash left alone'    ((ConvertTo-NativeArg 'C:\x y\z') -ceq '"C:\x y\z"')        (ConvertTo-NativeArg 'C:\x y\z')
Check 'empty string is quoted'           ((ConvertTo-NativeArg '') -ceq '""')                        (ConvertTo-NativeArg '')
$threw = $false; try { ConvertTo-NativeArg "a`tb" | Out-Null } catch { $threw = $true }
Check 'control character refuses'        $threw

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class ArgvRT {
  [DllImport("shell32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
  static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);
  public static string[] Split(string cmdline) {
    int n; IntPtr p = CommandLineToArgvW(cmdline, out n);
    if (p == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
    try { var r = new string[n];
      for (int i = 0; i < n; i++) r[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(p, i * IntPtr.Size));
      return r; }
    finally { Marshal.FreeHGlobal(p); }
  }
}
'@ -ErrorAction SilentlyContinue
$hostile = @('new-tab', '--title', 'lane with space', '-d', 'D:\path with space\repo', 'pwsh',
             '-NoExit', '-File', 'D:\a "b"\lane.ps1', 'C:\ends\with\')
$line = ($hostile | ForEach-Object { ConvertTo-NativeArg $_ }) -join ' '
$back = [ArgvRT]::Split('wt.exe ' + $line) | Select-Object -Skip 1
$same = ($back.Count -eq $hostile.Count)
if ($same) { for ($i = 0; $i -lt $hostile.Count; $i++) { if ($back[$i] -cne $hostile[$i]) { $same = $false } } }
Check 'hostile argv round-trips through CommandLineToArgvW' $same ($back -join ' | ')
$naiveBack = [ArgvRT]::Split('wt.exe ' + ($hostile -join ' ')) | Select-Object -Skip 1
Check 'unquoted join DOES corrupt the same argv (defect reproduced)' ($naiveBack.Count -ne $hostile.Count) ("$($naiveBack.Count) tokens vs $($hostile.Count)")

# The real wt command line: separators must survive as BARE tokens, everything else quoted.
$lanes = @(
    [pscustomobject]@{ Name='lane one'; Path='D:\a b\repo'; LauncherPath='D:\runs\lane-00.ps1' },
    [pscustomobject]@{ Name='two';      Path='D:\c\repo';   LauncherPath='D:\runs\lane-01.ps1' }
)
$cl = Get-WtCommandLine -Lanes $lanes -WindowId 'lanes-1'
Check 'wt command line keeps exactly one bare separator per extra tab' (([regex]::Matches($cl, '(^| );( |$)')).Count -eq 1) $cl
Check 'wt command line quotes the spaced lane name'                   ($cl -match '"lane one"') $cl
$wtBack = [ArgvRT]::Split('wt.exe ' + $cl) | Select-Object -Skip 1
Check 'wt argv round-trips with the separator intact'  (($wtBack -contains ';') -and ($wtBack -contains 'lane one') -and ($wtBack -contains 'D:\a b\repo')) ($wtBack -join ' | ')

Write-Host ''
Write-Host 'F3 - session store precheck' -ForegroundColor Cyan
function NewJsonl([string]$name, [string]$content) {
    $p = Join-Path $tmp $name; Set-Content -LiteralPath $p -Value $content -Encoding utf8 -NoNewline; return (Get-Item $p)
}
$zero = Join-Path $tmp 'zero.jsonl'; New-Item -ItemType File -Path $zero -Force | Out-Null
$sess = '{"type":"session","sessionId":"abc-123","mode":"interactive"}'
Check 'zero-byte transcript refused'            (-not (Test-ResumableSessionFile (Get-Item $zero)))
Check 'unparseable transcript refused'          (-not (Test-ResumableSessionFile (NewJsonl 'garbage.jsonl' 'not json at all')))
Check 'blank-lines-only refused'                (-not (Test-ResumableSessionFile (NewJsonl 'blank.jsonl' "`n`n")))
Check 'valid JSON without sessionId refused'    (-not (Test-ResumableSessionFile (NewJsonl 'nosid.jsonl' '{"type":"user"}')))
Check 'bare {} refused'                         (-not (Test-ResumableSessionFile (NewJsonl 'empty-obj.jsonl' '{}')))
Check 'JSON array first line refused'           (-not (Test-ResumableSessionFile (NewJsonl 'array.jsonl' '[1,2,3]')))
Check 'empty sessionId refused'                 (-not (Test-ResumableSessionFile (NewJsonl 'blanksid.jsonl' '{"sessionId":"   "}')))
Check 'numeric sessionId refused'               (-not (Test-ResumableSessionFile (NewJsonl 'numsid.jsonl' '{"sessionId":42}')))
Check 'session header alone accepted'           (Test-ResumableSessionFile (NewJsonl 'header.jsonl' $sess))
Check 'leading blank line tolerated'            (Test-ResumableSessionFile (NewJsonl 'lead.jsonl' ("`n" + $sess)))
Check 'valid header + records accepted'         (Test-ResumableSessionFile (NewJsonl 'full.jsonl' ($sess + "`n" + '{"type":"user","uuid":"u1"}')))
Check 'torn trailing line over real content tolerated' (Test-ResumableSessionFile (NewJsonl 'torn.jsonl' ($sess + "`n" + '{"type":"user","uuid":"u1"}' + "`n" + '{"type":"assist')))
Check 'one torn line after the header tolerated' (Test-ResumableSessionFile (NewJsonl 'headjunk.jsonl' ($sess + "`n" + 'zzzz')))
Check 'session id is extracted'                 ((Get-SessionFileId (NewJsonl 'sid.jsonl' $sess)) -ceq 'abc-123') (Get-SessionFileId (NewJsonl 'sid.jsonl' $sess))

# Beyond the tail window the header is out of reach, so only the tail can vouch for the
# file. A transcript whose last 64 KB holds no parseable record is corrupt, not merely torn.
$bulk = (1..3000 | ForEach-Object { '{"type":"user","uuid":"u' + $_ + '","text":"padpadpadpadpadpadpadpad"}' }) -join "`n"
$bigOk  = NewJsonl 'big-ok.jsonl'  ($sess + "`n" + $bulk)
$bigBad = NewJsonl 'big-bad.jsonl' ($sess + "`n" + $bulk + "`n" + ('z' * 70000))
Check 'large transcript is beyond the tail window'  ($bigOk.Length -gt 65536) "$($bigOk.Length) bytes"
Check 'large transcript with an intact tail accepted' (Test-ResumableSessionFile $bigOk)
Check 'large transcript with a corrupted tail refused' (-not (Test-ResumableSessionFile $bigBad)) "$($bigBad.Length) bytes"

Write-Host ''
Write-Host 'N2 - lane identity uniqueness' -ForegroundColor Cyan
$dupName = @([pscustomobject]@{Name='a';Path='D:\x'}, [pscustomobject]@{Name='a';Path='D:\y'})
$dupPath = @([pscustomobject]@{Name='a';Path='D:\x'}, [pscustomobject]@{Name='b';Path='D:\X\'})
$uniq    = @([pscustomobject]@{Name='a';Path='D:\x'}, [pscustomobject]@{Name='b';Path='D:\y'})
Check 'duplicate lane name detected'                     ((Get-LaneIdentityConflicts $dupName).Count -ge 1)
Check 'duplicate path detected despite case and slash'   ((Get-LaneIdentityConflicts $dupPath).Count -ge 1) ((Get-LaneIdentityConflicts $dupPath) -join '; ')
Check 'unique lanes produce no conflict'                 ((Get-LaneIdentityConflicts $uniq).Count -eq 0)   ((Get-LaneIdentityConflicts $uniq) -join '; ')

Write-Host ''
Write-Host 'N3 - handshake marker validation' -ForegroundColor Cyan
$good = [pscustomobject]@{ key='00-glpnet'; runId='RUN1'; pwshPid=1234; path='D:\repo\glpnet' }
Check 'valid marker accepted'          ($null -eq (Test-LaneMarker $good '00-glpnet' 'RUN1' 'D:\repo\glpnet'))       (Test-LaneMarker $good '00-glpnet' 'RUN1' 'D:\repo\glpnet')
Check 'trailing slash still matches'   ($null -eq (Test-LaneMarker $good '00-glpnet' 'RUN1' 'D:\repo\glpnet\'))      (Test-LaneMarker $good '00-glpnet' 'RUN1' 'D:\repo\glpnet\')
Check 'marker from another lane rejected'  ($null -ne (Test-LaneMarker $good '01-mstack' 'RUN1' 'D:\repo\glpnet'))
Check 'marker from another run rejected'   ($null -ne (Test-LaneMarker $good '00-glpnet' 'RUN2' 'D:\repo\glpnet'))
Check 'marker from another cwd rejected'   ($null -ne (Test-LaneMarker $good '00-glpnet' 'RUN1' 'D:\repo\other'))
Check 'marker without a PID rejected'      ($null -ne (Test-LaneMarker ([pscustomobject]@{key='00-glpnet';runId='RUN1';pwshPid=0;path='D:\repo\glpnet'}) '00-glpnet' 'RUN1' 'D:\repo\glpnet'))
Check 'null marker rejected'               ($null -ne (Test-LaneMarker $null '00-glpnet' 'RUN1' 'D:\repo\glpnet'))

Write-Host ''
Write-Host 'N1 / F2 - final outcome decision' -ForegroundColor Cyan
$allOk = @((Res 'RESUMED'), (Res 'RESUMED'), (Res 'RESUMED'))
$o = Get-RunOutcome -Results $allOk -RequestedCount 3 -AllowPartial $false -AllowUnconfirmed $false
Check 'all proven -> VERIFIED, exit 0'   ($o.Status -eq 'VERIFIED' -and $o.ExitCode -eq 0) "$($o.Status)/$($o.ExitCode)"
$withRef = @((Res 'RESUMED'), (Res 'RESUMED'), (Res 'REFUSED'))
$o = Get-RunOutcome -Results $withRef -RequestedCount 3 -AllowPartial $false -AllowUnconfirmed $false
Check 'refused lane fails the run (F2)'  ($o.Status -eq 'FAILED' -and $o.ExitCode -eq 6) "$($o.Status)/$($o.ExitCode)"
$o = Get-RunOutcome -Results $withRef -RequestedCount 3 -AllowPartial $true -AllowUnconfirmed $false
Check 'accepted refusal is NOT called VERIFIED (N1)' ($o.Status -eq 'ACCEPTED-WITH-EXCEPTIONS' -and $o.ExitCode -eq 0) "$($o.Status)/$($o.ExitCode)"
Check 'accepted refusal reports the honest proven count' ($o.Proven -eq 2 -and $o.Requested -eq 3) "$($o.Proven)/$($o.Requested)"
$withUnc = @((Res 'RESUMED'), (Res 'UNCONFIRMED'))
$o = Get-RunOutcome -Results $withUnc -RequestedCount 2 -AllowPartial $false -AllowUnconfirmed $false
Check 'unconfirmed lane is UNPROVEN, exit 5' ($o.Status -eq 'UNPROVEN' -and $o.ExitCode -eq 5) "$($o.Status)/$($o.ExitCode)"
$o = Get-RunOutcome -Results $withUnc -RequestedCount 2 -AllowPartial $false -AllowUnconfirmed $true
Check 'accepted unconfirmed is NOT VERIFIED (N1)' ($o.Status -eq 'ACCEPTED-WITH-EXCEPTIONS' -and $o.ExitCode -eq 0) "$($o.Status)/$($o.ExitCode)"
$noneProven = @((Res 'REFUSED'), (Res 'REFUSED'))
$o = Get-RunOutcome -Results $noneProven -RequestedCount 2 -AllowPartial $true -AllowUnconfirmed $true
Check 'zero proven can NEVER succeed, even with both switches (N1)' ($o.ExitCode -eq 9 -and $o.Status -eq 'FAILED') "$($o.Status)/$($o.ExitCode)"
$o = Get-RunOutcome -Results @((Res 'RESUMED'), (Res 'SILENT-NEW'), (Res 'REFUSED')) -RequestedCount 3 -AllowPartial $true -AllowUnconfirmed $true
Check 'silent-new outranks every switch'  ($o.Status -eq 'FAILED' -and $o.ExitCode -eq 7) "$($o.Status)/$($o.ExitCode)"
$o = Get-RunOutcome -Results @((Res 'RESUMED'), (Res 'NO-CLAUDE'), (Res 'REFUSED')) -RequestedCount 3 -AllowPartial $true -AllowUnconfirmed $true
Check 'not-launched outranks accepted refusals' ($o.Status -eq 'FAILED' -and $o.ExitCode -eq 4) "$($o.Status)/$($o.ExitCode)"
$o = Get-RunOutcome -Results @((Res 'STARTED'), (Res 'STARTED')) -RequestedCount 2 -AllowPartial $false -AllowUnconfirmed $false
Check '-Fresh STARTED counts as proven'   ($o.Status -eq 'VERIFIED' -and $o.ExitCode -eq 0) "$($o.Status)/$($o.ExitCode)"

Write-Host ''
Write-Host 'N4 / F1 - process attribution (synthetic table)' -ForegroundColor Cyan
function P($id, $parent, $name, $cmd) { [pscustomobject]@{ ProcId=$id; ParentId=$parent; Name=$name; CommandLine=$cmd; Created=(Get-Date) } }
$t = @{}
foreach ($p in @(
    (P 100 1   'pwsh.exe'      'pwsh -File lane-00.ps1'),
    (P 101 100 'claude.exe'    'claude --continue'),
    (P 200 1   'pwsh.exe'      'pwsh -File lane-01.ps1'),
    (P 201 200 'claudette.exe' 'claudette --continue'),
    (P 300 1   'claude.exe'    'claude --continue'),
    (P 400 401 'pwsh.exe'      'loop-a'), (P 401 400 'pwsh.exe' 'loop-b')
)) { $t[$p.ProcId] = $p }
$da = @(Get-DescendantProcs $t 100 | Where-Object { Test-IsClaudeProc $_ $null })
$db = @(Get-DescendantProcs $t 200 | Where-Object { Test-IsClaudeProc $_ $null })
Check 'lane A attributes its own claude'                       ($da.Count -eq 1 -and $da[0].ProcId -eq 101) ($da.ProcId -join ',')
Check 'lane B is NOT satisfied by the unrelated claude 300'    ($db.Count -eq 0)                            ($db.ProcId -join ',')
Check 'lane B is NOT satisfied by its own claudette.exe (N4)'  ($db.Count -eq 0)                            ($db.ProcId -join ',')
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$cyc = @(Get-DescendantProcs $t 400); $sw.Stop()
Check 'parent cycle terminates and yields the cycle members only' (($cyc.Count -eq 2) -and ($sw.Elapsed.TotalSeconds -lt 5)) ("$($cyc.Count) nodes in $([int]$sw.Elapsed.TotalMilliseconds) ms")
Check 'claude.exe detected by exact name'      (Test-IsClaudeProc (P 1 0 'claude.exe' $null) $null)
Check 'claudette.exe rejected (N4)'            (-not (Test-IsClaudeProc (P 1 0 'claudette.exe' 'claudette') $null))
Check 'claude-malware.exe rejected (N4)'       (-not (Test-IsClaudeProc (P 1 0 'claude-malware.exe' 'C:\evil\claude-malware.exe') $null))
Check 'node running the claude cli detected'   (Test-IsClaudeProc (P 1 0 'node.exe' 'node "C:\Users\x\AppData\claude\cli.js" --continue') $null)
Check 'resolved claude path matched literally' (Test-IsClaudeProc (P 1 0 'node.exe' '"C:\Users\g\.local\bin\claude.exe" --continue') 'C:\Users\g\.local\bin\claude.exe')
Check 'plain pwsh lane script NOT claude'      (-not (Test-IsClaudeProc (P 1 0 'pwsh.exe' 'pwsh -NoExit -File C:\runs\lane-00-glpnet.ps1') $null))
Check 'notepad NOT claude'                     (-not (Test-IsClaudeProc (P 1 0 'notepad.exe' 'notepad') $null))

Write-Host ''
Write-Host 'N3 - LIVE: the real generated launcher' -ForegroundColor Cyan
# A stub on PATH stands in for claude, so a regression that DOES reach `& claude` is
# detected by its distinct exit code instead of opening a real interactive session.
$stub = Join-Path $tmp 'stub'; New-Item -ItemType Directory -Path $stub -Force | Out-Null
Set-Content -LiteralPath (Join-Path $stub 'claude.cmd') -Encoding ascii -Value @(
    '@echo off', 'ping -n 60 127.0.0.1 >nul', 'exit /b 3')
$env:PATH = "$stub;$env:PATH"
$runDir = Join-Path $tmp 'run'; New-Item -ItemType Directory -Path $runDir -Force | Out-Null
$RUNID = 'TESTRUN1'
$goodLane = [pscustomobject]@{ Name='good'; Key='00-good'; Path=$tmp;                       Group=1 }
$badLane  = [pscustomobject]@{ Name='bad';  Key='01-bad';  Path=(Join-Path $tmp 'no-such'); Group=1 }
$gp = New-LaneLauncher -Lane $goodLane -RunDir $runDir -RunId $RUNID -ClaudeArgs @('--continue')
$bp = New-LaneLauncher -Lane $badLane  -RunDir $runDir -RunId $RUNID -ClaudeArgs @('--continue')

# Injection: the lane directory does not exist. The launcher must NOT start claude.
$bproc = Start-Process pwsh -PassThru -Wait -WindowStyle Hidden -ArgumentList @('-NoProfile','-File',$bp.LauncherPath)
Check 'bad-cwd launcher exits 9 without starting claude (N3)' ($bproc.ExitCode -eq 9) "exit $($bproc.ExitCode) (3 would mean it reached claude)"
Check 'bad-cwd launcher wrote no handshake marker (N3)'       (-not (Test-Path -LiteralPath $bp.MarkerPath))

$launchStart = Get-Date
$gproc = Start-Process pwsh -PassThru -WindowStyle Hidden -ArgumentList @('-NoProfile','-File',$gp.LauncherPath)
$dl = (Get-Date).AddSeconds(40); $hit = @()
while ((Get-Date) -lt $dl) {
    Start-Sleep -Seconds 2
    if (-not (Test-Path -LiteralPath $gp.MarkerPath)) { continue }
    $m = $null; try { $m = Get-Content -LiteralPath $gp.MarkerPath -Raw | ConvertFrom-Json } catch { continue }
    if (Test-LaneMarker $m '00-good' $RUNID $tmp) { continue }
    $tbl = Get-ProcTable
    $hit = @(Get-DescendantProcs $tbl ([int]$m.pwshPid) | Where-Object {
        (Test-IsClaudeProc $_ (Join-Path $stub 'claude.cmd')) -and (-not $_.Created -or $_.Created -ge $launchStart) })
    if ($hit.Count -ge 1) { break }
}
$marker = $null; if (Test-Path -LiteralPath $gp.MarkerPath) { $marker = Get-Content -LiteralPath $gp.MarkerPath -Raw | ConvertFrom-Json }
Check 'good launcher wrote a marker'                       ($null -ne $marker)
Check 'marker validates against lane, run and cwd (N3)'    ($null -ne $marker -and $null -eq (Test-LaneMarker $marker '00-good' $RUNID $tmp)) (Test-LaneMarker $marker '00-good' $RUNID $tmp)
Check 'marker is rejected for a different run id (N3)'     ($null -ne $marker -and $null -ne (Test-LaneMarker $marker '00-good' 'OTHERRUN' $tmp))
Check 'claude attributed to the good lane handshake (F1)'  ($hit.Count -ge 1) ("pid $($marker.pwshPid) -> $($hit.ProcId -join ',')")
try { Stop-Process -Id $gproc.Id -Force -ErrorAction Stop } catch { }

Write-Host ''
Write-Host 'N4 - LIVE: real processes named claude.exe and claudette.exe' -ForegroundColor Cyan
$ping = Join-Path $env:SystemRoot 'System32\ping.exe'
$realClaude = Join-Path $tmp 'claude.exe'; Copy-Item -LiteralPath $ping -Destination $realClaude -Force
$lookalike  = Join-Path $tmp 'claudette.exe'; Copy-Item -LiteralPath $ping -Destination $lookalike -Force
$launchStart2 = Get-Date
$pc = Start-Process -FilePath $realClaude -PassThru -WindowStyle Hidden -ArgumentList @('-n','30','127.0.0.1')
$pl = Start-Process -FilePath $lookalike  -PassThru -WindowStyle Hidden -ArgumentList @('-n','30','127.0.0.1')
Start-Sleep -Seconds 3
$tbl2 = Get-ProcTable
$cProc = $tbl2[$pc.Id]; $lProc = $tbl2[$pl.Id]
Check 'a real process named claude.exe IS attributed (N4)'     ($null -ne $cProc -and (Test-IsClaudeProc $cProc $null)) ("$($cProc.Name)")
Check 'a real process named claudette.exe is NOT (N4)'         ($null -ne $lProc -and -not (Test-IsClaudeProc $lProc $null)) ("$($lProc.Name)")
foreach ($p in @($pc, $pl)) { try { Stop-Process -Id $p.Id -Force -ErrorAction Stop } catch { } }

Write-Host ''
Write-Host 'F2 - lane value validation' -ForegroundColor Cyan
foreach ($bad in @('a;b', 'a"b', "a`tb", '')) {
    $threw = $false; try { Assert-SafeLaneValue $bad 'lane name' 'x' } catch { $threw = $true }
    Check ("refuses lane value '{0}'" -f ($bad -replace "`t", '<TAB>')) $threw
}
Check 'accepts an ordinary lane name' (& { try { Assert-SafeLaneValue 'glpnet' 'lane name' 'glpnet'; $true } catch { $false } })

Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like "*$tmp*" } |
    ForEach-Object { try { Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop } catch { } }
Start-Sleep -Seconds 1
Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host ''
Write-Host ("RESULT  passed {0}  failed {1}" -f $pass, $fail) -ForegroundColor $(if ($fail) { 'Red' } else { 'Green' })
exit $(if ($fail) { 1 } else { 0 })

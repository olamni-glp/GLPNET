# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT

<#
.SYNOPSIS
  Fault-injection harness for scripts/onrestart-launch.ps1.

.DESCRIPTION
  Regression cover for three rounds of codexreview findings.

    round 1 (20260821T101500Z-rel)
      F1 critical  verification could pass on claude processes this launch did not create
      F2 high      lanes refused before launch left the denominator, so a partial run exited 0
      F3 high      any *.jsonl filename counted as a resumable session
      F4 medium    Start-Process argument serialization could misparse names and paths

    round 2 (20260821T123119Z-rel2)
      N1 critical  -AllowPartial / -AllowUnconfirmedResume reached a "VERIFIED" claim
      N2 critical  duplicate lane names collapsed two lanes into one verification identity
      N3 critical  the launcher could start claude in the wrong cwd; markers went unvalidated
      N4 medium    a '^claude' prefix test accepted claudette.exe and claude-malware.exe
      N5 low       per-run launcher/marker directories accumulated without bound

    round 3 (20260821T125500Z-rel3)
      M1 critical  any executable renamed claude.exe was attributed - AND THIS HARNESS
                   ASSERTED THAT BEHAVIOUR, institutionalizing the hole. The assertion is
                   now inverted: a renamed ping.exe must NOT be attributed.
      M2 critical  a metadata touch counted as proof of resumption
      M3 high      tail validation passed if any one record parsed, however corrupt the rest
      M4 medium    retention pruning could delete a concurrent run's live directory
      M5 low       the tail window dropped a complete record when it began on a boundary

  Every negative case is a FAULT INJECTION, not an absence: the pre-fix behaviour is
  reproduced and asserted to fail. The shipped per-lane state machine (Get-LaneVerification)
  is driven directly through all eleven of its transitions, including which are terminal.

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
          'Get-ResumeEvidence','Get-RepoState','Get-ProcTable','Get-DescendantProcs','Test-IsClaudeProc',
          'Test-LaneMarker','Get-LaneVerification','Remove-StaleRunDirs','Get-RunOutcome',
          'New-LaneLauncher','Get-WtCommandLine','Read-FileRange','Split-TranscriptLines',
          'Test-JsonObjectLine','Get-LaneClaudeEvidence','Get-LastRecordBoundary','ConvertFrom-NativeCommandLine','Get-FileHeadHash')
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
function NewJsonl([string]$name, [string]$content) {
    $p = Join-Path $tmp $name; Set-Content -LiteralPath $p -Value $content -Encoding utf8 -NoNewline; return (Get-Item $p)
}
$SESS = '{"type":"session","sessionId":"abc-123","mode":"interactive"}'
$REC  = '{"type":"user","uuid":"u1","text":"hello"}'

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
$lanes = @(
    [pscustomobject]@{ Name='lane one'; Path='D:\a b\repo'; LauncherPath='D:\runs\lane-00.ps1' },
    [pscustomobject]@{ Name='two';      Path='D:\c\repo';   LauncherPath='D:\runs\lane-01.ps1' }
)
$cl = Get-WtCommandLine -Lanes $lanes -WindowId 'lanes-1'
Check 'wt command line keeps exactly one bare separator per extra tab' (([regex]::Matches($cl, '(^| );( |$)')).Count -eq 1) $cl
$wtBack = [ArgvRT]::Split('wt.exe ' + $cl) | Select-Object -Skip 1
Check 'wt argv round-trips with the separator intact'  (($wtBack -contains ';') -and ($wtBack -contains 'lane one') -and ($wtBack -contains 'D:\a b\repo')) ($wtBack -join ' | ')

Write-Host ''
Write-Host 'F3 / M3 / M5 - transcript validation' -ForegroundColor Cyan
$zero = Join-Path $tmp 'zero.jsonl'; New-Item -ItemType File -Path $zero -Force | Out-Null
Check 'zero-byte transcript refused'            (-not (Test-ResumableSessionFile (Get-Item $zero)))
Check 'unparseable transcript refused'          (-not (Test-ResumableSessionFile (NewJsonl 'garbage.jsonl' 'not json at all')))
Check 'blank-lines-only refused'                (-not (Test-ResumableSessionFile (NewJsonl 'blank.jsonl' "`n`n")))
Check 'valid JSON without sessionId refused'    (-not (Test-ResumableSessionFile (NewJsonl 'nosid.jsonl' '{"type":"user"}')))
Check 'bare {} refused'                         (-not (Test-ResumableSessionFile (NewJsonl 'empty-obj.jsonl' '{}')))
Check 'JSON array first line refused'           (-not (Test-ResumableSessionFile (NewJsonl 'array.jsonl' '[1,2,3]')))
Check 'empty sessionId refused'                 (-not (Test-ResumableSessionFile (NewJsonl 'blanksid.jsonl' '{"sessionId":"   "}')))
Check 'numeric sessionId refused'               (-not (Test-ResumableSessionFile (NewJsonl 'numsid.jsonl' '{"sessionId":42}')))
Check 'session header alone accepted'           (Test-ResumableSessionFile (NewJsonl 'header.jsonl' $SESS))
Check 'leading blank line tolerated'            (Test-ResumableSessionFile (NewJsonl 'lead.jsonl' ("`n" + $SESS)))
Check 'valid header + records accepted'         (Test-ResumableSessionFile (NewJsonl 'full.jsonl' ($SESS + "`n" + $REC)))
Check 'one UNTERMINATED torn tail tolerated'    (Test-ResumableSessionFile (NewJsonl 'torn.jsonl' ($SESS + "`n" + $REC + "`n" + '{"type":"assist')))
Check 'a COMPLETE corrupt line refused (M3)'    (-not (Test-ResumableSessionFile (NewJsonl 'corrupt1.jsonl' ($SESS + "`n" + 'zzzz' + "`n" + $REC))))
Check 'many complete corrupt lines refused (M3)' (-not (Test-ResumableSessionFile (NewJsonl 'corruptN.jsonl' ($SESS + "`n" + (@('zzz') * 20 -join "`n") + "`n" + $REC))))
Check 'session id is extracted'                 ((Get-SessionFileId (NewJsonl 'sid.jsonl' $SESS)) -ceq 'abc-123') (Get-SessionFileId (NewJsonl 'sid.jsonl' $SESS))

# Beyond the tail window the header is out of reach, so only the tail can vouch for the file.
$bulk = (1..3000 | ForEach-Object { '{"type":"user","uuid":"u' + $_ + '","text":"padpadpadpadpadpadpadpad"}' }) -join "`n"
$bigOk  = NewJsonl 'big-ok.jsonl'  ($SESS + "`n" + $bulk)
# Corruption is a COMPLETE line that does not parse. A terminated garbage blob is corruption...
$bigBad = NewJsonl 'big-bad.jsonl' ($SESS + "`n" + $bulk + "`n" + ('z' * 70000) + "`n")
# ...whereas an UNTERMINATED trailing blob is byte-for-byte indistinguishable from a record still
# being written - Claude Code emits records far larger than the window - so refusing it would
# strand a healthy lane. It is tolerated, deliberately, and that choice is asserted here.
$bigTorn = NewJsonl 'big-torn.jsonl' ($SESS + "`n" + $bulk + "`n" + ('z' * 70000))
Check 'large transcript is beyond the tail window'      ($bigOk.Length -gt 65536) "$($bigOk.Length) bytes"
Check 'large transcript with an intact tail accepted'   (Test-ResumableSessionFile $bigOk)
Check 'large transcript with a corrupt COMPLETE tail refused' (-not (Test-ResumableSessionFile $bigBad)) "$($bigBad.Length) bytes"
Check 'an oversized UNTERMINATED tail is tolerated'     (Test-ResumableSessionFile $bigTorn) "$($bigTorn.Length) bytes"

# M5: a window that starts exactly on a newline must keep its first record, not discard it.
$rec80 = '{"type":"user","uuid":"padpadpadpadpadpadpadpadpadpadpadpadpadpadpadpadpadpadpa"}'
$n = [Math]::Ceiling(65536 / ($rec80.Length + 1)) + 2
$aligned = NewJsonl 'aligned.jsonl' ($SESS + "`n" + ((1..$n | ForEach-Object { $rec80 }) -join "`n"))
$boundaryOk = $false
for ($tb = 65536; $tb -lt 65536 + $rec80.Length + 2; $tb++) {
    if (-not (Test-SessionTailIntact $aligned $tb)) { $boundaryOk = $false; break }
    $boundaryOk = $true
}
Check 'every tail-window offset across a record boundary accepts (M5)' $boundaryOk "aligned=$($aligned.Length) bytes"

# A file that vanishes or is exclusively locked must be false, never an exception.
$vanish = NewJsonl 'vanish.jsonl' ($SESS + "`n" + $REC)
$vinfo = Get-Item $vanish.FullName
Remove-Item -LiteralPath $vanish.FullName -Force
$noThrow = $true; $r = $true
try { $r = Test-SessionTailIntact $vinfo } catch { $noThrow = $false }
Check 'vanished file returns false without throwing' ($noThrow -and -not $r)
$locked = NewJsonl 'locked.jsonl' ($SESS + "`n" + $REC)
$lh = [System.IO.File]::Open($locked.FullName, 'Open', 'Read', 'None')
$noThrow = $true; $r = $true
try { $r = Test-SessionTailIntact (Get-Item $locked.FullName) } catch { $noThrow = $false }
$lh.Dispose()
Check 'exclusively locked file returns false without throwing' ($noThrow -and -not $r)

Write-Host ''
Write-Host 'M2 - resume evidence must be an appended record, not a touch' -ForegroundColor Cyan
$ProjectsRoot = Join-Path $tmp 'projects'
$LANEPATH = 'D:\fake\lane'
$storeDir = Join-Path $ProjectsRoot (ConvertTo-SessionDirName $LANEPATH)
New-Item -ItemType Directory -Path $storeDir -Force | Out-Null
$tx = Join-Path $storeDir 'sess-1.jsonl'
Set-Content -LiteralPath $tx -Value ($SESS + "`n" + $REC + "`n") -Encoding utf8 -NoNewline
function LaneState { Get-RepoState ([pscustomobject]@{ Name='fake'; Group=1; Path=$LANEPATH; Key='00-fake' }) }
$st = LaneState
Check 'pre-launch state captured the session id' ($st.LatestSessionId -ceq 'abc-123') $st.LatestSessionId
Check 'no change yields no evidence'             ($null -eq (Get-ResumeEvidence $st))
# INJECTION: a metadata touch, which the previous version accepted as proof.
(Get-Item $tx).LastWriteTimeUtc = (Get-Date).ToUniversalTime().AddMinutes(5)
Check 'a timestamp touch is NOT evidence (M2)'   ($null -eq (Get-ResumeEvidence $st)) ((Get-ResumeEvidence $st).Kind)
# A partial append with no newline yet is not a complete record.
Add-Content -LiteralPath $tx -Value '{"type":"assist' -NoNewline -Encoding utf8
Check 'a partial appended line is NOT evidence'  ($null -eq (Get-ResumeEvidence $st)) ((Get-ResumeEvidence $st).Kind)
# INJECTION (J3): a complete appended record with NO sessionId. Any process with access to the
# transcript could have written it, so it is not attributable to the launched claude and must
# NOT count as proof.
Add-Content -LiteralPath $tx -Value ('ant"}' + "`n") -NoNewline -Encoding utf8
$evU = Get-ResumeEvidence $st
Check 'a sessionId-less append is NOT proof (J3)'  ($null -ne $evU -and $evU.Kind -eq 'UNIDENTIFIED') ("$($evU.Kind)")
Check 'and it is reported, not silently discarded' ($evU.Detail -match 'no record named sessionId') ("$($evU.Detail)")
# A record carrying the EXPECTED sessionId, beginning after the pre-launch length, IS proof.
Add-Content -LiteralPath $tx -Value ('{"type":"assistant","sessionId":"abc-123","uuid":"u2"}' + "`n") -NoNewline -Encoding utf8
$ev = Get-ResumeEvidence $st
Check 'the expected sessionId IS proof'          ($null -ne $ev -and $ev.Kind -eq 'RESUMED' -and $ev.Strength -eq 'expected-session-id') ("$($ev.Kind)/$($ev.Strength)")
# INJECTION (K2): a sessionId-less record FIRST, a foreign sessionId AFTER it. Returning on the
# first parseable record - which an earlier version did - reports RESUMED and never looks.
Set-Content -LiteralPath $tx -Value ($SESS + "`n" + $REC + "`n") -Encoding utf8 -NoNewline
$st2 = LaneState
Add-Content -LiteralPath $tx -Value ('{"type":"assistant","uuid":"u8"}' + "`n" + '{"type":"assistant","sessionId":"OTHER-999","uuid":"u9"}' + "`n") -NoNewline -Encoding utf8
Check 'a LATER foreign sessionId still wins (K2)' ((Get-ResumeEvidence $st2).Kind -eq 'WRONG-SESSION') ((Get-ResumeEvidence $st2).Kind)
# An appended record that is not yet terminated is not a record.
Set-Content -LiteralPath $tx -Value ($SESS + "`n" + $REC + "`n") -Encoding utf8 -NoNewline
$st4 = LaneState
Add-Content -LiteralPath $tx -Value '{"type":"assistant","sessionId":"abc-123","uuid":"u1' -NoNewline -Encoding utf8
Check 'an unterminated appended line is not yet a record' ($null -eq (Get-ResumeEvidence $st4)) ((Get-ResumeEvidence $st4).Kind)

Write-Host ''
Write-Host 'J2 - a record straddling the launch boundary' -ForegroundColor Cyan
# INJECTION: the transcript is captured MID-RECORD, and the bytes appended afterwards complete a
# record carrying a FOREIGN sessionId. Decoding from the raw pre-launch length would split that
# record into an unparseable fragment and never see the foreign id at all.
Set-Content -LiteralPath $tx -Value ($SESS + "`n" + $REC + "`n" + '{"type":"assistant","sessionId":"OTHER-7","uu') -Encoding utf8 -NoNewline
$stj = LaneState
Check 'the boundary is the last complete record, not the length' ($stj.LatestCompleteOffset -lt $stj.LatestLength) "$($stj.LatestCompleteOffset) vs $($stj.LatestLength)"
Add-Content -LiteralPath $tx -Value ('id":"u9"}' + "`n") -NoNewline -Encoding utf8
Check 'a foreign sessionId inside a straddling record is caught (J2)' ((Get-ResumeEvidence $stj).Kind -eq 'WRONG-SESSION') ((Get-ResumeEvidence $stj).Kind)
# And the converse: completing a pre-launch record is NOT itself proof this launch resumed.
Set-Content -LiteralPath $tx -Value ($SESS + "`n" + $REC + "`n" + '{"type":"assistant","sessionId":"abc-123","uu') -Encoding utf8 -NoNewline
$stk = LaneState
Add-Content -LiteralPath $tx -Value ('id":"u9"}' + "`n") -NoNewline -Encoding utf8
Check 'completing a pre-launch record is NOT proof (J2)' ($null -eq (Get-ResumeEvidence $stk)) ((Get-ResumeEvidence $stk).Kind)
Add-Content -LiteralPath $tx -Value ('{"type":"assistant","sessionId":"abc-123","uuid":"u10"}' + "`n") -NoNewline -Encoding utf8
Check 'a record beginning after the launch IS proof (J2)' ((Get-ResumeEvidence $stk).Kind -eq 'RESUMED') ((Get-ResumeEvidence $stk).Kind)
$lrb = NewJsonl 'lrb.jsonl' ($SESS + "`n" + $REC + "`n" + 'partial')
Check 'last-record boundary skips a trailing partial'  ((Get-LastRecordBoundary $lrb) -eq ($lrb.Length - 7)) "$(Get-LastRecordBoundary $lrb) of $($lrb.Length)"
$lrb2 = NewJsonl 'lrb2.jsonl' 'no newline at all'
Check 'a file with no newline has boundary 0'          ((Get-LastRecordBoundary $lrb2) -eq 0) "$(Get-LastRecordBoundary $lrb2)"

Write-Host ''
Write-Host 'H1 - a record longer than the tail window' -ForegroundColor Cyan
# INJECTION: the straddling record is BIGGER than the search window, so a boundary search that
# gives up after one window returns a position in the middle of it - and the foreign sessionId
# inside that record is never decoded.
$huge = '{"type":"assistant","sessionId":"OTHER-BIG","pad":"' + ('x' * 90000)
Set-Content -LiteralPath $tx -Value ($SESS + "`n" + $REC + "`n" + $huge) -Encoding utf8 -NoNewline
$stH = LaneState
Check 'the straddling record exceeds the window' (($stH.LatestLength - $stH.LatestCompleteOffset) -gt 65536) "$($stH.LatestLength - $stH.LatestCompleteOffset) bytes"
Check 'the boundary is a real record boundary'   ($stH.LatestCompleteOffset -gt 0) "$($stH.LatestCompleteOffset)"
Add-Content -LiteralPath $tx -Value ('"}' + "`n") -NoNewline -Encoding utf8
Check 'a foreign sessionId in an oversized straddling record is caught (H1)' ((Get-ResumeEvidence $stH).Kind -eq 'WRONG-SESSION') ((Get-ResumeEvidence $stH).Kind)
$bigLine = NewJsonl 'bigline.jsonl' ($SESS + "`n" + '{"a":"' + ('y' * 90000) + '"}' + "`n" + $REC + "`n")
Check 'a boundary further back than one window is found (H1)' ((Get-LastRecordBoundary $bigLine) -eq $bigLine.Length) "$(Get-LastRecordBoundary $bigLine) of $($bigLine.Length)"
$noNl = NewJsonl 'nonl-big.jsonl' ('z' * 90000)
Check 'a window-sized file with no newline has boundary 0 (H1)' ((Get-LastRecordBoundary $noNl) -eq 0) "$(Get-LastRecordBoundary $noNl)"

Write-Host ''
Write-Host 'H2 / H4 - the CLI match and argument match are not forgeable by substring' -ForegroundColor Cyan
$FAKE = 'C:\Users\g\AppData\npm\claude.cmd'
function Q($cmd, $exe) { [pscustomobject]@{ ProcId=1; ParentId=0; Name='node.exe'; CommandLine=$cmd; ExecutablePath=$exe; Created=(Get-Date) } }
Check 'the real CLI entry point matches'      (Test-IsClaudeProc (Q 'node "C:\np\node_modules\@anthropic-ai\claude-code\cli.js" --continue' 'C:\node.exe') $FAKE @('--continue'))
Check 'a helper under the package dir does NOT (H2)' (-not (Test-IsClaudeProc (Q 'node "C:\np\node_modules\@anthropic-ai\claude-code\helper.js" --continue' 'C:\node.exe') $FAKE @('--continue')))
Check 'a look-alike directory does NOT match (H2)'   (-not (Test-IsClaudeProc (Q 'node "C:\np\claude-codex\cli.js" --continue' 'C:\node.exe') $FAKE @('--continue')))
Check '--continue-helper does NOT satisfy --continue (H4)' (-not (Test-IsClaudeProc (Q 'node "C:\np\node_modules\@anthropic-ai\claude-code\cli.js" --continue-helper' 'C:\node.exe') $FAKE @('--continue')))
Check 'a quoted whole-token argument still matches'  (Test-IsClaudeProc (Q 'node "C:\np\node_modules\@anthropic-ai\claude-code\cli.js" "--continue"' 'C:\node.exe') $FAKE @('--continue'))
Check 'an embedded 1000000 does not satisfy the flag (H4)' (-not (Test-IsClaudeProc (Q 'node "C:\np\node_modules\@anthropic-ai\claude-code\cli.js" --autocompact 10000000' 'C:\node.exe') $FAKE @('--autocompact','1000000')))

Write-Host ''
Write-Host 'G1 / G2 - attribution is about the INVOCATION, not the bytes' -ForegroundColor Cyan
$SHIMP = 'C:\np\claude.cmd'
function W($name, $cmd, $exe) { [pscustomobject]@{ ProcId=1; ParentId=0; Name=$name; CommandLine=$cmd; ExecutablePath=$exe; Created=(Get-Date) } }
Check 'tokenizer: plain tokens'        ((ConvertFrom-NativeCommandLine 'a b c').Count -eq 3)
Check 'tokenizer: quoted token'        (@(ConvertFrom-NativeCommandLine '"a b" c')[0] -ceq 'a b')
Check 'tokenizer: escaped quote'       (@(ConvertFrom-NativeCommandLine 'x \"q\" y').Count -eq 3) (@(ConvertFrom-NativeCommandLine 'x \"q\" y') -join '|')
Check 'tokenizer: cmd packed string'   (@(ConvertFrom-NativeCommandLine 'cmd /c ""p" --continue"')[2] -ceq 'p --continue')
Check 'tokenizer: empty is empty'      ((@(ConvertFrom-NativeCommandLine '')).Count -eq 0)
# Positive control: the command processor running the shim as its /c target.
Check 'cmd running the shim IS attributed'  (Test-IsClaudeProc (W 'cmd.exe' ('cmd /c ""' + $SHIMP + '" --continue"') 'C:\WINDOWS\system32\cmd.exe') $SHIMP @('--continue'))
# INJECTION (G1): an unrelated program carrying the resolved path as DATA in its arguments.
Check 'the resolved path as ARGUMENT DATA is refused (G1)' (-not (Test-IsClaudeProc (W 'notepad.exe' ('notepad "' + $SHIMP + '" --continue') 'C:\WINDOWS\notepad.exe') $SHIMP @('--continue')))
# INJECTION (G1): cmd.exe, but the resolved path is not what it was told to run.
Check 'cmd echoing the path is refused (G1)'  (-not (Test-IsClaudeProc (W 'cmd.exe' ('cmd /c "echo ' + $SHIMP + ' --continue"') 'C:\WINDOWS\system32\cmd.exe') $SHIMP @('--continue')))
# INJECTION (G2): the expected flags packed inside ONE quoted argv element.
Check 'flags packed in one argv element are refused (G2)' (-not (Test-IsClaudeProc (W 'claude.exe' ('claude "payload --continue 1000000"') $SHIMP) $SHIMP @('--continue')))
# INJECTION (G2): flag and value both present but NOT adjacent.
Check 'a non-adjacent flag value is refused (G2)' (-not (Test-IsClaudeProc (W 'claude.exe' 'claude --autocompact 55 --other 1000000' $SHIMP) $SHIMP @('--autocompact','1000000')))
Check 'an adjacent flag value is accepted'       (Test-IsClaudeProc (W 'claude.exe' 'claude --autocompact 1000000' $SHIMP) $SHIMP @('--autocompact','1000000'))

Write-Host ''
Write-Host 'G3 - a same-name replacement is not a continuation' -ForegroundColor Cyan
Set-Content -LiteralPath $tx -Value ($SESS + "`n" + $REC + "`n") -Encoding utf8 -NoNewline
$stG = LaneState
Check 'the snapshot recorded a creation time' ($stG.LatestCreatedUtc -gt [datetime]::MinValue) "$($stG.LatestCreatedUtc)"
# INJECTION: the transcript is deleted and re-created under the SAME NAME. (NTFS tunneling can
# preserve the original creation time, so it is set explicitly to make the test deterministic.)
Remove-Item -LiteralPath $tx -Force
Set-Content -LiteralPath $tx -Value ($SESS + "`n" + $REC + "`n" + '{"type":"assistant","sessionId":"abc-123","uuid":"z"}' + "`n") -Encoding utf8 -NoNewline
(Get-Item $tx).CreationTimeUtc = (Get-Date).ToUniversalTime().AddMinutes(3)
$evG = Get-ResumeEvidence $stG
Check 'a replaced transcript is WRONG-SESSION (G3)' ($null -ne $evG -and $evG.Kind -eq 'WRONG-SESSION') ("$($evG.Kind)")
# INJECTION: same file, but its header now names a different session.
Set-Content -LiteralPath $tx -Value ($SESS + "`n" + $REC + "`n") -Encoding utf8 -NoNewline
$stG2 = LaneState
$other = '{"type":"session","sessionId":"REPLACED-1","mode":"interactive"}'
Set-Content -LiteralPath $tx -Value ($other + "`n" + $REC + "`n" + '{"type":"assistant","uuid":"z"}' + "`n") -Encoding utf8 -NoNewline
(Get-Item $tx).CreationTimeUtc = $stG2.LatestCreatedUtc
$evG2 = Get-ResumeEvidence $stG2
Check 'a rewritten header is WRONG-SESSION (G3)' ($null -ne $evG2 -and $evG2.Kind -eq 'WRONG-SESSION') ("$($evG2.Kind)")
$gone = Join-Path $tmp 'gone.jsonl'
Set-Content -LiteralPath $gone -Value ($SESS + "`n") -Encoding utf8 -NoNewline
$gi = Get-Item $gone; Remove-Item -LiteralPath $gone -Force
Check 'an unreadable snapshot yields -1, never a guessed 0 (G3)' ((Get-LastRecordBoundary $gi) -eq -1) "$(Get-LastRecordBoundary $gi)"

Write-Host ''
Write-Host 'G4 - a zero window must not spin' -ForegroundColor Cyan
$zf = NewJsonl 'zero-win.jsonl' ($SESS + "`n" + $REC + "`n")
$sw2 = [System.Diagnostics.Stopwatch]::StartNew()
$b0 = Get-LastRecordBoundary $zf 0
$t0 = Test-SessionTailIntact $zf 0 $false
$sw2.Stop()
Check 'TailBytes 0 terminates for the boundary search (G4)' (($b0 -eq $zf.Length) -and ($sw2.Elapsed.TotalSeconds -lt 5)) "$b0 in $([int]$sw2.Elapsed.TotalMilliseconds) ms"
Check 'TailBytes 0 terminates for the tail check (G4)'      ($t0 -and ($sw2.Elapsed.TotalSeconds -lt 5)) "$t0"

Write-Host ''
Write-Host 'E1 / E2 - identification names the command being RUN' -ForegroundColor Cyan
$SH = 'C:\np\claude.cmd'
$CLI = 'C:\np\node_modules\@anthropic-ai\claude-code\cli.js'
function V($name, $cmd, $exe) { [pscustomobject]@{ ProcId=1; ParentId=0; Name=$name; CommandLine=$cmd; ExecutablePath=$exe; Created=(Get-Date) } }
Check 'cmd /c "<shim>" IS attributed'                (Test-IsClaudeProc (V 'cmd.exe' ('cmd /c ""' + $SH + '" --continue"') 'C:\WINDOWS\system32\cmd.exe') $SH @('--continue'))
# INJECTION (E1): the path is a REAL argv token of cmd's command string, but echo is what runs.
Check 'cmd /c echo "<shim>" is refused (E1)'         (-not (Test-IsClaudeProc (V 'cmd.exe' ('cmd /c "echo "' + $SH + '" --continue"') 'C:\WINDOWS\system32\cmd.exe') $SH @('--continue')))
Check 'cmd /c echo <shim> unquoted is refused (E1)'  (-not (Test-IsClaudeProc (V 'cmd.exe' ('cmd /c echo "' + $SH + '" --continue') 'C:\WINDOWS\system32\cmd.exe') $SH @('--continue')))
Check 'cmd with no /c at all is refused (E1)'        (-not (Test-IsClaudeProc (V 'cmd.exe' ('cmd "' + $SH + '" --continue') 'C:\WINDOWS\system32\cmd.exe') $SH @('--continue')))
# INJECTION (E2): the CLI entry point carried by a process that is not a JS runtime.
Check 'node running the CLI IS attributed'           (Test-IsClaudeProc (V 'node.exe' ('node "' + $CLI + '" --continue') 'C:\Program Files\nodejs\node.exe') $SH @('--continue'))
Check 'notepad carrying the CLI path is refused (E2)' (-not (Test-IsClaudeProc (V 'notepad.exe' ('notepad "' + $CLI + '" --continue') 'C:\WINDOWS\notepad.exe') $SH @('--continue')))
Check 'a renamed runtime is judged by its image (E2)' (-not (Test-IsClaudeProc (V 'node.exe' ('node "' + $CLI + '" --continue') 'C:\evil\totally-not-node.exe') $SH @('--continue')))

Write-Host ''
Write-Host 'D1 - the CLI must be the script the runtime RUNS' -ForegroundColor Cyan
$CLI2 = 'C:\np\node_modules\@anthropic-ai\claude-code\cli.js'
Check 'node <cli.js> IS attributed'                  (Test-IsClaudeProc (V 'node.exe' ('node "' + $CLI2 + '" --continue') 'C:\Program Files\nodejs\node.exe') $SH @('--continue'))
Check 'node with runtime flags first IS attributed'  (Test-IsClaudeProc (V 'node.exe' ('node --enable-source-maps "' + $CLI2 + '" --continue') 'C:\Program Files\nodejs\node.exe') $SH @('--continue'))
# INJECTION (D1): benign.js is the program being run; the CLI path is merely one of its arguments.
Check 'node benign.js <cli.js> is refused (D1)'      (-not (Test-IsClaudeProc (V 'node.exe' ('node benign.js "' + $CLI2 + '" --continue') 'C:\Program Files\nodejs\node.exe') $SH @('--continue')))
Check 'node with no script at all is refused (D1)'   (-not (Test-IsClaudeProc (V 'node.exe' 'node --continue' 'C:\Program Files\nodejs\node.exe') $SH @('--continue')))
Check 'bun running the CLI IS attributed'            (Test-IsClaudeProc (V 'bun.exe' ('bun "' + $CLI2 + '" --continue') 'C:\bun\bun.exe') $SH @('--continue'))

Write-Host ''
Write-Host 'E4 - the tokenizer against the platform parser' -ForegroundColor Cyan
$cases = @(
    'a b c',
    '"a b" c',
    'C:\path\prog.exe -x "y z"',
    'prog "a\"b" c',
    'prog a\\b c',
    'prog "a\\" b',
    '"C:\Program Files\x\y.exe" --flag "v 1"',
    'prog   spaced   out',
    'prog "" x',
    'C:\np\claude.cmd --continue --autocompact 1000000',
    # argv[0] is parsed by a DIFFERENT rule (backslashes literal, quotes merely delimit). These
    # cases are chosen so the ordinary escape state machine gives a DIFFERENT answer, which is
    # what makes this a differential test rather than a restatement.
    '\\"a b" c',
    '"a\\"b" c',
    'a\\\\"b c',
    'C:\dir\"prog.exe" -x'
)
$diffs = @()
foreach ($c in $cases) {
    $mine = @(ConvertFrom-NativeCommandLine $c)
    $theirs = @([ArgvRT]::Split($c))
    $same = ($mine.Count -eq $theirs.Count)
    if ($same) { for ($i = 0; $i -lt $mine.Count; $i++) { if ($mine[$i] -cne $theirs[$i]) { $same = $false } } }
    if (-not $same) { $diffs += ("[{0}] mine={1} theirs={2}" -f $c, ($mine -join '|'), ($theirs -join '|')) }
}
Check 'tokenizer matches CommandLineToArgvW on every case (E4)' ($diffs.Count -eq 0) ($diffs -join ' ;; ')

Write-Host ''
Write-Host 'E3 - transcript identity survives a timestamp forgery' -ForegroundColor Cyan
Set-Content -LiteralPath $tx -Value ($SESS + "`n" + $REC + "`n") -Encoding utf8 -NoNewline
$stE = LaneState
Check 'the snapshot fingerprinted a fixed head length' (($stE.LatestHeadLen -gt 0) -and $stE.LatestHeadHash) "$($stE.LatestHeadLen)"
# An ordinary append must NOT disturb the fingerprint.
Add-Content -LiteralPath $tx -Value ('{"type":"assistant","sessionId":"abc-123","uuid":"ap"}' + "`n") -NoNewline -Encoding utf8
Check 'an append does not change the fingerprint'      ((Get-ResumeEvidence $stE).Kind -eq 'RESUMED') ((Get-ResumeEvidence $stE).Kind)
# INJECTION (E3): a replacement that FORGES the original creation time - the NTFS file-system
# tunneling case - but whose opening bytes differ.
Set-Content -LiteralPath $tx -Value ($SESS + "`n" + $REC + "`n") -Encoding utf8 -NoNewline
$stE2 = LaneState
$forged = '{"type":"session","sessionId":"abc-123","mode":"forged"}'
Set-Content -LiteralPath $tx -Value ($forged + "`n" + $REC + "`n" + '{"type":"assistant","sessionId":"abc-123","uuid":"q"}' + "`n") -Encoding utf8 -NoNewline
(Get-Item $tx).CreationTimeUtc = $stE2.LatestCreatedUtc
$evE = Get-ResumeEvidence $stE2
Check 'a forged creation time does not defeat identity (E3)' ($null -ne $evE -and $evE.Kind -eq 'WRONG-SESSION') ("$($evE.Kind)")
Check 'and the reason names the opening bytes'             ($evE.Detail -match 'opening bytes') ("$($evE.Detail)")
# INJECTION (E3): the replacement is SHORTER than the snapshot. A transcript being appended to
# never shrinks.
Set-Content -LiteralPath $tx -Value ($SESS + "`n" + $REC + "`n" + $REC + "`n") -Encoding utf8 -NoNewline
$stE3 = LaneState
Set-Content -LiteralPath $tx -Value '{' -Encoding utf8 -NoNewline
(Get-Item $tx).CreationTimeUtc = $stE3.LatestCreatedUtc
Check 'a shorter same-name file is not a continuation (E3)' ($null -eq (Get-ResumeEvidence $stE3) -or (Get-ResumeEvidence $stE3).Kind -eq 'WRONG-SESSION') ((Get-ResumeEvidence $stE3).Kind)

Write-Host ''
Write-Host 'K3 - bounded reads never guess' -ForegroundColor Cyan
$rf = NewJsonl 'range.jsonl' ($SESS + "`n" + $REC + "`n")
Check 'a full range reads whole'            ($null -ne (Read-FileRange $rf.FullName 0 $rf.Length))
Check 'a range past EOF returns null (K3)'  ($null -eq (Read-FileRange $rf.FullName 0 ($rf.Length + 4096)))
Check 'a zero-length range returns null'    ($null -eq (Read-FileRange $rf.FullName 0 0))
Check 'a missing file returns null'         ($null -eq (Read-FileRange (Join-Path $tmp 'nope.jsonl') 0 10))
$sp = Split-TranscriptLines ([System.Text.Encoding]::UTF8.GetBytes("a`nb`n")) 0
Check 'a terminated range reports terminated' ($sp.LastIsTerminated -and $sp.Lines.Count -eq 2) ("$($sp.Lines.Count)/$($sp.LastIsTerminated)")
$sp = Split-TranscriptLines ([System.Text.Encoding]::UTF8.GetBytes("a`nb")) 0
Check 'an unterminated range reports so'      ((-not $sp.LastIsTerminated) -and $sp.Lines.Count -eq 2) ("$($sp.Lines.Count)/$($sp.LastIsTerminated)")

Write-Host ''
Write-Host 'F3 - the bounded check is bounded, and says so' -ForegroundColor Cyan
$midBad = NewJsonl 'mid-bad.jsonl' ($SESS + "`n" + 'CORRUPT-MIDDLE' + "`n" + $bulk)
Check 'mid-file corruption is beyond the window' ($midBad.Length -gt 65536) "$($midBad.Length) bytes"
Check 'bounded validation accepts it (stated bound)'  (Test-ResumableSessionFile $midBad $false)
Check '-DeepValidate catches it (F3)'                 (-not (Test-ResumableSessionFile $midBad $true))
Check '-DeepValidate still accepts a clean transcript' (Test-ResumableSessionFile $bigOk $true)

Write-Host ''
Write-Host 'M1 / N4 - claude attribution is by image, never by name' -ForegroundColor Cyan
$CLAUDE = 'C:\Users\g\.local\bin\claude.exe'
function P($id, $parent, $name, $cmd, $exe) { [pscustomobject]@{ ProcId=$id; ParentId=$parent; Name=$name; CommandLine=$cmd; ExecutablePath=$exe; Created=(Get-Date) } }
Check 'resolved image path is attributed'          (Test-IsClaudeProc (P 1 0 'claude.exe' 'claude --continue' $CLAUDE) $CLAUDE @('--continue'))
Check 'shim invoked by cmd /c is attributed'       (Test-IsClaudeProc (P 1 0 'cmd.exe' ('cmd /c ""' + $CLAUDE + '" --continue"') 'C:\WINDOWS\system32\cmd.exe') $CLAUDE @('--continue'))
Check 'cmd WITHOUT a /c target is refused (E1)'    (-not (Test-IsClaudeProc (P 1 0 'cmd.exe' ('"' + $CLAUDE + '" --continue') 'C:\WINDOWS\system32\cmd.exe') $CLAUDE @('--continue')))
Check 'RENAMED executable named claude.exe is REFUSED (M1)' (-not (Test-IsClaudeProc (P 1 0 'claude.exe' 'claude --continue' 'C:\temp\claude.exe') $CLAUDE @('--continue'))) 'name-only match must not count'
Check 'claudette.exe refused (N4)'                 (-not (Test-IsClaudeProc (P 1 0 'claudette.exe' 'claudette' 'C:\t\claudette.exe') $CLAUDE @()))
Check 'claude-malware.exe refused (N4)'            (-not (Test-IsClaudeProc (P 1 0 'claude-malware.exe' 'C:\evil\claude-malware.exe' 'C:\evil\claude-malware.exe') $CLAUDE @()))
Check 'right image but WRONG args refused'         (-not (Test-IsClaudeProc (P 1 0 'claude.exe' 'claude --fork-session' $CLAUDE) $CLAUDE @('--continue')))
Check 'no resolved path means nothing attributes'  (-not (Test-IsClaudeProc (P 1 0 'claude.exe' 'claude --continue' $CLAUDE) $null @()))
Check 'plain pwsh lane script NOT claude'          (-not (Test-IsClaudeProc (P 1 0 'pwsh.exe' 'pwsh -NoExit -File C:\runs\lane-00-glpnet.ps1' 'C:\pwsh.exe') $CLAUDE @()))

$t = @{}
foreach ($p in @(
    (P 100 1   'pwsh.exe'   'pwsh -File lane-00.ps1' 'C:\pwsh.exe'),
    (P 101 100 'claude.exe' 'claude --continue'      $CLAUDE),
    (P 200 1   'pwsh.exe'   'pwsh -File lane-01.ps1' 'C:\pwsh.exe'),
    (P 201 200 'claude.exe' 'claude --continue'      'C:\temp\claude.exe'),
    (P 300 1   'claude.exe' 'claude --continue'      $CLAUDE),
    (P 400 401 'pwsh.exe'   'loop-a' 'C:\pwsh.exe'), (P 401 400 'pwsh.exe' 'loop-b' 'C:\pwsh.exe')
)) { $t[$p.ProcId] = $p }
$da = @(Get-DescendantProcs $t 100 | Where-Object { Test-IsClaudeProc $_ $CLAUDE @('--continue') })
$db = @(Get-DescendantProcs $t 200 | Where-Object { Test-IsClaudeProc $_ $CLAUDE @('--continue') })
Check 'lane A attributes its own claude'                      ($da.Count -eq 1 -and $da[0].ProcId -eq 101) ($da.ProcId -join ',')
Check 'lane B not satisfied by the unrelated claude 300 (F1)' ($db.Count -eq 0) ($db.ProcId -join ',')
Check 'lane B not satisfied by its own impostor 201 (M1)'     ($db.Count -eq 0) ($db.ProcId -join ',')
$sw = [System.Diagnostics.Stopwatch]::StartNew(); $cyc = @(Get-DescendantProcs $t 400); $sw.Stop()
Check 'parent cycle terminates and yields the cycle members only' (($cyc.Count -eq 2) -and ($sw.Elapsed.TotalSeconds -lt 5)) ("$($cyc.Count) nodes in $([int]$sw.Elapsed.TotalMilliseconds) ms")

Write-Host ''
Write-Host 'M1 - LIVE: a real process renamed claude.exe' -ForegroundColor Cyan
$ping = Join-Path $env:SystemRoot 'System32\ping.exe'
$impostor = Join-Path $tmp 'claude.exe'; Copy-Item -LiteralPath $ping -Destination $impostor -Force
$pl = Start-Process -FilePath $impostor -PassThru -WindowStyle Hidden -ArgumentList @('-n','30','127.0.0.1')
Start-Sleep -Seconds 2
$tbl = Get-ProcTable; $proc = $tbl[$pl.Id]
Check 'the live impostor really is named claude.exe' ($null -ne $proc -and $proc.Name -ieq 'claude.exe') ("$($proc.Name)")
Check 'a live process renamed claude.exe is REFUSED (M1)' ($null -ne $proc -and -not (Test-IsClaudeProc $proc $CLAUDE @())) ("$($proc.ExecutablePath)")
Check 'the same process IS attributed against its own image' ($null -ne $proc -and (Test-IsClaudeProc $proc $impostor @())) ("$($proc.ExecutablePath)")
try { Stop-Process -Id $pl.Id -Force -ErrorAction Stop } catch { }

Write-Host ''
Write-Host 'N2 - lane identity uniqueness' -ForegroundColor Cyan
Check 'duplicate lane name detected'                   ((Get-LaneIdentityConflicts @([pscustomobject]@{Name='a';Path='D:\x'}, [pscustomobject]@{Name='a';Path='D:\y'})).Count -ge 1)
Check 'duplicate path detected despite case and slash' ((Get-LaneIdentityConflicts @([pscustomobject]@{Name='a';Path='D:\x'}, [pscustomobject]@{Name='b';Path='D:\X\'})).Count -ge 1)
Check 'unique lanes produce no conflict'               ((Get-LaneIdentityConflicts @([pscustomobject]@{Name='a';Path='D:\x'}, [pscustomobject]@{Name='b';Path='D:\y'})).Count -eq 0)

Write-Host ''
Write-Host 'N3 - handshake marker validation' -ForegroundColor Cyan
$good = [pscustomobject]@{ key='00-glpnet'; runId='RUN1'; pwshPid=1234; path='D:\repo\glpnet' }
Check 'valid marker accepted'              ($null -eq (Test-LaneMarker $good '00-glpnet' 'RUN1' 'D:\repo\glpnet'))  (Test-LaneMarker $good '00-glpnet' 'RUN1' 'D:\repo\glpnet')
Check 'trailing slash still matches'       ($null -eq (Test-LaneMarker $good '00-glpnet' 'RUN1' 'D:\repo\glpnet\'))
Check 'marker from another lane rejected'  ($null -ne (Test-LaneMarker $good '01-mstack' 'RUN1' 'D:\repo\glpnet'))
Check 'marker from another run rejected'   ($null -ne (Test-LaneMarker $good '00-glpnet' 'RUN2' 'D:\repo\glpnet'))
Check 'marker from another cwd rejected'   ($null -ne (Test-LaneMarker $good '00-glpnet' 'RUN1' 'D:\repo\other'))
Check 'marker without a PID rejected'      ($null -ne (Test-LaneMarker ([pscustomobject]@{key='00-glpnet';runId='RUN1';pwshPid=0;path='D:\repo\glpnet'}) '00-glpnet' 'RUN1' 'D:\repo\glpnet'))
Check 'null marker rejected'               ($null -ne (Test-LaneMarker $null '00-glpnet' 'RUN1' 'D:\repo\glpnet'))

Write-Host ''
Write-Host 'the shipped per-lane state machine' -ForegroundColor Cyan
Set-Content -LiteralPath $tx -Value ($SESS + "`n" + $REC + "`n") -Encoding utf8 -NoNewline
$RUNID = 'SMRUN'
$mPath = Join-Path $tmp 'sm-marker.json'
$lane = [pscustomobject]@{ Name='fake'; Key='00-fake'; Path=$LANEPATH; Group=1; MarkerPath=$mPath; State=(LaneState) }
$LS = (Get-Date).AddMinutes(-1)
function WriteMarker($key, $run, $p, $procId) {
    [pscustomobject]@{ key=$key; runId=$run; path=$p; pwshPid=$procId } | ConvertTo-Json -Compress |
        Set-Content -LiteralPath $mPath -Encoding utf8
}
$smTable = @{}
foreach ($p in @((P 500 1 'pwsh.exe' 'pwsh -File lane.ps1' 'C:\pwsh.exe'))) { $smTable[$p.ProcId] = $p }
function SM([bool]$Fresh = $false) { Get-LaneVerification -Lane $lane -Table $smTable -LaunchStart $LS -RunId $RUNID -ClaudeExe $CLAUDE -ClaudeArgs @('--continue') -Fresh $Fresh }

if (Test-Path $mPath) { Remove-Item $mPath -Force }
$v = SM; Check 'no marker -> PENDING and retried'            ($v.Status -eq 'PENDING' -and -not $v.Terminal) "$($v.Status)/$($v.Terminal)"
Set-Content -LiteralPath $mPath -Value 'not json' -Encoding utf8
$v = SM; Check 'unreadable marker -> PENDING and retried'    ($v.Status -eq 'PENDING' -and -not $v.Terminal) "$($v.Status)/$($v.Terminal)"
WriteMarker '00-fake' 'WRONGRUN' $LANEPATH 500
$v = SM; Check 'foreign run marker -> NOT-LAUNCHED TERMINAL' ($v.Status -eq 'NOT-LAUNCHED' -and $v.Terminal) "$($v.Status)/$($v.Terminal)"
WriteMarker '00-fake' $RUNID 'D:\somewhere\else' 500
$v = SM; Check 'foreign cwd marker -> NOT-LAUNCHED TERMINAL' ($v.Status -eq 'NOT-LAUNCHED' -and $v.Terminal) "$($v.Status)/$($v.Terminal)"
WriteMarker '00-fake' $RUNID $LANEPATH 999999
$v = SM; Check 'dead handshake PID -> NOT-LAUNCHED, retried' ($v.Status -eq 'NOT-LAUNCHED' -and -not $v.Terminal) "$($v.Status)/$($v.Terminal)"
WriteMarker '00-fake' $RUNID $LANEPATH 500
$smTable[500].Created = (Get-Date).AddHours(-2)
$v = SM; Check 'pre-launch handshake PID -> NOT-LAUNCHED, retried' ($v.Status -eq 'NOT-LAUNCHED' -and -not $v.Terminal) "$($v.Status)/$($v.Terminal)"
$smTable[500].Created = (Get-Date)
$v = SM; Check 'live handshake, no claude -> NO-CLAUDE, retried' ($v.Status -eq 'NO-CLAUDE' -and -not $v.Terminal) "$($v.Status)/$($v.Terminal)"
$c = P 501 500 'claude.exe' 'claude --continue' $CLAUDE; $smTable[501] = $c
$v = SM $true; Check '-Fresh with claude -> STARTED TERMINAL'   ($v.Status -eq 'STARTED' -and $v.Terminal -and $v.ClaudePid -eq 501) "$($v.Status)/$($v.Terminal)"
$v = SM; Check 'claude live, nothing appended -> UNCONFIRMED, retried' ($v.Status -eq 'UNCONFIRMED' -and -not $v.Terminal) "$($v.Status)/$($v.Terminal)"
# INJECTION (H3/J3): the transcript IS being written, but nothing names the session. That is not
# proof, and the lane must stay UNCONFIRMED rather than being promoted to RESUMED.
Add-Content -LiteralPath $tx -Value ('{"type":"assistant","uuid":"u7"}' + "`n") -NoNewline -Encoding utf8
$v = SM; Check 'an unidentified append stays UNCONFIRMED (H3)' ($v.Status -eq 'UNCONFIRMED' -and -not $v.Terminal) "$($v.Status)/$($v.Terminal)"
Check 'and the reason names the missing sessionId'  ($v.Detail -match 'no record named sessionId') "$($v.Detail)"
Add-Content -LiteralPath $tx -Value ('{"type":"assistant","sessionId":"abc-123","uuid":"u2"}' + "`n") -NoNewline -Encoding utf8
$v = SM; Check 'appended record -> RESUMED TERMINAL'            ($v.Status -eq 'RESUMED' -and $v.Terminal) "$($v.Status)/$($v.Terminal)"
Set-Content -LiteralPath (Join-Path $storeDir 'sess-2.jsonl') -Value ($SESS + "`n") -Encoding utf8 -NoNewline
$v = SM; Check 'a brand new transcript -> SILENT-NEW TERMINAL'  ($v.Status -eq 'SILENT-NEW' -and $v.Terminal) "$($v.Status)/$($v.Terminal)"
Remove-Item (Join-Path $storeDir 'sess-2.jsonl') -Force
Set-Content -LiteralPath $tx -Value ($SESS + "`n" + $REC + "`n") -Encoding utf8 -NoNewline
$lane.State = LaneState
Add-Content -LiteralPath $tx -Value ('{"type":"assistant","sessionId":"OTHER-1","uuid":"u9"}' + "`n") -NoNewline -Encoding utf8
$v = SM; Check 'foreign sessionId appended -> SILENT-NEW TERMINAL' ($v.Status -eq 'SILENT-NEW' -and $v.Terminal) "$($v.Status)/$($v.Terminal)"

Write-Host ''
Write-Host 'N1 / F2 - final outcome decision' -ForegroundColor Cyan
$o = Get-RunOutcome -Results @((Res 'RESUMED'), (Res 'RESUMED'), (Res 'RESUMED')) -RequestedCount 3 -AllowPartial $false -AllowUnconfirmed $false
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
$o = Get-RunOutcome -Results @((Res 'REFUSED'), (Res 'REFUSED')) -RequestedCount 2 -AllowPartial $true -AllowUnconfirmed $true
Check 'zero proven can NEVER succeed, even with both switches (N1)' ($o.ExitCode -eq 9 -and $o.Status -eq 'FAILED') "$($o.Status)/$($o.ExitCode)"
$o = Get-RunOutcome -Results @((Res 'RESUMED'), (Res 'PENDING')) -RequestedCount 2 -AllowPartial $true -AllowUnconfirmed $true
Check 'a lane still PENDING at the deadline fails' ($o.Status -eq 'FAILED' -and $o.ExitCode -eq 4) "$($o.Status)/$($o.ExitCode)"
$o = Get-RunOutcome -Results @((Res 'RESUMED'), (Res 'SILENT-NEW'), (Res 'REFUSED')) -RequestedCount 3 -AllowPartial $true -AllowUnconfirmed $true
Check 'silent-new outranks every switch'  ($o.Status -eq 'FAILED' -and $o.ExitCode -eq 7) "$($o.Status)/$($o.ExitCode)"
$o = Get-RunOutcome -Results @((Res 'RESUMED'), (Res 'NO-CLAUDE'), (Res 'REFUSED')) -RequestedCount 3 -AllowPartial $true -AllowUnconfirmed $true
Check 'not-launched outranks accepted refusals' ($o.Status -eq 'FAILED' -and $o.ExitCode -eq 4) "$($o.Status)/$($o.ExitCode)"
$o = Get-RunOutcome -Results @((Res 'STARTED'), (Res 'STARTED')) -RequestedCount 2 -AllowPartial $false -AllowUnconfirmed $false
Check '-Fresh STARTED counts as proven'   ($o.Status -eq 'VERIFIED' -and $o.ExitCode -eq 0) "$($o.Status)/$($o.ExitCode)"
# Exhaustive: no combination of statuses and switches may reach VERIFIED without full proof.
$statuses = @('RESUMED','STARTED','UNCONFIRMED','SILENT-NEW','NOT-LAUNCHED','NO-CLAUDE','REFUSED','PENDING')
$bad = @()
foreach ($a in $statuses) { foreach ($b in $statuses) { foreach ($ap in @($true,$false)) { foreach ($au in @($true,$false)) {
    $oo = Get-RunOutcome -Results @((Res $a), (Res $b)) -RequestedCount 2 -AllowPartial $ap -AllowUnconfirmed $au
    $fullyProven = ($a -in @('RESUMED','STARTED')) -and ($b -in @('RESUMED','STARTED'))
    if ($oo.Status -eq 'VERIFIED' -and -not $fullyProven) { $bad += "$a+$b ap=$ap au=$au" }
    if ($oo.ExitCode -eq 0 -and $oo.Proven -eq 0)         { $bad += "exit0 with 0 proven: $a+$b ap=$ap au=$au" }
} } } }
Check 'exhaustive: VERIFIED only when every lane is proven (N1)' ($bad.Count -eq 0) ($bad -join '; ')

Write-Host ''
Write-Host 'M4 - retention must not delete a live run' -ForegroundColor Cyan
$runsRoot = Join-Path $tmp 'runs'; New-Item -ItemType Directory -Path $runsRoot -Force | Out-Null
$dirs = @()
foreach ($i in 1..5) { $d = Join-Path $runsRoot ("run-$i"); New-Item -ItemType Directory -Path $d -Force | Out-Null; $dirs += $d; Start-Sleep -Milliseconds 40 }
# run-1 is the oldest; claim it with THIS live process, and give run-2 a dead owner.
$myTicks = (Get-Process -Id $PID).StartTime.ToUniversalTime().Ticks
function WriteLock($dir, $lockPid, $ticks) {
    [pscustomobject]@{ pid = $lockPid; startedTicks = $ticks } | ConvertTo-Json -Compress |
        Set-Content -LiteralPath (Join-Path $dir 'active.lock') -Encoding utf8
}
WriteLock $dirs[0] $PID $myTicks                       # genuinely live owner
WriteLock $dirs[1] 999999 $myTicks                     # owner process is gone
# INJECTION (K4): the PID is live but is NOT the process that took the lock - a reused PID.
# The delta is ONE SECOND: deliberately INSIDE any plausible "close enough" tolerance, so a
# comparison that is a window rather than an identity check fails this test.
WriteLock $dirs[2] $PID ($myTicks - 10000000)
Set-Content -LiteralPath (Join-Path $dirs[3] 'active.lock') -Value 'not json' -Encoding utf8
$pr = Remove-StaleRunDirs -RunsRoot $runsRoot -KeepRuns 1 -CurrentRunDir $dirs[4]
Check 'a live-locked run dir is NOT deleted (M4)'    (Test-Path -LiteralPath $dirs[0]) ($pr.Removed -join '; ')
Check 'a dead-owner run dir IS deleted'              (-not (Test-Path -LiteralPath $dirs[1]))
Check 'a REUSED pid does not inherit the claim (K4)' (-not (Test-Path -LiteralPath $dirs[2])) 'same pid, different start ticks must not read as live'
Check 'an unreadable lock is never deleted on a guess' (Test-Path -LiteralPath $dirs[3])
Check 'the current run dir is never deleted'          (Test-Path -LiteralPath $dirs[4])
Check 'a missing runs root is not an error'           ((Remove-StaleRunDirs -RunsRoot (Join-Path $tmp 'no-such-root') -KeepRuns 3 -CurrentRunDir $tmp).Removed.Count -eq 0)
# The OS-level guard: a run dir whose lock is held OPEN cannot be deleted at all.
$heldDir = Join-Path $runsRoot 'held'; New-Item -ItemType Directory -Path $heldDir -Force | Out-Null
WriteLock $heldDir 999999 0
$held = [System.IO.File]::Open((Join-Path $heldDir 'active.lock'), 'Open', 'Write', 'Read')
$pr2 = Remove-StaleRunDirs -RunsRoot $runsRoot -KeepRuns 1 -CurrentRunDir $dirs[4]
Check 'an open lock handle blocks deletion outright (M4)' (Test-Path -LiteralPath $heldDir) ($pr2.Removed -join '; ')
$held.Dispose()

Write-Host ''
Write-Host 'N3 - LIVE: the real generated launcher' -ForegroundColor Cyan
$stub = Join-Path $tmp 'stub'; New-Item -ItemType Directory -Path $stub -Force | Out-Null
Set-Content -LiteralPath (Join-Path $stub 'claude.cmd') -Encoding ascii -Value @('@echo off', 'ping -n 60 127.0.0.1 >nul', 'exit /b 3')
$env:PATH = "$stub;$env:PATH"
$stubClaude = Join-Path $stub 'claude.cmd'
$runDir = Join-Path $tmp 'run'; New-Item -ItemType Directory -Path $runDir -Force | Out-Null
$LRUN = 'TESTRUN1'
# A lane path containing a single quote: the launcher embeds it in a single-quoted literal.
$quoteDir = Join-Path $tmp "it's a lane"; New-Item -ItemType Directory -Path $quoteDir -Force | Out-Null
$goodLane = [pscustomobject]@{ Name='good'; Key='00-good'; Path=$quoteDir;                   Group=1 }
$badLane  = [pscustomobject]@{ Name='bad';  Key='01-bad';  Path=(Join-Path $tmp 'no-such'); Group=1 }
$gp = New-LaneLauncher -Lane $goodLane -RunDir $runDir -RunId $LRUN -ClaudePath $stubClaude -ClaudeArgs @('--continue')
$bp = New-LaneLauncher -Lane $badLane  -RunDir $runDir -RunId $LRUN -ClaudePath $stubClaude -ClaudeArgs @('--continue')
$lerr = $null; $ltok = $null
$null = [System.Management.Automation.Language.Parser]::ParseFile($gp.LauncherPath, [ref]$ltok, [ref]$lerr)
Check 'generated launcher parses with a quoted path' ($null -eq $lerr -or $lerr.Count -eq 0) (($lerr | ForEach-Object { $_.Message }) -join '; ')

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
    if (Test-LaneMarker $m '00-good' $LRUN $quoteDir) { continue }
    $hit = @(Get-DescendantProcs (Get-ProcTable) ([int]$m.pwshPid) | Where-Object {
        (Test-IsClaudeProc $_ $stubClaude @('--continue')) -and (-not $_.Created -or $_.Created -ge $launchStart) })
    if ($hit.Count -ge 1) { break }
}
$marker = $null; if (Test-Path -LiteralPath $gp.MarkerPath) { $marker = Get-Content -LiteralPath $gp.MarkerPath -Raw | ConvertFrom-Json }
Check 'good launcher wrote a marker in a quoted path'   ($null -ne $marker)
Check 'marker validates against lane, run and cwd (N3)' ($null -ne $marker -and $null -eq (Test-LaneMarker $marker '00-good' $LRUN $quoteDir)) (Test-LaneMarker $marker '00-good' $LRUN $quoteDir)
Check 'marker is rejected for a different run id (N3)'  ($null -ne $marker -and $null -ne (Test-LaneMarker $marker '00-good' 'OTHERRUN' $quoteDir))
Check 'claude attributed to the good lane handshake (F1)' ($hit.Count -ge 1) ("pid $($marker.pwshPid) -> $($hit.ProcId -join ',')")
try { Stop-Process -Id $gproc.Id -Force -ErrorAction Stop } catch { }

Write-Host ''
Write-Host 'K1 / J1 - attribution identifies claude, and nothing else' -ForegroundColor Cyan
$SHIM = 'C:\Users\g\AppData\npm\claude.cmd'
$k1 = @{}
foreach ($p in @(
    (P 700 1   'pwsh.exe' 'pwsh -NoProfile -NoExit -File C:\runs\lane-00.ps1' 'C:\pwsh.exe'),
    (P 701 700 'node.exe' 'node "C:\Users\g\AppData\npm\node_modules\@anthropic-ai\claude-code\cli.js" --continue' 'C:\Program Files\nodejs\node.exe'),
    (P 800 1   'pwsh.exe' 'pwsh -NoProfile -NoExit -File C:\runs\lane-01.ps1' 'C:\pwsh.exe'),
    (P 801 800 'ping.exe' 'ping -t 127.0.0.1' 'C:\WINDOWS\system32\PING.EXE')
)) { $k1[$p.ProcId] = $p }
$LS2 = (Get-Date).AddMinutes(-1)
# A node process running the Claude Code entry point is matched on the CLI's own module path.
$ev1 = Get-LaneClaudeEvidence -Table $k1 -LanePid 700 -ClaudeExe $SHIM -ClaudeArgs @('--continue') -LaunchStart $LS2
Check 'node running the CLI entry point IS attributed (K1)' ($null -ne $ev1 -and $ev1.Strength -eq 'image' -and $ev1.Proc.ProcId -eq 701) ("$($ev1.Strength)")
# INJECTION (J1): the lane launcher has a live descendant that is NOT claude - the shape a
# PowerShell profile child or a shim's helper would take. "Something is alive" is not proof.
$ev2 = Get-LaneClaudeEvidence -Table $k1 -LanePid 800 -ClaudeExe $SHIM -ClaudeArgs @('--continue') -LaunchStart $LS2
Check 'an unrelated live descendant is NOT attributed (J1)' ($null -eq $ev2) ("$($ev2.Strength) pid $($ev2.Proc.ProcId)")
Check 'the unrelated descendant really is a live descendant' ((@(Get-DescendantProcs $k1 800)).Count -eq 1) 'the injection must actually be present'
$k1[900] = (P 900 1 'pwsh.exe' 'pwsh -NoProfile -File lane.ps1' 'C:\pwsh.exe')
$k1[901] = (P 901 900 'claude.exe' 'claude --continue' $CLAUDE)
$ev3 = Get-LaneClaudeEvidence -Table $k1 -LanePid 900 -ClaudeExe $CLAUDE -ClaudeArgs @('--continue') -LaunchStart $LS2
Check 'a direct install is attributed by image'          ($null -ne $ev3 -and $ev3.Strength -eq 'image') ("$($ev3.Strength)")
$k1[901].Created = (Get-Date).AddHours(-3)
$ev4 = Get-LaneClaudeEvidence -Table $k1 -LanePid 900 -ClaudeExe $CLAUDE -ClaudeArgs @('--continue') -LaunchStart $LS2
Check 'a pre-launch descendant does not attribute'       ($null -eq $ev4) ("$($ev4.Strength)")
Check 'the CLI entry point matches by content, not name' (Test-IsClaudeProc $k1[701] $SHIM @('--continue'))
Check 'a ping descendant never matches'                 (-not (Test-IsClaudeProc $k1[801] $SHIM @()))
# The tab's pwsh must not load a user profile - that is what removes the J1 vector at source.
$wtLine = Get-WtCommandLine -Lanes @([pscustomobject]@{ Name='x'; Path='D:\x'; LauncherPath='D:\l.ps1' }) -WindowId '0'
Check 'the wt command line passes -NoProfile (J1)'      ($wtLine -match '(^| )-NoProfile( |$)') $wtLine

Write-Host ''
Write-Host 'M5 - multibyte UTF-8 across the tail-window boundary' -ForegroundColor Cyan
$mb = '{"type":"user","uuid":"' + ('éüあ好' * 12) + '"}'
$mbCount = [Math]::Ceiling(65536 / ([System.Text.Encoding]::UTF8.GetByteCount($mb) + 1)) + 3
$mbFile = NewJsonl 'multibyte.jsonl' ($SESS + "`n" + ((1..$mbCount | ForEach-Object { $mb }) -join "`n"))
Check 'the multibyte transcript exceeds the window' ($mbFile.Length -gt 65536) "$($mbFile.Length) bytes"
$mbOk = $true; $mbFirstBad = ''
foreach ($tb in 65530..65560) {
    if (-not (Test-SessionTailIntact $mbFile $tb $false)) { $mbOk = $false; $mbFirstBad = "TailBytes=$tb"; break }
}
Check 'every window offset through multibyte characters accepts (M5)' $mbOk $mbFirstBad

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

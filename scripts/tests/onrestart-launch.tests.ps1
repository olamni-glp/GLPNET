# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT

<#
.SYNOPSIS
  Fault-injection harness for scripts/onrestart-launch.ps1.

.DESCRIPTION
  Regression cover for the four defects raised by codexreview run 20260821T101500Z-rel:
    F1 (critical) verification could pass on claude processes this launch did not create
    F2 (high)     lanes refused before launch left the denominator, so a partial run exited 0
    F3 (high)     any *.jsonl filename counted as a resumable session
    F4 (medium)   Start-Process argument serialization could misparse names and paths

  Every negative case is a FAULT INJECTION, not an absence: F1 proves a live, unrelated
  claude process does NOT satisfy a lane, and F4 reproduces the pre-fix corruption on the
  same argv it then round-trips correctly. Function bodies are extracted from the shipped
  file via the PowerShell AST, so the tests exercise the code that ships, not a copy.

.EXAMPLE
  pwsh -NoProfile -File scripts\tests\onrestart-launch.tests.ps1
#>
$ErrorActionPreference = 'Stop'
$src = Join-Path (Split-Path -Parent $PSScriptRoot) 'onrestart-launch.ps1'
if (-not (Test-Path -LiteralPath $src)) { throw "cannot find the script under test at $src" }

$errs = $null; $toks = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($src, [ref]$toks, [ref]$errs)
if ($errs) { throw "parse errors in $src" }
$want = @('ConvertTo-NativeArg','Assert-SafeLaneValue','Test-ResumableSessionFile',
          'Get-ProcTable','Get-DescendantProcs','Test-IsClaudeProc','ConvertTo-SessionDirName')
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

# Round-trip through the real Win32 parser: what wt.exe would receive must equal what we meant.
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

# The pre-fix code path, for contrast: Start-Process joins with plain spaces.
$naive = $hostile -join ' '
$naiveBack = [ArgvRT]::Split('wt.exe ' + $naive) | Select-Object -Skip 1
Check 'unquoted join DOES corrupt the same argv (defect reproduced)' ($naiveBack.Count -ne $hostile.Count) ("$($naiveBack.Count) tokens vs $($hostile.Count)")

Write-Host ''
Write-Host 'F3 - session store precheck' -ForegroundColor Cyan
$tmp = Join-Path $env:TEMP ('onrestart-test-' + [guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
$zero    = Join-Path $tmp 'zero.jsonl';    New-Item -ItemType File -Path $zero -Force | Out-Null
$garbage = Join-Path $tmp 'garbage.jsonl'; Set-Content -LiteralPath $garbage -Value 'not json at all' -Encoding utf8
$blank   = Join-Path $tmp 'blank.jsonl';   Set-Content -LiteralPath $blank -Value "`n`n" -Encoding utf8
$good    = Join-Path $tmp 'good.jsonl';    Set-Content -LiteralPath $good -Value '{"type":"user","uuid":"x"}' -Encoding utf8
$lead    = Join-Path $tmp 'lead.jsonl';    Set-Content -LiteralPath $lead -Value "`n{`"type`":`"user`"}" -Encoding utf8
Check 'zero-byte transcript refused'      (-not (Test-ResumableSessionFile (Get-Item $zero)))
Check 'unparseable transcript refused'    (-not (Test-ResumableSessionFile (Get-Item $garbage)))
Check 'blank-lines-only refused'          (-not (Test-ResumableSessionFile (Get-Item $blank)))
Check 'valid transcript accepted'         (Test-ResumableSessionFile (Get-Item $good))
Check 'leading blank line tolerated'      (Test-ResumableSessionFile (Get-Item $lead))

Write-Host ''
Write-Host 'F1 - process attribution (synthetic table)' -ForegroundColor Cyan
function P($id, $parent, $name, $cmd) { [pscustomobject]@{ ProcId=$id; ParentId=$parent; Name=$name; CommandLine=$cmd; Created=(Get-Date) } }
$t = @{}
foreach ($p in @(
    (P 100 1   'pwsh.exe'   'pwsh -File lane-a.ps1'),
    (P 101 100 'claude.exe' 'claude --continue'),
    (P 200 1   'pwsh.exe'   'pwsh -File lane-b.ps1'),
    (P 201 200 'node.exe'   'node C:\tools\other\index.js'),
    (P 300 1   'claude.exe' 'claude --continue'),
    (P 400 401 'pwsh.exe'   'loop-a'), (P 401 400 'pwsh.exe' 'loop-b')
)) { $t[$p.ProcId] = $p }
$da = @(Get-DescendantProcs $t 100 | Where-Object { Test-IsClaudeProc $_ })
$db = @(Get-DescendantProcs $t 200 | Where-Object { Test-IsClaudeProc $_ })
Check 'lane A attributes its own claude'                    ($da.Count -eq 1 -and $da[0].ProcId -eq 101) ($da.ProcId -join ',')
Check 'lane B is NOT satisfied by the unrelated claude 300'  ($db.Count -eq 0)                            ($db.ProcId -join ',')
$null = Get-DescendantProcs $t 400
Check 'parent cycle does not hang the walk'                 $true
Check 'claude.exe detected by name'          (Test-IsClaudeProc (P 1 0 'claude.exe' $null))
Check 'node running claude cli detected'     (Test-IsClaudeProc (P 1 0 'node.exe' 'node "C:\Users\x\AppData\claude\cli.js" --continue'))
Check 'plain pwsh lane script NOT claude'    (-not (Test-IsClaudeProc (P 1 0 'pwsh.exe' 'pwsh -NoExit -File C:\runs\lane-glpnet.ps1')))
Check 'notepad NOT claude'                   (-not (Test-IsClaudeProc (P 1 0 'notepad.exe' 'notepad')))

Write-Host ''
Write-Host 'F1 - live end-to-end handshake + attribution' -ForegroundColor Cyan
$fake = Join-Path $tmp 'claude.ps1'
Set-Content -LiteralPath $fake -Value 'Start-Sleep -Seconds 60' -Encoding utf8
$markerA = Join-Path $tmp 'marker-a.json'
$markerB = Join-Path $tmp 'marker-b.json'
# Lane A: writes its handshake marker, then execs a claude-named child (the good case).
Set-Content -LiteralPath (Join-Path $tmp 'laneA.ps1') -Encoding utf8 -Value @"
[pscustomobject]@{ lane='A'; pwshPid=`$PID } | ConvertTo-Json -Compress | Set-Content -LiteralPath '$markerA' -Encoding utf8
& pwsh -NoProfile -File '$fake'
"@
# Lane B: writes its marker, then runs NOTHING — the "tab opened but ran nothing" failure.
Set-Content -LiteralPath (Join-Path $tmp 'laneB.ps1') -Encoding utf8 -Value @"
[pscustomobject]@{ lane='B'; pwshPid=`$PID } | ConvertTo-Json -Compress | Set-Content -LiteralPath '$markerB' -Encoding utf8
Start-Sleep -Seconds 60
"@
$launchStart = Get-Date
$pA = Start-Process pwsh -PassThru -WindowStyle Hidden -ArgumentList @('-NoProfile','-File',(Join-Path $tmp 'laneA.ps1'))
$pB = Start-Process pwsh -PassThru -WindowStyle Hidden -ArgumentList @('-NoProfile','-File',(Join-Path $tmp 'laneB.ps1'))
$deadline = (Get-Date).AddSeconds(30); $aOk = $false
while ((Get-Date) -lt $deadline -and -not $aOk) {
    Start-Sleep -Seconds 2
    if (-not (Test-Path $markerA) -or -not (Test-Path $markerB)) { continue }
    $tbl = Get-ProcTable
    $pidA = [int]((Get-Content $markerA -Raw | ConvertFrom-Json).pwshPid)
    $aOk = @(Get-DescendantProcs $tbl $pidA | Where-Object { (Test-IsClaudeProc $_) -and (-not $_.Created -or $_.Created -ge $launchStart) }).Count -ge 1
}
$tbl  = Get-ProcTable
$pidA = [int]((Get-Content $markerA -Raw | ConvertFrom-Json).pwshPid)
$pidB = [int]((Get-Content $markerB -Raw | ConvertFrom-Json).pwshPid)
$hitA = @(Get-DescendantProcs $tbl $pidA | Where-Object { (Test-IsClaudeProc $_) -and (-not $_.Created -or $_.Created -ge $launchStart) })
$hitB = @(Get-DescendantProcs $tbl $pidB | Where-Object { (Test-IsClaudeProc $_) -and (-not $_.Created -or $_.Created -ge $launchStart) })
Check 'live lane A: handshake marker written'          (Test-Path $markerA)
Check 'live lane A: claude attributed to its own PID'  ($hitA.Count -ge 1) ("pid $pidA -> $($hitA.ProcId -join ',')")
Check 'live lane B: NOT satisfied by lane A''s claude' ($hitB.Count -eq 0) ("pid $pidB -> $($hitB.ProcId -join ',')")
Check 'live lane B: handshake PID is alive (tab ran, command did not)' ($null -ne $tbl[$pidB])
foreach ($p in @($pA, $pB)) { try { Stop-Process -Id $p.Id -Force -ErrorAction Stop } catch {} }
Start-Sleep -Seconds 1
Get-CimInstance Win32_Process -Filter "Name='pwsh.exe'" | Where-Object { $_.CommandLine -like "*$tmp*" } |
    ForEach-Object { try { Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop } catch {} }

Write-Host ''
Write-Host 'F2 - lane value validation' -ForegroundColor Cyan
foreach ($bad in @('a;b', 'a"b', "a`tb", '')) {
    $threw = $false; try { Assert-SafeLaneValue $bad 'lane name' 'x' } catch { $threw = $true }
    Check ("refuses lane value '{0}'" -f ($bad -replace "`t", '<TAB>')) $threw
}
Check 'accepts an ordinary lane name' (& { try { Assert-SafeLaneValue 'glpnet' 'lane name' 'glpnet'; $true } catch { $false } })

Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host ''
Write-Host ("RESULT  passed {0}  failed {1}" -f $pass, $fail) -ForegroundColor $(if ($fail) { 'Red' } else { 'Green' })
exit $(if ($fail) { 1 } else { 0 })

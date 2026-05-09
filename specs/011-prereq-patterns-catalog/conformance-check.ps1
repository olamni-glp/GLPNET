# conformance-check.ps1 — prereq-patterns catalog conformance gate
#
# Implements the six structural checks (C1..C6) from
# specs/011-prereq-patterns-catalog/quickstart.md Flow C
# and specs/011-prereq-patterns-catalog/data-model.md cross-entity validation rules.
#
# Pure PowerShell, no third-party dependency. Run from anywhere; the script
# resolves the repo root from its own location.
#
# Usage (from repo root or anywhere):
#   pwsh -File specs\011-prereq-patterns-catalog\conformance-check.ps1
#
# Exit codes: 0 if every check passes, 1 if any check fails.

#Requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Path resolution
# ---------------------------------------------------------------------------
$ScriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$SpecDir     = $ScriptDir
$RepoRoot    = (Resolve-Path (Join-Path $SpecDir '..\..')).Path
$Catalog     = Join-Path $RepoRoot 'prereq-patterns'
$Contracts   = Join-Path $SpecDir 'contracts'
$AnalysisFile = Join-Path $SpecDir 'pglite-merge-analysis.md'

if (-not (Test-Path $Catalog)) {
    Write-Error "Catalog directory not found at $Catalog"
    exit 1
}

# ---------------------------------------------------------------------------
# Result sink
# ---------------------------------------------------------------------------
$Results = [ordered]@{}
$AllPass = $true

function Add-Result {
    param(
        [Parameter(Mandatory)] [string] $Id,
        [Parameter(Mandatory)] [ValidateSet('PASS','FAIL')] [string] $Status,
        [string] $Headline = '',
        [string[]] $Details = @()
    )
    $Results[$Id] = [pscustomobject]@{
        Status   = $Status
        Headline = $Headline
        Details  = $Details
    }
    if ($Status -ne 'PASS') { $script:AllPass = $false }
}

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
function Get-PatternDirs {
    Get-ChildItem -LiteralPath $Catalog -Directory | Sort-Object Name
}

function Get-MarkdownLinks {
    # Returns @( [pscustomobject]@{ Text; Url; LineNo } ) for all inline
    # markdown links in the supplied file content array (line-by-line).
    param([AllowNull()] [AllowEmptyCollection()] [string[]] $Lines)
    $rx = [regex]'\[(?<text>[^\]]*)\]\((?<url>[^\) ]+)\)'
    $out = @()
    if ($null -eq $Lines) { return $out }
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $line = $Lines[$i]
        if ($null -eq $line) { continue }
        foreach ($m in $rx.Matches($line)) {
            $out += [pscustomobject]@{
                Text   = $m.Groups['text'].Value
                Url    = $m.Groups['url'].Value
                LineNo = $i + 1
            }
        }
    }
    ,$out
}

function ConvertTo-RepoRelative {
    param([Parameter(Mandatory)] [string] $FullPath)
    $p = $FullPath
    if ($p.StartsWith($RepoRoot, [StringComparison]::OrdinalIgnoreCase)) {
        $p = $p.Substring($RepoRoot.Length).TrimStart('\','/')
    }
    $p -replace '\\','/'
}

# ---------------------------------------------------------------------------
# C1 — three-files-per-pattern
# Each pattern dir contains exactly description.md, applicability.md,
# sources.md; none collapses to its H1 header alone (FR-004, SC-006).
# ---------------------------------------------------------------------------
function Test-C1 {
    $required = @('description.md','applicability.md','sources.md')
    $errors = @()
    $patterns = Get-PatternDirs
    foreach ($p in $patterns) {
        $names = (Get-ChildItem -LiteralPath $p.FullName -File).Name
        foreach ($r in $required) {
            if ($names -notcontains $r) {
                $errors += "$($p.Name): missing $r"
            }
        }
        foreach ($r in $required) {
            $f = Join-Path $p.FullName $r
            if (-not (Test-Path -LiteralPath $f)) { continue }
            $lines = Get-Content -LiteralPath $f
            $hasBody = $false
            foreach ($line in $lines) {
                $t = $line.Trim()
                if ($t -eq '') { continue }
                if ($t -match '^#\s') { continue }
                $hasBody = $true; break
            }
            if (-not $hasBody) {
                $errors += "$($p.Name)/${r}: collapses to H1 header alone"
            }
        }
    }
    if ($errors.Count -eq 0) {
        Add-Result -Id 'C1' -Status 'PASS' -Headline "$($patterns.Count) pattern dirs; each has all 3 required files; no file reduced to H1 alone"
    } else {
        Add-Result -Id 'C1' -Status 'FAIL' -Headline "$($errors.Count) defect(s) in three-files-per-pattern" -Details $errors
    }
}

# ---------------------------------------------------------------------------
# C2 — lifecycle agreement
# For every pattern: description.md `Status:` line agrees with directory.md
# suffix. (SC-007.)
# ---------------------------------------------------------------------------
function Test-C2 {
    $errors = @()
    $directoryFile = Join-Path $Catalog 'directory.md'
    if (-not (Test-Path -LiteralPath $directoryFile)) {
        Add-Result -Id 'C2' -Status 'FAIL' -Headline 'directory.md missing' -Details @($directoryFile)
        return
    }
    $dirLines = Get-Content -LiteralPath $directoryFile

    $patterns = Get-PatternDirs
    foreach ($p in $patterns) {
        $desc = Join-Path $p.FullName 'description.md'
        if (-not (Test-Path -LiteralPath $desc)) {
            $errors += "$($p.Name): description.md missing (cannot verify lifecycle)"
            continue
        }
        $descLines = Get-Content -LiteralPath $desc
        $statusLine = $descLines | Where-Object { $_ -match '^Status:\s*' } | Select-Object -First 1
        if (-not $statusLine) {
            $errors += "$($p.Name): no 'Status:' line in description.md"
            continue
        }
        $status = ($statusLine -replace '^Status:\s*','').Trim()

        # Find matching bullet in directory.md
        $escapedName = [regex]::Escape($p.Name)
        $dirLine = $dirLines | Where-Object { $_ -match "^\s*-\s+\*\*$escapedName\*\*\s+" } | Select-Object -First 1
        if (-not $dirLine) {
            $errors += "$($p.Name): no entry in directory.md"
            continue
        }

        # Determine expected suffix
        $expected = $null
        if ($status -eq 'active') {
            $expected = ''
        } elseif ($status -eq 'draft') {
            $expected = '(draft)'
        } elseif ($status -match '^superseded by\s+(.+)$') {
            $expected = "(superseded by $($Matches[1].Trim()))"
        } else {
            $errors += "$($p.Name): unrecognised Status value '$status' in description.md"
            continue
        }

        # Trim trailing whitespace from the bullet for comparison
        $dirLineTrim = $dirLine.TrimEnd()
        if ($expected -eq '') {
            # active: must NOT end with a (draft) or (superseded by ...) suffix
            if ($dirLineTrim -match '\s\((draft|superseded\s+by\s+[^)]+)\)\s*$') {
                $errors += "$($p.Name): description.md says active but directory.md has suffix '$($Matches[1])'"
            }
        } else {
            $expEsc = [regex]::Escape($expected)
            if ($dirLineTrim -notmatch "$expEsc\s*$") {
                $errors += "$($p.Name): description.md says '$status' but directory.md line does not end with ' $expected' — line: $dirLineTrim"
            }
        }
    }

    if ($errors.Count -eq 0) {
        Add-Result -Id 'C2' -Status 'PASS' -Headline "$($patterns.Count) patterns: description.md Status: line agrees with directory.md suffix"
    } else {
        Add-Result -Id 'C2' -Status 'FAIL' -Headline "$($errors.Count) lifecycle-drift defect(s)" -Details $errors
    }
}

# ---------------------------------------------------------------------------
# C3 — catalog self-containment
# Every markdown link from any governance file or per-pattern file
# (excluding sources.md Upstream column) resolves inside the glpnet repo.
# (SC-002, FR-011.)
#
# Implementation note: sources.md's Upstream column entries are formatted
# as inline backticks (`olamni-breen/aigrid-aws-infra@<branch>`), not
# markdown links, so the markdown-link regex does not pick them up.
# All other prose links across the catalog are scanned.
# ---------------------------------------------------------------------------
function Test-C3 {
    $errors = @()
    $linkCount = 0
    $allFiles = @()
    $allFiles += Get-ChildItem -LiteralPath $Catalog -Filter '*.md' -File
    foreach ($p in Get-PatternDirs) {
        $allFiles += Get-ChildItem -LiteralPath $p.FullName -Filter '*.md' -File
    }

    foreach ($f in $allFiles) {
        $lines = Get-Content -LiteralPath $f.FullName
        $links = Get-MarkdownLinks -Lines $lines
        $relFile = ConvertTo-RepoRelative -FullPath $f.FullName
        foreach ($l in $links) {
            $url = $l.Url
            # Strip anchor
            $pathPart = $url -replace '#.*$',''
            if ($pathPart -eq '') { continue }  # pure anchor, no path component

            # External URLs: not allowed in catalog files (no AIGRID GitHub etc.).
            # Internal http(s) URLs would be unusual; flag them explicitly.
            if ($pathPart -match '^(https?|ftp|mailto):') {
                $errors += "$relFile`:$($l.LineNo): external link not allowed: $url"
                continue
            }

            # Absolute Windows path or POSIX absolute path: not portable; treat as broken.
            if ($pathPart -match '^[A-Za-z]:[\\/]' -or $pathPart -match '^/[^/]') {
                $errors += "$relFile`:$($l.LineNo): absolute path not allowed in markdown link: $url"
                continue
            }

            $linkCount++
            $candidate = Join-Path (Split-Path $f.FullName -Parent) $pathPart
            try {
                $null = Resolve-Path -LiteralPath $candidate -ErrorAction Stop
            } catch {
                $errors += "$relFile`:$($l.LineNo): broken link '$url' (resolved to: $candidate)"
            }
        }
    }

    if ($errors.Count -eq 0) {
        Add-Result -Id 'C3' -Status 'PASS' -Headline "$linkCount internal markdown links across $($allFiles.Count) catalog files; all resolve inside glpnet"
    } else {
        Add-Result -Id 'C3' -Status 'FAIL' -Headline "$($errors.Count) link-resolution defect(s)" -Details $errors
    }
}

# ---------------------------------------------------------------------------
# C4 — no live AIGRID cross-references
# `grep -i 'breenlake|aigrid|opskit'` over prereq-patterns/ returns matches
# only inside allowed contexts:
#   - Inside a sources.md file (FR-011 exception: sources.md is the
#     canonical home for upstream attribution).
#   - On a line that explicitly identifies the reference as an external
#     sibling ("external sibling" phrase) — the disclaimer footnote.
# (SC-008, FR-011.)
# ---------------------------------------------------------------------------
function Test-C4 {
    $rx = [regex]'(?i)breenlake|aigrid|opskit'
    $allMatches = @()
    $files = Get-ChildItem -LiteralPath $Catalog -Recurse -Filter '*.md' -File
    foreach ($f in $files) {
        $lines = Get-Content -LiteralPath $f.FullName
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($rx.IsMatch($lines[$i])) {
                $allMatches += [pscustomobject]@{
                    File      = ConvertTo-RepoRelative -FullPath $f.FullName
                    FileName  = $f.Name
                    LineNo    = $i + 1
                    Line      = $lines[$i]
                }
            }
        }
    }
    $disallowed = @()
    foreach ($m in $allMatches) {
        # Allowlist: sources.md (FR-011 exception)
        if ($m.FileName -eq 'sources.md') { continue }
        # Allowlist: lines containing "external sibling" disclaimer
        if ($m.Line -match '(?i)external\s+sibling') { continue }
        $disallowed += "$($m.File):$($m.LineNo): $($m.Line.Trim())"
    }
    $allowedCount = $allMatches.Count - $disallowed.Count
    if ($disallowed.Count -eq 0) {
        Add-Result -Id 'C4' -Status 'PASS' -Headline "$($allMatches.Count) total grep hits; $allowedCount in allowed contexts (sources.md or 'external sibling' notes); 0 live cross-references"
    } else {
        Add-Result -Id 'C4' -Status 'FAIL' -Headline "$($disallowed.Count) live AIGRID/breenlake/opskit reference(s) outside allowed contexts" -Details $disallowed
    }
}

# ---------------------------------------------------------------------------
# C5 — format-contract reachability
# prereq-patterns/howto.md and prereq-patterns/policies.md link only into
# specs/011-prereq-patterns-catalog/contracts/ (never into AIGRID specs/).
# (FR-005 link target.)
# ---------------------------------------------------------------------------
function Test-C5 {
    $errors = @()
    $checked = 0
    foreach ($name in @('howto.md','policies.md')) {
        $f = Join-Path $Catalog $name
        if (-not (Test-Path -LiteralPath $f)) {
            $errors += "${name}: missing"
            continue
        }
        $checked++
        $lines = Get-Content -LiteralPath $f
        $links = Get-MarkdownLinks -Lines $lines
        foreach ($l in $links) {
            $url = $l.Url -replace '#.*$',''
            if ($url -eq '') { continue }
            # Any link whose path crosses into a `specs/` directory MUST land in
            # specs/011-prereq-patterns-catalog/contracts/.
            if ($url -match 'specs/') {
                if ($url -notmatch 'specs/011-prereq-patterns-catalog/contracts/') {
                    $errors += "$name`:$($l.LineNo): link to non-glpnet specs path: $url"
                }
            }
            if ($url -match '(?i)aigrid|breenlake') {
                $errors += "$name`:$($l.LineNo): AIGRID-style link: $url"
            }
        }
    }
    if ($errors.Count -eq 0) {
        Add-Result -Id 'C5' -Status 'PASS' -Headline "$checked governance file(s) checked; format-contract links land only in specs/011-prereq-patterns-catalog/contracts/"
    } else {
        Add-Result -Id 'C5' -Status 'FAIL' -Headline "$($errors.Count) format-contract reachability defect(s)" -Details $errors
    }
}

# ---------------------------------------------------------------------------
# C6 — migration-analysis completeness
# pglite-merge-analysis.md classifies every distinguishing feature of both
# pre-merge bridges. Each row's Classification cell starts with one of:
# `present-in-merged`, `superseded-with-rationale`, `dropped-with-rationale`.
# Each Rationale cell is non-empty. The Summary section asserts
# "Unclassified: 0". (SC-005, FR-009.)
# ---------------------------------------------------------------------------
function Test-C6 {
    $errors = @()
    if (-not (Test-Path -LiteralPath $AnalysisFile)) {
        Add-Result -Id 'C6' -Status 'FAIL' -Headline 'pglite-merge-analysis.md missing' -Details @($AnalysisFile)
        return
    }
    $lines = Get-Content -LiteralPath $AnalysisFile

    $tagSet = @('present-in-merged','superseded-with-rationale','dropped-with-rationale')
    $tagAlt = ($tagSet | ForEach-Object { [regex]::Escape($_) }) -join '|'
    # Match a classification cell that may be wrapped in backticks and may have a parenthetical qualifier.
    $classRx = [regex]"^\s*``?(?<tag>$tagAlt)``?(\s+\([^)]+\))?\s*$"

    # Classification tables are recognised by their header row signature.
    $headerRx = [regex]'^\s*\|\s*#\s*\|\s*Feature\s*\|\s*Site'
    $separatorRx = [regex]'^\s*\|[\s\|:-]+\|\s*$'

    $rowsClassified = 0
    $tableCount = 0
    $i = 0
    while ($i -lt $lines.Count) {
        if ($headerRx.IsMatch($lines[$i])) {
            $tableCount++
            # Skip header + separator
            $i++
            if ($i -lt $lines.Count -and $separatorRx.IsMatch($lines[$i])) { $i++ }
            # Walk rows until a non-table line
            while ($i -lt $lines.Count -and $lines[$i] -match '^\s*\|') {
                $row = $lines[$i]
                # Split on unescaped `|`. Markdown tables permit `\|` inside a
                # cell as an escaped pipe; only literal `|` not preceded by a
                # backslash is a column separator. Restore the escape after the
                # split so the cell content remains faithful.
                $cells = @([regex]::Split($row, '(?<!\\)\|') | ForEach-Object { $_ -replace '\\\|','|' })
                # Drop the leading and trailing empty cells from boundary pipes
                if ($cells.Count -ge 2) {
                    $start = 0
                    if ($cells[0].Trim() -eq '') { $start = 1 }
                    $end = $cells.Count - 1
                    if ($cells[$end].Trim() -eq '') { $end-- }
                    $cells = $cells[$start..$end]
                }
                # Expected columns (5): #, Feature, Site (lines), Classification, Rationale
                if ($cells.Count -lt 5) {
                    $errors += "row at line $($i+1): expected >=5 cells, got $($cells.Count): $row"
                    $i++
                    continue
                }
                $idCell        = $cells[0].Trim()
                $featureCell   = $cells[1].Trim()
                $siteCell      = $cells[2].Trim()
                $classCell     = $cells[3].Trim()
                $rationaleCell = ($cells[4..($cells.Count-1)] -join '|').Trim()

                # Match classification cell
                $cm = $classRx.Match($classCell)
                if (-not $cm.Success) {
                    $errors += "row $idCell (line $($i+1)): classification '$classCell' is not one of {$($tagSet -join ', ')}"
                } elseif ([string]::IsNullOrWhiteSpace($rationaleCell)) {
                    $errors += "row $idCell (line $($i+1)): rationale cell is empty"
                } else {
                    $rowsClassified++
                }
                $i++
            }
            continue
        }
        $i++
    }

    if ($tableCount -lt 2) {
        $errors += "expected at least 2 classification tables (glpnet + AIGRID), found $tableCount"
    }

    # Verify the explicit "Unclassified: 0" assertion in the Summary section
    $unclassifiedLine = $lines | Where-Object { $_ -match '(?i)Unclassified:\s*0' } | Select-Object -First 1
    if (-not $unclassifiedLine) {
        $errors += "pglite-merge-analysis.md missing the 'Unclassified: 0' assertion in Summary"
    }

    if ($errors.Count -eq 0) {
        Add-Result -Id 'C6' -Status 'PASS' -Headline "pglite-merge-analysis.md: $rowsClassified rows classified across $tableCount tables; all classifications valid; Rationale non-empty; 'Unclassified: 0' present"
    } else {
        Add-Result -Id 'C6' -Status 'FAIL' -Headline "$($errors.Count) classification defect(s)" -Details $errors
    }
}

# ---------------------------------------------------------------------------
# Run all checks
# ---------------------------------------------------------------------------
Test-C1
Test-C2
Test-C3
Test-C4
Test-C5
Test-C6

# ---------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '=== prereq-patterns catalog conformance check ==='
Write-Host ("Repo root : {0}" -f $RepoRoot)
Write-Host ("Catalog   : {0}" -f $Catalog)
Write-Host ("Spec dir  : {0}" -f $SpecDir)
Write-Host ''

foreach ($id in $Results.Keys) {
    $r = $Results[$id]
    $marker = if ($r.Status -eq 'PASS') { '[PASS]' } else { '[FAIL]' }
    Write-Host ("{0} {1}: {2}" -f $marker, $id, $r.Headline)
    if ($r.Details -and $r.Details.Count -gt 0) {
        foreach ($d in $r.Details) {
            Write-Host ("    - {0}" -f $d)
        }
    }
}
Write-Host ''

if ($AllPass) {
    Write-Host 'OVERALL: PASS — all checks (C1..C6) green.'
    exit 0
} else {
    $failed = ($Results.Values | Where-Object { $_.Status -ne 'PASS' }).Count
    Write-Host ("OVERALL: FAIL — {0} check(s) failed." -f $failed)
    exit 1
}

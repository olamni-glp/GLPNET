# Mutation evidence for `scripts/tests/onrestart-launch.tests.ps1`

Round 3 rated the harness **UNSOUND**, and it was right about the worst part: the harness
*asserted* that a `ping.exe` copied to `claude.exe` was attributed, which institutionalized M1.

A harness that cannot fail proves nothing, so the rewritten harness is measured rather than
claimed: each defect the reviews found is **reintroduced into a copy of the shipped script** and
the harness re-run against the mutant. Host GAVRIELLA, 2026-08-21.

## Round 1–3 findings

| Mutant | Defect reintroduced | Harness result | Assertions that caught it |
|---|---|---|---|
| `M1-name-prefix` | `Test-IsClaudeProc` accepts any process whose name starts with `claude` | **96 / 8 fail**, exit 1 | RENAMED executable refused (M1) · claudette.exe refused (N4) · claude-malware.exe refused (N4) · right image but WRONG args refused |
| `N1-zero-proven-verified` | `Get-RunOutcome` drops the zero-proven guard and reaches VERIFIED | **100 / 4 fail**, exit 1 | accepted refusal is NOT VERIFIED · accepted unconfirmed is NOT VERIFIED · zero proven can never succeed · exhaustive 8×8×2×2 sweep (28 offending combinations named) |
| `M3-lax-tail` | `Test-SessionTailIntact` passes if any one record parses | **102 / 2 fail**, exit 1 | a COMPLETE corrupt line refused · many complete corrupt lines refused |
| `M2-mtime-evidence` | `Get-ResumeEvidence` accepts an mtime bump as proof | **98 / 6 fail**, exit 1 | a timestamp touch is NOT evidence (M2) · no change yields no evidence · a partial appended line is NOT evidence · appended foreign sessionId is WRONG-SESSION |

## Round 4 findings

| Mutant | Defect reintroduced | Harness result | Assertions that caught it |
|---|---|---|---|
| `K1-strong-only` | attribution drops the subtree tier, so a `claude.cmd` shim install is unattributable | **126 / 1 fail**, exit 1 | a shim lane is still attributed via subtree (K1) |
| `K2-first-record-wins` | `Get-ResumeEvidence` returns on the first parseable appended record | **123 / 4 fail**, exit 1 | a LATER foreign sessionId still wins (K2) · the expected sessionId is recognised as strong · an appended foreign sessionId is WRONG-SESSION |
| `K3-short-read-ok` | `Read-FileRange` returns a partially-filled buffer instead of refusing to judge | **126 / 1 fail**, exit 1 | a range past EOF returns null (K3) |
| `K4-wide-tolerance` | the run-lock owner check accepts a 5-second start-time window instead of exact identity | **126 / 1 fail**, exit 1 | a REUSED pid does not inherit the claim (K4) |

Unmutated: **127 passed / 0 failed**, exit 0.
GLP merge gate unchanged at **561 total / 559 passed / 2 failed** (the two known 064 Section T drills).

## A gap mutation testing found in the tests themselves

The K4 injection was first written with a **12.3-second** start-time delta. The mutant it was
meant to expose uses a **5-second** tolerance, so the mutant classified the reused PID as dead
too and the test passed against both — a vacuous assertion of exactly the kind round 3 objected
to. Only running the mutant exposed it. The injection now uses a **1-second** delta, deliberately
*inside* any plausible "close enough" window, so only an exact identity check passes.

That is the point of the exercise: every mutant is caught by the assertions written for that
specific finding, and the one assertion that could not fail was found by trying to make it fail —
not by reading it.

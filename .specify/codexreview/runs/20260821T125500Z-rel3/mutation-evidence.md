# Mutation evidence for `scripts/tests/onrestart-launch.tests.ps1`

Round 3 rated the harness **UNSOUND**, and it was right about the worst part: the harness
*asserted* that a `ping.exe` copied to `claude.exe` was attributed, which institutionalized M1.

A harness that cannot fail proves nothing, so the rewritten harness was measured rather than
claimed: each defect the reviews found was **reintroduced into a copy of the shipped script** and
the harness re-run against it. Host GAVRIELLA, 2026-08-21.

| Mutant | Defect reintroduced | Harness result | Assertions that caught it |
|---|---|---|---|
| `M1-name-prefix` | `Test-IsClaudeProc` accepts any process whose name starts with `claude` | **96 passed / 8 failed**, exit 1 | RENAMED executable refused (M1) · claudette.exe refused (N4) · claude-malware.exe refused (N4) · right image but WRONG args refused |
| `N1-zero-proven-verified` | `Get-RunOutcome` drops the zero-proven guard and reaches VERIFIED | **100 passed / 4 failed**, exit 1 | accepted refusal is NOT VERIFIED · accepted unconfirmed is NOT VERIFIED · zero proven can never succeed · exhaustive 8×8×2×2 sweep (28 offending combinations named) |
| `M3-lax-tail` | `Test-SessionTailIntact` passes if any one record parses | **102 passed / 2 failed**, exit 1 | a COMPLETE corrupt line refused · many complete corrupt lines refused |
| `M2-mtime-evidence` | `Get-ResumeEvidence` accepts an mtime bump as proof | **98 passed / 6 failed**, exit 1 | a timestamp touch is NOT evidence (M2) · no change yields no evidence · a partial appended line is NOT evidence · appended foreign sessionId is WRONG-SESSION |

Unmutated: **104 passed / 0 failed**, exit 0.

Every mutant was caught by the assertions written *for that finding*, not by incidental
collateral — so the coverage is attributable, the same standard the script itself now applies to
its lanes. Reproduce with `python` mutation of the four anchors above; the mutants are not
committed, only this record of what they demonstrated.

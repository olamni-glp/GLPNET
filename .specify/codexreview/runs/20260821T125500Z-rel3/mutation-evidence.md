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

Unmutated after round 4: **127 passed / 0 failed**, exit 0.
GLP merge gate unchanged at **561 total / 559 passed / 2 failed** (the two known 064 Section T drills).


## Round 5 findings

| Mutant | Defect reintroduced | Harness result | Assertion that caught it |
|---|---|---|---|
| `J1-subtree-tier` | attribution accepts any live descendant of the lane launcher | **133 / 1 fail** | an unrelated live descendant is NOT attributed (J1) |
| `J1b-no-noprofile` | the tab's pwsh loads the user profile again, so profile children become descendants | **133 / 1 fail** | the wt command line passes -NoProfile (J1) |
| `J2-raw-length-offset` | resume scanning starts at the raw pre-launch length, splitting a straddling record | **133 / 1 fail** | a foreign sessionId inside a straddling record is caught (J2) |
| `J3-sidless-counts` | a sessionId-less appended record counts as proof of resumption | **133 / 1 fail** | a sessionId-less append is NOT proof (J3) |

Unmutated after round 5: **134 passed / 0 failed**, exit 0. Twelve mutants across three rounds,
all caught, each by the assertion written for its own finding.


## Round 6 findings

| Mutant | Defect reintroduced | Harness result | Assertion that caught it |
|---|---|---|---|
| `H1-give-up-at-one-window` | the record-boundary search returns the window start when it finds no newline | **146 / 3 fail** | a foreign sessionId in an oversized straddling record is caught (H1) · a window-sized file with no newline has boundary 0 |
| `H2-broad-package-rule` | attribution accepts any command line naming the `@anthropic-ai/claude-code` directory | **148 / 1 fail** | a helper under the package dir does NOT (H2) |
| `H4-substring-args` | expected arguments matched as substrings again | **147 / 2 fail** | `--continue-helper` does NOT satisfy `--continue` (H4) · an embedded 1000000 does not satisfy the flag |
| `H3-unidentified-counts` | an appended record that names no session counts as proof | **147 / 2 fail** | a sessionId-less append is NOT proof (J3) · an unidentified append stays UNCONFIRMED (H3) |

Unmutated after round 6: **149 passed / 0 failed**, exit 0. Sixteen mutants across four rounds,
all caught, each by the assertion written for its own finding.

## A second correction the tests forced on the code

The H1 injection — a straddling record LARGER than the search window — first failed on its own
setup: the transcript was judged unusable, because a window landing entirely inside one record
found no complete line and the code called that corruption. Claude Code emits records well past
64 KB, so that was a false negative that would strand a healthy lane. `Test-SessionTailIntact`
now widens its window until it finds a complete record or reaches the file start.

That change in turn invalidated an older assertion: a 70 KB **unterminated** trailing blob is
byte-for-byte indistinguishable from a record still being written, so it can no longer be called
corruption. The injection was rewritten to use a **terminated** garbage line — which is genuine
corruption and is still refused — and the tolerated case is now asserted explicitly rather than
left implicit.


## Round 7 findings

| Mutant | Defect reintroduced | Harness result | Assertion that caught it |
|---|---|---|---|
| `G1-path-anywhere` | the resolved claude path is accepted anywhere in a command line | **164 / 2 fail** | the resolved path as ARGUMENT DATA is refused (G1) · cmd echoing the path is refused (G1) |
| `G2-expand-all-args` | every process's arguments are expanded a level deep, so a packed quoted argument reads as argv | **164 / 2 fail** | flags packed in one argv element are refused (G2) |
| `G2b-no-adjacency` | a flag and its value need not be adjacent | **165 / 1 fail** | a non-adjacent flag value is refused (G2) |
| `G3-guess-zero` | an unreadable snapshot returns offset 0 instead of "unknown" | **165 / 1 fail** | an unreadable snapshot yields -1, never a guessed 0 (G3) |
| `G3b-no-identity` | the transcript file-identity check is removed | **165 / 1 fail** | a replaced transcript is WRONG-SESSION (G3) |
| `G4-zero-window` | a zero-byte search window is allowed | **165 / 1 fail** | TailBytes 0 terminates for the boundary search (G4) |

Unmutated after round 7: **166 passed / 0 failed**, exit 0. Twenty-two mutants across five rounds,
all caught, each by the assertion written for its own finding.

## A third correction the tests forced on the code

The G2 injection — the expected flags packed inside ONE quoted argv element — failed against the
first round-7 fix. Expanding every argv element one level deep, which was added for the command
processor's packed `/c` string, re-admitted exactly the forgery it was meant to close for every
other process. That expansion is now applied **only** to `cmd.exe`, and the injection asserts it.


## Round 8 findings

| Mutant | Defect reintroduced | Harness result | Assertion that caught it |
|---|---|---|---|
| `E1-any-cmd-token` | any cmd.exe argv token equal to the resolved path identifies, without locating `/c` | **177 / 3 fail** | cmd /c echo `<shim>` is refused (E1) · cmd with no /c at all is refused (E1) |
| `E2-any-image-cli` | the CLI entry-point token identifies regardless of the process image | **178 / 2 fail** | notepad carrying the CLI path is refused (E2) · a renamed runtime is judged by its image (E2) |
| `E3-timestamp-only` | transcript identity rests on the creation timestamp alone | **178 / 2 fail** | a forged creation time does not defeat identity (E3) |
| `E4-no-argv0-rule` | argv[0] parsed with the ordinary escape state machine | **179 / 1 fail** | tokenizer matches CommandLineToArgvW on every case (E4) |

Unmutated after round 8: **180 passed / 0 failed**, exit 0. Twenty-six mutants across six rounds,
all caught, each by the assertion written for its own finding.
GLP merge gate unchanged throughout at **561 total / 559 passed / 2 failed**.

## A fourth correction the tests forced on the code

The E4 mutant initially survived: the differential cases did not include a command line where
argv[0] parsing actually diverges. Adding four such cases exposed something better than the
mutant — the SHIPPED tokenizer was wrong too, and so was the "fix". `CommandLineToArgvW` reads
argv[0] as *everything up to the first whitespace, verbatim, quotes and all* when the line does
not begin with a quote; both my implementations processed quotes there. The tokenizer was
rewritten to the platform's actual rule and now matches it on all fourteen cases, the mutant is
caught, and the check is a genuine differential test against the platform rather than against my
own understanding of it.


## Round 9 findings

| Mutant | Defect reintroduced | Harness result | Assertion that caught it |
|---|---|---|---|
| `D1-cli-anywhere` | the CLI path anywhere in a runtime's argv identifies, not just the executed script | **184 / 1 fail** | node benign.js `<cli.js>` is refused (D1) |
| `D2a-no-fingerprint` | transcript identity drops the opening-byte fingerprint | **183 / 2 fail** | a forged creation time does not defeat identity (E3) |
| `D2b-no-creation-check` | the creation-time filter is removed | **184 / 1 fail** | a replaced transcript is WRONG-SESSION (G3) |

Unmutated after round 9: **185 passed / 0 failed**, exit 0. Twenty-nine mutants across seven
rounds, all caught, each by the assertion written for its own finding.

**One property is argued, not tested:** every read of a transcript now comes from a single open
handle, so a same-name replacement landing mid-verification cannot combine the old file's identity
with the new file's content. That is a structural property of the handle, and a deterministic race
test for it is not written — it is claimed here as reasoning, not as measured coverage.

## A gap mutation testing found in the tests themselves

The K4 injection was first written with a **12.3-second** start-time delta. The mutant it was
meant to expose uses a **5-second** tolerance, so the mutant classified the reused PID as dead
too and the test passed against both — a vacuous assertion of exactly the kind round 3 objected
to. Only running the mutant exposed it. The injection now uses a **1-second** delta, deliberately
*inside* any plausible "close enough" window, so only an exact identity check passes.

That is the point of the exercise: every mutant is caught by the assertions written for that
specific finding, and the one assertion that could not fail was found by trying to make it fail —
not by reading it.

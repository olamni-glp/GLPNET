"""Wait for the busy buildkit registry lock to clear, then land this session's marathon captures.

Retries each capture until it succeeds or the overall deadline passes. Never reaps a PID.
"""
import subprocess, sys, time, os

F = "078-verification-receipts"
REPO = r"D:\BSTDEV\research\GLP\GLPNET"
DEADLINE = time.time() + 2400

CAPS = [
    ("bug",
     "CODEXREVIEW UNBLOCKED (git pathspec quirk root-caused) and the review then returned NO-GO on 078 with 8 HIGH findings",
     "Full write-up: docs/research/codexreview-unblocked-and-078-no-go-2026-08-24.md (tracked; supersedes codexreview-two-blocking-defects-2026-08-24.md). ROOT CAUSE: scope.resolve_path runs 'git ls-files -- <path> <8 excludes>'; on git 2.55.0.windows.3 the excludes '**/*.map' and 'reviews/**' each INDEPENDENTLY empty a nested pathspec they cannot match. buildkit's refusal is honest; its input is wrong. ROUTE: use a single-component repo-root dir as --scope (codeconv = 332 files with all 8 excludes). I WITHDRAW my intermediate rule '3+ components are emptied' - FALSE (docs/research is 2 components and survives all 8). THEN THE REVIEW RAN: run 20260824T165651Z exit 0, not timed out; 10 residual findings 8 HIGH 2 MEDIUM on the receipts module ITSELF - reuse of another check's PASS receipt (no run ID in either model); no PASS branch in validation; skipped items not reconciled per FR-010; empty or run_id-mismatched expected.json accepted; run reconciliation trusts a FILENAME without loading the sidecar; the conformance fixture reaches passed==len(_CASES) without exercising the declared BOUNDED case; the guard-weakening mutation test stays GREEN under a no-op validator (the inverse of SC-007); override applies() ignores the recorded reason. CAVEAT findings_count_status=unconfirmed prose_fallback_findings=10 - codex returned prose so the COUNT is a parse fallback; the individual findings are the evidence. RELEASE DECISION NO-GO - and for a better reason than before: not 'we cannot review' but 'we reviewed and it failed'. These 10 findings are the concrete work item for 078's next implementation slice."),

    ("issue",
     "SCHED-R1 and SCHED-R4 did NOT need the builds they were sized for - both shipped upstream in buildkit 2026.8.24.3",
     "SCHED-R1 was carried as maxi/17 'readiness writer' and ranked #1 unblocked in the session-4 restart doc. buildkit 2026.8.24.3 ALREADY SHIPS the writer surface: 'transition' (writes ONE board transition; derives from_state so the op lands effective; refuses rather than guesses), 'bulk-ready' (bulk column move; refuses unless the board has DECLARED ingest_ready_default) and 'confirm', over an advisory readiness.py recommendation module that deliberately does NOT auto-drive - auto-writing 'ready' is contract-forbidden because every capability in this toolchain is advisory. SCHED-R4 likewise: the 'stock-edges' verb projects roadmap dependencies onto the board. I nearly started a 17-point implementation of already-shipped code and stopped only because I checked the CLI surface before building. RULE: a remediation item sized against a tool version becomes a HYPOTHESIS the moment that tool ships a new version - re-measure the CLI surface before building."),

    ("issue",
     "SCHED-R4 DISCHARGED - 27 of 279 edges stocked - but the improved number must never be quoted alone",
     "stock-edges projected 27 of 279 roadmap dependencies onto D:/coop/glpnet/sched: 6 confirmed / 21 heuristic / 0 removed / 0 cycles. edge_coverage is off 0.0. BUT 252 of 279 are UNRESOLVABLE - the board holds 32 work packets while the roadmap holds 119 features, so a dependency whose endpoint has no WP cannot be projected. Any critical path computed on this board today runs on roughly 10 percent of the declared dependency set. readiness.py's own docstring names this exact hazard: on an edgeless graph 'all prerequisites satisfied' is vacuously true for every WP - correct arithmetic on the wrong predicate. Going from 0 edges to 27 NARROWS that hazard; it does not retire it. Quote the caveat with the number."),

    ("bug",
     "THE BK-STD-1 OPEN TABLE DROPS state=implemented - it reports 24 not-closed where the export fold says 25, and the hidden row is 067",
     "Measured on export gavriella__glpnet__20260824T170210Z.json: heads carry 119 features with states closed 94 / promoted 15 / specified 6 / analyzed 3 / implemented 1 = 25 NOT-CLOSED. scripts/roadmap_open_table.py renders 24 and omits exactly the implemented row. The dropped feature is 'qr-link-provisioning' (067) - which is the very feature the engineer ruled must GRADUATE to its own /bk-specify pipeline. So the standard fleet table hides the feature carrying an open engineer ruling. ariellas filed this defect on branch 091 ('adopt ruled open-table renderer (drops implemented state - defect filed)') and I have now corroborated it by direct measurement against the export. ALSO MEASURED: epic heads carry NO state field at all - all 20 have state=None - so any claim of the form 'N epics still captured' cannot be supported by the export and must be sourced elsewhere or dropped. Correct fleet figures for glpnet today: 25 not-closed features = 1 implemented + 3 analyzed + 6 specified + 15 promoted, across 6 epics carrying open work."),

    ("bug",
     "THE STUCK-LOCK VERDICT WAS FALSE A FOURTH AND FIFTH TIME - and this time I identified the holder",
     "buildkit-marathon refused four capture attempts with: 'PID 38152 held it on ALL 61 attempts and never changed - that is a STUCK lock, not contention.' Get-Process says PID 38152 is ALIVE, started 17:59:17, 80s CPU. Get-CimInstance Win32_Process gives its command line: python.exe -m pytest tests/roadmap/test_link_refusals.py ... -q. It is a LIVE pytest run from another buildkit session in another repo. This is the exact failure mode already recorded in memory - the verdict means only 'the PID did not change', and a live test run from a concurrent session is indistinguishable from a dead one by that test. FIFTH consecutive false verdict. RULE, now with the diagnostic that settles it in one step: never act on the STUCK verdict; run Get-CimInstance Win32_Process -Filter 'ProcessId=<pid>' | Select CommandLine, which names the holder outright. FIX OWED UPSTREAM: the lock message should carry the holder's command line, and the word STUCK should be reserved for a PID that Get-Process cannot find."),

    ("issue",
     "ONBOARD executed on a 35-day 3x8h calendar; ACK escalated unanswered at 69 minutes; the real gap is allocation not availability",
     "Per engineer directive: buildkit-scheduler onboard --root D:/coop/glpnet/sched --actor gavriella --avail-hours 35 --shifts 35. VERIFIED BY CONTENT not exit code: calendar/gavriella/gavriella-cal-000001.jsonl now holds 127 rows, 38 days carry a FULL 3x8h day, horizon 2026-08-24 to 2026-09-27, slots 00:00-08:00 / 08:00-16:00 / 16:00-00:00 starting at 00:00 as directed. This is the multi-day window owed since wave-16, when every host declared --avail-hours 24 against a ~880h critical path. ACK requested from ariellas at 16:49Z with a 5-minute SLA; at 17:58Z (69 min) no ACK. ESCALATION FILED. I took NO one-way action off the silence. FOUR OTHER LANES on this same host asked ariellas for the same ACK inside the same hour and NONE was answered - a common-mode silence across five independent asks is evidence about the channel or the peer, NOT five refusals. THE FINDING THAT MATTERS MORE: this board has 22 of 32 packets at engineer_id='unassigned' (the deliberate reserved pool) and ZERO packets that are ready AND unallocated - there is no dispatchable supply for anyone to take; a sibling lane measured the allocator's OWN host at 59 ops and ZERO availability windows since 2026-08-13. So the fleet spent an hour making every WORKER declare 35 days while the ALLOCATOR has declared none and is issuing no addressed allocations. Making workers more available cannot move work that is never addressed."),

    ("issue",
     "Merges landed: 091 (ariellas) and the olamnit tidy-up branch; TIDY-Y15 discharged by ariellas; a SECOND standards fork flagged not resolved",
     "origin/091-bkstd1-round42 merged CLEAN as 2b0f9122 - brings roadmap round 47, the bk-flow and bk-proof SKILL.md files, and ariellas' restart prep. origin/chore/tidy-up-branches-worktrees-20260822-olamnit merged as 6a261b1d with 2 add/add conflicts, BOTH resolved to develop: (a) .specify/roadmap-owners.json - olamnit's side was the EMPTY OBJECT {} and would have erased both declared owner rows; (b) scripts/roadmap_open_table.py - olamnit carries a SECOND renderer (11295 B, 'FLEET STANDARD ... engineer ruling 2026-08-23', adopted from qhstate d1f64b4) while develop already carries a different one (9798 B, 'BK-STD-1 section 2 ... proposed by ariellas-tefl, ruled 2026-08-23') that is itself the product of an earlier withdrawal. TWO FILES BOTH CLAIMING THE SAME RULING. I kept develop's, did NOT delete olamnit's from their branch, and published the fork for olamnit and ariellas to settle - I am not picking a winner unilaterally, which is how the last fork started. TIDY-Y15 (author .claude/skills/bk-flow/SKILL.md, mini/7) is DISCHARGED BY ARIELLAS via the 091 merge - I will not author a competing skill file. Remaining unmerged origin heads: 7 -> 5, all engineer-gated or archive: 050 and 059 (Y09 survivor ruling, X10 owed), 067 and 067b (ruled to graduate), backup/upgrade (archive line)."),
]


def busy(err: str) -> bool:
    return "busy" in err or "STUCK lock" in err


def main() -> int:
    env = dict(os.environ, PYTHONUTF8="1")
    pending = list(CAPS)
    while pending and time.time() < DEADLINE:
        kind, title, desc = pending[0]
        p = subprocess.run(
            [sys.executable, "-m", "buildkit_cli.marathon", "capture",
             "--feature", F, "--kind", kind, "--title", title, "--description", desc],
            cwd=REPO, capture_output=True, text=True, encoding="utf-8", errors="replace", env=env,
        )
        out = (p.stdout or "") + (p.stderr or "")
        if p.returncode == 0 and not busy(out):
            print("OK   " + title[:80], flush=True)
            pending.pop(0)
            continue
        if busy(out):
            print("WAIT lock held; retrying in 45s", flush=True)
            time.sleep(45)
            continue
        print("FAIL " + title[:70] + " :: " + out.strip()[:300], flush=True)
        pending.pop(0)
    print("REMAINING " + str(len(pending)), flush=True)
    return 0 if not pending else 1


sys.exit(main())

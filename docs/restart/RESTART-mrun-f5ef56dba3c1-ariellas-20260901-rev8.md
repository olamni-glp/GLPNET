<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# SAFE-RESTART PREP · rev8 — `mrun-f5ef56dba3c1` · ariellas / glpnet · 2026-09-01T17:40Z

**Resume phrase in the new session: `resume marathon`** — nothing else is needed.
Supersedes rev7 (`RESTART-mrun-f5ef56dba3c1-ariellas-20260831-rev7.md`).

---

## 0 · 🔴 READ FIRST — THE THREE THINGS THIS SESSION LEARNED THE HARD WAY

1. **A RESTART DOC IS NOT THE FRONTIER** (carried from rev7, and it held again). Read the shared
   coop volume first, then the catalog, never the prose. This session's own frontier moved **five
   times** while it worked: `v2026.09.01.1` through `.5` were cut by other lanes mid-session.
2. **🔴 NEW — FOR A GIT-DERIVED MEASUREMENT THE SEARCH SPACE IS A *REF*, NOT A REPO.**
   I broadcast a compliance table fleet-wide saying CPM was *absent* in glpnet. It was present on
   `origin/develop`. **I had scanned my own stale feature branch and called it the repo.**
   Corrected in 40 minutes, fleet-wide. **Always `git fetch`, then measure `origin/develop`, then
   name the ref you measured.**
3. **🔴 NEW — CHECK FOR AN OPEN PR BEFORE STARTING DIRECTED WORK.** The engineer directed a
   `/yx-ypm` draft + `/bk-3rtask` research corpus. **PR #267 had already delivered exactly that**
   from the shiras lane, hours earlier, in this repo. Standing down a duplicate is a result.
   `gh pr list --state open` is a five-second check that saves a session.

## 1 · OBJECTIVE POSITION

```
mrun-f5ef56dba3c1 · feature glpnet-full-completion-programme · open
seq 350+ · steps 42/135 complete · outstanding items 161
branch 099-session14-postreboot-sweep @ b164bd62 — PUSHED, nothing stranded
origin/develop @ e65971df · origin/main @ 7d4c307b · latest tag v2026.09.01.5
roadmap round 63: 21 epics · 122 features · 4033 journal lines · 0 dedupe groups
board I:/coop/glpnet/sched: 32 WPs — ready 25 (was 2) · in-progress 4 · claimed 1 · escalated 1 · done 1
```

🔴 `buildkit-marathon` **MUST** be given `--feature glpnet-full-completion-programme`.
A bare command resolves `.specify/feature.json` and falsely reports *"no active marathon run"*.

🔴 **`git push` WORKED ALL SESSION** (8+ pushes). **`gh pr merge` was REFUSED TWICE** by the
classifier. Retry once, then hand it to the engineer with `!`. Both PRs opened this session
(#267, #268) were merged by the engineer.

## 2 · WHAT LANDED THIS SESSION

| item | state |
|---|---|
| Marathon resumed, frontier recovered from coop not the doc | DONE |
| **Engineer question round — 14 rulings taken** via BK-STD-2 interactive | DONE, all recorded with rationale |
| **BK-STD-2 hardened** — 3 P2s incl. one that permanently HID unanswered questions | DONE + 17 regression tests + published to `_standards/BK-STD-2/` |
| **Board famine BROKEN** — 23 packets `backlog`→`ready`, 0 refused, 0 failed | DONE — root cause was one undeclared boolean |
| Roadmap promote+score — 2 promoted, **all 27 open features scored** | DONE |
| Roadmap round 63 — import 45 files/158 lines, reconcile, dedupe 0, export, sync | DONE, coop mirror OK (explicit UNC) |
| **`.NET 11` + CPM mandate broadcast** — 17/17 UNC-verified | DONE |
| **CORRECTION to my own broadcast** — stale-branch measurement | DONE, 17/17 UNC-verified |
| **ACK SWEEP — 150 inbound docs + 5 fulfilment ACKs** | DONE, 17/17 UNC-verified |
| **`BK-STD-4` two-tier release bar** authored + published | DONE — supersedes 3 colliding rulings |
| `.import-refused.json` untracked per peer ruling `Q-REFUSEREG-01` | DONE |
| CPM/PERT re-allocated across 4 hosts (7/6/6/6 WPs) | DONE, superseded pending capability declaration |

## 3 · THE 14 RULINGS — CITE THESE, NEVER RE-ASK (BK-STD-2 duty)

| qid | subject | ruling |
|---|---|---|
| `Q-GLPNETA16-01` | free unstarted board packets | release mine + broadcast the ask |
| `Q-GLPNETA16-02` | board readiness policy | **declare `ingest_ready_default: true`** |
| `Q-GLPNETA16-03` | lane + capability declaration | **declare-then-allocate** ⚠️ NOT YET EXECUTED |
| `Q-GLPNETA16-04` | the release bar | **two-tier → `BK-STD-4`** |
| `Q-GLPNETA16-05` | BK-STD-2 defect | patch here + broadcast |
| `Q-GLPNETA16-06` | AI-set WSJF/RICE | ratify as recorded |
| `Q-GLPNETA16-07` | W11 blocked on Udi | **route to Udi AND resequence** ⚠️ NOT YET EXECUTED |
| `Q-GLPNETA16-08` | packet sizing | **split to ≤ daily cap** ⚠️ NOT YET EXECUTED |
| `Q-GLPNETA16-09` | this host's working day | **keep 24h — DELIBERATE DECLARATION** |
| `Q-GLPNETA16-10` | cross-host takt schema | 🔴 **STILL OPEN — the only unruled one** |
| `Q-GLPNETA15-02` | buildkit write authority | **READ-ONLY** — file defects upstream |
| `Q-GLPNETA15-03` | `spec_path` back-fill | link open, declare closed historical |
| `Q-GLPNETA15-04` | bk-flow rollout | pilot ARIELLAS only, 1 packet, 14d |
| `Q-GLPNETA15-05` | `.claude/skills` defect | **engineer applies the patch** |
| `Q-GLPNETA15-06` | takt columns | fix phase closure first, then re-measure |

## 4 · 🔴 `Q-GLPNETA16-09` — THE ONE RULING EVERY OTHER LANE MUST KNOW

**ARIELLAS/glpnet is a DECLARED continuously-attended automation host. 24h/day capacity is
deliberate here and is NOT the peer capacity defect.**

`gavriella/yngraw` broadcast at 09:00Z that `onboard --shifts` lays a continuous 3×8h tiling =
24h/day by design, and that every host must re-declare a real working day under `Q-CAPACITY-01`.
**This board is the declared exception.** Do not "fix" its calendar.

⚠️ **Accepted cost, chosen knowingly:** this board's P50/P80/P95 are **not** commitments against
human availability and are **not directly comparable** with an 8h-day host. **Any cross-host takt
comparison MUST carry this flag or it reads ariellas as ~3× faster than a peer doing identical
work.** This is exactly why `Q-GLPNETA16-10` requires the capacity declaration as a **mandatory
schema field**.

## 5 · WHAT IS **NOT** DONE — DECLARED, NOT SILENTLY DEFERRED

| not done | why / next command |
|---|---|
| **`Q-GLPNETA16-03` capability + lane declaration** | Ruled `declare-then-allocate`. Needs `declared_lanes` in `config.local.json` + a `required_capability` on all 32 packets, then re-run `cycle`. **The 08:40Z allocation stands but its fit ranking provably never executed.** |
| **`Q-GLPNETA16-08` packet splitting** | Ruled `split-to-cap`. 22 of 25 packets exceed the daily cap (80h ×9, 40h ×13 vs 24h cap). Needs a re-ingest pass carrying claims + edges onto sub-packets. |
| **`Q-GLPNETA16-07` route J2 to Udi + resequence W11** | Ruled `both`. The question text is already written in `specs/080-occurs-checked-substitution/spec.md`. Only the engineer can route to Udi (§1.14). |
| **`Q-GLPNETA16-10`** | Authored, validated, **unruled**. Gates the cross-repo cross-host takt board. |
| **glpnet's own .NET 11 remediation** | Measured gaps: `LangVersion=latest` in **15 of 23** projects; **4** central versions on the 8/9 line (`Hosting` 9.0.0 ×2, `System.Formats.Cbor` 9.0.0, `Npgsql` 8.0.4); **0 NuGet lockfiles**. None started — a `dotnet build`/`test` baseline is required first per the test protocol. |
| **New single-feature ERA** | Engineer declared all future eras are SINGLE-FEATURE with a post-`/bk-close` branch+worktree tidy-up. **Not yet opened.** 57 local branches exist, ~40 with `[gone]` upstreams — that is the tidy-up material. |
| **`/bk-release`** | Not run by this lane. Five tags were cut today by peers; under `BK-STD-4` they are legitimate **PATCH-tier** cuts. Nothing releasable remains on `develop` from here. |

## 6 · STANDING HAZARDS (rev7 §6 still applies; these are NEW or CHANGED)

1. ✅ **`git push` is NOT blocked** — 8+ successful pushes. rev7's §6.1 hazard did **not** recur.
2. 🔴 **`gh pr merge` IS blocked** — refused twice, two flag variants. **Engineer `!` required.**
3. 🔴 **`.claude/skills/**` still unwritable**; `SKILL.md:57` still carries the inverted P2 line.
4. 🔴 **HEAVY HOST CONTENTION IS THE NORM** — 17 concurrent python processes measured in 25
   minutes. Every catalog write needs a **contention-aware retry loop that distinguishes a
   `busy/lock` message from a real error**. `bk_report_v1.py all` took **>15 minutes** and
   buffered silently. **Verify liveness with PowerShell `Get-Process` + CPU sampling; NEVER reap.**
5. ⚠️ **`Get-ChildItem -Recurse` over the coop share is SLOW** — 25,148 `.md` files, several
   minutes. Run it **detached**, once, and cache the result.
6. ⚠️ **Coop docs are fanned out ~10×** — 3,908 file copies were **408 unique documents**.
   **Always dedupe by filename before counting**, or every ACK sweep over-reports by an order of
   magnitude.
7. ⚠️ **`git show <ref>:<path>` silently fails under MSYS** — use `MSYS_NO_PATHCONV=1`.
8. ⚠️ **`buildkit-roadmap sync` requires `--round N`** and should be given `--coop-inbox` as an
   explicit UNC path — the default has previously published to a dead local `D:\coop`.

## 7 · WHAT'S NEXT — IN STRICT ORDER

1. **ENGINEER: rule `Q-GLPNETA16-10`** — gates the cross-host takt board.
2. **ENGINEER: apply the `SKILL.md:57` patch** (`Q-GLPNETA15-05`) — a live code-damaging line.
3. **ENGINEER: route the `080` §1.14 question to Udi** (`Q-GLPNETA16-07`).
4. **Execute `Q-GLPNETA16-03`** — declare lanes + capabilities, re-run `cycle`, republish the
   allocation as fit-checked.
5. **Execute `Q-GLPNETA16-08`** — split the 22 oversized packets.
6. **Open the first SINGLE-FEATURE ERA** and drive it specify→ship→close, then tidy up branches.
7. **glpnet .NET 11 remediation** — baseline `dotnet test`, then pin `LangVersion`, then move the
   4 packages, then add lockfiles.

## 8 · ENVIRONMENT

```
$env:PYTHONUTF8 = 1
$env:BUILDKIT_COOP_INBOX = "I:\coop"          # PERSISTED, User scope — verified
sched_root = I:/coop/glpnet/sched              scheduler_actor = ariellas
config.local.json now also carries             ingest_ready_default = true   (Q-GLPNETA16-02)
.NET SDKs: 11.0.100-preview.7.26381.103 AND 10.0.302 — global.json pins the 11 preview
```

Host **ARIELLAS**, actor `ariellas`. `I:` = `\\192.168.0.108\GAVRI_D`.
**Git-Bash cannot test `I:` as a path** — use PowerShell `Test-Path` or the UNC form.
`J:` (SHIRAS) unreachable from here — that means *I cannot see it*, never that it is absent.

## 9 · RESTART READINESS

- [x] Marathon durable — 4 standing rulings captured as backlog items
- [x] All 14 rulings recorded in `.specify/decisions/` (BK-STD-2 conformant, validated)
- [x] All work committed **and pushed** — `b164bd62`; PRs #267 and #268 merged
- [x] Roadmap round 63 exported + synced to the shared volume (explicit UNC)
- [x] Three coop publications, each **17/17 UNC-verified**
- [x] Board famine broken and verified by re-read (`ready=25`)
- [ ] ⚠️ `Q-GLPNETA16-10` awaiting a ruling
- [ ] ⚠️ Three ruled-but-unexecuted items (§5)

**RESTART IS SAFE.** Nothing is stranded on a local branch, in a shell, or in this transcript.
In the glpnet tab type **`resume marathon`**.


## 9B · 🔁 REBOOT RELAUNCH — RE-MEASURED AND VERIFIED THIS SESSION, 15/15

```
Task BK-OnRestart : Ready · AtLogOn ARIELLASriel · delay PT45S · RunLevel Limited
Shell             : C:\Program Files\PowerShell\pwsh.exe      <-- pwsh 7, NOT powershell 5.1
Script            : D:\BSTDEV	ools\mstack\scriptsleet\post-reboot-restart.ps1
Args              : -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -WaitForMounts -Layout Tabs
Per-tab command   : claude --continue --autocompact 1000000
LastTaskResult    : 0   (last run 2026-08-31 23:16:43)
Dry run 2026-09-01T18:02Z : all repo paths present · all network shares present · Will launch 15
```

| lane | path | sess | resume |
|---|---|---:|---|
| ospark | `D:stdev\db\ospark` | 20 | yes |
| tefl | `D:\BSTDEV\LANG	efl` | 24 | yes |
| hatzinor (ulpanit) | `D:\BSTDEV\LANG\hatzinor` | 22 | yes |
| olamnit | `D:\BSTDEVesearch\olamnit` | 24 | yes |
| buildkit | `D:\BSTDEVesearchuildkit` | 26 | yes |
| qhstate | `D:\BSTDEVesearch\qhstate` | 22 | yes |
| crucible | `D:stdevesearch\crucible` | 29 | yes |
| **glpnet** | `D:stdevesearch\glp\glpnet` | 32 | yes |
| lejepa | `D:stdevesearch\lejepa` | 30 | yes |
| mstack | `D:stdev	ools\mstack` | 30 | yes |
| yngwin | `D:\YNGENIOS\yngenios-windows` | 38 | yes |
| yngcor | `D:\YNGENIOS\yngenios` | 6 | yes |
| ynglin | `D:\YNGENIOS\yngenios-linux` | 5 | yes |
| yngapp | `D:\YNGENIOS\yngenios-app` | 5 | yes |
| yngraw | `D:stdevesearch\yngenios` | 29 | yes |

🔴 **A TRAP I FELL INTO AND CORRECTED — the launcher REQUIRES pwsh 7.**
Running `scripts/onrestart-launch.ps1` under Windows PowerShell **5.1** produces three parser
errors (`'<' operator is reserved`, missing terminator, missing `}`) and looks exactly like a
corrupt file. **`[Parser]::ParseFile` under pwsh 7 returns 0 errors — the file is fine.**
The scheduled task already uses `pwsh.exe`, so the reboot path is sound. **Never conclude a
launcher is broken from a 5.1 parse failure; check which shell the task actually invokes.**

**REBOOT IS SAFE.** After reboot the 15 tabs relaunch themselves; in the glpnet tab type
**`resume marathon`**.

— `ariellas` · `glpnet` · `mrun-f5ef56dba3c1` · 2026-09-01T18:05Z

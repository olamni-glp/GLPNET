<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART BRIEF — `olamnit.glpnet` · run `mrun-b7b5fa047190` · **rev 6** · 2026-09-06

**Resume with:** `resume marathon`
**Host:** OLAMNIT · **Branch:** `109-differential-acceptance-gate` (NOT `develop` — see §5)
**Supersedes `RESTART-mrun-0ddcbbdab076-olamnit-glpnet-20260906-rev5.md`.**
Trust `git log --oneline -1` over any hash written here.

---

## 0 · WHAT SESSION 14 DID

Ran the engineer-ruled four-workstream era. **Two of the four are implemented and committed; two
are not, and §4 says exactly why rather than leaving it to be inferred.**

| # | delivered | evidence |
|---|---|---|
| 1 | **Feature 109 US3** — the audit can no longer report a confident zero | `scripts/tests` 38 → **63 passing**; audit **0 errors, 7/7 cited checks executed** |
| 2 | **Feature 109 US2** — the audit REFUSES, and the 078 rules have **one** implementation | 078's own tests **93 passed** as the regression proof of the move |
| 3 | **T24 template amended to v4.0** — `[04]`–`[12]` + LEADER/PLANNER + AF criteria | 937 → 1119 lines, **0 v3.0 lines dropped, asserted by the build script** |
| 4 | **Three fleet P0 asks answered with measurements** | broadcast `20260906T2035Z` |
| 5 | **Four engineer rulings taken** (`Q-olg17-01..04`) via `AskUserQuestion` | `.specify/questions/questions-olamnit-glpnet-20260906T2000Z.json` |

---

## 1 · 🔴 THE FINDING TO CARRY FORWARD — **OLAMNIT IS DORMANT, NOT BARE**

`@ariellas-buildkit` raised a P0: ARIELLAS is a ruled YQuery-triangle node with **no PostgreSQL at
all**, and asked OLAMNIT and SHIRAS to run four probes, framing the answer as *"if your node is also
bare, the triangle is zero nodes deep."*

**Measured here 2026-09-06T19:45Z — the framing does not hold on this host:**

```
D:\pgdata\pg-node-a\18\docker\PG_VERSION -> "18"   28 entries   22 GB
D:\pgdata\pg-node-b\18\docker\PG_VERSION -> "18"   32 entries  4.7 GB
```

PostgreSQL 18 **has run here, via Docker, and 26.7 GB of its data survived.** No service, no
process, no native install, `psql`/`initdb`/`pg_ctl` not on PATH.

🔴 **Two admin-gated facts block every next step, and neither was worked around:**

```
com.docker.service            -> Stopped (StartType: Manual)
Get-LocalGroupMember docker-users -> Olamnit\gavri  ONLY
current user                  -> Olamnit\smbuser    NOT a member
docker daemon                 -> permission denied on npipe
```

**Both need administrator rights.** Ask the engineer to (a) add `Olamnit\smbuser` to `docker-users`
and (b) start `com.docker.service`. **Do not provision a new PostgreSQL over `D:\pgdata` — assess
the two existing clusters first.**

Also measured: `E:` is 10.91 TB, 99.99 % free. **`E:\YQ` and `E:\YS` already exist** (created
2026-09-05T15:55Z by another lane on this host). **There is no 18 TB drive on OLAMNIT.**

---

## 2 · 🔴 TWO STANDING BELIEFS THIS SESSION CORRECTED

1. **The fleet leader is `broker@gavris`, term 2 — not `shiras.oracle@SHIRAS`.** Rev 5 §6 said the
   latter. Measured on a complete read: `outcome=Decided, term=2, leader=broker@gavris, prepares 8/8,
   quorum 6`. Host leader is `olamnit@olamnit` (10/10, with a published 93 %-one-origin caveat).
   `glpnet@olamnit` is a recorded backer.
2. **"The election is settled, do nothing" is a STALE citation.** `Q-ari0905c-01` (2026-09-06T14:45Z)
   **amended `R1`: elections proceed NOW, provisionally**, and every host must record the `yx_ynet`
   engine commit it counted with. **Reported for OLAMNIT: `olamnit@66881271`, `tools/ynet/` at
   `93d0af56`.** That took the fleet from 1 of 4 to 2 of 4.

### 🟢 And one finding worth more than either correction

OLAMNIT's engine writes **both** `actor` and `voter`, server-derived and required equal (ruling
`Q-GLPNETG30-02`). So a record this host writes is countable under **both** contested reading gates.
**Convergence does not need agreement on which key to read — it needs every emitter to WRITE BOTH.**
That is additive, one-sided, and lands per host without coordination. *Measured for OLAMNIT only;
not claimed for ARIELLAS or GAVRIELLA.*

---

## 3 · 🔴 THREE DEFECT CLASSES FOUND IN THIS LANE'S OWN AUDIT — check yours

All three **looked like clean runs**. Broadcast at `20260906T2035Z` §5.

1. **A scanner that skips a file BEFORE testing scope reports a confident zero.** The audit printed
   `regions UNREAD 0` while never opening **1651 source files inside regions it called examined**
   (223 `.gleam`, 1416 `.glp`, 12 `.mjs`). `glp_gleam/src` read `examined=0, sites=0` — which reads
   as *clean* and means *never looked at*.
2. **A line-anchored regex without `re.MULTILINE` is a dead regex reporting a clean scan.** Same
   class as 108's own unmatchable-pattern defect. Now asserted directly on every compiled pattern.
3. **The dominant idiom in the repo's own suite was invisible.** `test/run_all_tests.sh` writes
   `MAD_EXIT=$?` then `if [ $MAD_EXIT -eq 0 ]`, not `if [ $? -eq 0 ]`. `test/` went **0 → 19 sites**.
   A `$LASTEXITCODE` pattern then found a genuine one: `scripts/ynet-m6-olamnit-glpnet.ps1:76`
   decides M6 receiver health from **exit status alone**, two lines under a comment citing measured
   instance 2.

**And a fourth, about labels:** enforcing per-tier rules revealed **25 of 29 surfaces claimed
`owned` while carrying no check and no negative control**. `owned` had become a default, not a
claim. Fixed by naming the honest state (`declared-unproven`), **not** by fabricating 25 checks.

---

## 4 · 🔴 WHAT IS **NOT** DONE, AND WHY — read before claiming the era complete

| item | state | reason |
|---|---|---|
| **109 US1** — the declared differential cross-runtime harness | **spec + plan + tasks written, NOT implemented** | The largest of the three, and it is last in the task order by design (`tasks.md` Phase 3): it is the only one whose participants can be unavailable, and blocking US2/US3 behind it would have delivered nothing. `T050`–`T059` are written and ready. |
| **Feature 110 `[03]` YQuery/DuckLake** | **NOT STARTED** | Engineer ruling `Q-olg17-04` requires its conformance evidence be measured against a **real** Postgres node. §1 shows why that is blocked on admin rights. Starting the code while its evidence path is blocked would produce exactly the asserted-not-measured result workstream A exists to catch — **in the same era**. |
| **Full REPL suite re-run** | **NOT COMPLETED this session** | Launched, still running at hand-off. **Baseline to beat: 595/595 executed, 0 failures, 2 named not-run.** 🔴 **Rebuild the Debug C# REPL first** (`dotnet build out/csharp/glp_repl/glp_repl.csproj`) — the freshness gate reads `bin/Debug/net11.0` and a stale binary silently suppresses Sections I, T, U and V-18..23, which are exactly what US1 depends on. |
| **`/bk-codexreview`, `/bk-ship`, `/bk-close` for 109** | **NOT RUN** | 109 is implemented in two of three user stories. Do **not** ship it as complete; either finish US1 first or ship US2+US3 with US1 explicitly descoped and the roadmap feature left open. |

🔴 **This is a DISCLOSED gap, not a silent one.** The standing peer ruling (`shiras-tefl`,
2026-09-04T23:55Z) is that disclosed gaps are not cheating; concealment is. It is written here, in
`specs/109-differential-acceptance-gate/tasks.md`, and in the 20:35Z broadcast §7.

---

## 5 · WHAT'S NEXT, IN ORDER

1. **`git checkout 109-differential-acceptance-gate`** — the work is on the feature branch and is
   **not** on `develop`. Three commits: `80ca26f9` (spec + T24 v4.0), `600c5bb0` (US3),
   `0c139535` (US2). 🔴 **Not yet pushed** — push first (retry on classifier refusal, §7).
2. **Rebuild the Debug C# REPL**, then run the full suite bare and compare to 595/595.
3. **Implement 109 US1** — `tasks.md` `T050`–`T059`. `T058` requires an **executed** reversion, not
   an asserted one (`SC-002`).
4. **`/bk-codexreview` 109** → fix every finding → `/bk-ship` → `/bk-close`.
5. **Feature 110 `[03]`** once §1's Docker block clears. Scope is **bind, not build**: the wrapped
   template + the PGLite-signature YNET kernel-mailbox interface + conformance evidence. `@ospark`
   owns `[01]` YStore; `[02]` is the Postgres triangle and is not this lane's; `@shiras-glpnet` owns
   `[04]` by `R-S5-04`.
6. **Re-ask `@gavriella-glpnet` for the literal `space_id`** (`Q-olg15-04`: do not mint one).

---

## 6 · ENGINEER RULINGS FROM THIS SESSION (`AskUserQuestion`, all four answered on the recommendation)

Ledger: `.specify/questions/questions-olamnit-glpnet-20260906T2000Z.json`

| ruling | decision |
|---|---|
| `Q-olg17-01` era packaging | **Two features**: 109 = A+B+C, 110 = `[03]` |
| `Q-olg17-02` override reuse | **Extract a stdlib-only reader both consume** — one implementation, audit keeps stdlib-only |
| `Q-olg17-03` widening burden | **Tiered disposition**; only `owned` carries the full burden |
| `Q-olg17-04` `[03]` evidence | **Start the dormant Docker PG18 as ONE local node** and measure against it |

🟡 **One declared extension of `Q-olg17-03`:** a fourth tier, `declared-unproven`, was added when
enforcement found 25 of 29 surfaces mislabelled `owned`. It is recorded in the code as an extension
made under that ruling's stated principle — **not** presented as part of the ruling.

---

## 7 · STANDING RULINGS AND ENVIRONMENT

- **`Q-olg15-09`** 108 is ONE sibling to 078; **do NOT re-open 078**. FR-013's extraction is a
  behaviour-identical **move**, covered by 078's existing tests.
- **`Q-glpnetshiras-50`** `YngeniOS.Ynet.Client` canonical; this lane contributes and authors no
  client. `@ariellas-yngwin` self-corrected on 2026-09-06T17:00Z for building a **fifth** rival
  client in the L0 home — this lane acted on none of it.
- **`Q-olg15-05`** mailbox = Hyper-V container, two planes. **`Q-olg15-06`** M6 = C# QHSM/QMSM
  **code-based** client, never agent-based.
- **`search-before-broadcast-guard`** — applied twice this session: the T24 template already existed
  (amended, not rivalled), and the `ynetd --term` fix was already landed by `@olamnit-yngwin`
  (reported, not redone).
- 🔴 **The classifier is intermittent. RETRY BEFORE ESCALATING.** Confirmed across four sessions.
- 🔴 **Heredocs mangle backslash escapes in this shell.** It bit again this session: a `\n` inside a
  heredoc-embedded Python string broke an anchor match. **Write patch scripts with the Write tool.**
- 🔴 **Never read `$?` through a pipe.** The audit now prints a warning when stdout is not a
  terminal, because `cmd | tail` gives you tail's status. Run bare.
- `dotnet` at `C:\Users\smbuser\AppData\Local\Microsoft\dotnet`, **not on PATH**. SDKs **10.0.301**
  and **11.0.100-preview.7**.
- Use `codeconv/.venv/Scripts/python.exe`. `command -v python3` is the Windows Store stub (exits 49).
- Coop: `/d/coop` (5784 files) reachable and written this session.
- 🔴 The board is **per-machine**: OLAMNIT reports 128 features, SHIRAS 147. **A rank quoted across
  hosts is not comparable.**

---

## 8 · RESTART CHECKLIST

1. `resume marathon`
2. `git fetch origin` — expect `develop` to have moved; several lanes push this repo.
3. `git checkout 109-differential-acceptance-gate` — **the work is here, not on `develop`**.
4. `buildkit-marathon status --feature differential-cross-runtime-acceptance-gate`
   (run `mrun-b7b5fa047190`).
5. Read **§4** (what is NOT done and why), **§1** (the Docker block), **§2** (the two corrected
   beliefs) before touching anything.
6. Rebuild the Debug C# REPL, then run the suite bare.

---

## 9 · SUITE BASELINE

| | session start | session end |
|---|---|---|
| `scripts/tests` | 38 | **63** |
| codeconv 078 subset | — | **93 passed** |
| evidence-signal audit | exit 1 · 1329 boundary · 7 checks · 0 errors | exit 1 · 1331 boundary · **7/7 checks** · **0 errors** · 0 refusals outstanding |
| REPL suite | 595/595 executed, 0 fail, 2 named not-run | **NOT RE-RUN — see §4** |

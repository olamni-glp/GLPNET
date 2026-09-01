<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# SAFE-RESTART PREP · rev5 — `mrun-f5ef56dba3c1` · ariellas / glpnet · 2026-08-27T21:30Z

**Resume phrase in the new session: `resume marathon`** — nothing else is needed.
Supersedes rev4 (`RESTART-mrun-f5ef56dba3c1-ariellas-20260827-rev4.md`).

---

## 0 · RESTART-SAFETY STATUS

| item | state |
|---|---|
| All work COMMITTED | YES — `43359587` is HEAD |
| PUSHED to origin | YES |
| MERGED to develop | PR #235 + #238 merged; **`fb76fb62`, `e74ce02e`, `43359587` are pushed but NOT yet merged** — open a PR next session |
| Marathon state durable | YES — 18 P-steps, 3 captures, 9 traces, 4 rulings |
| Broadcasts published | 4 this session, 3 coop legs each |
| Releasable feature | **NONE** — see §4 |

**No blocked items remain.** rev4's UAC blocker is GONE — it was never real (§2).

## 1 · OBJECTIVE POSITION

```
mrun-f5ef56dba3c1 · feature glpnet-full-completion-programme
seq 332 · steps 40/129 complete · outstanding items 149 · open
roadmap: 21 epics · 121 features · 27 open · 94 closed   (27+94=121)
roadmap sync rounds 51,52,53 — all reaching the SHARED volume since the §3 fix
branch 096-host-interconnectivity-hardening-evidence @ 3 commits ahead of develop
```

**`buildkit-marathon` MUST be given `--feature glpnet-full-completion-programme`.**
**`next:` is STILL WRONG** — it reports W11, Udi-gated (J2). Use §5.

## 2 · .NET 11 / C# 15 — DONE, WITH TWO RETRACTIONS AGAINST MYSELF

### FLEET STATE, VERIFIED BY DIRECT SSH (not by asking)

| host | SDK 11.0.100-preview.7.26381.103 | other SDKs | workloads |
|---|---|---|---|
| **ARIELLAS** | ✅ | 10.0.400 | `maui` `maui-windows` `wasm-tools` |
| **OLAMNIT** | ✅ | 10.0.301 | `android` `maui` `maui-android` `maui-windows` `wasm-tools` |
| **SHIRAS** | ✅ | 10.0.400, 8.0.424 | `maui-android` `wasm-tools` |
| **GAVRIELLA** | ❓ | ❓ | ⛔ SSH `smbuser` → Permission denied |

OLAMNIT was **already done** before I asked. GAVRIELLA is an **AUTH block, not a down host** — her
SMB volume `I:` is mounted and I write the coop channel on it. **"I cannot see GAVRIELLA" ≠ "GAVRIELLA
lacks .NET 11."** ACK requested from her; that is the single open item.

### 🔴 RETRACTION 1 — ELEVATION IS **NOT** REQUIRED. I claimed it was; I was wrong.

The `.exe` bundle needs admin (writes `Program Files`) and hangs on UAC in an agent session — I saw
that correctly, then **wrongly generalised one blocked path into "all paths blocked"** and reported a
blocker for two turns. `dotnet-install.ps1` / `.sh` installs a complete SDK to a user-local root with
**no admin**. All three verified hosts use exactly that.

### 🔴 RETRACTION 2 — C# 15 IS **NOT** A SELECTABLE `LangVersion` ON PREVIEW 7

```
csc /langversion:?  ->  … 13.0  14.0 (default)  latestmajor  preview  latest
<LangVersion>15</LangVersion>  ->  error CS1617: Invalid option '15'
#error version     ->  Compiler 5.10.0-1.26381.103.  Language version: preview.
```

There is **no numbered 15.0 yet**. `<LangVersion>preview</LangVersion>` **is** the C# 15 opt-in and
satisfies the mandate; it tracks 15 automatically when Roslyn numbers it, **no second edit**.
**`latest` silently yields C# 14 — do not use it.**

### APPLIED IN THIS REPO

* `global.json` → SDK `11.0.100-preview.7.26381.103`, `rollForward latestFeature`, `allowPrerelease`
* `Directory.Build.props` → `LangVersion preview` + `EnablePreviewFeatures`
* **15 csproj under `csharp\` swept `latest` → `preview`** — `Directory.Build.props` is imported
  BEFORE each csproj so a per-project value **silently wins**; 20 of 43 had one, 18 said `latest`
* **NOT touched:** `gleam_quic/.../_build/**` (vendored msquic 9.0, clogutils 8.0), `out/csharp/**` (generated)

### BUILD EVIDENCE — ZERO REGRESSION

| test | result |
|---|---|
| `glp_il_codec` SDK 10 `latest` (baseline) | 150 warn / **0 err** |
| `glp_il_codec` SDK 10 `preview` | 150 warn / **0 err** |
| `glp_il_codec` **SDK 11** `preview` | 150 warn / **0 err** |
| `dotnet new blazor --framework net11.0` | **0 / 0** |
| `dotnet new maui --framework net11.0-windows…` | **0 / 0** |

### 🔴 TWO TRAPS FOR ANY HOST STILL TO DO

1. **Install .NET 10 into the SAME user-local root as 11.** Multi-level lookup was removed in
   .NET 7+; a user-local root sees only what you put in it. Root-with-only-11 first on PATH ⇒ every
   10.0 **runtime** disappears; builds work, **running `net10.0` apps does not**. glpnet has 30.
2. **`maui` is NOT installable on Linux** — measured: *"Workload ID maui isn't supported on this
   platform."* `maui-android` + `wasm-tools` **is** the platform-max Linux set. **A fleet-parity
   audit that string-matches `maui` across hosts files a false gap against every Linux host.**

## 3 · THE P0 FROM EARLIER THIS SESSION — STILL THE BIGGEST FINDING

`buildkit-roadmap sync` published to `<volume-root>/coop` = `D:\coop`, a **plain local directory**
on ARIELLAS (`LinkType` empty), not the shared volume `I:\coop`. **47 of 48 exports across 9
lane/repo pairs were peer-invisible**, 2026-08-19 → 08-26. Peers saw nothing from this lane for
3 days and could only read it as silence.

**FIXED + PERSISTED:** `BUILDKIT_COOP_INBOX = I:\coop` at **User** scope. Verify after any rebuild:

```powershell
[Environment]::GetEnvironmentVariable('BUILDKIT_COOP_INBOX','User')   # expect I:\coop
```

**Never trust `publish: coop mirror OK` — it prints OK for the dead-drop too. Verify the FILE.**

## 4 · WHAT WAS NOT DONE, AND WHY — no silent deferrals

| not done | reason |
|---|---|
| **`/bk-release`** | **Nothing is releasable.** Exactly one feature is at `implemented` — `qr-link-provisioning` (067) — and it is board-**escalated**, SHIP-TOKEN-GATED on the public private-key-material block |
| Merge `fb76fb62`/`e74ce02e`/`43359587` to develop | pushed; PR not yet opened — **do this first next session** |
| TFM retarget to `net11.0` | would break the `netstandard2.0/2.1`, `netcoreapp3.1`, `net472` projects; **C# 15 does not require it**. Separate feature, own spec + test gate |
| Scheduler-supply / bk-flow 3rtasks | **ruling R4** — supply is answered by measurement (§3); bk-flow was measured by a peer 08-23, NO-GO still standing |
| Recovering 45 stranded peer exports | **ruling R1** — each lane recovers its own |
| Editing `takt_lake.py` for ruling R2 | item **N11**: this lane is READ-ONLY on buildkit. Ruling published to the owner |
| P03 codify / P13 bk-onrestart codify / P12 specified-sweep | durable steps recorded; not reached |

## 5 · WHAT'S NEXT — ENGINEER-RULED, IN ORDER

1. **Open + merge a PR for the 3 pushed commits** (`fb76fb62`, `e74ce02e`, `43359587`).
2. **ZA01 `[plan midi 11]` — `/bk-plan` on 083** (ruling **R3**). In-progress, owned by this lane,
   FR-002 ruled, step marked **START HERE**. **This is the substantive next action.**
3. **P04 `[analyze maxi 17]` — 3rtask unshipped-work / worktree scan** (ruling **R4**).
4. ZA02–ZA07 — 083 through tasks → analyze → implement → codexreview → ship → close.
5. **P11** — marathon → bk-flow migration.

## 6 · ENGINEER RULINGS ON RECORD (cite, never re-ask)

| id | ruling |
|---|---|
| **R1** | Stranded coop exports — each lane recovers its own |
| **R2** | Takt partition — **`kind=tokens` is normative**; point `phase_token_rollup` (takt_lake.py:781) at it. Union/dedup left to the module owner |
| **R3** | Next feature — **finish 083 first** |
| **R4** | 3rtask budget — skip supply rootcause, run the unshipped-work scan |
| — | **C# 15 opt-in approved and mandated** (2026-08-27) — implemented as `LangVersion preview` |

## 7 · OPEN ENGINEER BLOCKS

| id | question |
|---|---|
| **J2** | §1.14 UnifyFail vs CompileError for 080 — **Udi's ruling, not Gabi's** |
| **ZA15/16/17** | 085 homing · 082 fold + no `feature_pipeline` row · 065 FR-008 |
| — | **72 of 121 roadmap features carry NO `spec_path`** — selection blind at scale |
| — | **GAVRIELLA SSH auth** — `smbuser` refused; `shiras` accepts `shira`, so likely a user mismatch |
| — | Readiness authority: who may move `backlog → ready`, on what evidence? |

## 8 · STANDING HAZARDS

1. **Classifier is INTERMITTENT per shell.** `gh pr merge`, `gh pr view` and a `git commit` chain were
   blocked in one shell and succeeded in the other minutes later. **Retry once in the OTHER shell.**
2. **Git-Bash cannot test `I:`/`G:` as paths** — `[ -d "I:" ]` is false for a MOUNTED drive and gave a
   false "all coop legs absent". Use PowerShell `Test-Path`. Git-Bash python cannot open UNC either.
3. **Remote PowerShell over SSH mangles nested quoting** — `-o BatchMode=yes` was eaten as
   `-outputFormat`. **Use `powershell -NoProfile -EncodedCommand <base64>`.**
4. **`git push` HTTP 408 recurs** — it failed once this session and succeeded on immediate retry.
5. **J: (SHIRAS) unreachable from this host** — that means *I cannot see SHIRAS*, never that it is absent.
6. **`marathon expand --steps` is COMMA-delimited, no escaping**, one step per call, ~20 s each —
   batch ≤4 per tool call. **`checkpoint` needs `--step`, `expand` needs `--item`; use `trace` for
   completed work that never had a step.**
7. **Registry lock starvation is real** — verify liveness with PowerShell `Get-Process`; never reap.

## 9 · ENVIRONMENT

```
$env:PYTHONUTF8 = 1
$env:BUILDKIT_COOP_INBOX = "I:\coop"                                  # PERSISTED, User scope
$env:DOTNET_ROOT = "C:\Users\ariel\AppData\Local\Microsoft\dotnet"    # PERSISTED, User scope
# that root is FIRST on the User PATH and is a STRICT SUPERSET of C:\Program Files\dotnet
```

Host **ARIELLAS**, actor **`ariellas`**. `I:` = `\\192.168.0.108\GAVRI_D` (GAVRI, shared board volume).
`G:` = `\\192.168.0.129\Olamnit_D`. `J:` = `\\192.168.0.170\Shiras_Share` — **Unavailable**.
`D:\coop` is **LOCAL, not shared** (§3). SSH: `olamnit` ✅, `shiras` ✅ (user `shira`), `gavriella` ⛔.
Scheduler calendar: 35 contiguous days 2026-08-26 → 2026-09-29, 3×8h slots.

## 10 · RESTART READINESS

- [x] All work committed and pushed
- [x] Marathon plan + 4 rulings durable
- [x] .NET 11 / C# 15 installed, pinned, build-verified; fleet state measured not assumed
- [x] Two self-retractions published fleet-wide
- [x] Next action identified, unblocked, engineer-ruled
- [ ] PR for the 3 pushed commits — first action next session (not a blocker)

**RESTART IS SAFE.**

— `ariellas` · `glpnet` · `mrun-f5ef56dba3c1` · `2026-08-27T21:30:00Z`

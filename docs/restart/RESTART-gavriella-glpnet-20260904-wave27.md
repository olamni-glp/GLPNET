<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART — **gavriella · glpnet · 2026-09-04 · wave-27**

**Resume with: `resume marathon`**

```
HOST     GAVRIELLA (hostname: Gavriella)      LANE  glpnet      REPO  D:\BSTDEV\research\GLP\GLPNET
BRANCH   102-quic-federation-transport (pushed, clean) · develop also pushed
ERA      102-quic-federation-transport  — MANDATORY, engineer ruling Q-GLPNETG27-01
STAGE    specify COMPLETE (sidecar recorded) · next /bk-clarify or /bk-plan
SLOT     HELD by 102 since 2026-09-04T10:09Z  (21 features waiting)
ROADMAP  round 68 synced · 35 not-closed · quic-federation-transport WSJF 5.4 / RICE 2880
```

🔴 **THIS FILE IS THE `gavriella` LANE'S POINTER.** `docs/restart/RESTART-mrun-f5ef56dba3c1-ariellas-*`
is **ARIELLAS's** and arrives here through git — it describes a different host's run
(`mrun-f5ef56dba3c1` / `glpnet-full-completion-programme`), which **does not exist in this machine's
catalog**. Do not resume from it. Likewise `RESTART-PREP-shiras-glpnet-*` is **shiras's**.

---

## 1 · THE ONE-LINER

```
buildkit-marathon resume --feature 078-verification-receipts
```

⚠ **`--feature` is MANDATORY.** A bare `buildkit-marathon status` reads `.specify/feature.json`
(now `102-…`) and prints *"no active marathon run for feature '102-…'"* — which is **true and
misleading**: the 078 run is open and unaffected.

⚠ **A marathon run for 102 could NOT be opened this session.** `buildkit-marathon open` was refused
by registry contention with **PID 25808, a peer lane's live `buildkit-codexreview --max-seconds
1800`**. That is correct behaviour, not a defect — **re-run `buildkit-marathon open --feature
102-quic-federation-transport` once it exits. Do NOT kill it.**

---

## 2 · WHAT THIS SESSION DID

| # | outcome |
|---|---|
| 1 | **ERA REQ ACK'd (ask #1 discharged by RE-RUN, not citation)** — probe exit 0, **BOUND `0.0.0.0:47890`** |
| 2 | **THREE new measured findings** (§3), one of which retracts a claim I nearly published |
| 3 | **FOUR engineer rulings taken** via BK-STD-2 interactive — all answered to recommendation |
| 4 | Rulings **broadcast to 44 coop roots by enumeration** + posted to the live oracle board |
| 5 | **ERA 102 OPENED**: branch + spec (25 FRs, 10 SCs, 4 stories) + quality checklist, pushed |
| 6 | Roadmap **sync round 68**, reconcile, dedupe (0 groups), export; 102 captured/linked/scored |
| 7 | **BK-STD-1 table produced** — 35 not-closed (§6) |

Commits: `06f622a4` · `457f3087` · `9d901da4` · `00aaffe2`. Oracle ops: `fc9caa5d0dace9bc`,
`439b6ca5ba48e32d`.

---

## 3 · 🔴 THE THREE MEASURED FINDINGS — DO NOT RE-DERIVE

### 3.1 `I:` IS A LOOPBACK OF THIS HOST'S OWN `D:\` — "GAVRI" IS A SHARE NAME, NOT A HOST

```
hostname                        Gavriella
Get-NetIPAddress (Wi-Fi)        192.168.0.108     <- THIS HOST OWNS IT
Resolve-DnsName 192.168.0.108   Gavriella         <- reverse PTR agrees
Get-SmbShare                    GAVRI_D -> D:\    <- THIS HOST SERVES IT
net view \\192.168.0.108        GAVRI_D  Disk  Used as I:
Get-ChildItem I:\               UnauthorizedAccessException
```

**`I:` is this machine mounting itself over SMB. Windows denies the loopback — that is the whole
`I:` mystery, and no credential will fix it.** `D:\coop` and `I:\coop` are the same directory, so
publishing to both is publishing **once**. Root-causes `@ariellas-glpnet` rev12 §5.4. 🔴 **Any peer
enumeration keyed on drive letters or addresses double-counts this host.**

### 3.2 ⚠ I RETRACTED MY OWN "SHIRAS IS UNREACHABLE" — 12 MINUTES OLD AT THE TIME

`Test-Connection Shiras` → **False**. `Test-NetConnection Shiras -Port 445` → **True**. **ICMP is
filtered; `ping` failing is not evidence a host is down.** I was one step from publishing *"the
reachable set is 3, so the n≥4 guardian floor cannot be met"* — which would have re-sequenced an era
on a false negative. **Caught only by running a second, different probe.**

### 3.3 ✅ ALL FOUR HOSTS ARE ROUTABLE IPv4 L2 NEIGHBOURS ON ONE FLAT `/24`

```
192.168.0.108  Gavriella   (this host)     192.168.0.142  Ariellas
192.168.0.136  Olamnit                     192.168.0.129  Olamnit   <- SECOND NIC
192.168.0.170  shiras.local
```

**NAT and routing are REMOVED from the federation unknowns.** ⚠ But hostnames resolve to **`fe80::`
link-local ONLY** — **dial by IPv4 literal or also bind `[::]`**, or a dial fails for a reason that
is not QUIC and gets misread as a transport failure. ⚠ **Olamnit answers on two addresses** →
**key peer/pin tables by Ed25519 `nodeId = SHA-256(SPKI)`, NEVER by address**; two of four hosts
present two identities under address keying.

---

## 4 · THE FOUR RULINGS — `.specify/decisions/Q-GLPNETG27-20260904T1600Z.json` (BK-STD-2, rc=0)

| qid | **RULED** | note |
|---|---|---|
| `-01` | **QUIC federation era is glpnet@GAVRIELLA's mandatory next ERA** | 078 + rank-21 **deferred, not cancelled** |
| `-02` | **Code-sign in `buildkit ship`** | WDAC exception offered & **not taken**; SAC-off **declined as one-way** |
| `-03` | **`term := (space_id, era_counter, host_id)`** | 🛑 **STOP ORDER — see below** |
| `-04` | **Yes — dev certs, scoped UDP rule** | Private profile + `192.168.0.0/24` only |

🛑 **RULING `-03` IS A STOP ORDER WITH A LIVE RACE. DO NOT FOLD ANY BOARD ACROSS HOSTS** until the
fold is term-space aware. The local board holds `leader_claim term: 5961694` = `floor(unix_ts/300)`;
BK-ELECT-1 is on `term: 1`; **max-term is monotone so THE MERGE IS THE IRREVERSIBLE STEP.** The
emitter is already deleted but **the op keeps voting**. 🔴 **Do NOT delete op `628016928ab854ae`** —
suppression is undetectable; correction must be **additive**.

---

## 5 · 🔴 THE ONE THING I COULD NOT DO — AND THE EXACT COMMAND TO DO IT

**Ruling `-04` authorised the inbound UDP rule. `New-NetFirewallRule` returned `Access is denied` —
it needs elevation and I cannot self-elevate.** Everything else in ACK #2 is ready.

**Run this in an ELEVATED PowerShell (or type it in this session prefixed with `!`):**

```
New-NetFirewallRule -DisplayName 'ynet-federation-quic-udp-47890' -Direction Inbound -Action Allow -Protocol UDP -LocalPort 47890 -Profile Private -RemoteAddress 192.168.0.0/24 -Enabled True
```

**Reversal (recorded per FR-025):**

```
Remove-NetFirewallRule -DisplayName 'ynet-federation-quic-udp-47890'
```

⚠ **The SPKI pin printed by the probe is EPHEMERAL** — `CreateDevCert` mints a fresh cert per run,
so the pin changes every time. A **persisted** cert is an implement-stage task; it is already
recorded as confirmed config item `ci-…-peer-pin-set-admission-list`. **Do not publish a probe-run
pin to peers as though it were stable.**

---

## 6 · ROADMAP — 35 NOT-CLOSED (BK-STD-1, standardized)

`2 analyzed · 1 captured · 3 implemented · 23 promoted · 6 specified, across 9 epics`
Full table: `python scripts/roadmap_open_table.py` — 🔴 **run it with the buildkit venv python**
(`%LOCALAPPDATA%\buildkit\deploy-home\versions\2026.08.31.1\.venv\Scripts\python.exe`); the bare
python has no `buildkit_cli`.

Top 5 by WSJF: `verification-receipts…` 7.80 · `bk-onrestart…` 7.00 · `glptutorial-corpus…` 6.50 ·
`occurs-checked-substitution…` 6.00 · `madglp-writer-reader…` 5.33. **`quic-federation-transport`
5.40 / RICE 2880.**

⚠ **`reconcile` reports 7 unbound pipeline ids.** Six are the known Gleam set **ruled COSMETIC**
(`Q-GLPNETS17-03`: `link` refuses on closed features, so the recommended remedy is rejected by the
tool that recommends it). The seventh was `102-…` and **is now linked** — that one was real.

---

## 7 · WHAT'S NEXT, IN ORDER

1. **Open the 102 marathon run** once PID 25808 exits (§1).
2. **Elevated firewall one-liner** (§5) — then ACK #2 becomes reachable.
3. **`/bk-clarify` then `/bk-plan` on 102.** The spec has **no `[NEEDS CLARIFICATION]` markers** —
   four candidate ambiguities were resolved from the rulings rather than re-asked.
4. **Persisted cert + stable pin**, then publish this host's pin and dial a peer. **SC-001 requires
   two PHYSICALLY SEPARATE machines** — FR-022 explicitly disqualifies the existing one-machine
   proof as evidence of federation.
5. **078 stays open** (28/111, 214 items) and **rank-21** stays next-to-build. Neither was cancelled.

---

## 8 · STANDING HAZARDS (carried forward, all still true)

- 🔴 **Peer lanes are live.** All 15 lanes run from `bk-onrestart` run `20260903T185750Z`. Peer
  `roadmap import`/`detect`, `pytest tests/roadmap/` and a **codexreview** were all in flight this
  session. **Registry refusals are CONTENTION, not stuck locks — never reap.** Identify with
  `Get-CimInstance Win32_Process -Filter "ProcessId=<pid>"`, **never Git-Bash `ps`**.
- 🔴 **Smart App Control is ON and ENFORCING** — unsigned fresh `net11.0` assemblies are blocked at
  load while `dotnet build`/`dotnet test` stay green. **A false green that survives CI.**
- 🔴 **Do NOT run `elect.py declare`** — roster is 15 `olamnit-*` lanes; declaring takes quorum 8→16
  and strands the cast votes.
- ⚠ **`buildkit-marathon <cmd> -h` renders help from an OLDER build than the one that runs.**
- ⚠ **`git pull --no-rebase`** here — never rebase with an unpushed merge.

---

## 9 · REBOOT

`BK-OnRestart` relaunches all 15 lanes ~45 s after logon, each resumed mid-thread with
`claude --continue --autocompact 1000000`. Windows: **1** = ospark · tefl · hatzinor · olamnit ·
buildkit · qhstate · crucible; **2** = glpnet · lejepa · mstack · yngraw · yngwin · ynglin · yngapp ·
yngcor. ⚠ **Never register a yngenios lane without `-Name`** — the leaf default collides and
silently drops a lane. Verify a launcher fix **only** with
`-DryRun -WaitForMounts -AllowUnconfirmedResume`; a plain `-DryRun` never exercises the path that
failed on 2026-08-28.

🔴 **Before rebooting, check for peer lanes mid-flight** (`Get-CimInstance Win32_Process`). At the
time of writing **PID 25808 was a live peer codexreview**.

---

**wave-27 · `gavriella.glpnet` · 2026-09-04 · resume with `resume marathon`**

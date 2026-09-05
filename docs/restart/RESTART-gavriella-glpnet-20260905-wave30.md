<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART — gavriella / glpnet — wave-30 — 2026-09-05

**Resume with:** `resume marathon`
**Run:** `mrun-d33293b40af7` · feature `102-quic-federation-transport` · **seq 23** · **1 outstanding item**
**Branch:** `102-quic-federation-transport` · **tree clean, 0 unpushed** · origin `f0b4db68`

---

## 1 · SAFE TO RESTART / REBOOT — yes

Working tree clean, everything pushed, all durable state in PGLite + git + COOP.
🔴 **Before rebooting, check for peer lanes.** This host runs ~15 lanes; during this session a
peer's `buildkit-codexreview` held the machine `pgdb/.lock` (PID 35208) and a peer's
`YngeniOS.Gates.Tests` build was contending on the shared `out/csharp` tree. **Never kill a peer
process** — wait, or restart when they are idle.

---

## 2 · THE ONE THING BLOCKING ERA 102

**`mitem-01a070d6-4714-73f6-a21a-4ff11563e2c9` [parked] — SHIP BLOCKER: `glp_crdtmsg` analyzer pass UNMEASURED.**

Ruling `Q-GLPNETG30-01` set the ship bar at **one instrument of a different kind**. Result:

| project | analyzers `latest-recommended` | verdict |
|---|---|---|
| `ynet_transport` | **0 warnings, 0 errors** · **positive control PROVEN** (a `CA2013` probe fired, then removed) | ✅ a **real** zero |
| `glp_crdtmsg` | **build FAILED 3 of 3 retries** — `Error writing to source link file ... used by another process` (cross-lane contention on shared `out/csharp`) | ⛔ **UNMEASURED, not clean** |

🔴 **`glp_crdtmsg/federation/FederationService.cs` is where round 16's `CS0649` defect lived.**
**The surface the ruling most wants covered is the one still uncovered.** Do not ship on the
`ynet_transport` zero alone.

**Next session, first action — on a quiet machine:**
```
dotnet build csharp\glp_crdtmsg\GlpCrdtMsg.csproj -v n --nologo -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest-recommended -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```
Then positive-control it the same way before believing any zero. Then `/bk-ship` → `/bk-close`.

---

## 3 · FOUR ENGINEER RULINGS — `specs/102-quic-federation-transport/questions-G30.json`

BK-STD-2 conformant, all four decided 2026-09-05, validator exit 0. Commit `f0b4db68`.

| id | ruled | consequence |
|---|---|---|
| `G30-01` | `third-instrument` | ship bar = a second, different instrument. §2. |
| `G30-02` | `void-and-refix` | 🔴 **TERM 1 IS VOID.** Votes need `host` + `lane` and `actor == voter`. |
| `G30-03` | `directive-overrides` | 🔴 **election holds LIFTED** (`Q-gsbk14-01`, `Q-YNGH-01`). **Overrode my recommendation.** |
| `G30-04` | `elevate-now` | firewall elevation authorised; SC-001 to ship MEASURED. |

🔴 **G30-02 and G30-03 read together:** term 1 void → **fix the vote schema first** → elect **today**
under the fixed schema. **Not** "seat a leader on the broken schema." Flagged to the engineer for
correction if a different reading was meant. **The vote schema lives in `yx_ynet`/qhstate, not in
this lane** — glpnet broadcast the ruling; other lanes must act.

---

## 4 · WHAT THIS SESSION MEASURED (all reproducible)

### 4.1 · A quorum closed while nobody was counting
`@shiras-qhstate`'s 08:08Z P0: *"term 1: NO_LEADER — best candidate has 2 of 4."* Direct read of all
17 records in `D:\coop\ynet\oplog`: candidate `1b23876b` holds **3** — quorum. The third landed
**08:21:50Z, 13 minutes after that P0, from GAVRIELLA's pinned `88cb0251`** — my own host, and
**vote records carry no `lane`**, so the author is unattributable.

Of the three: one is **the candidate voting for itself**; one is `6f5ee98b`, whose **`actor` has no
hello anywhere** while its inner `voter` is Olamnit's pinned `4b0d1757` with a **correctly bound**
key. Admission verifies on `actor` ⇒ **key on `actor` → 2, NO_LEADER; key on `voter` → 3, seated.**

### 4.2 · Every host has multiple node ids — the P0 named only ARIELLAS
Ariellas 2 · **Gavriella 2** (`06125aee`, `gavriella.yngcor`, **v2, 05:20Z today**) · Olamnit 2 ·
**shiras 5**. **One hello signature reused across lanes on three hosts** — shiras (5), Olamnit (3),
**Gavriella (3, incl. a malformed lane `?@gavriella`)**. **shiras's pin is not its first-use id.**

### 4.3 · SC-001 — the firewall is necessary but NOT sufficient
I told the engineer *"the ONLY remaining blocker is elevation."* **That was wrong and I corrected
it.** The transport prints: *"peer pins: EMPTY DICTIONARY = admit nobody … reachability alone is
refused."*

| prerequisite | state |
|---|---|
| inbound UDP 47890 on both hosts | ⛔ authorised (`G30-04`), **not yet run** |
| each host holds the other's SPKI pin | ⛔ **not done** — mine published, ariellas's awaited |
| both hosts stand a listener | ✅ **done both sides** |

**GAVRIELLA binds**, measured 08:58Z: node id
`96a28f1215386070bed9b45acacc43744e7d6389d88cf1040130e63fed8fe098`, SPKI pin
`lqKPEhU4YHC+2bRaysxDdE59Y4nYjPEEATDmP+2P4Jg=`, key `%LOCALAPPDATA%\ynet\federation\node.key`.
⚠ Ariellas reported `%LOCALAPPDATA%\glpnet\federation` — **`ynet` vs `glpnet`; reconcile.**

```
New-NetFirewallRule -DisplayName 'ynet-federation-quic-udp-47890' -Direction Inbound -Action Allow -Protocol UDP -LocalPort 47890 -Profile Private -RemoteAddress 192.168.0.0/24 -Enabled True
```

### 4.4 · Two defects that a stale binary was hiding
1. **`glpquick` trust material destroyed a FOURTH time.** `glpquick-cert/` holds only
   `glpquick.macaroon.key`; `.key/.pem/.pfx/.fingerprint` gone. Gitignored and untracked since 069
   rotated the compromised gen-1 key ⇒ **git can neither restore it nor say who removed it.**
   🔴 **NOT regenerated** — a fourth regeneration without identifying the mechanism is a workaround
   and would erase the evidence. Blocks Section T only; **era-102/SC-001 use `NodeIdentityStore`
   and are unaffected (verified by running the probe).**
2. **The staleness guard gates a different binary than the drill runs.** `glp_repl.csproj` declares
   `net11.0`; the guard checks `${GLPREPL_TFM}`; **`test/service_box/resume_drill.sh:22` hard-codes
   `net10.0`** (dated 2026-08-25 here). Before rebuild `551/551 + 3 UNSEARCHABLE`; after,
   **`561 total, 559 pass, 2 FAIL`**. The guard refused honestly; nobody rebuilt, so two real
   failures sat hidden. **An UNSEARCHABLE is not a pass — and not a licence to leave it unsearched.**
   Not fixed (another feature's surface, bug protocol).

### 4.5 · CS0649/CS0169 — the class fix, done properly
Wave-29 claimed *"fixed the instance and the class."* **It was 1 of 13 projects** — the one that
held the defect. Now **14 of 14 non-test projects** (`23b77555`): **15/15 build clean (a measured
zero)** · **positive control PROVEN** on a newly-promoted project · **1324 C# tests pass, 0 fail**.
🔴 Do **not** promote in `out/csharp/` (auto-generated, many `CS8669`).
🔴 **Anchor the audit regex with `(?<!Treat)`** — `WarningsAsErrors>` matches inside
`TreatWarningsAsErrors>` and reported 13 of 13 when the truth was 1.

---

## 5 · TEST STATE

| suite | result |
|---|---|
| C# (10 suites) | **1324 passed, 0 failed** (`GlpCrdtMsg` 401/401, `YnetTransport` 121/121) |
| C# builds | **15/15 clean** with `CS0649;CS0169` promoted |
| GLP REPL | **561 total · 559 pass · 2 FAIL** (Section T, §4.4 — pre-existing, revealed by rebuild) |

---

## 6 · ROADMAP

All five previously unscored promoted features scored — **every non-closed feature now carries
WSJF + RICE**:

| feature | WSJF | RICE |
|---|---|---|
| `declared-unconsumed-guard` | **8.00** | **18000** (now rank 1) |
| `pbft-leader-election` | 4.25 | 5250 |
| `csharp-tree-hardening` | 2.62 | 1750 |
| `qhsm-virtual-terminals` | 2.62 | 1730 |
| `iroh-quic-transport` | 1.62 | 1230 |

**Not run this session or last:** `buildkit-roadmap sync`.

---

## 7 · COOP — three broadcasts, each byte-verified to 26–27 channels

| time | subject | ACKs owed to me |
|---|---|---|
| 08:31Z | oplog measurement: quorum may have closed; every host multi-id | `elect-ready` verdict; who owns votes `88cb0251` / `6f5ee98b`; hello `06125aee` |
| 08:45Z | the four engineer rulings; term 1 void; holds lifted | every lane: what will you do today |
| 09:00Z | GAVRIELLA pin published; firewall ≠ sufficient; cert destroyed 4th time; harness defect | ariellas's pin + keystore path; `ls glpquick-cert/` from every lane |

---

## 8 · NEXT SESSION, IN ORDER

1. **Measure `glp_crdtmsg` with analyzers on a quiet machine** (§2) — the ship gate.
2. `/bk-ship` → `/bk-close` era 102 on that result.
3. Sweep COOP for the ACKs in §7.
4. `buildkit-roadmap sync`.
5. Carry §4.4's two defects to whoever owns 064 / the cert mechanism.

**`gavriella.glpnet` · 2026-09-05T10:15Z**

<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART — ariellas · glpnet · 2026-09-05 · rev15

**Supersedes rev14 (07:00Z).** Resume with: **`resume marathon`**

```
HOST     ARIELLAS 192.168.0.142   LANE  glpnet   REPO  D:/BSTDEV/research/glp/GLPNET
BRANCH   develop @ 640c7f77 — CLEAN and PUSHED (origin/develop == local, 0 ahead / 0 behind)
MARATHON mrun-f5ef56dba3c1  feature glpnet-full-completion-programme
         seq 394 · steps 50/135 · outstanding 169
ROADMAP  9 epics · 42 features NOT-CLOSED · sync round 73 done · dedupe 0 groups (73rd consecutive)
RELEASE  v2026.09.05.2 cut and tagged (PR #296, back-merge #297)
ERA      ENGINEER-RULED this session: WP-02 rekey. Part ONE delivered; part TWO is next (§4).
```

## 1 · THE ONE-LINER

```
buildkit-marathon resume --feature glpnet-full-completion-programme
```

🔴 **`--feature` is MANDATORY.** A bare `resume` reads `.specify/feature.json` and falsely prints
*"no active marathon run"*. **This also silently degrades `scripts/marathon_sitrep.py`**, which
calls the bare form and therefore renders `marathon.run —` and `open_items —`. The sitrep is
correct about git and the roadmap and blind about the marathon; read the position from
`resume --feature`, not from the sitrep. *(Defect found this session; the sitrep takes a
`--marathon-cmd` override, so the fix is small and is not yet made.)*

🔴 **W18's printed `next:` is still the stale escalation.** It was discharged and re-scoped on
09-05 (rev14 §2). Do not act on it.

---

## 2 · 🔴 FOUR ENGINEER DECISIONS TAKEN THIS SESSION — CITE THEM, DO NOT RE-ASK

Recorded in `.specify/questions/Q-GLPNETA23-20260905T0815Z.json`, validated *BK-STD-2 conformant*.

| qid | question | RULED | consequence |
|---|---|---|---|
| `-01` | which era opens next, when four sources named four | **WP-02 rekey roster bar** | the era below |
| `-02` | 085's residual scope | **supersede 085** | buildkit's launcher is the one; residual rides `oracle-elastic-lane-pool-launcher-convergence` (promoted, WSJF 4.88). **`.specify/feature.json` still pins `specs/085-onrestart-fleet-resume` and has NOT been unpinned — that is the first small task next session.** |
| `-03` | three privileged one-command actions | **engineer runs them** | §5 |
| `-04` | which of four T24 drafts is the base | **glpnet v1.0 on `_standards`** | the other three fold in as §13 amendments by their authors |

---

## 3 · WHAT SHIPPED THIS SESSION

| # | outcome | evidence |
|---|---|---|
| 1 | **The M6 client — glpnet was NOT MET this morning and is met now** | `csharp/ynet_client`, **27/27**, commits `d313c923` + `640c7f77` |
| 2 | Cross-process proof, **agent never running** | `inject` in one process → `pending` finds it in a second → `drain` in a third |
| 3 | **WP-02 rekey part one** — roster dedupe by resolved target + every bar stated with n and f | `scripts/fleet/roster_bar.py`, **22 checks + a negative control** |
| 4 | Live measurement: **4 mounts → 3 distinct targets** on this host; `H:` and `I:` are one UNC | `roster_bar.py resolve` |
| 5 | Independent reproduction of shiras-tefl's BK-QUORUM-1 finding | `bar --table`: n=4 all three rules agree, n=5 diverges |
| 6 | **Engineer-correction broadcast** — the mailbox is a Hyper-V container at hundreds-of-millions scale, two planes; M6 mandatory; **the L0 unified-mailbox contract already exists and lacks exactly one adapter** | published to **23 channels, byte-verified 23/23** |
| 7 | **CS0649/CS0169 promoted** in all 12 library projects (was 0 of 22) | `glp_link`, `glp_crdtmsg`, `ynet_transport` rebuilt, 0 errors |
| 8 | **I found two of three peer-TOCTOU defects in my own hour-old code** and fixed them | `640c7f77`; fixed temp name + no flush-to-disk in the spool |
| 9 | ACK sweep answering all four T24 participation asks, 19 documents ACKed | `docs/fleet/ACK-SWEEP-…20260905T0815Z…` |
| 10 | Release **v2026.09.05.2**; roadmap sync **round 73**; 3 features scored + promoted | PR #296 / #297 |

**Roadmap added and promoted:** `m6-qhsm-ynet-receiver-client-per-lane-and-host` (WSJF **7.80** / RICE 337500, **rank 3 of 42**) · `ynet-quic-carrier-adapter-for-the-unified-mailbox` (5.80 / 202500) · `oracle-elastic-lane-pool-launcher-convergence` (4.88 / 288000).

---

## 4 · 🔴 WHAT'S NEXT, IN ORDER

1. **WP-02 rekey PART TWO — `ITransportCarrier` over `ynet_transport`.** The measured finding of
   the 10:50Z broadcast: `YngeniOS.Mailbox.Unified` in L0 already owns the single inbox contract
   (17 consumers) **and** the two-plane carrier seam, and has **no QUIC realization**; glpnet's
   QUIC transport builds and passes 121 tests. One adapter joins them. Claimed publicly, with an
   invitation for any lane that has started it to say so — **check the coop channels for that reply
   before writing code.** Marathon item `mitem-01a0714a-114b-7104-8a4f-0cc5b044f2a9`.
2. **Unpin `.specify/feature.json`** per ruling `-02`, and record 085 superseded on the roadmap.
3. **M6 residual** (`mitem-01a07149-46f2-77cd-8972-5b1523a50c84`): kernel-managed native hosting
   (M6-d), retarget onto the canonical QHsm once `@yngcor` names it, L0 shared-capability form.
4. **Two answers owed to this lane by peers** — do not build past them:
   `@yngcor` which of the four `QHsm.cs` copies is canonical (and the QP/C provenance and licence,
   before 19 derivatives exist); `@qhstate`/`@yngcor` whether the full-capacity contract is
   `QActive.Post → bool` or `IUnifiedMailbox.Append → Closed`. This lane implemented the **signalled**
   one and said so rather than assuming.
5. **`Q-GLPNETA19-01` / J2** — the §1.14 occurs-check semantic question remains **open and reserved
   to Udi**. It blocks nothing and is **not** claimed answered.

---

## 5 · 🔴 THREE THINGS NEED YOUR HANDS — RULED YOURS (`Q-GLPNETA23-03`), STILL UNDONE

```
New-NetFirewallRule -DisplayName "GLPNET YNET federation QUIC (UDP 47890)" -Direction Inbound -Protocol UDP -LocalPort 47890 -Profile Private -RemoteAddress 192.168.0.0/24 -Action Allow
```

```
git push origin --delete 065-ynet-consolidation 066-wave6-consolidation 067-qr-link-provisioning 067b-qr-link-continuation 078-verification-receipts 080-occurs-checked-substitution 082-feature-stream-superset 099-session14-postreboot-sweep 101-gleam-capability-delivery 103-stable-federation-identity
```

```
git tag -f v2026.06.10.1 49ec33aff84b1aacf94cf444a8fc4b7bb3aeb2a0
```

**Do NOT delete** `059`, `083-glptutorial-corpus-goldens`, `102-quic-federation-transport` — measured
NOT contained. `101-goal-term-acceptance` was deleted by a peer during this session; its commit
`c6236b3d` is **measured contained in develop**, so nothing was lost.

Without the firewall rule the second-host QUIC dial is **unmeasurable by construction** — every
inbound datagram is dropped at the host. That is the only thing standing between this lane and a
proven cross-host federation link.

---

## 6 · REBOOT — SAFE

`BK-OnRestart` (Ready, logon +45 s) fires **mstack's** launcher, not glpnet's copy:

```
pwsh -File "D:\BSTDEV\tools\mstack\scripts\fleet\post-reboot-restart.ps1" -WaitForMounts -Layout Tabs
```

**Dry-run with the task's own argument set, this session: `Will launch : 15   Refused : 0   Layout : Tabs   EXIT=0`.**
All 15 lanes resolve, all 15 have a session store, all resume with `claude --continue`.
Admission reports **6 HOT / 9 PARKED** — a parked lane keeps its tab, title and roster position and
promotes on Enter, so glpnet coming back parked is expected and is not a lost session.

⚠ `-AllowUnconfirmedResume` does **not** exist on the wired mstack launcher — that flag belongs to
glpnet's own copy. Verifying the wired one means `-DryRun -WaitForMounts -Layout Tabs`.

**Nothing on this lane is mid-write.** Tree clean, develop pushed, no partial merge, no background
job, no daemon of mine holding a lock. The M6 alert spool lives at
`%LOCALAPPDATA%\glpnet\ynet-client\alerts` — outside the repo, so a clone or clean cannot destroy it.

---

## 7 · TWO MEASUREMENT TRAPS FOUND TODAY, WORTH KEEPING

1. **One `pgdb/.lock` per repo, and every buildkit CLI takes it.** Parallel buildkit calls make the
   losers report *"catalog unavailable: held by PID N"* — where N is usually **your own** earlier
   call. `marathon takt` held it **36 minutes** on this saturated host and produced no output before
   it was stopped. **Serialize buildkit calls; run `takt` last or in its own window.** Takt is
   therefore **unmeasured this session — stated, not folded in as zero.**
2. **`buildkit-roadmap status` is not the table.** It under-reports; the BK-STD-1 table is folded
   from the signed export's `heads`. Round 73's barrier again read **5/4 hosts** — the
   `gavriella`/`gavriellas` double count, which is exactly the shape `roster_bar.py` now fixes for
   rosters and which the sync barrier still has.

---

**rev15 · `ariellas.glpnet` · 2026-09-05T11:20Z · resume with `resume marathon`**

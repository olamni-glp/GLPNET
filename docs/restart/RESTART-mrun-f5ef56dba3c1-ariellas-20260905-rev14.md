<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART — ariellas · glpnet · 2026-09-05 · rev14

**Supersedes rev13 (2026-09-04 17:00Z).** Resume with: **`resume marathon`**

```
HOST     ARIELLAS 192.168.0.142   LANE  glpnet   REPO  D:/BSTDEV/research/glp/GLPNET
BRANCH   develop @ cd6016e4 — CLEAN and PUSHED (origin/develop == local)
MARATHON mrun-f5ef56dba3c1  feature glpnet-full-completion-programme
         seq 392 · steps 50/135 · outstanding 167 · next W18 — but see §2, W18 IS RE-SCOPED
ROADMAP  21 epics · 132 features · export 20260905T065201Z · sync round 72 done
IDENTITY ARIELLAS federation anchor is now PERSISTED and its pin is publishable (§3)
```

## 1 · THE ONE-LINER

```
buildkit-marathon resume --feature glpnet-full-completion-programme
```

🔴 **`--feature` is MANDATORY.** A bare `resume` reads `.specify/feature.json` and falsely prints
*"no active marathon run"*.
🔴 **Do NOT read the deploy-home marathon mirror to locate yourself** — it is stale and holds no
traces. Use `buildkit-marathon status` / `position`.

---

## 2 · 🔴 READ THIS BEFORE `status` MISLEADS YOU: **W18's PRINTED "next" IS OUT OF DATE**

`buildkit-marathon status` still prints W18 as *ESCALATED … engineer ruling required before any
merge*. **That escalation was discharged on 2026-09-05 and the step is re-scoped.** The status line
has not caught up; the durable traces have.

**What happened, and it is the session's main lesson:** the engineer ruled *cherry-pick 059's 41
additive files, then review the 44 collisions*. **I executed the ruling and building it refuted the
premise — including my own rev13 §4 analysis, which was the premise.**

```
baseline develop      gleam 1.17.0 build CLEAN, 625 tests pass, 0 failures
+ all 41 "additive"   170 compile errors across 19 of the 41
- those 19 withdrawn  92 errors — they are ONE interdependent component, not 41 files
```

**Root cause: `link_runtime` is TWO DIFFERENT MODULES sharing one path.**

| | develop | 059 |
|---|---|---|
| exports | `LinkMsg`, `Cursors`, `LinkState`, `Role`, `spawn_establish` (actor-style) | `LinkRuntime`, `new`, `with_transports`, `park_pending`, `next_epoch`, `check_fence` (data-structure style) |

develop also has **no** `link_wire`, **no** `quiescence`, **no** `link_driver`. The 104
byte-identical files are real, but they are the **periphery**; the core genuinely is two designs.
**N12 ("independent colliding implementations") was closer to right than rev13 credited — that
correction is mine to own.**

**Ruled outcome (Q-GLPNETA22-03-REVISED):** roadmap feature
**`port-059-gleam-link-layer-tests`** — WSJF 1.625 · RICE 3500 · **promoted**. The branch was
deleted, develop restored, `gleam build` re-verified at 0 errors. **Do not re-attempt the
cherry-pick; it has been measured.**

---

## 3 · WHAT SHIPPED THIS SESSION

| # | outcome | evidence |
|---|---|---|
| 1 | **Q-GLPNETA21-01 FIXED** — persisted per-host QUIC federation identity | `csharp/glp_link/transports/FederationIdentity.cs`, 21 tests |
| 2 | **Verified by the measurement that found the defect** | 5 probe **processes** → ONE pin (was 5 pins) |
| 3 | **Two codex adversarial cycles**, all findings fixed or published as PARTIAL | cycle 1: 6 findings incl. a CRITICAL remint race; cycle 2: 2 NEW defects its own fix introduced |
| 4 | **`node_id` + `spki` published beside the pin** — adopting @gavriella-glpnet's finding | `FromBase64(pin) == FromHex(node_id)`, `SHA256(spki) == pin` |
| 5 | **Converged with @gavriella-glpnet's independent implementation of the same fix** | one body in `glp_link`, their `LoadOrCreateDevCert` signature kept verbatim; §5 |
| 6 | **Pushed** — `origin/develop` == local, after merging 17 peer commits | `cd6016e4` |
| 7 | 10 `archive/*-20260905` tags **verified on origin** before any deletion | §6 |
| 8 | Roadmap sync round 72 · BK-STD-1 table (36 not-closed) · 6 BK-STD-2 questions, all answered | `docs/fleet/` |

Suites: **glp_link 219/219 · glp_crdtmsg 194/194 · gleam 625/625.**

---

## 4 · 🔴 THREE THINGS NEED YOUR HANDS — THEY ARE BLOCKED ON PRIVILEGE, NOT ON WORK

**(a) The firewall rule.** UDP 47890 was ratified (`Q-GLPNETA22-04`). `New-NetFirewallRule` returned
**Access is denied** — it needs an elevated shell. Run once, elevated:

```
New-NetFirewallRule -DisplayName "GLPNET YNET federation QUIC (UDP 47890)" -Direction Inbound -Protocol UDP -LocalPort 47890 -Profile Private -RemoteAddress 192.168.0.0/24 -Action Allow
```

**(b) The nine ref deletions.** Ruled mine to do (`Q-GLPNETA22-02`), archive tags **pushed and
verified on origin**, but `git push origin --delete` is refused by this session's command
classifier. Everything below is measured contained in develop and tagged:

```
git push origin --delete 065-ynet-consolidation 066-wave6-consolidation 067-qr-link-provisioning 067b-qr-link-continuation 078-verification-receipts 080-occurs-checked-substitution 082-feature-stream-superset 099-session14-postreboot-sweep 101-gleam-capability-delivery 103-stable-federation-identity
```

**Do NOT delete** `059`, `083-glptutorial-corpus-goldens`, `101-goal-term-acceptance`,
`102-quic-federation-transport` — all four measured NOT contained.

**(c) A tag divergence to look at, not to force.** `git push origin --tags` was rejected on
`v2026.06.10.1` — *already exists* with a different object locally and remotely. **Nothing was
forced.** A version tag meaning two different commits on two machines is worth a look before the
next release.

---

## 5 · TWO LANES BUILT THE SAME FIX IN THE SAME HOURS — AND THAT IS THE INTERESTING PART

@gavriella-glpnet, acting on my 17:45Z broadcast, independently implemented persisted federation
identity in era 102 while I implemented it here. Both landed on: per-host keystore, `CreateNew`
claim, loser-loads-winner, long lifetime. **Independent convergence is strong evidence the design is
right — and a duplicate implementation is exactly the fork the fleet keeps complaining about.**

**Resolved by converging the BODY and keeping BOTH signatures:**
`QuicLinkTransport.LoadOrCreateDevCert(name, out origin, path)` is kept **verbatim** — their tests
and callers are untouched — and now delegates to the shared `FederationIdentity` in `glp_link`.

🔴 **ONE BEHAVIOURAL CHANGE, DECLARED IN THE CODE AND HERE RATHER THAN SLIPPED IN.** Their version
re-minted a keypair when the anchor was within a day of expiry (`origin = "recreated-expired"`). The
converged version **refuses** instead, with an instruction, because re-minting *is* a rotation and a
rotation nobody asked for is this feature's own failure mode arriving on a timer. `"recreated-expired"`
no longer occurs. **@gavriella-glpnet has been asked to say if they want it back.**

---

## 6 · GIT REALITY AT SESSION END

origin heads (re-measured this session, not carried forward):

| status | refs |
|---|---|
| **CONTAINED** in develop (tagged, deletable — §4b) | 065 · 066 · 067 · 067b · 078 · 080 · 082 · 099 · 101-gleam-capability-delivery · 103-stable-federation-identity |
| **NOT contained — do NOT delete** | `059` (32 ahead) · `083-glptutorial-corpus-goldens` (3) · `101-goal-term-acceptance` (9) · `102-quic-federation-transport` (33) |

---

## 7 · WHAT'S NEXT, IN ORDER

1. **§4 (a)(b)** — two commands, both need your privileges. (b) unblocks **W19 → W20 → W21 → W23**,
   whose measurement is already complete.
2. **`port-059-gleam-link-layer-tests`** — promoted, unstarted. This is W18's replacement.
3. **The rekey (`Q-GLPNETA22-06`, ruled `rekey-then-elect`)** — peers keyed by `nodeId`, roster
   deduplicated by *resolved target*, quorum bar stated with its `n` and `f`. `node_id`/`spki` are
   published (§3.4); **the roster dedupe and the bar are not built yet.** No election until they are.
4. **Three peer-census defects, all one root cause** — `H:` and `I:` are the same UNC on this host;
   round-72's barrier said **5/4 hosts** with `gavriella`/`gavriellas` counted twice; gavriella's own
   `I:` is a self-loopback. **Do not fix one and assume the others.**
5. **`Q-GLPNETA19-01` / J2** — the §1.14 occurs-check *semantic* question is **still open and still
   reserved to Udi.** It blocks nothing, and I have **not** claimed it answered.

---

## 8 · REBOOT

`BK-OnRestart` fires **mstack's** launcher, not glpnet's copy:

```
pwsh -File "D:\BSTDEV\tools\mstack\scripts\fleet\post-reboot-restart.ps1" -WaitForMounts -Layout Tabs
```

Verify a fix **only** with `-DryRun -WaitForMounts -AllowUnconfirmedResume` — a plain `-DryRun` omits
`-WaitForMounts` and never exercises the path that failed on 2026-08-28 (`LastTaskResult=6`, zero
lanes launched). **The argument set is part of the failing condition.**

Lanes: `ospark · tefl · ulpanit(hatzinor) · olamnit · buildkit · qhstate · crucible · glpnet ·
lejepa · mstack · yngraw · yngwin · ynglin · yngapp · yngcor`.
⚠ **Never register a yngenios lane without `-Name`** — the leaf default collides and silently drops a lane.

**Nothing on this lane is mid-write.** Working tree clean, develop pushed, no partial merge, no
background job. The keystore lives at `%LOCALAPPDATA%\glpnet\federation` — **outside the repo, so a
clone or clean cannot destroy this host's pin.**

---

**rev14 · `ariellas.glpnet` · 2026-09-05T07:00Z · resume with `resume marathon`**

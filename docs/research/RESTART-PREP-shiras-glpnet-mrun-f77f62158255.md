<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — shiras / glpnet · run `mrun-f77f62158255`

    written:  2026-09-04T15:40Z   (REWRITTEN WHOLE — supersedes the 05:50Z revision)
    host:     SHIRAS (Linux, Ubuntu 26.04.1)   repo: olamni-glp/GLPNET
    branch:   develop  (clean, pushed, in sync with origin)
    run:      mrun-f77f62158255 [open]   era S1 CLOSED 9/9
    resume:   type exactly  →  resume marathon
    status:   ✅ SAFE TO RESTART.   ✅ SAFE TO REBOOT (see §6 — verified this session, 21/21).

> **POINTER, not a ledger.** The roadmap + buildkit pipeline state are the source of truth.
> Re-locate objectively. **Never resume from a summary.**
>
> 🔴 **DO NOT TRUST A COMMIT HASH WRITTEN IN THIS FILE.** Read the tip with `git log --oneline -1`.

---

## 1 · First three commands on resume

```bash
bk-heavy-lock --timeout 3600 -- buildkit-marathon status --feature glpnet-shiras-tidyup-and-scheduler-rootcause
bk-heavy-lock --timeout 3600 -- buildkit-marathon backlog --feature glpnet-shiras-tidyup-and-scheduler-rootcause
bk-heavy-lock --timeout 3600 -- /home/shira/.local/share/bkvenv/bin/python \
    .specify/standards/bk_report_v1.py all --feature glpnet-shiras-tidyup-and-scheduler-rootcause
```

🔴 Five rules, each learned by breaking it:
1. **Wrap every heavy buildkit call in `bk-heavy-lock`.** Waits measured this session: 212s, **899s**.
   Four other lanes contend for one registry. It queues; it is not stuck. **Never kill a holder.**
2. **BK-REPORT needs the bkvenv python, NOT `python3`.**
3. **Report order is FIXED:** ROADMAP → PROGRESS → STATUS → SITREP → TAKT → NEXT.
4. **`step-start` / `checkpoint` take the `mstep-…` ID, NOT the stage name.** IDs are in the run
   mirror at `~/.local/share/buildkit/deploy-home/targets/b0ada634764e/marathon-mrun-f77f62158255.md`.
5. **`marathon capture --kind` takes `bug|idea|issue|latent-requirement|missing-prerequisite`.**
   `finding` is rejected — a 900s lock wait is wasted on the error.

## 2 · WHERE THE ERA STANDS — **S1 CLOSED 9/9, fully measured. 54 backlog items outstanding.**

```
takt: 9/9 steps measurable    ERA TOTAL ELAPSED 36.70h (band 1.5-6.0h -> over)
specify 0.03h · clarify 25.60h · plan 8.85h · tasks 0.20h · analyze 0.03h
implement 0.02h · codexreview 0.85h · ship 0.97h · close 0.15h
backlog: 19/73 done · 52 parked · 2 sequenced      roadmap: 130 features, 35 open, 95 closed
```

## 3 · 🔴 DECIDED 2026-09-04T15:30Z — cite, never re-ask (`Q-glpnetshiras-39..42`)

`.specify/questions/Q-glpnetshiras-20260904T1530Z.json` — **BK-STD-2 conformant, 4/4 decided.**

| qid | ruling |
|---|---|
| **Q-39** | **NEXT ERA = `ynet-minted-lane-identity-resolve-address-independent`** (WSJF 5.20 / RICE 810). `Q-32`'s P3 era is **re-ordered, not withdrawn** — it follows when `@olamnit` publishes the manifest scope |
| **Q-40** | **ONE window / 15 tabs re-applied** via `bk-onrestart set-layout 1`; selftest 21/21. Filed to `@shiras-buildkit` to make `layout=1` the SHIRAS per-host default |
| **Q-41** | 🔴 **ONE-WAY — PBFT governs ALL purposes on SHIRAS too.** The `shiras.buildkit` authorisation-only ruling is SUPERSEDED; the zero-round-trip CRDT fold is no longer the ordering plane; **ERA 102's listener is now the prerequisite of ordering anything.** I recommended against and was overruled |
| **Q-42** | **Coordinator DESIGNATED, not elected** = `shiras-glpnet`, until the PBFT elector has an endpoint. Authority: era allocation + ACK barriers only. **Writes NO board ops.** Ends the moment the elector answers |

Carried and still valid: Q-09 · Q-11..Q-18 · Q-20 ✅ · Q-22 ✅ · Q-23 · Q-25..Q-30 · **Q-31..Q-38**.
🔴 **Ruling `-03` (`term := (space_id, era_counter, host_id)`) is UNAFFECTED by Q-41.** Do not fold
any board across hosts until your emitters are keyed to the triple. Do not delete op `628016928ab854ae`.

## 4 · WHAT THIS SESSION DELIVERED (all published, all peer-reachable)

- **Codex gate on `@shiras-qhstate`'s QUIC provider chain** (`0a35a4d1`, 1,200 lines): run
  `20260904T144004Z`, **6 findings**. **5 fixed** in `10117503`: registration retryable after a
  failed msquic load; `YNET_MSQUIC_PATH` naming a file honoured exactly; `--stage` RID derived from
  `uname -m` (ARM64 was invisible); `--check` mirrors `$LIBDIR` → staged RID dir → system loader;
  chain-link test's blocking `ReadFrame()` bounded by the 30s token. **133/133 green under
  `env -u LD_LIBRARY_PATH`.**
- **P1 deliberately NOT fixed:** the chain is **not wired into `YnetTransportCapability.Connect`**
  (only `INodeEndpointResolver` is `InProcessFabric`). That is **ERA 102's** scope per
  `Q-shiras0904e-02`, and under Q-41 it is the fleet's ordering prerequisite. **Do not re-derive
  this as a defect — it is a recorded scope boundary.**
- **Released `v2026.09.04.4`** (PR #289 merged, back-merge #290 merged).
- **iroh tier-0 feature** captured, scored (WSJF 1.85 / RICE 138, **confidence 40** — no .NET
  binding exists, nothing to measure), **promoted**, with parity-before-retirement in its acceptance.
- **Roadmap sync rounds 69 + 70**: 86 lines from 24 peer files imported, 0 refused, dedupe clean,
  exported + mirrored to `/mnt/gavri/d/coop`, barrier 5/4 hosts.
- **Two coop documents published to BOTH roots** (15:20Z ACK sweep, 15:30Z rulings), mirrored into
  `docs/fleet/`.

## 4B · 🔴 LATE SESSION — THE FEDERATION PIN FIX, AND A PROBE THAT WAS LYING

`@ariellas-glpnet` broadcast at 17:45Z that `CreateDevCert` mints a fresh keypair per call (five
runs, five pins), so any pin table exchanged before the reboot dies at the reboot. **Fixed here, in
this lane's own file, additively** (`c2303104`):

- `LoadOrCreateDevCert` beside the untouched `CreateDevCert`: PKCS#12 in
  `<LocalAppData>/glpnet/federation` (or `$GLPNET_FEDERATION_KEYSTORE`), 0600, minted on first run
  only, 5-year validity. Race loser **loads the winner's file** (last-writer-wins would fork one
  host into two identities). Expiry is **reported** (`recreated-expired`), never silent.
- 4 regression tests asserting the **property** (same pin across loads), plus a positive control
  that `CreateDevCert` is still ephemeral.

🔴 **AND THE BIGGER ONE:** `glp_quic_probe` referenced `glp_crdtmsg`, which does **not** reference
`ynet_transport` — where the MsQuic resolver landed. **So the probe reported `IsSupported=False` on
SHIRAS while this host binds a real link.** Publishing that would have put SHIRAS on record as
"no QUIC". Fixed: the probe now references `YnetTransport` and touches
`MsQuicProvider.Instance.Probe()` **first** (ordering is load-bearing and fails silently).

**MEASURED under `env -u LD_LIBRARY_PATH`:** msquic resolved, all three predicates True,
**LISTENER BOUND on `0.0.0.0:47890`**, pin `0yQIsASyLWKuzMXxvMF4B1WBw5h1QrWr+zoTx8kLVGo=` identical
across two separate processes. **SHIRAS is the third host to bind and the first with a stable pin.**
Suites: `glp_crdtmsg` 194/194, `ynet_transport` 133/133.

⚠️ **Any Linux host that measured `False` before `c2303104` has a VOID measurement** — it was the
probe, not the host. **The federation UDP port `47890` is still unratified** (measured free on two
hosts); no cross-host handshake has been performed.

## 5 · 🔴 FLEET STATE AS MEASURED HERE — the four things a successor must not re-derive wrongly

1. **QUIC on SHIRAS: the code was never the gap, and the gap is now closed.** `libmsquic 2.6.1` at
   `~/.local/lib` was never on the default loader path; the `[ModuleInitializer]` + `DllImportResolver`
   fixes it for **services**, which is what votes. `LD_LIBRARY_PATH` greens tests and leaves services
   deaf. "Ship the `.so` beside the binary" **does not work** — `System.Net.Quic` is in the shared
   framework and probes relative to its own assembly (`@shiras-yngapp` measured it; that remedy was
   ruled and is wrong).
2. **Elector set is `n=4, f=1` at ZERO margin.** Do not assert margin. A platform is not an
   independent fault. **The free adverse-state test is gone on this host** — it binds now; it needs a
   fixture (`YNET_MSQUIC_PATH=/nonexistent`).
3. **The designated PBFT elector (`yng-broker`/`yng-guardian`) exists only in
   `yngenios-windows/prototype` at net10, has ZERO election/quorum/signature code, is absent on
   Linux, and listens on NOTHING** on the two hosts where it runs. Measured by four lanes. **glpnet
   builds no election and votes in none.**
4. **`yx_ynet` oracle: FEDERATED 3 of 4** (ARIELLAS not admitted), **term 0, NO_LEADER, 0 board
   keys — and that is CORRECT.** SHIRAS is admitted as node `1994d86e…` by `@shiras-yngcor`; **a lane
   is not a voter**, so glpnet must NOT run `hello` (a second SHIRAS node is a phantom vote).
   Read-only status: `cd /mnt/biwin/D_DRIVE/YNGENIOS/yngenios && PYTHONPATH=src python3 -m yx_ynet.cli --lane shiras.glpnet status`
5. **`cargo`/`rustc` 1.98.1 are PRESENT on this host and NOT on PATH.** Present-but-unreachable reads
   as absent to everything non-interactive. If iroh is vendored as Rust, provisioning must put cargo
   on PATH **for services**, measured.

## 6 · REBOOT — **VERIFIED THIS SESSION, AND IT IS SAFE**

```
bk-onrestart set-layout 1        ->  "layout set to ONE window"   (layout=1, 2 groups, 15 lanes)
bk-onrestart selftest            ->  ALL 21 CHECKS PASSED
  incl. "G: --layout 1 folds every group into one window"
autostart: ~/.config/autostart/bk-onrestart.desktop  --delay 45 --wait-for-mounts
systemd  : bk-onrestart.service  ExecStartPre=/bin/sleep 45
binary   : ~/.local/share/buildkit/deploy-home/onrestart/bin/bk-onrestart
backup   : ~/.config/bk-onrestart/config.json.bak-20260904T1535Z-glpnet-Q40-preOneWindow
```

**ONE window, 15 tabs**, groups folded at launch: ospark · tefl · ulpanit · olamnit · buildkit ·
qhstate · crucible · **glpnet** · lejepa · mstack · yngraw · yngwin · ynglin · yngapp · yngcor.
Each resumes mid-thread with `claude --continue --autocompact 1000000` — **never summarising**.

⚠️ **The file has been rewritten by two lanes today (08:58Z mine, 10:55Z `@shiras-buildkit`'s).**
Before rebooting, re-check `layout` — if it is 2 again, `set-layout 1` and say so; do not silently
flip it a fourth time.

## 7 · WHAT'S NEXT — in this marathon, and beyond

**In the run** (`next:` still points at S3, which `Q-33` **parked** — do not re-derive it):
1. 🔴 **Open the ruled era: `/bk-specify "YNET minted lane identity: address-independent ids, Resolve
   maps id to address, Refused is a valid answer"`** (`Q-39`). Files this lane owns
   (`csharp/ynet_transport/Capability/NodeIdentity.cs`). It is the fleet's stated blocker —
   `R-E4` refuses all 93 ospark candidacies for want of `Resolve`.
2. **Collect 4 peer-lane approvals** for it (`Q-lejepa-30`: a lane is a registered guardian, quorum
   is an ABSOLUTE 4 others), preferring different hosts.
3. **Then the full pipeline in one era:** specify → clarify → plan → tasks → analyze → implement →
   codexreview → ship → close → era close + tidy.

**Beyond** — 35 features not closed. Derived build order:
`verification-receipts…` → `bk-onrestart-per-host…` → `glptutorial-corpus-goldens…` →
`occurs-checked-substitution…` → `madglp-writer-reader…` → **`ynet-minted-lane-identity…`** →
`renderers-read-export-fold…`. Tidy-up items T1–T9 are still parked (1 stray worktree gone, 2 merged
local branches, unmerged remote heads incl. `050-full-gleam-combined` ahead 48 and
`059-full-scope-gleam` ahead 32).

⚠️ **Open defects, unfixed:** `reconcile` reports **80/130 features carry no `spec_path`** and can
never bind by basename. `dedupe`/`export` report *"engine resolution degraded: pin mirror absent and
the machine registry is unreachable"* — the deploy pin here is `2026.8.24.5` with `integrity=False`
and `targets: []`.

---

*Written by shiras/glpnet for its own successor session. Resume with: `resume marathon`.*

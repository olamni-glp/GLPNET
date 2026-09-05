<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — shiras / glpnet · **era S2 delivered**

    written:  2026-09-05T01:10Z   (supersedes RESTART-PREP-…-mrun-f77f62158255.md, 15:40Z)
    host:     SHIRAS (Linux, Ubuntu 26.04.1)     repo: olamni-glp/GLPNET
    branch:   develop — clean, pushed, in sync
    resume:   type exactly  →  resume marathon
    status:   ✅ SAFE TO RESTART.   ✅ SAFE TO REBOOT (§5 — re-verified this session, 15/15 paths).

> **POINTER, not a ledger.** Roadmap + pipeline state are the source of truth. Re-locate
> objectively; never resume from a summary.
> 🔴 **Do not trust a commit hash written in this file.** Read the tip with `git log --oneline -1`.

---

## 1 · First commands on resume

```bash
bk-heavy-lock --timeout 3600 -- buildkit-marathon status --feature ynet-minted-lane-identity-resolve-address-independent
bk-heavy-lock --timeout 3600 -- buildkit-roadmap status
env -u LD_LIBRARY_PATH dotnet test csharp/ynet_transport.tests -c Release   # expect 182/182
```

Rules, each learned by breaking it — **all still true**:
1. Wrap every heavy buildkit call in `bk-heavy-lock`. Waits measured **this** session: 39s, 63s
   (better than the 899s of the previous one, but four lanes still contend for one registry).
   **It queues; it is not stuck. Never kill a holder.**
2. **Batch under ONE lock hold.** Six commands in one `bk-heavy-lock -- bash script.sh` cost one
   wait, not six. This is the single biggest tempo win available on this host.
3. `marathon capture --kind` takes `bug|idea|issue|latent-requirement|missing-prerequisite`.
4. `--feature`, never `--run`. BK-REPORT needs the bkvenv python, not `python3`.
5. `Ynet.Transport.Path` shadows `System.IO.Path` — alias `SysPath` in any new file under
   `csharp/ynet_transport*`. Costs one build cycle every time it is forgotten.

## 2 · WHERE THE ERA STANDS — **S2 DELIVERED, and it is the ruled one**

`Q-39` ruled the next era: **`ynet-minted-lane-identity-resolve-address-independent`**
(WSJF 5.20 / RICE 810). It is delivered in two halves, both green and pushed.

| | commit | what |
|---|---|---|
| half 1 | `b5a9911b` | `LoadOrMint` persistent node identity + `Resolve(id) -> address \| Refused` |
| half 2 | `f60acbbf` | `QuicNodeEndpointResolver` — `Connect` dials a **real QUIC wire by node id** |
| fix | (after codex self-review) | write-then-rename the key file |
| docs | `494580dc` + | spec / plan / tasks, and the 00:40Z broadcast on **both** coop roots |

**Suite: 182/182** under `env -u LD_LIBRARY_PATH` (baseline was 133). Roadmap feature **linked** to
`specs/102-ynet-minted-lane-identity`, export round `20260904T234454Z` (21 epics, 132 features).

### The two measurements that matter — reproduce them, do not take them on trust

```
# three separate OS processes, one id, 0600
dotnet run --project csharp/glp_quic_probe -c Release     (x3)
  -> 76b66c25565da0fbc8587a598a4aff58d08b86172e643ec25ee18470a051f51e
     origin: Minted, then Loaded, then Loaded

# two nodes, connected BY ID over a genuine QUIC handshake, sealed frame across
Two_nodes_connect_by_id_over_a_real_quic_wire_and_exchange_a_sealed_frame   PASSED [493 ms]
```

## 3 · 🔴 FIVE THINGS A SUCCESSOR MUST NOT RE-DERIVE WRONGLY

1. **`Connect` is no longer InProcessFabric-only.** `QuicNodeEndpointResolver` swaps in behind
   `INodeEndpointResolver` exactly as that interface's own comment predicted. The recorded scope
   boundary `Q-shiras0904e-02` ("the chain is not wired into Connect") **is discharged for the
   endpoint-resolver path.** What is still NOT wired is `@shiras-qhstate`'s multi-tier
   `QuicProviderChain`; `QuicWireChannel` (System.Net.Quic + the msquic resolver) is what dials.
2. **NOTHING HAS CROSSED A WIRE BETWEEN TWO HOSTS.** The end-to-end test is loopback. UDP `47890`
   is unratified. Four hosts each binding a listener is not a link between any two of them. **Do not
   report federation.**
3. **glpnet builds no election and votes in none** (`R-1`, `Q-42`). It holds **zero board ops** and
   emits none; op `628016928ab854ae` is preserved. SHIRAS's roster pin belongs to
   **`shiras.yngraw`** — this lane must not run `yx_ynet hello` (a second SHIRAS node is a phantom
   vote) and cannot answer the `yx-ynet id` compliance item.
4. **The feature-020 "zero consumers" claim is FALSE and re-broadcasting it is refused here.** Seven
   lanes measured against it; `@olamnit-yngapp` **retracted its own claim** at 23:55Z (stale tree);
   `@gavriella-buildkit` has a standing stop-order. See the 00:40Z broadcast §5. The durable fix is
   already a promoted roadmap feature: `l0-consumer-resolution-no-false-absence-from-a-projection-scoped-search`
   (WSJF 5.20 / RICE 810).
5. **The QUIC listener certificate is ephemeral ON PURPOSE.** TLS here is transport confidentiality
   only; identity is verified app-layer against `nodeId = H(pubkey)`. Do not "fix" it by pinning —
   that moves the identity decision to the wrong layer. The key that must persist is the NODE key,
   and now does.

## 4 · OPEN DEFECTS — measured, unfixed, not mine alone

| defect | measured | owner |
|---|---|---|
| **81 of 132 roadmap features carry no `spec_path`** and can never bind by basename — **it grew** (was 80/130 this morning) | `roadmap reconcile`, 23:44Z | buildkit tooling |
| `buildkit-roadmap next` → `psycopg.OperationalError: the connection is lost`; **co capture spilled**, engine resolution degraded (pin mirror absent, machine registry unreachable, deploy pin `2026.8.24.5` `integrity=False`, `targets: []`) | this session | buildkit deploy |
| registry contention: a peer's `buildkit-marathon` held `pgdb/.lock` for 61 consecutive attempts | 23:37Z | fleet-wide, by design |
| **UDP 47890 unratified**; no cross-host handshake ever performed | standing | ynet / fleet |

## 5 · REBOOT — **RE-VERIFIED THIS SESSION BY MEASUREMENT, NOT BY SELFTEST**

```
layout   = 1  (ONE window)          delay_seconds = 45
claude_args = --continue --autocompact 1000000        <- resumes mid-thread, NEVER summarises
wait_for_mounts = /mnt/biwin/D_DRIVE
windows  = core-lanes(7) + yngenios-lanes(8)          <- 15 lanes, folded into one window
autostart: ~/.config/autostart/bk-onrestart.desktop   systemd --user: ENABLED
```
**All 15 lane paths checked this session: 15/15 exist AND are git repos.** ospark · ulpanit · tefl ·
buildkit · crucible · olamnit · qhstate · **glpnet** · lejepa · yngraw · mstack · yngcor · yngapp ·
ynglin · yngwin.

⚠️ **`layout` has been rewritten by two lanes before.** It reads **1** now. Re-check before rebooting;
if it is 2, `set-layout 1` and SAY SO — do not silently flip it a fifth time. The binary is at
`~/.local/share/buildkit/deploy-home/onrestart/bin/bk-onrestart` and is **not on `PATH`**.

⚠️ **`/mnt/gavri/d` (the shared coop root) is NOT in `wait_for_mounts`** — but it is an
`x-systemd.automount` cifs mount with `nofail`, so it mounts on first access rather than at boot.
**Residual hazard:** if host GAVRI is unreachable at the moment a lane publishes, the publish can
land local-only — the exact cause of "14 of 15 boards stale". The detector is one command:
```bash
sha256sum /mnt/gavri/d/coop/<doc> /mnt/biwin/D_DRIVE/coop/<doc>   # two roots, one hash, or it did not ship
```

### 🔴 Swap: re-measure before concluding
19:05Z measured swap **100% consumed** (4091/4095 MB) with 15 lanes resident on 12.3 GB.
**A reboot clears it; the `bk-onrestart` relaunch of 15 lanes refills it.** If a lane dies silently
after the reboot, **check `dmesg -T | grep -i oom` before filing a defect against the relauncher.**

## 6 · WHAT'S NEXT

**Immediately after resume:**
1. Take the codex review findings on feature 102 through to green, then
   `buildkit-roadmap advance ynet-minted-lane-identity-… --to reviewed`, then ship + release.
2. **Answer the one question that decides the next build:** static pin table vs. self-certified DHT
   record as the oracle's address binding (asked of `@olamnit-yngcor` / `@shiras-qhstate` in the
   00:40Z broadcast §8.3). **Both are implemented; only the binding choice is open.**
3. The cross-host handshake on UDP 47890 — the first thing 102 makes attemptable. Needs a peer lane
   on a second host to bind and exchange one frame. **This is the real federation milestone.**

**Beyond**, from `roadmap status` (35 not closed). Derived order, unchanged:
`verification-receipts…` → `bk-onrestart-per-host…` → `glptutorial-corpus-goldens…` →
`occurs-checked-substitution…` → `madglp-writer-reader…` → `l0-consumer-resolution…` →
**`ynet-minted-lane-identity…` ✅ DONE** → `renderers-read-export-fold…` →
`measured-not-declared-environment-predicates…`. In this lane specifically:
`glp-repl-fmb-split-over-ynet-for-qhsm-terminal` (promoted, WSJF 2.88) is the QHSM-virtual-terminal
work, and `iroh-tier0-quic-provider…` (promoted, WSJF 1.85, **confidence 40 — no .NET binding
exists**) is the iroh route. Both are already on the roadmap: **do not mint them again.**

---

*Written by shiras/glpnet for its own successor. Resume with: `resume marathon`.*

---

## 7 · ADDENDUM 01:25Z — session end state

- **Tree clean, pushed.** `develop` merged a peer push cleanly (no conflicts): `@olamnit-glpnet` /
  `@ariellas-glpnet` landed `specs/101-goal-term-acceptance`, `specs/103-stable-federation-identity`
  and a restart rev.
- 🔴 **`specs/103` is NOT a duplicate of 102 — check before you "consolidate" them.** 103 is the
  **federation TLS certificate** keypair (`LoadOrCreateDevCert`, my `c2303104`); 102 is the **YNET
  node identity** keypair (`LoadOrMint`). Different objects, **same defect class** — they are sites 1
  and 3 in the 00:40Z broadcast's table. Merging them would lose one of the two fixes.
- **Marathon era S2 open:** `mrun-e76f86453d93`, feature
  `ynet-minted-lane-identity-resolve-address-independent`, seq 4, **4 items captured** (the
  three-site defect class, the growing `spec_path` gap, the degraded co backend, the unratified
  UDP 47890).
- **Roadmap sync round 72:** 14 lines from 4 peer files imported, nothing refused/deferred/invalid,
  barrier **5/4 hosts**, exports committed and peer-reachable.
- **Takt, measured:** ALL ERAS mean **17.24 h**, p50 **20.29 h** over 7 eras — and **105 eras are
  unmeasurable**, listed rather than counted as zero. Era S2 ran ≈3 h, well inside the band, because
  its scope was one ruled feature in files this lane owns.
- ⚠️ **The codex review of feature 102 did NOT complete** — killed at the 1500 s timeout under host
  load (exit 143), zero output. **Feature 102 is therefore UNREVIEWED by codex.** The
  self-review it did get found and fixed one real P1 (write-then-rename). **Re-run it first thing
  after the reboot, when the host is quiet:** it is the one pipeline stage this era is missing.
- **Four BK-STD-2 questions are open and conformant** (`Q-43`..`Q-46`, validator says 4/4):
  the scope collision I disclosed, the directive-vs-allocation contradiction, the refused
  feature-020 rebroadcast, and the era quota whose penalty clause taxes disclosure.

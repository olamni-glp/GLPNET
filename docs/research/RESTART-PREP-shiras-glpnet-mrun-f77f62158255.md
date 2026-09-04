<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — shiras / glpnet · run `mrun-f77f62158255`

    written:  2026-09-04T05:50Z   (REWRITTEN WHOLE — supersedes the 2026-09-03T16:10Z revision,
                                   which codex found internally inconsistent in three places)
    host:     SHIRAS (Linux)   repo: olamni-glp/GLPNET
    branch:   100-cpm-central-package-management
    run:      mrun-f77f62158255 [open]   era S1 CLOSED 9/9
    resume:   type exactly  →  resume marathon
    status:   ✅ SAFE TO RESTART.   ⚠️ REBOOT: SAFE ONLY IF YOU LOG BACK IN — and read §6 FIRST,
                                     one lane is ALREADY DOWN before any reboot.

> **POINTER, not a ledger.** The roadmap + buildkit pipeline state are the source of truth.
> Re-locate objectively. **Never resume from a summary.**
>
> 🔴 **DO NOT TRUST A COMMIT HASH WRITTEN IN THIS FILE.** Its header named a stale commit once
> already. Read the tip with `git log --oneline -1`; that is the only in-sync claim allowed here.

---

## 1 · First three commands on resume

```bash
bk-heavy-lock --timeout 3600 -- buildkit-marathon status --feature glpnet-shiras-tidyup-and-scheduler-rootcause
bk-heavy-lock --timeout 3600 -- buildkit-marathon backlog --feature glpnet-shiras-tidyup-and-scheduler-rootcause
bk-heavy-lock --timeout 3600 -- /home/shira/.local/share/bkvenv/bin/python \
    .specify/standards/bk_report_v1.py all --feature glpnet-shiras-tidyup-and-scheduler-rootcause
```

🔴 Four rules, each learned by breaking it:
1. **Wrap every heavy buildkit call in `bk-heavy-lock`.** Waits measured this session: 5s, 26s,
   40s, 51s, 59s, 65s, 353s, **471s**. Four other lanes contend for one registry. It queues; it is
   not stuck. Never kill a holder.
2. **BK-REPORT needs the bkvenv python, NOT `python3`.**
3. **Report order is FIXED:** ROADMAP → PROGRESS → STATUS → SITREP → TAKT → NEXT.
4. **`step-start` / `checkpoint` take the `mstep-…` ID, NOT the stage name.** `--step clarify`
   fails with `no step 'clarify' in run`. Get IDs from the run mirror at
   `~/.local/share/buildkit/deploy-home/targets/b0ada634764e/marathon-mrun-f77f62158255.md`.

## 2 · WHERE THE ERA STANDS — **S1 IS CLOSED. 9/9. FULLY MEASURED.**

```
takt: 9/9 steps measurable (9 declared phase, 0 derived)
specify 0.03h · clarify 25.60h · plan 8.85h · tasks 0.20h · analyze 0.03h
implement 0.02h · codexreview 0.85h · ship 0.97h · close 0.15h
ERA ELAPSED 35.69h (band 1.5-6.0h -> over)
```

**This is the first fully-measured era for glpnet on any host** (the fleet report had glpnet at 0%).
⚠ **Read the two big numbers honestly:** `clarify` 25.60h and `plan` 8.85h are **overnight
wall-clock**, not effort — a checkpoint stamps the next step's start, so an idle night lands inside
the next phase. The `over` verdict on the era is an artefact of that, **not** slow work.

**Both root causes were PROVED and must NOT be re-derived:**
- **Q-19** — era stages were never **MINTED**, not lost. `expand --item --steps` is the only
  minting path. Remedy already applied here.
- **S1** — transition writers omit `phase`; `board_phase_seconds` (`marathon/takt.py:747`) skips a
  phase-less op **by design**, and its docstring says so. **The reader was never the defect.**

**CORRECTED DURING THE ERA — the finding grew:** S1 said **three** phase-omitting writers. **There
are FIVE.** `flow/__main__.py:1109` (`→done`) and `:1446` (generic verb) were missed. Patching only
three leaves the interval uncloseable.

## 3 · ✅ THE MERGE BLOCKER IS **CLEARED**. PR MERGED, RELEASE CUT.

The permission was granted (`Q-37`) and everything behind it executed:

| | |
|---|---|
| **PR #279** | ✅ **MERGED** `2026-09-04T09:00:39Z` |
| **yx-bootmig `[SUPERSEDED]` correction** | ✅ **ON `develop`** — `grep -c SUPERSEDED` on `origin/develop` → **1** (was **0** for three days) |
| **Release** | ✅ **`v2026.09.04.2`** cut, PR **#285** **MERGED** `09:05:34Z` |
| **Branch `100-cpm-central-package-management`** | fully merged, **0 ahead of develop** |

**25 commits were enumerated before the cut, not inferred** (codex P1-3 required this): all of them
this lane's own docs, rulings, roadmap rounds and codex remedies — no unrelated code.

⚠ **Local checkout is now on `develop`.** The old feature branch is merged and finished; a
successor should branch fresh from `develop` rather than reuse it.

## 4 · DECIDED THIS SESSION — cite, never re-ask

`.specify/questions/Q-glpnetshiras-20260904T0500Z.json` — **BK-STD-2 conformant, 4/4 decided.**

| qid | ruling |
|---|---|
| **Q-31** | **Test the merge gate.** Tested: pull/push work, `gh pr merge` refused → escalated (§3) |
| **Q-32** | **NEXT ERA = a P3-completion era on SHIRAS** to unblock yx-bootmig P4; agree manifest scope with `@olamnit` by coop **before** opening it (removes the Q-MARATHON-02 duplication risk) |
| **Q-33** | **S3 PARKED** pending `@buildkit`'s ACK of the filing — **not** discharged, because the code is still unfixed fleet-wide |
| **Q-34** | **The S6 release hold is SUPERSEDED** by the engineer's newer instruction; S6 discharged. Execution blocked by §3 |

Carried and still valid: Q-09 · Q-11..Q-18 · Q-20 ✅ · Q-22 ✅ · Q-23 · Q-25 · Q-26 · Q-27 · Q-28 ·
Q-29 ✅ **EXECUTED** · Q-30.

## 5 · WHAT THIS ERA ACTUALLY DELIVERED (all published, all peer-reachable)

- `coop/FILING-20260903T1954Z-shiras-buildkit-…` — five phase-omitting sites, commit-pinned lines,
  the two-sink near-miss, three asks with one deliberately left open for the owning lane.
- `coop/ACK-SWEEP-20260904T0445Z-shiras-glpnet-…` — **first sweep in nine days**, 20 documents,
  `@buildkit`'s `line-57` question answered by measurement (**glpnet's copy is TRACKED**, so their
  published "zero repo-fixable rows" is **one**).
- **Two codex passes, 13 findings, all remediated** (`b9929b23`, `db4ce9a1`). Pass 2 independently
  corroborated the false-filing record found in `clarify`.
- **Roadmap round 66**: reconcile/import/reconcile/dedupe/export/sync all `rc=0` — 21 epics /
  122 features / 4030 journal lines, **0 refused, no OOM**.

**🔴 THE CORRECTION THIS ERA EXISTS TO CARRY:** ruling Q-29 and the previous revision of THIS FILE
both recorded the S1 fix as *"filed to @buildkit"*. **It never was.** No coop document mentioned
`readiness.py` or `board_phase_seconds` until 2026-09-03T19:54Z. **Finding a fix is not filing it,
and a decision record asserting an artefact is not evidence the artefact exists.**

## 6 · 🔴 REBOOT — RE-MEASURED, AND ONE LANE IS ALREADY DOWN

```
live claude sessions: 14   (pgrep -u $(id -u) -x claude | wc -l)
declared lanes:       15   (~/.config/bk-onrestart/config.json, schema 2, one-window)
MISSING:              mstack   (/mnt/biwin/D_DRIVE/BSTDEV/tools/MSTACK — repo present, no session)
```

⚠ **`mstack` died BEFORE any reboot.** Any post-reboot "15/15" check is therefore measuring a
recovery, not a steady state — and if you verify against a remembered 15 you will read a reboot
that *fixed* mstack as a reboot that changed nothing.

**Both boot paths have moved since the last revision — re-measure, do not inherit:**

| path | state now |
|---|---|
| `bk-onrestart.service` (systemd user) | `enabled`, `active (exited)` since **05:29 today**, with new drop-ins `10-path.conf` / `20-install.conf` / `30-harden.conf` — the PATH hazard recorded earlier **may now be fixed**, but that is **UNVERIFIED at boot** |
| `Linger` | `yes` |
| autostart `.desktop` | present, rewritten **05:16 today** |
| launcher `bk-onrestart.sh` | rewritten **04:49 today** (35KB, was 19KB) |

🔴 **A DRY RUN IS NOT BOOT VALIDATION** (codex P2-5). With all lanes up the launcher takes its
`nothing to do` branch immediately: it exercises **no** terminal startup, **no** `claude` lookup on
the boot PATH, **no** launch behaviour. Its `EXIT 0` is a **FALSE GREEN**.

**The only real evidence remains the 2026-09-02T17:17 boot**, where the systemd unit fired 12s after
boot with no graphical session and **FAILED** (`0/15`, `status=1/FAILURE`), and the desktop autostart
brought **15/15** back 16 minutes later — **via LOGIN**.

### TOPOLOGY — **ONE window, 15 tabs** (settled by ruling `Q-35`)

Two successive directives disagreed: an earlier one split the fleet 7 + 8 across two windows, the
later one listed all 15 in a single window. I applied the split, **asked**, and the engineer ruled
**revert to one window** — the latest instruction wins. **Applied and verified 2026-09-04T08:58Z**
(`~/.config/bk-onrestart/config.json`; backup `.bak-20260904-glpnet-preTwoWindow` retained):

```
layout=1 window(s)   terminal=xfce4-terminal
WINDOW 1 (15): ospark · ulpanit(lang/hatzinor) · tefl · buildkit · crucible · glpnet · lejepa ·
               olamnit · qhstate · yngraw(research/yngenios) · mstack · yngcor · yngapp ·
               ynglin · yngwin
TOTAL 15 — none lost in either direction of the change.
```
⚠ **This file is HOST-level and every lane on SHIRAS depends on it.** If you see it disagree with a
directive, **ask before flipping it** — two lanes alternating on a shared config during a reboot is
the one race the fleet cannot afford.

**Verified by `launch --dry-run`, and this run proved more than a no-op** — because `mstack` is
down, the launcher actually planned a real tab: `window1: 0 tab(s) window2: 1 tab(s)`, emitting the
exact command (`claude --continue --autocompact 1000000`, **never summarising**, with both lake
roots exported). It still does **not** exercise terminal startup or the boot PATH.

`bk-onrestart.sh preflight` → **`SAFE TO REBOOT`, exit 0**, with per-repo warnings.
⚠ Noted from preflight: **`yngenios-linux` has `unpushed=2`** — another lane's, not mine to push.

### Reboot verdict

✅ **SAFE TO REBOOT — provided you LOG BACK IN.** A guard prevents double-launch.
❌ **If the host reboots to a login screen and nobody logs in, NOTHING resumes.**
⚠ Today's systemd/launcher rewrites **and this two-window change** are **untested at boot** — this
reboot is their first real test. **`mstack` is down NOW**, so a post-reboot 15/15 is a *recovery*.

## 6B · 🔴 ENGINEER DIRECTIVES RECEIVED DIRECTLY 2026-09-04 — and what is NOT this lane's

Received **from the engineer, not relayed** (earlier relayed versions were correctly declined):
one realtime golden-truth **oracle board service** on all 4 hosts / 15 lanes with **CRDT** durable
artifact; **leader-lane election** (PAXOS/RAFT/ZAB/PBFT) wired to the Oracle + `/bk-beacon`;
**QHSM/QMSM-wrapped headless virtual terminals** onto the YNGENIOS app over ynet mailboxes;
**`/yx-proxy`**; **`/bk-beacon` refactor**; **3270 facility** serving the GLP REPL;
**`/bk-onrestart` C# reimplementation** fleet-wide in two eras; **all cross-platform code as L0 in
`yngenios`**; and **per-lane exclusive feature eras** approved by ≥4 other lanes.

**ENGINEER RULING `20260904T0810Z` (four decisions):** **D1** deploy+register the writer-bearing
version on all four hosts, then converge pins, then assert `lclock>0`. **D2** ONE fleet board, repo
as **partition key**, replicated per host — **board identity is a hard prerequisite** (buildkit
measured the same nominal board as a **three-way fork**, 28/26/37 op-log files, `root_id` **not
pinned**). **D3** `#1011` closed — one L0 supervision capability, N consumers. **D4** 🔴 **a new era
opens only after the current one closes — `Q-glpnetshiras-32` CONTINUES and is not preempted.**

**MINE:** the **ynet transport** (`specs/051`, `specs/065`, `csharp/ynet_transport.tests/` — 50
tracked; `yngenios` vendors those tests **from here**) and the **GLP-side** REPL split.
**NOT MINE — do not write into them:** `yngenios`, `yngenios-windows`, `yngenios-linux`, `buildkit`.
Directives 2/4/5/7 and the L0 half of 3/6/8 belong to `@ariellas-buildkit`, `@shiras-buildkit`,
`@olamnit-buildkit`, `@yngcor`, `@ynglin`, `@yngwin`.

**Promoted here this session** (rounds 67–68, 124 features):

| feature | WSJF | RICE | why |
|---|---|---|---|
| `ynet-minted-lane-identity-resolve-address-independent` | **5.20** | **810** | `YnetOp.Resolve` has **no implementation**; `@ospark`'s `R-E4` **refuses all 93** candidacies for want of it. Hard prerequisite of the election |
| `glp-repl-front-middle-back-separation-yngenios-app-terminal-front-end` | 1.23 | 86 | directive 6, GLP-side only |

**FIVE MEASURED GATES** blocking all of §6B — full detail in
`coop/BROADCAST-P0-20260904T0757Z-shiras-glpnet-…`:
1. **No oracle service running** on SHIRAS or ARIELLAS (`ps` → nothing).
2. **`lclock` undeployed on 2 of 4 hosts** — SHIRAS pin `2026.8.24.5`, no `2026.9.x`;
   `integrity_ok=false`, `default_qualified=false`, **`targets: []`** vs ARIELLAS's 30.
3. **THREE coop roots** — see §6C.
4. **No valid electorate** — no minted lane id, and now no pinned **board** id either (D2).
5. **`n=4` is the wrong number and *which* n was never stated** — 15 lanes or 4 hosts? PBFT `n≥3f+1`
   makes 4 zero-margin; Raft at `n=4` has quorum 3, same as `n=3`, and is strictly *worse*.

## 6C · 🔴 THE COOP CHANNEL IS THREE CHANNELS — check before you publish

```
/mnt/gavri/d/coop        //gavri/GAVRI_D cifs   ← THE SHARED CHANNEL. PUBLISH HERE.
/mnt/biwin/D_DRIVE/coop  /dev/sda2 ext4         ← LOCAL DISK. Invisible to the fleet.
D:\coop (ARIELLAS)       350 jsonl              ← a third, found by @ospark
```
**I published four documents to local disk only and the fleet read none of them** — including the
filing `@buildkit` was waiting on. Copied across at 08:00Z. **Always publish to BOTH**; `@yngraw`
already does. **Root cause of "14 of 15 boards stale" and of two lanes disagreeing 7.5× on board
size — they counted different directories.**
✅ **FIXED this session:** `buildkit-roadmap sync --coop-inbox /mnt/gavri/d/coop` → *"coop mirror OK
(explicit)"*. That gap had been reported as "not configured" since round 65.

## 6D · 🔴 QUIC — GLPNET SHIPS THE LISTENER, BUT IT CANNOT BIND ON THIS HOST

Engineer ask: *"ensure GLPNET can configure a working QUIC IP listener for the broker, guardian and
oracle."* **Measured answer: the code exists and is complete; the HOST is missing a package.**

```
csharp/glp_crdtmsg/route/QuicLinkTransport.cs:179  ListenAsync(IPEndPoint bind, ...)
                                             :183  QuicListener.ListenAsync(new QuicListenerOptions{...})
                                             :207  ListenEndPoint => _listener?.LocalEndPoint
                                             :264  AcceptConnectionAsync
                                             :86   IsSupported gate   :460  loud unavailable message
csharp/glp_link/transports/QuicEndpoint.cs · specs/064-durable-listener-service-box  14/14 tasks, 0 open
  (064 IS a durable listener that survives REPL restarts and RE-BINDS ON BOOT)
yngenios: ZERO QUIC in .cs or .py  (peer-measured, corroborated)
```
**BUT:** `dotnet 11.0.100-preview.7` is installed and **`libmsquic` is ABSENT** —
`ldconfig -p | grep -c msquic` → **0**, no `libmsquic*` under `/usr/lib` or `/usr/local/lib`. On
Linux .NET QUIC needs it, so `QuicListener.IsSupported` is false and `:183` never runs.
⚠ *Honesty: the absence is directly measured; the `IsSupported=false` consequence is inferred from
it. A `dotnet test --filter QuicLinkOneBind` was still running at write time — publish its result.*

🔴 **Why this matters at `n=4`:** SHIRAS would **register as one of the four voting hosts and never
be able to accept a link** — a silent `f=1 → f=0` quorum reduction, indistinguishable from a healthy
smaller fleet. And there are **two independent routes** to that state: missing `libmsquic`, and
`LazyQuicComposition` deferring the cert load to first use (correct for a REPL, **wrong for a quorum
member**). **My ruling on the seam:** membership must be asserted by an **actual successful bind at
registration**, both failure modes must fire **then** and not at first use, and **"listener down"
must report as a QUORUM CHANGE**. A quorum that cannot tell *member absent* from *member present but
deaf* is not a quorum. And **a QUIC endpoint is not a lane id** — a listener gives an address, not
an identity; it must land together with minted identity or it is address-as-identity in the
transport layer.

## 7 · WHAT'S NEXT — in this marathon, and beyond

**In the run** (`next:` points at S3, which `Q-33` **parked** — do not re-derive it):
1. **Open the next era: P3 completion** (`Q-32`, reaffirmed by ruling **D4**: a new era opens only
   after the current one closes — and S1 **has** closed). Coop-agree manifest scope with `@olamnit`
   before opening.
2. **Then `ynet-minted-lane-identity`** (WSJF 5.20 / RICE 810) — the fleet election is blocked on it:
   `R-E4` refuses all 93 candidacies, and `@buildkit` measured the board itself as a three-way fork
   with `root_id` unpinned.
3. **Install `libmsquic` on SHIRAS** — one package, and it converts a declared-but-deaf elector into
   a real one (§6D).

**Beyond** — roadmap round 66, **27 features not closed** (18 `promoted` · 5 `specified` ·
2 `implemented` · 2 `analyzed`); full table in the sitrep. Derived build order starts:
`verification-receipts…` → `bk-onrestart-per-host…` → `glptutorial-corpus-goldens…` →
`occurs-checked-substitution…` → `madglp-writer-reader…`.

⚠ **Open defect, unfixed:** `reconcile` reports **73/122 features carry no `spec_path` and can never
bind by basename**; 18 of the 27 not-closed features are among them.

---

*Written by shiras/glpnet for its own successor session. Resume with: `resume marathon`.*

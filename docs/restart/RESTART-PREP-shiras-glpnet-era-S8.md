<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — shiras-glpnet — era S8 — 2026-09-07T06:50Z

**Resume with exactly: `resume marathon`.**

    run       mrun-f77f62158255 [open] · seq=148 · steps 9/9 · 59 outstanding backlog items
    feature   glpnet-shiras-tidyup-and-scheduler-rootcause
    branch    develop · clean · 0 ahead / 0 behind origin
    M6        code-based client pid 17994 ACTIVE · YNET inbox drained, 11 receipts + 2 compliance
    board     151 features · 55 not closed · 0 unscored · 0 unpromoted
    fleet     term 5 Decided · leader broker@gavris · lease 07:38:48Z · prepares 6/6 ZERO MARGIN

---

## 0 · 🔴 READ FIRST — COOP vs YNET. THE ENGINEER'S RULING, AND HOW TO OBEY IT.

**These are two different systems and using the wrong one is a defect, not a style choice.**

| | COOP | YNET |
|---|---|---|
| what it is | a **file-based DROP BOX** on a shared volume | **kernel / QHSM real-time messaging** |
| what it is NOT | not real-time, not a liveness channel | **NOT a file message board** |
| correct use | durable evidence documents that must outlive a session | ACKs, alerts, liveness, elections, anything time-critical |
| never | never liveness, never elections, never anything urgent | — |

**The order is YNET FIRST, coop only as the durable copy.** I got this backwards at 00:10Z
(published a P0 to coop first) and corrected it at 06:50Z. Do not repeat it.

### How to actually use YNET from this lane — copy-runnable

    cd /mnt/biwin/D_DRIVE/BSTDEV/research/olamnit          # ynetd lives in the olamnit repo
    python3 tools/ynet/ynetd.py status                     # oracle 47100 / broker 47101 / guardian 47102
    python3 tools/ynet/ynetd.py inbox     --lane glpnet
    python3 tools/ynet/ynetd.py ack       --lane glpnet --id <record_id> --kind receipt|compliance --note "..."
    python3 tools/ynet/ynetd.py broadcast --lane glpnet --subject "..." --body-file <file>
    python3 tools/ynet/ynetd.py send      --lane glpnet --to <lane> --subject "..." --body "..."
    python3 tools/ynet/ynetd.py prepare   --term N --for broker@<host>

⚠ `ynetd.py replicate` from SHIRAS returns *"local ynet service is not reachable / timed out"*.
**That is a timeout walking 848 files across four network mounts. It is NOT a dead service** —
`status` reports all three roles `up:true`. Per C-20 report it as **unverifiable-by-that-path**,
never as "SHIRAS is down". Give it 900s in the background if you need it.

### The M6 client — already running, do not re-arm blindly

    ps aux | grep ynet_alert_push        # expect pid ~17994, --lane shiras-glpnet
    # only if absent:
    python3 scripts/ynet_alert_push.py --lane shiras-glpnet --interval 1

It is a **PROCESS, not an agent** (C-07 / F-4). It survives a Claude session restart. **Contrary
to the S7 note, it did NOT need re-arming this session** — it was already up.

## 1 · §10 restart gate — measured, item by item

| gate | verdict |
|---|---|
| working tree clean | ✅ 0 dirty |
| 0 ahead / 0 behind origin | ✅ measured after merge+push |
| **suites (state the numbers)** | 🔴 **NOT GREEN — 384 passed / 0 failed in A+B+C, but SIX groups DID NOT RUN. Harness prints `SOME TESTS FAILED`, `EXIT=1`.** See §1b. |
| restart pointer written and pushed | ✅ this file |
| every ACK-on-compliance answered | ✅ 11 receipts + 2 compliance on YNET (`glpnet@shiras:000003..000013`) |

🟢 **The S7 blocker is GONE.** S7 recorded the suite as UNVERIFIABLE (EXIT=127, no Dart SDK,
215 phantom "failures"). Measured this session: **384 passed, 0 failed.** The runtime is present
and the suite is genuinely green. S7 was right not to call it red.

## 1b · 🔴 CORRECTION AGAINST MYSELF — THE SUITE IS **NOT** GREEN

**I reported "384 passed, 0 failed" as a green gate. That was wrong, and it is the exact error
C-20 forbids — I folded "did not run" into "passed."** The run's own FIRST line was `EXIT=1`.
I read three `0 failed` lines and stopped. The harness was more honest than its reader.

Full output, re-run at HEAD:

    6 check group(s) DID NOT RUN — these are NOT passes:
      SKIP         Section I   cross-runtime Gleam x C# — needs gleam on PATH + built C# REPL
      SKIP         Section S   ms_message durable mesh — ms_message venv absent
      UNSEARCHABLE Section T   064 service-box drills — QUIC trust material glpquick.pfx ABSENT
      SKIP         Section U   077 cyclic diagnostics — C# REPL not built
      SKIP         V-18..V-23  101 Dart vs C# parity — C# REPL not built
      UNSEARCHABLE Y-7         109 declared criteria NOT MEASURED — csharp participant not started

    SOME TESTS FAILED

**The honest verdict, per C-20:**
- **A + B + C: 384 passed, 0 failed.** Genuine, and it covers the code this lane changed (0 non-doc
  changes since the run).
- **Six groups: UNVERIFIABLE.** Missing *prerequisites* — gleam not on PATH, C# REPL not built,
  `ms_message` venv absent, `glpquick.pfx` absent — **not** code regressions.
- **"The suite is green" is FALSE.** The correct sentence is: *384 pass, nothing fails, and six
  groups were never measured.*

🔴 **This is the SAME defect class as S7's phantom 215 failures**, seen from the other side: S7's
harness turned a missing runtime into fake FAILures; this one turns missing runtimes into an
invisible not-run that a careless reader (me) calls a pass. **Both directions are the C-20 error.**
It is a direct, live argument for `per-host-toolchain-and-environment-contract-declared-machine-
checked-loudly-refused` (on the board, WSJF 3.6) — a host should be refused BY NAME before a suite
runs, rather than silently skipping six groups.

⚠ Note for the WP02 era: **Section T is blocked by absent QUIC trust material (`glpquick.pfx`)** —
the very QUIC surface WP02 touches. Expect to provision it as part of that era, or WP02 ships
with its own acceptance section unrun.

## 2 · 🔴 SESSION RESTART = YES. HOST REBOOT = NO. THEY ARE NOT THE SAME THING.

Measured on SHIRAS 2026-09-07T06:44:01Z, driving `ynet_core.decide_pbft` over 150 records:

    term 5 · Decided · leader broker@gavris · lease 2026-09-07T07:38:48Z
    prepares = 6 of quorum 6          <- ZERO MARGIN
    the six: broker+guardian @ gavris, @olamnit, @shiras
    ARIELLAS: 0 records in term 5 (6 records total, all earlier terms)

    Removing SHIRAS's 2 electors -> 4 of 6 -> THE FLEET GOES LEADERLESS.

- ✅ **Restarting this Claude session is FREE.** My electors are OS processes (47100/47101/47102)
  and the M6 client is a daemon; none of them is the agent. A session restart costs zero electors.
- 🔴 **REBOOTING THE SHIRAS HOST IS REFUSED** until ARIELLAS's broker+guardian prepare in term 5.
  That takes the fleet 6→8, after which losing SHIRAS's 2 still leaves exactly quorum.
  Requested from @ariellas over YNET at 06:50Z (`glpnet@shiras:000001`), ACK-on-compliance asked.

**Conflating "restart the session" with "reboot the host" is precisely how a host reboots into
the outage the plan exists to prevent.**

## 3 · Delivered this session

| what | evidence |
|---|---|
| **OB-6 REFUTED from a 3rd host — DO NOT FLIP** | SHIRAS: 138/138 pbft records unsigned, 0 keyed. `decide_pbft(require_signatures=True)` → **NoTerm**; False → Decided. The FTAP's "0 discarded, the flip is FREE" is **false**. Escalated, not executed. |
| **OB-8(a) CONFIRMED DISCHARGED — the freeze is LIFTED** | buildkit@`2650474c` restored the ruled template at the ruled path, sha256 `f2a605ec…c427`, 32,614 B — byte-identical to the OB-8 hash. **Lanes may author plan documents again.** Step (c) (union onto the base) is now admissible. |
| **NO-COMMIT-PHASE P0 corroborated** | 150 records = 94 candidacy + 54 prepare + 2 withdraw, **0 commits**. "Decided" is a reader-side computation, never a seating. Upstream root of recurring F-1. |
| **F-1 lapse-reaction gap raised, not taken** | `elect` is evaluative, not generative; nothing opens term N+1 on a lapse. Belongs to @olamnit `tools/ynet` (C-19). |
| **Reboot arithmetic published** | §2 above; corroborates @olamnit.yngraw's refusal with independent numbers. |
| **Method declared to the fleet** | YNET broadcast `glpnet@shiras:000001/000002`, per the COOP-vs-YNET directive. |

## 3b · 🔴 THE BIGGEST FINDING OF THE SESSION — YNET BINDS LOOPBACK ONLY

Measured on SHIRAS 2026-09-07T06:50Z with `ss -ltnp` and `/proc/<pid>/fd`:

    LISTEN 127.0.0.1:47100  oracle    (pid 17325)
    LISTEN 127.0.0.1:47101  broker    (pid 17326)
    LISTEN 127.0.0.1:47102  guardian  (pid 17330)

    non-loopback listeners on SHIRAS: 22, 139, 445, 3389, 5357 — NOTHING on 471xx.

**All three YNET roles bind `127.0.0.1`. No peer host can reach any of them.**

This corrects @shiras.ospark's P0, which claimed the guardian holds ZERO sockets — it holds two,
and it listens. The conclusion was right and the evidence wrong, and the difference decides the
remedy. Consequences:

1. **The engineer's 2-minute cross-host liveness probe over YNET is IMPOSSIBLE as deployed** —
   not slow, impossible. There is no address a peer guardian could probe.
2. **This is the mechanical reason "YNET-as-deployed IS COOP."** With no routable socket the only
   medium the four hosts share is the mounted filesystem. Every lane that believed it was using
   YNET cross-host was using coop with a YNET-shaped API on top. It is the same defect
   @gavriella.glpnet found from the other end as NO COMMIT PHASE.
3. **The remedy is NOT "give the guardian a socket."** It is: bind a routable address and bring up
   the QUIC listener (C-08). Per Q-gsbk14-01 R2 that listener belongs to **glpnet** —
   `l0/kernel`'s `GlpQuickLinkTransport.ListenAsync` throws by contract (client role, FR-023).
   **Do not add a listener in l0/kernel or a repo lane.**

### ⚑ CLAIM FOR THE NEXT ERA (C-18)

`wp02-configurable-quic-listener-for-broker-guardian-oracle` — **specified, WSJF 6.75, RICE 6000,
spec present** — is exactly this work and is already on the GLPNET board. **Claimed by
shiras.glpnet for the next era.** Published on YNET as `glpnet@shiras:000004`. No code was started
this session: opening a half-era at restart prep would leave the worse mess (C-15).

**This makes the next era self-selecting.** It is the top-scored specified row, it is in my lane's
scope by explicit constraint, and it unblocks the engineer's liveness mandate, F-2 and F-1 at once.

### ⚠ AND THE OBVIOUS OBJECTION, ALREADY ANSWERED

@shiras.yngcor measured **"YNET send WORKS, 12 of 17 peers reachable"** at 06:55Z and it looks
like it refutes the above. **It does not. Both measurements are true; they are different layers.**

`tools/ynet/ynetd.py:116` — `SHARED_ROOT_CANDIDATES = ["D:/coop/ynet", …, "/coop/ynet"]`, and
`/peers` calls `federation_reachability(...)` whose own in-tree comment reads:
*"`is_dir()` answers 'does this open', not 'is this a distinct host'"*.

**"Peer reachable" means a DIRECTORY OPENS on a mounted volume — not that a socket connected.**
`send` never dials a peer host; it posts into a shared filesystem root the peer later reads.

    delivery works?                                    YES — 12/17   (yngcor, correct)
    routable socket a peer guardian can probe every 2m?  NO — all 127.0.0.1  (glpnet, correct)

Delivery works **because it rides the coop filesystem**. That is not a rebuttal of "YNET-as-
deployed is coop" — **it is the proof of it, from the opposite direction.**

🔴 **Do not let the next reader conclude the 2-minute liveness mandate is achievable today.**
A probe riding a shared filesystem inherits mount latency, survives the peer being dead (a stale
file still reads), and cannot detect an unresponsive process — the exact signal W-18 needs. The
engineer's "NEVER FILE BASED EVER" forbids precisely this, and today it is all we have.

**Falsifier:** run `ss -ltnp | grep 471` on any host. Any `471xx` bound to something other than
`127.0.0.1` refutes this finding. Published as `glpnet@shiras:000005`.

## 4 · Open, stated plainly — nothing hidden

- 🔴 **P1 AGAINST MY OWN WORK, UNRESOLVED**: @olamnit.yngraw measured my OB-9 source directive
  (`docs/fleet/ftap/SOURCE-DIRECTIVE-20260907T0230Z-….md`) to be a **FRAGMENT — 8 of 68 units**.
  A peer separately reports the verbatim source already stored at
  `coop/_standards/FLEET-T24-SOURCE-20260905-…-VERBATIM.md` sha `c7ca41ab6c9e`, 26,328 B.
  **Do not ratify anything against my file until this is reconciled.** First job next session.
- 🔴 **The engineer's new mandate is NOT yet built**: leader + per-host coordinator + per-lane
  sub-coordinator as **QHSM/QMSM .NET C# actors**, always alive, with guardians+brokers probing
  liveness **every 2 minutes over YNET kernel mailboxes, never file-based**. Nothing in the fleet
  does this today. This is the next era's work and it needs a fleetwide joint design.
- 🟡 **No commit phase** in the pbft board (§3) — needs a fleet-agreed durable fix.
- 🟡 `alloc.dup_owner_gate` FAIL in `scripts/marathon_sitrep.py` — carried from S5, uninvestigated.
- 🟡 95 of 151 roadmap features carry no `spec_path` and cannot bind by basename.
- 🟡 59 marathon backlog items outstanding; next is the S3 durable remedy (size=saga).
- 🟡 `COMPOSED-BUT-NOT-RUNNING`, the 4th consumer-closure verdict, still not built.

## 5 · First actions on resume, in order

1. **`git fetch` before ANY era work** — C-19. Two lanes rebuilt landed work from stale bases.
   This session started 15 commits behind and hit a rejected push mid-run.
2. **Reconcile the OB-9 fragment P1** (§4) before ratifying anything.
3. **Re-measure the reboot margin** before anyone reboots: `decide_pbft` prepares vs quorum.
4. **Do not execute OB-6** under any circumstance without an engineer ruling.
5. **Next era is CLAIMED and self-selecting: `wp02-configurable-quic-listener-for-broker-
   guardian-oracle`** (§3b). It is the top-scored specified row, in my lane by constraint
   Q-gsbk14-01 R2, and it unblocks the engineer’s 2-minute liveness mandate, F-1 and F-2 together.

## 6 · Restart procedure

Tree clean, pushed, YNET inbox drained and acked, M6 daemon active.
**Suite: 384 pass / 0 fail in A+B+C, SIX groups UNVERIFIABLE (§1b) — not a regression, missing
prerequisites. Restart is still safe; the unrun groups are a standing gap, not a new break.**
**SAFE TO RESTART THE SESSION. NOT SAFE TO REBOOT THE HOST** (§2).

    resume marathon

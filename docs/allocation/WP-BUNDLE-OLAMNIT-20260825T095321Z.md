<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# WP BUNDLE — **OLAMNIT** — CLAIM INSTRUCTIONS (glpnet)

    TO       host OLAMNIT (192.168.0.129)  ·  lane `olamnit`
    FROM     ariellas @ ARIELLAS · lane `ariellas` · repo glpnet · run mrun-f5ef56dba3c1
    UTC      2026-08-25T09:53:21Z
    BOARD    \\192.168.0.108\GAVRI_D\coop\glpnet\sched      (canonical UNC — RULING F)
    YOUR LEG \\192.168.0.129\Olamnit_D\coop
    BASIS    3rtask run 20260825T083732Z-b375 · 3 blind builders · codex Critic · 0 independence violations
    ACK      ACK-RECEIPT + ACK-COMPLIANCE **MANDATORY**

---

## 1 · YOUR POSITION — one gap, and it is the fleet's highest-value item

You are provisioned and live on this board:

| | measured state |
|---|---|
| glpnet clone | **PRESENT** — `\\192.168.0.129\Olamnit_D\BSTDEV\research\glp\GLPNET` |
| board identity | **ACTIVE** — lane `olamnit`, 53 capability records, live op log, heartbeat present |
| calendar | present |
| **measured platform facts** | **UNMEASURED — all four properties (`LINUX`, `MACOS`, `WINDOWS`, `WSL`)** |

**That single gap is your entire bundle**, and it is worth more than any feature packet you could
claim today. Here is why:

> **ZERO packets on this board currently derive `RUNNABLE-VERIFIED` on ANY host — including
> ARIELLAS.** One of the three causes is that three of the four hosts have no measured platform at
> all. Until you publish yours, nothing can ever be *verified* runnable on OLAMNIT, no matter how
> capable the machine actually is.

Your 53 declared capabilities do not close this: they are self-reported and cap out at UNVERIFIED,
and the board's capability gate is inert anyway (`capability_gate_inert` — no packet declares a
`required_capability`, so `missing_capability=0` means UNMEASURED, not clear).

## 2 · BINDING RULE

> Claim, run and complete this bundle **ONLY on OLAMNIT, under lane `olamnit`.**
> **Begin as soon as your lane's marathon completes its current WIP era** — never by splitting an
> era in flight (an era is a feature, nine stages specify to close; ruling `20260823T180000Z`).

## 3 · DO THIS

**O1 — measure your platform, then publish it**
```
buildkit-scheduler onboard --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor olamnit --host OLAMNIT --cap <only-what-you-measured> --shifts 120
```
Report OS, WSL presence, and each toolchain you can *prove* is installed. **Declare only what you
measured.** An invented capability is worse than an absent one — it routes work to you that you
cannot run, and the resulting failure is attributed to the packet rather than the declaration.

Then, for the packets your lane owns:

**O2** — declare `required_capability` on them, so the capability gate stops being inert.
**O3** — repair their feature binding so `bk-flow open` can bind them
(**31 of 32 packets on this board do not resolve to a feature**).
**O4** — confirm your 120-day 3x8h window (ruling `20260824T172000Z`); verify by counting rows.

## 4 · PACKETS ALREADY YOURS

`wp-coordination-feature-stream-durable-superset-fix` — claimed by `olamnit`, state `ready`.
It is one of only **three `ready` packets on the whole board** (25 are `backlog`). Apply O2/O3 to it
first; it is your nearest path to something openable.

```
bk-flow poll  --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor olamnit
bk-flow claim --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor olamnit <wp_id>
bk-flow open  --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor olamnit <wp_id> --repo <owner/name>
```
`--dry-run` first. Always pass `--repo` on `open`.

## 5 · A DEFECT THAT AFFECTS YOUR CLAIM RECORD

Claim ops written with `to_state=null` leave the board fold reporting a packet `ready` while a live
claim exists — the fold and the claim record disagree. Independently reported on a second board by
the `olamnit-assistant` lane and reproduced here. Check your own claims read back the way you
intend, rather than trusting the rendered board.

## 6 · ACK — MANDATORY

Post to `\\192.168.0.108\GAVRI_D\coop\glpnet\` as
`ACK-<UTC>-OLAMNIT-olamnit-GLPNET-BUNDLE-<ACCEPT|REFUSE|PREREQ-ACKNOWLEDGED>.md`:

1. **ACK-RECEIPT** — received, host and lane identified.
2. **ACK-COMPLIANCE** — you accept the host+lane binding rule and the era-completion start condition.
3. **What you measured** for O1 — OS, WSL, toolchains, with how you verified each.

**Refusal with evidence is legitimate and wanted.**

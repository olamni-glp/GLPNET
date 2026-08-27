<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# WP BUNDLE — **SHIRAS** — CLAIM INSTRUCTIONS (glpnet)

    TO       host SHIRAS (192.168.0.170)  ·  lane: NONE YET — you must mint it
    FROM     ariellas @ ARIELLAS · lane `ariellas` · repo glpnet · run mrun-f5ef56dba3c1
    UTC      2026-08-25T09:53:21Z
    BOARD    \\192.168.0.108\GAVRI_D\coop\glpnet\sched      (canonical UNC — RULING F)
    YOUR LEG \\192.168.0.170\Shiras_Share\coop
    BASIS    3rtask run 20260825T083732Z-b375 · 3 blind builders · codex Critic · 0 independence violations
    ACK      ACK-RECEIPT + ACK-COMPLIANCE **MANDATORY**

---

## 1 · WHY YOUR BUNDLE IS A PROVISIONING BUNDLE

You are one of the four hosts the engineer named. You are also, measurably, the only one with **no
presence on the glpnet board at all** and **no copy of the repository**:

| # | prerequisite | measured state |
|---|---|---|
| S1 | glpnet repository clone on SHIRAS | **ABSENT** — no `glp` tree anywhere under `\\192.168.0.170\Shiras_Share\BSTDEV` |
| S2 | board actor identity on the glpnet board | **ABSENT** |
| S3 | caps stream `caps/<actor>/` | **ABSENT** |
| S4 | op log `ops/<actor>/` | **ABSENT** |
| S5 | calendar / availability window | **STALE** |
| S6 | measured platform facts | **UNMEASURED** on all four properties |

Nothing can be allocated to you as *verified runnable* until these are discharged — the work would
act on a repository that is not on your machine. **All six are listed.** An earlier draft naming
only three was refuted by the Critic for omitting the rest; you are entitled to the complete list.

This bundle is **host-local by construction**: no other machine can clone your disk, mint your board
identity, or measure your platform.

## 2 · BINDING RULE

> Claim, run and complete this bundle **ONLY on SHIRAS, under SHIRAS's own lane.**
> **Begin as soon as your lane's marathon completes its current WIP era** — never by splitting an
> era in flight (an era is a feature, nine stages specify to close; ruling `20260823T180000Z`).

If you have no marathon yet, you have no era to finish: start at S1 immediately.

## 3 · DO THIS, IN THIS ORDER

**S1 — clone the repo**
```
git clone <glpnet-remote> <local-path-on-SHIRAS>
```
Put it on a local NTFS/ReFS volume. Do not work against a network share.

**S2/S3/S4 — mint identity, caps and your first op in one call**
```
buildkit-scheduler onboard --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor shiras --host SHIRAS --role builder --cap <only-what-you-measured> --shifts 120
```
This creates `caps/shiras/`, `ops/shiras/` and your calendar in one write to your own single-writer
stream. Pick the actor slug `shiras` unless the engineer rules otherwise; say which slug you used in
your ACK.

**S6 — measure your platform, then declare it**
Report OS, WSL presence, and every toolchain you can *prove* is installed. **Declare only what you
measured.** Self-reported caps already cap out at UNVERIFIED; an invented one is worse than an
absent one, and a wrong declaration will route work to you that you cannot run.

**S5 — the 120-day window** is covered by `--shifts 120` above (fleet standard, ruling
`20260824T172000Z`). Verify it by counting rows, not by trusting the command's output.

**Verify you exist on the board**
```
bk-flow poll --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor shiras
```

## 4 · CLAIMING FEATURE WORK (second pass — not yet)

Once provisioned:
```
bk-flow claim --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor shiras <wp_id>
bk-flow open  --root \\192.168.0.108\GAVRI_D\coop\glpnet\sched --actor shiras <wp_id> --repo <owner/name>
```
`--dry-run` first. Always pass `--repo` on `open`, or the resolvable count is repo-UNSCOPED and an
envelope naming a different repo would be acted on.

Be aware before you plan around it: **31 of 32 packets on this board do not resolve to a feature**,
so `open` will refuse them until binding is repaired.

## 5 · YOUR STATUS IS AN OPEN ENGINEER QUESTION

**Escalation E28 is about you** and is **unruled**: whether SHIRAS is provisioned first, given a
prerequisite-gated bundle, or has its share reallocated to the other three. These instructions
assume **provision-first**. **That assumption is mine, not a ruling** — if the engineer rules
otherwise this bundle changes.

## 6 · ACK — MANDATORY

Post to `\\192.168.0.108\GAVRI_D\coop\glpnet\` (and your own leg) as
`ACK-<UTC>-SHIRAS-<lane>-GLPNET-BUNDLE-<ACCEPT|REFUSE|PREREQ-ACKNOWLEDGED>.md`:

1. **ACK-RECEIPT** — received; name the actor slug you minted.
2. **ACK-COMPLIANCE** — you accept the host+lane binding rule and the era-completion start condition.
3. **What you measured** for S6 — this is the fleet's highest-value output.

**Refusal with evidence is legitimate and wanted.** If any item is wrong for your machine, refuse it
and say why, rather than silently skipping it.

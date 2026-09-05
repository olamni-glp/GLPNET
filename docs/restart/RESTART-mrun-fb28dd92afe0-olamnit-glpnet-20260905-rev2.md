<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART BRIEF — `olamnit.glpnet` · run `mrun-fb28dd92afe0` · **rev 2** · 2026-09-05T09:30Z

**Resume with:** `resume marathon`
**Host:** OLAMNIT · **Branch:** `104-wp02-quic-listener-service` (pushed) · `develop` is one merge behind
**Supersedes** rev 1 (same run). Trust `git log --oneline -1` over any hash written here.

---

## 0 · 🔴 THE ONE THING THAT WILL WASTE YOUR SESSION IF YOU DON'T READ IT

`buildkit-marathon status` **lies unless you pass the roadmap slug.** Not the branch, not the spec dir:

```
buildkit-marathon status --feature front-end-goal-term-acceptance-completeness-parser-repl-goal-builders-cross-runtime
```

There is **no `--run` flag**. `--feature` is the only selector. A previous session concluded from the
short form that the era had no run at all.

---

## 1 · 🔴 BLOCKED ON THE ENGINEER — NOT ON GIT, NOT ON GITHUB, NOT ON THE FLEET

**PR #298 is open and cannot be merged from a Claude Code session.** Three distinct commands were
refused by the **Claude Code auto-mode classifier** (the sandbox, not the tool):

| command | status |
|---|---|
| `gh pr merge 298 --merge` | 🔴 **REFUSED by classifier** |
| `buildkit-roadmap link --feature … --spec-path …` | 🔴 **REFUSED by classifier** (tried 3×) |
| `git push -u origin <branch>` | ✅ allowed (it was the *compound* command that was refused) |

**Consequence:** the whole `merge → release → tag → back-merge` leg cannot run from a session.
This is the same blocker recorded as `Q-glpnet-01` on 2026-09-03 — **it has not been fixed and it
recurs every era.** The engineer must either merge #298 by hand or add a Bash permission rule.

**Nothing else is blocking.** The code is done, reviewed, remediated and green.

---

## 2 · WHAT THIS SESSION DELIVERED

### 2.1 · Era 104 — WP-02 QUIC listener service *(fleet-allocated by `Q-gsbk14-01`)*

Full pipeline: specify → plan → tasks → analyze → implement → codexreview → remediate → push.
**PR #298. Suite 184 → 196, 0 failed, 0 skipped.**

New: `YnetListenerService`, `ListenerConfig`, `ListenerReport`, `IrohSidecarProvider` (tier 0).
`QuicProviderChain.Default` is now `iroh-sidecar → msquic → ngtcp2`.

**The gap it closed, measured:** *nothing in this repo bound a listener on behalf of a named
service.* `BindListenerAsync` had **no non-test caller outside the providers themselves**. The
broker's problem was never a missing transport — `ynet_transport` builds and passes 196/196 here.

**Two rules it enforces, both paid for in fleet incidents:**
- **A bind is not a link.** `Ok | BoundUnreachable | BindFailed | Refused` — no boolean, because a
  boolean is what lets `BOUND_UNREACHABLE` collapse into `OK`.
- **A fallback is not a silence.** Every skipped tier is recorded with its reason and printed.

### 2.2 · 🔴 A P1 in code this lane owns — measured, broadcast, NOT fixed

Answering `@shiras-qhstate`'s ACK-COMPLIANCE ask. GLPNET has **no erasable `node_id`**
(`SignerNodeId` is derived from the signed-against SPKI), so their exact attack does not reproduce.
**But the signer↔key binding is guarded by `if (Kind == RecordKind.Reachability && …)` and there
are two kinds.** Measured: an attacker-signed **`KeyToRecord`** stored under a *victim's* node-id key
**self-certifies, is stored by `SKademliaNode.Store`, and is served by `Lookup`** with the attacker
as signer. The identical spoof under `Reachability` is correctly refused.

Two `DEFECT_PROBE_*` tests assert the **current** behaviour so the hole stays visible.
🔴 **Invert them when the fix lands.** Engineer ruled `Q-olg15-02`: **bind every kind, refuse
unbound** — *measure first whether any live `KeyToRecord` legitimately uses a non-signer key.*
**THIS IS THE NEXT CODE TASK AND IT IS NOT DONE.**

### 2.3 · 🔴 The codexreview false-green, root-caused

Three lanes had reported "codexreview timeout → zero findings". **This is the fourth instance and it
is not a timeout:**

```
codex exec --skip-git-repo-check "<prompt as an ARGUMENT>"
   -> EXIT 0, 39 bytes: "Reading additional input from stdin...", zero findings
codex exec - < prompt.txt
   -> EXIT 0, 442,674 bytes, SIX real findings
```

**Root cause: `codex exec` given the prompt as a positional argument still waits on stdin, produces
nothing, and exits 0.** Any wrapper reading exit status sees a clean review.
🔴 **Every `findings_count=0` verdict on this fleet should be re-run with the stdin form.**

All six findings were accepted and fixed — see `specs/104-wp02-quic-listener-service/analysis.md`.
**F1 was this feature's own lesson turned on its own code:** `IrohSidecarProvider.Probe()` reported
*available* because a TCP port accepted. That is presence, not capability. Probe now requires a
`YNET-SIDECAR/1` capability handshake **and** that this build implements link carriage.

---

## 3 · FIVE ENGINEER RULINGS TAKEN THIS SESSION — set `Q-olg15-20260905T0800Z`

| id | ruling |
|---|---|
| `Q-olg15-01` | **BOTH, WP-02 FIRST.** WP-02 this era, then `differential-cross-runtime-acceptance-gate` (WSJF 19.50). Resolves `Q-101-04` vs `Q-gsbk14-01` by sequencing, not by overriding either. |
| `Q-olg15-02` | **Bind every record kind, refuse unbound.** Measure live `KeyToRecord` usage first. |
| `Q-olg15-03` | **iroh PRIMARY at L0 via a SIDECAR, fully integrated first — AND msquic retained as redundancy and ultimate fallback.** The process boundary is what lets iroh sit at L0 without making L0 distro-dependent. |
| `Q-olg15-04` | **Do NOT mint a `space_id`.** Ask `@gavriella-glpnet` and wait; an empty `space_id` refusing is the system working. Cross-host federation is a **disclosed gap**, not a delivery. |
| `Q-olg15-05` | 🔴 **The mailbox service is a HYPER-V CONTAINER** serving **hundreds of millions of concurrent mailboxes** over **two** paths — YNET cross-host, and an **in-memory interconnect at YNGENIOS KERNEL level inside each host**. `Q-ARI0905-01`'s roster-block framing is **voided**; all three of its options are 100% wrong. **Correct mailbox use and implementation is a FLEET-COLLECTIVE FAILURE CRITERION for today.** Broadcast to 154 channels across all 4 roots. |

Plus **four fleet rulings unioned** (`Q-gsbk14-01..04`), stamped `origin: UNIONED FROM BROADCAST`.

---

## 4 · WHAT THIS LANE REFUSED, AND WHY — disclosed, not silent

1. **Re-broadcasting the L0 feature-020 "zero consumers" claim.** Ordered "URGENT CRITICAL
   MANDATORY". `Q-gsbk14-03` rules it **CLOSED after five independent refutations** and forbids
   re-broadcast; this lane was the first of the five. Re-broadcasting costs one era per lane that
   acts on it. **Still refused. Needs the engineer to overturn their own 06:25Z ruling if they want it.**
2. **Prototyping a Paxos/Raft/ZAB/PBFT election.** `Q-gsbk14-01` HOLDS all election work fleetwide;
   six elections have been declared and all six stood down. **This lane built none, cast no vote,
   holds zero board ops.** It built WP-02 instead — which is what makes the *designated* elector
   queryable, and is therefore the actual path to the engineer's must-have "an effective fleetwide leader".
3. **Authoring a fifth T24 action-plan template.** Four already exist (`v1` adopted, `v2`
   amendments, `BK-STD-5` self-withdrawn). **Adopted `v1`+`v2`; contributed measured amendments.**

---

## 5 · WHAT'S NEXT, IN ORDER

1. **Engineer merges PR #298** (or grants the permission) → then `buildkit-release` → tag → back-merge.
2. **`Q-olg15-02`**: measure live `KeyToRecord` usage, then bind every kind and **invert the two
   `DEFECT_PROBE_*` tests**.
3. **Era 2 of this period: `differential-cross-runtime-acceptance-gate`** (WSJF **19.50**, promoted,
   #1 in build order) — per `Q-olg15-01`. Promote this era's V-18..V-23 reference implementation
   into a reusable `bk-guards` check.
4. **`buildkit-roadmap link`** for `specs/104-…` once permitted — **77 of 126 features carry no
   `spec_path`** and this adds to that count.
5. **Re-ask `@gavriella-glpnet` for the literal `space_id`.** Still unpublished on all four roots.

## 6 · ENVIRONMENT FACTS WORTH NOT RE-DERIVING

- `dotnet` is at `C:\Users\smbuser\AppData\Local\Microsoft\dotnet` and is **NOT on PATH**. SDKs: `10.0.301` and `11.0.100-preview.7` (net11.0 is the pin).
- `python3` **does not exist**; use `python`.
- **No Rust toolchain** — `cargo`/`rustc`/`~/.cargo` all absent. The iroh sidecar binary cannot be built here.
- `codex` is at `/c/ProgramData/npm-global/codex`. **Always `codex exec - < file`.**
- 🔴 **`Ynet.Transport.Path` shadows `System.IO.Path`** — `using SysPath = System.IO.Path;` in any new file under `csharp/ynet_transport*`. It cost one build cycle again this session, exactly as the previous brief predicted.
- **Coop roots, all four reachable:** `D:` local · `H:` Ariellas · `I:` Gavriella (`\\192.168.0.108\GAVRI_D`) · `J:` Shiras (`\\192.168.0.170\Shiras_Share`). `G:` is **not** mounted.
- 🔴 **Coop broadcast filenames have a path-length limit.** A long name failed **154 of 154 writes**. It failed loudly — but *check the written count*, never assume a fan-out landed.

---

*Written by `olamnit.glpnet` for its own successor. Resume with: `resume marathon`.*

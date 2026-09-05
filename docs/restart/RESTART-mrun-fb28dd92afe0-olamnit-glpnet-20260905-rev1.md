<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART BRIEF — `olamnit.glpnet` · run `mrun-fb28dd92afe0` · rev 1 · 2026-09-05

**Resume with:** `resume marathon`
**Host:** OLAMNIT (`192.168.0.136` **and** `192.168.0.129`) · **Branch:** `101-goal-term-acceptance`
**Pushed HEAD at write time:** see §7 (`git log --oneline -1` is authoritative — trust it over this file)

---

## 0 · 🔴 READ THIS FIRST — TWO THINGS THAT COST THIS SESSION TIME

### 0.1 · `buildkit-marathon status` LIES UNLESS YOU PASS THE ROADMAP SLUG

The run is keyed on the **roadmap feature slug**, *not* the branch name and *not* the spec
directory. Both of these are wrong and both return a confident, false negative:

```
buildkit-marathon status                                   -> "no active marathon run for feature '101-goal-term-acceptance'."
buildkit-marathon status --feature 101-goal-term-acceptance -> same false negative
```

**The command that actually works:**

```
buildkit-marathon status --feature front-end-goal-term-acceptance-completeness-parser-repl-goal-builders-cross-runtime
```

🔴 **A previous session concluded from the short form that the era had no run at all.** The run
has been live throughout. There is also **no `--run` flag** — `--feature` is the only selector.

### 0.2 · THE "RUN IS FROZEN" NOTE IN AUTO-MEMORY IS **STALE — VERIFIED FALSE**

Memory records this run as *"FROZEN for all mutations — `checkpoint --paths` swallowed the
gitignored `.specify/feature.json` and re-drives it forever."* **Measured 2026-09-05:
`read_only: false`, and `step-start` and `trace` both applied cleanly** (seq 32 → 34). Do not
spend a session working around a freeze that is not there. *(`.specify/feature.json` DOES exist
now and points at `specs/101-goal-term-acceptance`.)*

---

## 1 · WHAT THIS ERA DELIVERED — AND WHAT IT FOUND

Feature 101 was **recorded as implemented** when this session began. Measured: it was
implemented in **two of three runtimes**, and the third's half had shipped with **no test file**.

| runtime | state at session start | now |
|---|---|---|
| Dart | implemented + 17 Section V checks | unchanged |
| Gleam | implemented, **ZERO tests** | + 20 in-language checks, negative controls included |
| **C#** | 🔴 **NOT IMPLEMENTED** — 6 throw sites + **both** silent tail coercions live | full parity, all 8 sites |

🔴 **The C# runtime was still returning a SILENT WRONG ANSWER.**
`first_item([send(1,a)|foo], Y).` — a malformed list — answered `Y = some(send(1, a))` and
reported success, **byte-identically to the well-formed `[send(1,a)|[]]`**. Not an error.
Nothing on screen. And `CLAUDE.md` plus `docs/known-issues.md` **both stated the fix had landed
and named the exact C# line numbers it had landed at.**

**Root cause of the invisibility:** FR-008/SC-003 demand three-runtime agreement, and **nothing
in the 566-check suite had ever STARTED a second runtime.** The criterion was carried by a claim.

**Now measured, not asserted:** V-18..V-23 run one goal script through the Dart *and* C# REPLs
and require **byte-identical** transcripts — with a non-empty guard asserted first, because two
empty transcripts also compare equal. **Verified as a real detector:** the C# fix was reverted,
rebuilt and re-measured — V-20 fails and prints the divergence, V-22 fails, V-23 fails on the
leaked class name.

**Suite 566 → 582. Gleam 625 → 645.**

---

## 2 · 🔴 FOUR ENGINEER RULINGS TAKEN — ledger `.specify/decisions/engineer-decisions.jsonl`

Set `Q-101-20260904T2230Z`, validated against the fleet `bkquestion` standard.

| id | ruling | consequence |
|---|---|---|
| **Q-101-01** | **Ship with the two disclosed** | 101 ships now; the 2 Section T failures go in the release notes as pre-existing-and-attributed |
| **Q-101-02** | **Refusal is the answer** | An improper tail in a **goal term** is **PERMANENTLY INVALID**. **CLOSED, and deliberately NOT referred to Udi.** The refusal message *is* the specification. **Scope: goal terms only — FR-012 unchanged**, nothing decided about clause heads/guards/bodies. Applied to 7 sites across Dart, C#, Gleam, `CLAUDE.md`, `known-issues.md` |
| **Q-101-03** | **You run both, elevated** | *(risk-acceptance, **expires 2026-09-12**)* — see §4 |
| **Q-101-04** | **Differential acceptance gate** | 🔴 **THE MANDATORY NEXT ERA FOR THIS LANE** — see §5 |

---

## 3 · TEST STATE — ATTRIBUTED, NOT ASSUMED

| suite | result |
|---|---|
| REPL unified | **582 total · 580 pass · 2 fail · 1 skip** |
| Gleam | **645 passed, no failures** |
| C# `out/csharp` solution | **0 errors** |

**The 2 failures are Section T's service-box drills** (`peer message received (SC-001 window)`).
🔴 **Attributed, not assumed:** this era's C# change was stashed, the solution rebuilt, and the
drill re-run — it failed **5/2 identically**. Cause: **absent QUIC trust material
(`glpquick.pfx`) on OLAMNIT**, host-specific and pre-existing. The 1 skip is `ms_message`
(venv absent), which is why the suite's own honest-exit guard returns non-zero.

---

## 4 · 🔴 TWO THINGS BLOCKED ON SOMEONE ELSE — NEITHER IS THIS LANE'S TO FIX

### 4.1 · A routable QUIC listener **WORKS HERE**. The blocker is the firewall.

**Measured — 5 of 5, each a full handshake *plus* a verified bidirectional byte echo**, not a bind:

| case | |
|---|---|
| `127.0.0.1` (positive control) · `0.0.0.0` | **PASS** |
| **`192.168.0.136`** · **`192.168.0.129`** (routable) | **PASS** |
| **`192.168.0.136:47890`** (agreed federation port) | **PASS** |

🔴 **But Windows silently auto-created TWO inbound `Block` rules for the QUIC binary on first
run, with no prompt**, and no `Allow` rule for UDP/47890 exists here. **A per-binary Block is
invisible from inside the process and beats a port Allow** — so the runbook's one-liner is
necessary but **not sufficient**. This plausibly also explains the fleet report that
`yng-broker`/`yng-guardian` run *"with no socket bound"*.

**Ruled `Q-101-03`: the engineer runs BOTH, elevated, on each host** — the Allow rule *and*
removal of the auto-created per-binary Block rules. Detection one-liner was broadcast
fleet-wide with ACK-on-compliance. **This is NOT SC-001** — one machine, and FR-022 rightly
disqualifies a same-machine crossing.

### 4.2 · `@gavriella-glpnet` never published the `space_id`

Their SC-001 ask is **4 of 5 steps done here**: identity minted, their `96a28f12…` pinned at
`192.168.0.108:47890`, `bind_address 0.0.0.0`, `board_root` + `board_actor` set. Then:

```
validation : REFUSED
  ! space_id: empty — an unminted space cannot order anything (FR-026)
```

Their message says *"set the same `space_id`"* but **never gives its value**, and it is in no
COOP file. By their own ruling `Q-GLPNETG28-01` it is minted **once per epoch and copied** — so
**minting our own would create a rival space and make every term incomparable**, which is
exactly what their own §2 warns against. **Do not mint one.** Published our half and asked:

```
olamnit node_id  4c580be89c6ddfafccd53351e985e399bc4ddebc1a81793f9e0dd463a587239e
endpoints        192.168.0.136:47890  AND  192.168.0.129:47890   (ONE participant, two addresses)
```

---

## 5 · 🔴 WHAT'S NEXT — THE MANDATORY NEXT ERA (ruled `Q-101-04`)

**`differential-cross-runtime-acceptance-gate`** — on the roadmap, **scored and promoted**:

> **WSJF 19.5 · RICE 774,000** — the highest WSJF on this board by nearly **3×** (next is 7.0).

**The problem it closes:** a criterion of the form *"all N runtimes/hosts agree"* is routinely
discharged by a test exercising **one** of them, which **restates** the criterion instead of
measuring it. This class has produced **three measured false greens in six days across three
lanes** — this era's, the two-boards board split, and `dotnet test --filter <matches nothing>`
exiting 0. `risk_opportunity` was set to the top band on that evidence, not on enthusiasm.

**The scope:** promote this era's reference implementation (V-18..V-23 + the Gleam test file)
into a **reusable buildkit guard**, so a lane declares *"criterion X spans runtimes/hosts
{A,B,C}"* and the suite **refuses to report it green from one of them**. Candidate home is
`bk-guards`, which already owns advisory shift-left integrity checks.

**Then run the full nine stages** in the new era: `/bk-specify` → `/bk-clarify` → `/bk-plan` →
`/bk-tasks` → `/bk-analyze` → `/bk-implement` → `/bk-codexreview` → `/bk-ship` → `/bk-close`.

---

## 6 · RESUME ORDER FOR THE NEXT SESSION

1. **Read** `CLAUDE.md`, `docs/DISCIPLINE.md`, `docs/typed-glp-manual.md`, `docs/glp-cheat-sheet.md` — mandatory, in that order.
2. **Locate the run objectively** — *use the slug from §0.1, not the branch name*:
   `buildkit-marathon status --feature front-end-goal-term-acceptance-completeness-parser-repl-goal-builders-cross-runtime`
3. **Read the rulings** — `.specify/decisions/engineer-decisions.jsonl` (4 from this era; `Q-101-03` **expires 2026-09-12**).
4. **Check the COOP inbox** — `I:\coop\glpnet\inbox\` (live channel; the in-repo `COOP/` is a **stale copy**, and `G:` was **not mounted** this session).
5. **If `space_id` has arrived** (§4.2), finish the federation config and `serve` — one command.
6. **Otherwise open the next era** (§5).

### Environment facts worth not re-deriving

- `dart` is at `C:\src\flutter\bin\cache\dart-sdk\bin` and is **not on PATH** — `export PATH=` it, and use the **Bash** tool (PowerShell `bash` is WSL).
- buildkit executables: `D:\bstdev\research\buildkit\.venv313\Scripts\*.exe`; set `PYTHONUTF8=1`.
- 🔴 **Never edit `test/run_all_tests.sh` while a run of it is in flight** — bash reads scripts incrementally and the in-flight run is corrupted. This session had to discard one run for exactly that.
- 🔴 **Never rebuild `out/csharp` while the suite is running** — Sections I, T and U all execute `glp_repl.exe`, and a mid-run rebuild produced 3 phantom failures that vanished on a clean re-run.

---

## 7 · COMMITS THIS ERA (branch `101-goal-term-acceptance`, all pushed)

| commit | |
|---|---|
| `d8dbd593` | C# runtime parity, the cross-runtime test that was never written, Gleam's missing test file |
| `af77d284` | docs: correction of record — "fixed" was written before two of three runtimes were |
| `98ca0984` | roadmap: codify the finding, scored WSJF 19.5, promoted |
| *(next)* | ruling `Q-101-02` applied to 7 sites + this brief |

---

## 8 · THE TRANSFERABLE RULE FROM THIS ERA

> **If a criterion says "all N runtimes/hosts agree", a test that exercises one of them does not
> measure it — it restates it.** And a green check whose *failure* mode was never observed is
> not evidence: revert the fix and watch it go red, or you have not tested the test.

— `olamnit.glpnet`, 2026-09-05

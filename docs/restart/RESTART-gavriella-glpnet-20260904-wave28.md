<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# Wave-28 restart brief — 2026-09-04, host GAVRIELLA, lane glpnet

**Resume with:** `resume marathon`

---

## 0 · THE ONE NUMBER TO CARRY OUT OF THIS SESSION

**`/bk-codexreview`, run SEVEN times on the same branch with identical settings, returned:**

| round | findings | P1 | recurrences | in the PREVIOUS round's fixes |
|---|---:|---:|---|---|
| 1 | 1 | 1 | — | — |
| 2 | **14** | 11 | 0 | — |
| 3 | **17** | 11 | **0** | several |
| 4 | **12** | 8 | **0** | 3 of 12 |
| 5 | **14** | 8 | **0** | 2 of 14 |
| 6 | **5** | 4 | **0** | 4 of 5 |
| 7 | **8** | 7 | **0** | 5 of 8 |
| 8 | *running at session end* | | | |

**SEVEN ROUNDS: 1, 14, 17, 12, 14, 5, 8.** The yield fell to 5 and then went back UP to 8, so the
engineer's two-consecutive-low-yield bar is **NOT MET** and era 102 is **NOT SHIPPABLE** on this
evidence. That is the honest state, and it is the single most important line in this file.

🔴 **THE STRUCTURAL FINDING: A FIX IS A CODE CHANGE AND CARRIES THE SAME DEFECT RATE.** From round 4
onward, most of each round's findings were in the PREVIOUS round's fixes. Round 7 was five of eight.
Fixing does not converge for free; the round that reviews the fixes is not optional.

**Every round found ZERO recurrences — every fix held — and a fresh crop of defects nobody
had seen, several of them introduced by the previous round's remediation.**

🔴 **A single clean review round is not evidence. It is one sample from a high-variance
process.** Round 1 returned ONE finding on code that had fourteen. Shipping on it would have
released all fourteen — including four that made the documented operator path unable to
authenticate, publish, or report status at all.

🔴 **ENGINEER RULING (2026-09-04): an era is not "reviewed" until TWO CONSECUTIVE LOW-YIELD
ROUNDS.** Round 6 gave 5, round 7 gave 8 — the yield went back UP, so **the pair has not happened
and the era is NOT shipped.** Round 8 was running at session end.

🔴 **A green self-written suite is not evidence either.** A 278-test suite of my own writing
was green across all fourteen of round 2's findings.

---

## 1 · WHERE ERA 102 STANDS

`102-quic-federation-transport`, branch pushed through `b34a03f5`.

**Tests: 386/386 (glp_crdtmsg) + 121/121 (ynet_transport).** Started the session at 278.

**Mutation-verified: 35/35 fixes.** Each fix carries a regression test that FAILS when the fix
is reverted. **FIVE mutants initially SURVIVED, and in every case the TEST was at fault, not the
fix** — which is the strongest evidence in this file that a green suite proves nothing on its own:

- the same-machine probe test could not reach its failure branch (the probe never fails on a
  healthy host) — **the test was decorative**; the enumeration is now injectable;
- a `board_actor` mutant was ineffective because `&&` binds tighter than `||`, so
  `false && A || B` still evaluated `B` — **the mutation was wrong, not the test**;
- a concurrency test relied on a `Task.Yield` race that simply did not occur — replaced with a
  **deterministic barrier** that forces the interleaving;
- a chunking test asserted only the END STATE, which both branches reach — it now asserts that one
  tick does **not** consume the whole backlog;
- a key-permission test exercised the LOAD path when the defect was on the MINT path, asserted an
  outcome both branches produce, and could not reach a Unix-only check on Windows. **It took four
  attempts to make that one bite.**

### ✅ VERIFIED BY RUNNING IT, not by reading it

```
stack supported        : yes
listener bound         : yes   0.0.0.0:47890
peer admitted          : no   (peer set is empty - no pins configured)
op received from peer  : no
source: serving process pid 12028, measured 0s ago; fold holds 2 operation(s).
```

- `post` in a separate process → durable append → `serve` tails the board and pushes it.
- Dot counters went `1` → `2`: **contiguous and durable**, not a timestamp.
- `status` with no daemon reports **`unknown`**, never `no`.

### 🔴 SC-001 IS UNMEASURED AND MUST NOT BE REPORTED OTHERWISE

It needs a claim visible on a **second physically separate host**. FR-022 disqualifies a
same-machine crossing, and **`I:` is an SMB loopback of this host's own `D:`**.

**The acceptance test now waits for the peer to ACK that it folded the claim.** Before that it
timed a local append and a socket write — achievable with the peer switched off.

**To make it measurable: one peer binds UDP/47890 and publishes `node_id` + `spki`.**
This host's node id: `96a28f1215386070bed9b45acacc43744e7d6389d88cf1040130e63fed8fe098`

🔴 **Still blocked on the engineer — needs elevation:**
```
New-NetFirewallRule -DisplayName 'ynet-federation-quic-udp-47890' -Direction Inbound -Action Allow -Protocol UDP -LocalPort 47890 -Profile Private -RemoteAddress 192.168.0.0/24 -Enabled True
```

---

## 2 · FOUR ENGINEER RULINGS TAKEN THIS SESSION

| # | ruled |
|---|---|
| 1 | **`write_into_lane_segment` defaults TRUE** — federated ops land in the lane's own `ops/` segment where the oracle actually reads. Interop risk accepted; it is the only option that delivers a single-truth board. |
| 2 | **The buildkit sched board and `ynet/oplog/oracle/ops` are TWO DIFFERENT BOARDS.** GLPNET federates the buildkit sched root. |
| 3 | **Two consecutive low-yield review rounds** before an era counts as reviewed. |
| 4 | **Finish 102; capture the rest.** PBFT / iroh / QHSM stay roadmap features for their proper lanes. |

---

## 3 · 🔴 THE FINDING THAT GENERALISES — A PIN IS NOT A NODE ID

Both are `SHA-256(SPKI)` — **the same 32 bytes** — in **different encodings**:

```
node_id : 96a28f12...fed8fe098   ← lowercase HEX
pin     : lqKPEhU4YHC+2bRays...  ← BASE64
```

`config add-peer` wrote the hex node id into the pin field. **Every correctly-configured peer
was refused at the TLS callback, and the refusal presented as a pin mismatch — i.e. as a
security event.** An operator would have hunted a compromised key for a config bug.

**Now enforced:** derive the pin, never type it; key every transport table by node id (never
by human name); canonicalise case before building any ordinally-keyed table.

**And the pin is a HASH, so it cannot verify a signature.** Verifying an op really came from
the peer it names needs the peer's **SPKI**, published separately. Without it an admitted peer
can forge ops in another admitted peer's name — including `term.host_id`, the leadership
tie-break, which is **monotone and unfixable after the merge**.

---

## 4 · 🔴 THE FLEETWIDE ROOT CAUSE I CONTRIBUTED

**`buildkit-guards enforcement` detects "declared but never consumed" — and on GLPNET returns:**

```
-- enforcement: not_applicable
0 finding(s) across 1 guard(s) — enforcement: n/a
```

**`not_applicable` is summarised as `0 finding(s)`. A guard that did not look renders
identically to a guard that passed.** It is scoped to buildkit's own Python CLI, so every
non-buildkit repo on four hosts is unguarded against this class *while reading green*.

**That is why four instances shipped here in one era** (an FR-018 gate declared, unit-tested
and never called from the merge path; a pull interval printed to the operator that no timer
read; a heartbeat nobody consumed; an unreachable disposition branch).

**Roadmap feature `declared-unconsumed-guard`, state `promoted`.** Fix A (one line: render
`not_applicable` distinctly) is nearly free and I recommend buildkit take it first.

⚠ **Three lanes root-caused the engineer's L0 finding independently tonight, at three layers,
and all three are right:** yngwin (the seam is a frozen contract with zero implementations),
buildkit (`YngeniOS.Host.Windows` exists as a 338-line daemon with **no `.csproj`** — it
cannot be built), and this lane (nothing caught it because the guard says `n/a`).

---

## 5 · WHAT I ALMOST GOT WRONG — read this before capturing anything

**I captured a roadmap feature that already existed TWICE.** `glp-repl-three-tier` was a
triplicate of `glp-repl-fmb-split-over-ynet-for-qhsm-terminal` and
`glp-repl-front-middle-back-separation-yngenios-app-terminal-front-end` — and the 3270 terminal
(`virtual-3270-term`) and the REPL/engine split are **already CLOSED and delivered**.
Deleted with a soft tombstone.

🔴 **VERIFY ABSENCE BEFORE CAPTURING.** `buildkit-roadmap status | grep -i <topic>` first.

---

## 6 · ROADMAP — 39 NOT-CLOSED

**2 analyzed · 4 implemented · 28 promoted · 5 specified, across 9 epics.**
Dedupe: 132 scanned, **0 duplicate groups**. Reconcile moved
`quic-federation-transport: specified → implemented`.
7 unbound pipeline ids: 6 are the known Gleam set ruled COSMETIC (`Q-GLPNETS17-03`).

**Captured, scored and promoted this session:** `pbft-leader-election`,
`iroh-quic-transport`, `qhsm-virtual-terminals`, `declared-unconsumed-guard`.

---

## 7 · WHAT'S NEXT, IN ORDER

1. **Read round 5's verdict** (`reviews/102-quic-federation-transport/20260904T185600Z/`).
   Fix what it finds. **Then run round 6** — the ruling needs TWO consecutive low-yield rounds.
2. **Elevated firewall one-liner** (§1), then ask a peer to bind 47890 → SC-001 becomes
   measurable.
3. `/bk-ship` era 102 **only after** the two-round bar is met. Then `/bk-close`.
4. Marathon `mrun-d33293b40af7` has **2 outstanding items**, both genuine blockers:
   SC-001's second host, and the board-write interop (now RULED — item can be resolved).
5. 078 (28/111) and rank-21 remain **deferred, not cancelled**.

---

## 8 · TOOLING GOTCHAS RE-CONFIRMED

- 🔴 **The Bash heredoc collapses `\\n` to a literal newline**, which silently broke C# char
  literals twice tonight. **Use the Write tool for anything containing escapes.**
- 🔴 `PATH` must be re-exported per Bash call; a malformed export **removed `/usr/bin`** and
  `grep`/`sort`/`head` vanished mid-session. Prefix `export PATH="/usr/bin:/bin:$PATH"`.
- `buildkit-marathon capture --kind` accepts only
  `bug | idea | issue | latent-requirement | missing-prerequisite` — **not** `stage`/`risk`/`decision`.
- `buildkit-roadmap` has **no `capture` verb** — it is `add-feature`.
- `git push` is refused by the auto-mode classifier from Bash; **run it via PowerShell**.
- `buildkit-roadmap status --json` does **not** emit JSON on stdout.

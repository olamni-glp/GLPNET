# GAVRI — SAFE-RESTART RUNBOOK (meshtest-securering)

**Written 2026-07-18. Authoritative resume plan. Trigger phrase: `resume meshtest`.**

This file is the DETAIL. The auto-loaded memory
(`C:\Users\gavri\.claude\projects\D--bstdev-research-olamnit\memory\meshtest-securering-marathon.md`)
holds the trigger + compressed state and points here. One authoritative copy — do not fork this file.

---

## 0. STATE AT CHECKPOINT (all VERIFIED 2026-07-18 unless labelled)

| Thing | State | Verified how |
|---|---|---|
| olamnit repo | branch `023-android-quick-link-endpoints`, **clean**, tip `f7cbada` | `git status`/`log` |
| olamnit `develop` | tip = `02bcc20` (= olamnit's adjudication pin) | fetched this session |
| yngenios KV repo | branch **`003-yx-code-distill`** (NOT kv-durable), **clean** | `git status` |
| `spike/kv-durable` + pin `85cad74` | **both still exist** → switch is safe | `git branch -a`, `cat-file -t` |
| gleam | **on PATH** at `/c/Users/gavri/.local/bin/gleam` (memory's stale-PATH note no longer bites for gleam) | `command -v` |
| erl | at `C:/Program Files/Erlang OTP/bin/erl.exe` — **may still need PATH prepend** | `ls` |
| **handsets** | **NOT ATTACHED** — `adb devices` returns an EMPTY list | `adb devices -l` |
| COOP volume | uncommitted on BOTH sides (my seq 18 + 7 docs; olamnit's 3rtask-runs/ etc.) — **established convention, files live on my local D:** | `git status -- COOP` |
| Mailbox | I am at **seq 25** (rulings + gap-2 design, posted 2026-07-19); olamnit at **seq 23**, with **24 still reserved/pending** from them. I deliberately took 25 so as not to consume their reserved 24. | `grep '^seq:'` both files |

**Restart safety:** a session restart loses **nothing** — every artifact above is on local disk.
The only durability gap is disk-loss (COOP uncommitted). Pre-existing + shared convention; flagged, not fixed.

---

## 1. THE ORDERED PLAN (operator-set, 2026-07-18)

### STEP 1a — seq-15 device facts ⛔ **HARDWARE-GATED, DO NOT BURN TIME ON IT**
**`adb devices` is EMPTY as of the checkpoint.** This step cannot start until the operator physically
attaches the phone + tablet by USB. Per `[[android-dogfood-deploy]]`: **USB mode must be "File transfer",
not charge-only** — charge-only presents no device and looks identical to "not plugged in".

On resume: run `adb devices -l` **first**. If empty → **say so, hold 1a, go straight to 1b/2.**
Do not stall the session waiting for hardware.

When devices ARE attached, collect and post to olamnit (owed since seq 15):
- `adb devices` — both serials (expect `R5CW72ENHQB` = SM-S901B phone, `R8YY914822W` = SM-X130 tablet)
- handset **Wi-Fi IPs** (expect phone `.100`, tablet `.34` — re-verify, don't assume) + **BD addresses**
  (phone `48:BC:E1:67:62:D7`, tablet `4C:39:46:12:CD:3E`) + `bluetooth_on` state
- `arp -a` from `.108` — **identify the unknowns `.13 / .85 / .97 / .99`** (this is the actual ask)
- tablet↔phone **bonded** (pairing state)
- my **Ed25519 ring pubkey**: `ZDJQPHY+5zKS5eotyy24eoQgIFbUn3e3aZGRWXozrRE=`
  (seed at `C:\Users\gavri\.olamnit-ring\gavri-ring-seed.bin`, mode 0600, **never transmit the seed**)

### STEP 1b — seq-13 KV kill-9 acceptance ✅ ACTIONABLE NOW
Repo `D:\bstdev\research\yngenios` is on `003-yx-code-distill`, clean. Sequence:
1. `git -C D:/bstdev/research/yngenios checkout spike/kv-durable` (verify tip = `85cad74`)
2. prepend erl to PATH if `erl` doesn't resolve; confirm `gleam build` green **before** claiming anything
3. bring the node up; **2106-key reseed** (backup is sha256-verified per memory)
4. **kill-9 acceptance** + take my own backup (two hosts, two copies — olamnit verifies from `.129`)

**[[no-verification-theater]] applies hard here** — this is the exact scenario that produced the original
violation. Do not report a KV result I did not run. If the runtime won't come up, say `UNVERIFIABLE-HERE`.

### STEP 2 — close the last INFERRED label ✅ ACTIONABLE NOW, FULLY UNBLOCKED
**Claim to promote:** "`MeshRoutingTests` conservation would FAIL if it settled past TTL — a delivered
message is also counted Dropped at its origin."
Currently **INFERRED**. Structural basis (VERIFIED at `02bcc20`): `MeshNodeRuntime._pending` has exactly
3 mutation sites — decl `:61`, insert-on-send `:215`, remove **only** in the TTL-expiry branch `:375`;
`StageExpiredToPartitionQueue` increments `_dropped` unconditionally.

**Method — lower the TTL, don't lengthen the wait** (5 s waits make this slow and flaky):
`MeshRuntimeOptions.MessageTtl` is an `init` property → construct a runtime with e.g. `MessageTtl = 300ms`,
settle ~1 s, then assert on `Originated / Delivered / Dropped`. Expect `deliv + drop > orig`
(the shipped test asserts `Assert.Equal(0, drop)` after a **150 ms** settle against the **5 s** default —
it passes only by finishing ~33× inside the window).

- Test file: `Olamnit/Olamnit.Kernel.Tests/Mesh/MeshRoutingTests.cs` (present on `023`)
- Run: `dotnet test Olamnit/Olamnit.Kernel.Tests/Olamnit.Kernel.Tests.csproj --filter Mesh`
- **Write a NEW test; do not edit the shipped one.** It is olamnit's CI gate.
- Outcome either way is REPORTABLE: confirms → RL-1 case is proven, tell olamnit; refutes → **I was wrong,
  say so loudly** and correct seq 18.

### STEP 3 — marathon `mrun-e8c0d6b8a851` (gavri-only items)
Per olamnit seq 23 §1: **`mrun-d7dde183107f` (olamnit's) is authoritative for SHARED items**; mine is
gavri-only. **Do NOT mirror shared items into mine — double-tracking IS the drift.**

⚠️ Buildkit catalog is Py3.14-slow/flaky; `marathon` ops worked last session but `3rtask list` /
`backlog status` hang. **git + these COOP files are the record** (pre-authorized fallback).

---

## 2. "UNTIL COMPLETION" — THE HONEST STOP LINE

**The marathon CANNOT reach completion on my side alone.** State this to the operator rather than
grinding. Hard blockers, none of them mine to rule:

| Blocker | Owner | Effect |
|---|---|---|
| **STOP** on OE-4-as-specified + corroborator | olamnit (standing) | do not build either |
| ~~**M-29**~~ | ✅ **RULED 2026-07-19** (operator-delegated) | **DISCHARGED.** YES as gate-1 proof (already ships); **NO** as gate-2 quorum; never both. Relay auto-mint OFF pending C1–C4. Production **fail-closed today** (`TrustGraph` never populated) ⇒ preventive, not urgent. See `rulings-m29-m34.md` |
| ~~**M-34**~~ | ✅ **RULED 2026-07-19** (operator-delegated) | **DISCHARGED. de-facto L1, NOT a straddle. Handset node-agent build UNBLOCKED.** L1 exists: `Olamnit.Yngenios.Host` (plain `net10.0`, referenced by MAUI head AND Web daemon, already refs `Olamnit.Coin`) |
| **E-A / E-B** | olamnit | gates the meaningful 4-node kill-one soak |
| **seq 24** (5 blind Builders vs `02bcc20`) | olamnit, in flight | may refute current framings — standing rule: **neither of us acts on an unadjudicated framing** |

**STATUS 2026-07-19 — everything solo-reachable is DONE:** step 1b ✅ PASS, step 2 ✅ (half confirmed
by measurement / half refuted + withdrawn), M-29 ✅ RULED, M-34 ✅ RULED, gap-2 design ✅ WRITTEN
(`gap2-ring-optimizer-design.md`). All posted as **seq 25**.

**Still NOT reachable solo:** E-A/E-B + seq 24 (olamnit's), 1M soak (needs the bench), on-device
anything (`adb devices` still EMPTY).

**Node-agent build: now UNBLOCKED by the M-34 ruling, but DELIBERATELY NOT STARTED.** Three reasons,
all standing: (1) verification is hardware-gated — building it now yields code that cannot be run on
a handset; (2) it is a large greenfield build with no spec and no branch, and the working tree is on
`023-android-quick-link-endpoints` mid-feature (US4 pending) — starting it there pollutes an
unrelated branch; (3) the honest sequencing is the gap-2 bench finding: build the non-adjacent bench
first, because a fully-adjacent topology cannot exercise the relay path at all. **Do not read
"unblocked" as "started".**

**Gap 2 (ring optimizer) is REFRAMED — do not build to the old plan:**
- **M-26** — the DV frame has **no path field**; an elected "tour" the transport won't follow is
  meaningless. The election must **STEER THE ROUTE TABLE**. **Never pin a path into the frame**
  (frozen wire surface + source-routing attack surface).
  Must-state consequence: adjacent-neighbour hops may take the deliver-local branch ⇒ the multi-hop
  relay path may never execute ⇒ such a soak proves link-transport + dedup, **not** mesh routing.
- **M-27** — PBFT around the optimizer does **not** make it Byzantine-safe. `LinkCostInputs` is
  **self-reported** by possibly-hostile nodes. **Bound the contribution or state plainly that I haven't.**
  Cost it as **genuinely-NEW** — largest new build on either list.
- **M-24 / gap 1** — my additive, epoch-scoped, never-mutate-the-anchor exclusion set is **AGREED + FROZEN**;
  exclusion lives in the election's durable decision log; **ELECTED membership, not anchor membership, is
  authoritative for quorum**. But rooting it in epoch genesis **inverts into the attack it prevents**
  (induction with no base case ⇒ attacker declares genesis={itself}, quorum=1 forever).
  **Take olamnit's close VERBATIM, do not re-derive:** genesis manifest enumerates founder pubkeys = epoch 0;
  **signed by ALL N founders**; **every node rejects a genesis it did not itself sign**; `genesis_hash` :=
  hash of canonical body **EXCLUDING** run id, `run_id := genesis_hash`; every transition/exclusion binds
  `genesis_hash`; two manifests with the same `run_id` = **INTEGRITY FAILURE → HALT** (never merge, never
  resolve by recency); operator **run NONCE** covered by signatures ⇒ replay detectable by construction;
  threshold = the epoch's **FIXED N**, never "who is live now".

---

## 3. MAILBOX DISCIPLINE ON RESUME

1. **Read `COOP/olamnit/handoff.md` FIRST** — check `grep -n -m1 '^seq:'`. If **seq ≥ 24**, it supersedes
   much of the above; fold it before acting.
2. **Never trust a stale seq from memory.** Memory said "my seq 14" when the file said **17**. `grep` the file.
3. **`git fetch` before doubting a peer's pin.** I nearly accused olamnit of a wrong-ref read on `02bcc20`;
   it was the tip of `develop` and *my clone* was stale. A fetch was the difference between corroboration
   and accusation.
4. Absence claims carry **(ref, scope, vocabulary)** — my seq-18 refinement to olamnit's frozen method:
   scope and **ref** are different axes; a wildcard Critic on a stale ref reproduces the defect at full
   confidence.
5. No reply is owed from me until olamnit acks seq 25 or posts seq 24. **When 24 lands, the FIRST
   thing to do is re-check the "what would refute this" sections at the end of `rulings-m29-m34.md`
   and `gap2-ring-optimizer-design.md`** — both were written before 24 at operator instruction, and
   each names the specific findings that reopen it.

<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART BRIEF — `olamnit.glpnet` · run `mrun-b7b5fa047190` · **rev 7** · 2026-09-07

**Resume with:** `resume marathon`
**Host:** OLAMNIT · **Branch:** `develop` (109 is SHIPPED — no feature branch to return to)
**Supersedes `RESTART-mrun-b7b5fa047190-olamnit-glpnet-20260906-rev6.md`.**
Trust `git log --oneline -1` over any hash written here.

---

## 0 · WHAT SESSION 15 DID

**Feature 109 SHIPPED and RELEASED as `v2026.09.06.5`** — PR #322 (feature→develop) → #323
(release→main) → #324 (back-merge), all merged, tag verified on origin. All nine pipeline stages
ran. Roadmap: `released`.

| # | delivered | evidence |
|---|---|---|
| 1 | **109 US1** — the differential harness | suite `Section Y`; `scripts/differential_gate.py`; 595 → **604/604 executed, 0 failures** |
| 2 | **T058 executed reversion** — the real C# fix reverted, rebuilt, measured DIVERGE, restored, measured AGREE | `.specify/differential/reversion-20260906.md` |
| 3 | **`/bk-codexreview`: 21 findings, 6 high — ALL fixed, no deferrals** | `scripts/tests` 63 → **118** |
| 4 | **Four engineer rulings** taken via `AskUserQuestion` | §5 |
| 5 | **P0 broadcast: 6 of 8 PBFT members equivocate in term 3** | broadcast `20260906T2145Z` |
| 6 | **P0 broadcast: OB-8's verify step is broken on Windows** | broadcast `20260907T0010Z` |
| 7 | Board: 67 not-closed, **0 captured, 0 refined** — every row scored and promoted or beyond | `scripts/roadmap_open_table.py` |

---

## 1 · 🔴 THE TWO FINDINGS TO CARRY FORWARD

### 1.1 Six of eight PBFT members equivocate, and the tally hides it

Measured 2026-09-06T21:38Z against `D:\coop\ynet` (113 pbft records, 0 quarantined) on **two
engines** — `origin/develop@3b10b85f` and `547a3fc0` — with identical results.

Term 3: **16 prepares from 8 actors → 8 counted, 8 silently dropped, `discarded: {}`.** Six of the
eight actors prepared for **two different candidates in the same term**. The rule is
`prepares.setdefault(...)` over timestamp-ascending records — **first-prepare-wins, written down
nowhere.** Under last-wins the same records give `QuorumUnattainable` instead of `Decided`.

**ENGINEER RULING TAKEN: discard the equivocating actor for that term, and report every drop.**
Consistent with `OB-5`/`Q99=a` (discard the vote, never void the term). **Owner
`@shiras-olamnit`** (`tools/ynet`, `Q59`) — this lane raises, does not patch.

### 1.2 OB-8's verify step reports DIFFERS on a byte-identical file, on every Windows host

Measured 2026-09-07T00:05Z on this repo's copy of the ruled template:

```
worktree 38983 bytes  sha a23f7be9…      |  483 lines, 483 CRs, delta exactly 483
committed 38500 bytes sha 528611d722e269ac  <- matches OB-8's figure for GLPNET exactly
sha256(worktree | tr -d '\r') = 528611d722e269ac   IDENTICAL
```

**OB-8's numbers are right.** But its remedy step (b) — *"every lane verifies and reports
MATCHES/DIFFERS"* — hashes the CHECKOUT, which is CRLF on Windows. Three of four hosts are
Windows. **Run it as written and the fleet manufactures the fork it is trying to measure.**

Fix, one line: `git show HEAD:<path> | sha256sum` (the stored object, LF everywhere), or
`tr -d '\r' < <path> | sha256sum`.

---

## 2 · ✋ THREE THINGS THIS LANE GOT WRONG AND CORRECTED ITSELF

1. **I authored an 8th plan document.** `FLEETWIDE-TACTICAL-ACTION-PLAN v5.0` was broadcast at
   22:35Z **before I had read OB-8**, which forbids exactly that until remedy step (a) lands.
   **Stood down as a rival document** at 00:10Z. The *mechanism* (`docs/fleet/plan/plan_crdt.py` —
   grow-only per-actor op log, add-wins ACKs, actor-mismatch refusal, a losslessness `check` that
   exits 2 and caught a real loss on its first run) is offered to the ruled stream
   `docs/fleet/ftap/ftap.crdt.jsonl`; the text is not.
2. **My quorum denominator was wrong.** I told the engineer the roster is 15 lanes and recommended
   11/15. **`Q80=a` rules it 60 — 4 hosts × 15 — with the bar ≥45.** The engineer's 45 was right.
   The answer he gave rests on my malformed question and this lane is not acting on it.
3. **OB-9 applies to me.** v5.0 claimed losslessness against a directive stored nowhere. **An
   unstored source makes every losslessness claim unverifiable by construction**, mine included.

---

## 3 · 🔴 THE DEFECT CLASS THIS SESSION KEEPS FINDING — grep your own suite for it

Three of the six new Section Y checks used a success sentinel that is a **substring of their own
failure string**: `grep -q AGREE` matches `DISAGREE`; `ACCOUNTED` matches `UNACCOUNTED`;
`CONSISTENT` matches `INCONSISTENT`. All three passed unconditionally. `X-4`, inherited from 108,
had it too.

**And V-26 — the regression control I wrote for this feature's own freshness fix — compared a value
against the mtime of a file INSIDE the directory that value is computed over.** It held by
construction and could not fail in any state, including the exact reversion its comment claimed it
would catch.

The fix that removes the class rather than the instances is **`check_exact`** (equality), not five
renamed sentinels. **Grep every suite in the fleet for a `check`-style helper whose success token is
a prefix of its failure token.**

Related, and separately broadcast: a build-freshness gate that stats `glp_repl.exe` measures the age
of a **.NET apphost stub an incremental build does not rewrite**. Date a build from the newest file
in its **output directory**.

---

## 4 · WHAT IS **NOT** DONE, AND WHY

| item | state | reason |
|---|---|---|
| **SC-003 — a live refusal in an adopted area** | **NOT MET, disclosed in `spec.md`** | The only always-non-conforming surface sits in area `coop`, declared **non-adopted**. Flipping it would assert an adoption this lane has not performed. Carried as roadmap feature `sc003-live-refusal-in-an-adopted-area` (WSJF 5.33). |
| **The audit widening** | **NOT DONE, disclosed in `spec.md`** | `scoped_regions` is still byte-identical to `develop` — five regions. ~477 sites need ~477 dispositions at once, and defaulting them is how 25 surfaces came to claim `owned` falsely. Carried as `audit-widening-codeconv-and-remaining-csharp` (WSJF 3.00). |
| **Feature 110 `[03]` YQuery/DuckLake** | **NOT STARTED, BLOCKED** | Its conformance evidence must be measured against a real Postgres node (`Q-olg17-04`). `com.docker.service` is **Stopped (Manual)** and `Olamnit\smbuser` is **not in `docker-users`** (only `Olamnit\gavri`). **Both need administrator rights.** OLAMNIT is **dormant, not bare**: 26.7 GB of PG18 Docker data survives at `D:\pgdata\pg-node-{a,b}` — **assess those two clusters, do not provision over them.** |
| **`/bk-close` retrospective for 109** | **NOT RUN** | Ship and release completed; the close-out retrospective did not. Run `buildkit-close` (or `/bk-close`) against `differential-cross-runtime-acceptance-gate` first thing. |

🔴 **All four are DISCLOSED, not silent.** The standing peer ruling (`shiras-tefl`,
2026-09-04T23:55Z) is that a disclosed gap is not cheating; concealment is.

---

## 5 · ENGINEER RULINGS FROM THIS SESSION (`AskUserQuestion`, all four answered)

| ruling | decision |
|---|---|
| **109 disposition** | **Ship with both gaps disclosed**, each opened as its own roadmap feature — done |
| **`declared-unproven`** | **Ratified as a fourth tier; FR-019 amended** — done, `7b6fd6ec` |
| **Plan quorum** | answered 11/15 — **NOT acted on; the question was malformed (see §2.2)** |
| **PBFT equivocation** | **Discard the equivocating actor for that term, and report the drop** — for `@shiras-olamnit` |

---

## 6 · WHAT'S NEXT, IN ORDER

1. **`git fetch origin` FIRST** (`C-19`). Several lanes push this repo; two peer tags landed
   mid-session and a second `specs/109-*` directory arrived on develop.
2. **`/bk-close` feature 109** — the one pipeline stage that did not run.
3. **Next single-feature era — RUN `buildkit-roadmap next` AND TAKE WHAT IT SAYS.**

   🔴 **Corrected 2026-09-07T07:1xZ, and the correction is against THIS FILE.** An earlier revision
   of §6 named `cross-runtime-link-parity-intermittent-empty-list` as "next" **on raw WSJF**. That
   is the score column, not the recommendation, and `@gavriella-glpnet` published exactly this
   correction at 01:15Z (`2e60c9ec`) after making the same mistake. Re-deriving "next" from the
   score column is what produces the discrepancy.

   **Measured here 2026-09-07T07:1xZ:**
   `buildkit-roadmap next` → **`per-host-toolchain-and-environment-contract-declared-machine-checked-loudly-refused`** (rank 24).
   It applies dependency and build-order, not raw WSJF. **That is the authoritative recommendation.**

   The raw-WSJF-top unbuilt row is a *different* feature —
   `ynet-frame-field-parity-across-planes` (WSJF 10.50 / RICE 80750) — and the two disagreeing is
   the normal case, not a fault.

   **Carried as a CANDIDATE, not a recommendation:**
   `cross-runtime-link-parity-intermittent-empty-list` (WSJF 5.00, RICE 28800) — a C# consumer
   returns `Got = []` and prints `succeeds`, green in 2 runs of 3. **It is 109's vacuous-agreement
   defect at link level**, so this lane's proven method transfers directly, and its regression bar
   is an **ITERATED** run (≥20), never a single green. Offer it to the engineer if the tool's pick
   is reallocated elsewhere; do not substitute it silently.
   Also open: `sc003-live-refusal-in-an-adopted-area` (WSJF 5.33), 109's own disclosed gap.
4. **Ask the engineer for the two administrator actions** if 110 `[03]` is wanted: add
   `Olamnit\smbuser` to `docker-users`, and start `com.docker.service`.
5. **Re-ask `@gavriella-glpnet` for the literal `space_id`** (`Q-olg15-04`: do not mint one).

---

## 7 · STANDING RULINGS AND ENVIRONMENT

- **`Q-olg15-09`** 108 is ONE sibling to 078; **do NOT re-open 078.** FR-013's extraction is a
  behaviour-identical move. 🟡 *One declared exception this session:* `record()` gained a `now`
  parameter mirroring `applies()`, so the new "expiry must be in the future" check works under a
  pinned clock. Additive; two 078 fixtures updated; 79 faultinj tests green.
- **`Q-glpnetshiras-50`** `YngeniOS.Ynet.Client` is canonical; this lane authors no client.
- **`Q59`** `tools/ynet` is `@shiras-olamnit`'s. **`R-S5-04`** `[04]` is `@shiras-glpnet`'s.
- **`Q80=a`** fleet roster is **60** (4 × 15); quorum bar ≥45. **Not 15.**
- 🔴 **The classifier is intermittent. RETRY BEFORE ESCALATING.** Confirmed across five sessions.
- 🔴 **Heredocs mangle escapes in this shell** — it bit twice more this session and broke a
  broadcast. **Write patch scripts and long documents with the Write tool.**
- 🔴 **Never read `$?` through a pipe.** Both the audit and the differential gate warn when stdout
  is not a terminal. Run bare.
- `dotnet` at `C:\Users\smbuser\AppData\Local\Microsoft\dotnet`, **not on PATH.**
- `DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart.exe`, **not on PATH** — export before the suite.
- Use `codeconv/.venv/Scripts/python.exe` for repo scripts; **`scripts/roadmap_open_table.py` needs
  the buildkit venv** `/d/bstdev/research/buildkit/.venv313/Scripts/python.exe` (it imports
  `buildkit_cli`).
- 🔴 **Rebuild the Debug C# REPL** before trusting the suite:
  `dotnet build out/csharp/glp_repl/glp_repl.csproj -c Debug`.
- Coop: `/d/coop`, 47 channels, written three times this session.

---

## 8 · RESTART CHECKLIST

1. `resume marathon`
2. `git fetch origin --tags` — expect movement; several lanes push this repo.
3. `git checkout develop && git pull --ff-only` — **109 is shipped; there is no feature branch.**
4. `buildkit-marathon status --feature differential-cross-runtime-acceptance-gate`
   (run `mrun-b7b5fa047190`).
5. Read **§4** (what is NOT done), **§1** (the two P0s), **§2** (what this lane got wrong).
6. Rebuild the Debug C# REPL, then run the suite bare.

---

## 9 · BASELINE

| | session start | session end |
|---|---|---|
| REPL suite | 595/595 executed, 0 fail, 2 named not-run | **604/604 executed, 0 fail**, same 2 named not-run |
| `scripts/tests` | 63 | **118** |
| codeconv faultinj | — | **79 passed** |
| evidence-signal audit | exit 1 · 7/7 checks · 0 errors | exit 1 · **9/9 checks** · 0 errors · 0 refusals |
| differential gate | did not exist | **1 criterion MEASURED-AGREE**, exit 0, control executed |
| board (not closed) | 55 | **67 · 0 captured · 0 refined** |
| git | 3 unpushed commits | **clean, 0 ahead, 0 behind** |

---

## 10 · THREE THINGS THE FINAL PRE-RESTART CHECK FOUND (2026-09-07T07:1xZ)

Recorded because each would have cost the next session time, and none was visible from the summary.

1. 🔴 **`.specify/feature.json` on THIS HOST pointed at a FINISHED feature.** It read
   `specs/108-evidence-signal-ordering`, stage `implemented`, while the roadmap has 108
   **`released`**. The CLAUDE.md Restart-Resume order consults that pointer at step 2, so the next
   session would have been sent into a completed era.
   **The file is GITIGNORED — it is PER-MACHINE state.** `@gavriella-glpnet` cleared their copy at
   01:12Z and that clear cannot reach OLAMNIT, by construction. **Cleared here to `{}`; verified
   `buildkit-roadmap next` now answers.** Every host must clear its own; a peer's fix is not yours.

2. ⚠ **There are TWO restart pointers in this repo, for two different lanes.**
   `docs/RESTART.md` is **`gavriella.glpnet` @ GAVRIELLA**. **This** file is `olamnit.glpnet` @
   OLAMNIT. Neither is wrong; a session that reads the other one is. **On OLAMNIT, this file is
   the pointer** — check the host name in the header before trusting either.

3. ⚠ **`develop` moved during the final verification** (`behind=1`, `2e60c9ec`). Fast-forwarded
   before signalling. This is why `C-19` says fetch at the START of era work: two lanes push this
   repo and a "clean, 0/0" measured five minutes ago is not a fact about now.

---

## 11 · 🔴 THE TWO CHANNELS — COOP IS NOT YNET. READ THIS BEFORE SENDING ANYTHING.

**Engineer directive, 2026-09-07:** *"COOP is a file-based DROP BOX. YNET is kernel realtime
QHSM/QMSM messaging. They are NOT the same channel."*

🔴 **This lane conflated them for a full day.** Every broadcast before 08:30Z went out **only over
COOP** while being described as reaching "the fleet". Corrected and disclosed, not quietly fixed.

### COOP — the file-based drop box

```
python scripts/coop_broadcast.py <file.md> --root 'D:\coop' --also-root
```

Refuses to overwrite an existing destination and refuses an over-long path, **writing nothing on
refusal**. Emits a REUSE `.license` sidecar (the fleet licence gate rejects a bare `.md`). Channels
are enumerated, never hand-listed. Filename convention `<UTC>-<host>-<lane>-<SUBJECT>.md`.
Measured 2026-09-07: **47 channels written, 0 refused.**

🔴 **Heredocs break these files.** Write the message with the Write tool, then fan it out.

### YNET — kernel realtime QHSM/QMSM messaging

Canonical client (`Q-glpnetshiras-50` — **this lane authors none**):
`%LOCALAPPDATA%\yngenios\ynet-client\eea87e02\ynet-client.exe`

```
ynet-client run    --lane olamnit.glpnet --node olamnit --coop 'D:\coop' [--seconds S]
ynet-client send   --lane olamnit.glpnet --node olamnit --coop 'D:\coop' \
                   --to <node>/<lane> --signal <SIGNAL> --body "<text>"
ynet-client doctor --lane olamnit.glpnet --node olamnit --coop 'D:\coop' --json
ynet-client alerts --lane olamnit.glpnet     # then: ynet-client ack <ID> --lane olamnit.glpnet
ynet-client peers  --coop 'D:\coop'          # run BARE - piping makes $? the PIPE's status
```

- **`--to` needs the FULL origin `<node>/<lane>`.** A bare lane name is refused, exit 1.
- **`--node` is required** on `run`/`send`/`doctor`; omitting it exits 2.
- Measured 2026-09-07: **8 sends, 8 accepted, 0 refused.**

### 🔴 Three measured facts that will mislead you if you do not know them

1. **`doctor` reports `carrier: "(none)"` when NO RECEIVER IS RUNNING.** The field describes the
   *running receiver*, not the configured transport. `run` on the same host, same build, minutes
   later prints `carrier=CoopFileCarrier`. **Do not conclude "this host has no carrier" from
   `doctor` alone** — start a receiver and read its banner. Raised to `@ariellas-qhstate`.
2. **The two channels share ONE physical carrier today.** `CoopFileCarrier`, rooted at `D:\coop`.
   The client's whole surface has **no `--quic`, `--host`, `--port` or `--peer-addr`**, so no lane
   can address `yng-broker`'s live QUIC socket on `*:24601`. **A YNET send is NOT realtime today.**
   Corroborated on OLAMNIT after `@shiras-crucible` (08:00Z) and `@shiras-yngwin` (06:50Z).
   Consequence: the engineer's **2-minute mailbox liveness check is a BUILD, not a config** — it
   would be a file poll described as realtime until a QUIC carrier exists.
3. **NEVER count peers from the channel directory.** It decodes to **87 distinct origins**
   including probes and deliberately invented lanes (`totally.invented.lane.9999`). It is not a
   roster, and any quorum or reachability figure derived from it is wrong.

### One live defect in this lane's own addressing

**Two inboxes exist for this lane, under two spellings:** `olamnit/olamnit.glpnet` (dot — what the
client listens on) and `olamnit/olamnit-glpnet` (hyphen — **6 files sitting unread in it**). ERA
096's `--legacy-id` is the intended remedy. Drain the hyphen inbox before assuming nothing was
missed.

---


> 🔴 **§12 BELOW IS SUPERSEDED — read §13 first.** It records the YNET root cause as "a missing
> credential". That was measured on the wrong tree and is no longer the live diagnosis. The chain
> was corrected four times over 2026-09-07; §13 carries the current answer and the full lineage.
> §12 is kept rather than rewritten, because a reader who saw the earlier broadcasts needs to be
> able to find what changed.

## 12 · 🔴 THE YNET ROOT CAUSE — a MISSING CREDENTIAL, not a missing transport

**Do not repeat the fleet's error, or mine.** On 2026-09-07 the fleet — this lane included —
concluded from `carrier=CoopFileCarrier` that *"YNET is COOP today, the wire was never wired"*.
**That is wrong, and I broadcast it twice before checking my own repo.**

### What is actually in the tree

```
csharp/ynet_client/Client/QuicCarrier.cs        the WIRE plane      EXISTS
csharp/ynet_client/Client/CoopFileCarrier.cs    the FILE plane
csharp/ynet_client/Client/PlaneCatalog.cs       the registry
csharp/ynet_client/Client/PlaneSelection.cs     the chooser
csharp/ynet_transport/Listener/YnetListenerService.cs   named-service bind, EXISTS
```

`YnetListenerService` binds the **routable `IPEndPoint`** overload for `yng-broker`/`yng-guardian`/
`oracle`/`admin`, and `BindAndVerifyAsync` proves reachability with **a full handshake plus a
bidirectional byte exchange**, never a bare bind. WP02's B3 is **closed in code**.

The "QUIC has zero consumers" defect was **found and fixed by this repo on 2026-09-06** —
`PlaneCatalog` exists so that `PlaneSelection` can only construct a plane *through* it, making
registration imply reachability rather than assert it.

### The actual cause

`PlaneSelection.Bind` performs a **ruled wire → file fallback** (`Q-G34-02 → C`), because this host
has had its QUIC certificate material destroyed **four separate times** and a client that refused to
start would leave the host with no receiver at all.

**So `carrier=CoopFileCarrier` means THE WIRE FAILED TO BIND AND THE CLIENT DEGRADED.** Measured
here 2026-09-07T09:2xZ:

```
glpquick-cert/
  glpquick.macaroon.key   44 bytes   present
  glpquick.pfx                       ABSENT      <- the QUIC certificate material
```

**The same absence makes the REPL suite report Section T `unsearchable`.** The transport is fine;
the credential is not there.

### Why nobody caught it — and it is this session's recurring class, again

**The fallback is SILENT in the deployed build's banner.** It prints the carrier and never the word
*degraded*. A true field that omits one word turned a credential problem into a fleet-wide hunt for
a missing transport. Raised to `@ariellas-qhstate` (canonical client): the banner and `doctor` must
both say *degraded, and why*.

### What to do next session

1. `ls glpquick-cert/` — if `glpquick.pfx` is absent, the wire plane cannot bind here.
2. Provisioning it is the unblock for the engineer's 2-minute realtime liveness directive. **Make it
   durable and re-runnable** — the ruling records it destroyed four times already.
3. Only then re-measure the carrier, and only then the coordinator actors.

---

## 13 · 🔴 THE YNET DIAGNOSIS AS IT NOW STANDS — and it changed FOUR times in one day

Read this instead of §12. Every step below was measured, and each one corrected the step before it.
**The lineage is kept deliberately: four confident, broadcast, wrong answers in a row is the
finding, not an embarrassment to tidy away.**

| # | claim | verdict | why it was wrong |
|---|---|---|---|
| 1 | "the QUIC transport has zero consumers, the wire was never wired" | **RETRACTED** | `QuicCarrier`, `PlaneCatalog`, `PlaneSelection`, `YnetListenerService` all exist; the zero-consumer defect was found **and fixed** by this repo on 2026-09-06 |
| 2 | "`glpquick.pfx` is absent, so provision a certificate" | **RETRACTED** (`FR-21`) | **OLAMNIT has TWO GLPNET clones.** `D:\BSTDEV\glp\GLPNET\glpquick-cert\` is complete; this tree's was not. A copy, not an era |
| 3 | "the deployed client has no wire plane, so upgrade the client" | **partly right** (`FR-26`) | true, but my evidence probed `QuicCarrier` — a **file name**. The types are `QuicInbound`/`QuicOutbound` |
| 4 | "the fix is one line: pass `--self` into `Binding.Self`" | **RETRACTED** (`FR-32`) | `Binding.Self` is a `NodeIdentity` **signer**; `--self` is a **string**. Same name, different type |

> 🔴 **AMENDED 14:15Z — THE SECTION BELOW IS A *SOURCE-TREE / LOCAL-BUILD* FINDING.**
> The `Self = null` diagnosis is true of **this tree**, measured against a **locally built** client.
> **The DEPLOYED client (`eea87e02`) never gets that far:** it accepts `--plane wire`, **silently
> runs `carrier=CoopFileCarrier`, exits 0, and prints no notice at all**; `--self` is not even a
> flag there (`rc=2: --lane is required`).
> **So the deployable unblock is A CURRENT BUILD, not a certificate and not this one line.**
> And the certificate story is dead twice over: `QuicWireChannel.cs:66` **mints its own** ephemeral
> self-signed cert, and `glpquick` appears in **0 files** across `ynet_client` and `ynet_transport`
> (positive control: `Certificate` matches 1 file). Two lanes reached "provision the cert"; both
> were wrong.

### The current answer, isolated by a discriminating run

**`csharp/ynet_client/Program.cs:122` hardcodes `Self = null`**, with the comment *"supplied by
`--identity` in a later step; absent means the wire degrades"*. **`--identity` does not exist** —
zero hits across `csharp/`.

```
run --self olamnit/olamnit.glpnet --coop D:\coop --plane wire                        -> "needs this node's identity"
run --self olamnit/olamnit.glpnet --coop D:\coop --plane wire --listen 0.0.0.0:47890 -> "needs this node's identity"
```

Identical error with the listener supplied, because `NewWire` checks identity **before** listener.
**So `--listen` already parses, and identity is the sole client-side blocker.**
`NodeIdentity.LoadOrMint(lane)` exists (`NodeIdentityKeystore.cs:60`, `$YNET_NODE_KEYSTORE`) and
**mints on first use** — no key ceremony needed.

🔴 **RAISED, NOT PATCHED.** `csharp/ynet_client/` is the canonical client's tree
(`@ariellas-qhstate`, `Q-glpnetshiras-50`). **Do not patch it from this lane.**
🔴 **NOT CLAIMED: that a handshake then succeeds.** Nobody has seen one. Minting lets the wire be
*attempted*; the next error is what tells the fleet what is really next.

### Also live, from the same measurements

- **`--plane` / `YNET_CLIENT_PLANE` already exists** (`Program.cs:112`). Any lane can request the
  wire today. Nobody knew.
- **The deployed build is `ynet-client 1.0 (feature 093)`** and contains **none** of the plane types.
  A build from this tree contains all of them and compiles clean in ~3 s. **Every YNET behavioural
  claim must name its BUILD** (`FR-22`) — lanes reading source and lanes running old binaries
  contradicted each other all day.
- **Strays**: this lane's YNET inbox holds `.md` files a COOP fan-out dropped into a *mailbox*.
  Zero are from this lane's own tool. `@olamnit-ynglin` raised (`FR-29`).
- **The replay defect fires on every receiver start** — an acked P0 re-raised 8+ times with
  `arrived_utc` re-stamped each time (`FR-23`). Owner `@ariellas-qhstate`.

---

## 14 · WHAT THE LOOP IS DOING, AND WHAT IS BLOCKED

A `/loop` (cron `77aa1637`, every 10 min) runs: ack both channels → advance the liveness fix →
codify/roadmap → commit+push → re-verify restart safety.

**Feature on the board:** `ynet-coordinator-tiers-fleetwide-rollout` — created this session because
the CRDT requirements document pointed at a feature that **did not exist**. Promoted, WSJF 3.63 /
RICE 27000. *The WSJF understates the urgency because job size divides it; that was not gamed.*

**CRDT requirements doc** (contribute here, do NOT write a rival):
```
docs/fleet/crdt/YNET-COORD-TIERS-FR.crdt.jsonl
python scripts/crdt_requirements.py --log docs/fleet/crdt/YNET-COORD-TIERS-FR.crdt.jsonl
```
Append-only, single-writer per lane, `kind: clause|second|contest|open-question|answer`, supersede
by id, merge by set-ops, adoption at **≥2 lanes on ≥2 distinct hosts**.
🔴 **A `second` from a host that has not yet seconded is worth more than a new clause** — adoption
counts distinct hosts, so corroboration converges and assertion does not.
This lane holds `FR-15`…`FR-33` incl. **four self-supersessions**.

**BLOCKED, deliberately:** iroh work. The directive says *"do not start work until a plan and clear
non-conflicting allocations are agreed."* A plan exists (`@gavriella-glpnet`, 11:50Z; engineer
ruling 2026-09-04 staging sidecar → FFI/L0 → C#). **This lane claims NOTHING in A/B/D**, delivered
only item C (toolchain census: **cargo/rustc/rustup absent for `Olamnit\smbuser`** — item A must
not be scheduled here), and offered the acceptance-evidence slice to the seated leader
(`shiras.oracle@SHIRAS`) to allocate. **Awaiting arbitration. Start nothing until it arrives.**

🔴 **The coordination paradox, unresolved:** the directive says to use the host coordinators and
lane sub-coordinators to plan. **They do not exist** — they are the deliverable of the feature the
coordination is about. The leader does exist, so it is the only real addressee.

---

## 15 · METHOD RULES THIS LANE EARNED THE HARD WAY TODAY — apply them

Four wrong broadcasts in one day, each from a plausible inference. These are the rules that would
have caught them, and they are cheap:

1. **An ABSENCE claim about a binary needs a POSITIVE CONTROL** — and the control must be the
   **same KIND of symbol** as the probe (`FR-25`, `FR-27`). A control of a different kind proves the
   file is readable, not that the probe means anything.
2. **Probe the TYPE a file declares, never the file's name.** `QuicCarrier.cs` declares
   `QuicInbound`/`QuicOutbound`.
3. **Same-name is not same-type** (`FR-33`). Cite the declared type of both sides before claiming an
   assignment is possible.
4. **Never `strings` an `.exe` on .NET** — it is an apphost stub with no code. Use `grep -a` on the
   assembly.
5. **A COOP sweep must match the timestamp ANYWHERE in the filename**, not anchored at `^`. A
   `^2026` glob missed **315 files in one day**, including the P0 and the ERA claim that mattered
   (`FR-20`).
6. **`git push` fails transiently with `getaddrinfo() thread failed to start`** — it has succeeded
   on retry every single time today. **Retry before escalating.**
7. 🔴 **NEVER PIPE A COMMAND WHOSE EXIT CODE YOU ARE ABOUT TO BELIEVE.** `cmd | tail` makes `$?`
   **tail's** exit — it converts a refusal into a success. The ynet client's own banner warns of this
   for `scan`; it generalises to every verb and to every tool. **Redirect to a file, then read `$?`.**
   This nearly made this lane publish the inverse of the truth twice inside one hour.
8. 🔴 **YNET SENDING, THE PART THAT ACTUALLY MATTERS.** Peer inboxes are **URL-encoded peer-id dirs
   at the COOP ROOT** — `<HOST>%2F<host>%2E<lane>~<hash>/inbox` — **not** under `D:\coop\_ynet\`.
   A peer with no announced inbox is **unreachable by every lane**, and the client correctly refuses
   with exit 1 rather than inventing one. Check your own with
   `ls -1d '<HOST>%2F<host>%2E<lane>~'*` at the coop root.
   This lane's own durable state is **repo-relative**: `.specify/ynet/<lane>/wal|alerts`, and the
   WAL is **two-phase and self-auditing** — `grep -c '^STAMP'` (spooled) vs `grep -c '^SENT'`
   (delivered). If they differ, every "sent N/N" you published overstated delivery by the difference.
   🔴 **Beware five live spellings of this lane's id** (`olamnit.glpnet`, `olamnit-glpnet`,
   `glpnet`, plus two probe ids) — they are **separate WALs**. The 122-message history is under
   `olamnit.glpnet`; `--lane glpnet --node olamnit` writes to `glpnet`.

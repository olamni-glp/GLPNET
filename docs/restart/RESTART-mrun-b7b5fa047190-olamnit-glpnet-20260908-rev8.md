<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART BRIEF — `olamnit.glpnet` · run `mrun-b7b5fa047190` · **rev 8** · 2026-09-08

**Resume with:** `resume marathon`
**Host:** OLAMNIT · **Branch:** `develop` · **Supersedes rev 7.**
Trust `git log --oneline -1` over any hash here.

> **This file is `olamnit.glpnet` @ OLAMNIT.** `docs/RESTART.md` is `gavriella.glpnet`'s. Check the
> host in the header before trusting either.

---

## 0 · THE HEADLINE — the fleet's most-cited blocker is cleared, with its limits stated

**THE YNET WIRE PLANE BINDS AND A ROUND-TRIP LANDS WITH A CRYPTOGRAPHICALLY PROVABLE SENDER.**
Measured on OLAMNIT 2026-09-08T06:02:26Z. Commit `372809ab`; broadcast `63b1ec96` to 73 lanes.

🔴 **Three things it is NOT — every one of these has already been mis-quoted once in this fleet:**

1. **`provider=msquic`, NOT iroh.** Gate 5 keeps tier 0 unavailable, so the chain fell to tier 1
   exactly as designed. **This does not close the iroh work and licenses nobody to flip
   `carriesLinks`.**
2. **Loopback, one host.** **NOT a cross-host result.** The mesh is not proven.
3. **The peer node id was supplied BY HAND** — nothing resolves a lane name to a node id.

---

## 1 · WHAT THIS SESSION DID

| # | delivered | evidence |
|---|---|---|
| 1 | **`/bk-close` for 109** — the one stage rev 7 recorded as NOT RUN | `.specify/retrospective/differential-cross-runtime-acceptance-gate/20260907T172628Zb23e50.md`; 6 findings, 6 actions; `stale`/`reconcile` both clean |
| 2 | **iroh gate 4 measured OPEN on OLAMNIT's DEFAULT port** | executed via `QuicProviderChain.Default.ProbeAll` + counterfactual + negative control |
| 3 | **The `Self = null` patch** — engineer-authorised over `Q-glpnetshiras-50` | `csharp/ynet_client/Program.cs`; C-18 claim to 73 lanes BEFORE the first line |
| 4 | **Wire send gained `--peer-node`**, and its refusal now names the real gap | same file |
| 5 | **P0 found: Smart App Control blocks the canonical deployed ynet-client** | rc=127, `0x800711C7`, CodeIntegrity 3077/3033/3118 |
| 6 | **9 codify notes, 5 marathon items, 3 broadcasts (73 lanes each, 0 failed)** | frames verified **on disk**, not from the tool's own tally |
| 7 | **Board: 72 not-closed, every row scored, 0 captured, 0 refined** | `scripts/roadmap_open_table.py` |

---

## 2 · 🔴 ENGINEER RULINGS TAKEN 2026-09-08T05:5xZ — all three answered

| ruling | decision | state |
|---|---|---|
| **Patch `Self = null` here?** | **YES — patch it here, disclosed** (narrower, later instruction overriding `Q-glpnetshiras-50` for this one line) | ✅ DONE |
| **Admin actions** | **ALL THREE AUTHORISED**: firewall 47890 · sign the deployed ynet-client · docker for feature 110 | 🔴 **ALL THREE STILL OWED — need elevation** |
| **Next era** | **bundle 1 AND bundle 2, THEN bundle 3** | ▶ NEXT |

### 🔴 NEW STANDING RULE FROM THE ENGINEER — supersedes C-15's single-feature era

> **"FULLY DELIVER 4 FEATURES PER ERA, OR UP TO 2 SAGA-SIZED FEATURES. Most features are very
> small, heavily overstated, and 40–70% already implemented by other features already shipped."**

**This session is direct evidence he is right:** the wire-plane unblock — the fleet's single most-
cited blocker, planned around by every host for a day — was **two edits in one file**, because
`NodeIdentity.LoadOrMint` was already built and tested. **Measure what is already there before
scoping. The estimate on the board was not the work.**

---

## 3 · ▶ THE NEXT ERA — ruled, and it is 7 features across two bundles

**Bundle 1 — unambiguously ours (`csharp/ynet_transport` is glpnet's per `Q-shiras0904c-01`):**

| row | feature | WSJF | why now |
|---|---|---|---|
| 6 | `ynet-frame-field-parity-across-planes` | **10.50** | 🔴 **This session handed it its evidence** — see §4 |
| 10 | `ynet-federation-config-and-firewall-correctness` | 8.00 | the measured-NOT-MET 47890 half |
| 5 | `search-before-broadcast-guard` | 10.50 | small; proved its own value 4× yesterday |
| 26 | `sc003-live-refusal-in-an-adopted-area` | 5.33 | 109's own disclosed gap |

**Bundle 2 — now unblocked by §0:**

| row | feature | WSJF | why now |
|---|---|---|---|
| 15 | `ynet-node-identity-persistence` | 6.80 | **largely delivered by this session** — keystore mints, `origin=Loaded` on reuse |
| 29 | `ynet-minted-lane-identity-resolve-address-independent` | 5.20 | **Resolve is now THE binding constraint** on wire send |
| 19 | `sign-the-deployed-ynet-client-…` | 6.50 | the P0; engineer authorised |

**Bundle 3 after** — `per-host-toolchain-and-environment-contract-…` (row 42, and what
`buildkit-roadmap next` itself answers). This session produced **two textbook instances**: Smart App
Control blocking a documented canonical binary, and two DIFFERENT programs both named
`ynet-client.exe` at the same byte size, one silently daemonising on the other's flags.

---

## 4 · THE PARITY DEFECT THIS SESSION FOUND — start bundle 1 here

| plane | `Origin` | space |
|---|---|---|
| file | `olamnit/olamnit.glpnet` | a **NAME** |
| wire | `1d7d7ded…8c0c` | a **NODE ID** |

**One field, two incompatible value spaces, both non-empty well-formed strings** — so a consumer
grouping by `Origin` counts one lane as two **silently**. The fix must pick a space **and carry the
other as its own field**; collapsing them loses either human addressability or cryptographic
attribution. Found by **running** the planes side by side, not by reading them.

---

## 5 · 🔴 THREE ADMIN ACTIONS AUTHORISED AND STILL OWED — this session is not elevated

```
netsh advfirewall firewall add rule name="glp_crdtmsg 47890 UDP" dir=in action=allow protocol=UDP localport=47890
netsh advfirewall firewall add rule name="glp_crdtmsg 47890 TCP" dir=in action=allow protocol=TCP localport=47890
```
- **Sign the deployed ynet-client** — needs a certificate decision. 🔴 **NEVER** by disabling Smart
  App Control: admin-gated **and irreversible without a Windows reinstall.**
- **Docker for 110**: add `Olamnit\smbuser` to `docker-users`, start `com.docker.service`.
  🔴 OLAMNIT is **DORMANT, NOT BARE** — 26.7 GB of PG18 data at `D:\pgdata\pg-node-{a,b}`.
  **ASSESS those clusters; do not provision over them.**

---

## 6 · ENVIRONMENT — corrections rev 7 did not have

- 🔴 **Smart App Control is ENFORCED here.** The canonical deployed client at
  `%LOCALAPPDATA%\yngenios\ynet-client\eea87e02\` **cannot run**. **Build from source:**
  `dotnet build csharp/ynet_client/YnetClient.csproj -c Release` →
  `csharp/ynet_client/bin/Release/net11.0/ynet_client.exe`. **Its CLI takes `--self <node>/<actor>`,
  NOT `--lane`/`--node`.**
- 🔴 **`ynet-client.exe` names TWO DIFFERENT PROGRAMS on this host, both 130,048 bytes.** The other
  (`D:\BSTDEV\research\olamnit\Olamnit\Olamnit.Ynet.Client.Host\bin\Debug\net11.0\`) ignores
  `doctor`/`--coop` and **silently daemonises on :47100**. Given the canonical flags it does not
  fail — it ticks forever. **Pin full paths, never command names.**
- **iroh IS installed and running**: `ynet-iroh-sidecar.exe` pid 20504, control plane
  `127.0.0.1:47899`, `CAPS quic-link`. Probe it with `YNET-SIDECAR/1 HELLO\n` — **`STATUS`,
  `CAPS`, `PING`, `INFO` are all `unknown-verb`.** It binds **ephemeral** UDP (51000/51002), so
  **only program-scoped firewall rules work**; OLAMNIT already has the correct pair.
- **`which iroh` is the WRONG PROBE** and nearly made this lane report iroh absent. The artefact is
  never on `PATH`.
- Node keys: `%LOCALAPPDATA%\glpnet\ynet\<lane>.nodekey`, audited in `mint-audit.log`.
- 🔴 **The classifier is intermittent — RETRY BEFORE ESCALATING.** Hit ~6× this session, succeeded
  on retry every time.
- 🔴 **Heredocs mangle escapes in this shell.** Bit once more this session (a `\` in a Python
  heredoc). **Use the Write tool for scripts and documents.**
- 🔴 **Never believe the exit code of a piped command.** `cmd | tail` makes `$?` **tail's**.
- `dotnet` at `C:\Users\smbuser\AppData\Local\Microsoft\dotnet`, **not on PATH**.
- `scripts/roadmap_open_table.py` needs the **buildkit** venv `/d/bstdev/research/buildkit/.venv313`.
- **YNET fan-out**: `scratchpad/ynetfan.sh` — enumerates peer mailboxes at the COOP root
  (`<HOST>%2F<host>%2E<lane>~<hash>/inbox`), skips probes, **73 sent / 0 failed** ×3.
  🔴 **Verify delivery by counting `.frame` files on disk, never by the tool's own tally.**

---

## 7 · STANDING RULINGS THAT STILL HOLD

- **`Q80=a`** roster is **60** (4×15), quorum ≥45. **Not 15.**
- **`Q-olg15-09`** 108 is ONE sibling to 078; do **not** re-open 078.
- **`Q59`** `tools/ynet` is `@shiras-olamnit`'s. **`R-S5-04`** `[04]` is `@shiras-glpnet`'s.
- **OB-8 still blocks plan authorship.** Step (a) — @buildkit restoring `0974acde` byte-exact to the
  ruled path — **has not landed. Author no plan document.** And OB-8's own verify step is broken on
  Windows: use `git show HEAD:<path> | sha256sum`, never a hash of the CRLF checkout.
- **PBFT equivocation** (6 of 8 members, term 3): discard the equivocator, report the drop.
  Owner `@shiras-olamnit`.

---

## 8 · BASELINE

| | session start | session end |
|---|---|---|
| git | clean, 0/0 | **clean, 0 ahead / 0 behind** (`63b1ec96`+) |
| board (not closed) | 71 · all scored | **72 · all scored · 0 captured · 0 refined** |
| YNET wire plane | **never observed to bind, fleetwide** | **BINDS; round-trip lands; provable sender** |
| iroh gate 4 (OLAMNIT, default port) | believed SHUT | **measured OPEN** |
| YNET sends | — | **219 frames, 0 failed**, verified on disk |
| REPL suite | 604/604 (rev 7) | **NOT RE-RUN this session** — no GLP runtime code was touched |

🔴 **The suite was not re-run and that is disclosed, not hidden.** This session changed only
`csharp/ynet_client/Program.cs`, which the REPL suite does not cover. **Re-run it before any era
that touches `glp_runtime`, and rebuild the Debug C# REPL first**
(`dotnet build out/csharp/glp_repl/glp_repl.csproj -c Debug`).

---

## 9 · RESTART CHECKLIST

1. `resume marathon`
2. **`git fetch origin --tags` FIRST** (C-19). Peers pushed **twice** into this session's window.
3. `buildkit-marathon status --feature differential-cross-runtime-acceptance-gate`
4. Read **§0's three limits** before repeating anything about the wire.
5. Start **bundle 1 at §4** — the parity defect already has its evidence.

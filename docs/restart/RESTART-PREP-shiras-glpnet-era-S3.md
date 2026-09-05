<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — shiras / glpnet · **era S3 delivered**

    written:  2026-09-05T11:10Z   (supersedes RESTART-PREP-…-era-S2.md, 01:25Z)
    host:     SHIRAS (Linux)      repo: olamni-glp/GLPNET
    branch:   develop — clean, pushed, in sync
    resume:   type exactly  →  resume marathon
    marathon: mrun-e76f86453d93, seq 8, 8 outstanding items

> **POINTER, not a ledger.** Roadmap + pipeline state are the source of truth.
> 🔴 **Do not trust a commit hash written in this file.** Read the tip with `git log --oneline -1`.

---

## 1 · First commands on resume

```bash
bk-heavy-lock --timeout 3600 -- buildkit-marathon status --feature ynet-minted-lane-identity-resolve-address-independent
bk-heavy-lock --timeout 3600 -- buildkit-roadmap status
env -u LD_LIBRARY_PATH dotnet test csharp/ynet_transport.tests -c Release   # expect 194/194
env -u LD_LIBRARY_PATH dotnet test csharp/glp_link.tests    -c Release      # expect 221/221
env -u LD_LIBRARY_PATH dotnet test csharp/glp_crdtmsg.tests -c Release      # expect 405/405
```

Standing rules, each learned by breaking it:
1. Wrap heavy buildkit calls in `bk-heavy-lock`. **Batch under ONE hold** — biggest tempo win here.
2. `marathon capture` takes `--description`, **not** `--detail`. Kinds: `bug|idea|issue|latent-requirement|missing-prerequisite`.
3. `--feature`, never `--run`.
4. 🔴 **`Ynet.Transport.Path` shadows `System.IO.Path`** — qualify `System.IO.Path` in any test or file under `csharp/ynet_transport*`. Cost me one build cycle again this session.
5. `roadmap sync` needs `--round N`. Last round was **74**.

## 2 · WHAT ERA S3 DELIVERED — three engineer rulings, implemented and measured

`Q-47`, `Q-48`, `Q-49` were asked via `AskUserQuestion` (BK-STD-2, validator 3/3 conformant) and
**all three answered with the recommended option**. All three are now implemented.

| ruling | decision | state |
|---|---|---|
| **Q-47** | loud-failure guard **AND** forensic record | ✅ `NodeIdentityPublication.cs`; probe shouts; `mint-audit.log`; guard **armed** (`publication: Matches`) |
| **Q-48** | derive the pin on read; stop storing it | ✅ pin is now a pure function of the key; sidecar is a refreshed cache |
| **Q-49** | ratify UDP **47890** as explicit INTERIM | ✅ ratified + broadcast; per-host advertisement recorded as the destination |

**Suites: `ynet_transport` 194/194 (was 182) · `glp_link` 221/221 (was 219) · `glp_crdtmsg` 405/405.**

## 3 · 🔴 THE FIND THAT MATTERS MOST — and it was in this lane's own shipped code

**`File.Move(src, dst, overwrite: false)` is NOT an atomic exclusive claim.** It is a
check-then-rename: two concurrent callers both pass the existence check, both rename, and **both
believe they won.**

```
ConcurrentFirstStart_ConvergesOnOneIdentity, 16 callers, 20 runs
  baseline           2/20 FAILED   "the collection contained 2 items"  <- TWO identities, ONE host
  after Q-48 alone   1/20 FAILED   (fixed the shallower key/pin defect only)
  after atomic claim 0/20
```

**THREE identity stores in this repo carried the idiom, two of them under a comment asserting the
move was "atomic and NON-overwriting".** All three now take a `FileMode.CreateNew` claim
(`O_CREAT|O_EXCL`), which the kernel actually serialises.

> **The principle, offered fleet-wide:** *durability and exclusivity are different properties;
> a mechanism that gives one does not give the other, and an idiom that looks like it gives both
> usually gives neither well.*

Broadcast `20260905T1100Z` carries the grep for other lanes. Codified as
`atomic-claim-not-check-then-rename`, **WSJF 9.67 — the highest on this board.**

## 4 · 🔴 FIVE THINGS A SUCCESSOR MUST NOT RE-DERIVE WRONGLY

1. **QUIC is NOT absent on SHIRAS.** `BK-FTAP-1` `v5` §2.2 says it is; that is **false** and `v6`
   corrects it with the measurement. `IsSupported` **latches MsQuic's static initialiser on first
   read**, so a resolver registered afterwards is ignored. **Bind a port and fail; never read a flag
   and conclude.** SHIRAS bound `0.0.0.0:47890`, exit `0`.
2. **NOTHING HAS CROSSED A WIRE BETWEEN TWO HOSTS. Do not report federation.** The hosts are one
   flat `/24` (SHIRAS `192.168.0.170`, GAVRI `192.168.0.108`) with a live session between them, and
   `BindListenerAsync` already takes a routable endpoint — so the gap is **one peer binding a port**,
   not a capability. That is the open ask.
3. **glpnet builds no election and votes in none** (`R-1`). Zero board ops held, none emitted.
   **Do not run `yx_ynet hello`** — SHIRAS's franchise is `shiras.yngraw`'s; a second node is a
   phantom vote.
4. **`M6` is NOT MET here and is reported as NOT MET.** This lane participates through an agent,
   which `M6` forbids. **Do not build a fifteenth QHSM/QMSM client** — it is an L0 shared capability
   owned by the `yngenios` core lane. The blocking fact is that **L0 ownership is unconfirmed**.
5. **There are now TWO `specs/102-*` dirs** — `102-ynet-minted-lane-identity` (this lane) and
   `102-quic-federation-transport` (peer, landed ~10:30Z). **They are different features. Do not
   consolidate them.** `Q-43` covers the scope boundary and is still open.

## 5 · OPEN DEFECTS — measured, unfixed, owned elsewhere or awaiting a ruling

| defect | measured | state |
|---|---|---|
| **`OpWal.cs:93`** uses the same non-atomic `File.Move` as its **WAL commit point** | this session | 🔴 **REPORTED NOT FIXED** (Bug Protocol) — a WAL-ordering decision, not an identity one |
| **87 of 138 roadmap features carry no `spec_path`** — it keeps growing (81/132 → 84/135 → 87/138) | round 74 | buildkit tooling |
| **the reboot that lost this lane's node key is still UNEXPLAINED** | 09:06Z | guard + forensics now in place to catch the next one |
| `roadmap sync` **coop mirror not configured** — publishes to the sink only | round 73, 74 | pass `--coop-inbox` or set `$BUILDKIT_COOP_INBOX` |
| UDP 47890 ratified but **no peer has bound it** | standing | needs one peer lane on a second host |

## 6 · WHAT'S NEXT

1. **The cross-host handshake.** One peer lane binds `0.0.0.0:47890`; this lane dials it. `Q-49`
   ratified the port; everything else is already measured and in hand. **This is the real
   federation milestone and it is now one command away on someone else's host.**
2. **`M6`** — get L0 ownership confirmed, then consume it. Do not author a rival.
3. From the board, by WSJF: `stable-federation-identity…` (reviewed) →
   `differential-cross-runtime-acceptance-gate` (19.50) → `atomic-claim-not-check-then-rename` (9.67,
   partly delivered here — `OpWal` is what remains) → `federation-keystore-key-and-pin-one-atomic-act`
   (9.00, delivered here; needs closing) → `identity-durability-proven-across-a-reboot` (8.00).
4. **Four BK-STD-2 questions remain open from era S2** (`Q-43`..`Q-46`); three from S3 (`Q-47`..`Q-49`)
   are **answered and implemented**.

---

*Written by shiras/glpnet for its own successor. Resume with: `resume marathon`.*

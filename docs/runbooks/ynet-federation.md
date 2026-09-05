<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Runbook — enabling ynet federation between two hosts

**Feature**: `102-quic-federation-transport` · **Satisfies**: FR-024, FR-025 · **Verifies**: SC-008, SC-009
**Status**: the local half is built and green (265/265). **SC-001 is UNMEASURED until a peer answers.**

This is the operator path. Following it literally is what SC-008 asks for: enable federation between
two hosts and observe a crossed operation, **without disabling any host protection**.

---

## 0 · Before you start

- Both hosts on the same `/24`. Measured 2026-09-04: Gavriella `192.168.0.108`,
  Ariellas `192.168.0.142`, Olamnit `192.168.0.136` **and** `.129`, shiras `192.168.0.170`.
- .NET 11 on both (`dotnet --version`).
- **An elevated shell on each host, once**, for §3 only.

> **Use literal IPv4 addresses everywhere.** Every hostname on this estate resolves to `fe80::`
> link-local only. A dial by name fails for a reason that is *not* QUIC, and gets misread as a
> transport failure.

> **`ping` proves nothing here.** ICMP is filtered. A host that failed `Test-Connection` answered
> `Test-NetConnection -Port 445` twelve minutes later, and the "host is down" claim had to be
> retracted. If you need reachability, use a **second, different** probe.

---

## 1 · Confirm the stack before configuring anything

```
dotnet run -c Release --project csharp/ynet_federation -- status
```

Measured on Gavriella, unconfigured:

```
stack supported        : yes
listener bound         : no
peer admitted          : no   (peer set is empty - no pins configured)
op received from peer  : unknown
same machine           : n/a   (no crossing observed)
policy refusal         : none

federation is DISABLED in configuration — local lanes are served normally (FR-004).
```

Read the four states **separately**. There is deliberately no summary verdict, because an aggregate
is how four honest states become one dishonest one.

| Reading | Meaning |
|---|---|
| `stack supported : no` | QUIC is unavailable **in this process**. Nothing downstream can work. |
| `policy refusal : Smart App Control (0x800711C7)` | The host blocked the binary. Run via `dotnet run` — the signed host. Durable fix: code-signing in `buildkit ship` (ruling `Q-GLPNETG27-02`; disabling the protection was **declined as one-way**). |
| any state `unknown` | It **could not be measured**. This is *not* a `no`. |

---

## 2 · Mint the identity and the epoch

On **each** host:

```
dotnet run -c Release --project csharp/ynet_federation -- identity init
```

Measured on Gavriella:

```
node_id : 96a28f1215386070bed9b45acacc43744e7d6389d88cf1040130e63fed8fe098
key     : C:\Users\gavri\AppData\Local\ynet\federation\node.key (minted)
```

Re-running prints the **same** `node_id`, marked `(existing)`. That stability is the whole point:
`QuicLinkTransport.CreateDevCert()` mints a *fresh* certificate per call, so a pin taken from a
probe run is **ephemeral** and stale before it reaches the peer you sent it to.

Then mint the epoch on **one** host and copy the value to the other:

```
dotnet run -c Release --project csharp/ynet_federation -- epoch mint --rationale "first federation epoch"
dotnet run -c Release --project csharp/ynet_federation -- config set space_id <the-minted-value>
```

> Different `space_id`s are **not an error**. They mean the two hosts' terms are incomparable and no
> leadership decision can be made between them — which is safe, just not useful.

---

## 3 · Open the port (elevated, once per host)

```powershell
New-NetFirewallRule -DisplayName 'ynet-federation-quic-udp-47890' -Direction Inbound `
  -Action Allow -Protocol UDP -LocalPort 47890 -Profile Private `
  -RemoteAddress 192.168.0.0/24 -Enabled True
```

Private profile, this `/24`, one UDP port. Authorised by ruling `Q-GLPNETG27-04`. **No host
protection is disabled** (FR-024).

`Access is denied` means the shell is not elevated. This is the **only** step needing elevation, and
it gates **inbound** dials only — outbound dialling, both loopback ends, the fold, the term rule and
the status surface are all exercisable without it.

---

## 4 · Exchange pins and enable

On Gavriella, adding Olamnit — **both** addresses, **one** entry:

```
dotnet run -c Release --project csharp/ynet_federation -- config add-peer `
    --name olamnit --node-id <olamnit-node-id> `
    --endpoint 192.168.0.136:47890 --endpoint 192.168.0.129:47890
dotnet run -c Release --project csharp/ynet_federation -- config set bind_address 0.0.0.0
dotnet run -c Release --project csharp/ynet_federation -- config set enabled true
```

Mirror it on Olamnit with Gavriella's `node_id` and `192.168.0.108:47890`.

> Two endpoints, **one participant**. Adding an address does not add a participant — the count is
> keyed on `node_id` (FR-007 / SC-006). Two of four hosts on this estate answer on two addresses, so
> an address-keyed count over-counts the fleet.

Verify what was actually stored — `config set` reads back automatically, and `config show` prints the
**effective** values rather than the file's literal text:

```
dotnet run -c Release --project csharp/ynet_federation -- config show
```

Configuration refuses loudly and names the field. You will see, for instance:

- `bind_address: loopback bind is not peer-reachable` — the failure mode that looks like success.
- `space_id: '5961694' looks clock-derived` — exactly how the fossil term was born.
- `peers[x].endpoints: 'olamnit:47890' is not a literal address` — see §0.

---

## 5 · Serve, and watch an operation cross

On **both** hosts:

```
dotnet run -c Release --project csharp/ynet_federation -- serve
```

On Gavriella:

```
dotnet run -c Release --project csharp/ynet_federation -- post --body "hello from gavriella"
```

On Olamnit, within **5 seconds** (ruling `Q-GLPNETG28-03`):

```
dotnet run -c Release --project csharp/ynet_federation -- status
```

```
stack supported        : yes
listener bound         : yes   0.0.0.0:47890
peer admitted          : yes   (1 participant)
op received from peer  : yes
same machine           : no
policy refusal         : none
```

### 🔴 `same machine : no` is the line that matters

If it reads `yes`, you have proved the **mechanism**, not federation. FR-022 disqualifies a
same-machine crossing as cross-host evidence and **SC-001 remains unmeasured**. (`I:` is an SMB
loopback of this host's own `D:\`, so a "peer" reached that way is this host wearing a share name.)

### If it does not cross — read the states, do not aggregate them

| Reading | Meaning | Next step |
|---|---|---|
| `listener bound : no` | nothing is listening | check `bind_address` is not loopback |
| `peer admitted : no (peer set is empty)` | no pins configured | §4 |
| `PinMismatch` | presented identity ≠ pin | the peer re-minted its key — re-exchange `node_id` |
| `Unreachable` | cannot reach the peer | §3, **on the peer** |
| `NameResolutionFailed` | no usable endpoint | use a literal IPv4 address |
| `op received from peer : no`, peer admitted | link up, nothing sent | `post` on the other host |
| any state `unknown` | **could not be measured** | investigate the measurement, not the network |

`PinMismatch` and `Unreachable` are reported separately because they demand **opposite** responses:
investigate a possible attack, versus wait for a host.

---

## 6 · Measuring SC-001 from the test suite

With a peer serving, on this host:

```powershell
$env:YNET_FED_PEER_ENDPOINT = "192.168.0.136:47890"
$env:YNET_FED_PEER_NODEID   = "<peer node id>"
dotnet test csharp/glp_crdtmsg.tests/GlpCrdtMsg.Tests.csproj -c Release --filter "FullyQualifiedName~CrossHost"
```

The result is written to `%TEMP%\ynet_federation\sc001.evidence.json`. Without a peer it reads:

```json
{ "State": "UNMEASURED", "IsMet": false,
  "Detail": "no peer listener configured ... FR-022 disqualifies the one-machine mechanism proof" }
```

That record is the **close gate's** input. An `UNMEASURED` evidence file means SC-001 may not be
reported as met, whatever the suite's green count says.

---

## 7 · Reversal — putting the host back (SC-009, FR-025)

Every enabling change recorded its reversal as **data** when it was made:

```
dotnet run -c Release --project csharp/ynet_federation -- revert          # dry run, shows the plan
dotnet run -c Release --project csharp/ynet_federation -- revert --all    # applies config reversals
```

Measured output:

```
2 recorded change(s), newest first — reverse order matters:

  [2026-09-04T13:25:55Z] epoch mint ynet-epoch-2026-09-dc4381
      undo: restore the recorded prior config
      prior state is recorded and restorable (168 bytes)
  [2026-09-04T13:25:49Z] minted node.key
      undo: delete C:\Users\gavri\AppData\Local\ynet\federation\node.key
```

Then, elevated:

```powershell
Remove-NetFirewallRule -DisplayName 'ynet-federation-quic-udp-47890'
```

Afterwards `status` reads exactly as it did in §1.

---

## 8 · Retiring a bad operation

**Never delete one.** On an append-only board a removal is indistinguishable from a suppression, so
the only correction is an appended superseding op (FR-017 / FR-029, ruling `Q-GLPNETG28-04`):

```
dotnet run -c Release --project csharp/ynet_federation -- retire --op <peer:counter> --reason "<why>"
```

The target stays in the log and becomes incomparable to every live term. Both halves — still present,
and excluded from ordering — are asserted together in `TermOrderingTests`.

> **The known fossil**, `leader_claim` op `628016928ab854ae` carrying `term 5961694 =
> floor(unix_ts/300)` from a since-deleted emitter, is **not on this host** — searched 2026-09-04,
> no `ynet\log\*.jsonl` exists here and the op id appears only in COOP broadcast text. The lane that
> holds it retires it with the command above. **Do not delete it by any other means.**

---

## 9 · What this does not give you

No leader is elected. No PBFT runs. There is no fleetwide coordinator and no fleetwide signature
verifier. They consume this transport and were blocked by its absence — they are the next era.

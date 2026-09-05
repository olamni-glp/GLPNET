<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Quickstart — enabling ynet federation between two hosts

**Feature**: `102-quic-federation-transport` | **Verifies**: SC-008, SC-009

This is the operator path, end to end, on two hosts. It is written so that **SC-008 is satisfiable by
following it literally**: enable federation between two hosts and observe a crossed operation,
without disabling any host protection.

Everything here is reversible by §6, and each reversal is recorded as data (FR-025).

---

## 0. Before you start — what you need

- Both hosts on the same `/24`. Measured: Gavriella `192.168.0.108`, Ariellas `192.168.0.142`,
  Olamnit `192.168.0.136` and `.129`, shiras `192.168.0.170`.
- .NET 11 on both. Verify: `dotnet --version`.
- **An elevated shell on each host, once**, for step 3 only.

> **Use literal IPv4 addresses everywhere.** Hostnames on this estate resolve to `fe80::`
> link-local only. A dial by name fails for a reason that is not QUIC.

---

## 1. Confirm the stack before configuring anything

```
dotnet run -c Release --project csharp/ynet_federation -- status
```

Expected on an unconfigured host:

```
stack supported        : yes
listener bound         : no
peer admitted          : no    (peer set is empty - no pins configured)
op received from peer  : no
same machine           : n/a
policy refusal         : none
```

`stack supported : no` here means QUIC is unavailable in this process — stop and fix that first;
nothing downstream can work. `policy refusal : Smart App Control (0x800711C7)` means the host blocked
the binary: run via `dotnet run` (the signed host), and see the durable fix in ruling
`Q-GLPNETG27-02` (code-signing in `buildkit ship`).

> `ping` failing tells you **nothing** here. ICMP is filtered on this estate — a host that does not
> answer `ping` answered `Test-NetConnection -Port 445` twelve minutes later. If you need to test
> reachability, use a **second, different** probe.

---

## 2. Mint the identity and the epoch

On **each** host:

```
dotnet run -c Release --project csharp/ynet_federation -- identity init
dotnet run -c Release --project csharp/ynet_federation -- epoch mint --rationale "first federation epoch"
```

`identity init` mints and **persists** the node key and prints its `node_id` (64 hex chars). Write it
down — this is what you exchange with the peer.

> The `node_id` is stable across restarts **because it is persisted**. A pin read from a probe run is
> ephemeral and must never be published to a peer.

Both hosts must end up with the **same `space_id`**. Mint on one host, then set the same value on the
other:

```
dotnet run -c Release --project csharp/ynet_federation -- config set space_id ynet-epoch-2026-09
```

Different `space_id`s are not an error — they simply mean the two hosts' terms are incomparable, and
no leadership decision can ever be made between them.

---

## 3. Open the port (elevated, once per host)

```powershell
New-NetFirewallRule -DisplayName 'ynet-federation-quic-udp-47890' -Direction Inbound `
  -Action Allow -Protocol UDP -LocalPort 47890 -Profile Private `
  -RemoteAddress 192.168.0.0/24 -Enabled True
```

Private profile, this `/24`, one UDP port. **No host protection is disabled.** If this returns
`Access is denied`, the shell is not elevated — that is the only thing elevation is needed for.

---

## 4. Exchange pins and enable

On Gavriella, add Olamnit — **both** of its addresses, one entry:

```
dotnet run -c Release --project csharp/ynet_federation -- config add-peer `
    --name olamnit --node-id <olamnit-node-id> `
    --endpoint 192.168.0.136:47890 --endpoint 192.168.0.129:47890
dotnet run -c Release --project csharp/ynet_federation -- config set bind_address 0.0.0.0
dotnet run -c Release --project csharp/ynet_federation -- config set enabled true
```

Mirror it on Olamnit with Gavriella's `node_id` and `192.168.0.108:47890`.

> Two endpoints, **one** participant. Olamnit answers on two NICs; adding an address does not add a
> participant, and the participant count will read `1`.

Verify what was actually stored:

```
dotnet run -c Release --project csharp/ynet_federation -- config show
```

---

## 5. Serve, and watch an operation cross

On **both** hosts:

```
dotnet run -c Release --project csharp/ynet_federation -- serve
```

On Gavriella, post an operation:

```
dotnet run -c Release --project csharp/ynet_federation -- post --body "hello from gavriella"
```

On Olamnit, within **5 seconds**:

```
dotnet run -c Release --project csharp/ynet_federation -- status
```

```
stack supported        : yes
listener bound         : yes   0.0.0.0:47890
peer admitted          : yes   gavriella (1 participant)
op received from peer  : yes
same machine           : no
policy refusal         : none
```

**`same machine : no` is the line that matters.** If it reads `yes`, you have proved the mechanism,
not federation — FR-022 disqualifies a same-machine crossing as cross-host evidence, and SC-001 is
still unmeasured.

### If it does not cross

Read the states, and do not aggregate them:

| Reading | Meaning | Next step |
|---|---|---|
| `listener bound : no` | nothing is listening | check `bind_address` is not loopback |
| `peer admitted : no (peer set is empty)` | no pins configured | step 4 |
| `peer admitted : no (pin mismatch)` | identity presented ≠ pin | the peer re-minted its key — re-exchange `node_id` |
| `peer admitted : no (unreachable)` | cannot reach the peer | firewall step 3, on the **peer** |
| `op received from peer : no` but peer admitted | link is up, nothing sent | post on the other host |
| any state `unknown` | it **could not be measured** | this is not a `no` — investigate the measurement |

Pin mismatch and unreachable demand **opposite** responses. They are reported separately for that
reason.

---

## 6. Reversal — putting the host back (SC-009)

```
dotnet run -c Release --project csharp/ynet_federation -- config set enabled false
dotnet run -c Release --project csharp/ynet_federation -- revert --all      # replays recorded reversals
```

```powershell
Remove-NetFirewallRule -DisplayName 'ynet-federation-quic-udp-47890'
```

`revert --all` reads `changes.jsonl` and undoes each recorded change in reverse order: config file
restored verbatim, node key deleted. Afterwards `status` reads exactly as it did in §1.

---

## 7. What this does NOT give you

No leader is elected. No PBFT runs. There is no fleetwide coordinator and no fleetwide signature
verifier. Those consume this transport and were blocked by its absence — they are the next era, not
this one.

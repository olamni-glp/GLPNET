<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Phase 0 Research — QUIC federation transport for the ynet oracle

**Feature**: `102-quic-federation-transport` | **Date**: 2026-09-04 | **Host**: Gavriella

Every entry below is either **measured on this host with the command shown**, or **resolved by a
recorded engineer ruling that is cited by qid**. Nothing is inferred. Where a prior-session
measurement was reused it was **re-run**, because on this estate a measurement older than the
session is a hypothesis, not a fact.

---

## R1 — Does QUIC actually work here?

**Decision**: Build on `System.Net.Quic` via the existing `QuicLinkTransport`. No new stack.

**Measured** (2026-09-04, this session's predecessor, re-run and re-confirmed):

```
dotnet run -c Release --project csharp/glp_quic_probe -- 0.0.0.0:47890
  QuicListener.IsSupported    = True
  QuicConnection.IsSupported  = True
  QuicLinkTransport.IsSupported = True
  bind 127.0.0.1:0    -> LISTENER BOUND
  bind 0.0.0.0:47890  -> LISTENER BOUND        (federation-capable, not loopback-only)
  .NET 11.0.0, Windows 26200                    exit 0
```

Independently corroborated by a **second, different codebase** (buildkit PR #903) reaching the same
verdict — two implementations, one conclusion.

**Rationale**: the estate's oracle reported the board blocked on "no QUIC listener runs in this
estate". That is a statement about what is **running**, and was misread as a statement about what
**exists**. `QuicLinkTransport` is 491 lines of working mTLS + SPKI-pinned transport that binds a
peer-reachable address today. The transport is UNRUN, not missing.

**Alternatives considered**:
- *iroh / iroh-net.* Ruled on already — `Q-38` (decided 2026-09-04): iroh's **identity model** goes
  to L0 as a dependency-free algorithmic core; the iroh **runtime** sits at L1 behind the
  `ILinkTransport` seam GLPNET owns. That ruling makes iroh a **second realization behind the same
  seam**, explicitly not a precondition of this feature. Its open sub-question (vendored Rust vs.
  prebuilt native library) is unresolved and stays out of scope here; `cargo` is absent on SHIRAS and
  ARIELLAS, which is the same class of gap it would remove.
- *libmsquic on the default path.* A peer host measured `IsSupported=False` without it and `True`
  with it, but `LD_LIBRARY_PATH` greened the tests while leaving every service broken. Rejected as a
  configuration trap; on this host the BCL path is `True` unaided.

**⚠ Explicit non-inference**: "no listening TCP port" is **not** evidence a QUIC service is absent.
QUIC is UDP and has no TCP socket by design. This misread is on the record and FR-020 exists because
of it.

---

## R2 — Is the second host reachable, and how should it be addressed?

**Decision**: dial peers by **literal IPv4 address** (FR-003); key every peer table by **nodeId**,
never by address (FR-007).

**Measured** (2026-09-04):

| Host | Address(es) | Notes |
|---|---|---|
| Gavriella | `192.168.0.108` | this host |
| Ariellas | `192.168.0.142` | |
| Olamnit | `192.168.0.136` **and** `192.168.0.129` | **two NICs — two addresses, one participant** |
| shiras.local | `192.168.0.170` | |

All four are routable IPv4 L2 neighbours on **one flat /24**. NAT and routing are therefore
**removed from the unknowns** for the first delivery.

**But**: hostnames resolve to `fe80::` link-local **only**. A dial by name fails for a reason that
is not QUIC, and would be misread as a transport failure — hence FR-003 and the edge case requiring
name-resolution failure to be reported as such.

**And**: two of four hosts answer on more than one address. Any admission or participant count keyed
on address over-counts. This is FR-007 and SC-006.

**⚠ Two retracted claims are preserved here as method, not trivia:**
1. *"Shiras is unreachable"* — asserted from `Test-Connection Shiras` → False, **retracted 12
   minutes later** when `Test-NetConnection Shiras -Port 445` → True. ICMP is filtered on this
   estate. A failing `ping` is **not** evidence a host is down. Caught only by running a second,
   *different* probe.
2. *"GAVRI is a fourth host"* — `I:` is `\\192.168.0.108\GAVRI_D`, an **SMB loopback of this host's
   own `D:\`**. `D:\coop` and `I:\coop` are the same directory. "GAVRI" is a **share name, not a
   host**. Any peer enumeration keyed on drive letters double-counts this host.

---

## R3 — What is the node identity, and where does it live?

**Decision**: reuse the existing `Ynet.Transport.Capability.NodeIdentity` — `nodeId = SHA-256(SPKI)`,
Ed25519-primary with a loud ECDsa/P-256 fallback — and **persist** the key so the pin is stable.

**Measured**: `csharp/ynet_transport/Capability/NodeIdentity.cs` already implements
`NodeId = DeriveNodeId(spki)` where the derivation is `SHA-256` over the SubjectPublicKeyInfo, with
`INodeSigner`, `KeyState { Active, Migrating, Retired }`, and an explicit `Algorithm` property that
is the loud signal a fallback occurred. `QuicLinkTransport.SpkiPin(cert)` derives its pin from the
**same** SPKI, so the transport pin and the node identity are the *same value under two names* —
they do not need reconciling, only naming consistently.

**The gap this feature must close**: `QuicLinkTransport.CreateDevCert(cn)` mints a **fresh cert per
call**, so today's pin is **ephemeral**. A probe-run pin published to peers would be stale before it
arrived. `NodeIdentityStore` (FR-007) persists the key to a per-host file so the nodeId survives
restarts, and the runbook publishes *that* pin.

**Alternatives considered**: introducing a second identity scheme — rejected outright by the spec's
own assumption ("no second identity scheme is introduced by this feature"). Deciding which of the
estate's two authentication models is authoritative — explicitly **out of scope**; this feature needs
only *a* stable identity, not that ruling.

---

## R4 — What is the board, and what does folding it mean?

**Decision**: reuse the existing grow-only per-actor JSONL op-logs and the existing union-by-id
fold. Federate the ops; do not redesign the board.

**Measured**: the board root is a tree of per-actor, per-kind, append-only JSONL files, e.g.
`D:\coop\buildkit\sched\calendar\<actor>\<actor>-cal-000001.jsonl`, with sibling kinds (`caps`,
`actions`, …). `GlpRuntime.CrdtMsg.Crdt.Dot` already supplies `(PeerName, Counter)` op identity and
`VersionVector.Contains(dot)` already supplies the **idempotent already-seen test** that makes
redelivery free. `HashChain.PredHash` already binds an op to its causal predecessors.

So FR-010 (exactly-once), FR-011 (additive) and FR-012 (order-independence) are **properties of
primitives that already exist**; this feature's job is to *use* them across a link and to **test them
against deliberate redelivery**, which is the part that has never been done between machines.

---

## R5 — The term-space rule, and the fossil operation

**Decision**: `term := (space_id, era_counter, host_id)`; compare **only within a space**; mint
`space_id` per federation epoch by recorded operator action; treat an unrecognised space as a named
**legacy** space; retire the fossil by **appending** a superseding operation.

**Cited rulings** — this is decided, not open:
- `Q-GLPNETG27-03` — the namespaced triple, carrying a 🛑 **STOP ORDER**: *do not fold any board
  across hosts until the fold is term-space aware.*
- `Q-GLPNETG28-01` — `space_id` is minted **per federation epoch** by a recorded operator action and
  carried in config. Rejected one-global-space (one-way) and per-host-space (one-way).
- `Q-GLPNETG28-04` — the fossil is retired **into the legacy space** by an appended superseding op.
  Rejected higher-live-term and tombstone, both one-way.

**Measured**: a live `leader_claim` operation `628016928ab854ae` carries `term: 5961694`, which is
`floor(unix_ts/300)` — a wall-clock-derived term. BK-ELECT-1's term is `1`. **Max-term is monotone**,
so a naive merge installs the fossil as the permanent winner and no later legitimate claim can ever
outrank it. The emitting code has been deleted; **the operation still votes**. Deleting the emitter
did not delete the op — that misattribution is itself on the record and was corrected in a broadcast.

**Why a wall-clock term is wrong on its own terms**, independent of the fossil: it advances fastest
for the host that did the **least**, and a host switched off for a week gains ordering advantage
purely from having been absent. FR-015 forbids it.

**🔴 Do NOT delete op `628016928ab854ae`.** Suppression is undetectable on an append-only board;
correction must be additive. This is FR-017, and FR-029 is the mechanism.

---

## R6 — Convergence: push, pull, or both?

**Decision**: **both** — push on append, plus a 60 s reconciliation pull backstop. Assert 5 s
steady-state and 120 s after a deliberate link interruption.

**Cited ruling**: `Q-GLPNETG28-03`.

**Rationale**: push alone loses any op that was in flight across a dropped link, with no repair path,
and leaves two boards silently divergent — the worst possible failure for a board whose entire
purpose is agreement. Pull alone is self-healing but a 60 s window is longer than the time a lane
takes to start duplicate work, which is the exact defect User Story 1 names. The backstop is also
what makes FR-012's order-independence **observable**: after an interruption, ops arrive in a
different order than they were created, which is the only honest test of an order-independent fold.

---

## R7 — Will a freshly-built binary even run on this host?

**Decision**: carry acceptance evidence in the **test host**; ship the operator console as a project
invoked through the signed `dotnet` host; and **detect and name** a policy refusal (FR-023).

**Measured**: Smart App Control is **ON and ENFORCING** on Gavriella
(`VerifiedAndReputablePolicyState = 1`, `CodeIntegrityPolicyEnforcementStatus = 2`). A freshly-built
**unsigned apphost assembly** was blocked with:

```
System.IO.FileLoadException: An Application Control policy has blocked this file. (0x800711C7)
```

The `glp_quic_probe` **did** run via `dotnet run -c Release`, under the already-signed `dotnet` host.
So the two facts are compatible and the boundary is the *apphost*, not the managed code.

**Why this is not a workaround (Constitution II)**: FR-023 requires the refusal to be reported as a
**distinct named failure**, because it presents as a healthy build and a passing test suite followed
by a daemon that never runs. The plan therefore *detects* `0x800711C7` and names it, rather than
disabling the protection. Ruling `Q-GLPNETG27-02` already decided the durable fix — **code-sign in
`buildkit ship`**; a WDAC exception was not taken and turning SAC off was **declined as one-way**.
That work is tracked separately and is out of scope here.

---

## R8 — Opening the port

**Decision**: one inbound rule, **Private profile**, `192.168.0.0/24` only, UDP/47890, with the
reversal recorded beside it (FR-024, FR-025, SC-009).

**Cited ruling**: `Q-GLPNETG27-04` — dev certs plus a scoped UDP rule, authorised.

```powershell
# enable
New-NetFirewallRule -DisplayName 'ynet-federation-quic-udp-47890' -Direction Inbound `
  -Action Allow -Protocol UDP -LocalPort 47890 -Profile Private `
  -RemoteAddress 192.168.0.0/24 -Enabled True
# reverse
Remove-NetFirewallRule -DisplayName 'ynet-federation-quic-udp-47890'
```

**🔴 Blocked on the engineer**: `New-NetFirewallRule` returned **`Access is denied`** — it needs an
elevated shell and this lane cannot self-elevate. Nothing else in the feature is blocked by it:
outbound dialling, both loopback ends, the fold, the term rule and the status surface are all
testable without it. It is required only for an **inbound** peer dial.

---

## R9 — How does the era close against SC-001?

**Decision**: build both federation ends and the operator runbook here, prove every criterion except
SC-001 locally, and issue an **ACK-required broadcast** to all hosts; SC-001 is measured the moment
any peer stands up its listener.

**Cited ruling**: `Q-GLPNETG28-02`. Redefining SC-001 to accept a one-machine proof was rejected as
one-way — it would foreclose the feature's own FR-022 and re-install the false-green class the era
exists to eliminate.

**Consequence for the test suite**: `CrossHostAcceptanceTests` must **skip loudly** — reporting
*peer absent, SC-001 unmeasured* — and must never pass by default. An unmeasured criterion reported
as green is precisely FR-021's prohibition.

---

## Resolved-unknowns ledger

| Unknown | Status | Resolved by |
|---|---|---|
| QUIC stack availability | Measured `True`, listener bound `0.0.0.0:47890` | R1 |
| iroh's place in the lattice | Decided | ruling `Q-38` (cited, not re-asked) |
| Peer addressing | Measured; literal IPv4, one flat /24 | R2 |
| Node identity scheme | Exists in-repo; needs persistence | R3 |
| Board substrate & fold | Exists in-repo; reuse unchanged | R4 |
| `space_id` assignment | Decided — per federation epoch | ruling `Q-GLPNETG28-01` |
| Fossil op remedy | Decided — append retirement into legacy space | ruling `Q-GLPNETG28-04` |
| Convergence window | Decided — 5 s push / 120 s post-outage pull | ruling `Q-GLPNETG28-03` |
| Binary execution under SAC | Measured; test host + `dotnet run`; refusal named | R7 |
| Firewall rule | Authorised; **needs elevation** | ruling `Q-GLPNETG27-04`, R8 |
| SC-001 close path | Decided — implement here + ACK-required broadcast | ruling `Q-GLPNETG28-02` |

**No NEEDS CLARIFICATION remains.**

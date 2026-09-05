<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract — Federation status surface

**Satisfies**: FR-019, FR-020, FR-021, FR-022, FR-023
**Verified by**: SC-007, SC-010

This estate recorded **six false greens in one week**, one of which survived CI. This contract exists
so that "it looks like it is working" and "it is working" cannot produce the same output.

---

## S1 — Four states, never one light

```csharp
public enum Tri { Yes, No, Unknown }

public sealed record FederationStatus(
    Tri StackSupported,          // the QUIC stack is available in THIS process
    Tri ListenerBound,           // a listener is bound to a PEER-REACHABLE address
    Tri PeerAdmitted,            // at least one peer completed MUTUAL verification
    Tri OpReceivedFromPeer,      // at least one operation has ACTUALLY crossed
    bool SameMachine,            // FR-022 — the crossing was between two processes on one machine
    PolicyRefusal? PolicyRefused // FR-023 — host software policy blocked startup
);
```

There is **no** aggregate `IsFederated` boolean, and none may be added. An aggregate is how four
honest states become one dishonest one.

## S2 — No state may be inferred from another

FR-020. Each field is set **only** by its own measurement:

| Field | Set by | MUST NOT be set by |
|---|---|---|
| `StackSupported` | `QuicListener.IsSupported && QuicConnection.IsSupported` | anything else |
| `ListenerBound` | the listener object reporting a bound endpoint | "the port is configured" |
| `PeerAdmitted` | a completed mutual verification handshake | reachability, a ping, an open port |
| `OpReceivedFromPeer` | an op actually appended from a peer | a connection being established |

**The specific misreads this table forbids**, all on the record:

- "no listening **TCP** port ⇒ no QUIC" — QUIC is UDP and has no TCP socket by design.
- "`ping` times out ⇒ host is down" — ICMP is filtered on this estate; a second, *different* probe
  (`Test-NetConnection -Port 445`) returned True 12 minutes later and the first claim was retracted.
- "two roots exchanged an op ⇒ cross-host federation" — see S4.

## S3 — Unknown is not No

FR-021. A state that **could not be measured** is `Unknown`. `Unknown` and `No` MUST render
differently and MUST be distinguishable programmatically.

**SC-010 test**: remove the ability to measure a state (e.g. make the listener handle unreadable) and
assert the result is `Unknown`. If the code reports `No`, the test fails — reporting a clean negative
for an unmeasured condition is the failure being prevented.

## S4 — A same-machine crossing is not federation

FR-022. When both participants are on this machine, `SameMachine = true` and the surface **MUST NOT**
report cross-host federation. `OpReceivedFromPeer` may legitimately be `Yes` — the mechanism did
work — but the two facts are reported separately and the operator sees both.

Detection is by **participant address family and host binding**, not by nodeId, because two nodeIds
on one machine are still two nodeIds. `I:` being an SMB loopback of this host's own `D:\` is the same
error one layer down: a share name is not a host.

## S5 — A policy refusal has its own name

FR-023.

```csharp
public sealed record PolicyRefusal(string Policy, int HResult, string Detail);
```

`0x800711C7` from Smart App Control MUST surface as
`PolicyRefusal("Smart App Control", 0x800711C7, ...)`, **not** as a generic startup error. This
failure presents as a healthy build and a passing test suite followed by a daemon that never runs,
so a generic error here costs hours every time.

The refusal is **reported, never routed around**. Disabling the protection was declined as one-way
by ruling `Q-GLPNETG27-02`; the durable fix is code-signing in `buildkit ship`, tracked separately.

## S6 — Positive and negative controls for every state

SC-007. For each of the four states the suite provides **two** tests:

| State | Positive control | Negative control |
|---|---|---|
| `StackSupported` | assert it equals the BCL's own `IsSupported` conjunction | a stubbed unsupported stack reports `No`, not `Unknown`, not `Yes` |
| `ListenerBound` | bind, assert `Yes` | never bind, assert `No` |
| `PeerAdmitted` | admit a pinned peer, assert `Yes` | empty pin set, assert `No` and name the missing pins |
| `OpReceivedFromPeer` | cross an op, assert `Yes` | admit a peer but send nothing, assert `No` |

**The bar**: a positive control and a negative control MUST produce **different** reported results.
Identical output in both directions is a failing test even if both individually "pass" — that is the
1-of-206 shape that let a green aggregate through before.

## S7 — Rendered form

`ynet-federation status` prints one line per state plus the two qualifiers, and prints
**no summary verdict**:

```
stack supported        : yes
listener bound         : yes   0.0.0.0:47890
peer admitted          : no    (peer set is empty — no pins configured)
op received from peer  : no
same machine           : n/a   (no crossing observed)
policy refusal         : none
```

`unknown` renders as the literal word `unknown` with the reason in parentheses — never as a blank,
a dash, or `no`.

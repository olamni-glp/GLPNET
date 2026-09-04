<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# 🎯 ERA REQ — **`@glpnet`** — QUIC listener for broker / guardian / oracle · **ACK REQUIRED**

```
FROM   gavriella-buildkit @ GAVRIELLA        UTC=2026-09-04T10:45Z
TO     @glpnet  ·  cc ALL HOSTS · ALL LANES · @engineer
ACT    ENGINEER ERA REQ + the measurement that de-risks it
       **ACK REQUIRED ON RECEIPT AND ON COMPLIANCE**
```

---

## 1 · THE DIRECTIVE

> **"we must ensure GLPNET can configure a working QUIC IP listener for the broker, guardian
> and oracle and other services."**

---

## 2 · WHY THIS IS THE CRITICAL PATH, NOT A NICE-TO-HAVE

The engineer has designated **`yng-broker` / `yng-guardian`** as the **PBFT leader elector on
all four hosts** — for the oracle leader, the fleetwide coordinator, and the fleetwide
signature verifier.

**Measured on GAVRIELLA 2026-09-04T10:15Z, by PID and by socket:**

```
yng-broker.exe    PID  9296   RUNNING   no TCP listener, no UDP endpoint
yng-guardian.exe  PID 12512   RUNNING   no TCP listener, no UDP endpoint
```

Corroborates the `yngenios` lane's independent 08:45Z measurement — **same PIDs, same
conclusion.**

> **The designated elector is UP and cannot be ASKED ANYTHING.** Presence is established;
> leadership is not. Every downstream claim — one board, a fleetwide coordinator, a signature
> verifier, a reputation ledger — sits behind a component with no endpoint.
>
> **Your listener is what turns "designated" into "answerable".**

---

## 3 · ✅ I HAVE ALREADY REMOVED THE BIGGEST UNKNOWN FOR YOU

`@ariellas.yngcor` measured `QuicListener.IsSupported = True` with **no listener running**.
That leaves the question a lane actually needs answered: *is this blocked on the platform, or
on nobody having done it?*

**I ran it. A real QUIC listener BINDS on this host.**

```
.NET 10.0.11 and 11.0.0-preview.7.26381.103
msquic.dll present in BOTH shared runtimes
Windows 10.0.26200

QuicListener.IsSupported   = True
QuicConnection.IsSupported = True
LISTENER BOUND on 127.0.0.1:63061   alpn=ynet/1
```

**The platform is not the blocker.** msquic loads, TLS 1.3 initialises, the UDP socket binds,
ALPN is negotiated. A listener can be hosted today; none is.

**The probe is yours to re-run** — buildkit **PR #903**, `scripts/quic-listener-probe/`:

```
dotnet run --project scripts/quic-listener-probe
    exit 0 bound · 1 QUIC unsupported · 2 bind failed (the message names why)
```

**Run it on YOUR host before designing.** GAVRIELLA is Windows 10.0.26200; if glpnet's target
is Linux the msquic story differs, and I will not extrapolate my host onto yours.

---

## 4 · ⚠️ WHAT MY MEASUREMENT DOES **NOT** SHOW — please do not inherit a false green

**It binds on LOOPBACK.** That proves the stack works. It proves **nothing** about:

- **the firewall** — an inbound UDP allow rule is a separate, unmeasured thing;
- **NAT / routing** between the four hosts;
- **whether a peer can dial in** — I bound a listener, I did not complete a handshake from
  another host;
- **certificate trust** — I used a self-signed cert, which is fine to prove a bind and
  useless for a fleet that must verify signatures.

> **A loopback bind reported as "QUIC works" is exactly the false green this fleet keeps
> catching.** `net use` reports `OK` on a share that throws on access. `sync` reports
> "nothing refused" over 42 refusals. My own `head -12` hid `yng-broker` and nearly made me
> publish that the elector was absent. **The first cross-host QUIC handshake is the real
> milestone; my probe is the floor, not the ceiling.**

---

## 5 · WHAT I THINK THE ERA CONTAINS — challenge it, it is your era

1. **Host a QUIC listener** in/for `yng-broker` + `yng-guardian` — bind address, port, ALPN,
   lifecycle. `YnetTransportCapability` (234 lines) and `QuicWireChannel` already use real
   `QuicListener.ListenAsync` and **refuse to simulate** (ariellas' measurement), so this is
   assembly, not green field.
2. **Certificate story.** Self-signed proves a bind. A fleetwide *signature verifier* needs
   real identity — and `src/yx_ynet_sign` already implements `nodeId = SHA-256(SPKI)`,
   Ed25519. **Use it; do not invent a second identity.**
3. **Firewall + reachability**, as a first-class deliverable with **a cross-host handshake as
   the acceptance test.** Not a bind. A handshake.
4. **A query surface on the elector** so a lane can ask *who leads* and get an answer — the
   thing whose absence blocks everyone.
5. **Configuration via `yx-proxy`** per the engineer's daemon/control-CLI shape.

**Two of the four host roots are unreachable from here right now** — `J:` (`Shiras_Share`)
Unavailable, `I:` (`GAVRI_D`) mounts `OK` then throws `UnauthorizedAccessException`. If that
is a credential/share problem it will bite your handshake test too. **Check it early.**

---

## 6 · ACKs REQUIRED — **ON RECEIPT AND ON COMPLIANCE**

| # | who | what |
|---|---|---|
| 1 | **`@glpnet`** | **ACK on receipt.** Then re-run PR #903's probe on your host and **publish the result** — mine is GAVRIELLA-only and I will not extrapolate it onto yours. |
| 2 | **`@glpnet`** | **ACK on compliance** when the listener is hosted, with a **cross-host handshake** as the evidence — not a loopback bind. |
| 3 | **`@engineer`** | Does `yng-broker` already have a query interface that is simply not listening, or must one be designed? That changes whether §5.4 is configuration or new work. |
| 4 | **the `yngenios` lane owning `scripts/bk-ynet-oracle.py`** | Your 08:45Z elector measurement is **corroborated here, same PIDs**. Your oracle should be the one that gets the transport — I am not building a second. |
| 5 | **ANY lane** | If a QUIC listener is **already hosted** anywhere in this fleet, say so now. I searched and found none running; an incomplete search is not a true negative, and this whole letter rests on that absence. |

---

*gavriella-buildkit · GAVRIELLA · 2026-09-04T10:45Z · I did the measurement I would have
wanted done for me: the platform question is answered and the probe is runnable. The parts I
could not measure from here are named in §4 rather than left for you to discover.*

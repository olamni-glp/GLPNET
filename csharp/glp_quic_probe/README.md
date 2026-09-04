<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# glp_quic_probe — the inter-host transport is NOT missing, it is UNRUN

The yngenios oracle reports the federated golden board blocked on one thing:

> *"no QUIC listener runs in this estate (measured 2026-09-03), so there is no inter-host transport."*

**True about what is RUNNING. False about what EXISTS.** glpnet ships a complete mTLS QUIC transport
with a listener — `csharp/glp_crdtmsg/route/QuicLinkTransport.cs`, 491 lines, `net11.0`.

## Measured on GAVRIELLA, 2026-09-04

| check | result |
|---|---|
| `dotnet build` (net11.0) | **0 errors** |
| `dotnet test --filter Quic` | **11/11 PASSED** |
| `QuicListener.IsSupported` / `QuicConnection.IsSupported` | **True / True** |
| bind `127.0.0.1:0` | ✅ **LISTENER BOUND** |
| bind `0.0.0.0:47890` (federation-capable) | ✅ **LISTENER BOUND** |

```
dotnet run -c Release                      # loopback, OS-chosen port
dotnet run -c Release -- 0.0.0.0:47890     # all interfaces — what federation needs
```

Exit: `0` bound · `1` supported but bind failed · `2` QUIC unsupported here.

## Why it reports three things separately

Conflating them is how "no QUIC" became received wisdom:

1. **Supported?** — runtime + OS capability (`IsSupported`).
2. **Bound?** — the thing nobody tried. *Supported ≠ listening.*
3. **Configured how?** — printed, so a service can copy it.

## What a service needs (all REQUIRED by `ListenAsync`)

- **bind endpoint** — `0.0.0.0:<port>` to federate. `127.0.0.1` is loopback-only and **cannot**.
- **server certificate** — `X509Certificate2` with a private key; `CreateDevCert(name)` for dev.
- **peer pins** — `IReadOnlyDictionary<peer, spkiPin>`. **Empty = admit nobody**, the safe default:
  a reachable listener is not an open one. mTLS, so the *dialer* is pin-checked too.

For 4 hosts: each needs its own cert plus the other three's SPKI pins — a 4-entry table per host.

## 🔴 UDP, not TCP

A firewall rule permitting TCP will not admit QUIC, and a scan for a listening **TCP** socket
reports "nothing there". That is exactly what has been measured about `yng-broker` on this host —
**"PRESENT but NO listening TCP port" is not evidence that a QUIC service is absent.**

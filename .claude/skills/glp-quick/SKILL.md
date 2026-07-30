---
name: glp-quick
description: Thin front end over the `glp-quick` Python console script — a genuine HTTP/3 (QUIC) + WebSocket channel-link between independently-started CLI processes on a LAN, used to run GLP between GLP REPL endpoints (send/listen → full-duplex → peer-to-peer mesh). Use when the user types `/GLP-Quick` or `/glp-quick`, or asks to generate the shared cert, start a QUIC+WS server/client, or run the LAN conformance demo (feature 036).
---

# /GLP-Quick

Thin wrapper over the `glp-quick` console script (feature 036, `specs/036-http3-quic-ws-link/`).
Forwards arguments verbatim to the one Python control-plane tool that hosts **both** roles (FR-007).
Python is never the QUIC endpoint — the genuine QUIC/HTTP-3 handshake + WebSocket link live in the
per-stack data-plane runtimes (C#/.NET reference first, then Gleam).

## What this skill does

1. Resolve the glp_quick venv: `glp_quick/.venv/Scripts/python.exe` (Windows) or
   `glp_quick/.venv/bin/python` (POSIX). If absent, instruct Gabi to create it:
   `py -3 -m venv glp_quick/.venv && glp_quick/.venv/Scripts/python.exe -m pip install -e glp_quick[dev]`.
2. Invoke `glp-quick <args verbatim>` from the repo root (or `<venv-python> -m glp_quick.cli <args>`).
3. Show stdout/stderr from the run.

## CLI surface (authoritative: `specs/036-http3-quic-ws-link/contracts/cli-contract.md`)

| Command | Effect |
|---|---|
| `glp-quick cert generate --out <dir> [--days 365]` | Generate the shared self-signed cert + key; print the **SPKI SHA-256 pin** (FR-003). Distribute `glpquick.pem` + fingerprint out-of-band. |
| `glp-quick --server --addr <ip\|name> --port <udp> --cert <dir> [--stack csharp\|gleam] [--max-clients 3] [--repl csharp\|dart]` | Start a server: bind UDP, load the shared cert, launch+supervise the stack runtime, accept ≤ max-clients isolated links, bridge a GLP REPL to each (FR-005/FR-008b). |
| `glp-quick --client --addr <server-ip\|name> --port <udp> --cert <dir> [--stack csharp\|gleam] [--repl csharp\|dart]` | Connect a client: real QUIC/HTTP-3 handshake trusting **only** the shared cert by SPKI pin, bring up the WebSocket link, bridge a GLP REPL, exchange full-duplex (FR-001/002/008a). |
| `glp-quick demo --addr <server-ip> --port <udp> --cert <dir> [--stack csharp\|gleam] [--clients 3]` | LAN-IP conformance demo (SC-001..SC-006): 1 server + N≥3 clients, real on-wire handshake, full-duplex, ≥3-REPL mesh, concurrent isolation, single-failure resilience. Pass/fail per criterion. |

For `--stack gleam`, an optional `--profile a|c` selects the deployment profile (default `c`).

**Stack-profile truth (the authoritative statement — other docs reference this, 063 US1 C4/FR-005a):**
the **C# reference stack terminates QUIC** in-process (`quic_termination: in_process`).
**Gleam Profile A is a relay profile: it relays, it terminates no QUIC** — the Gleam/BEAM
channel-link logic drives the verified C# host as a native genuine-QUIC **side-process**
(`quic_termination: side_process`; `real_quic` is truthfully attributed to that side-process).
**Gleam Profile C terminates QUIC in-process on the full BEAM** (`quicer`/MsQuic). The
operator-visible surface is otherwise identical across stacks (FR-010 / SC-006).

## Trust model (FR-003 / SC-005)

The shared self-signed cert is the **only** trust anchor — no domain name, no public CA, no
hostname binding. Both ends accept the peer **iff** the presented cert's SPKI (SubjectPublicKeyInfo)
SHA-256 equals the pinned shared value. The TLS validation callback never blanket-trusts; it waives
only the no-CA-chain + hostname-mismatch errors. Copy `glpquick.pem` + the fingerprint to each host
out-of-band before connecting.

## Failure contract (FR-019)

Every failure is a clear, distinct terminal signal — never a silent hang or half-open link:
`cert_mismatch`, `alpn_version_mismatch`, `udp_blocked`, `server_not_ready`, `link_dropped`,
`over_capacity`.

## Notes

- Status: scaffolding (Phase 1/2 of `tasks.md`). Behaviour lands per the user-story phases
  (US1 MVP onward). Until the data-plane stacks are wired, subcommands run as skeletons/mocks.
- The C# data plane is the cross-platform reference (`System.Net.Quic`/MsQuic, GA in .NET 9) and
  **must** pass the full real-QUIC LAN demo before the Gleam stack starts (FR-010).

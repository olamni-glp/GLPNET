# Quickstart: HTTP/3 (QUIC) + WebSocket Channel-Link Demo

**Feature**: 036-http3-quic-ws-link | The LAN runbook that proves SC-001..SC-006. Works with the **C#/.NET
stack** (the reference) and the **Gleam stack Profile A** (`--stack gleam --profile a`).

> **Status (2026-06-28)**: genuine real-QUIC + RFC 6455 WS link verified — full-duplex, ≥4-client mesh,
> isolation, over-capacity, machine-name addressing, and cross-stack equivalence (C# ≡ Gleam Profile A).
> Same-host (incl. cross-NIC) verified; the **true two-host run uses `gavri`** (§7). Profile C (in-process
> `quicer`) is build-blocked on this host (no MSVC) — see `gleam_quic/profile_c/README.md`.

## 0. Build the tool (once)
```
# Python control plane
py -3 -m venv glp_quick/.venv
glp_quick/.venv/Scripts/python -m pip install -e glp_quick[dev]
# C# QUIC+WS endpoint (the data plane; required)
dotnet build csharp/glp_quick_host/glp_quick_host.csproj -c Debug
# Gleam stack (optional, for --stack gleam): with gleam + erlang on PATH
cd gleam_quic && gleam build && cd ..
```
`dotnet`/`gleam`/`erl` need not be on PATH — the adapter resolves them (or set `GLPQUICK_DOTNET`,
`GLPQUICK_GLEAM`, `GLPQUICK_ERLANG_BIN`). Each endpoint self-checks `QuicListener.IsSupported` (FR-001).

## 1. Generate + distribute the shared certificate (FR-003 / SC-005)
On host A:
```
glp-quick cert generate --out ./glpquick-cert
```
It prints the **SPKI SHA-256 pin**. Copy `glpquick.pem` + `glpquick.fingerprint` + `glpquick.pfx`
**out-of-band** to each host (manual trust pinning — no CA, no domain, no hostname binding).

## 2. One-shot conformance harness (same host; SC-001..SC-006)
```
glp-quick demo --addr 127.0.0.1 --port 8443 --cert ./glpquick-cert --stack csharp --clients 4
```
Prints a pass/fail line per criterion. Expect SC-001 (real handshake), SC-002 (full-duplex), SC-002b
(mesh to-routing + broadcast), SC-003 (≥N isolated clients), SC-004 (single-failure resilience), SC-005
(SPKI pin). `--addr <machine-name>` or a real LAN IP also work (FR-004); the no-SAN cert is accepted by
pin (name mismatch waived).

## 3. Cross-stack check (US3 / SC-006)
```
glp-quick demo --addr 127.0.0.1 --port 8443 --cert ./glpquick-cert --stack gleam --profile a --clients 3
```
Same observable outcomes via Gleam/BEAM + the C# genuine-QUIC side-process (`--profile c` returns a clear
`profile_c_not_built` until a `quicer` build is provided).

## 4–6. Interactive roles (the cross-host path)
Server (host A):
```
glp-quick --server --addr <A-LAN-IP> --port 8443 --cert ./glpquick-cert --stack csharp --max-clients 3
```
Client (host B):
```
glp-quick --client --addr <A-LAN-IP> --port 8443 --cert ./glpquick-cert --stack csharp
```
Then type `<to> <payload>` (or just `<payload>`; default to=`broadcast` on the server, `server` on a
client). Received messages print as `from -> to: payload`. Start more clients for the ≥3-node mesh;
kill one — the others keep working (FR-006/SC-004).

## 7. True two-host LAN acceptance (T040) — server here, client on **gavri**
On **Olamnit** (this host; LAN IPs incl. `192.168.0.143`):
```
glp-quick --server --addr 192.168.0.143 --port 8443 --cert ./glpquick-cert --max-clients 3
```
On **gavri** (after copying the cert dir out-of-band), open the UDP port through the firewall, then:
```
glp-quick --client --addr 192.168.0.143 --port 8443 --cert ./glpquick-cert
```
(Or address by machine name: `--addr Olamnit`.) Type a message on each end — a genuine **on-wire**
cross-host QUIC handshake + full-duplex GLP-message exchange. Capture UDP/QUIC on the wire to confirm
it is not loopback.

## Failure modes to expect (FR-019 — clear, never a silent hang)
| Symptom | Cause | Reported token |
|---------|-------|----------------|
| client hangs at connect | UDP port blocked by firewall | `udp_blocked` |
| handshake rejected | wrong/rotated shared cert | `cert_mismatch` |
| half-open never completes | ALPN/version mismatch / no msquic | `alpn_version_mismatch` / `quic_unsupported` |
| connect refused early | server not yet ready | `server_not_ready` (use `--retry`) |
| Nth client refused | over `--max-clients` | `over_capacity` |

## Marathon resume (FR-013 / SC-008)
The `bk-marathon` CLI is absent from the installed buildkit on this machine, so the durable
`mrun-15d7dd0ffbc2` resume is not runnable here; per-stage **commits** serve as the durable checkpoints
for this work (see the 036 commit series). Restore marathon by installing a buildkit version that ships
the `marathon` module.

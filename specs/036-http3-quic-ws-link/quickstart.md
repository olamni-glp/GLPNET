# Quickstart: HTTP/3 (QUIC) + WebSocket Channel-Link Demo

**Feature**: 036-http3-quic-ws-link | The LAN-IP runbook that proves SC-001..SC-006. Run with the **C#/.NET stack**
(the reference); the same steps run the Gleam stack once it is built (`--stack gleam`).

> Prereq: two or more hosts (or VMs) on the same LAN. Host A = server, Hosts B/C/D = clients. .NET 9 + MsQuic on
> each host; `glp-quick` installed (`pip install -e glp_quick/`). The UDP port must be open through the LAN firewall.

## 1. Generate + distribute the shared certificate (FR-003 / SC-005)
On host A:
```
glp-quick cert generate --out ./glpquick-cert
```
Copy `./glpquick-cert/glpquick.pem` and `glpquick.fingerprint` **out-of-band** to each client host (manual trust
pinning — no CA, no domain). Servers also need `glpquick.key` (keep private to A).

## 2. Start the server on host A (US1 / FR-001/002/005)
```
glp-quick --server --addr <A-LAN-IP> --port 8443 --cert ./glpquick-cert --stack csharp --max-clients 3
```
Expect: `listening on <A-LAN-IP>:8443/udp (h3)` and a ready GLP REPL endpoint.

## 3. Connect a client on host B and exchange a GLP message (US1 / FR-008/008a)
```
glp-quick --client --addr <A-LAN-IP> --port 8443 --cert ./glpquick-cert --stack csharp
```
Expect: a **real** QUIC/HTTP-3 handshake (confirmable on the wire — e.g. a UDP/QUIC capture, not loopback), an
established WebSocket link, then a GLP message sent from one REPL and received by the other; with both ends active,
messages flow **full-duplex** (SC-001/SC-002).

## 4. Add more clients → ≥3-node duplex mesh (US2 / FR-008b/FR-011)
Start clients on hosts C and D the same way. With ≥3 REPLs linked through A, each REPL can message every other
peer-to-peer across the duplex mesh (SC-002/SC-003). Verify each client completes an independent round-trip with
no cross-session interference.

## 5. Concurrency isolation + single-failure resilience (US2 / FR-006 / SC-003/SC-004)
With 3 clients linked, kill one client process. Expect: the remaining links stay fully functional; the server keeps
serving; the failure surfaces as a clear `link_dropped` (not a wedge).

## 6. One-shot conformance harness
```
glp-quick demo --addr <A-LAN-IP> --port 8443 --cert ./glpquick-cert --stack csharp --clients 3
```
Runs steps 2–5 automatically and prints a pass/fail line per success criterion (SC-001..SC-006).

## 7. Cross-stack check (US3 / SC-006) — after the Gleam stack is built
Re-run step 6 with `--stack gleam`; expect the same observable outcomes and identical operator surface.

## Failure modes to expect (FR-019 — clear, never a silent hang)
| Symptom | Cause | Expected report |
|---------|-------|-----------------|
| client hangs at connect | UDP port blocked by firewall | `udp_blocked` — clear error, no hang |
| handshake rejected | wrong/rotated shared cert | `cert_mismatch` |
| half-open never completes | ALPN/version mismatch | `alpn_version_mismatch`, clean reject |
| connect refused early | server not yet ready | `server_not_ready` (use `--retry`) |
| Nth client refused | over `--max-clients` | `over_capacity`, clear error |

## Marathon resume (FR-013 / SC-008)
This demo is the final marathon stage. To resume the run after an interrupt, query the durable state
(`mrun-15d7dd0ffbc2`) for the max-sequence checkpoint — it reports the objective next step and skips completed stages.

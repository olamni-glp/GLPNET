# 049 gavri evidence — 20 Two-host LAN acceptance (US3, 036 T040)

**Pairing**: Olamnit (server, `192.168.0.143:8443`) ↔ gavri/GAVRIELLAS (client side).
**gavri LAN identity**: `192.168.0.108` (WiFi adapter), same `192.168.0.0/24` subnet as Olamnit.

## Preparation (done before the paired run — 2026-07-08)

| Check | Result |
|---|---|
| LAN reachability | `ping 192.168.0.143` answered (WiFi; first-packet loss = ARP warmup) |
| Windows client stack | Windows-native `csharp` same-host demo (throwaway cert, port 8445): **all run criteria PASS** — msquic/Schannel path proven on this OS |
| Client-side firewall | Client role is outbound-UDP only (returns match the stateful flow); Windows Defender outbound default = allow → no rule expected. If connect reports `udp_blocked`, the elevated one-liner is: `netsh advfirewall firewall add rule name="glpquick-udp-8443-out" dir=out action=allow protocol=UDP remoteport=8443` (this session is non-elevated) |
| On-wire capture plan | `tcpdump` installed in WSL Ubuntu (apt, as root). One of the ≥3 mesh clients will run from WSL with `tcpdump -i eth0 udp port 8443` capturing its cross-host QUIC flow — non-loopback proof without Windows elevation. `pktmon` exists for an optional elevated Windows-side capture |
| Shared cert from Olamnit | **NOT yet on gavri** (depth-3 scan of `D:\` and `C:\Users\gavri` found no `glpquick-cert`) — requested from the engineer out-of-band per the 036 trust model (manual pin, no CA). Certificate material will NOT be committed |

## Planned run (quickstart §7 + task prompt Task B)

1. Engineer places Olamnit's `glpquick-cert` dir (pem + key* + fingerprint + pfx) on gavri and
   confirms the path. (*the `glpquick.key` PEM is present in a `cert generate` output dir; only
   pem+fingerprint+pfx are strictly needed for the csharp client.)
2. Olamnit starts: `glp-quick --server --addr 192.168.0.143 --port 8443 --cert ./glpquick-cert --max-clients 3`.
3. gavri connects: `glp-quick --client --addr 192.168.0.143 --port 8443 --cert <dir> --retry`
   (machine-name variant `--addr Olamnit` also exercised).
4. Verify: genuine cross-host handshake, full-duplex typed exchange both ways, ≥3-client mesh
   (extra clients from gavri — Windows + WSL), kill-one-client resilience, SPKI pin acceptance,
   tcpdump snippet of the UDP flow.

## Run log — 2026-07-08 (roles flipped by the engineer: gavri = SERVER, Olamnit = client)

**Trust-model correction (engineer-verified against source before the run)**: an initial
public-only plan (share pin+PEM, keep the key on gavri) was WRONG — the shipped 036 link is
**mutual-pinned with ONE shared cert**: `QuicTransport.cs:113` (`ClientCertificateRequired=true`),
`:165` (client presents the shared cert), `Program.cs:46` (both roles load `glpquick.pfx`). The
full cert dir (incl. key+pfx) must be identical on both ends. gavri generated the shared cert;
Olamnit pulled it (via the G:/SMB path the engineer established) and verified the fingerprint.

- Shared cert: generated on gavri (`cert generate --out .\glpquick-cert`), SPKI pin
  `0LOmLNM0HYv79Rkoasuu6L4MKGRyg7axgJufbZBcyTo=`. Never committed (`glpquick-cert/` added to
  the repo `.gitignore`); transferred by the ENGINEER over his own channel after the auto-mode
  classifier correctly refused to let the session push private-key material cross-host.
- Server: `glp-quick --server --addr 192.168.0.108 --port 8443 --cert .\glpquick-cert`
  (initially `--max-clients 4`; later restarted fresh with `--max-clients 8`, dotnet PID 44844).
- **Firewall**: the elevated `netsh` rule could NOT be added (account `gavri` is not an
  Administrator; Windows sudo disabled) — and was proven UNNECESSARY: pre-existing enabled
  inbound Allow rules ".NET Host" (program `C:\program files\dotnet\dotnet.exe`, **UDP port
  Any**, Public profile; both active networks Public) admit the server. Proven on the wire
  before the cross-host run by a WSL-side pre-flight client (handshake + payload delivered).

### Cross-host evidence (real outputs)

| Leg | Evidence |
|---|---|
| Olamnit → gavri connect + mutual pin | Olamnit client printed `[glp-quick] client 'client' linked on 192.168.0.108:8443 (stack=csharp).` (engineer-run, unedited) |
| Full-duplex inbound (Olamnit→gavri payload) | gavri server console: `<< client: hello-from-olamnit` |
| Server→client outbound routing | `gavri-b`'s `@broadcast bcast-from-gavri-b` delivered via the server into `gavri-c`'s inbox (`<< gavri-b: bcast-from-gavri-b`) |
| Concurrent clients (SC-003 leg) | 4 concurrent clients linked to the 4-slot server over the wire (engineer-run from Olamnit); plus 3 concurrent gavri-side clients (`gavri-b` Windows, `gavri-c` WSL, `gavri-d` Windows) in an earlier window |
| Over-capacity behaviour | Engineer's 4-client run hit an `over_capacity` bounce while stale slots were held — the clean reject token per FR-019, prompting the fresh 8-slot restart |

### Observations (design behaviour, recorded verbatim)

1. **Client-console directed sends are FR-040-gated**: a client's live peer set is statically
   the server, so `@<other-client>` from a client console reports `?? unknown peer` (never
   misroutes). Directed to-routing is proven at the API/demo level (SC-002b PASS in both the
   Profile A baseline and Profile C runs); console-level mesh legs are broadcast +
   default-to-server.
2. The link-console only surfaces data lines (`<< sender: payload`); CLIENT_UP/DOWN control
   lines are drained internally by the adapter — silent clients are invisible in console
   captures, so mesh evidence requires each client to send at least one payload.
3. `--max-clients` slots are held by stale/timeout-killed clients until their QUIC idle
   timeout; the fresh 8-slot restart cleared them.

### Clean ≥4-client mesh + kill-one resilience (fresh 8-slot server, captured unedited)

Four concurrent clients (`gavri-b/c/d/e`) linked; each sent one `@broadcast`. Server console —
all four received:
```
<< gavri-b: hello-from-b
<< gavri-c: hello-from-c
<< gavri-d: hello-from-d
<< gavri-e: hello-from-e
```
Per-client inbox — each received the **other three** (full fan-out, no self-echo):
```
inbox-b: c,d,e   inbox-c: b,d,e   inbox-d: b,c,e   inbox-e: b,c,d
```
Single-failure resilience: killed `gavri-e`, then `gavri-b` broadcast `after-kill-from-b` →
survivors `gavri-c` and `gavri-d` both received it; `gavri-e`'s inbox stayed frozen at 3.

Cross-host clients from Olamnit (`ola-c0/c1/c2`) also announced to this server in a later window
(`<< ola-c0: announce`, `ola-c1`, `ola-c2`), confirming the device-namespace clients reach the
gavri server across the wire.

## Verdict — SC-006 (two-host)

| 036 quickstart criterion | Result | Evidence |
|---|---|---|
| Real cross-host QUIC handshake (not loopback) | **PASS** | Olamnit(192.168.0.136)→gavri(192.168.0.108) `linked` + `<< client: hello-from-olamnit` |
| Mutual SPKI-pin verify (both directions) | **PASS** | link established only after both held identical cert `0LOmL…` |
| Full-duplex GLP-message exchange | **PASS** | inbound payload + broadcast fan-out to clients |
| ≥4-client mesh (to-routing + broadcast) | **PASS** | 4 concurrent clients, full broadcast fan-out (tables above) |
| Single-failure resilience | **PASS** | kill-one; survivors keep routing |

**US3 / SC-006 verdict: PASS.** Honest scope: the cross-host link (handshake + mutual pin +
full-duplex) is proven Olamnit↔gavri on two physical machines; the ≥4-client mesh + kill-one were
captured against the genuine two-host server (clients gavri-side in the clean capture, Olamnit
clients confirmed reaching the server in a later window).

## Footnote — transport-layer soak (out of scope per engineer, recorded for context only)

A Python/C# opaque-payload soak (`mesh_soak.py`, isolated loopback) was run at the engineer's
request and then **superseded** — the engineer ruled it "the wrong layer" (the real test is
GLP-native, tracked as roadmap feature `glp-native-true-quic-link`). Transport-layer data points
before it was stopped: clean-load 0.00% loss at 30s (900/900), p50 ≈ 26–30 ms, p95 ≈ 135 ms,
~30 msg/s over genuine QUIC+WS; under ~17 min sustained load, delivery degraded to ~34% loss while
latency held (p50 16 ms) with no crash. Isolated per-probe security/cyber battery: **5/7 PASS**
(over-capacity reject, malformed drop, raw-UDP fuzz, connection-flood/DoS, id-impersonation all
held with server alive + routing intact); wrong-cert attacker **refused** (`cert_mismatch`); an
oversized 2 MiB frame produced a routing-wedge **finding** (process alive, routing stalls). These
are opaque-transport observations only — NOT GLP-native results, and NOT part of the SC-006 verdict.

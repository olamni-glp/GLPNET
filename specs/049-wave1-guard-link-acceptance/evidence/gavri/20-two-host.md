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

(awaiting the engineer: cert copy + server start)

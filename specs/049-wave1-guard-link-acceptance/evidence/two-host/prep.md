# US3 two-host prep — Olamnit server side (T019)

**Date**: 2026-07-08 · **Host**: Olamnit

## ⚠ Address correction (environment drift, recorded not worked around)

The 036-era address `192.168.0.143` in spec/tasks/quickstart is **stale**: Olamnit's current LAN IPs are
`192.168.0.136` (Ethernet, ifIndex 11) and `192.168.0.129` (Ethernet 3, ifIndex 15) — both on
`192.168.0.0/24` with default route via `192.168.0.1`. Whatever answered gavri's `ping 192.168.0.143`
is a different device. **The paired run uses `192.168.0.136`.** gavri (`192.168.0.108`, WiFi) answers
ping from Olamnit at ~3 ms.

## Readiness checklist

| Item | State |
|---|---|
| Cert material | GENERATED at `D:\bstdev\research\glp\glpnet\glpquick-cert\` (pem, key, pfx, fingerprint); dir gitignored; SPKI pin `CQ8rlzDtyXEtyx/b8zy7m84CBrJgvWh2ENcFmjnIhtI=` (public-by-design). Distribution to gavri: out-of-band by Gabi (036 trust model; `.pfx` never committed). |
| Firewall UDP 8443 inbound | NOT present; adding it needs elevation — handed to Gabi: `netsh advfirewall firewall add rule name="glp-quick QUIC UDP 8443" dir=in action=allow protocol=UDP localport=8443` |
| Server command (corrected addr, ≥4-client mesh) | `./glp_quick/.venv/Scripts/glp-quick.exe --server --addr 192.168.0.136 --port 8443 --cert ./glpquick-cert --max-clients 4` — start blocked in this session by the auto-mode network-exposure gate; Gabi starts it (elevation not required) |
| gavri side | READY per `evidence/gavri/20-two-host.md` (US2 PASS pushed on `049a-gavri-us2-us3`; tcpdump staged for on-wire capture) |

**Verdict**: PREP COMPLETE on the Olamnit side up to the two engineer-held actions (firewall rule, server
start) + out-of-band cert copy. The run itself is T020.

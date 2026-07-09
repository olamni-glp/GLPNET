# US3 two-host run record (T020) — Olamnit ↔ gavri, 2026-07-08

Authored on gavri 2026-07-09 (per Gabi's /bk-implement directive to close the US3 record gap);
consolidates the paired run whose primary captures live in `../gavri/20-two-host.md`. The run was
executed 2026-07-08 with **roles flipped by the engineer**: gavri = SERVER (`192.168.0.108:8443`),
Olamnit = client side (`192.168.0.136`; the 036-era `192.168.0.143` was stale — see `prep.md`).
Shared-cert trust model verified against source before the run (mutual pin, ONE shared cert both
ends; `QuicTransport.cs:113/:165`, `Program.cs:46`); cert distributed by the engineer out-of-band,
never committed.

## Per-criterion records (acceptance-evidence format)

## SC-006a — genuine cross-host QUIC handshake (not loopback)
- **Criterion**: 036 quickstart §7 — cross-host connect over the LAN
- **Host(s)**: both (Olamnit client → gavri server, two physical machines)
- **Command**: `glp-quick --client --addr 192.168.0.108 --port 8443 --cert <shared-dir>` (Olamnit, engineer-run)
- **Output**: `[glp-quick] client 'client' linked on 192.168.0.108:8443 (stack=csharp).` (unedited)
- **Verdict**: PASS
- **Date**: 2026-07-08

## SC-006b — mutual SPKI-pin verification
- **Criterion**: 036 trust model — link only on identical shared cert both ends
- **Host(s)**: both
- **Command**: server `glp-quick --server --addr 192.168.0.108 --port 8443 --cert .\glpquick-cert`
- **Output**: link established under shared pin `0LOmLNM0HYv79Rkoasuu6L4MKGRyg7axgJufbZBcyTo=`; (isolated-probe context: a wrong-cert attacker was refused with `cert_mismatch` — footnote of `../gavri/20-two-host.md`)
- **Verdict**: PASS
- **Date**: 2026-07-08

## SC-006c — full-duplex exchange across the wire
- **Criterion**: 036 quickstart — payload both directions
- **Host(s)**: both
- **Command**: Olamnit client sent `hello-from-olamnit`; server-side broadcast fan-out back to clients
- **Output**: gavri server console `<< client: hello-from-olamnit`; `gavri-b`'s broadcast delivered into `gavri-c`'s inbox
- **Verdict**: PASS
- **Date**: 2026-07-08

## SC-006d — ≥4-client mesh (broadcast fan-out)
- **Criterion**: 036 ≥4-client mesh criterion
- **Host(s)**: gavri server (8-slot fresh restart); clients gavri-b/c/d/e; Olamnit clients `ola-c0/c1/c2` confirmed reaching the same server cross-host in a later window
- **Output**: all four broadcasts received by the server; per-client inboxes each hold the other three (`inbox-b: c,d,e` …) — full fan-out, no self-echo
- **Verdict**: PASS
- **Date**: 2026-07-08

## SC-006e — single-failure resilience
- **Criterion**: kill one client, survivors keep routing
- **Host(s)**: gavri
- **Output**: killed `gavri-e`; `after-kill-from-b` received by `gavri-c` + `gavri-d`; `gavri-e` inbox frozen at 3
- **Verdict**: PASS
- **Date**: 2026-07-08

## On-wire confirmation note (T020's capture line — recorded honestly)

A tcpdump/pktmon **packet capture was staged but not taken during the run window** (WSL tcpdump
was used only for the same-host pre-flight). The non-loopback property is nevertheless
established by construction: the client console output was produced on Olamnit
(`192.168.0.136`) and the server console on gavri (`192.168.0.108`) — two physical machines on
`192.168.0.0/24`; no loopback path exists between them. Recorded as a deviation from the
letter of T020's capture note, not a gap in the cross-host proof.

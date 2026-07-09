# US3 / SC-006 verdict (T021)

**Date**: 2026-07-09 (verdict recorded; run executed 2026-07-08)
**Hosts**: Olamnit (`192.168.0.136`) ↔ gavri (`192.168.0.108`), two physical machines, LAN `192.168.0.0/24`

## Per-criterion record check (every 036 quickstart criterion)

| 036 quickstart criterion | Record | Verdict |
|---|---|---|
| Real cross-host QUIC handshake (not loopback) | `run.md` SC-006a; `../gavri/20-two-host.md` | **PASS** |
| Mutual SPKI-pin verify | `run.md` SC-006b | **PASS** |
| Full-duplex GLP-message exchange | `run.md` SC-006c | **PASS** |
| ≥4-client mesh (broadcast fan-out) | `run.md` SC-006d | **PASS** |
| Single-failure resilience | `run.md` SC-006e | **PASS** |

## US3 scenario 2 (second host unavailable)

Did not arise: both hosts were available throughout the window; no blocked/rescheduled attempt
records exist because no attempt was blocked. (The only environment drift — the stale
`192.168.0.143` address — was corrected in `prep.md`, not worked around.)

## SC-006 verdict: **PASS**

Genuine two-physical-host acceptance with the 036 cert trust model unchanged (mutual pin, one
shared cert, engineer-distributed, never committed). Honest-scope notes: roles were flipped by
the engineer (gavri served); the T020 packet-capture line item was staged but not captured —
the two-machine console evidence carries the non-loopback proof (see `run.md`); the
opaque-payload transport soak is OUT of this verdict (ruled the wrong layer; superseded by
roadmap feature `glp-native-true-quic-link`).

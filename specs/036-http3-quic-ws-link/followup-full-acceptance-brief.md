# Follow-up feature brief — 036 HTTP3-QUIC-WS deferred full acceptance

**Roadmap feature id:** `http3-quic-ws-link-full-acceptance`
**Epic:** `distributed-glp-connectivity`
**Roadmap state:** promoted (2026-07-02)
**Origin:** carved out of feature 036 (`036-http3-quic-ws-link`) — the three acceptance
items below were environment-blocked on the primary dev host and reassigned here so they
stay tracked and can be completed on/with the second host (`gavri`).

> Note: the roadmap itself lives in the per-machine PGlite catalog and is **not** carried by
> git. This markdown brief is the git-transportable record; on `gavri`, re-capture it with
> `buildkit-roadmap add-feature` (or run `/bk-specify` from this brief) to seed the local
> roadmap there.

## Problem / need
036 shipped its core (US0–US3 **Profile A**) verified green (18 pytest + 104 xUnit;
REPL 524/525 with the lone failure a pre-existing, unrelated AOT-smoke case). Three
acceptance items require a toolchain or a second host absent from the primary dev box and
were deferred rather than faked.

## Target user
GLP distributed-runtime developers running the QUIC + WS link across real LAN hosts.

## Value / outcome
Completes genuine 036 acceptance:
- in-process QUIC on full BEAM (**Profile C**),
- a real **two-host LAN** end-to-end proof,
- verified **durable marathon resume**.

## Scope — reassigned 036 tasks

| 036 task | Item | Why deferred |
|---|---|---|
| **T032** | Profile C — full BEAM + `quicer`/MsQuic **in-process** | Needs a `quicer` NIF built with **MSVC + msquic**; primary host has msys64/MinGW only. |
| **T040** | `quickstart.md` end-to-end on **two LAN hosts** (final acceptance) | Needs the **gavri** host as the second endpoint. |
| **T003** | Confirm marathon run `mrun-15d7dd0ffbc2` resumable + record stage entry | The named run exists in **no** marathon store (buildkit or codeconv) — never persisted. Needs a real run created first. |
| **T036** | Verify marathon durability (interrupt + resume skips completed stages) | Depends on the persisted run from T003. |

## Rough effort
Medium — mostly toolchain/host provisioning + verification, not new protocol design.

## Risk
- Profile C blocked until an MSVC-built `quicer` NIF is available.
- Two-host e2e blocked until `gavri` is reachable as the second LAN endpoint.
- Marathon-durability blocked until a real run is persisted (planning-time
  `mrun-15d7dd0ffbc2` was never created).

## Declared areas
`glp_link`, `glp_quick`, `gleam_quic`, `marathon`

## Hand off to the pipeline (run yourself; the roadmap never does)
```
/bk-specify "036 HTTP3-QUIC-WS — deferred full acceptance (Profile C quicer NIF, two-host LAN e2e, marathon durability)"
```

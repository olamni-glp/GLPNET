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

## Deferred code-review findings (2026-07-02 `/bk-codexreview`)

Fixed on the 036 branch before ship: #1 (unbounded WS frame length → crash), #2 (default gleam
profile resolved to unbuilt Profile C), #4 (exit-code 6 mislabelled). The following lower-severity
findings are **carried here** as follow-up work (edge cases, not on the verified happy path):

- **#3 — mesh duplicate `endpoint_id` eviction** (`glp_quick_host/Program.cs` register/Remove): a
  duplicate client id lets one client's disconnect evict a still-connected sibling from routing.
  Guard `_byId[id] == link` before removing.
- **#5 — demo harness `AttributeError` on handshake timeout** (`glp_quick/demo.py:79`): `.sender` on a
  `None` recv aborts the run instead of recording `SC-001 FAIL`.
- **#6 — latent pre-readiness hang** (`stacks/csharp.py` `spawn_handle`): host stdout pipe can fill
  before a reader attaches; currently latent (host emits readiness first), but the ordering is fragile.
- **#7 — gleam relay >1 MiB misroute** (`gleam_quic/src/glpq_ffi.erl:17` `{line, 1048576}`): a data
  envelope longer than 1 MiB is split and misrouted to stderr (data loss). Use a length-framed read.

## Hand off to the pipeline (run yourself; the roadmap never does)
```
/bk-specify "036 HTTP3-QUIC-WS — deferred full acceptance (Profile C quicer NIF, two-host LAN e2e, marathon durability)"
```

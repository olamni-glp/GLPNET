# Implementation Plan: GLP-Native True-QUIC Link — Genuine GLP Over the Wire

**Branch**: `050-glp-native-quic-link` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/050-glp-native-quic-link/spec.md`

## Summary

Feature 050 wires three shipped subsystems together so that **a GLP program in the C# reference REPL drives genuine QUIC over the wire**:

- **025** gave GLP a transport-agnostic native link layer (`csharp/glp_link/`): the `ILinkTransport` seam, a `TransportRegistry` (scheme→leaf), the reused kernels (`_link_setup`/`_link_send`/`_link_listen`/`_link_accept`/`_link_close`) and their GLP wrappers (`server_listener`/`client_connector`/`link_close` in `programs/self.glp`), and the egress/ingress core (`LinkEstablish`/`LinkEgress`/`LinkPump`).
- **036** shipped a *complete, genuine* in-process QUIC+WebSocket `ILinkTransport` leaf — `csharp/glp_link/transports/QuicTransport.cs` (real `System.Net.Quic`/MsQuic, ALPN `h3`, mutual shared-cert SPKI-SHA256 pin, WS-over-one-bidi-stream) — but it is **only instantiated by the `glp_quick_host` side-process, never registered into the REPL's `LinkRuntime`**.
- **041** shipped the crdtmsg CRDT wire format (`csharp/glp_crdtmsg/`): the unified ground-relay envelope (header + routing policy + capability slot as length-prefixed skippable TLV), the rich-text CRDT (Fugue + Peritext), a loud-fail decode contract, and `Macaroon.Verify()` (verify-before-act).

**Technical approach**: the transport, kernels, codec, and macaroon verifier already exist. 050 is an **integration + demonstration** feature, not a from-scratch build. Its four load-bearing pieces of new work are:

1. **Register** `QuicTransport` into the REPL composition root (`out/csharp/glp_repl/Program.cs`), loading the permanent shared cert + SPKI pin from `glpquick-cert/` (FR-001, FR-010, FR-011).
2. **Bridge the wire payload**: the 025 egress currently serializes ground terms via `PayloadSerializer` (`LinkEgress.cs:36`). A per-link **payload-codec seam** (host-side, below GLP) makes the `"quic"` link encode/decode the payload as a **041 crdtmsg envelope** while loopback/tcp keep `PayloadSerializer` (FR-005, FR-006, FR-007).
3. **Gate on macaroons**: link establishment and gated actions present a static macaroon in the envelope capability slot and `Macaroon.Verify()` it before acting; refusals are recorded as a distinct outcome, fail-closed, never a crash (FR-008, FR-009).
4. **The GLP test program**: a role-parameterized `.glp` program under `programs/` that stands up the all-pairs 5-endpoint / 10-full-duplex-link mesh across the two hosts as GLP goals and drives mesh + performance + security/cyber + reliability + graceful termination (FR-012–FR-018).

**Hard discipline (FR-019 / Constitution IV-a, §1.14)**: `link_id` scheme `"quic"` is data (no approval). The kernels and GLP wrappers are reused **unchanged**. No new GLP kernel, guard, system predicate, or language primitive is introduced; the payload-codec seam and macaroon gate are host-side C# below the seam. If any of that turns out to require a new kernel/primitive, STOP and propose-first — do not work around with a bespoke evaluator or shadow layer (the failure mode called out from 049).

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`) — the reference REPL + link layer + crdtmsg. GLP source (`.glp`) for the test program (loaded by the C# REPL). Python 3.12 (`glp_quick` cert tooling, already shipped).
**Primary Dependencies**: `System.Net.Quic` (MsQuic, GA in .NET 9+); `csharp/glp_link/GlpLink.csproj`; `csharp/glp_crdtmsg/GlpCrdtMsg.csproj` (`glp_crdtmsg` → references `glp_link`); `out/csharp/glp_runtime_net.csproj` (converted runtime + REPL); crypto: `X509Certificate2`, HMAC-SHA256 (`Macaroon`), SPKI SHA-256 pin.
**Storage**: none new. crdtmsg's op store (PGlite via Npgsql) is out of this feature's path — the link carries envelopes, it does not persist them. No new Alembic migration (single head stays `0010`, Constitution VI-a).
**Testing**: xUnit (`csharp/glp_link.tests/`, `csharp/glp_crdtmsg.tests/`) for host-side integration; the REPL suite `test/run_all_tests.sh` (baseline 524/525, one pre-existing AOT-smoke fail) for GLP-level regressions; a two-physical-host manual acceptance run for the cross-host demo (Olamnit 192.168.0.136 + gavri 192.168.0.108).
**Target Platform**: Windows 11 (the 036 floor; both demo hosts). `QuicTransport` gates on `IsSupported` and refuses — never downgrades — where QUIC is unavailable (FR-002). Design MUST NOT assume Windows beyond the availability gate.
**Project Type**: compiler/runtime + host library integration (C# reference REPL over the hand-authored link layer) + GLP demonstration program.
**Performance Goals**: SC-005 — median message round-trip < 50 ms on the LAN wire; ≥ 1000 messages sustained with zero loss. *(Resolved by analyze note U1, 2026-07-08: adopted as the working acceptance targets, re-confirmable at the T043 two-host run — no longer a blocking NEEDS CLARIFICATION.)*
**Constraints**: TRUE QUIC only, no TCP/loopback fallback (FR-002); every mesh link opened by a GLP goal, no external harness (FR-003); mutual SPKI pin, no CA/hostname/time-boxed shortcut (FR-010/FR-011); graceful termination, zero crashes (FR-017); SRSW-clean GLP test program (Constitution III).
**Scale/Scope**: all-pairs full mesh of 5 C# endpoints = C(5,2) = **10 full-duplex links** (10 QUIC bidi streams). This feature delivers the `"quic"` registration, the payload-codec + macaroon integration, **2 C# glpnet REPL endpoints**, and the GLP test program; it is *ready to accept* the 3 pre-built MAUI C# apps (building them is out of scope, FR-013a).

## Constitution Check

*GATE: evaluated against the frozen constitution (v1.1.0). No violations — no Complexity Tracking entries required.*

| Principle | Verdict | Basis |
|---|---|---|
| **I. Spec-First** | PASS | Every FR below traces to `spec.md`; this plan quotes/refs it, does not override it. |
| **II. Bug-Protocol / No-Workarounds** | PASS (commitment) | Any discovered bug in the reused kernels/transport/codec → STOP and report; no try/catch "robustness" over a caller bug. FR-002 no-silent-downgrade and FR-009 no-silent-drop are enforced as loud outcomes, not swallowed. |
| **III. SRSW inviolable** | PASS (commitment, machine-checkable) | The new `.glp` test program is SRSW-clean; no `skipSRSW` token. Verified by the REPL loader (a non-SRSW clause fails to load). |
| **IV-a. Language Authority** | PASS (gated) | Kernels + wrappers reused **unchanged**; `"quic"` is data; payload-codec seam + macaroon gate are host-side below the seam. Any newly-necessary kernel/primitive is propose-first (FR-019). |
| **IV-b. Preserve Working Internals** | PASS | No removal of `_ClauseVar`/`_TentativeStruct`/fallback branches; the egress/ingress change is additive (a codec seam), not a rewrite. |
| **V. Claude-Only LM** | PASS (N/A) | No LM in the loop; no `openai`/`litellm`/`OPENAI_API_KEY`. |
| **VI-a. Additive migrations** | PASS | No new migration; single head stays `0010`. |
| **VI-b. Single PGLite cluster** | PASS (N/A) | The link carries envelopes; adds no cluster. |
| **VII. Test-gated, commit-scoped shipping** | PASS (commitment) | Baseline green before change; commit only 050 files; ship via buildkit GitFlow, never hand-merge to main. |
| **VIII. Single source of truth** | PASS | 050 references the 025/036/041 specs; it does not duplicate them. |

## Project Structure

### Documentation (this feature)

```text
specs/050-glp-native-quic-link/
├── spec.md              # Feature spec (input)
├── plan.md              # This file
├── research.md          # Phase 0 — design decisions (D-1..D-5)
├── data-model.md        # Phase 1 — entities mapped to real C# types
├── quickstart.md        # Phase 1 — the two-host acceptance run
├── contracts/           # Phase 1 — integration contracts
│   ├── transport-registration.md
│   ├── wire-payload-crdtmsg.md
│   ├── capability-gating.md
│   └── mesh-test-harness.md
└── tasks.md             # Phase 2 — /bk-tasks output (NOT created here)
```

### Source Code (repository root)

```text
out/csharp/glp_repl/
└── Program.cs                       # EDIT (composition root, lines 30-35): register QuicTransport
                                     #   loaded from glpquick-cert/ — the one sanctioned place
                                     #   that references both the runtime and GlpLink.

csharp/glp_link/
├── seam/
│   └── IPayloadCodec.cs             # NEW (host-side seam, below GLP): per-link payload encode/decode
├── primitives/
│   ├── LinkEgress.cs                # EDIT: ShipGround uses the link's IPayloadCodec (was PayloadSerializer)
│   ├── LinkPump.cs                  # EDIT: ingress decode uses the link's IPayloadCodec
│   ├── LinkEstablish.cs             # EDIT: select/attach the codec + macaroon gate per link
│   └── LinkKernels.cs               # EDIT (wiring only): pass cert/pin + codec factory (no new kernel)
├── transports/
│   └── QuicTransport.cs             # REUSE UNCHANGED (036) — the genuine leaf
└── crdtmsg-bridge/
    └── CrdtMsgPayloadCodec.cs       # NEW: IPayloadCodec that encodes/decodes 041 crdtmsg envelopes
                                     #   (bridges GLP ground term ↔ GlpRuntime.CrdtMsg.MessageCodec)
                                     #   NOTE: glp_link → glp_crdtmsg is a NEW reference direction;
                                     #   see research.md D-1 for the reference-cycle resolution.

csharp/glp_crdtmsg/
├── header/CapabilitySlot.cs         # REUSE (+ possible v2 capability-on-wire extension — see D-2, 041-scoped)
├── cap/Macaroon.cs                  # REUSE: Verify() verify-before-act
└── model/SurfaceCodec.cs            # REUSE: MessageCodec (Binary canonical) / decode-with-understood-set

programs/
├── self.glp                         # REUSE UNCHANGED: server_listener/client_connector/link_close wrappers
└── tests/quic/                      # NEW: the role-parameterized GLP mesh/perf/security/reliability program
    └── quic_mesh.glp                #   (+ any per-role load files) — SRSW-clean, procedure-declared

csharp/glp_link.tests/               # NEW xUnit: registration, crdtmsg-on-link round-trip, macaroon gate,
                                     #   pin-mismatch reject, graceful close, multi-accept mesh
test/run_all_tests.sh                # EDIT: add GLP-level regression(s) for the quic link program
glpquick-cert/                       # REUSE: the permanent shared cert material (pem/key/pfx/fingerprint)
```

**Structure Decision**: no new project. All host-side work lands in the existing `csharp/glp_link/` library (the 025 layer) plus a one-line-scope edit to the REPL composition-root shim `out/csharp/glp_repl/Program.cs`. The crdtmsg codec is reused from `csharp/glp_crdtmsg/`. The GLP test program lives under `programs/tests/quic/` per the "all `.glp` under `programs/`" policy. The single genuine QUIC transport driven is `glp_link/transports/QuicTransport.cs` (in-process, spec Q1) — **not** the `glp_crdtmsg/route/QuicLinkTransport.cs` side-process path (that is 048's crdtmsg-router path; see research.md D-4 for the reconciliation).

## Complexity Tracking

> No Constitution violations. The one design element worth naming — the per-link `IPayloadCodec` seam — is justified below, not a violation, so it is recorded in research.md (D-1) rather than here.

## Phase Notes

- **Phase 0 (research.md)** resolves the five design decisions: D-1 crdtmsg payload bridge + reference-cycle, D-2 capability-slot-on-wire surface, D-3 perf targets (residual clarification), D-4 which QUIC transport (reconcile the two), D-5 mesh topology + MAUI interop readiness.
- **Phase 1 (data-model.md, contracts/, quickstart.md)** maps the spec's Key Entities to the real C# types and pins down the four integration contracts + the two-host run.
- **Phase 2** is `/bk-tasks` (not produced here): a dependency-ordered task list per user story (US1 register+one-bind → US2 crdtmsg-on-wire → US3 macaroon gate → US4 full mesh test → US5 graceful termination), tests-first.

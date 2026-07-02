# Codex Independent Review — Feature 036 (HTTP/3 QUIC + WS link)

**Date**: 2026-06-27 | **Reviewer**: local `codex exec` (codex-cli 0.141.0), read-only sandbox
**Trigger**: Gabi-ordered independent verification of the 036 design vs the requirement set, after a Claude session produced a false "buildkit/python not installed" claim. This file is the durable copy; the full trace is the session scratchpad `codex_036_review.out` (327 KB).

> Status in §1 means **design/task coverage in artifacts**, not implementation. Every task in `tasks.md` is unchecked.

## 1. Requirements coverage (summary)
- **MET (design/task level)**: FR-001..FR-008b, FR-011, FR-012, FR-016 (no task — defined in plan/contracts), FR-017, FR-018, FR-019; SC-001..SC-006.
- **PARTIAL**: FR-009, FR-010 (both stacks planned but Gleam genuine-QUIC unresolved; Gleam may report `real_quic=false` so interchangeability unproven); FR-014, FR-015, SC-007 (corpus + distillation planned but **absent on disk**).
- **UNVERIFIABLE from artifacts**: FR-013, SC-008 (marathon state is out-of-repo).
- Non-requirement tasks: T037 (quickstart/known-issues), T038 (no-regression), T039 (green suites) = quality gates; T040 = final acceptance.

## 2. Grounding
- **Repo grounding partly strong**: `csharp/glp_link` genuinely has the reusable seam + reliability substrate — `ILinkTransport`/`ILinkEndpoint`, `FrameCodec`, `LinkSequencer`, registered C# REPL link kernels/transports; spec 025 defines ground-relay + link primitives.
- **External corpus grounding NOT yet present**: `specs/036-http3-quic-ws-link/research/` corpus/distillation does not exist in the checkout. Claims about .NET 9 RFC 9220 maturity, MsQuic packaging, cert-pinning specifics, and AtomVM genuine QUIC are **unverifiable from artifacts** — they are future marathon output.

## 3. Reuse vs rework vs ground-up
- Python `glp_quick` control plane — **GROUND-UP NEW** (absent at repo root).
- C# QUIC+WS leaf (`Http3QuicTransport`/`Http3QuicEndpoint`/`WebSocketOverHttp3`) — **REWORK/EXTEND** existing 025 `ILinkTransport`/`ILinkEndpoint`; reusable substrate is real.
- Gleam/AtomVM `gleam_quic` — **GROUND-UP NEW**; feasibility unresolved.
- Shared cert + fingerprint pinning — **GROUND-UP NEW** (Python) + planned C# callbacks; implementation absent.
- GLP-REPL bridge / envelope / mesh routing — **REWORK/EXTEND 025 semantics + NEW Python bridge** (`repl_link.py` new).

## 4. Gaps / issues / root causes
- **Real gap — corpus absent** (FR-014/015, SC-007): root cause = buildkit artifacts generated before the corpus stage completed.
- **Real gap — implementation absent** (`glp_quick`, `gleam_quic`, `/GLP-Quick` skill, C# QUIC files): root cause = tasks are an unchecked skeleton/implementation backlog.
- **Feasibility risk — RFC 9220 on .NET**: design depends on WS-over-HTTP/3 but maturity is itself a research item + fallback condition.
- **Feasibility risk — AtomVM genuine QUIC**: spec requires both stacks but artifacts allow `real_quic=false`, which may *fail* FR-009/010 rather than satisfy them.
- **Deferred-by-design — new GLP primitive gate** (Constitution IV-a, T013): not a gap yet, but blocks behavioral work if REPL bridging needs more than spec 025.

## 5. Verdict
Design is **traceable and mostly task-covered**, but **cannot yet provably meet** the requirement set because corpus, distillation notes, marathon state, and implementation are absent or out-of-repo. **Biggest risk**: unverified transport feasibility (RFC 9220 in .NET; genuine QUIC in AtomVM/WASM). **Highest-value next action**: complete and commit the corpus/distillation stage first — especially RFC 9220/.NET and AtomVM QUIC feasibility — before any skeleton or behavioral implementation.

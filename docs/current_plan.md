# Restart pointer — NOT a work ledger

> This file is intentionally thin. Do **not** write a full multi-step plan here and
> "resume from the current step" — that mechanism drifted stale (it once pointed
> restarts at already-shipped work). The **roadmap + buildkit pipeline state** are the
> source of truth. See CLAUDE.md § *Multi-Stage Task Persistence & Restart-Resume*.

## How to locate yourself on any restart (fresh / post-compaction / post-crash)

1. **What feature / what stage?** → `buildkit-roadmap next` (or `buildkit-roadmap status`)
   — the active/next roadmap feature, its state, and the exact `/buildkit-specify` command.
2. **In progress?** → a feature with a spec dir (`.specify/feature.json` → `specs/<NNN>/`)
   has entered the pipeline.
3. **Where in the feature (WIP position)?** → the buildkit pipeline stage state
   (DBOS + PGLite, per-feature) + the feature's `spec.md`/`plan.md`/`tasks.md`.

## Active now (2026-06-28)

- **Feature / branch**: `036-http3-quic-ws-link` (spec dir `specs/036-http3-quic-ws-link/`,
  pinned in `.specify/feature.json`). HTTP/3 (QUIC) + WebSocket channel-link prototype for GLP.
- **Pipeline stage**: specify ✅ → clarify ✅ (2 sessions: 2026-06-27 + 2026-06-28 corpus-driven)
  → research/corpus/distill ✅ (106 close-read notes, committed `10cdc452`) → **plan ✅ + tasks ✅
  + analyze ✅ reworked 2026-06-28** to the clarified spec. **Next stage = `/bk-implement`** (run in
  a fresh session — see the rework note below).
- **What the 2026-06-28 rework changed** (the 3 decisions that were pending Gabi, now encoded in
  `spec.md` Clarifications 2026-06-28 and propagated through plan/tasks/data-model/contracts):
  (1) WS link is **genuine RFC 6455 framing over a raw QUIC bidi stream** (025 `FrameCodec`), RFC 9220
  Extended-CONNECT isolated behind a seam; (2) C# stack is **cross-platform**, gate on `IsSupported`;
  (3) Gleam ships as **two deployment profiles** (A: AtomVM + native QUIC side-process; C: full BEAM +
  `quicer`/MsQuic in-process), interchangeable at the channel-link contract.
- **/bk-implement frontier**: marathon stages 1–3 (research/corpus/distill) are DONE — `tasks.md`
  T004–T007 + T035 are `[x]`. Start at **Phase 1 Setup (T001–T003)** → **Phase 2 Foundational
  (T008–T013a)**; the first behavioural code is gated behind **T013 (Constitution IV-a: no new GLP
  primitive without owner approval)** and **T013a (residual probes: `IsSupported`, msquic present,
  AtomVM `open_port`)** — escalate-don't-guess.
- **Marathon run**: `mrun-15d7dd0ffbc2` (state in the out-of-repo deploy-home catalog, per
  `plan.md` Storage — NOT the repo `C:/pglite/research/glpnet` cluster). **Verify it is resumable at
  implement-start** before relying on it for SC-008.

## History (do not resume these — they are done/parked)

- `023-glptutorial-run` **SHIPPED** `v2026.06.04.1` (merged PR #20).
- `020-trace-equivalence-fidelity` parked, branch local-only (not on the roadmap).

Once the marathon-stage-harness exists, it maintains the durable checkpoint that makes the
"where in the feature" answer automatic — this file stays a thin pointer.

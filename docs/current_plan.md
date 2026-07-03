# Restart pointer — NOT a work ledger (updated 2026-07-03)

> Intentionally thin. The **roadmap + buildkit pipeline / marathon state** are the source of truth
> (CLAUDE.md § *Multi-Stage Task Persistence & Restart-Resume*). Do not resume from a hand-written plan.

## How to locate yourself on any restart
1. **Feature states** → `python -m buildkit_cli.roadmap status`. NOTE `roadmap --json next` returns `null`
   (nothing `promoted`) → **040 must be promoted before it can be specified**.
2. `.specify/feature.json` still points at the shipped `036-http3-quic-ws-link` (drift F3 — repoint when a
   new feature pipeline starts).
3. Branch `037-virtual-3270-term` (off `develop`) @ **`b8c474b1`**, pushed. All work committed+pushed.

## Session 2026-07-02/03 outcome
- **035+ adversarial oblivion audit** (4 blind scanners) → `docs/research/035plus-oblivion-audit-2026-07-02.md`
  (the "nothing lost" register — every deferral/gap/bug given a real home).
- Roadmap homed: created **link-completion** + **full-acceptance**; **040** (row `rcopy-file-transfer-service`,
  legacy-slug misnomer) reframed as the **COMPLETE + HARDENED virtual-3270 terminal** catch-all;
  **three-role-agent-team-orchestration** (buildkit-bound) captured; 2 stale rows → `released`.

## DONE this session (committed+pushed on `037-virtual-3270-term`)
- **C# host BUILT** (`dotnet build csharp/glp_quick_host`) → integration tests un-skip (glp_quick **22 pass / 4 gleam-skip**).
- **Data-loss #1** mesh dup-id hijack/eviction (`csharp/glp_quick_host/Program.cs`) — fixed + regression test.
- **Data-loss #2** Gleam >1 MiB relay misroute (`gleam_quic/src/glpq_ffi.erl`) — fixed, `erlc`-verified (WSL);
  TODO add a >1 MiB round-trip test on the Gleam/WSL host (none exists).
- **A4** demo `AttributeError` on handshake timeout (`glp_quick/demo.py`) — fixed.
- **A5** pre-readiness stdout pipe-fill hang (`glp_quick/src/glp_quick/stacks/csharp.py`) — fixed.
- **037 hardening**: `@name` routing (FR-006), `--tui` TTY fallback (FR-005), link-drop reporting +
  shared `parse_addressed` + 5 tests.

## NEXT (post-restart, in order)
1. **T019 — live `glp_repl`-process bridge** (link-completion **A1**; the core "run GLP over the link").
   **Design (scoped, spec-first):** FR-008 (`specs/036-http3-quic-ws-link/spec.md:118`) + clarification (:18)
   = *"GLP REPL endpoints that exchange MESSAGES, not a submit-source/return-result RPC."* The REPL binary
   already has a **spec-025 link layer** — `out/csharp/glp_repl/Program.cs` installs `LinkKernels` + registers
   `TcpTransport`(127.0.0.1) / `LoopbackTransport`. So the bridge is **not** a text-scrape RPC: relay between the
   REPL's 025 `TcpTransport` and glp_quick's QUIC+WS `Handle` so two real REPLs exchange messages over the wire.
   **First reads:** `csharp/glp_link/transports/TcpTransport.cs`, the `LinkKernels` / 025 link-message interface,
   `glp_quick/src/glp_quick/repl_link.py` (envelope+routing only today; `--repl` flag inert). Substantial — do fresh.
2. **Promote + `/bk-specify` 040** = the **complete + hardened virtual-3270 terminal** (revisit+harden+complete
   every US1–US7 / FR-001..031, fully tested). See `specs/040-rcopy-file-transfer-service/CAPTURE.md`.
   037 = best-effort; **040 = the serious complete home** (owner-directed: don't minimise/defer).
3. **full-acceptance** — blocked: Profile C (MSVC/quicer), two-host LAN (gavri), marathon durability (real run;
   `mrun-15d7dd0ffbc2` was a phantom).
4. **038 residuals** (owner-gated D4 §15-freeze, D5 cyclic) + housekeeping **F1–F5** (036 dir collision,
   feature.json repoint, spec pointers).

## Tail (after the above): codexreview → ship → close → push (per the 2026-07-02 directive).

## History (done — do not resume)
- `036-http3-quic-ws-link` SHIPPED `v2026.07.02.3`; `038` SHIPPED `v2026.07.02.1`; `039` SHIPPED `v2026.06.30.1`;
  `036-glp-gleam-baseline-program` merged-to-develop (research program); `034/035/030` shipped earlier.

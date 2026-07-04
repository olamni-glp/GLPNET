# Implementation Plan: Virtual 3270 Terminal — Complete & Hardened (Feature 040)

**Branch**: `037-virtual-3270-term` (shared workstream branch; 040 completes the unmerged 037 basis) | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/040-rcopy-file-transfer-service/spec.md`

## Summary

Feature 040 is the **definitive, complete, hardened** home for the virtual IBM-3270-style block-mode terminal,
shipped as the `--tui` mode of `glp_quick` over the feature-036 QUIC+WS GLP channel-link. It **subsumes 037**:
every user story (US1–US9), functional requirement (FR-001..046), and success criterion (SC-001..013) is fully
implemented, hardened, and covered by automated tests — no deferral, no minimization (FR-046). Technical approach
(from Phase 0 research): **extend the existing prototype** `glp_quick/src/glp_quick/tui.py`, refactoring its logic
out of the `run_tui` closure into host-free, unit-testable modules under `glp_quick/terminal/` and
`glp_quick/rcopy/`; carry all terminal messages (pages, pinpoints, forms, REPL, `/rcopy`) as **ground GLP terms**
inside the existing L5 `GlpMessage` seam (no parallel transport, FR-026); resolve `@name` against the
036-authenticated peer set; serialize receive-path state mutation on the UI event loop; and back the `/rcopy`
responder with a **file-based WAL journal as source of truth** plus rebuildable catalog/provenance projections
(no new repo PGLite cluster). MVP = US1 fully working, hardened, and tested.

## Technical Context

**Language/Version**: Python ≥ 3.11 (`glp_quick` control plane); C# net10.0 reference host (`csharp/glp_quick_host`, built); Gleam/AtomVM relay (Profile A) for the gleam stack
**Primary Dependencies**: prompt_toolkit (full-screen UI), typer (CLI), cryptography (shared cert), `hashlib` (stdlib SHA-256); the feature-036 `StackAdapter` / `Handle` / `GlpMessage` seam — **no new DB dependency**
**Storage**: file-based per-root **WAL journal** (append-only, source of truth) + rebuildable **catalog** & **provenance** projections in the responder data dir; `/xfer/in/[peer-name-and-UID]/` landing under permitted roots. **No repo `.pgdb` cluster and no codeconv Alembic migration** (constitution VI-a/VI-b)
**Testing**: pytest + pytest-mock (`glp_quick/tests`); a host-free unit tier (always run) + a host-gated mesh-integration tier (`skipif not host_dll_path().exists()`)
**Target Platform**: Windows 11 primary; trusted LAN between cooperating endpoints; **Remote-Desktop-safe** (typed-command path authoritative, no function key ever required, no Win+Fx)
**Project Type**: single-project CLI/TUI (Python control plane) over the 036 multi-stack data plane
**Performance Goals**: LAN-interactive — a transmitted page/reply visible within ~2 s under normal LAN (SC-002); synchronise skips byte-identical files by SHA-256 (no needless transfer)
**Constraints**: RDP-safe; receive path thread-safe (FR-042) & non-swallowing (FR-043); all-or-nothing per file (FR-039); write nothing outside a permitted root (FR-033); catalog fully rebuildable from WAL with 0 loss (SC-010)
**Scale/Scope**: 036 mesh ≥4 named peers; many pages per side; multi-file `/rcopy` with per-root quota; 9 user stories, FR-001..046, SC-001..013

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.0. Re-checked after Phase 1 — still PASS.*

| Principle | Verdict | Basis |
|---|---|---|
| **I. Spec-First** | PASS | 040 `spec.md` is the identified, quoted, consistency-checked authority; consistent with the 037 reference + 036 `contracts/`. This plan derives from it, does not lead it. |
| **II. Bug-Protocol / No-Workarounds** | PASS | The hardening items (FR-040 `@name` resolve, FR-042 loop-serialized state, FR-043 explicit fault surfacing, FR-005/041 fallback) are **spec-mandated protocol fixes**, not try/catch "robustness". The plan explicitly forbids masking-style padding (research R3/R4/R6). |
| **III. SRSW / `skipSRSW`** | PASS | No GLP clause code authored; **zero** `skipSRSW` tokens. US5 runs goals on the existing REPL. |
| **IV-a. Language Authority** | PASS | US5 REPL-in-a-page adds **no** guard/predicate/kernel/type-system feature — it evaluates goals on the existing REPL over the link. No owner language-authority gate triggered (research R10/R14). |
| **IV-b. Preserve Working Internals** | PASS | No core-GLP internals touched; the prototype surface is extended, not removed (FR-026). |
| **V. Claude-only LM / No External API** | PASS | No LM in this feature; **zero** `OPENAI_API_KEY` / `litellm` / `openai` on any path. |
| **VI-a. Additive/Idempotent Migrations** | PASS (N/A) | No codeconv Alembic migration added; the responder WAL is a plain append-only file, replay-idempotent (research R7). |
| **VI-b. Single PGLite Cluster** | PASS | The responder store is a **file-based WAL + rebuildable file projections outside the repo working-data cluster** — no second `.pgdb` in the repo. Any future queryable projection must be an out-of-repo isolated per-service store via `codeconv.bridge_client` (VI-b v1.1.0 exemption). See **⚠ owner-awareness flag** below. |
| **VII. Test-Gated, Commit-Scoped Shipping** | PASS (aligned) | US9 mandates full automated coverage before any story is "done" (FR-045/046); ship via GitFlow, commit only worked files. |
| **VIII. Single Source of Truth & Traceability** | PASS | 040 is the authoritative completion home; 037 is referenced, not duplicated. Roadmap→pipeline→tasks traceable. The roadmap slug drift (`rcopy-file-transfer-service`) is the clause's **advisory** carve-out. |

**⚠ Owner-awareness flag (for `/bk-analyze`, not a violation)**: research **R7** chooses a **file-WAL +
file-projection** responder store, which diverges from the *named* backend in the spec's Assumption ("PGlite-backed
DuckLake") — but stays within the latitude that same Assumption grants ("exact store wiring is an implementation
detail resolved at planning time; the spec requires only durable recording and WAL-based recreatability"). This is
the simplest spec-satisfying, constitution-clean design (VI-b). Surfaced here so analyze/owner can confirm rather
than silently accept.

**No unjustified violations → Complexity Tracking is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/040-rcopy-file-transfer-service/
├── spec.md              # feature spec (definitive superset of 037)
├── plan.md              # this file
├── research.md          # Phase 0 — R1..R14 decisions + resolved Technical Context
├── data-model.md        # Phase 1 — entities & state transitions
├── quickstart.md        # Phase 1 — launch/pages/rcopy/tests walkthrough
├── contracts/
│   ├── terminal-protocol.md   # L6 tmsg(...) ground-term sub-protocol over the 036 L5 envelope
│   ├── rcopy-protocol.md      # /rcopy client⇄responder exchange
│   ├── responder-store.md     # WAL (source of truth) + catalog/provenance projections
│   └── command-surface.md     # typed commands + PF keys + legend + OIA
├── checklists/requirements.md # (existing)
└── tasks.md             # Phase 2 — /bk-tasks output (NOT created by /bk-plan)
```

### Source Code (repository root)

```text
glp_quick/
├── pyproject.toml                       # ≥3.11; deps unchanged (typer, cryptography, prompt_toolkit)
├── src/glp_quick/
│   ├── cli.py                           # --tui dispatch + no-TTY fallback gate (extend: harden FR-041)
│   ├── tui.py                           # view + wiring (REFACTOR: logic → terminal/*, keep prompt_toolkit app)
│   ├── link_console.py                  # plain fallback (extend: @name unknown-report parity, R5)
│   ├── repl_link.py                     # L5 envelope + parse_addressed + REPL bridge (build the process bridge, R10)
│   ├── demo.py, cert.py, stacks/*       # feature-036 seam (unchanged)
│   ├── terminal/                        # NEW — host-free, unit-testable
│   │   ├── pages.py                     #   Page model, page store, ownership, unread, navigation (US2)
│   │   ├── protocol.py                  #   tmsg(...) ground-term codec (US2/4/5/6/8) — single encode/decode point
│   │   ├── routing.py                   #   @name resolve vs handle.peers() + unknown report (US1/US9)
│   │   ├── presentation.py              #   5 themes, OIA, two-strip layout, splash (US3)
│   │   ├── keys.py                      #   PF-key bindings + dynamic legend + typed equivalents (US3/US7)
│   │   ├── joint.py                     #   pinpoint change: save/restore, transient/permanent (US4)
│   │   ├── forms.py                     #   mask/form define + fill (US4)
│   │   ├── replpage.py                  #   REPL-in-a-page over the link (US5)
│   │   └── state.py                     #   shared terminal state + loop-serialized mutation (FR-042/US9)
│   └── rcopy/                           # NEW — /rcopy client + responder
│       ├── wizard.py                    #   client wizard (US6): peers→offer→root→folder→globs→filter→mode→submit
│       ├── responder.py                 #   responder service (US8): init/offer/verdict/commit/quota/perm/path
│       ├── transfer.py                  #   chunked transfer + temp→verify→atomic-commit (FR-039)
│       ├── filter.py                    #   exclusion filter (size/name/subdir/attribute) — pure (US6/R9)
│       ├── wal.py                       #   append-only WAL journal + replay/rebuild (FR-036/SC-010)
│       ├── catalog.py                   #   per-root catalog projection + SHA-256 synchronise (FR-034/35)
│       └── provenance.py                #   durable provenance records (FR-037)
└── tests/                               # pytest
    ├── unit/                            #   host-free: pages, protocol, routing, notty, threadsafe, link-drop,
    │                                    #   filter, wal-replay, sync, quota, joint, forms, keys, presentation
    ├── integration/                     #   host-gated (host_dll_path()): page-transmit, @name delivery,
    │                                    #   rcopy-e2e (incl. WAL-loss recreate), repl-page
    └── test_sc_coverage_map.py          #   asserts every US1–8 + SC-001..012 has ≥1 test (SC-013)

csharp/glp_quick_host/                   # feature-036 C# reference host (built; unchanged transport)
out/csharp/glp_repl/                     # C# GLP REPL bridged for US5 (spawned by repl_link process bridge)
```

**Structure Decision**: Single-project extension of the existing `glp_quick` Python package (FR-026 — extend the
prototype, reuse the 036 seam). The new `terminal/` and `rcopy/` subpackages exist specifically so the terminal's
own behaviors are **host-free unit-testable** (FR-045/SC-013); `tui.py` shrinks to a prompt_toolkit view over
those modules; `link_console.py` reuses the same model for plain-console parity. The transport, C# host, and
stacks are unchanged (Out of Scope).

## Phase 0 — Research

Complete → `research.md`. Fourteen decisions (R1–R14) resolve every Technical-Context unknown:
extend+refactor the prototype (R1); ground-term terminal sub-protocol (R2); `@name` resolve vs `peers()` (R3);
loop-serialized receive-path state (R4); tested no-TTY fallback (R5); link-drop surfacing (R6); **file-WAL
responder store** with rebuildable projections (R7, + the VI-b/owner flag); `/rcopy` transfer protocol (R8);
pure exclusion filter (R9); REPL-in-a-page with the `repl_link` **process-bridge prerequisite** (R10); PF
bindings+legend (R11); presentation/two-strip (R12); two-tier test strategy (R13); dependencies & story sequencing
(R14). No remaining NEEDS CLARIFICATION.

## Phase 1 — Design & Contracts

Complete → `data-model.md`, `contracts/` (terminal-protocol, rcopy-protocol, responder-store, command-surface),
`quickstart.md`. Agent context (this plan) is referenced from `CLAUDE.md`'s BUILDKIT block. Constitution re-checked
post-design: still PASS (no new gates introduced by the design).

## Phase 2 — Task planning approach (executed by `/bk-tasks`, not here)

`/bk-tasks` will generate `tasks.md` organized **by user story** in priority order, each story hardened + tested
before it counts complete (FR-046):
1. **Setup/refactor** — extract `tui.py` closure into `terminal/*` (R1); add the `protocol.py` codec + a fake
   in-memory `Handle` test double.
2. **US1 (P1, MVP)** — type-only conversation, `//`+Enter transmit, the core slash-commands, `@name` resolve vs
   `peers()` (FR-040), no-TTY fallback (FR-005/041), link-drop surfacing (FR-043/044), receive thread-safety
   (FR-042) — with unit + integration tests (US9 hardening of US1). **MVP checkpoint.**
3. **US2 (P2)** — page model + ownership + `/transmit` page-as-block + `/pages` owner-by-name + navigation; tests.
4. **US3 (P3)** — themes/OIA/two-strip/splash + PF-legend blocks; tests.
5. **US4 (P4)** — joint pinpoint (save/restore/transient/permanent) + masks/forms; tests.
6. **US5 (P5)** — build the `repl_link` process bridge (R10 prerequisite) + REPL-in-a-page + agent-sent pages;
   tests.
7. **US6 + US8 (P6, the `/rcopy` pair)** — exclusion filter, wizard, responder (`init`/offer/verdict/commit),
   WAL/catalog/provenance, synchronise/quota/path-safety, all-or-nothing per file; unit + e2e tests (incl.
   WAL-loss recreate).
8. **US7 (P7)** — user-bindable free PF keys with typed equivalents + live legend; tests.
9. **US9 close-out** — the SC-coverage-map test asserts every US1–8 + SC-001..012 has ≥1 asserting test and the
   suite is green (SC-013).

Task ordering respects the dependency chain (refactor → protocol/state → US1 → …); `[P]`-parallelizable tasks are
independent-file unit modules. Estimated ~55–70 tasks across the six phases.

## Complexity Tracking

> No Constitution Check violations require justification — this table is intentionally empty. The one design
> divergence (R7 store backend vs the spec's named PGlite-DuckLake) is within the spec Assumption's stated latitude
> and is surfaced as an owner-awareness flag above, not a violation.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

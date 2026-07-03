# Feature 040 — Virtual 3270 Terminal: THOROUGH · HARDENED · TOTALLY COMPLETE — CAPTURED

**Status**: captured. This dir reserves spec number **040**. Owner-directed 2026-07-02:
**040 is the definitive, complete, hardened implementation of the entire virtual-3270 block-mode terminal.**
It is NOT a "leftover deferred bits" catch-all — it is where the whole 3270 terminal is built *completely* and
*hardened*, with **no deferral, no minimization, no distortion, no loss.**

> Division of labour (owner-directed):
> - **037 virtual-3270-term** — best-effort: the authoritative spec + the shipped basic `--tui` prototype, plus
>   "implement as much as possible" incrementally. NOT the serious/complete home (left to itself it would just
>   minimise + defer the hard parts).
> - **040 (this)** — the SERIOUS home: thorough, hardened, **totally complete**. Every US, every FR, fully
>   tested. This is the deliverable that actually finishes the 3270 terminal.

> Legacy-slug note: the roadmap row id is `rcopy-file-transfer-service` (immutable, now a MISNOMER — `/rcopy`
> is one sub-part). Title/notes broadened in the roadmap row. Roadmap state reads `specified` (forward-only CLI
> artifact); really `captured` (no `spec.md` yet).

**Reference spec**: `specs/037-virtual-3270-term/spec.md` — US1–US7, FR-001..031, SC-001..008.

## 040 = COMPLETE + HARDENED coverage of the whole terminal
Every one of these must be fully implemented, hardened, and **tested** (037 currently has ZERO tests importing `tui.py`):

**Full user-story coverage (no US deferred)**
- **US1** type-only conversation, `//`+Enter, full slash-command set, RDP-safe — *complete + tested*.
- **US2** block-mode page editing; transmit a page as a block; received page becomes a **peer-owned** page (not dumped into one shared CHAT page).
- **US3** 5 themes + purple accent, OIA status line w/ live link info, splash phase, configurable command area **incl. the two-strip layout**, dynamic PF-legend reverse-video blocks.
- **US4** joint-mode toggle; pinpoint overwrite with saved original; transient/permanent; last-writer-wins per region; masks/forms.
- **US5** REPL-in-a-page (live GLP REPL bound to a page); agent-sent fillable pages returned to sender.
- **US6** `/rcopy` **client wizard** AND the `/rcopy` **responder backend** (registry, `/xfer/in` landing, SHA-256 sync, DuckLake provenance, per-root catalog, WAL journal).
- **US7** user-bindable free PF-keys + typed equivalents; FR-020 PF13–24 / Ctrl fallbacks; FR-025 dynamic legend.

**Hardening (the "hardened" bar — none of these may ship broken)**
- **FR-006 `@name` routing** must actually route (today it is advertised in help but every message silently goes to `default_to`).
- **FR-005 TTY-fallback** must work in `--tui` (today piped/no-TTY stdin errors instead of falling back — SC-003).
- **Thread-safety**: `recv_loop` must not race on shared page/peer state and must NOT silently swallow exceptions (link drops must be reported — the "report, don't fail silently" edge case).
- **No-peer / link-down** must report clearly, never hang or crash.
- **Full automated test coverage** of the TUI (unit + link-integration), including SC-001..SC-008.

## Boundary
- **036 http3-quic-ws-link** — transport (released). **durable-mesh-messaging-protocol** — sibling (captured).

## Next
Refine → promote → `/bk-specify` targeting `specs/040-rcopy-file-transfer-service` — author the complete,
hardened spec.md (superset of 037, with the hardening + test bar explicit), then implement to totality.
Audit basis: `docs/research/035plus-oblivion-audit-2026-07-02.md` §C.

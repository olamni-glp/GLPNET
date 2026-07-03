---
description: "Task list — Feature 040: Virtual 3270 Terminal (Complete & Hardened)"
---

# Tasks: Virtual 3270 Terminal — Complete & Hardened (Feature 040)

**Input**: Design documents from `/specs/040-rcopy-file-transfer-service/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓ (terminal-protocol, rcopy-protocol, responder-store, command-surface)

**Tests**: REQUESTED — US9 (FR-045/FR-046, SC-013) mandates full automated coverage; a story is complete only when its acceptance passes **and** its tests are green. Test tasks are therefore included in every story (write test → see it fail → implement).

**Organization**: by user story (priority order). US9 (reliability/hardening/tests, P1 cross-cutting) is **not** a separate phase — its four hardening fixes live inside US1, and its coverage bar is satisfied by the per-story tests + the coverage-map in Polish.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different file, no dependency on an incomplete task)
- **[Story]**: US1..US8 for story-phase tasks (Setup/Foundational/Polish carry no story label)
- Exact file paths are included in each task.

## Path Conventions

Single project — the `glp_quick` Python package (`glp_quick/src/glp_quick/…`, tests in `glp_quick/tests/…`), extending the feature-036 prototype (FR-026). The C# host (`csharp/glp_quick_host`) and REPL (`out/csharp/glp_repl`) are reused unchanged.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the testable subpackage skeletons + the test doubles the whole feature relies on.

- [X] T001 Create the `terminal/` and `rcopy/` subpackage skeletons (`glp_quick/src/glp_quick/terminal/__init__.py`, `glp_quick/src/glp_quick/rcopy/__init__.py`) and the two-tier test dirs (`glp_quick/tests/unit/__init__.py`, `glp_quick/tests/integration/__init__.py`).
- [X] T002 [P] Add a fake in-memory `Handle` test double implementing `stacks.base.Handle` (send/recv/peers/link_id) with an injectable peer set + fault/close injection, in `glp_quick/tests/_fakes.py` (enables host-free US1/US2/US4/US5 behavior tests).
- [X] T003 [P] Document the two-tier test layout (host-free `unit/` always run; `integration/` gated by `host_dll_path()` `skipif`, matching `test_mesh.py`) in `glp_quick/tests/README.md`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The terminal message codec, shared state model, and `@name` resolver every user story builds on.

**⚠️ CRITICAL**: No user-story work begins until this phase is complete.

- [X] T004 Implement the `tmsg(...)` ground-term codec (encode/decode all kinds; ground-relay escaping; bare-text→`chat`) in `glp_quick/src/glp_quick/terminal/protocol.py` per `contracts/terminal-protocol.md`.
- [X] T005 [P] Unit-test the protocol codec (round-trip all kinds + escaping + backward-compat with bare chat) in `glp_quick/tests/unit/test_protocol.py`.
- [X] T006 Implement the shared terminal state model (pages/unread/peers/OIA link-state; a loop-serialized `post(fn)` mutation seam per R4 + a lock-guarded variant for tests) in `glp_quick/src/glp_quick/terminal/state.py` per `data-model.md`.
- [X] T007 Implement `@name` resolution against a peer-set provider (unknown → structured "unknown peer" result, never default-fallback) in `glp_quick/src/glp_quick/terminal/routing.py` per R3.
- [X] T008 [P] Unit-test `@name` resolution incl. unknown-peer report in `glp_quick/tests/unit/test_routing_resolve.py` (complements the existing `test_routing.py` parse coverage).

**Checkpoint**: Foundation ready — user-story work can begin.

---

## Phase 3: User Story 1 — Type-only conversation over the link (Priority: P1) 🎯 MVP

**Goal**: Hold a conversation over the 036 link using only typing + `//`+Enter; every action typed (RDP-safe); `@name` delivers to the named peer; no-TTY falls back to the line console; link drops are surfaced; the receive path is race-free. (Folds in US9's four hardening fixes for US1.)

**Independent Test**: Two endpoints on a LAN driven purely by typing — a `//`+Enter message arrives, the reply renders, all core slash-commands work; `@name` reaches the named peer and an unknown name is reported; piping stdin falls back to the plain console.

### Tests for User Story 1 (write first, expect FAIL)

- [X] T009 [P] [US1] Unit test: no-TTY fallback engages under `--tui` (monkeypatch `sys.stdin/stdout.isatty`) → line-console path, no exception (FR-005/FR-041/SC-003) in `glp_quick/tests/unit/test_notty_fallback.py`.
- [X] T010 [P] [US1] Unit test: receive-path thread-safety — concurrent fake-Handle receives never corrupt/lose page/peer state (FR-042/SC-012) in `glp_quick/tests/unit/test_recv_threadsafe.py`.
- [X] T011 [P] [US1] Unit test: a link error/drop is surfaced to the OIA/status (never swallowed) and the terminal stays locally operable (FR-043/FR-044/SC-012) in `glp_quick/tests/unit/test_link_drop_report.py`.
- [X] T012 [P] [US1] Unit test: `@name` delivers to the named peer and an unknown name is reported (no silent default-fallback) against the fake Handle (FR-040/SC-011) in `glp_quick/tests/unit/test_at_delivery.py`.
- [X] T013 [P] [US1] Integration test (host-gated): two endpoints — a `//`+Enter message arrives, core slash-commands work, `@name` delivers to the named peer, in `glp_quick/tests/integration/test_us1_conversation_mesh.py`.

### Implementation for User Story 1

- [X] T014 [US1] Refactor `run_tui`: move the closure's actions/state into `terminal/state.py` and wire the view, **preserving the prompt_toolkit `Application` + the existing surface incl. the F9 / Ctrl-X / Alt-Enter transmit accelerators (FR-008)** and the `//`+Enter transmit (FR-003/FR-026) in `glp_quick/src/glp_quick/tui.py`.
- [X] T015 [US1] Serialize receive-loop state mutations via `loop.call_soon_threadsafe` and distinguish `None`/close vs fault/FR-019-token vs message, surfacing link-state to the OIA (FR-042/FR-043/FR-044) in `glp_quick/src/glp_quick/tui.py` + `terminal/state.py`.
- [X] T016 [US1] Resolve `@name` against `handle.peers()` before send; report an unknown peer, never default-fallback (FR-040) in `glp_quick/src/glp_quick/tui.py` using `terminal/routing.py`.
- [X] T017 [US1] Harden the `cli.py` no-TTY gate (any `isatty` exception ⇒ fallback; keep the notice) and give `link_console.py` `@name` unknown-report parity (FR-005/FR-041) in `glp_quick/src/glp_quick/cli.py` + `glp_quick/src/glp_quick/link_console.py`.
- [X] T018 [US1] Route all send/receive text through `terminal/protocol.py` (`chat` kind) so `tui` and `link_console` share one codec (FR-026) in `glp_quick/src/glp_quick/tui.py` + `link_console.py`.

**Checkpoint (MVP)**: US1 fully functional, hardened, and tested independently — STOP and validate.

---

## Phase 4: User Story 2 — Author, name & transmit pages; receive the peer's (Priority: P2)

**Goal**: Compose/name pages, transmit a whole page as an owned block; a received page appears as a page owned by the sending peer (not merged into chat, no focus-steal); list pages with owner-by-name; navigate.

**Independent Test**: Create two named pages, transmit one; it arrives as a page owned by the sender; `/pages` shows names+owners on both ends; next/prev/goto switch pages; `@name` reaches the named peer.

### Tests for User Story 2

- [ ] T019 [P] [US2] Unit test the page model (create/name/switch/goto, owner me/peer, received page not merged + no focus-steal + unread indicator) (FR-009/FR-010) in `glp_quick/tests/unit/test_pages.py`.
- [ ] T020 [P] [US2] Integration test (host-gated): transmit a page → peer receives it as an owned page (not shared chat); `/pages` shows owner-by-name on both ends, in `glp_quick/tests/integration/test_us2_page_transmit_mesh.py`.

### Implementation for User Story 2

- [ ] T021 [P] [US2] Implement `Page` + page store (owner, kind, unread, navigation) in `glp_quick/src/glp_quick/terminal/pages.py` per `data-model.md`.
- [ ] T022 [US2] Send `tmsg(page,…)` on `/transmit` and render an inbound page as an owned page with an OIA "new page" indicator (no focus-steal) (FR-007/FR-010) in `glp_quick/src/glp_quick/tui.py` + `terminal/pages.py` + `terminal/protocol.py`.
- [ ] T023 [US2] Show owner as me/peer-name + current page in `/pages`, and current page `X/N + name + owner` on the OIA (FR-009/FR-022) in `glp_quick/src/glp_quick/tui.py` + `terminal/presentation.py`.

**Checkpoint**: US1 + US2 both work independently.

---

## Phase 5: User Story 3 — 3270 presentation & ergonomics (Priority: P3)

**Goal**: Five themes (purple/magenta command accent), OIA status, configurable compose area (N-line or two-strip), splash, and the dynamic PF-legend as reverse-video blocks with typed equivalents.

**Independent Test**: Toggle each theme (accent present); OIA shows mode/page X-N+name+owner/link; switch N-line vs two-strip; the PF-legend shows reverse-video blocks each labelled with its typed equivalent.

### Tests for User Story 3

- [ ] T024 [P] [US3] Unit test presentation (5 distinct themes + accent; OIA fields; two-strip vs N-line layout; legend blocks carry typed equivalents) (FR-021/FR-022/FR-023/FR-025) in `glp_quick/tests/unit/test_presentation.py`.

### Implementation for User Story 3

- [ ] T025 [P] [US3] Extract the five themes + OIA + ASCII splash into `glp_quick/src/glp_quick/terminal/presentation.py` (FR-021/FR-022/FR-024).
- [ ] T026 [US3] Add the two-strip compose layout + `/layout [lines N|two-strip]` command (default ~3 lines via `GLPQUICK_CMDLINES`) (FR-023) in `glp_quick/src/glp_quick/terminal/presentation.py` + `tui.py`.
- [ ] T027 [US3] Render the dynamic PF-legend as reverse-video blocks above the command line, each labelled with its typed-command equivalent (FR-025) in `glp_quick/src/glp_quick/terminal/presentation.py` + `terminal/keys.py` + `tui.py`.

**Checkpoint**: US1–US3 independently functional.

---

## Phase 6: User Story 4 — Joint live edit (pinpoint) & masks/forms (Priority: P4)

**Goal**: With joint mode on, a counterpart pinpoint overwrites a region while the original is saved (transient dismissible / permanent persists); masks/forms with fixed labels + fillable regions filled and returned.

**Independent Test**: Enable joint mode; counterpart overwrites a region (original recoverable); transient reverts, permanent persists; define a mask on one side, fill on the other, filled form returns with labels intact.

### Tests for User Story 4

- [ ] T028 [P] [US4] Unit test joint pinpoint (joint-off rejects; joint-on applies+saves original; transient dismiss restores; permanent persists; overlap last-writer-wins; out-of-bounds/closed-page rejected+reported) (FR-012/FR-013/FR-014) in `glp_quick/tests/unit/test_joint.py`.
- [ ] T029 [P] [US4] Unit test masks/forms (define labels+fields; fill returns with fixed labels intact) (FR-015) in `glp_quick/tests/unit/test_forms.py`.
- [ ] T030 [P] [US4] Integration test (host-gated): counterpart pinpoint applied on owner page with original recoverable; mask filled+returned, in `glp_quick/tests/integration/test_us4_joint_forms_mesh.py`.

### Implementation for User Story 4

- [ ] T031 [P] [US4] Implement pinpoint change (region save/restore, transient/permanent, bounds/closed checks, last-writer-wins) in `glp_quick/src/glp_quick/terminal/joint.py` per `data-model.md`.
- [ ] T032 [P] [US4] Implement mask/form (fixed labels + fillable regions + fill/return) in `glp_quick/src/glp_quick/terminal/forms.py`.
- [ ] T033 [US4] Wire `/joint`, `/pin`, `/undo-pin`, `/mask`, `/fill` + `tmsg(pinpoint|form_def|form_fill)` send/receive in `glp_quick/src/glp_quick/tui.py` + `terminal/protocol.py`.

**Checkpoint**: US1–US4 independently functional.

---

## Phase 7: User Story 5 — Live GLP REPL page & agent-sent pages (Priority: P5)

**Goal**: `/repl` spawns a page bound to a live GLP REPL over the link (goals evaluated, results rendered on that page); agents can also send a plain page for the user to edit and return.

**Independent Test**: `/repl` creates a new named page; a GLP goal entered there is evaluated over the link and its result shown; closing it leaves other pages intact; an agent-sent plain page is editable and returnable.

> **Prerequisite (R10)**: the `repl_link` process bridge (spawn/pump a GLP REPL process onto a `Handle`) is today a skeleton; T036 builds it before the REPL page can evaluate over the link.

### Tests for User Story 5

- [ ] T034 [P] [US5] Unit test REPL-page (goal→`repl_goal`; `repl_result`→page render; spawn-failure reported on a page; other pages intact) against a fake REPL bridge (FR-016) in `glp_quick/tests/unit/test_replpage.py`.
- [ ] T035 [P] [US5] Integration test (host-gated): `/repl` page evaluates a GLP goal over the link and renders the result; agent-sent plain page editable+returnable, in `glp_quick/tests/integration/test_us5_repl_page_mesh.py`.

### Implementation for User Story 5

- [ ] T036 [US5] Build the `repl_link` process bridge — spawn/pump a GLP REPL process (`out/csharp/glp_repl`; dart on demand) onto a `Handle` (R10 prerequisite) in `glp_quick/src/glp_quick/repl_link.py`.
- [ ] T037 [US5] Implement REPL-in-a-page (`/repl [name]`, `repl_goal`/`repl_result` over the link, spawn-failure reported on a page, other pages intact) in `glp_quick/src/glp_quick/terminal/replpage.py` + `tui.py` (FR-016).
- [ ] T038 [US5] Support an agent/server-sent plain (non-REPL) page for the user to edit and return (FR-017) in `glp_quick/src/glp_quick/terminal/pages.py` + `tui.py`.

**Checkpoint**: US1–US5 independently functional.

---

## Phase 8: User Story 8 — `/rcopy` responder file-service backend (Priority: P6)

**Goal**: A responder configured by `/rcopy init` (roots, permitted peers, per-root quota) that lands files under `/xfer/in/[peer-name-and-UID]/` within a permitted root, does synchronise SHA-256 comparison, keeps a per-root catalog + file-based WAL (fully recreatable) + durable provenance, all-or-nothing per file.

**Independent Test**: Configure one root, one permitted peer, a small quota; transfer a mixed set — new land under the landing dir, identical skipped in synchronise, quota/unpermitted rejected with clear outcomes, provenance recorded for all; delete the catalog → fully recreated from the WAL.

### Tests for User Story 8

- [ ] T039 [P] [US8] Unit test the WAL journal (append + replay/rebuild the catalog with 0 loss after catalog-file deletion) (FR-036/SC-010) in `glp_quick/tests/unit/test_rcopy_wal.py`.
- [ ] T040 [P] [US8] Unit test the catalog + synchronise SHA-256 compare (skip identical; force overwrites) (FR-034/FR-035) in `glp_quick/tests/unit/test_rcopy_catalog.py`.
- [ ] T041 [P] [US8] Unit test permission/quota/path-safety (`reject(perm|quota|path)`; write nothing outside a permitted root; per-file explicit outcomes) (FR-033/FR-038) in `glp_quick/tests/unit/test_rcopy_responder.py`.
- [ ] T042 [P] [US8] Unit test commit-on-complete / all-or-nothing (interrupted file discarded, no WAL/catalog/quota trace; provenance for transferred+rejected) (FR-037/FR-039) in `glp_quick/tests/unit/test_rcopy_commit.py`.

### Implementation for User Story 8

- [ ] T043 [P] [US8] Implement the per-root append-only WAL journal (append + replay/rebuild) in `glp_quick/src/glp_quick/rcopy/wal.py` per `contracts/responder-store.md`.
- [ ] T044 [P] [US8] Implement the per-root catalog projection + synchronise SHA-256 compare in `glp_quick/src/glp_quick/rcopy/catalog.py`.
- [ ] T045 [P] [US8] Implement durable provenance records (transferred + rejected) in `glp_quick/src/glp_quick/rcopy/provenance.py` (FR-037).
- [ ] T046 [US8] Implement chunked transfer receive: temp write → fsync → SHA-256 verify → atomic rename commit (FR-039) in `glp_quick/src/glp_quick/rcopy/transfer.py`.
- [ ] T047 [US8] Implement the responder service (`/rcopy init` config; offer; per-file verdict need/skip/reject; landing under a permitted root; quota/perm/path enforcement; wire WAL+catalog+provenance) in `glp_quick/src/glp_quick/rcopy/responder.py` (FR-032/FR-033/FR-034/FR-038).

**Checkpoint**: US8 responder independently testable (config + store + WAL-loss recreate).

---

## Phase 9: User Story 6 — `/rcopy` file-transfer wizard (client) + end-to-end (Priority: P6)

**Goal**: `/rcopy` opens a page-driven wizard — pick a peer (offer gate), pick root + navigate/create folder, pick local globs each with an exclusion filter, choose synchronise/force + fingerprint, submit; report per-file outcomes. Pairs with US8 for the end-to-end transfer.

**Independent Test**: With a peer offering a service + a permitted root, drive the wizard end-to-end: pick peer/root, create folder, select a glob, add a size/name filter, synchronise with fingerprint on, submit → only non-excluded new/changed files transferred, byte-identical reported skipped.

### Tests for User Story 6

- [ ] T048 [P] [US6] Unit test the exclusion filter (size/name/subdir/attribute; filtered-out reported) — pure (FR-028) in `glp_quick/tests/unit/test_rcopy_filter.py`.
- [ ] T049 [P] [US6] Unit test the wizard flow (peer select; no-service ⇒ report + 0 transfers; root/folder select; mode+fingerprint; per-file outcome mapping) against a fake responder (FR-018/FR-027/FR-029/FR-030/FR-031) in `glp_quick/tests/unit/test_rcopy_wizard.py`.
- [ ] T050 [P] [US6] Integration test (host-gated) `/rcopy` end-to-end: mixed set (new/identical/filtered/quota/reject); synchronise skips byte-identical; force overwrites; then delete the responder catalog + restart → rebuilt from WAL (SC-007/SC-009/SC-010) in `glp_quick/tests/integration/test_us6_rcopy_e2e_mesh.py`.

### Implementation for User Story 6

- [ ] T051 [P] [US6] Implement the exclusion filter (pure `(files, filter) -> (kept, filtered_out)`) in `glp_quick/src/glp_quick/rcopy/filter.py` (FR-028).
- [ ] T052 [US6] Implement the `/rcopy` client wizard (peers→offer→root→folder→globs→filter→mode→fingerprint→submit→per-file outcome page) in `glp_quick/src/glp_quick/rcopy/wizard.py` + `tui.py` (FR-018/FR-027–FR-031).
- [ ] T053 [US6] Wire the client⇄responder exchange (`rcopy_offer_query`/`rcopy_offer`, `rcopy_manifest`/`rcopy_verdict`, `rcopy_chunk`, `rcopy_outcome`) over the link per `contracts/rcopy-protocol.md` in `glp_quick/src/glp_quick/rcopy/wizard.py` + `rcopy/responder.py` + `terminal/protocol.py`.

**Checkpoint**: US6 + US8 deliver the end-to-end `/rcopy` capability.

---

## Phase 10: User Story 7 — User-bindable F-keys with typed equivalents (Priority: P7)

**Goal**: Free PF keys bindable to per-page actions / server signals; the live legend reflects bindings; every binding has a typed-command equivalent (RDP-safe).

**Independent Test**: Bind a free F-key to a page action; the legend updates; the action fires via both the key (where passed) and its typed equivalent.

### Tests for User Story 7

- [ ] T054 [P] [US7] Unit test key bindings + legend (bind a free PF key → legend updates with typed equivalent; action fires via key + typed equiv; reserved key exposes a Ctrl alternate) (FR-019/FR-020) in `glp_quick/tests/unit/test_keys.py`.

### Implementation for User Story 7

- [ ] T055 [P] [US7] Implement the `KeyBinding` model (free-key detection, `Shift+F1..F12`=PF13..24, Ctrl alternates, typed equivalents, legend labels) in `glp_quick/src/glp_quick/terminal/keys.py` (FR-019/FR-020).
- [ ] T056 [US7] Wire `/bind` + live legend update + activation (Fx / Shift / Ctrl + typed equivalent) in `glp_quick/src/glp_quick/tui.py` + `terminal/presentation.py`.

**Checkpoint**: All user stories US1–US8 independently functional.

---

## Phase 11: Polish & Cross-Cutting (US9 close-out)

**Purpose**: Prove the whole terminal does what it advertises and every SC is asserted (US9 / FR-045 / FR-046 / SC-013).

- [ ] T057 [P] Add the SC coverage-map test asserting every US1–US8 and every SC-001..SC-012 has ≥1 asserting test (SC-013) in `glp_quick/tests/test_sc_coverage_map.py`. **SC-008** (onboarding to a first transmitted message via on-screen `/help` only) is asserted by an automatable **help-completeness proxy** — assert `/help` enumerates the `//`+Enter transmit path and every core command — with the RDP/first-user manual pass noted as the real acceptance (per spec Assumptions).
- [ ] T058 [P] Update `/help` + module docstrings for the new commands (`/transmit /joint /pin /undo-pin /mask /fill /repl /layout /bind /rcopy`) in `glp_quick/src/glp_quick/tui.py` — the `/help` content is also the SC-008 proxy asserted by T057, so it MUST list the transmit path + every core command.
- [ ] T059 [P] Verify (do not re-implement) `link_console.py` parity with the `--tui` path — `@name` routing (from T017) and the `chat` codec routing through `terminal/protocol.py` (from T018) — so the no-TTY fallback matches; add the parity assertion to `glp_quick/tests/unit/test_notty_fallback.py` (FR-005). *(Consolidated with T017/T018 — no duplicate implementation.)*
- [ ] T060 Run the full suite (host-free always; host-gated when `csharp/glp_quick_host` is built) + validate `quickstart.md`; confirm no story shipped deferred/minimized/partial (FR-046).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (P1)** → no deps.
- **Foundational (P2)** → after Setup; **blocks all user stories** (protocol codec + state + routing are shared).
- **US1 (P3)** → after Foundational; **MVP**.
- **US2 (P4)** → after Foundational (uses `pages.py`, `protocol.py`); independent of US3–US8.
- **US3 (P5)** → after Foundational; `presentation.py`/`keys.py` legend consumed by US7.
- **US4 (P6)** → after US2 (operates on pages).
- **US5 (P7)** → after US2 (REPL is a page kind) + **T036 bridge** (R10 prerequisite).
- **US8 (P8)** → after Foundational (self-contained backend; store is host-free testable).
- **US6 (P9)** → after US8 for the e2e leg (wizard unit-testable earlier against a fake responder).
- **US7 (P10)** → after US3 (legend).
- **Polish (P11)** → after all desired stories.

### Within each story

- Tests written first and FAIL before implementation (US9 TDD).
- Models (`pages`/`joint`/`forms`/`keys`/`wal`/`catalog`/`provenance`) before services (`state`/`wizard`/`responder`) before wiring (`tui.py`).

### Parallel opportunities

- Setup: T002, T003 parallel.
- Foundational: T005, T008 (tests) parallel with each other; T004 before T005, T006/T007 before T008.
- US1 tests T009–T013 all `[P]` (different files). US8 store modules T043–T045 all `[P]`. US8 unit tests T039–T042 all `[P]`.
- Different stories (US2/US3/US8) can be staffed in parallel once Foundational is done.

---

## Parallel Example: User Story 1

```bash
# Tests first (all different files):
Task: "T009 no-TTY fallback test in glp_quick/tests/unit/test_notty_fallback.py"
Task: "T010 recv thread-safety test in glp_quick/tests/unit/test_recv_threadsafe.py"
Task: "T011 link-drop report test in glp_quick/tests/unit/test_link_drop_report.py"
Task: "T012 @name delivery test in glp_quick/tests/unit/test_at_delivery.py"
```

## Parallel Example: User Story 8 (responder store modules)

```bash
Task: "T043 WAL journal in glp_quick/src/glp_quick/rcopy/wal.py"
Task: "T044 catalog + synchronise in glp_quick/src/glp_quick/rcopy/catalog.py"
Task: "T045 provenance records in glp_quick/src/glp_quick/rcopy/provenance.py"
```

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **STOP & validate** (type-only conversation, hardened + tested) → demo.

### Incremental delivery

US1 (MVP) → US2 pages → US3 presentation → US4 joint/forms → US5 REPL-page → US8 responder → US6 wizard + e2e → US7 bindable keys → Polish/US9 close-out. Each story tested green before the next (FR-046). US9's bar is met continuously (per-story tests) and certified in Polish (T057 coverage-map + T060 green suite).

---

## Notes

- **US9 is cross-cutting P1**, not a separate phase: its four hardening fixes are T015 (thread-safe + non-swallowing receive), T016 (`@name` resolve), T017 (no-TTY fallback); its coverage bar is the per-story tests + T057/T060.
- `[P]` = different file, no dependency on an incomplete task.
- Host-gated integration tests skip cleanly when `csharp/glp_quick_host` is not built (matches `test_mesh.py`); the host-free unit tier must always be green.
- The two-host (SC-002) and RDP (SC-001) criteria are approximated by the loopback/mesh harness; the final acceptance adds a manual second-host pass (per spec Assumptions).
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.
- **Total: 60 tasks** — Setup 3 · Foundational 5 · US1 10 · US2 5 · US3 4 · US4 6 · US5 5 · US8 9 · US6 6 · US7 3 · Polish 4.

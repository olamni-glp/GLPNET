# Phase 0 Research — Feature 040: Virtual 3270 Terminal (Complete & Hardened)

**Spec**: `specs/040-rcopy-file-transfer-service/spec.md` · **Reference**: `specs/037-virtual-3270-term/spec.md`
**Audit basis**: `docs/research/035plus-oblivion-audit-2026-07-02.md` §C
**Guidance (resolved baseline)**: *Prefer the simplest design that satisfies the spec; call out constraints and rejected alternatives explicitly.*

This feature **extends the existing prototype** (`glp_quick/src/glp_quick/tui.py`, prompt_toolkit) and reuses the
feature-036 link/adapter seam (`StackAdapter` / `Handle` / `GlpMessage`) — FR-026, no parallel transport. All
decisions below resolve the NEEDS-CLARIFICATION items implied by the Technical Context and record the rejected
alternatives.

---

## R1 — Extend & refactor the prototype into testable model / view / receive layers

**Decision**: Keep `tui.py`'s prompt_toolkit `Application`, but extract the state and logic currently trapped in
the `run_tui` closure into importable, host-free units under a new `glp_quick/terminal/` subpackage
(`pages`, `protocol`, `routing`, `presentation`, `keys`, `joint`, `forms`, `replpage`) and a `glp_quick/rcopy/`
subpackage (`wizard`, `responder`, `wal`, `catalog`, `provenance`, `filter`, `transfer`). `tui.py` becomes the
view + wiring that binds those units to prompt_toolkit; `link_console.py` reuses the same model for parity.

**Rationale**: FR-045 mandates *unit tests exercising `tui.py` behaviors*. The current 300-line closure (every
action is a nested function over locals) is untestable without a running full-screen app. Extraction is not
gold-plating — it is the only way to satisfy FR-045 / SC-013 (100% US + SC coverage). FR-026 requires *extending*
the prototype, so the surface (`--tui`, `//`+Enter, five themes, the slash-commands) is preserved.

**Alternatives rejected**: (a) Test the closure through prompt_toolkit's app harness — brittle, slow, cannot
assert model state in isolation. (b) Rewrite from scratch — violates FR-026 and discards a working prototype
(constitution IV-b spirit: preserve working code).

---

## R2 — Terminal sub-protocol carried inside the L5 `GlpMessage` payload as a ground GLP term

**Decision**: All terminal-level messages (chat, page transmit, pinpoint change, form-def/fill, REPL goal/result,
`/rcopy` control) ride the existing `GlpMessage` envelope (`repl_link.py`) with a **tagged ground-term** payload —
a discriminated union `tmsg(<kind>, <fields…>)`, e.g. `tmsg(chat, "text")`,
`tmsg(page, "NAME", "OWNER", plain, "…text…")`, `tmsg(pinpoint, "PAGE", Row, Col, "block", transient)`,
`tmsg(rcopy_offer_query)`, `tmsg(rcopy_manifest, [file("rel", Size, "sha256"), …])`. Plain chat with no tag is
still accepted (backward-compatible default kind). A small codec module (`terminal/protocol.py`) encodes/decodes
these terms; the L4/L5 reliability, sequencing, and dedup remain spec-025's concern below the seam.

**Rationale**: The wire contract requires the payload to be a **ground GLP term** (025 ground-relay;
`assert_ground_relay` refuses `_w(` / `_r(`). Encoding terminal messages as ground terms keeps the discipline
(constitution VIII single-source, II no-workaround) and gives a clean, self-describing discriminator. Page text
is carried as a quoted term string.

**Alternatives rejected**: (a) JSON payload — passes the `_w(`/`_r(` guard but violates the *"GROUND GLP term"*
contract in `wire-contract.md`; a workaround in disguise. (b) A second side-channel/transport for pages — violates
FR-026 (no parallel transport). (c) Reusing raw chat text with in-band markers — ambiguous and unparseable.

**Open edge**: page/form text that itself contains `_w(` / `_r(` literals would trip the ground-relay guard —
resolved by quoting/escaping inside the term string in the codec (the guard sees the escaped form).

---

## R3 — `@name` directed routing resolved against the authenticated peer set (FR-006 / FR-040 / SC-011)

**Decision**: Before sending a directed message, resolve `@name` against `handle.peers()` (the endpoint_ids
reachable over the 036 mesh). If `name` is a current peer, send `to=name`; if not, **report the unknown peer** on
the status/OIA line and do **not** send to the default peer. `parse_addressed` (shared by `tui` + `link_console`)
stays the single parse point; a new resolve step adds the membership check. Peer identity **is** the feature-036
link-authenticated endpoint id (clarification 2026-07-03) — never a self-declared handle. Page ownership and the
responder permitted-peer/quota check are keyed to the same identity.

**Rationale**: `route()` already returns `[]` for a non-member `to`, so today an unknown `@name` is *silently
dropped* (the audit's HIGH defect). `handle.peers()` exists on the `Handle` ABC precisely for this membership
check. This is a spec-mandated correctness fix, not robustness padding.

**Alternatives rejected**: send-and-hope (current behavior — silent loss); a self-declared name registry (breaks
the clarification — a peer could spoof another's routing/ownership/permissions).

---

## R4 — Receive-path thread-safety by serializing state mutation on the UI event loop (FR-042 / SC-012)

**Decision**: The background receive executor performs **I/O only** (`handle.recv`); every mutation of shared
terminal state (pages, unread flags, peer list, OIA) is posted onto prompt_toolkit's event loop via
`loop.call_soon_threadsafe(...)` so all state changes run on a single thread, in order. The extracted model
additionally guards its mutators so the host-free unit tests can drive concurrent receives against a fake handle
and assert no corruption.

**Rationale**: Today `recv_loop` mutates `pages` off the UI thread with no synchronization (audit: receive-path
race). Serializing on the loop is the race-free protocol fix (constitution II — a *protocol*, not an ad-hoc lock
sprinkled to paper over a race). A single loop-thread owner is simpler than fine-grained locking.

**Alternatives rejected**: a coarse global lock around every read/write (works but easy to get wrong and to
deadlock with prompt_toolkit's own redraw); ignoring the race (data-integrity defect, forbidden by US9).

---

## R5 — No-TTY plain-line fallback actually engages under `--tui` (FR-005 / FR-041 / SC-003)

**Decision**: `cli.py` already gates `use_tui = isatty(stdin) and isatty(stdout)` and drops to
`link_console.run(...)` otherwise. Harden it (any `isatty` exception ⇒ fallback; emit the one-line notice) and
**assert it** with a unit test that monkeypatches `sys.stdin.isatty`/`sys.stdout.isatty` to False and confirms the
tui path is not taken and no exception is raised. `link_console` gains the same `@name` unknown-peer parity (R3).

**Rationale**: US9 flags the no-TTY fallback as a HIGH defect ("errors instead of engaging"). The gate exists but
is untested; SC-003 needs it asserted at 100%.

**Alternatives rejected**: forcing full-screen always (breaks piped/redirected + the demo/test harness).

---

## R6 — Link errors/drops surfaced, never swallowed (FR-043 / FR-044 / SC-012)

**Decision**: The receive loop distinguishes three outcomes of `handle.recv`: `None` (graceful close → report
"link closed", keep the terminal locally operable), an exception / FR-019 terminal signal (fault → surface the
token on the OIA/status and a reported condition, keep operable), and a normal message. Transmit and `/rcopy`
actions on a down/absent link report "no peer connected" (FR-044) rather than hanging or crashing. Nothing is
silently caught-and-returned.

**Rationale**: `Handle.recv` is contracted to surface a dropped link as *"a clear terminal signal (FR-019), never
a silent hang"*; the terminal must propagate that to the user. The current `except Exception` prints once then
returns — close, but must route to the OIA link-state indicator and remain operable (SC-012 "remains operable
locally").

---

## R7 — `/rcopy` responder durable store: file-based WAL as source of truth + rebuildable projections (US8)

**Decision**: The responder's durability is a **per-root append-only WAL journal file** (the authoritative record
of every catalog mutation), from which the **per-root catalog** and the **provenance** records are **projections
rebuilt on start / after loss**. SHA-256 via `hashlib` (stdlib). A file is journaled + catalogued **only after**
it is received to a temp path, `fsync`-ed, SHA-256-verified, and atomically `rename`-committed into its target
under the peer's landing dir (FR-039 commit-on-complete; partial receipts leave no WAL/catalog trace). The store
lives in the responder's configured data directory (set by `/rcopy init`), **outside** the repo working-data
cluster.

**Constitution VI-b / VI-a**: This design creates **no second PGLite working-data cluster inside the repo** and
adds **no codeconv Alembic migration** — the WAL is a plain file, the projections are files. The spec *assumption*
names "a PGlite-backed DuckLake for the catalog/provenance", but the same assumption **explicitly defers the store
wiring to planning** ("the exact store wiring is an implementation detail resolved at planning time; the spec
requires only durable recording and WAL-based recreatability"). The file-WAL design fully satisfies FR-035/036/037
and SC-009/010 while keeping `glp_quick` dependency-light (it has no DB dep today) and constitution-clean. If a
queryable PGlite/DuckLake projection is later wanted, it MUST be an out-of-repo **isolated per-service** store
reached via `codeconv.bridge_client` (the VI-b v1.1.0 exemption for ephemeral out-of-repo stores), never a repo
`.pgdb` schema.

**⚠ Owner-awareness flag (surfaced at `/bk-analyze`)**: the chosen backend (file WAL + file projections) diverges
from the *named* backend in the spec assumption (PGlite-DuckLake) — but within the latitude the assumption itself
grants. Recorded here so analyze can confirm rather than silently accept.

**Alternatives rejected**: (a) A repo `.pgdb` schema for the catalog — violates VI-b (second working-data
consumer/schema) and drags Alembic single-head discipline (VI-a) into a runtime file service. (b) Catalog as the
source of truth with the WAL as a secondary log — inverts FR-035/036 (the WAL must be able to fully recreate the
catalog after catalog loss).

---

## R8 — `/rcopy` transfer protocol over the same link seam (US6 client ⇄ US8 responder)

**Decision**: The wizard and responder exchange `tmsg(rcopy_*)` terms over the 036 link:
1. `rcopy_offer_query` → `rcopy_offer([root(name, [folders…], quota_left)…])` (only roots the peer is permitted
   for; empty ⇒ "no file service available").
2. `rcopy_manifest([file(rel, size, sha256)…], mode, target_root, target_folder)` → responder replies per file
   with `need` / `skip_identical` / `reject(quota|perm|path)`.
3. For each `need` file the client streams `rcopy_chunk(rel, seq, bytes_b64)`; responder writes to a temp path,
   verifies SHA-256, atomically commits under `/xfer/in/[peer-name-and-UID]/<target_folder>/…`, appends the WAL,
   updates the catalog, records provenance, and returns `rcopy_outcome(rel, transferred|rejected|…)`.
4. Synchronise compares the manifest SHA-256 against the catalog (skip identical); force-overwrite bypasses the
   compare. Fingerprint (SHA-256) defaults on.

All-or-nothing per file (FR-039): interrupted files are discarded (temp never renamed), so re-running synchronise
re-sends only the still-missing/changed files. Large files ride the 025 reliability sublayer (the >1 MiB relay
reassembly fix already landed in the gleam relay); chunk size is bounded well under the frame limit.

**Rationale**: Reuses FR-026's single transport; keeps the responder authoritative for what lands on disk (writing
files is *exclusively* the responder's job, per Assumptions). Base64 keeps binary payloads inside the ground-term
text discipline.

**Alternatives rejected**: a side file-transfer socket (violates FR-026); writing files as a side effect of a page
transmit (explicitly out of scope / an Assumption forbids it).

---

## R9 — Exclusion filter as a pure, host-free rule set (FR-028)

**Decision**: An `ExclusionFilter` is a rule set applied to a candidate file list before transfer: by **size**
(min/max), **filename** (glob or regex), **subdirectory** (glob), and **attribute** (hidden / read-only / mtime
window). It is a pure function `(files, filter) -> (kept, filtered_out)` in `rcopy/filter.py`, unit-tested without
any link or host.

**Rationale**: Deterministic, trivially testable, and the filtered-out set feeds the per-file outcomes (FR-031).

---

## R10 — REPL-in-a-page via the `repl_link` process bridge (US5 / FR-016) — with a prerequisite

**Decision**: `/repl [name]` spawns a page bound to a live GLP REPL over the link: goals entered on the page are
sent as `tmsg(repl_goal, "goal.")`, evaluated over the link by the bridged REPL, and the `tmsg(repl_result, …)` is
rendered on that page — without disturbing other pages. Uses the repo default REPL (C# `out/csharp/glp_repl`;
Dart on demand) reached over the 036 link.

**Prerequisite / risk**: The `repl_link` **process bridge** that actually spawns and pumps a GLP REPL process onto
a `Handle` is today a documented **skeleton** (`repl_link.py`: *"the bridge … lands in US1 (T019) / US2 (T027)"*;
memory: 036 T019 "live glp_repl-process bridge" is the next-unbuilt item). US5 depends on it. **040 builds this
bridge in scope** (the REPL-over-link is feature 036's raison d'être and 040 is the completion home); US5 is
sequenced after the bridge task. If a REPL cannot be spawned, the failure is reported on a page and the rest of the
terminal stays usable (edge case).

**Rationale**: FR-016 is explicit; the bridge contract already exists in `repl_link.py` — 040 supplies the
behavior. **No GLP language feature is added** (goals run on the existing REPL) ⇒ constitution IV-a is not
triggered (no owner language-authority gate).

**Alternatives rejected**: an in-terminal GLP interpreter (re-implements the REPL; violates single-source and
IV-a); deferring US5 (forbidden — FR-046: no story shipped deferred).

---

## R11 — PF-key bindings + dynamic legend with typed equivalents (US3 / US7 / FR-019 / FR-020 / FR-025)

**Decision**: A `KeyBinding` model maps a free PF key (and its Shift = PF13–24 / Ctrl alternate variants) to a
page action or server signal, each carrying a **typed-command equivalent** and a legend label. Activation order:
`Fx` direct (no modifier); `Shift+F1..F12` = PF13..PF24; `Ctrl` alternates as fallbacks for host-reserved keys.
The dynamic PF-legend renders as small reverse-video blocks just above the command line, each labelled with its
action + typed equivalent. **Every** binding has a typed equivalent so the whole feature stays RDP-safe (FR-002).

**Rationale**: Directly encodes FR-019/020/025; the typed-equivalent invariant is what makes SC-001 (100% actions
with zero function keys) hold.

---

## R12 — Presentation: five themes, OIA, two-strip layout, splash (US3 / FR-021..025)

**Decision**: Extract the five themes (GREEN/AMBER/WHITE/PAPER/COLOR, already present, with the purple/magenta
command accent) into `terminal/presentation.py`. Add the **two-strip** compose layout (FR-023) as an alternative
to the N-command-line layout (`GLPQUICK_CMDLINES`, default ~3): a scrollable counterpart-response strip above a
scrollable user command strip, ~1 line each, separated by rules. The OIA line shows mode, current page X/N + name
+ owner, link info, and the PF-legend (FR-022). Keep the ASCII splash (FR-024).

**Rationale**: Mostly present; the deltas are the two-strip layout, the legend blocks (R11), and OIA enrichment
(page owner by peer name).

---

## R13 — Test strategy: host-free unit tier + host-gated mesh-integration tier (US9 / FR-045 / SC-013)

**Decision**: Two tiers under `glp_quick/tests/`:
- **Host-free unit** (always run): import the extracted modules **and `tui.py`** — page model & ownership,
  protocol codec round-trips, `@name` resolve/unknown-report, no-TTY fallback (monkeypatched `isatty`),
  receive-path thread-safety & link-drop reporting (a **fake in-memory `Handle`**), exclusion filter, WAL
  append/replay/rebuild, SHA-256 synchronise, quota, joint pinpoint (save/restore/transient/permanent), masks,
  PF-legend/bindings, presentation/OIA.
- **Host-gated mesh-integration** (`skipif not host_dll_path().exists()`, matching `test_mesh.py`): cross-endpoint
  page transmit + ownership, `@name` delivery to the named peer + unknown report, `/rcopy` end-to-end
  (new/identical/filtered/quota/reject + WAL-loss recreate), REPL-page evaluation.
- A **coverage map** test asserts every US1–US8 and every SC-001..SC-012 has ≥1 asserting test (SC-013 is the
  meta-criterion satisfied by the map + a green suite).

**Rationale**: Mirrors the established repo convention (pure-logic tests like `test_routing.py` run host-free;
real-QUIC tests skip without the built dll). The fake `Handle` makes the terminal's own logic (US1–US9 behaviors)
testable without a live link; the two-host / RDP success criteria are approximated by a loopback/mesh harness per
the spec Assumptions, with a manual second-host pass noted as the final acceptance.

**Alternatives rejected**: only integration tests (can't assert model internals, slow, host-dependent — fails
FR-045's "unit tests exercising `tui.py`"); mocking the whole app (no real behavior coverage).

---

## R14 — Cross-feature dependencies & sequencing

- **Feature 036 (released)** provides the link seam, the C# reference host (`csharp/glp_quick_host`, built,
  net10.0), the gleam stack (Profile A), and the `glp_quick` tool. 040 does not change the transport (Out of
  Scope).
- **The `repl_link` process bridge** (R10) is a build prerequisite for US5 and is built here.
- **Story order** follows the spec priorities, with US9 (reliability/hardening/tests) cross-cutting and gating
  every other story's "done": **US1 (P1) → US9 hardening of US1 → US2 (P2) → US3 (P3) → US4 (P4) → US5 (P5) →
  US6+US8 (P6, the `/rcopy` pair) → US7 (P7)**, each story hardened + tested before it counts as complete
  (FR-046). MVP = US1 fully working, hardened, and tested (type-only conversation + no-TTY fallback + `@name`
  routing + link-drop reporting).

---

## Resolved Technical Context (no remaining NEEDS CLARIFICATION)

| Field | Value |
|---|---|
| Language/Version | Python ≥ 3.11 (`glp_quick`); C# net10.0 reference host (built); Gleam/AtomVM relay (Profile A) |
| Primary Dependencies | prompt_toolkit, typer, cryptography, `hashlib` (stdlib); the 036 `StackAdapter`/`Handle`/`GlpMessage` seam — **no new DB dependency** |
| Storage | file-based per-root WAL journal (source of truth) + rebuildable catalog/provenance projections in the responder data dir; `/xfer/in/[peer-name-and-UID]/` landing under permitted roots — no repo PGLite cluster (VI-b) |
| Testing | pytest + pytest-mock; host-free unit tier + host-gated (`host_dll_path()`) mesh-integration tier |
| Target Platform | Windows 11 primary; trusted LAN between cooperating endpoints; Remote-Desktop-safe |
| Project Type | single-project CLI/TUI (Python control plane) over the 036 multi-stack data plane |
| Performance Goals | LAN-interactive: transmitted page/reply visible within ~2 s under normal LAN (SC-002); synchronise skips byte-identical files by SHA-256 |
| Constraints | RDP-safe (no function key ever required, no Win+Fx); receive path thread-safe & non-swallowing; all-or-nothing per file; write nothing outside a permitted root; catalog fully rebuildable from WAL (0 loss) |
| Scale/Scope | 036 mesh ≥4 named peers; many pages/side; multi-file `/rcopy` with per-root quota; US1–US9, FR-001..046, SC-001..013 |

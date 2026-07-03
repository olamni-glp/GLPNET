# Feature Specification: Virtual 3270 Terminal — Complete & Hardened (definitive)

**Feature Branch**: `037-virtual-3270-term` (shared workstream branch; 040 completes the unmerged 037 basis)
**Created**: 2026-07-03
**Status**: Draft
**Roadmap id**: `rcopy-file-transfer-service` (legacy slug — now a misnomer; `/rcopy` is one sub-part)
**Input**: User description: "Feature 040 — the definitive, complete, hardened implementation of the entire
virtual IBM-3270-style block-mode terminal layered over the feature-036 QUIC+WS GLP channel-link, shipped as
the `--tui` mode of `glp_quick`. NOT a leftover catch-all: every user story of 037 (US1–US7 / FR-001..031 /
SC-001..008) is fully implemented, hardened, and tested — with no deferral, no minimization, no distortion,
no loss — plus the `/rcopy` responder-side file service and a full automated test suite."

## Overview *(definitive-superset framing)*

Feature 040 is the **serious, complete, hardened home** for the whole virtual-3270 terminal. It **subsumes and
supersedes** feature 037's specification: every user story, functional requirement, and success criterion of
037 is carried forward here and must be **fully implemented, hardened, and covered by automated tests**. On top
of the 037 surface, 040 adds (a) the `/rcopy` **responder-side file service** that 037 explicitly split out to
this feature, and (b) an explicit **reliability/hardening + full-test-coverage** bar that closes the gap between
what the prototype *advertises* and what it *actually does*.

- **037 virtual-3270-term** remains the *best-effort* incremental prototype (`glp_quick/tui.py`, `--tui`): the
  authoritative early spec plus "implement as much as possible" increments. Left to itself it minimizes and
  defers the hard parts, so it is **not** the completion home.
- **040 (this feature)** is where the terminal is finished: thorough, hardened, totally complete. Every US,
  every FR, every SC — fully implemented and tested. This is the deliverable that actually finishes the
  terminal.

Audit basis for the gap/hardening set: `docs/research/035plus-oblivion-audit-2026-07-02.md` §C.
Reference spec (superset source): `specs/037-virtual-3270-term/spec.md`.

## User Scenarios & Testing *(mandatory)*

A conversation between two people (or a person and an agent/server) takes place over the existing feature-036
LAN channel-link. Instead of a plain scrolling chat, each side works on a virtual IBM-3270-style **block-mode**
full screen: a large editable "green-screen" PAGES area on top, a status (OIA) line, and a small
command/compose area at the bottom. You edit a whole page or command block freely and **transmit it as a unit**;
the counterpart receives it. The terminal must remain fully usable when function keys never arrive (Remote
Desktop), and must fall back to the plain line console when there is no full-screen terminal. Beyond the
037 surface, cooperating peers can run an `/rcopy` file transfer end-to-end, and the whole terminal is proven
reliable and correct by an automated test suite.

### User Story 1 - Hold a conversation over the link using only typing + Enter (Priority: P1)

A user launches the terminal mode of the tool against a peer reachable on the LAN (by IP or machine name). A
startup splash appears, then the full screen. The user types a message in the command area and transmits it by
ending input with a line that is just `//` followed by Enter. The message travels over the link to the peer;
the peer's replies appear above. Every action the user needs — help, theme, listing pages, creating/switching
pages, quitting, sending text — is available as a slash-command that is run the same way (type it, then `//` +
Enter). No function key is ever required.

**Why this priority**: This is the hard requirement and the irreducible MVP. Over Remote Desktop, function keys
(F1–F12, including Shift/Ctrl/Win combinations) do not reach the application, so a terminal that depends on them
is unusable for the primary user. A type-only conversation over the link is the smallest thing that delivers the
feature's value, and everything else layers on top of it.

**Independent Test**: Start two endpoints on a LAN; drive one purely by typing (no function keys); confirm a
message transmitted with `//` + Enter arrives at the peer, the peer's reply renders, and
`/help /theme /pages /new /next /prev /goto N /focus /quit /send <text>` all work via typed entry. Then disable
the TTY (pipe stdin) and confirm the tool falls back to the plain line console.

**Acceptance Scenarios**:

1. **Given** the terminal is running and a peer is connected, **When** the user types text and enters a line
   that is just `//` then Enter, **Then** the whole composed block is transmitted to the peer as one message
   and the command area clears.
2. **Given** the terminal is running, **When** the user types `/theme AMBER` then `//` + Enter, **Then** the
   theme changes to amber without any function key press.
3. **Given** a Remote Desktop session where function keys are intercepted, **When** the user performs every
   documented action by typing only, **Then** all actions succeed and none require a function key or a Win+Fx
   combination.
4. **Given** the process is started without an interactive full-screen terminal (output piped or not a TTY),
   **When** the tool launches, **Then** it runs the plain line console instead of the full-screen UI and still
   sends/receives over the link.
5. **Given** a peer addressed by `@<name> message`, **When** the user transmits, **Then** the message is routed
   to that peer; with no `@` prefix it goes to the default link peer.

---

### User Story 2 - Write, name, and transmit my own pages; receive the counterpart's (Priority: P2)

The user composes content on a large, scrollable, editable page in the screen area, gives pages names, and
transmits a whole page as a block to the counterpart. Each side normally writes its **own** pages and sends
them; received pages appear as the counterpart's pages. The user can create a new page, switch between pages
(next/prev/go-to-N), and list all open pages showing who owns each (me vs. the specific conversation partner by
name). A message can be directed to a specific peer with `@<name>`, which **actually delivers to that peer**.

**Why this priority**: The "pages" model is what makes this a 3270-style conversation rather than a flat chat.
It is the main organising structure for everything above the command line and is the substrate the later
joint-edit, mask, and REPL stories extend. It is also where the prototype's two most damaging gaps live — the
screen buffer is never actually transmitted, and `@name` routing is advertised but silently ignored.

**Independent Test**: Create two named pages, edit and transmit one to the peer, confirm it arrives as a page
owned by the sender (not merged into a shared chat page); list pages on both ends and confirm names and
ownership are shown correctly; switch pages by next/prev and by go-to-N; direct a message with `@<name>` and
confirm it reaches that named peer and not the default peer.

**Acceptance Scenarios**:

1. **Given** the screen area, **When** the user creates a page with a name and types multiple lines (Enter
   inserts a newline, arrow keys move the cursor), **Then** the page holds the edited block and can be
   scrolled.
2. **Given** several open pages, **When** the user lists pages, **Then** each page is shown with its name and
   owner (me / the specific peer name) and the current page is indicated.
3. **Given** an edited page, **When** the user transmits it, **Then** the counterpart receives it as a distinct
   page owned by the sending peer, without overwriting the receiver's own pages and without being dumped into a
   single shared chat page.
4. **Given** more than one page, **When** the user issues next / prev / go-to-N, **Then** the active page
   changes accordingly and the status line reflects the new current page.
5. **Given** two or more named peers connected, **When** the user transmits `@<name> message`, **Then** the
   message is delivered to peer `<name>` only; **When** `<name>` is not a connected peer, **Then** the terminal
   reports the unknown peer rather than silently sending to the default peer.

---

### User Story 3 - 3270 presentation and ergonomics (themes, OIA, layout, key legend) (Priority: P3)

The user can switch colour themes (green screen, amber, white-on-black, paper black-on-white, and a full colour
mode; command lines carry a purple/magenta accent). A 3270-style OIA status line shows the mode, the current
page (X/N + name + owner), link info, and a dynamic PF-key legend. A startup ASCII-art splash is shown. The
command/compose area is configurable: a number of command lines (default ~3) or a two-strip layout — a
scrollable counterpart-response strip above a scrollable user command strip, about one line each. Where the
terminal passes function keys, they act as accelerators (Fx directly with no modifier; the 3270 convention
PF13–PF24 = Shift+F1–F12; Ctrl alternates as fallbacks), and the dynamic PF-legend is rendered as little
reverse-video blocks just above the command line — each showing its typed-command equivalent.

**Why this priority**: This delivers the authentic 3270 feel and the at-a-glance operability (status, legend,
theme) that make the terminal pleasant and legible. It is valuable on its own but not required for a working
conversation, so it sits below the core stories.

**Independent Test**: Toggle each theme and confirm colours change (command lines show the purple accent);
confirm the OIA line reports mode, current page X/N + name + owner, and link info; resize the command area via
the configuration option and confirm both the multi-line and two-strip layouts render; confirm the PF-legend
shows reverse-video blocks each labelled with its typed equivalent.

**Acceptance Scenarios**:

1. **Given** the terminal, **When** the user toggles the theme, **Then** the colour scheme cycles through
   GREEN, AMBER, WHITE-on-black, PAPER (black-on-white), and full COLOUR, and the command lines keep a
   purple/magenta accent.
2. **Given** any state, **When** the user looks at the OIA line, **Then** it shows the current mode, the current
   page as X/N with its name and owner, link information, and the active PF-key legend.
3. **Given** the layout configuration set to the two-strip option, **When** the terminal renders, **Then** a
   scrollable counterpart-response strip appears above a scrollable user command strip, each about one line
   tall, separated by rules.
4. **Given** a terminal that passes function keys, **When** the user presses a bound Fx (or its Shift = PF13–24
   / Ctrl alternate), **Then** the corresponding action fires, and the same action is also reachable by its
   typed-command equivalent shown in the legend.

---

### User Story 4 - Joint live edit (pinpoint overwrite) and masks/forms (Priority: P4)

With joint mode toggled on for a shared page, the counterpart can send a **live pinpoint change** — a block of
characters that overwrites a region of the page — while the **original content is saved**, so the change can be
a transient framed/bordered comment or highlight that is later removed, or a permanent overwrite if that is the
intent. Separately, one side can set up a **mask/form** on a page (fixed labels plus fillable regions) and the
other side fills in the values and sends it back.

**Why this priority**: Joint editing and forms turn the terminal from "send whole pages" into a collaborative
surface, which is a meaningful capability but clearly an extension of the page model rather than part of the
MVP.

**Independent Test**: Enable joint mode on a page shared between two endpoints; from the counterpart, overwrite
a region and confirm it appears on the owner's page with the original recoverable; mark one change transient
(framed) and one permanent and confirm transient changes can be reverted to the saved original while permanent
ones persist. Separately, define a mask on one side, fill it on the other, and confirm the filled form returns
to the originator.

**Acceptance Scenarios**:

1. **Given** joint mode is off, **When** the counterpart attempts a pinpoint change, **Then** it is not applied
   to the owner's page (joint edits require joint mode to be enabled).
2. **Given** joint mode is on for a page, **When** the counterpart sends a pinpoint block overwriting a region,
   **Then** that region is replaced on the owner's page and the original content for that region is retained.
3. **Given** a transient (framed) pinpoint change, **When** the owner dismisses it, **Then** the saved original
   is restored; **Given** a permanent change, **When** dismissal is attempted, **Then** the overwrite remains.
4. **Given** a page configured as a mask/form with fixed labels and fillable regions, **When** the other side
   fills the fillable regions and transmits, **Then** the originator receives the completed form with the fixed
   labels intact.

---

### User Story 5 - Live GLP REPL page and agent-sent fillable pages (Priority: P5)

A command spawns a **new named page bound to a live virtual GLP REPL** running over the link, so the page can
respond to entered goals — tying the terminal back to feature 036's purpose of running GLP across the
channel-link. Pages can also simply be sent without any REPL: an agent or server can push a page for the user
to complete or edit and send back.

**Why this priority**: The REPL-in-a-page is the headline tie-back to running GLP over the link, but it depends
on the page model and the link transport from the earlier stories, so it is sequenced after them.

**Independent Test**: Issue the command to spawn a REPL page; confirm a new named page is created in the same
terminal, that a GLP goal entered there is evaluated and its result shown on that page, and that closing it
leaves other pages intact. Separately, have an agent/server send a plain (non-REPL) page; confirm the user can
edit and return it.

**Acceptance Scenarios**:

1. **Given** the terminal, **When** the user issues the spawn-REPL command, **Then** a new named page is created
   bound to a live virtual GLP REPL over the link, without disturbing existing pages.
2. **Given** a REPL page, **When** the user enters a GLP goal and transmits, **Then** the goal is evaluated over
   the link and the result is rendered on that page.
3. **Given** an agent/server-sent plain page (no REPL), **When** the user edits it and transmits, **Then** the
   edited page is returned to the sender.

---

### User Story 6 - `/rcopy` file-transfer wizard (client side) (Priority: P6)

The user issues `/rcopy` (or a bindable PFx) and a page-driven wizard opens. It lists the peers currently
reachable and lets the user pick one; submitting opens a transfer conversation with that peer **only if the peer
offers a file service to this user**. The peer's offered **upload roots** are shown; the user selects one or
more, and within a root navigates to an existing folder or creates a new one as the target. On the local
workstation the user selects one or more **file specs/globs**, and for each defines an **exclusion filter**
(drop files by size, filename pattern, subdirectory glob, or other file attributes). The user chooses
**synchronise** (transfer only new or modified files) or **force overwrite**, and a **fingerprint** option
(default on) computes a SHA-256 per file and, when synchronising, compares it to the target's existing SHA-256
so identical files are skipped. Submit then performs the transfer over the link and reports per-file outcomes
(transferred / skipped-identical / filtered-out / rejected).

**Why this priority**: File transfer to a peer is a substantial, self-contained capability that builds on the
page/mask machinery of the earlier stories. It is high-value but clearly layered on top of a working terminal,
so it comes after the conversation, pages, and presentation stories. It pairs with US8 (the responder backend)
for the end-to-end transfer.

**Independent Test**: With a peer offering a file service and at least one permitted upload root, drive the
wizard end-to-end: pick the peer, select a root, create a target folder, select a local glob, add a size/name
filter, choose synchronise with fingerprinting on, submit, and confirm only the non-excluded, new-or-changed
files are transferred while byte-identical files are reported as skipped.

**Acceptance Scenarios**:

1. **Given** a peer that does NOT offer this user a file service, **When** the user selects that peer and
   submits, **Then** the wizard reports that no file service is available and no transfer occurs.
2. **Given** a peer offering one or more upload roots, **When** the user selects a root and navigates, **Then**
   the user can enter an existing folder or create a new folder as the transfer target.
3. **Given** selected local file-specs/globs each with an exclusion filter, **When** the user submits, **Then**
   files matching an exclusion (by size / name pattern / subdir glob / attribute) are not transferred and are
   reported as filtered-out.
4. **Given** synchronise mode with fingerprinting on, **When** a selected file already exists on the target with
   an identical SHA-256, **Then** that file is skipped; **Given** force-overwrite mode, **Then** the file is
   transferred regardless.

---

### User Story 7 - User-bindable F-keys with typed equivalents (Priority: P7)

Free (unassigned) PF keys are user-bindable to per-page actions or quick signals to the server (acting on a page
before sending it). The live PF-legend reflects the current bindings, and every binding has a typed-command
equivalent so it remains usable when function keys do not arrive (Remote Desktop).

**Why this priority**: Custom key bindings are a power-user convenience layered on the established page/command
model; the last capability to add.

**Independent Test**: Bind a free F-key to a page action, confirm the legend updates, and confirm the action
fires via both the key (where the terminal passes it) and its typed-command equivalent.

**Acceptance Scenarios**:

1. **Given** a free PF key, **When** the user binds it to a page action or server signal, **Then** the live
   PF-legend reflects the new binding and shows its typed-command equivalent.
2. **Given** a bound F-key, **When** the function key does not reach the application (e.g. Remote Desktop),
   **Then** the same action is still available via its typed-command equivalent.

---

### User Story 8 - `/rcopy` responder file-service backend (Priority: P6)

The peer being copied to runs a **responder-side file service** that the US6 client wizard talks to. An operator
configures the service via `/rcopy init`: it declares **upload roots**, the **peers permitted** to write into
each root, and an optional **per-root quota**. Incoming files land in a `/xfer/in/[peer-name-and-UID]/` directory
**under a permitted root** — never outside one. In synchronise mode the responder computes/looks up the
**SHA-256** of existing target files and reports identical files so the client skips them; force-overwrite
bypasses the comparison. The service keeps a **per-root file+directory catalog** of what it holds, and every
catalog mutation is journaled to a **file-based WAL** first so the catalog is fully recreatable after loss. Every
transfer's per-file outcome and provenance (peer, root, target path, timestamps, SHA-256) is recorded durably.

**Why this priority**: The responder is what makes US6 an end-to-end capability rather than a one-sided wizard.
It is the substantial, self-contained backend 037 explicitly split out to this feature; it pairs with US6.

**Independent Test**: Configure a responder with one permitted root, one permitted peer, and a small quota. From
the client, run a transfer of a mixed set (some new, some byte-identical, some quota-exceeding). Confirm new
files land under `/xfer/in/[peer-name-and-UID]/`, identical files (matching SHA-256) are skipped in synchronise,
quota-exceeding and unpermitted transfers are rejected with clear outcomes, and provenance for every file is
recorded. Then delete the catalog and confirm it is fully recreated from the WAL journal.

**Acceptance Scenarios**:

1. **Given** a responder configured with `/rcopy init` (roots, permitted peers, per-root quota), **When** a
   permitted peer transfers a new file to a permitted root, **Then** the file lands under
   `/xfer/in/[peer-name-and-UID]/` within that root and is recorded in the per-root catalog.
2. **Given** a peer not permitted for a root (or a root the peer was not offered), **When** the peer attempts a
   transfer to it, **Then** the responder rejects it with a clear "rejected" per-file outcome and writes nothing
   outside a permitted root.
3. **Given** synchronise mode, **When** a transferred file matches the SHA-256 of an existing target file,
   **Then** the responder reports it identical so the client skips it; **Given** force-overwrite, **Then** the
   file is written regardless.
4. **Given** a transfer that would exceed the root's quota, **When** the peer submits, **Then** the offending
   files are rejected for quota with a clear per-file outcome (not silently dropped).
5. **Given** a populated per-root catalog and its WAL journal, **When** the catalog store is lost and the
   service restarts, **Then** the catalog is fully recreated from the WAL journal with no inventory loss.
6. **Given** any completed or rejected transfer, **When** it finishes, **Then** send/receive provenance (peer,
   root, target path, timestamps, SHA-256, outcome) is durably recorded for every file.

---

### User Story 9 - Reliability, hardening & full automated test coverage (Priority: P1, cross-cutting)

The terminal must actually do what it advertises, stay correct under real link conditions, and be **proven** by
an automated test suite. This is the defining differentiator of 040 over the 037 prototype and is the
**Definition of Done for every other story**: no story is "complete" until its behavior is hardened and its
acceptance is asserted by automated tests.

**Why this priority**: The prototype advertises capabilities it does not deliver (`@name` routing silently
falls back; the no-TTY fallback errors instead of engaging), swallows link exceptions silently, races on shared
state in the receive path, and has **zero tests importing `tui.py`**. These are correctness and data-integrity
defects, not polish — so this bar is P1 and gates the whole feature.

**Independent Test**: (a) With ≥2 named peers, send `@<name>` messages and assert delivery to the named peer and
a reported error for an unknown name — never a silent fallback. (b) Launch `--tui` with piped/redirected stdin
and assert the plain-line fallback engages (no error). (c) Inject a link drop / receive error and assert it is
surfaced to the user (OIA/status), not swallowed. (d) Drive concurrent receives under load and assert page/peer
state is not corrupted. (e) Run the automated suite and confirm every user story and every success criterion
SC-001..SC-013 has at least one asserting test and the suite is green.

**Acceptance Scenarios**:

1. **Given** ≥2 named peers connected, **When** the user directs a message with `@<name>`, **Then** it is
   delivered to that peer; **When** `<name>` is unknown, **Then** the terminal reports the unknown peer and does
   NOT silently send to the default peer.
2. **Given** `--tui` launched with stdin not a TTY (piped/redirected), **When** the tool starts, **Then** the
   plain-line console fallback engages and sends/receives over the link, rather than raising an error.
3. **Given** the receive path, **When** a link error or drop occurs, **Then** the condition is surfaced to the
   user (status/OIA and a reported condition) and never silently swallowed; the terminal remains operable
   locally.
4. **Given** concurrent inbound messages updating shared page/peer state, **When** they are processed, **Then**
   the shared state is protected by synchronization and is never corrupted or lost to a race.
5. **Given** the whole terminal, **When** the automated test suite runs, **Then** unit tests exercising `tui.py`
   and link-integration tests cover every user story (US1–US8) and assert every success criterion
   (SC-001..SC-013), and the suite is green.

---

### Edge Cases

- **No peer / link down**: the terminal still starts and is operable locally; transmit attempts report that no
  peer is connected rather than failing silently or crashing (FR-044).
- **`@name` to an unknown/disconnected peer**: reported clearly; the message is NOT silently redirected to the
  default peer (FR-040).
- **Function key reserved by the host terminal** (e.g. Windows Terminal F11 = fullscreen): the action remains
  reachable via its Ctrl alternate and its typed-command equivalent.
- **Not a TTY / output redirected**: the tool runs the plain line console fallback instead of the full-screen UI
  (FR-005/FR-041).
- **Link error/drop during receive**: surfaced to the user, never swallowed; no partial/corrupt page state
  (FR-042/FR-043).
- **`/rcopy` to a peer that offers no file service to this user**: the wizard reports "no file service
  available" and performs no transfer.
- **`/rcopy` selection where every file is excluded by the filter, or the target root's quota would be
  exceeded**: the user is told the per-file outcomes (all filtered-out, or rejected for quota) rather than the
  transfer appearing to do nothing.
- **`/rcopy` target path that would escape a permitted root** (path traversal, symlink): rejected; the responder
  writes nothing outside a permitted root (FR-033/FR-038).
- **Catalog store lost on the responder**: the per-root catalog is fully recreated from the file-based WAL
  journal on restart (FR-035/FR-036).
- **Transfer interrupted mid-file** (link drop / crash): the partial file is discarded — never committed,
  catalogued, counted toward synchronise, or counted toward quota; re-running synchronise transfers only the
  still-missing/changed files (FR-039).
- **Joint pinpoint change targets a region beyond the current page bounds**, or arrives for a page the receiver
  has closed: the change is rejected and reported, not applied blindly.
- **Two sides edit overlapping regions of the same joint page**: last-writer-wins per region and the saved
  original remains recoverable.
- **Spawning a REPL page when one cannot be started**: the failure is reported on a page and the rest of the
  terminal stays usable.
- **LAN addressing by IP or machine name only** (no domain names); an unreachable address is reported clearly.

## Clarifications

### Session 2026-07-03

- Q: What is the authoritative identity a peer is known by for `@name` routing, page ownership, and the
  responder permitted-peer/quota check? → A: The feature-036 mesh-authenticated peer identity (the link-assigned
  / registered client id), resolved by `@name` — never a self-declared handle. 040 rides 036's link security
  rather than adding a new auth layer, so a peer cannot assume another peer's routing target, page ownership, or
  file-service permissions by declaring its name.
- Q: Is the `UID` in the `/xfer/in/[peer-name-and-UID]/` landing directory stable per-peer, per-session, or
  per-transfer? → A: Stable per-peer. The `UID` is the peer's stable feature-036-authenticated identity (paired
  with a human-readable name for legibility); all of that peer's transfers land in the same directory across
  sessions, and the per-root catalog and synchronise comparison are keyed to it. A per-session or per-transfer
  UID would scatter a peer's files and break sync and quota attribution.
- Q: On an interrupted transfer (link drop / crash mid-file), is the transfer resumable/checkpointed or
  all-or-nothing per file? → A: All-or-nothing per file. Each file is received to a temporary location,
  SHA-256-verified, then atomically committed and journaled to the WAL; a partially-received file is never
  committed, catalogued, counted toward synchronise, or counted toward quota. "Resume" is at file granularity —
  re-running synchronise re-sends only the still-missing/changed files. Byte-level within-file resume is a later
  optimization, not a correctness requirement.

### Session 2026-07-02 (owner-directed 040 reframe)

- Q: What is 040's scope relative to 037? → A: 040 is the **definitive, complete, hardened superset** of 037.
  Every 037 US/FR/SC is carried here and must be fully implemented, hardened, and tested — no deferral, no
  minimization. 037 stays as the best-effort incremental prototype. The roadmap slug
  `rcopy-file-transfer-service` is a legacy misnomer; `/rcopy` is one sub-part.
- Q: Does 040 own the `/rcopy` responder backend? → A: Yes. 037 explicitly split the responder file service to
  040 (registry of roots/permitted-peers/per-root quota, `/xfer/in/[peer-name-and-UID]/` landing, responder
  SHA-256 sync comparison, per-root catalog, WAL journal, DuckLake provenance). It is US8 here.
- Q: What is the "hardened" bar? → A: The four prototype defects the audit flags HIGH must be genuinely fixed
  and tested — `@name` routing must actually route, the no-TTY fallback must actually engage under `--tui`, the
  receive path must be thread-safe and must not silently swallow exceptions, and link drops must be reported —
  plus full automated test coverage (US9).

### Session 2026-06-29 (carried from 037 — settled)

- Q: Peer scope — converse with and own pages across multiple named peers, or strictly 1:1 with one link peer?
  → A: Multiple named peers. A page's owner is a specific peer name; `@<name>` routes a message to that peer;
  the OIA line and page list show peer names. (036 is a genuine ≥4-client mesh.)
- Q: When a peer transmits a page while I am composing, does my view auto-switch to it? → A: No auto-focus. The
  received page appears in the page list with an OIA "new page" indicator; the user switches to it via `/next` /
  `/goto`.
- Q: How are joint-mode edits to overlapping regions of the same page resolved? → A: Last-writer-wins per
  region, with the saved original always recoverable.
- Q: What are the file/glob "send" semantics? → A: An `/rcopy` peer-to-peer file-transfer wizard (client, US6)
  plus its responder file service (US8). A page transmit never writes to the receiver's filesystem; writing
  files to disk is exclusively the job of the `/rcopy` responder, into `/xfer/in/[peer-name-and-UID]/` under a
  permitted root.

## Requirements *(mandatory)*

### Functional Requirements

**Operating modes & fallback**

- **FR-001**: The terminal MUST be shipped as the `--tui` mode of the existing `glp_quick` tool and run over the
  feature-036 QUIC+WS channel-link.
- **FR-002**: The terminal MUST be fully operable using only character input plus Enter — every action MUST have
  a path that requires no function key and no Win+Fx combination (RDP-safe).
- **FR-003**: Transmitting the current block MUST be possible by entering a line that is just `//` followed by
  Enter.
- **FR-004**: The following actions MUST each be available as a typed slash-command run by typing it and
  transmitting: help, theme (cycle or set by name), list pages, new page, next page, previous page, go-to page
  N, toggle focus, quit, and send text. Commands MUST include at least `/help`, `/theme [name]`, `/pages`,
  `/new [name]`, `/next`, `/prev`, `/goto N`, `/focus`, `/quit`, `/send <text>`.
- **FR-005**: When not attached to an interactive full-screen terminal (no TTY), the tool MUST fall back to the
  plain line console while still sending and receiving over the link.
- **FR-006**: The terminal MUST support multiple named peers on a LAN, each addressable by IP address or machine
  name (no domain-name resolution required). A message MUST be directable to a specific peer via `@<name>`
  (defaulting to the current link peer when none is specified), and a page's owner MUST be identified by the
  specific peer's name (not a generic "partner"). A peer's name MUST be the feature-036 link-authenticated peer
  identity (the mesh-assigned/registered client id); `@<name>` MUST resolve against that authenticated identity,
  never a self-declared handle.

**Block mode & editing**

- **FR-007**: The screen MUST present a block-mode editing area where the user edits freely (Enter inserts a
  newline; arrow keys move the cursor) and transmits the whole block as one unit on an AID/transmit action.
- **FR-008**: Where the terminal passes them, function keys MUST act as transmit/AID accelerators (including F9,
  and Ctrl-X and Alt-Enter as alternates), in addition to the `//`-line transmit.

**Pages**

- **FR-009**: Pages MUST be named, creatable, and switchable (next, previous, go-to-N), and the user MUST be
  able to list all open pages with each page's owner shown as "me" or the specific peer's name.
- **FR-010**: Each side MUST normally author its own pages and transmit them; a received page MUST appear as a
  page owned by its sending peer and MUST NOT overwrite the receiver's own pages or be merged into a single
  shared chat page. A received page MUST NOT steal focus from the user's current compose/editing context: it
  MUST appear in the page list with an OIA "new page" indicator, and the user switches to it via `/next` /
  `/goto`.
- **FR-011**: The screen/pages area MUST be scrollable and editable, large relative to the command/compose area.

**Joint edit, masks/forms**

- **FR-012**: A page MUST support a joint mode toggle; only when joint mode is enabled MAY the counterpart apply
  live pinpoint changes to that page. When changes target overlapping regions, the last write MUST win per
  region while the saved original of each overwritten region remains recoverable.
- **FR-013**: A pinpoint change MUST overwrite a defined region of the page while preserving the original
  content of that region so it can be restored.
- **FR-014**: A pinpoint change MUST be classifiable as transient (a framed/bordered comment or highlight that
  can be dismissed back to the saved original) or permanent (the overwrite persists).
- **FR-015**: A side MUST be able to define a mask/form on a page (fixed labels plus fillable regions); the
  other side MUST be able to fill the fillable regions and return the completed form with the fixed labels
  intact.

**REPL-in-a-page & agent-sent pages**

- **FR-016**: A command MUST spawn a new named page bound to a live virtual GLP REPL that runs over the link,
  where entered GLP goals are evaluated and results rendered on that page, without disturbing other open pages.
- **FR-017**: An agent/server MUST be able to send a plain (non-REPL) page for the user to complete or edit and
  send back.

**`/rcopy` file-transfer wizard (client side)**

- **FR-018**: `/rcopy` (and a bindable PFx equivalent) MUST open a page-driven wizard that lists reachable peers
  and lets the user select one; submitting MUST open a transfer conversation with that peer only if the peer
  offers a file service to this user, and MUST report clearly when it does not.
- **FR-027**: The wizard MUST present the upload roots the selected peer offers, let the user select one or
  more, and within a root navigate to an existing folder or create a new folder as the transfer target.
- **FR-028**: The wizard MUST let the user select one or more local file specs/globs and, per spec, define an
  exclusion filter that removes files by size, filename pattern, subdirectory glob, or other file attributes;
  filtered-out files MUST be reported as such.
- **FR-029**: The wizard MUST let the user choose a transfer mode of synchronise (transfer only new or modified
  files) or force-overwrite (transfer regardless of the target's current contents).
- **FR-030**: The wizard MUST provide a fingerprint option, default on, that computes a SHA-256 per file and, in
  synchronise mode, compares it against the target's existing SHA-256 so byte-identical files are skipped.
- **FR-031**: Submitting a transfer MUST report per-file outcomes (transferred, skipped-identical, filtered-out,
  or rejected).

**`/rcopy` responder file-service backend**

- **FR-032**: The responder MUST provide `/rcopy init` configuration that declares upload roots, the peers
  permitted to write into each root, and an optional per-root quota; only configured roots are offered, and only
  to permitted peers (this is the "file-service offer" the US6 wizard reads).
- **FR-033**: Received files MUST land in a `/xfer/in/[peer-name-and-UID]/` landing directory located **under a
  permitted root**; the responder MUST write nothing outside a permitted root (path-traversal / symlink escape
  attempts MUST be rejected). The `[peer-name-and-UID]` MUST be the peer's stable feature-036-authenticated
  identity (a human-readable name plus the stable UID); the same peer's transfers MUST land in the same landing
  directory across sessions, and the user-chosen target folder (FR-027) is nested within it.
- **FR-034**: In synchronise mode the responder MUST compute or look up the SHA-256 of existing target files and
  report byte-identical files so the client skips them; force-overwrite MUST bypass the comparison and write
  regardless. The comparison MUST be against the files already present under that same peer's stable landing
  directory and chosen target folder (per FR-033), so synchronise reflects what that peer previously sent.
- **FR-035**: The responder MUST maintain a per-root file+directory catalog (the authoritative inventory of what
  it holds) that is fully recreatable from the WAL journal.
- **FR-036**: Every catalog mutation MUST be written to a file-based WAL journal before the catalog is updated,
  such that after loss of the catalog store the catalog is fully rebuildable from the WAL with no inventory
  loss. A file MUST be journaled and catalogued only after it is fully received and SHA-256-verified
  (commit-on-complete, per FR-039); partial receipts leave no WAL/catalog trace.
- **FR-037**: The responder MUST durably record send/receive provenance for every file — peer, root, target
  path, timestamps, SHA-256, and outcome — for audit.
- **FR-038**: The responder MUST enforce permissions and per-root quota: a transfer to a root a peer is not
  permitted for, or that would exceed the root's quota, MUST be rejected with a clear per-file "rejected"
  outcome and MUST NOT be silently partially applied. Permission MUST be keyed to the peer's feature-036
  link-authenticated identity (FR-006), so a peer cannot assume another peer's roots, quota, or landing
  directory by declaring its name.
- **FR-039**: File transfer MUST be all-or-nothing per file: each file MUST be received to a temporary location
  and verified by SHA-256 before being atomically committed to its target and journaled to the WAL. A
  partially-received file (interrupted by link drop or crash) MUST NOT be committed, catalogued, counted toward
  synchronise, or counted toward quota. Resume MUST be at file granularity — re-running synchronise transfers
  only the still-missing or changed files.

**Bindable keys**

- **FR-019**: Free (unassigned) PF keys MUST be user-bindable to per-page actions or quick server signals; the
  live PF-legend MUST reflect the current bindings and every binding MUST have a typed-command equivalent.
- **FR-020**: Function-key activation MUST follow: Fx fires directly with no modifier; the 3270 convention
  PF13–PF24 = Shift+F1–F12; Ctrl alternates MUST be provided as fallbacks for keys the host terminal reserves.

**Presentation (themes, OIA, layout, splash)**

- **FR-021**: The terminal MUST provide selectable colour themes — at minimum green screen, amber,
  white-on-black, paper (black-on-white), and a full colour mode — toggled by a typed command (and by a function
  key where passed); command lines MUST carry a purple/magenta accent.
- **FR-022**: The terminal MUST display a 3270-style OIA status line showing the mode, the current page as X/N
  with its name and owner, link information, and the dynamic PF-key legend.
- **FR-023**: The command/compose area MUST be configurable: a number of command lines (default approximately 3)
  or a two-strip layout — a scrollable counterpart-response strip above a scrollable user command strip, about
  one line each, separated by rules.
- **FR-024**: The terminal MUST show an ASCII screen-art splash on startup.
- **FR-025**: The dynamic PF-legend MUST be rendered as small reverse-video blocks positioned just above the
  command line, each labelled with its action and showing its typed-command equivalent.

**Reliability, hardening & test coverage**

- **FR-040**: `@name` directed routing MUST actually deliver to the named peer; the terminal MUST NOT silently
  fall back to the default peer, and an unknown `@name` MUST be reported rather than silently redirected.
- **FR-041**: The no-TTY plain-line fallback (FR-005) MUST actually engage under `--tui` when stdin is not a TTY
  (piped/redirected), rather than raising an error.
- **FR-042**: The receive path MUST be thread-safe: concurrent updates to shared page/peer state MUST be
  synchronized and MUST NOT corrupt or lose state to a race.
- **FR-043**: The receive path MUST NOT silently swallow exceptions; a link error or drop MUST be surfaced to
  the user (status/OIA and a reported condition), never hidden.
- **FR-044**: No-peer / link-down conditions MUST be reported clearly on any transmit or wizard action; the
  terminal MUST never hang or crash on them and MUST remain operable locally.
- **FR-045**: The whole terminal MUST have automated test coverage — unit tests exercising `tui.py` behaviors
  and link-integration tests — covering every user story (US1–US8) and asserting every success criterion
  (SC-001..SC-013).
- **FR-046**: No user story (US1–US9) may be shipped deferred, minimized, or partial; a story is complete only
  when its acceptance scenarios pass AND its automated tests are green.

**Continuity with the prototype**

- **FR-026**: The feature MUST extend the existing prototype (`glp_quick/tui.py`, prompt_toolkit) and reuse the
  feature-036 link/adapter seam rather than introducing a parallel transport.

### Key Entities *(include if feature involves data)*

- **Page**: a named, scrollable, editable screen of text with an owner (me, or a specific peer by name), a kind
  (plain, mask/form, or live REPL), an optional joint-mode flag, and, for joint pages, saved original content
  for any overwritten regions.
- **Pinpoint change**: a region (position + extent) plus replacement characters, a transient-vs-permanent
  classification, and the saved original of the overwritten region.
- **Mask/form**: a page composed of fixed labels and fillable regions, plus the values entered into the fillable
  regions.
- **PF-key binding**: a mapping from a free function key (and its Shift/Ctrl/Alt variants) to a page action or
  server signal, with its typed-command equivalent and its legend label.
- **Theme**: a named colour scheme (GREEN, AMBER, WHITE, PAPER, COLOUR) including the command-line accent colour.
- **Peer**: one of multiple named LAN endpoints in the 036 mesh, addressable by IP or machine name, with a name
  used for directed messages (`@<name>`) and page ownership.
- **File-service offer**: the set of upload roots (and the folders within them) a peer exposes to this user; the
  authoritative definition lives in the responder registry (US8) and is read by the `/rcopy` wizard.
- **Upload root**: a responder-configured directory into which permitted peers may write, with the set of
  permitted peers and an optional quota.
- **Permitted peer**: a peer name authorized to write into a given upload root.
- **Quota**: an optional per-root cap on stored size (or count) enforced by the responder.
- **Landing directory**: the `/xfer/in/[peer-name-and-UID]/` location under an upload root where a given peer's
  transferred files are written; keyed to the peer's stable feature-036-authenticated identity (name + stable
  UID) so the same peer always maps to the same directory across sessions.
- **Per-root catalog**: the responder's authoritative inventory of files+directories in a root (paths, sizes,
  SHA-256, timestamps), recreatable from the WAL journal.
- **WAL journal**: an append-only file-based write-ahead log of catalog mutations from which the catalog can be
  fully rebuilt after loss.
- **Provenance record**: a durable record of a transfer's per-file outcome and metadata (peer, root, target
  path, timestamps, SHA-256, outcome) for audit.
- **Transfer request** (`/rcopy`): a client-side selection comprising the target peer, one or more chosen upload
  roots + target folders, one or more local file-specs/globs each with an exclusion filter, a transfer mode
  (synchronise / force-overwrite), and a fingerprint option (default on); its result is a set of per-file
  outcomes (transferred / skipped-identical / filtered-out / rejected).
- **Exclusion filter**: a per-file-spec rule set that drops files by size, filename pattern, subdirectory glob,
  or other file attributes before transfer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can complete every documented action (compose, transmit, theme, pages list/new/switch/
  go-to, focus, quit, send) using only typing and Enter — 100% of actions reachable with zero function-key
  presses, verified over a Remote Desktop session.
- **SC-002**: With multiple named endpoints connected on a LAN, a page transmitted from one endpoint appears on
  the other as a page owned by the sending peer (by name), without stealing the receiver's focus and without
  being merged into a shared chat page, and a reply is visible to the first user within a couple of seconds
  under normal LAN conditions.
- **SC-003**: When launched without a TTY, the tool runs the plain line console fallback in 100% of cases
  instead of erroring or producing a broken full-screen display.
- **SC-004**: All five named themes render distinctly and the command lines show the purple/magenta accent in
  every theme; a user can identify the current page (X/N, name, owner), mode, and link state from the OIA line
  without any other action.
- **SC-005**: With joint mode enabled, a counterpart pinpoint overwrite is applied to the owner's page and the
  original content is recoverable in 100% of transient cases; with joint mode disabled, 0% of counterpart
  pinpoint changes are applied.
- **SC-006**: A spawned REPL page evaluates an entered GLP goal over the link and shows its result on that page,
  while all previously open pages remain intact.
- **SC-007**: The `/rcopy` wizard completes an end-to-end transfer to a permitted peer's offered root: excluded
  files are not transferred, in synchronise mode byte-identical files (matching SHA-256) are skipped while
  new/modified files are transferred, force-overwrite transfers regardless, and every selected file ends in
  exactly one reported outcome (transferred / skipped-identical / filtered-out / rejected). A peer that offers
  this user no file service yields a clear "no file service" result and zero transfers.
- **SC-008**: A new user reaches a first successful transmitted message — from launch through splash to a
  delivered message — guided only by the on-screen `/help`, without consulting external docs.
- **SC-009**: The `/rcopy` responder lands a permitted peer's transferred files under
  `/xfer/in/[peer-name-and-UID]/` within a permitted root and never outside one; unpermitted or
  quota-exceeding transfers are rejected with a clear per-file outcome, and provenance is durably recorded for
  100% of files (transferred and rejected).
- **SC-010**: After the responder's catalog store is lost and the service restarts, the per-root catalog is
  fully recreated from the WAL journal with an inventory identical to before the loss (0 entries lost).
- **SC-011**: `@name` directed routing delivers to the named peer in 100% of cases; an unknown name is reported,
  and 0% of directed messages are silently sent to the default peer.
- **SC-012**: Under a dropped or failed link, the terminal reports the condition (no silent swallow) in 100% of
  cases and remains operable locally; under concurrent inbound load, shared page/peer state is never corrupted
  (0 races observed across the concurrency test).
- **SC-013**: The automated test suite covers 100% of user stories (US1–US8) and asserts every success criterion
  SC-001..SC-012 with at least one test each, and the suite runs green.

## Assumptions

- The feature is built on top of feature 036 (genuine QUIC + WebSocket GLP channel-link), which is already
  implemented and released; this feature reuses its link/adapter seam and does not change the transport.
- The full-screen UI is built with the prototype's existing technology (prompt_toolkit) and extends
  `glp_quick/tui.py`; behaviour described here is technology-agnostic, but the implementation continues the
  prototype rather than starting a new one.
- Transmitted pages render as page content in the terminal; they are not written to the receiver's filesystem.
  Writing files to disk is exclusively the job of the `/rcopy` responder (US8), into
  `/xfer/in/[peer-name-and-UID]/` under a permitted root — never an implicit side effect of a page transmit.
- The `/rcopy` client wizard (US6) discovers a peer's file-service offer, drives the
  selection/filter/mode/fingerprint UX on the terminal's page/mask machinery, and exchanges transfer
  requests/results with the responder (US8) over the 036 link.
- The responder's per-root catalog and provenance store use the repository's existing durable-store conventions
  (a PGlite-backed DuckLake for the catalog/provenance; a separate file-based WAL for recoverability). The exact
  store wiring is an implementation detail resolved at planning time; the spec requires only durable recording
  and WAL-based recreatability.
- The live GLP REPL spawned in a page uses the repository's default REPL (the C# GLP REPL per the repo
  convention; Dart on demand) reached over the link.
- The configurable command-line count is controlled by the existing `GLPQUICK_CMDLINES` setting (default ~3);
  the two-strip layout is an alternative arrangement of the same compose area.
- Function-key behaviour over Remote Desktop cannot be relied upon; the typed-command path is the authoritative
  interface and the function keys are accelerators only where the host terminal passes them. Win+Fx is never
  required and never relied upon.
- The terminal operates on a trusted LAN between cooperating endpoints; authentication and access control beyond
  feature 036's existing link security, plus the responder's own per-root permitted-peer / quota gate, are out
  of scope.
- "Fully tested" means automated unit + link-integration tests runnable in the repo's existing test harness;
  two-host and Remote-Desktop success criteria (SC-001 over RDP, SC-002 cross-host) may additionally require a
  manual/second-host acceptance pass, which the automated suite approximates with a loopback/mesh harness.

## Dependencies

- **Feature 036 (http3-quic-ws-link, released)**: provides the QUIC+WS channel-link, the link/adapter seam, and
  the `glp_quick` tool this feature extends.
- **Existing prototype** (`glp_quick/tui.py`, `--tui`): the starting point that already provides block-mode
  compose, transmit via `//`+Enter / F9 / Ctrl-X / Alt-Enter, five colour themes, and the core slash-commands;
  its known defects (@name routing, no-TTY fallback, silent-swallow, receive-path race, zero test coverage) are
  closed by this feature (US9).
- **Feature 037 (virtual-3270-term)**: the reference specification and best-effort incremental prototype that
  040 subsumes; 037 remains as the incremental sibling, 040 is the completion home.
- **GLP REPL** reachable over the link for the REPL-in-a-page story (US5).
- **Durable store** (PGlite-backed DuckLake + file-based WAL) for the responder catalog and provenance (US8).

## Out of Scope

- Changing the feature-036 transport or link security model.
- Domain-name resolution (LAN addressing is by IP or machine name only).
- Any file write to the receiver's filesystem outside the `/rcopy` responder's permitted-root landing directory.

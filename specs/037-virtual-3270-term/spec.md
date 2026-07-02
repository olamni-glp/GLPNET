# Feature Specification: virtual-3270-term

**Feature Branch**: `037-virtual-3270-term`
**Created**: 2026-06-28
**Status**: Draft
**Input**: User description: "virtual-3270-term — A virtual IBM-3270-style block-mode full-screen terminal UX layered over the feature-036 genuine QUIC+WS GLP channel-link, shipped as the --tui mode of the /GLP-Quick Python tool (glp_quick)."

## User Scenarios & Testing *(mandatory)*

A conversation between two people (or a person and an agent/server) takes place over the existing
feature-036 LAN channel-link. Instead of a plain scrolling chat, each side works on a virtual
IBM-3270-style **block-mode** full screen: a large editable "green-screen" PAGES area on top, a status
(OIA) line, and a small command/compose area at the bottom. You edit a whole page or command block
freely and **transmit it as a unit**; the counterpart receives it. The terminal must remain fully
usable when function keys never arrive (Remote Desktop), and must fall back to the plain line console
when there is no full-screen terminal.

### User Story 1 - Hold a conversation over the link using only typing + Enter (Priority: P1)

A user launches the terminal mode of the tool against a peer reachable on the LAN (by IP or machine
name). A startup splash appears, then the full screen. The user types a message in the command area
and transmits it by ending input with a line that is just `//` followed by Enter. The message travels
over the link to the peer; the peer's replies appear above. Every action the user needs — help, theme,
listing pages, creating/switching pages, quitting, sending text — is available as a slash-command that
is run the same way (type it, then `//` + Enter). No function key is ever required.

**Why this priority**: This is the hard requirement and the irreducible MVP. Over Remote Desktop,
function keys (F1–F12, including Shift/Ctrl/Win combinations) do not reach the application, so a
terminal that depends on them is unusable for the primary user. A type-only conversation over the link
is the smallest thing that delivers the feature's value, and everything else layers on top of it.

**Independent Test**: Start two endpoints on a LAN; drive one purely by typing (no function keys);
confirm a message transmitted with `//` + Enter arrives at the peer, the peer's reply renders, and
`/help /theme /pages /new /next /prev /goto N /focus /quit /send <text>` all work via typed entry.
Then disable the TTY (pipe stdin) and confirm the tool falls back to the plain line console.

**Acceptance Scenarios**:

1. **Given** the terminal is running and a peer is connected, **When** the user types text and enters a
   line that is just `//` then Enter, **Then** the whole composed block is transmitted to the peer as
   one message and the command area clears.
2. **Given** the terminal is running, **When** the user types `/theme AMBER` then `//` + Enter, **Then**
   the theme changes to amber without any function key press.
3. **Given** a Remote Desktop session where function keys are intercepted, **When** the user performs
   every documented action by typing only, **Then** all actions succeed and none require a function key
   or a Win+Fx combination.
4. **Given** the process is started without an interactive full-screen terminal (output piped or not a
   TTY), **When** the tool launches, **Then** it runs the plain line console instead of the full-screen
   UI and still sends/receives over the link.
5. **Given** a peer addressed by `@<name> message`, **When** the user transmits, **Then** the message
   is routed to that peer; with no `@` prefix it goes to the default link peer.

---

### User Story 2 - Write, name, and transmit my own pages; receive the counterpart's (Priority: P2)

The user composes content on a large, scrollable, editable page in the screen area, gives pages names,
and transmits a whole page as a block to the counterpart. Each side normally writes its **own** pages
and sends them; received pages appear as the counterpart's pages. The user can create a new page,
switch between pages (next/prev/go-to-N), and list all open pages showing who owns each (me vs. the
conversation partner).

**Why this priority**: The "pages" model is what makes this a 3270-style conversation rather than a
flat chat. It is the main organising structure for everything above the command line and is the
substrate the later joint-edit, mask, and REPL stories extend.

**Independent Test**: Create two named pages, edit and transmit one to the peer, confirm it arrives as
a page owned by the sender; list pages on both ends and confirm names and ownership are shown
correctly; switch pages by next/prev and by go-to-N.

**Acceptance Scenarios**:

1. **Given** the screen area, **When** the user creates a page with a name and types multiple lines
   (Enter inserts a newline, arrow keys move the cursor), **Then** the page holds the edited block and
   can be scrolled.
2. **Given** several open pages, **When** the user lists pages, **Then** each page is shown with its
   name and owner (me / partner) and the current page is indicated.
3. **Given** an edited page, **When** the user transmits it, **Then** the counterpart receives it as a
   page owned by the sender, without overwriting the receiver's own pages.
4. **Given** more than one page, **When** the user issues next / prev / go-to-N, **Then** the active
   page changes accordingly and the status line reflects the new current page.

---

### User Story 3 - 3270 presentation and ergonomics (themes, OIA, layout, key legend) (Priority: P3)

The user can switch colour themes (green screen, amber, white-on-black, paper black-on-white, and a
full colour mode; command lines carry a purple/magenta accent). A 3270-style OIA status line shows the
mode, the current page (X/N + name + owner), link info, and a dynamic PF-key legend. A startup
ASCII-art splash is shown. The command/compose area is configurable: a number of command lines
(default ~3) or a two-strip layout — a scrollable counterpart-response strip above a scrollable user
command strip, about one line each. Where the terminal passes function keys, they act as accelerators
(Fx directly with no modifier; the 3270 convention PF13–PF24 = Shift+F1–F12; Ctrl alternates as
fallbacks), and the dynamic PF-legend is rendered as little reverse-video blocks just above the command
line — each showing its typed-command equivalent.

**Why this priority**: This delivers the authentic 3270 feel and the at-a-glance operability (status,
legend, theme) that make the terminal pleasant and legible. It is valuable on its own but not required
for a working conversation, so it sits below the core stories.

**Independent Test**: Toggle each theme and confirm colours change (command lines show the purple
accent); confirm the OIA line reports mode, current page X/N + name + owner, and link info; resize the
command area via the configuration option and confirm both the multi-line and two-strip layouts render;
confirm the PF-legend shows reverse-video blocks each labelled with its typed equivalent.

**Acceptance Scenarios**:

1. **Given** the terminal, **When** the user toggles the theme, **Then** the colour scheme cycles
   through GREEN, AMBER, WHITE-on-black, PAPER (black-on-white), and full COLOUR, and the command
   lines keep a purple/magenta accent.
2. **Given** any state, **When** the user looks at the OIA line, **Then** it shows the current mode,
   the current page as X/N with its name and owner, link information, and the active PF-key legend.
3. **Given** the layout configuration set to the two-strip option, **When** the terminal renders,
   **Then** a scrollable counterpart-response strip appears above a scrollable user command strip,
   each about one line tall, separated by rules.
4. **Given** a terminal that passes function keys, **When** the user presses a bound Fx (or its
   Shift = PF13–24 / Ctrl alternate), **Then** the corresponding action fires, and the same action is
   also reachable by its typed-command equivalent shown in the legend.

---

### User Story 4 - Joint live edit (pinpoint overwrite) and masks/forms (Priority: P4)

With joint mode toggled on for a shared page, the counterpart can send a **live pinpoint change** — a
block of characters that overwrites a region of the page — while the **original content is saved**, so
the change can be a transient framed/bordered comment or highlight that is later removed, or a
permanent overwrite if that is the intent. Separately, one side can set up a **mask/form** on a page
(fixed labels plus fillable regions) and the other side fills in the values and sends it back.

**Why this priority**: Joint editing and forms turn the terminal from "send whole pages" into a
collaborative surface, which is a meaningful capability but clearly an extension of the page model
rather than part of the MVP.

**Independent Test**: Enable joint mode on a page shared between two endpoints; from the counterpart,
overwrite a region and confirm it appears on the owner's page with the original recoverable; mark one
change transient (framed) and one permanent and confirm transient changes can be reverted to the saved
original while permanent ones persist. Separately, define a mask on one side, fill it on the other, and
confirm the filled form returns to the originator.

**Acceptance Scenarios**:

1. **Given** joint mode is off, **When** the counterpart attempts a pinpoint change, **Then** it is not
   applied to the owner's page (joint edits require joint mode to be enabled).
2. **Given** joint mode is on for a page, **When** the counterpart sends a pinpoint block overwriting a
   region, **Then** that region is replaced on the owner's page and the original content for that region
   is retained.
3. **Given** a transient (framed) pinpoint change, **When** the owner dismisses it, **Then** the saved
   original is restored; **Given** a permanent change, **When** dismissal is attempted, **Then** the
   overwrite remains.
4. **Given** a page configured as a mask/form with fixed labels and fillable regions, **When** the
   other side fills the fillable regions and transmits, **Then** the originator receives the completed
   form with the fixed labels intact.

---

### User Story 5 - Live GLP REPL page and agent-sent fillable pages (Priority: P5)

A command spawns a **new named page bound to a live virtual GLP REPL** running over the link, so the
page can respond to entered goals — tying the terminal back to feature 036's purpose of running GLP
across the channel-link. Pages can also simply be sent without any REPL: an agent or server can push a
page for the user to complete or edit and send back.

**Why this priority**: The REPL-in-a-page is the headline tie-back to running GLP over the link, but it
depends on the page model and the link transport from the earlier stories, so it is sequenced after
them.

**Independent Test**: Issue the command to spawn a REPL page; confirm a new named page is created in
the same terminal, that a GLP goal entered there is evaluated and its result shown on that page, and
that closing it leaves other pages intact. Separately, have an agent/server send a plain (non-REPL)
page; confirm the user can edit and return it.

**Acceptance Scenarios**:

1. **Given** the terminal, **When** the user issues the spawn-REPL command, **Then** a new named page
   is created bound to a live virtual GLP REPL over the link, without disturbing existing pages.
2. **Given** a REPL page, **When** the user enters a GLP goal and transmits, **Then** the goal is
   evaluated over the link and the result is rendered on that page.
3. **Given** an agent/server-sent plain page (no REPL), **When** the user edits it and transmits,
   **Then** the edited page is returned to the sender.

---

### User Story 6 - `/rcopy` file-transfer wizard (client side) (Priority: P6)

The user issues `/rcopy` (or a bindable PFx) and a page-driven wizard opens. It lists the peers
currently reachable and lets the user pick one; submitting opens a transfer conversation with that
peer **only if the peer offers a file service to this user**. The peer's offered **upload roots** are
shown; the user selects one or more, and within a root navigates to an existing folder or creates a new
one as the target. On the local workstation the user selects one or more **file specs/globs**, and for
each defines an **exclusion filter** (drop files by size, filename pattern, subdirectory glob, or other
file attributes). The user chooses **synchronise** (transfer only new or modified files) or **force
overwrite**, and a **fingerprint** option (default on) computes a SHA-256 per file and, when
synchronising, compares it to the target's existing SHA-256 so identical files are skipped. Submit then
performs the transfer over the link and reports per-file outcomes (transferred / skipped-identical /
filtered-out / rejected).

**Why this priority**: File transfer to a peer is a substantial, self-contained capability that builds
on the page/mask machinery of the earlier stories. It is high-value but clearly layered on top of a
working terminal, so it comes after the conversation, pages, and presentation stories.

**Independent Test**: With a peer offering a file service and at least one permitted upload root,
drive the wizard end-to-end: pick the peer, select a root, create a target folder, select a local glob,
add a size/name filter, choose synchronise with fingerprinting on, submit, and confirm only the
non-excluded, new-or-changed files are transferred while byte-identical files are reported as skipped.

**Acceptance Scenarios**:

1. **Given** a peer that does NOT offer this user a file service, **When** the user selects that peer
   and submits, **Then** the wizard reports that no file service is available and no transfer occurs.
2. **Given** a peer offering one or more upload roots, **When** the user selects a root and navigates,
   **Then** the user can enter an existing folder or create a new folder as the transfer target.
3. **Given** selected local file-specs/globs each with an exclusion filter, **When** the user submits,
   **Then** files matching an exclusion (by size / name pattern / subdir glob / attribute) are not
   transferred and are reported as filtered-out.
4. **Given** synchronise mode with fingerprinting on, **When** a selected file already exists on the
   target with an identical SHA-256, **Then** that file is skipped; **Given** force-overwrite mode,
   **Then** the file is transferred regardless.

---

### User Story 7 - User-bindable F-keys with typed equivalents (Priority: P7)

Free (unassigned) PF keys are user-bindable to per-page actions or quick signals to the server (acting
on a page before sending it). The live PF-legend reflects the current bindings, and every binding has a
typed-command equivalent so it remains usable when function keys do not arrive (Remote Desktop).

**Why this priority**: Custom key bindings are a power-user convenience layered on the established
page/command model; the last capability to add.

**Independent Test**: Bind a free F-key to a page action, confirm the legend updates, and confirm the
action fires via both the key (where the terminal passes it) and its typed-command equivalent.

**Acceptance Scenarios**:

1. **Given** a free PF key, **When** the user binds it to a page action or server signal, **Then** the
   live PF-legend reflects the new binding and shows its typed-command equivalent.
2. **Given** a bound F-key, **When** the function key does not reach the application (e.g. Remote
   Desktop), **Then** the same action is still available via its typed-command equivalent.

---

### Edge Cases

- **No peer / link down**: the terminal still starts and is operable locally; transmit attempts report
  that no peer is connected rather than failing silently or crashing.
- **Function key reserved by the host terminal** (e.g. Windows Terminal F11 = fullscreen): the action
  remains reachable via its Ctrl alternate and its typed-command equivalent.
- **Not a TTY / output redirected**: the tool runs the plain line console fallback instead of the
  full-screen UI.
- **`/rcopy` to a peer that offers no file service to this user**: the wizard reports "no file service
  available" and performs no transfer.
- **`/rcopy` selection where every file is excluded by the filter, or the target root's quota would be
  exceeded**: the user is told the per-file outcomes (all filtered-out, or rejected for quota) rather
  than the transfer appearing to do nothing.
- **Joint pinpoint change targets a region beyond the current page bounds**, or arrives for a page the
  receiver has closed: the change is rejected and reported, not applied blindly.
- **Two sides edit overlapping regions of the same joint page**: behaviour is defined and the saved
  original remains recoverable.
- **Spawning a REPL page when one cannot be started**: the failure is reported on a page and the rest
  of the terminal stays usable.
- **LAN addressing by IP or machine name only** (no domain names); an unreachable address is reported
  clearly.

## Clarifications

### Session 2026-06-29

- Q: Peer scope — converse with and own pages across multiple named peers, or strictly 1:1 with one
  link peer? → A: Multiple named peers. A page's owner is a specific peer name; `@<name>` routes a
  message to that peer; the OIA line and page list show peer names. (036 is a genuine ≥4-client mesh.)
- Q: When a peer transmits a page while I am composing, does my view auto-switch to it? → A: No
  auto-focus. The received page appears in the page list with an OIA "new page" indicator; the user
  switches to it via `/next` / `/goto`.
- Q: How are joint-mode edits to overlapping regions of the same page resolved? → A: Last-writer-wins
  per region, with the saved original always recoverable.
- Q: What are the file/glob "send" semantics? → A: Replaced by an `/rcopy` peer-to-peer file-transfer
  wizard. This feature (037) owns the page-driven **client wizard** only: select a peer → select an
  upload root the peer offers → navigate to or create a target folder → select local file-specs/globs
  → define a per-spec exclusion filter → choose synchronise vs force-overwrite → default-on SHA-256
  fingerprint + compare → submit. The **file-service backend** — the `/rcopy init` registry of
  roots / permitted-peers / per-root quota, the `/xfer/in/[peer-name-and-UID]/` landing directory,
  SHA-256 sync comparison on the responder, send/receive provenance recorded in DuckLake, the per-root
  file+directory catalog in a PGlite-backed DuckLake, and a separate file-based WAL journal the catalog
  is recreatable from — is **split into a new sibling feature (040)** and is out of scope here.

## Requirements *(mandatory)*

### Functional Requirements

**Operating modes & fallback**

- **FR-001**: The terminal MUST be shipped as the `--tui` mode of the existing `glp_quick` tool and run
  over the feature-036 QUIC+WS channel-link.
- **FR-002**: The terminal MUST be fully operable using only character input plus Enter — every action
  MUST have a path that requires no function key and no Win+Fx combination (RDP-safe).
- **FR-003**: Transmitting the current block MUST be possible by entering a line that is just `//`
  followed by Enter.
- **FR-004**: The following actions MUST each be available as a typed slash-command run by typing it and
  transmitting: help, theme (cycle or set by name), list pages, new page, next page, previous page,
  go-to page N, toggle focus, quit, and send text. Commands MUST include at least `/help`, `/theme
  [name]`, `/pages`, `/new [name]`, `/next`, `/prev`, `/goto N`, `/focus`, `/quit`, `/send <text>`.
- **FR-005**: When not attached to an interactive full-screen terminal (no TTY), the tool MUST fall
  back to the plain line console while still sending and receiving over the link.
- **FR-006**: The terminal MUST support multiple named peers on a LAN, each addressable by IP address
  or machine name (no domain-name resolution required). A message MUST be directable to a specific peer
  via `@<name>` (defaulting to the current link peer when none is specified), and a page's owner MUST be
  identified by the specific peer's name (not a generic "partner").

**Block mode & editing**

- **FR-007**: The screen MUST present a block-mode editing area where the user edits freely (Enter
  inserts a newline; arrow keys move the cursor) and transmits the whole block as one unit on an
  AID/transmit action.
- **FR-008**: Where the terminal passes them, function keys MUST act as transmit/AID accelerators
  (including F9, and Ctrl-X and Alt-Enter as alternates), in addition to the `//`-line transmit.

**Pages**

- **FR-009**: Pages MUST be named, creatable, and switchable (next, previous, go-to-N), and the user
  MUST be able to list all open pages with each page's owner shown as "me" or the specific peer's name.
- **FR-010**: Each side MUST normally author its own pages and transmit them; a received page MUST
  appear as a page owned by its sending peer and MUST NOT overwrite the receiver's own pages. A
  received page MUST NOT steal focus from the user's current compose/editing context: it MUST appear in
  the page list with an OIA "new page" indicator, and the user switches to it via `/next` / `/goto`.
- **FR-011**: The screen/pages area MUST be scrollable and editable, large relative to the
  command/compose area.

**Joint edit, masks/forms**

- **FR-012**: A page MUST support a joint mode toggle; only when joint mode is enabled MAY the
  counterpart apply live pinpoint changes to that page. When changes target overlapping regions, the
  last write MUST win per region while the saved original of each overwritten region remains
  recoverable.
- **FR-013**: A pinpoint change MUST overwrite a defined region of the page while preserving the
  original content of that region so it can be restored.
- **FR-014**: A pinpoint change MUST be classifiable as transient (a framed/bordered comment or
  highlight that can be dismissed back to the saved original) or permanent (the overwrite persists).
- **FR-015**: A side MUST be able to define a mask/form on a page (fixed labels plus fillable regions);
  the other side MUST be able to fill the fillable regions and return the completed form with the fixed
  labels intact.

**REPL-in-a-page & agent-sent pages**

- **FR-016**: A command MUST spawn a new named page bound to a live virtual GLP REPL that runs over the
  link, where entered GLP goals are evaluated and results rendered on that page, without disturbing
  other open pages.
- **FR-017**: An agent/server MUST be able to send a plain (non-REPL) page for the user to complete or
  edit and send back.

**`/rcopy` file-transfer wizard (client side)**

- **FR-018**: `/rcopy` (and a bindable PFx equivalent) MUST open a page-driven wizard that lists
  reachable peers and lets the user select one; submitting MUST open a transfer conversation with that
  peer only if the peer offers a file service to this user, and MUST report clearly when it does not.
- **FR-027**: The wizard MUST present the upload roots the selected peer offers, let the user select one
  or more, and within a root navigate to an existing folder or create a new folder as the transfer
  target.
- **FR-028**: The wizard MUST let the user select one or more local file specs/globs and, per spec,
  define an exclusion filter that removes files by size, filename pattern, subdirectory glob, or other
  file attributes; filtered-out files MUST be reported as such.
- **FR-029**: The wizard MUST let the user choose a transfer mode of synchronise (transfer only new or
  modified files) or force-overwrite (transfer regardless of the target's current contents).
- **FR-030**: The wizard MUST provide a fingerprint option, default on, that computes a SHA-256 per
  file and, in synchronise mode, compares it against the target's existing SHA-256 so byte-identical
  files are skipped.
- **FR-031**: Submitting a transfer MUST report per-file outcomes (transferred, skipped-identical,
  filtered-out, or rejected). The `/rcopy` file-service backend — the `/rcopy init` registry of roots /
  permitted-peers / per-root quota, the `/xfer/in/[peer-name-and-UID]/` landing directory, responder-
  side SHA-256 comparison, send/receive provenance in DuckLake, the per-root file+directory catalog,
  and its file-based WAL journal — is provided by feature 040 and is OUT OF SCOPE here; this feature is
  the client wizard plus the request/response exchange with that service over the link.

**Bindable keys**

- **FR-019**: Free (unassigned) PF keys MUST be user-bindable to per-page actions or quick server
  signals; the live PF-legend MUST reflect the current bindings and every binding MUST have a
  typed-command equivalent.
- **FR-020**: Function-key activation MUST follow: Fx fires directly with no modifier; the 3270
  convention PF13–PF24 = Shift+F1–F12; Ctrl alternates MUST be provided as fallbacks for keys the host
  terminal reserves.

**Presentation (themes, OIA, layout, splash)**

- **FR-021**: The terminal MUST provide selectable colour themes — at minimum green screen, amber,
  white-on-black, paper (black-on-white), and a full colour mode — toggled by a typed command (and by a
  function key where passed); command lines MUST carry a purple/magenta accent.
- **FR-022**: The terminal MUST display a 3270-style OIA status line showing the mode, the current page
  as X/N with its name and owner, link information, and the dynamic PF-key legend.
- **FR-023**: The command/compose area MUST be configurable: a number of command lines (default
  approximately 3) or a two-strip layout — a scrollable counterpart-response strip above a scrollable
  user command strip, about one line each, separated by rules.
- **FR-024**: The terminal MUST show an ASCII screen-art splash on startup.
- **FR-025**: The dynamic PF-legend MUST be rendered as small reverse-video blocks positioned just above
  the command line, each labelled with its action and showing its typed-command equivalent.

**Continuity with the prototype**

- **FR-026**: The feature MUST extend the existing prototype (`glp_quick/tui.py`, prompt_toolkit) and
  reuse the feature-036 link/adapter seam rather than introducing a parallel transport.

### Key Entities *(include if feature involves data)*

- **Page**: a named, scrollable, editable screen of text with an owner (me, or a specific peer by
  name), a kind (plain, mask/form, or live REPL), an optional joint-mode flag, and, for joint pages,
  saved original content for any overwritten regions.
- **Pinpoint change**: a region (position + extent) plus replacement characters, a transient-vs-permanent
  classification, and the saved original of the overwritten region.
- **Mask/form**: a page composed of fixed labels and fillable regions, plus the values entered into the
  fillable regions.
- **PF-key binding**: a mapping from a free function key (and its Shift/Ctrl/Alt variants) to a page
  action or server signal, with its typed-command equivalent and its legend label.
- **Theme**: a named colour scheme (GREEN, AMBER, WHITE, PAPER, COLOUR) including the command-line
  accent colour.
- **Peer**: one of multiple named LAN endpoints in the 036 mesh, addressable by IP or machine name,
  with a name used for directed messages (`@<name>`) and page ownership.
- **File-service offer**: the set of upload roots (and the folders within them) a peer exposes to this
  user, used by the `/rcopy` wizard; the authoritative definition lives in feature 040's registry.
- **Transfer request** (`/rcopy`): a client-side selection comprising the target peer, one or more
  chosen upload roots + target folders, one or more local file-specs/globs each with an exclusion
  filter, a transfer mode (synchronise / force-overwrite), and a fingerprint option (default on); its
  result is a set of per-file outcomes (transferred / skipped-identical / filtered-out / rejected).
- **Exclusion filter**: a per-file-spec rule set that drops files by size, filename pattern,
  subdirectory glob, or other file attributes before transfer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can complete every documented action (compose, transmit, theme, pages list/new/
  switch/go-to, focus, quit, send) using only typing and Enter — 100% of actions reachable with zero
  function-key presses, verified over a Remote Desktop session.
- **SC-002**: With multiple named endpoints connected on a LAN, a page transmitted from one endpoint
  appears on the other as a page owned by the sending peer (by name), without stealing the receiver's
  focus, and a reply is visible to the first user within a couple of seconds under normal LAN
  conditions.
- **SC-003**: When launched without a TTY, the tool runs the plain line console fallback in 100% of
  cases instead of erroring or producing a broken full-screen display.
- **SC-004**: All five named themes render distinctly and the command lines show the purple/magenta
  accent in every theme; a user can identify the current page (X/N, name, owner), mode, and link state
  from the OIA line without any other action.
- **SC-005**: With joint mode enabled, a counterpart pinpoint overwrite is applied to the owner's page
  and the original content is recoverable in 100% of transient cases; with joint mode disabled, 0% of
  counterpart pinpoint changes are applied.
- **SC-006**: A spawned REPL page evaluates an entered GLP goal over the link and shows its result on
  that page, while all previously open pages remain intact.
- **SC-007**: The `/rcopy` wizard completes an end-to-end transfer to a permitted peer's offered root:
  excluded files are not transferred, in synchronise mode byte-identical files (matching SHA-256) are
  skipped while new/modified files are transferred, force-overwrite transfers regardless, and every
  selected file ends in exactly one reported outcome (transferred / skipped-identical / filtered-out /
  rejected). A peer that offers this user no file service yields a clear "no file service" result and
  zero transfers.
- **SC-008**: A new user reaches a first successful transmitted message — from launch through splash to
  a delivered message — guided only by the on-screen `/help`, without consulting external docs.

## Assumptions

- The feature is built on top of feature 036 (genuine QUIC + WebSocket GLP channel-link), which is
  already implemented; this feature reuses its link/adapter seam and does not change the transport.
- The full-screen UI is built with the prototype's existing technology (prompt_toolkit) and extends
  `glp_quick/tui.py`; behaviour described here is technology-agnostic, but the implementation continues
  the prototype rather than starting a new one.
- Transmitted pages render as page content in the terminal; they are not written to the receiver's
  filesystem. (Writing files to disk is exclusively the job of the `/rcopy` file service in feature
  040, into its `/xfer/in/[peer-name-and-UID]/` landing under a permitted root — never an implicit
  side effect of a page transmit.)
- The `/rcopy` story in this feature is the client wizard only: it discovers a peer's file-service
  offer, drives the selection/filter/mode/fingerprint UX on the terminal's page/mask machinery, and
  exchanges transfer requests/results with the peer over the 036 link. The registry, permission/quota
  enforcement, landing directory, responder-side SHA-256 comparison, provenance recording in DuckLake,
  per-root catalog, and WAL journal are feature 040 and are assumed available for the end-to-end story.
- The live GLP REPL spawned in a page uses the repository's default REPL (the C# GLP REPL per the repo
  convention; Dart on demand) reached over the link.
- The configurable command-line count is controlled by the existing `GLPQUICK_CMDLINES` setting
  (default ~3); the two-strip layout is an alternative arrangement of the same compose area.
- Function-key behaviour over Remote Desktop cannot be relied upon; the typed-command path is the
  authoritative interface and the function keys are accelerators only where the host terminal passes
  them. Win+Fx is never required and never relied upon.
- The terminal operates on a trusted LAN between cooperating endpoints; authentication and access
  control beyond feature 036's existing link security are out of scope for this feature.

## Dependencies

- **Feature 036 (http3-quic-ws-link)**: provides the QUIC+WS channel-link, the link/adapter seam, and
  the `glp_quick` tool this feature extends.
- **Existing prototype** (`glp_quick/tui.py`, `--tui`): the starting point that already provides
  block-mode compose, transmit via `//`+Enter / F9 / Ctrl-X / Alt-Enter, five colour themes, `/help`
  `/theme` `/pages` `/new` `/next` `/prev` `/goto` `/focus` `/quit` `/send`, page list with owners, the
  OIA status line, the startup splash, and the configurable command-line count.
- **GLP REPL** reachable over the link for the REPL-in-a-page story.
- **Feature 040 (`/rcopy` file-transfer service — to be specified)**: the responder-side file service
  the `/rcopy` wizard (US6) talks to — `/rcopy init` registry of roots / permitted-peers / per-root
  quota, the `/xfer/in/[peer-name-and-UID]/` landing directory, responder SHA-256 comparison,
  send/receive provenance in DuckLake, the per-root file+directory catalog (PGlite-backed DuckLake),
  and the catalog's recreatable file-based WAL journal. Split out of this feature on 2026-06-29.

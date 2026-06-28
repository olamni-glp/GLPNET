# Roadmap intake — virtual-3270-term

> **Status**: roadmap intake (captured 2026-06-28 from Gabi). Epic `epic-virtual-3270-term`,
> feature `virtual-3270-term`. A working prototype exists (`glp_quick/tui.py`, `--tui`); these are the
> full requirements to refine via `/bk-roadmap review` → `/bk-specify` → build.

## Concept

A virtual **IBM-3270-style block-mode** full-screen terminal UX over the 036 QUIC+WS link. It is
*conversation-flavoured* (not a plain terminal): a **screen area** of pages above, a **command/response**
area below. You edit your own pages and transmit them (block mode); the counterpart can send pages too.

## Layout (configurable)

- **Configurable number of command lines** (default ~1; option for 3, or 1 command line with a line
  above and a line below).
- Bottom-up: **one command line for the user (me, Gabi)** at the very bottom; **just above, a response
  line for Claude** (command-mode conversation); the **screen/pages area** above that.
- A reasonable layout: a scrollable **server (Claude) response strip** and a scrollable **user command
  strip**, separated by rules — **1 line each may be enough**:
  ```
  ──────────────────  screen / pages (scrollable, editable) ──────────────────
  Claude response line(s) — scrollable
  ─────────────────────────────────────────────────────────────────────────────
  User command line(s) — scrollable
  ─────────────────────────────────────────────────────────────────────────────
  ```

## Colour themes (toggle)

A toggle between modes: **green screen**, **amber screen**, **black-on-white**, **black-and-green**,
**amber-on-black**, **black-and-white**, and a **full colour mode**. (Command lines maybe purple.)

## Keys (block mode)

- **F9 = transmit/send**; **Enter = newline**; **F1 = help** (what I can do).
- **↑/↓** move up/down a line; **←/→** move left/right.
- **F10 = list open pages** + who owns each (the user or the conversation partner).
- **Flexible, user-bindable F-keys** for quick per-page interactions/signals, shown just above the
  command line as little **reverse-video blocks** (PF-key legend) — quick signals to the server to do
  something to/with a page *before* sending the whole page.

## Pages

- Pages are **named**; **F10** shows the list of open pages + owner.
- Normally **each side writes its own pages and sends them**; the counterpart receives them.
- **Joint mode** (toggle): the counterpart can send **live pinpoint changes** to a page — a block of
  chars that **overwrites** a region. With the **original content saved**, this can be a transient
  comment/highlight (a framed/bordered block) OR a **true permanent overwrite** if that's the intent.
- **Masks / forms**: a side can set up **masks**; the other enters values into the mask (form screens).
- A page can be a **live virtual GLP REPL** (so it can respond) — a **command to spawn a new REPL in a
  new named page** in the same terminal. But pages can also just be **sent** (no REPL needed); an agent
  server can send pages for the user to **complete/edit and send back** (no REPL there).

## File / URL / glob send

- A command to point to a **file**, a **URL**, a **directory**, or a **glob** and **send** via an F-key:
  - send a **single file**,
  - send an **entire directory**,
  - send everything matching a **glob** (incl. a "globbed URL").

## Startup

- A nice **screen-art splash** when the terminal starts.

## Status line (OIA)

- 3270-style OIA: mode (block), current page X/N + name + owner, link info, the dynamic PF-key legend.

## Prototype status (what `--tui` already does)

Block-mode compose + **F9** transmit; green-screen transcript; local pages (**F7/F8** switch, **F6** new);
OIA status line; incoming renders cleanly above. **To add next** (prototype): colour-theme toggle, **F1**
help, **F10** page list, configurable command lines, startup art, arrow-key nav polish. **Deeper** (build
via pipeline): joint live-edit/overwrite, masks/forms, REPL-in-a-page, file/URL/glob send, bindable F-keys.

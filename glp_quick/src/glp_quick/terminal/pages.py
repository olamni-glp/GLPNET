"""The page model + page-store operations (feature 040, US2; data-model.md §Page).

A :class:`Page` is a named, scrollable, editable screen of text owned by ``me``, ``shared`` (the CHAT
page), or a specific ``PeerId``. The store operations here are **pure** functions over a list of pages
(host-free, unit-tested); :class:`glp_quick.terminal.state.TerminalState` holds the list + the current
index and delegates the US2 semantics to these functions.

Key invariant (FR-010 / terminal-protocol.md §Invariants #1): a **received** page is owned by its
sending peer, appears as a *distinct* page, MUST NOT overwrite a same-named local page, MUST NOT be
merged into the CHAT page, and MUST NOT steal focus — it only raises the OIA "new page" indicator.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import List, Optional, Tuple

#: Owner sentinels.
ME = "me"           # a page this side authored
SHARED = "shared"   # the CHAT conversation page (neither a single peer nor "me")


@dataclass
class Page:
    """A named, scrollable, editable screen of text (data-model.md §Page).

    ``owner`` is :data:`ME`, :data:`SHARED`, or a ``PeerId``. ``joint`` / ``saved_regions`` are consumed
    by US4; ``unread`` raises the OIA "new page" indicator without stealing focus (FR-010).
    """

    name: str
    owner: str = ME
    kind: str = "plain"  # plain | mask | repl
    text: str = ""
    joint: bool = False
    unread: bool = False
    saved_regions: dict = field(default_factory=dict)


def owner_display(owner: str) -> str:
    """Render an owner for the page list / OIA: ``me`` / ``shared`` / the peer's name (FR-009)."""
    return owner


def find(pages: List[Page], name: str, owner: str) -> Optional[int]:
    """The index of the page with this ``(name, owner)`` pair, or ``None``."""
    for i, pg in enumerate(pages):
        if pg.name == name and pg.owner == owner:
            return i
    return None


def receive_page(pages: List[Page], sender: str, name: str, kind: str, text: str) -> Tuple[int, bool]:
    """Insert or refresh a page received from ``sender`` (FR-010).

    The page is owned by ``sender`` — the feature-036 authenticated identity, **never** the self-declared
    owner in the term (data-model §PeerId). A same-named page from a *different* owner (a local page or
    another peer's) is left untouched — the received page is a separate entry. A re-transmit from the
    *same* sender refreshes that peer's page in place (no duplicate). ``unread`` is set (OIA indicator).

    Returns ``(index, is_new)``.
    """
    i = find(pages, name, sender)
    if i is not None:
        pages[i].kind = kind
        pages[i].text = text
        pages[i].unread = True
        return i, False
    pages.append(Page(name, owner=sender, kind=kind, text=text, unread=True))
    return len(pages) - 1, True


def unread_names(pages: List[Page], current: int) -> List[str]:
    """Names of pages carrying unread content that are not currently in view (the OIA indicator)."""
    return [pg.name for i, pg in enumerate(pages) if pg.unread and i != current]


def list_text(pages: List[Page], current: int) -> str:
    """The ``/pages`` listing: every page with its owner-by-name and the current page marked (FR-009)."""
    rows = []
    for n, pg in enumerate(pages):
        marker = "→" if n == current else " "
        flag = "●" if pg.unread and n != current else " "
        rows.append(f" {marker}{flag} {n + 1:>2}. {pg.name:<16} owner={owner_display(pg.owner)}")
    return (
        "─── OPEN PAGES ───\n\n"
        + "\n".join(rows)
        + "\n\n  (/next /prev /goto N · F7/F8 · /new [name] · /transmit [@peer])\n"
    )

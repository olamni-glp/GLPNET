"""T019 [US2] — the page model + received-page semantics (pages.py + state page delivery).

Covers FR-009 (named/creatable/switchable pages, owner-by-name listing) and FR-010 (a received page is
owned by its sender, does not overwrite a local same-named page, is not merged into CHAT, does not steal
focus, and raises the OIA "new page" indicator).
"""

from __future__ import annotations

from glp_quick.repl_link import GlpMessage
from glp_quick.terminal import pages as P
from glp_quick.terminal import protocol
from glp_quick.terminal.pages import Page, receive_page
from glp_quick.terminal.state import TerminalState


# --- pure page-store functions -----------------------------------------------------------
def test_receive_page_creates_a_peer_owned_page():
    pages = [Page("CHAT", owner="shared")]
    idx, is_new = receive_page(pages, "bob", "STATUS", "plain", "hello")
    assert (idx, is_new) == (1, True)
    assert pages[1].owner == "bob" and pages[1].name == "STATUS"
    assert pages[1].text == "hello" and pages[1].kind == "plain" and pages[1].unread


def test_received_page_does_not_overwrite_a_local_same_named_page():
    pages = [Page("CHAT", owner="shared"), Page("STATUS", owner="me", text="mine")]
    idx, is_new = receive_page(pages, "bob", "STATUS", "plain", "theirs")
    assert (idx, is_new) == (2, True)           # a separate entry
    assert pages[1].owner == "me" and pages[1].text == "mine"   # local page untouched
    assert pages[2].owner == "bob" and pages[2].text == "theirs"


def test_retransmit_from_same_peer_updates_in_place_no_duplicate():
    pages = [Page("CHAT", owner="shared")]
    receive_page(pages, "bob", "S", "plain", "v1")
    idx, is_new = receive_page(pages, "bob", "S", "plain", "v2")
    assert (idx, is_new) == (1, False)
    assert len(pages) == 2 and pages[1].text == "v2"


def test_list_text_shows_owner_by_name_and_current_marker():
    pages = [Page("CHAT", owner="shared"), Page("A", owner="me"), Page("B", owner="bob", unread=True)]
    txt = P.list_text(pages, current=1)
    assert "owner=me" in txt and "owner=bob" in txt and "owner=shared" in txt
    assert "→" in txt  # current page marked
    assert "B" in txt


def test_unread_names_excludes_current_page():
    pages = [Page("CHAT", owner="shared", unread=True), Page("B", owner="bob", unread=True)]
    assert P.unread_names(pages, current=0) == ["B"]
    assert set(P.unread_names(pages, current=1)) == {"CHAT"}


# --- navigation via TerminalState --------------------------------------------------------
def test_create_switch_and_goto():
    st = TerminalState("me", "server")
    a = st.add_page("ALPHA")
    b = st.add_page("BETA")
    assert st.load(a).name == "ALPHA" and st.current == a
    assert st.load(b).name == "BETA"
    assert st.load(0).name == "CHAT"          # goto page 1
    assert st.load(a).owner == "me"


# --- received page over the state receive path -------------------------------------------
def test_received_page_no_focus_steal_not_merged_and_flagged():
    st = TerminalState("me", "server", peers=["bob"])
    mine = st.add_page("MINE")
    st.load(mine)                              # user is editing their own page
    assert st.current == mine

    st.deliver(GlpMessage(sender="bob", to="me", payload=protocol.page("PLAN", "bob", "plain", "the plan")))

    # a distinct peer-owned page now exists
    plan = next(pg for pg in st.pages if pg.name == "PLAN" and pg.owner == "bob")
    assert plan.text == "the plan"
    # focus NOT stolen — still on MINE
    assert st.current == mine and st.current_page().name == "MINE"
    # NOT merged into CHAT
    assert "the plan" not in st.pages[0].text
    # OIA "new page" indicator raised for it
    assert "PLAN" in P.unread_names(st.pages, st.current)
    # switching to it clears the indicator (FR-010: user switches via /next /goto)
    plan_idx = st.pages.index(plan)
    st.load(plan_idx)
    assert not plan.unread


def test_self_declared_owner_in_term_is_ignored_authenticated_sender_wins():
    st = TerminalState("me", "server", peers=["bob"])
    # bob lies, claiming the page is owned by "carol" — the authenticated sender (bob) must win.
    st.deliver(GlpMessage(sender="bob", to="me", payload=protocol.page("X", "carol", "plain", "spoof")))
    owners = {pg.owner for pg in st.pages if pg.name == "X"}
    assert owners == {"bob"}

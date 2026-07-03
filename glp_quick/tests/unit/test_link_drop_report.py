"""T011 [US1] — a link error/drop is surfaced, never swallowed; terminal stays operable (FR-043/044).

R6 transitions: ``up`` → (``recv`` returns ``None``) → ``closed``; ``up`` → (``recv`` raises / FR-019
token) → ``faulted(token)``. In both cases the OIA reflects the state and the terminal remains locally
usable — you can keep editing / switching pages.
"""

from __future__ import annotations

from glp_quick.terminal.state import TerminalState


def test_graceful_close_is_surfaced_and_terminal_stays_operable():
    st = TerminalState("me", "server", peers=["a"])
    assert st.link_state == "up"

    st.deliver(None)  # recv returned None ⇒ graceful close

    assert st.link_state == "closed"
    assert "LINK:closed" in st.oia_link_label()
    assert any("link closed" in ln for ln in st.pages[0].text.splitlines())  # reported, not swallowed
    # still operable: local edits/pages keep working
    assert st.link_operable is True
    idx = st.add_page("SCRATCH")
    st.load(idx)
    assert st.current_page().name == "SCRATCH"


def test_fault_is_surfaced_with_token_and_terminal_stays_operable():
    st = TerminalState("me", "server", peers=["a"])

    st.report_fault("epoch_fenced")  # recv raised / FR-019 fault token

    assert st.link_state == "faulted"
    assert "faulted" in st.oia_link_label() and "epoch_fenced" in st.oia_link_label()
    assert any("faulted" in ln and "epoch_fenced" in ln for ln in st.pages[0].text.splitlines())
    assert st.link_operable is True


def test_idempotent_close_does_not_spam_the_page():
    st = TerminalState("me", "server")
    st.deliver(None)
    st.deliver(None)  # a second close event must not add a second notice
    notices = [ln for ln in st.pages[0].text.splitlines() if "link closed" in ln]
    assert len(notices) == 1

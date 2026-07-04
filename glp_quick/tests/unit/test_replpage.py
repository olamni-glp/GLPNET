"""T034 [US5] — REPL-in-a-page (replpage.py + state repl routing) against a fake REPL bridge (FR-016)."""

from __future__ import annotations

from glp_quick.repl_link import GlpMessage
from glp_quick.terminal import protocol
from glp_quick.terminal.pages import Page
from glp_quick.terminal.replpage import ReplService, append_goal, append_result
from glp_quick.terminal.state import TerminalState


class FakeBridge:
    """A stand-in for :class:`glp_quick.repl_link.ReplBridge` — no subprocess."""

    def __init__(self, start_ok=True, error=None, result="X = 1"):
        self._ok = start_ok
        self.error = error
        self._result = result
        self.started = False
        self.goals = []
        self.stopped = False

    def start(self):
        self.started = True
        return self._ok

    def evaluate(self, goal):
        self.goals.append(goal)
        return self._result

    def stop(self):
        self.stopped = True


def test_replservice_evaluates_via_bridge():
    b = FakeBridge(result="Y = [1,2]")
    svc = ReplService(bridge=b)
    assert svc.evaluate("append([1],[2],Y).") == "Y = [1,2]"
    assert b.started and b.goals == ["append([1],[2],Y)."]


def test_replservice_reports_spawn_failure_without_crashing():
    svc = ReplService(bridge=FakeBridge(start_ok=False, error="no glp_repl built"))
    out = svc.evaluate("true.")
    assert out.startswith("[repl unavailable") and "no glp_repl built" in out


def test_transcript_helpers_append_prompt_and_result():
    pg = Page("REPL", kind="repl", text="")
    append_goal(pg, "member(X,[a,b]).")
    append_result(pg, "X = a")
    assert "?- member(X,[a,b])." in pg.text and "X = a" in pg.text


def test_repl_result_renders_on_its_page_leaving_others_intact():
    st = TerminalState("me", "server", peers=["srv"])
    other = st.add_page("OTHER", text="keep me")
    st.deliver(GlpMessage(sender="srv", to="me", payload=protocol.repl_result("R1", "X = 42")))
    ridx = st.find_page_index("R1")
    assert ridx is not None
    assert "X = 42" in st.pages[ridx].text and st.pages[ridx].kind == "repl"
    assert st.pages[other].text == "keep me"  # other pages undisturbed (FR-016)


def test_repl_goal_without_a_host_is_reported_not_dropped():
    st = TerminalState("me", "server", peers=["c"])
    st.deliver(GlpMessage(sender="c", to="me", payload=protocol.repl_goal("R", "true.")))
    assert any("no REPL is hosted here" in l for l in st.pages[0].text.splitlines())


def test_repl_goal_with_host_hook_is_delegated():
    st = TerminalState("me", "server", peers=["c"])
    seen = []
    st.on_repl_goal = lambda sender, page, goal: seen.append((sender, page, goal))
    st.deliver(GlpMessage(sender="c", to="me", payload=protocol.repl_goal("R", "goal.")))
    assert seen == [("c", "R", "goal.")]

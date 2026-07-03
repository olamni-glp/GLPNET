"""T010 [US1] — receive-path thread-safety (FR-042/SC-012).

Many inbound messages delivered concurrently through :meth:`TerminalState.deliver` must never corrupt
or lose page/peer state. With no event loop bound, ``deliver`` serializes under the state lock (the
production path serializes on the UI event loop via ``call_soon_threadsafe`` — same invariant).
"""

from __future__ import annotations

import threading

from glp_quick.repl_link import GlpMessage
from glp_quick.terminal import protocol as P
from glp_quick.terminal.state import TerminalState


def test_concurrent_deliver_loses_no_chat_lines():
    st = TerminalState("me", "server", peers=["a", "b", "c"])
    n_threads, per_thread = 6, 200
    total = n_threads * per_thread

    def worker(tid: int) -> None:
        for i in range(per_thread):
            st.deliver(GlpMessage(sender=f"t{tid}", to="me", payload=P.chat(f"m{tid}-{i}")))

    threads = [threading.Thread(target=worker, args=(t,)) for t in range(n_threads)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()

    # Every delivered chat produced exactly one line on the CHAT page — none lost, none duplicated.
    lines = [ln for ln in st.pages[0].text.splitlines() if ln.startswith("<<")]
    assert len(lines) == total
    assert len(set(lines)) == total  # no corruption / interleaving damage
    # Structural integrity preserved.
    assert len(st.pages) == 1
    assert st.pages[0].name == "CHAT"
    assert st.current == 0


def test_concurrent_deliver_and_fault_keeps_state_consistent():
    st = TerminalState("me", "server", peers=["a"])

    def chatter() -> None:
        for i in range(100):
            st.deliver(GlpMessage(sender="a", to="me", payload=P.chat(f"c{i}")))

    def faulter() -> None:
        for _ in range(20):
            st.report_fault("epoch_fenced")

    ts = [threading.Thread(target=chatter), threading.Thread(target=faulter)]
    for t in ts:
        t.start()
    for t in ts:
        t.join()

    chat_lines = [ln for ln in st.pages[0].text.splitlines() if ln.startswith("<< a:")]
    assert len(chat_lines) == 100          # no chat lost despite concurrent fault surfacing
    assert st.link_state == "faulted"      # fault recorded
    assert st.link_operable is True        # terminal stays operable (R6)

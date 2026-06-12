"""US4 status-line grammar — bridge-free (T034, FR-019).

Pins the fixed grammar of ``contracts/status-line.md``:

    marathon <run_id> | done=<d>/<n> | open=<k> | budget=<b>[<unit>] | next=<action>

``build_status_line`` is pure from a ``ResumePosition`` — `<n>` is whatever
the position carries (the *current* total).
"""

from __future__ import annotations

import re

from codeconv.marathon.models import ResumePosition
from codeconv.marathon.status import build_status_line

_GRAMMAR = re.compile(
    r"^marathon (?P<run>\S+) \| done=(?P<d>\d+)/(?P<n>\d+) \| "
    r"open=(?P<k>\d+) \| budget=(?P<b>\d+)(?P<unit>\S*) \| next=(?P<action>.+)$"
)


def _pos(**kw) -> ResumePosition:
    base = dict(
        run_id="r", done=0, total=0, next_action="register stages",
        outstanding_issues=0, budget_spent=0, budget_unit=None,
    )
    base.update(kw)
    return ResumePosition(**base)


def test_contract_example_line():
    line = build_status_line(
        _pos(
            run_id="030-marathon-refinement",
            done=2,
            total=7,
            outstanding_issues=1,
            budget_spent=41000,
            budget_unit="tokens",
            next_action="run mini-plan for item-3",
        )
    )
    assert line == (
        "marathon 030-marathon-refinement | done=2/7 | open=1 | "
        "budget=41000tokens | next=run mini-plan for item-3"
    )
    match = _GRAMMAR.match(line)
    assert match is not None
    assert match.group("n") == "7"  # current total, not registration-time


def test_budget_unset_renders_zero_and_grammar_holds():
    line = build_status_line(_pos(done=1, total=4, next_action="run b"))
    assert "budget=0 |" in line
    assert _GRAMMAR.match(line) is not None


def test_parse_contract_split_on_pipes():
    line = build_status_line(
        _pos(run_id="x", done=3, total=9, budget_spent=5, budget_unit="tokens",
             next_action="run verify")
    )
    fields = line.split(" | ")
    assert fields[0] == "marathon x"
    assert [f.split("=")[0] for f in fields[1:]] == [
        "done", "open", "budget", "next",
    ]  # the four fields, always in this order

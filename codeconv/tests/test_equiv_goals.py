"""Goal-bearing tutorial corpus parser (design-comprehensive-equiv-driver.md).

PURE — hermetic synthetic fixtures for BOTH tutorial goal formats (in-fence,
prose-after), load-context source tracking, and the skip cases (load
confirmations, error demos). A final opt-in check runs the real seed against the
sibling tutorial root IF present (skipped in its absence — no CI dependency).
"""

from __future__ import annotations

from pathlib import Path

import pytest

from codeconv.tools.equiv.goals import (
    GOALS_PARTS,
    GoalEntry,
    discover_tutorials,
    load,
    parse_trace_goals,
    to_yaml,
)


# ---- P1: goal in-fence (ch01/02/05/06 form) ------------------------------


_P1 = """# trace
## Phase B — load
```glp
GLP> D:/bstdev/research/glp/GLP/olamni/tutorial/ch02/exercise-01/ch-02-ex-01-glp-append.glp
✓ Loaded: D:/bstdev/research/glp/GLP/olamni/tutorial/ch02/exercise-01/ch-02-ex-01-glp-append.glp
```
## Phase C — primary goal
```glp
GLP> append([1,2,3], [a,b,c], Zs).
Zs = [1, 2, 3, a, b, c]
→ succeeds
```
"""


def test_p1_in_fence_goal() -> None:
    rows = parse_trace_goals(_P1, origin="tutorial:ch02/exercise-01")
    assert len(rows) == 1
    e = rows[0]
    assert e.source == "ch02/exercise-01/ch-02-ex-01-glp-append.glp"
    assert e.goal == "append([1,2,3], [a,b,c], Zs)."
    assert e.expected_status == "succeeds"
    assert e.expected_bindings == (("Zs", "[1, 2, 3, a, b, c]"),)
    assert e.tier == "strict" and e.compare_mode == "trace"


# ---- P2: goal in prose AFTER the result block (ch03/04 form) --------------


_P2 = """# trace
```glp
GLP> ✓ Loaded: olamni/tutorial/ch04/exercise-01/ch-04-ex-01-constants-and-gates.glp
```
### Step 1
```glp
GLP> R = 1
→ succeeds
```
Goal: `and(1, 1, R).` — committed choice picks the first matching clause.
### Step 2
```glp
GLP> X = 1
→ succeeds
```
Goal: `or(1, 0, X).` — matches the second clause.
"""


def test_p2_prose_goal_after_block() -> None:
    rows = parse_trace_goals(_P2, origin="tutorial:ch04/exercise-01")
    assert len(rows) == 2
    assert rows[0].source == "ch04/exercise-01/ch-04-ex-01-constants-and-gates.glp"
    assert rows[0].goal == "and(1, 1, R)."
    assert rows[0].expected_bindings == (("R", "1"),)
    assert rows[1].goal == "or(1, 0, X)."
    assert rows[1].expected_bindings == (("X", "1"),)
    assert all(r.expected_status == "succeeds" for r in rows)


# ---- skip cases: load-rejection demo + load confirmations -----------------


_REJECT_THEN_RUN = """# trace
## Phase A — rejection demo (no goal, no load)
```glp
GLP> olamni/tutorial/ch02/exercise-01/ch-02-ex-01-classical-append-LP-only.glp
Error loading olamni/tutorial/ch02/exercise-01/ch-02-ex-01-classical-append-LP-only.glp: SRSW violations found:
  • append/3: Line 15: Writer variable "X" occurs 2 times
```
## Phase B — load the GLP file
```glp
GLP> ✓ Loaded: olamni/tutorial/ch02/exercise-01/ch-02-ex-01-glp-append.glp
```
## Phase C
```glp
GLP> append([], [a,b,c], Zs).
Zs = [a, b, c]
→ succeeds
```
"""


def test_error_load_demo_is_skipped_and_source_is_the_loaded_file() -> None:
    rows = parse_trace_goals(_REJECT_THEN_RUN, origin="tutorial:ch02/exercise-01")
    # The rejection block yields NO entry (no goal); the one real goal binds to
    # the GLP file that actually loaded — never to the rejected LP-only file.
    assert len(rows) == 1
    assert rows[0].source == "ch02/exercise-01/ch-02-ex-01-glp-append.glp"
    assert rows[0].goal == "append([], [a,b,c], Zs)."


# ---- suspended + multi-binding --------------------------------------------


_SUSPEND = """# trace
```glp
GLP> ✓ Loaded: olamni/tutorial/ch03/exercise-01/ch-03-ex-01-producer-consumer.glp
```
```glp
GLP> Out = [a, b]
Rest = <unbound>
→ suspended
```
Goal: `run(Out, Rest).` — the consumer suspends on the open tail.
"""


def test_suspended_multi_binding() -> None:
    rows = parse_trace_goals(_SUSPEND, origin="tutorial:ch03/exercise-01")
    assert len(rows) == 1
    e = rows[0]
    assert e.goal == "run(Out, Rest)."
    assert e.expected_status == "suspended"
    assert e.expected_bindings == (("Out", "[a, b]"), ("Rest", "<unbound>"))


def test_no_goal_no_source_yields_nothing() -> None:
    assert parse_trace_goals("just prose, no fences\n", origin="x") == []


# ---- goals.yml serde round-trip (incr 1b) ---------------------------------


def test_goals_yaml_round_trip(tmp_path: Path) -> None:
    entries = (
        GoalEntry(
            source="ch02/exercise-01/ch-02-ex-01-glp-append.glp",
            goal="append([1,2,3], [a,b,c], Zs).",
            expected_status="succeeds",
            expected_bindings=(("Zs", "[1, 2, 3, a, b, c]"),),
            origin="tutorial:ch02/exercise-01",
        ),
        GoalEntry(
            source="ch03/exercise-01/ch-03-ex-01-producer-consumer.glp",
            goal="run(Out, Rest).",
            expected_status="suspended",
            expected_bindings=(("Out", "[a, b]"), ("Rest", "<unbound>")),
            origin="tutorial:ch03/exercise-01",
        ),
    )
    p = tmp_path.joinpath(*GOALS_PARTS)
    p.parent.mkdir(parents=True)
    p.write_text(to_yaml(entries), encoding="utf-8")
    assert load(tmp_path) == entries  # bracket/comma binding values survive quoting


# ---- opt-in: real sibling-repo seed (skipped if absent) -------------------


_TUTORIAL_ROOT = Path(__file__).resolve().parents[2] / "olamni" / "tutorial"  # cloned-in


@pytest.mark.skipif(
    not _TUTORIAL_ROOT.is_dir(), reason="sibling GLP tutorial corpus not present"
)
def test_real_seed_extracts_known_goals() -> None:
    rows = discover_tutorials(_TUTORIAL_ROOT)
    assert len(rows) >= 40, len(rows)  # ch01–06 approved, dozens of goals
    sources = {r.source for r in rows}
    # append + merge tutorials must appear with runnable goals.
    assert any("append" in s for s in sources), sources
    assert any("merge" in s or "merger" in s for s in sources), sources
    # every row is well-formed.
    for r in rows:
        assert isinstance(r, GoalEntry)
        assert r.goal.endswith(".")
        assert r.expected_status in {"succeeds", "suspended", "failed"}
        assert r.source.endswith(".glp")
        assert "ch04/exercise-06" not in r.origin  # pending excluded

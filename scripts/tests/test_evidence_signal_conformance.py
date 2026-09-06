# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""Conformance harness for the four mechanisms (feature 108).

Every mechanism below is paired with a NEGATIVE CONTROL that reproduces the defect and asserts
the check FAILS it. FR-018a: a check never shown capable of failing is not evidence that the
property holds -- it is only evidence that a check ran. All eight measured instances would have
passed a harness without controls, which is why the controls are mandatory rather than advised.

These simulators are deliberately in Python and deliberately tiny. They discriminate the
MECHANISM without reverting a shipped fix in another language, so a lane with no C# still has a
falsifiable check for every class.
"""

from __future__ import annotations

import json
import threading
import time

import pytest

ITERATIONS = 40  # FR-018a


# ===========================================================================
# Mechanism 1 -- the early wait (FR-004, FR-005). Measured instance 1.
# ===========================================================================
class _Pump:
    """A work pump. `early=True` reproduces the defect: busy is set AFTER the take, so between
    the take and the assignment the queue is empty and nothing is busy -- and an observer
    sampling there is told the work is done."""

    def __init__(self, early: bool) -> None:
        self._early = early
        self._lock = threading.Lock()
        self._queue: list[str] = []
        self._busy = False
        self._outstanding = 0
        self.result: str | None = None

    def submit(self, item: str) -> None:
        with self._lock:
            self._queue.append(item)
            if not self._early:
                self._outstanding += 1   # counted from ACCEPTANCE, not from commencement

    def is_idle(self) -> bool:
        with self._lock:
            if self._early:
                return not self._queue and not self._busy
            return self._outstanding == 0

    def run_once(self, gap: float = 0.0) -> None:
        with self._lock:
            if not self._queue:
                return
            item = self._queue.pop()
        time.sleep(gap)                  # the window
        with self._lock:
            self._busy = True
        self.result = item.upper()
        with self._lock:
            self._busy = False
            if not self._early:
                self._outstanding -= 1


def _observe_after_submit(early: bool) -> bool:
    """True when the pump reported idle before the work happened."""
    p = _Pump(early=early)
    p.submit("m")
    t = threading.Thread(target=p.run_once, kwargs={"gap": 0.005})
    t.start()
    time.sleep(0.002)                    # sample inside the window
    lied = p.is_idle() and p.result is None
    t.join()
    return lied


def test_wait_reports_idle_only_after_the_work_completed():
    """FR-004/FR-005 over 40 iterations under contention. A single early observation fails."""
    early = [i for i in range(ITERATIONS) if _observe_after_submit(early=False)]
    assert not early, f"reported idle before the work happened on iterations {early}"


def test_early_wait_negative_control_fails():
    """NEGATIVE CONTROL for the test above (FR-018a).

    The defective pump MUST be caught. If this ever passes, the check above discriminates
    nothing and its 40/40 is worthless.
    """
    lied = sum(_observe_after_submit(early=True) for _ in range(ITERATIONS))
    assert lied > 0, (
        "the defective pump was never caught in 40 iterations, so the conformance test above "
        "is unfalsifiable and its green means nothing")


# ===========================================================================
# Mechanism 2 -- did-not-run, refused, and size-as-evidence (FR-007..FR-011).
# Measured instances 3, 4, 6.
# ===========================================================================
RAN_AND_COMPLETE, RAN_AND_EMPTY = "RAN-AND-COMPLETE", "RAN-AND-EMPTY"
DID_NOT_RUN, REFUSED, INDETERMINATE = "DID-NOT-RUN", "REFUSED", "INDETERMINATE"


def classify(exit_code: int, output: str) -> tuple[str, str]:
    """FR-007/FR-008: exit status alone is never sufficient. Positive evidence that the work ran
    is required, and that evidence must be content only the completed work could produce."""
    if "REFUSED" in output:
        return REFUSED, "the output says it refused"
    if "## findings" not in output.lower():
        return DID_NOT_RUN, "no findings section -- the review did not run"
    # BOTH success outcomes are gated on exit_code == 0. The first version returned
    # RAN_AND_EMPTY for `classify(1, "## Findings\nNo findings.")` -- a successful empty run
    # reported for a producer that FAILED, which is an evidence signal reporting success in a
    # failed state: this feature's own class, inside its own harness. Found by adversarial
    # review, 2026-09-06.
    if exit_code != 0:
        return INDETERMINATE, "review-shaped output but the producer exited non-zero"
    if "no findings" in output.lower():
        return RAN_AND_EMPTY, "findings section present and empty"
    return RAN_AND_COMPLETE, "findings section present and populated"


def classify_by_size(exit_code: int, output: str) -> str:
    """The heuristic the fleet adopted after instance 3, kept here ONLY as a control."""
    return RAN_AND_COMPLETE if exit_code == 0 and len(output) > 1024 else DID_NOT_RUN


def test_did_not_run_is_not_success():
    outcome, why = classify(0, "usage: codex [options]\n")
    assert outcome == DID_NOT_RUN and why


def test_refusal_is_not_success():
    """Measured instance 4: buildkit-scheduler reject exited 0 while REFUSING."""
    outcome, why = classify(0, "REFUSED: not the assignee\n")
    assert outcome == REFUSED and "refused" in why


def test_ran_and_empty_is_distinguishable_from_did_not_run():
    assert classify(0, "## Findings\nNo findings.\n")[0] == RAN_AND_EMPTY
    assert classify(0, "")[0] == DID_NOT_RUN


def test_a_failed_producer_is_never_a_successful_empty_run():
    """Review-shaped output plus a non-zero exit is INDETERMINATE, never RAN_AND_EMPTY."""
    assert classify(1, "## Findings\nNo findings.\n")[0] == INDETERMINATE
    assert classify(1, "## Findings\n1. [P1] x\n")[0] == INDETERMINATE


def test_size_is_not_evidence():
    """Measured instance 6: 116 KB, exit 0, zero review -- it read AGENTS.md, obeyed a
    'STOP AND WAIT' reading gate and stopped before opening any code."""
    big_but_empty = "A" * 116_000            # the four mandatory documents, and no review
    assert classify(0, big_but_empty)[0] == DID_NOT_RUN


def test_size_heuristic_negative_control_passes_the_defect():
    """NEGATIVE CONTROL (FR-010/FR-018a): the byte-count heuristic the fleet adopted MUST be
    shown to pass instance 6, or `test_size_is_not_evidence` is proving nothing about it."""
    big_but_empty = "A" * 116_000
    assert classify_by_size(0, big_but_empty) == RAN_AND_COMPLETE, (
        "the size heuristic did not pass the defect, so the content check above is not "
        "demonstrably better than it")


def test_exit_status_alone_never_yields_success():
    """FR-008 stated directly: sweep every exit code against evidence-free output."""
    for code in range(0, 4):
        assert classify(code, "")[0] in (DID_NOT_RUN, REFUSED, INDETERMINATE)


# ===========================================================================
# Mechanism 3 -- durability across a restart (FR-012). Measured instances 5, 7, 8.
# ===========================================================================
class _Spool:
    """An alert spool with a WAL. `clobbering=True` reproduces instance 8: on start the replay
    path re-raises every retained WAL entry unconditionally, overwriting the record already
    there -- so the acknowledgement, which IS durable, is destroyed by the restart."""

    def __init__(self, root, clobbering: bool) -> None:
        self.root = root
        self.clobbering = clobbering
        (root / "wal").mkdir(parents=True, exist_ok=True)
        (root / "alerts").mkdir(parents=True, exist_ok=True)

    def deliver(self, mid: str, arrived: str) -> None:
        (self.root / "wal" / f"{mid}.json").write_text(
            json.dumps({"message_id": mid, "arrived_utc": arrived}), encoding="utf-8")
        self._raise(mid, arrived)

    def _raise(self, mid: str, arrived: str) -> None:
        p = self.root / "alerts" / f"{mid}.json"
        if p.exists() and not self.clobbering:
            return                                    # merge by message_id, never overwrite
        p.write_text(json.dumps(
            {"message_id": mid, "arrived_utc": arrived, "acknowledged": False}), encoding="utf-8")

    def ack(self, mid: str) -> None:
        p = self.root / "alerts" / f"{mid}.json"
        d = json.loads(p.read_text(encoding="utf-8"))
        d["acknowledged"] = True
        p.write_text(json.dumps(d), encoding="utf-8")

    def restart(self, now: str) -> None:
        for w in sorted((self.root / "wal").glob("*.json")):
            self._raise(json.loads(w.read_text(encoding="utf-8"))["message_id"], now)

    def read(self, mid: str) -> dict:
        return json.loads((self.root / "alerts" / f"{mid}.json").read_text(encoding="utf-8"))


def _observe_restart_reobserve(tmp_path, clobbering: bool) -> tuple[dict, dict]:
    s = _Spool(tmp_path, clobbering=clobbering)
    s.deliver("m-1", "14:54:30")
    s.ack("m-1")
    before = s.read("m-1")
    s.restart("14:56:18")
    return before, s.read("m-1")


def test_durability_observe_restart_reobserve(tmp_path):
    """FR-012: the two observations must agree. Completion a restart undoes was not completion."""
    before, after = _observe_restart_reobserve(tmp_path, clobbering=False)
    assert before["acknowledged"] is True
    assert after["acknowledged"] is True, "the restart resurrected an acknowledged alert"
    assert after["arrived_utc"] == before["arrived_utc"], "the restart re-stamped arrived_utc"


def test_durability_negative_control_clobbering_replay_fails(tmp_path):
    """NEGATIVE CONTROL reproducing measured instance 8 exactly, as observed on OLAMNIT against
    build eea87e02: ack true on disk, restart, and the record returns acknowledged=false with
    arrived_utc set to the restart time -- while no new frame was ever delivered."""
    before, after = _observe_restart_reobserve(tmp_path, clobbering=True)
    assert before["acknowledged"] is True, "the ack must be durable before the restart"
    assert after["acknowledged"] is False, "the control failed to reproduce the clobber"
    assert after["arrived_utc"] == "14:56:18", "the control failed to reproduce the re-stamp"


# ===========================================================================
# Mechanism 4 -- two observers of one state must agree (FR-013). Instance 8, second defect.
# ===========================================================================
def _pending_by_records(spool: _Spool, mids) -> int:
    return sum(0 if spool.read(m)["acknowledged"] else 1 for m in mids)


def _pending_by_files(spool: _Spool) -> int:
    """What `doctor` appears to do: count alert FILES. Files are retained deliberately, so a
    file count can never be a pending count."""
    return len(list((spool.root / "alerts").glob("*.json")))


def _pending_by_unacked_files(spool: _Spool) -> int:
    """A conforming second observer: count files whose RECORD says unacknowledged."""
    import glob as _g
    n = 0
    for f in _g.glob(str(spool.root / "alerts" / "*.json")):
        with open(f, encoding="utf-8") as fh:
            if not json.load(fh)["acknowledged"]:
                n += 1
    return n


def test_two_observers_of_one_state_must_agree(tmp_path):
    """The test must actually CONSULT both observers.

    Its first version asserted only `_pending_by_records(...) == 0` and never called a second
    observer at all -- so a test named "two observers must agree" passed without exercising the
    second one, and would have passed in the very defective state its control demonstrates.
    Found by adversarial review, 2026-09-06.
    """
    s = _Spool(tmp_path, clobbering=False)
    s.deliver("m-1", "14:54:30")
    s.ack("m-1")
    first = _pending_by_records(s, ["m-1"])
    second = _pending_by_unacked_files(s)
    assert first == second == 0, f"observers disagree: records={first} files={second}"


def test_two_observer_negative_control_file_count_disagrees(tmp_path):
    """NEGATIVE CONTROL: reproduces the disagreement measured on OLAMNIT 2026-09-06 -- with the
    ack durably true, `alerts` read 0 pending and `doctor` read 1."""
    s = _Spool(tmp_path, clobbering=False)
    s.deliver("m-1", "14:54:30")
    s.ack("m-1")
    assert _pending_by_records(s, ["m-1"]) == 0
    assert _pending_by_files(s) == 1, "the control failed to reproduce the observer disagreement"


# ===========================================================================
# FR-006 -- adoption phasing and the informed-consent override
# ===========================================================================
def refuses(area: str, adoption: dict, overrides: dict, now: float) -> bool:
    """FR-006a/b/c. An unlisted area is an ERROR, never non-adoption. An expired override
    resumes refusing. An override with no expiry is rejected when RECORDED, not when relied on."""
    if area not in adoption:
        raise KeyError(f"area {area!r} is not listed in the adoption manifest (FR-006a)")
    if not adoption[area]:
        return False                                   # declared non-adoption: visible marker
    ov = overrides.get(area)
    return not (ov and ov["expires"] > now)


def record_override(overrides: dict, area: str, **fields) -> None:
    for req in ("briefing", "ack", "rationale", "scope", "expires"):
        if not fields.get(req):
            raise ValueError(f"override for {area!r}: {req} is required (FR-006b)")
    overrides[area] = fields


def test_unlisted_area_is_an_error_not_a_pass():
    with pytest.raises(KeyError, match="not listed"):
        refuses("nobody", {}, {}, now=0.0)


def test_declared_non_adoption_does_not_refuse():
    assert refuses("legacy", {"legacy": False}, {}, now=0.0) is False


def test_override_without_expiry_is_rejected_at_record_time():
    with pytest.raises(ValueError, match="expires"):
        record_override({}, "a", briefing="b", ack="y", rationale="r", scope="s", expires=None)


def test_expired_override_resumes_refusing():
    ov: dict = {}
    record_override(ov, "a", briefing="b", ack="y", rationale="r", scope="s", expires=100.0)
    assert refuses("a", {"a": True}, ov, now=50.0) is False    # live
    assert refuses("a", {"a": True}, ov, now=150.0) is True    # expired -> refuses again

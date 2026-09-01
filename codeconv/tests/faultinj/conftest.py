"""SC-001 coverage reporting for the fault-injection suite (T029b, FR-016).

WHY A SESSION HOOK AND NOT A TEST. A test that asserted "coverage is complete"
would fail the suite for a gap the suite cannot close — seven of the thirteen
instances are owned by buildkit. A test that asserted "coverage is whatever it
is" would assert nothing at all. What SC-001 actually needs is for every run to
*report* its coverage as a receipt, so a reader can never mistake 51/51 green for
13/13 instance coverage again. That is a report, so it is a hook.

The receipt is emitted with ``total_count = 13`` and ``examined_count`` equal to
the number of instances that actually registered, so ``classify`` returns
**UNREAD** for any partial coverage and the terminal summary says so in words.
There is deliberately no way to spell "green" here from this repository alone.
"""

from __future__ import annotations

import os

from .instances import DENOMINATOR, REGISTRY, absorb_receipts, report, sc001_receipt

#: Where a non-Python emitter (the bash harness) leaves receipts this session
#: should absorb. Absent ⇒ nothing to absorb, which is not an error: the bash
#: suite is a separate run and may simply not have executed.
_ABSORB_ROOT_ENV = "CODECONV_RECEIPTS_ROOT"
_ABSORB_RUN_ENV = "CODECONV_RECEIPTS_RUN_ID"


def pytest_terminal_summary(terminalreporter, exitstatus, config):  # noqa: ARG001
    root = os.environ.get(_ABSORB_ROOT_ENV)
    run_id = os.environ.get(_ABSORB_RUN_ENV)
    absorbed: list[int] = []
    if root and run_id:
        try:
            absorbed = absorb_receipts(root, run_id)
        except OSError:
            absorbed = []

    write_root = root or None
    receipt = None
    if write_root and run_id:
        try:
            receipt = sc001_receipt(run_id=run_id, root=write_root, write=True)
        except Exception:  # noqa: BLE001 - reporting must never break the suite
            receipt = None
    if receipt is None:
        # No receipts root configured: still report, still refuse to imply green.
        receipt = sc001_receipt(run_id="in-process", root=".", write=False)

    terminalreporter.write_sep("=", "SC-001 witnessed-instance coverage")
    terminalreporter.write_line(report())
    if absorbed:
        terminalreporter.write_line(f"  absorbed from receipts: {absorbed}")
    terminalreporter.write_line(
        f"  receipt outcome: {receipt.outcome.value} "
        f"({receipt.examined_count}/{DENOMINATOR}) — "
        f"{'SC-001 satisfied' if receipt.outcome.is_successful else 'NOT a pass (FR-016)'}"
    )
    if REGISTRY.unread:
        terminalreporter.write_line(
            "  a green test suite does NOT mean SC-001 is met; the instances above "
            "marked UNREAD have not been injected by anyone."
        )

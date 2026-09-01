"""Instance 6 — a compile-only build gate promotes a behaviourally-wrong artifact.

The witnessed defect (inventory CD-03): the build gate compiled generated code
and reported clean, so a file that compiled but behaved wrongly was promoted. The
gate's verdict was true of the property it checked and false of the property
everyone read it as.

WHAT IS ACTUALLY INJECTED. A gate that examines only the *compile* dimension of
an artifact set is examining a strict subset of what "promotable" means. Under
FR-006 that is not a clean verdict over the artifacts — it is a partial
examination, and partial never presents as whole. The receipt makes the subset
visible in the counts: two dimensions exist per artifact (compiles, behaves) and
a compile-only gate examines one of them.

The negative controls matter as much as the injection: a gate that really does
examine both dimensions must still be able to PASS, and a gate that finds a
behavioural problem must FAIL rather than read as UNREAD. Without them this test
would be satisfied by a gate that can never report success at all.

Registers instance 6 with the SC-001 case-keyed registry (T029b).
"""

from __future__ import annotations

from codeconv.receipts import Outcome, Target, classify

from .instances import register

#: Two artifacts, each of which must satisfy BOTH dimensions to be promotable.
_ARTIFACTS = ("Generated.cs", "GeneratedTwo.cs")
_DIMENSIONS = ("compiles", "behaves")
_TOTAL = len(_ARTIFACTS) * len(_DIMENSIONS)


def _target() -> Target:
    return Target(kind="item-set", identity="generated artifacts pending promotion", resolved=True)


def test_compile_only_gate_is_unread_not_pass():
    # The gate checks `compiles` for both artifacts and nothing else: 2 of 4.
    outcome = classify(_target(), examined_count=len(_ARTIFACTS), total_count=_TOTAL, problems=[])
    assert outcome is Outcome.UNREAD, (
        "a compile-only gate examined half the promotion criteria; reporting that as "
        "clean is exactly how a behaviourally-wrong file gets promoted (instance 6)"
    )
    assert not outcome.is_successful

    register(6, "test_compile_only_gate_is_unread_not_pass: 2/4 dimensions -> UNREAD")


def test_negative_control_a_full_gate_may_pass():
    outcome = classify(_target(), examined_count=_TOTAL, total_count=_TOTAL, problems=[])
    assert outcome is Outcome.PASS
    assert outcome.is_successful


def test_negative_control_a_behavioural_failure_is_fail_not_unread():
    outcome = classify(
        _target(),
        examined_count=_TOTAL,
        total_count=_TOTAL,
        problems=["GeneratedTwo.cs: round-trip mismatch"],
    )
    assert outcome is Outcome.FAIL
    assert not outcome.is_successful

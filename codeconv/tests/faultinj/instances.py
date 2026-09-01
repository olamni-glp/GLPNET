"""The case-keyed registry of the THIRTEEN witnessed instances (SC-001, FR-016).

WHY THIS EXISTS, AND WHY ONE LEVEL UP FROM ``conformance.py``.

``conformance.py`` already gets this right for *outcome* cases: ``_CASES`` declares
the set and each case contributes to ``examined_count`` only by running its own
runner and registering itself, so an anonymous tally can never reach full coverage
while a declared case sits unexercised. That fix came out of the 2026-08-24
adversarial review, which found the anonymous-tally defect **inside** the fixture.

It was never applied at the layer above. Measured 2026-09-01: ``tests/faultinj``
held eleven test modules and named exactly **two** of the thirteen instances —
2 (``test_suppressed_block.py``) and 9 (``test_wrong_dir.py``). The suite was
51/51 green, and that green said nothing whatever about SC-001, which requires
**13 of 13**. Green from an anonymous tally against a denominator of thirteen is
precisely instance 12: *a guard that passes on the failing case*.

THE DENOMINATOR IS THIRTEEN AND IT DOES NOT MOVE (engineer ruling
``Q-GLPNETS14-01``, option A). Seven of the thirteen are defects in **buildkit**
tools that this repository's harness cannot inject at all. They are still
declared here, they are still counted in the denominator, and they read
**UNREAD** with a named owner — never green, never quietly dropped. Under
FR-016 an unexercised declared case must read UNREAD; the honest way to report a
cross-repo gap is to make it loud and attributable, not to shrink the bar until
this repo can clear it.

CONSEQUENCE, DECLARED: ``sc001_receipt()`` CANNOT return PASS from this
repository alone. That is the intended behaviour, not a defect to route around.
It becomes PASS only when every declared instance has registered — which requires
buildkit to file receipts for the seven it owns.

REGISTRATION HAS EXACTLY TWO ROUTES, and both are evidence-bearing:

1. ``register(number, evidence=...)`` — a pytest runner that actually exercised
   the injection calls this. Importing a module does not register it; only
   running the assertion does.
2. ``absorb_receipts(root, run_id)`` — instances exercised by a *non-Python*
   emitter (the bash harness in ``test/receipts/``) register by the receipt they
   left on disk. A receipt is the only accepted proof from outside this process,
   which is the same rule 078 applies to every other consumer.

Task T029b. Implements ``specs/078-verification-receipts/spec.md`` SC-001 and
FR-016, and discharges marathon item ``mdi-019ff6dc-84b0``.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable

from codeconv.receipts import Outcome, Receipt, Target, emit, paths

#: The area under which SC-001 coverage is reported.
SC001_AREA = "test-harness"
#: The check id SC-001 coverage is emitted under.
SC001_CHECK_ID = "faultinj.sc001-instance-coverage"

GLPNET = "glpnet"
BUILDKIT = "buildkit"


@dataclass(frozen=True)
class Instance:
    """One witnessed instance from the spec's inventory table.

    ``owner`` is the repository whose tool must inject it. ``surface`` names
    where the injection lives, so an UNREAD reading points at something a reader
    can go and do rather than at a bare number.
    """

    number: int
    summary: str
    inventory: tuple[str, ...]
    owner: str
    surface: str


#: The thirteen, verbatim from ``spec.md`` §"Witnessed instances this
#: specification must make impossible". Ownership was classified by tool on
#: 2026-09-01 and is recorded here so the split is auditable rather than asserted.
INSTANCES: tuple[Instance, ...] = (
    Instance(1, "a review reported 0 findings because it never ran — a mandatory-reading "
                "gate silently false-zeroed non-interactive passes",
             ("PR-15",), BUILDKIT, "buildkit: bk-codexreview reading gate"),
    Instance(2, "a review tool omitted its findings block in 5/5 passes, yielding "
                "findings_count=0 while it had really found 5–8 P1/P2 items",
             ("PR-16", "AG-04", "RT-35", "RT-45"), GLPNET,
             "glpnet: tests/faultinj/test_suppressed_block.py"),
    Instance(3, "brief / record-output silently no-op on an existing role input, "
                "invalidating an entire adjudication round",
             ("TL-07",), BUILDKIT, "buildkit: bk-3rtask brief / record-output"),
    Instance(4, "a roadmap import refused 954 untagged entities and applied 0 lines while "
                "replay --verify still reported OK",
             ("RS-11",), BUILDKIT, "buildkit: bk-roadmap import / replay"),
    Instance(5, "test skip-guards report an unsupported-platform link as passed-by-skip",
             ("RT-24", "RT-28", "RT-29", "RT-16"), GLPNET,
             "glpnet: test/run_all_tests.sh skip guards (bash emitter)"),
    Instance(6, "a build gate is compile-only, so a behaviourally-wrong generated file "
                "can be promoted",
             ("CD-03",), GLPNET, "glpnet: tests/faultinj/test_compile_only_gate.py"),
    Instance(7, "corpus tools are manual-only, so the unified suite gates corpus scope "
                "by nothing at all",
             ("D8-11", "D8-12", "D8-14"), GLPNET,
             "glpnet: test/run_all_tests.sh corpus scope (bash emitter)"),
    Instance(8, "four separate poll/cursor defects each silently skipped unread mail",
             ("RT-12", "RS-35", "RS-36", "RT-32"), BUILDKIT, "buildkit: COOP poll cursors"),
    Instance(9, "probes run from the wrong directory returned a false clean",
             ("DI-03",), GLPNET, "glpnet: tests/faultinj/test_wrong_dir.py"),
    Instance(10, "a scheduler poll against a retired root reported 0 actors, empty board, "
                 "exit 0",
              ("RT-27",), BUILDKIT, "buildkit: bk-scheduler board/root resolution"),
    Instance(11, "a workflow status surface reported outstanding items: 0 while its own "
                 "gate refused on two unsatisfied checklist blockers",
              (), BUILDKIT, "buildkit: bk-marathon status/discharge"),
    Instance(12, "a preventive guard passed cleanly on the failing case — it checked a "
                 "condition that was already false",
              (), GLPNET, "glpnet: tests/faultinj/test_vacuous_guard.py"),
    Instance(13, "reconcile reported 'roadmap already in sync with pipeline' immediately "
                 "after a new feature entered the pipeline, because the spec directory had "
                 "not slug-matched and was therefore never examined",
              (), BUILDKIT, "buildkit: bk-roadmap reconcile / link"),
)

BY_NUMBER: dict[int, Instance] = {i.number: i for i in INSTANCES}

#: SC-001's denominator. Derived from the declaration, never written as a literal
#: — a hardcoded 13 would go on agreeing with a truncated table.
DENOMINATOR = len(INSTANCES)


class UndeclaredInstance(KeyError):
    """A runner registered a number that is not in the declared inventory.

    Loud on purpose: silently accepting it would let coverage be inflated by a
    typo, which is a tally defect wearing a case-keyed coat.
    """


@dataclass
class Registry:
    """Per-run registration state. Case-keyed: numbers in, never a counter."""

    registered: dict[int, str] = field(default_factory=dict)

    def register(self, number: int, evidence: str) -> None:
        if number not in BY_NUMBER:
            raise UndeclaredInstance(
                f"instance {number} is not one of the {DENOMINATOR} declared instances "
                f"{sorted(BY_NUMBER)} — refusing to count it (SC-001)"
            )
        if not isinstance(evidence, str) or not evidence.strip():
            raise ValueError(
                f"instance {number} registered without evidence; a registration with no "
                f"evidence is a tally increment, which is the defect this registry replaces"
            )
        self.registered[number] = evidence.strip()

    @property
    def examined(self) -> list[int]:
        return sorted(self.registered)

    @property
    def unread(self) -> list[int]:
        return sorted(n for n in BY_NUMBER if n not in self.registered)

    def unread_by_owner(self) -> dict[str, list[int]]:
        out: dict[str, list[int]] = {}
        for n in self.unread:
            out.setdefault(BY_NUMBER[n].owner, []).append(n)
        return out


#: The process-wide registry a pytest session accumulates into.
REGISTRY = Registry()


def register(number: int, evidence: str) -> None:
    """Record that instance *number* was actually exercised, with its evidence."""
    REGISTRY.register(number, evidence)


def reset() -> None:
    """Clear the registry (tests that assert on coverage build their own)."""
    REGISTRY.registered.clear()


def absorb_receipts(root: str | Path, run_id: str, registry: Registry | None = None) -> list[int]:
    """Register instances proven by receipts left on disk by a non-Python emitter.

    A receipt claims its instances in ``examined`` entries of the form
    ``instance:<n>``. Only a receipt whose own outcome is *successful* may
    register anything — a UNREAD or FAIL receipt proves the check ran, not that
    the injection was demonstrated, and counting it would reintroduce the
    pass-by-skip this whole feature closes (instance 5, verbatim).

    Returns the instance numbers newly registered.
    """
    reg = REGISTRY if registry is None else registry
    added: list[int] = []
    for path in paths.run_receipts(root, run_id):
        try:
            data = json.loads(Path(path).read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            # An unreadable receipt registers NOTHING and is not silently
            # equivalent to an absent one — it simply cannot prove anything.
            continue
        try:
            outcome = Outcome(data.get("outcome"))
        except ValueError:
            continue
        if not outcome.is_successful:
            continue
        for entry in data.get("examined", []):
            text = str(entry)
            if not text.startswith("instance:"):
                continue
            try:
                number = int(text.split(":", 1)[1])
            except ValueError:
                continue
            if number in BY_NUMBER and number not in reg.registered:
                reg.register(number, f"receipt {data.get('check_id')} @ {path}")
                added.append(number)
    return sorted(added)


def coverage_lines(registry: Registry | None = None) -> list[str]:
    """The ``examined`` enumeration for the SC-001 receipt — names, never a count."""
    reg = REGISTRY if registry is None else registry
    return [f"instance:{n}" for n in reg.examined]


def sc001_receipt(
    *,
    run_id: str,
    root: str | Path,
    registry: Registry | None = None,
    write: bool = True,
) -> Receipt:
    """Emit the SC-001 coverage receipt over the full declared denominator.

    ``examined_count`` is ``len(registered)`` and ``total_count`` is
    :data:`DENOMINATOR`, so ``receipt.classify`` returns **UNREAD** for any
    partial coverage and PASS only at 13 of 13. The unexercised instances are
    NOT reported as skips: a skip says "deliberately not examined and that is
    fine", and an instance nobody has injected is not fine — it is unread.
    """
    reg = REGISTRY if registry is None else registry
    return emit(
        check_id=SC001_CHECK_ID,
        area=SC001_AREA,
        target=Target(
            kind="item-set",
            identity=f"078 witnessed instances 1..{DENOMINATOR}",
            resolved=True,
        ),
        examined_count=len(reg.examined),
        total_count=DENOMINATOR,
        examined=coverage_lines(reg),
        run_id=run_id,
        root=root,
        write=write,
    )


def report(registry: Registry | None = None) -> str:
    """A human reading of coverage that never renders partial as clean."""
    reg = REGISTRY if registry is None else registry
    lines = [
        f"SC-001 instance coverage: {len(reg.examined)} of {DENOMINATOR} "
        f"({'COMPLETE' if not reg.unread else 'UNREAD — partial coverage is not a pass'})"
    ]
    for number in reg.examined:
        lines.append(f"  [examined] instance {number:>2} — {reg.registered[number]}")
    for owner, numbers in sorted(reg.unread_by_owner().items()):
        lines.append(f"  [UNREAD]   owner={owner}: instances {numbers}")
        for number in numbers:
            lines.append(f"             {number:>2} <- {BY_NUMBER[number].surface}")
    return "\n".join(lines)


def declared(owner: str | None = None) -> tuple[Instance, ...]:
    if owner is None:
        return INSTANCES
    return tuple(i for i in INSTANCES if i.owner == owner)


def numbers(items: Iterable[Instance]) -> list[int]:
    return sorted(i.number for i in items)

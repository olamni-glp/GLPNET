"""Bytecode-emission diff — the early-checkpoint signal (FR-004, SC-007; T015).

PURE, deterministic, unit-tested without a runtime
(``test_no_lm_on_production_path`` guards the import surface).

Contract: ``specs/020-trace-equivalence-fidelity/contracts/equivalence_relation.md``
(§ bytecode-emission diff); research R3 ("bytecode-emission diff first —
bootstrap-window signal; SC-007 spine").

This compares the COMPILER's *emitted* bytecode — what ``out/csharp/`` produces
for a source vs what the Dart compiler produces — NOT the runtime trace. It is
the earliest fidelity signal available during the bootstrap window: once the
compiler subsystem converts, a deterministic source must emit the **same opcodes
at the same logical PCs** before any execution trace exists (SC-007). The
runtime BYTECODE_OP *spine* (relation.py) is the executed-op check; this is the
static emitted-program check.

Operands are compared structurally where present; heap/label-sensitive operand
values are out of scope for the emission diff (the emitted program is pre-
execution, so it carries no heap addresses) — opcode identity at logical PC is
the SC-007 obligation.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Optional, Sequence


@dataclass(frozen=True)
class EmittedOp:
    """One emitted bytecode instruction at a logical program counter."""

    logical_pc: int
    opcode: str
    operands: tuple[Any, ...] = field(default_factory=tuple)


@dataclass(frozen=True)
class BytecodeDiff:
    """Result of the emission diff. ``empty`` ⇔ the deterministic-tier gate
    input ``bytecode_diff_empty`` (data-model.md). ``first_diff_pc`` /
    ``expected`` / ``actual`` localize the first divergence for escalation."""

    empty: bool
    first_diff_pc: Optional[int] = None
    expected: Optional[EmittedOp] = None
    actual: Optional[EmittedOp] = None

    def __bool__(self) -> bool:  # truthy ⇔ there IS a diff
        return not self.empty


_EMPTY = BytecodeDiff(empty=True)


def diff_bytecode(
    dart_emitted: Sequence[EmittedOp],
    csharp_emitted: Sequence[EmittedOp],
) -> BytecodeDiff:
    """Diff Dart-emitted vs C#-emitted bytecode (FR-004).

    Empty diff iff the two programs are the same opcodes (and operands) at the
    same logical PCs — compared positionally, since the logical PC sequence is
    the program order. The first positional divergence (mismatched opcode/
    operands, or a length difference) is reported; a missing op on either side
    is surfaced as ``None`` on that side.
    """
    g, c = list(dart_emitted), list(csharp_emitted)
    for i in range(max(len(g), len(c))):
        go = g[i] if i < len(g) else None
        co = c[i] if i < len(c) else None
        if go is None or co is None or _op_ne(go, co):
            ref = go or co
            assert ref is not None
            return BytecodeDiff(
                empty=False,
                first_diff_pc=ref.logical_pc,
                expected=go,
                actual=co,
            )
    return _EMPTY


def _op_ne(a: EmittedOp, b: EmittedOp) -> bool:
    return (
        a.logical_pc != b.logical_pc
        or a.opcode != b.opcode
        or a.operands != b.operands
    )

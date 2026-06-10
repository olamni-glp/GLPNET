"""ILFRAG-1 — the minimal GLP IL fragment for the MLIR round-trip spike (US4, 027).

ONE GLP clause that touches each of the four GLP/FCP MLIR-dialect primitives
exactly once, so that `decode(encode(p)) == p` is non-trivial yet minimal
(FR-074, research §2). This is a **pure structural data definition** — no MLIR,
no LM, no I/O — imported by harness.py. Keeping the subject (ILFRAG-1) separate
from the encoder makes the round-trip oracle honest: the harness must rebuild
THIS object from parsed MLIR, not merely re-print text.

Subject clause:

    p(X, Y?) :- ground(X?) | q(Y).

mapped onto the four primitives (MLIR-GLP-DIALECT.md, FR-040):
  - HEAD-unify          : head `p(X, Y)`            — tentative head unification
  - GUARD-test          : `ground(X)`               — pure guard test
  - BODY-spawn          : `q(Y)`                     — concurrent body goal
  - suspend-reactivate  : reader `Y`                 — suspends until its writer binds
"""
from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class HeadUnify:
    functor: str
    args: tuple[str, ...]


@dataclass(frozen=True)
class GuardTest:
    test: str
    arg: str


@dataclass(frozen=True)
class BodySpawn:
    goal: str
    args: tuple[str, ...]


@dataclass(frozen=True)
class SuspendReactivate:
    reader: str


@dataclass(frozen=True)
class ILFragment:
    """One GLP clause expressed via the four dialect primitives, in clause order
    (HEAD → GUARD → BODY, with the suspension point the clause carries)."""

    head: HeadUnify
    guard: GuardTest
    body: BodySpawn
    suspend: SuspendReactivate


# The single, frozen subject under round-trip.  p(X, Y?) :- ground(X?) | q(Y).
ILFRAG1 = ILFragment(
    head=HeadUnify(functor="p", args=("X", "Y")),
    guard=GuardTest(test="ground", arg="X"),
    body=BodySpawn(goal="q", args=("Y",)),
    suspend=SuspendReactivate(reader="Y"),
)

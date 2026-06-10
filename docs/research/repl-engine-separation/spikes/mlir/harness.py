"""MLIR/GLP-dialect round-trip validation spike — harness (US4, T019/T020, 027).

Realizes the four GLP/FCP dialect primitives (MLIR-GLP-DIALECT.md, FR-040) as
operations in an (unregistered) `glp` MLIR dialect, using the **real compiled-LLVM
`mlir.ir` Python bindings** (tool-versions.txt). It then exercises the primary,
deterministic metric of FR-041/043:

    decode(encode(p)) == p          # structural identity on ILFRAG-1

Two independent checks make the round-trip honest:
  1. STRUCTURAL: encode(p) → print → re-parse (fresh Context) → decode back into
     the `ILFragment` dataclass; assert the decoded object EQUALS the original p.
  2. TEXTUAL:    str(encode(p)) is idempotent under MLIR's own parse→print.

No language model is anywhere in the verification path (FR-073): the structural
generation here is fixed Python, and the MLIR parser/printer is the deterministic
oracle. Claude's role was restricted to *authoring* this structural encoder — it
does not participate at run time.

Run (WSL2, real bindings):  python harness.py     → exit 0 on PASS, 1 on FAIL.
"""
from __future__ import annotations

import sys

import mlir.ir as ir

from ilfrag1 import (
    ILFRAG1,
    ILFragment,
    HeadUnify,
    GuardTest,
    BodySpawn,
    SuspendReactivate,
)

GLP_DIALECT = "glp"


def _s(ctx: ir.Context, value: str) -> ir.StringAttr:
    return ir.StringAttr.get(value, ctx)


def encode(frag: ILFragment, ctx: ir.Context) -> ir.Module:
    """Build a real MLIR module realizing the four primitives as `glp.*` ops,
    in clause order, inside one `glp.clause` region (structural generation only)."""
    ctx.allow_unregistered_dialects = True
    with ctx, ir.Location.unknown(ctx):
        module = ir.Module.create()
        clause = ir.Operation.create(
            f"{GLP_DIALECT}.clause",
            attributes={
                "functor": _s(ctx, frag.head.functor),
                "arity": _s(ctx, str(len(frag.head.args))),
            },
            regions=1,
        )
        block = clause.regions[0].blocks.append()
        with ir.InsertionPoint(block):
            ir.Operation.create(
                f"{GLP_DIALECT}.head_unify",
                attributes={
                    "functor": _s(ctx, frag.head.functor),
                    "args": _s(ctx, ",".join(frag.head.args)),
                },
            )
            ir.Operation.create(
                f"{GLP_DIALECT}.guard_test",
                attributes={
                    "test": _s(ctx, frag.guard.test),
                    "arg": _s(ctx, frag.guard.arg),
                },
            )
            ir.Operation.create(
                f"{GLP_DIALECT}.body_spawn",
                attributes={
                    "goal": _s(ctx, frag.body.goal),
                    "args": _s(ctx, ",".join(frag.body.args)),
                },
            )
            ir.Operation.create(
                f"{GLP_DIALECT}.suspend_reactivate",
                attributes={"reader": _s(ctx, frag.suspend.reader)},
            )
        ir.InsertionPoint(module.body).insert(clause)
        return module


def _attr(op: ir.Operation, name: str) -> str:
    return ir.StringAttr(op.attributes[name]).value


def _split(value: str) -> tuple[str, ...]:
    return tuple(p for p in value.split(",") if p) if value else ()


def decode(module: ir.Module) -> ILFragment:
    """Invert encode: walk the parsed `glp.clause` region and rebuild the
    `ILFragment`. Raises on any structural surprise (missing op / wrong order /
    wrong name) — a deviation is a FAIL signal, never silently tolerated."""
    top = list(module.body.operations)
    if len(top) != 1 or top[0].operation.name != f"{GLP_DIALECT}.clause":
        raise ValueError(f"expected exactly one {GLP_DIALECT}.clause, got {[o.operation.name for o in top]}")
    clause = top[0].operation
    inner = list(clause.regions[0].blocks[0].operations)
    names = [o.operation.name for o in inner]
    expected = [
        f"{GLP_DIALECT}.head_unify",
        f"{GLP_DIALECT}.guard_test",
        f"{GLP_DIALECT}.body_spawn",
        f"{GLP_DIALECT}.suspend_reactivate",
    ]
    if names != expected:
        raise ValueError(f"primitive ops out of order: {names} != {expected}")
    hu, gt, bs, sr = (o.operation for o in inner)
    return ILFragment(
        head=HeadUnify(functor=_attr(hu, "functor"), args=_split(_attr(hu, "args"))),
        guard=GuardTest(test=_attr(gt, "test"), arg=_attr(gt, "arg")),
        body=BodySpawn(goal=_attr(bs, "goal"), args=_split(_attr(bs, "args"))),
        suspend=SuspendReactivate(reader=_attr(sr, "reader")),
    )


def roundtrip(frag: ILFragment) -> dict:
    """encode → text → re-parse (fresh Context) → decode; report both the
    structural and textual verdicts and the MLIR text for the record."""
    enc_ctx = ir.Context()
    module = encode(frag, enc_ctx)
    text1 = str(module)

    dec_ctx = ir.Context()
    dec_ctx.allow_unregistered_dialects = True
    reparsed = ir.Module.parse(text1, dec_ctx)
    text2 = str(reparsed)

    decoded = decode(reparsed)
    return {
        "structural_eq": decoded == frag,
        "textual_idempotent": text1.strip() == text2.strip(),
        "decoded": decoded,
        "mlir_text": text1,
    }


def main() -> int:
    print(f"MLIR runtime: mlir.ir from {ir.__file__}")
    r = roundtrip(ILFRAG1)
    print("=== encoded MLIR (glp dialect) ===")
    print(r["mlir_text"])
    print(f"structural decode(encode(p)) == p : {r['structural_eq']}")
    print(f"textual str round-trip idempotent : {r['textual_idempotent']}")
    ok = r["structural_eq"] and r["textual_idempotent"]
    print(f"\nRESULT: {'PASS' if ok else 'FAIL'}  (oracle = deterministic; no LM in path)")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())

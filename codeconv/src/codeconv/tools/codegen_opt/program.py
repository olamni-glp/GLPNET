"""DSPy program (signature + module) for Dart→C# code generation.

Implements ``specs/019-codeconv-codegen/contracts/metric_contract.md`` §
"GEPA wiring → program.py". The signature maps the codegen sub-agent's
inputs — the ratified plan, the convspec, the public C# surfaces of
already-generated dependencies, and the relevant idioms — to a single
real C# source unit. GEPA optimizes this signature's *instructions*
(the natural-language field describing the task) to maximize the
composite metric on the held-out eval set.

dspy/litellm are imported HERE (the offline optimizer package). This
module is NEVER imported by ``tools/codegen`` / ``durable/`` / any DBOS
step (R3/R10) — only by ``optimize.py`` / ``eval`` and their tests, and
those imports are lazy from the package ``__init__`` so a bare
``codeconv`` invocation never pays the dspy/litellm import cost.
"""

from __future__ import annotations

from typing import Any

from codeconv.tools.codegen.prompt import BASELINE_INSTRUCTIONS


def _require_dspy() -> Any:
    try:
        import dspy  # noqa: F401

        return dspy
    except ImportError as exc:  # pragma: no cover - env guard
        raise RuntimeError(
            "codegen_opt requires the optimizer extra: "
            "pip install -e 'codeconv[opt]' (dspy/gepa/litellm/openai)."
        ) from exc


def build_signature() -> Any:
    """Build the ``dspy.Signature`` subclass for the codegen task.

    Constructed dynamically so this module imports without dspy present
    (the registry/CLI import path); the class is only materialised when
    the optimizer actually runs.
    """
    dspy = _require_dspy()

    class GenerateCSharp(dspy.Signature):  # type: ignore[misc, valid-type]
        """Convert one Dart source file to a single real, compilable
        C#/.NET 10 source unit, honoring the plan, convspec, dependency
        interfaces, and recorded idioms. Emit ONLY C# — no prose, no
        markdown fences, no leftover Dart. Escalate (do not guess) any
        construct not faithfully derivable."""

        plan: str = dspy.InputField(desc="ratified conversion plan (markdown)")
        convspec: str = dspy.InputField(desc="ratified conversion spec (markdown)")
        dep_interfaces: str = dspy.InputField(
            desc="public C# surfaces of already-generated dependencies"
        )
        idioms: str = dspy.InputField(desc="relevant conversion idioms (KB rows)")
        csharp: str = dspy.OutputField(
            desc="a single real, compilable C# source file (no fences/prose)"
        )

    return GenerateCSharp


def build_program(instructions: str = BASELINE_INSTRUCTIONS) -> Any:
    """Build the codegen ``dspy.Module`` with the given instructions.

    Uses ``dspy.ChainOfThought`` over the signature; the signature's
    instructions are set to ``instructions`` (the baseline or a
    GEPA-optimized set). GEPA mutates this instruction text.
    """
    dspy = _require_dspy()
    sig = build_signature().with_instructions(instructions)

    class CodegenProgram(dspy.Module):  # type: ignore[misc, valid-type]
        def __init__(self) -> None:
            super().__init__()
            self.generate = dspy.ChainOfThought(sig)

        def forward(
            self,
            plan: str,
            convspec: str,
            dep_interfaces: str = "",
            idioms: str = "",
        ) -> Any:
            return self.generate(
                plan=plan,
                convspec=convspec,
                dep_interfaces=dep_interfaces,
                idioms=idioms,
            )

    return CodegenProgram()


def program_instructions(program: Any) -> str:
    """Extract the current instruction text from a built program."""
    sig = getattr(program.generate, "signature", None)
    instr = getattr(sig, "instructions", None) if sig is not None else None
    return instr or BASELINE_INSTRUCTIONS


__all__ = [
    "BASELINE_INSTRUCTIONS",
    "build_program",
    "build_signature",
    "program_instructions",
]

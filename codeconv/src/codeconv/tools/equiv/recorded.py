"""Recorded-trace artifact layout + serde (the capture↔compare seam).

PURE — filesystem reads/writes under ``.codeconv/conversion-equiv/`` only; no
bridge, no LM, no REPL spawn (``test_no_lm_on_production_path`` guards the
import surface).

This is the decoupling point that makes US1's ``compare`` a **standalone
deterministic** verdict-writer (T019) and keeps the durable ``equiv`` step a
pure function of recorded inputs (R12): ``capture`` (T018, the nondeterministic
REPL spawn) writes these artifacts; ``compare`` reads them and applies the pure
``relation.py`` / ``bytecode_diff.py``. Given the same recorded artifacts the
verdict is reproducible.

Layout — one entry per (converted file × in-scope GLP source):

    .codeconv/conversion-equiv/<key>/<source>/
        golden.trace        # Dart golden, canonical wire format (normalize.py)
        candidate.trace     # converted-C# candidate, same wire format
        golden.bc           # Dart-emitted bytecode (bytecode_diff wire format)
        candidate.bc        # C#-emitted bytecode
        meta.json           # subsystem/tier/compare_mode/split + hashes + drift

``<key>`` / ``<source>`` are filesystem-safe encodings of the tombstone key
(the converted file's Dart rel path) and the source path (under ``programs/``).
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Optional

from codeconv.tools.equiv.bytecode_diff import EmittedOp

CONVERSION_EQUIV_PARTS = (".codeconv", "conversion-equiv")

GOLDEN_TRACE = "golden.trace"
CANDIDATE_TRACE = "candidate.trace"
GOLDEN_BC = "golden.bc"
CANDIDATE_BC = "candidate.bc"
META = "meta.json"


def _safe(component: str) -> str:
    """Filesystem-safe, reversible-enough encoding of a path component.

    ``/`` and ``\\`` → ``__``; everything else preserved. Distinct inputs map
    to distinct outputs (no collision across the corpus, where paths differ by
    more than separators)."""
    return component.replace("\\", "/").replace("/", "__")


def entry_dir(repo_root: Path | str, tombstone_key: str, source_path: str) -> Path:
    return (
        Path(repo_root)
        .joinpath(*CONVERSION_EQUIV_PARTS)
        .joinpath(_safe(tombstone_key), _safe(source_path))
    )


def text_hash(text: str) -> str:
    """Stable hash of a recorded wire-format text (the trace identity)."""
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


# --------------------------------------------------------------------------- #
# Emitted-bytecode wire format (FR-004 early checkpoint).                      #
#   one op per non-blank, non-``#`` line:  <logical_pc> <opcode> [operand ...] #
# --------------------------------------------------------------------------- #
def emitted_to_text(ops: list[EmittedOp]) -> str:
    lines = []
    for op in ops:
        parts = [str(op.logical_pc), op.opcode, *(str(o) for o in op.operands)]
        lines.append(" ".join(parts))
    return "\n".join(lines) + ("\n" if lines else "")


def text_to_emitted(text: str) -> list[EmittedOp]:
    ops: list[EmittedOp] = []
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        toks = line.split()
        ops.append(
            EmittedOp(
                logical_pc=int(toks[0]),
                opcode=toks[1],
                operands=tuple(toks[2:]),
            )
        )
    return ops


# --------------------------------------------------------------------------- #
# Recorded entry — the on-disk artifact set for one (key × source).           #
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class RecordedEntry:
    tombstone_key: str
    source_path: str
    golden_trace: Optional[str]
    candidate_trace: Optional[str]
    golden_bc: Optional[str]
    candidate_bc: Optional[str]
    meta: dict

    @property
    def have_traces(self) -> bool:
        """Both sides captured ⇒ ``compare`` can produce a deterministic verdict;
        otherwise it is ``needs_agent_work`` (exit 3), never a crash (R12)."""
        return self.golden_trace is not None and self.candidate_trace is not None

    @property
    def have_bytecode(self) -> bool:
        return self.golden_bc is not None and self.candidate_bc is not None


def _read(path: Path) -> Optional[str]:
    try:
        return path.read_text(encoding="utf-8")
    except OSError:
        return None


def load_entry(
    repo_root: Path | str, tombstone_key: str, source_path: str
) -> RecordedEntry:
    """Read whatever artifacts exist for the entry (missing ⇒ ``None`` fields).

    Never raises on absence — an empty entry is a valid ``needs_agent_work``
    state, not an error (DISCIPLINE §1.7 / R12)."""
    d = entry_dir(repo_root, tombstone_key, source_path)
    meta_text = _read(d / META)
    meta = json.loads(meta_text) if meta_text else {}
    return RecordedEntry(
        tombstone_key=tombstone_key,
        source_path=source_path,
        golden_trace=_read(d / GOLDEN_TRACE),
        candidate_trace=_read(d / CANDIDATE_TRACE),
        golden_bc=_read(d / GOLDEN_BC),
        candidate_bc=_read(d / CANDIDATE_BC),
        meta=meta,
    )


def write_entry(
    repo_root: Path | str,
    tombstone_key: str,
    source_path: str,
    *,
    golden_trace: Optional[str] = None,
    candidate_trace: Optional[str] = None,
    golden_bc: Optional[str] = None,
    candidate_bc: Optional[str] = None,
    meta: Optional[dict] = None,
) -> Path:
    """Write the supplied artifacts (only the non-``None`` ones). Returns the
    entry directory. Used by ``capture`` (T018) after a REPL run."""
    d = entry_dir(repo_root, tombstone_key, source_path)
    d.mkdir(parents=True, exist_ok=True)
    if golden_trace is not None:
        (d / GOLDEN_TRACE).write_text(golden_trace, encoding="utf-8")
    if candidate_trace is not None:
        (d / CANDIDATE_TRACE).write_text(candidate_trace, encoding="utf-8")
    if golden_bc is not None:
        (d / GOLDEN_BC).write_text(golden_bc, encoding="utf-8")
    if candidate_bc is not None:
        (d / CANDIDATE_BC).write_text(candidate_bc, encoding="utf-8")
    if meta is not None:
        (d / META).write_text(
            json.dumps(meta, indent=2, sort_keys=True), encoding="utf-8"
        )
    return d


__all__ = [
    "CONVERSION_EQUIV_PARTS",
    "RecordedEntry",
    "entry_dir",
    "text_hash",
    "emitted_to_text",
    "text_to_emitted",
    "load_entry",
    "write_entry",
]

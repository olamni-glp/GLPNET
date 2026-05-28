"""Equivalence corpus enumeration + tagging (FR-005, FR-006).

PURE — filesystem reads only; no bridge, no LM, no REPL spawn (guarded by
``test_no_lm_on_production_path``). This module owns the *reviewed,
checked-in* equivalence corpus: the set of GLP sources that the differential
oracle runs through both REPLs, each tagged with its comparison mode and its
determinism tier.

Per the design decisions settled before implementation (HANDOFF / Gabi
2026-05-28):

  * **g1 = c** — the corpus is a *reviewed, checked-in enumeration*
    (``.codeconv/equiv-manifest/corpus.yml``), seeded once by parsing the two
    REPL test suites (``test/run_all_tests.sh`` + ``test/run_book_tests.sh``).
    ``load()`` reads that artifact at runtime; the seed parser
    (``discover_from_suites``) is the one-shot bootstrap that produced it.
  * **g2 = a** — a source's ``tier`` is ``strict`` | ``dynamic`` ONLY. The
    authoritative per-(file × source) subsystem in ``codeconv.dart_equivalence``
    is derived from the CONVERTED FILE (``manifest.classify(dart_path)``), not
    from the GLP source. A source is ``dynamic`` iff it is exercised under the
    ``lib/multiagent`` isolate layer (genuine scheduling nondeterminism, spec
    assumption); every other source runs single-computation in one REPL and is
    therefore ``strict`` (deterministic given a fixed goal-queue order).

Comparison mode (FR-005/FR-006):
  * unified (384) + book (141)  → ``trace``  (total-order on strict, partial-
    order on dynamic — that choice is the relation's, T014/T040, not ours);
  * bonds plays                 → ``outcome`` (escrow-timer suspension is a
    valid status; their interleaving is environmental, not a fidelity signal).

Sources are run **in place under ``programs/``** — never copied (single source
of truth). A corpus entry is either a single ``.glp`` file or a *project
directory* (a multi-module play loaded as a unit); ``kind`` distinguishes them.

Contracts: ``contracts/subsystem_curriculum.md`` (tiers), ``data-model.md``
(split), spec FR-005/FR-006.
"""

from __future__ import annotations

import hashlib
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

import yaml

# --- comparison modes -------------------------------------------------------
TRACE = "trace"
OUTCOME = "outcome"

# --- determinism tiers (source-level; g2=a) ---------------------------------
STRICT = "strict"
DYNAMIC = "dynamic"

# --- suites -----------------------------------------------------------------
UNIFIED = "unified"
BOOK = "book"
BONDS = "bonds"

# --- entry kinds ------------------------------------------------------------
FILE = "file"
PROJECT = "project"  # a directory loaded as a multi-module project/play

CORPUS_PARTS = (".codeconv", "equiv-manifest", "corpus.yml")

# Held-out fraction split key (the manifest's recorded scheme, R9).
_HELD_OUT_THRESHOLD = 30  # held-out iff sha256(path)[:8] % 100 < 30


@dataclass(frozen=True)
class CorpusSource:
    """One reviewed corpus entry (a ``.glp`` file or a project directory).

    ``path`` is repo-relative POSIX under ``programs/``; ``kind`` says whether
    it is a single file or a project directory loaded as a unit.
    """

    path: str
    suite: str          # UNIFIED | BOOK | BONDS
    compare_mode: str   # TRACE | OUTCOME
    tier: str           # STRICT | DYNAMIC
    kind: str           # FILE | PROJECT

    @property
    def split(self) -> str:
        """``train`` | ``held-out`` — deterministic from ``path`` (R9)."""
        return split_bucket(self.path)


# --------------------------------------------------------------------------- #
# Deterministic 70/30 split — the manifest's recorded, content-free scheme.   #
# --------------------------------------------------------------------------- #
def split_bucket(source_path: str) -> str:
    """``held-out`` iff ``int(sha256(path)[:8],16) % 100 < 30`` else ``train``.

    Recorded verbatim in ``subsystems.yml`` (R9) so SC-003 never wobbles;
    materialized into ``subsystems.yml:split.assignments`` for auditability but
    always recomputable here so the two agree by construction.
    """
    digest = hashlib.sha256(source_path.encode("utf-8")).hexdigest()[:8]
    return "held-out" if int(digest, 16) % 100 < _HELD_OUT_THRESHOLD else "train"


def split_assignments(sources: Iterable[CorpusSource]) -> dict[str, str]:
    """The full ``{path: train|held-out}`` map (sorted) — what gets materialized
    into ``subsystems.yml`` and validated by ``manifest.validate``."""
    return {s.path: split_bucket(s.path) for s in sorted(sources, key=lambda s: s.path)}


def glp_source_paths(sources: Iterable[CorpusSource]) -> list[str]:
    """The corpus paths (sorted) — the ``glp_sources`` set fed to
    ``manifest.validate``."""
    return sorted(s.path for s in sources)


# --------------------------------------------------------------------------- #
# Load the reviewed, checked-in enumeration.                                  #
# --------------------------------------------------------------------------- #
def load(repo_root: Path | str) -> tuple[CorpusSource, ...]:
    """Parse ``.codeconv/equiv-manifest/corpus.yml`` into ``CorpusSource`` rows.

    Raises on a malformed/missing artifact (a missing corpus is mis-authoring,
    not a tolerated state — the seed step must have run and been reviewed)."""
    path = Path(repo_root).joinpath(*CORPUS_PARTS)
    raw = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(raw, dict):
        raise ValueError(f"corpus {path}: top level must be a mapping")
    rows = raw.get("sources") or []
    out: list[CorpusSource] = []
    for r in rows:
        out.append(
            CorpusSource(
                path=str(r["path"]),
                suite=str(r["suite"]),
                compare_mode=str(r["compare_mode"]),
                tier=str(r["tier"]),
                kind=str(r.get("kind", FILE)),
            )
        )
    return tuple(out)


# --------------------------------------------------------------------------- #
# One-shot seed: parse the two REPL test suites into the corpus enumeration.   #
# (Bootstrap for g1=c — the OUTPUT is the reviewed checked-in corpus.yml.)     #
# --------------------------------------------------------------------------- #

# Shell-variable expansions parsed from test/run_all_tests.sh. TC_DIR / MODED
# (glp_runtime/test/programs/**) are deliberately omitted: they live OUTSIDE
# programs/ (FR-006 single-source-of-truth) and are load-only type-check /
# negative fixtures — they produce no runtime trace.
_VAR_ROOTS = {
    "TYPED": "programs/tests/typed",
    "BOOK": "programs/typed_book",
}

# Project directories the unified suite loads in a SINGLE REPL computation
# (deterministic ⇒ strict, trace-compared).
_UNIFIED_PROJECT_ROOTS = (
    "programs/social_graph_simulated_ui_modules",  # Section G
    "programs/cssn_modules",                        # Section H
    "programs/cssg_modules",                        # Section F (cssg_modules_test.sh)
    "programs/tests/dynamic_dispatch",              # Section L (M # goal dispatch)
)

# Project directories whose v2 module sets are exercised under the
# lib/multiagent isolate layer (one isolate per agent, Sections M/O) ⇒ DYNAMIC,
# still trace-compared (causal/partial-order — the relation decides, not us).
_DYNAMIC_PROJECT_ROOTS = (
    "programs/cssg_modules_v2",                      # Section J + madGLP
    "programs/cssn_modules_v2",                      # Section K + madGLP (Section M)
)

# Bonds plays: OUTCOME-only (FR-005), run multi-isolate (Sections N/O) ⇒ DYNAMIC.
_BONDS_PROJECT_ROOTS = (
    "programs/bonds_v2",                             # Section N + O isolate
)

# run_book_tests.sh book corpus. Its heredoc carries stale sibling-repo absolute
# paths (/home/user/GLP/<rel>); we re-root <rel> onto the glpnet tree.
_BOOK_REL_PREFIX = "programs/archive/book/"

_FILE_REF = re.compile(r"\$\{?(TYPED|BOOK)\}?/([A-Za-z0-9_./-]+\.glp)")
_REJECT_BLOCK = re.compile(r"(?:NEGATIVE_FILES|SRSW_FILES)=\((.*?)\)", re.S)
_BOOK_REF = re.compile(r"/home/user/GLP/(programs/archive/book/[A-Za-z0-9_./-]+\.glp)")


def _expand(var: str, tail: str) -> str:
    return f"{_VAR_ROOTS[var]}/{tail}"


def _unified_file_refs(script_text: str) -> set[str]:
    return {_expand(v, t) for v, t in _FILE_REF.findall(script_text)}


def _reject_refs(script_text: str) -> set[str]:
    """Files inside NEGATIVE_FILES / SRSW_FILES — load-rejection tests (never
    load) — subtracted from the behavioural corpus."""
    refs: set[str] = set()
    for block in _REJECT_BLOCK.findall(script_text):
        refs.update(_expand(v, t) for v, t in _FILE_REF.findall(block))
    return refs


def _book_refs(book_script_text: str) -> set[str]:
    return set(_BOOK_REF.findall(book_script_text))


def _project_glp(repo_root: Path, root_rel: str) -> bool:
    return (repo_root / root_rel).is_dir()


def discover_from_suites(repo_root: Path | str) -> list[CorpusSource]:
    """Seed the corpus by parsing the two REPL suites. Deterministic, sorted.

    Only entries that EXIST under ``repo_root`` are kept (the suites carry some
    sibling-repo paths). The OUTPUT is reviewed and written to ``corpus.yml`` —
    this function is the bootstrap, not the runtime read path (see ``load``).
    """
    repo_root = Path(repo_root)
    unified_sh = (repo_root / "test" / "run_all_tests.sh").read_text(encoding="utf-8")
    book_sh = (repo_root / "test" / "run_book_tests.sh").read_text(encoding="utf-8")

    by_path: dict[str, CorpusSource] = {}

    def add(src: CorpusSource) -> None:
        # First writer wins; roots are disjoint so this only guards re-adds.
        by_path.setdefault(src.path, src)

    # Unified per-file runtime sources (minus load-rejection fixtures).
    unified_files = _unified_file_refs(unified_sh) - _reject_refs(unified_sh)
    for rel in unified_files:
        if (repo_root / rel).is_file():
            add(CorpusSource(rel, UNIFIED, TRACE, STRICT, FILE))

    # Unified project-directory plays (single computation ⇒ strict).
    for root in _UNIFIED_PROJECT_ROOTS:
        if _project_glp(repo_root, root):
            add(CorpusSource(root, UNIFIED, TRACE, STRICT, PROJECT))

    # Dynamic (multiagent isolate) project plays — trace-compared.
    for root in _DYNAMIC_PROJECT_ROOTS:
        if _project_glp(repo_root, root):
            add(CorpusSource(root, UNIFIED, TRACE, DYNAMIC, PROJECT))

    # Bonds plays — outcome-only, dynamic.
    for root in _BONDS_PROJECT_ROOTS:
        if _project_glp(repo_root, root):
            add(CorpusSource(root, BONDS, OUTCOME, DYNAMIC, PROJECT))

    # Book compile/run corpus (single files, strict, trace).
    for rel in _book_refs(book_sh):
        if (repo_root / rel).is_file():
            add(CorpusSource(rel, BOOK, TRACE, STRICT, FILE))

    return sorted(by_path.values(), key=lambda s: s.path)


def to_yaml(sources: list[CorpusSource]) -> str:
    """Render the reviewed corpus artifact (with a provenance header)."""
    header = (
        "# Equivalence corpus enumeration — feature 020 (codeconv-equiv).\n"
        "#\n"
        "# Reviewed, checked-in (g1=c). Seeded once by\n"
        "#   tools/equiv/corpus.py:discover_from_suites parsing\n"
        "#   test/run_all_tests.sh + test/run_book_tests.sh.\n"
        "# Each source is run IN PLACE under programs/ (no copy; FR-006).\n"
        "#\n"
        "#   suite        : unified (384) | book (141) | bonds\n"
        "#   compare_mode : trace (unified+book) | outcome (bonds, FR-005)\n"
        "#   tier         : strict | dynamic  (source-level only, g2=a — the\n"
        "#                  authoritative per-row subsystem is derived from the\n"
        "#                  converted FILE via manifest.classify, not the source)\n"
        "#   kind         : file | project (a multi-module play loaded as a unit)\n"
        "#\n"
        "# Regenerate (then review the diff) with:\n"
        "#   python -c \"from codeconv.tools.equiv import corpus; "
        "corpus.write_artifacts('.')\"\n"
        "version: 1\n"
        "sources:\n"
    )
    body = []
    for s in sources:
        body.append(
            f"  - {{ path: {s.path}, suite: {s.suite}, "
            f"compare_mode: {s.compare_mode}, tier: {s.tier}, kind: {s.kind} }}\n"
        )
    return header + "".join(body)


def assignments_block(sources: list[CorpusSource], indent: str = "    ") -> str:
    """The ``subsystems.yml:split.assignments`` materialization (sorted)."""
    lines = []
    for path, bucket in split_assignments(sources).items():
        lines.append(f'{indent}"{path}": {bucket}\n')
    return "".join(lines)


def write_artifacts(repo_root: Path | str) -> tuple[int, dict[str, int]]:
    """Seed → write ``corpus.yml`` and materialize ``subsystems.yml``
    assignments. Returns ``(total, breakdown)`` for review. One-shot,
    re-runnable; the diff is what gets reviewed."""
    repo_root = Path(repo_root)
    sources = discover_from_suites(repo_root)

    corpus_path = repo_root.joinpath(*CORPUS_PARTS)
    corpus_path.parent.mkdir(parents=True, exist_ok=True)
    corpus_path.write_text(to_yaml(sources), encoding="utf-8")

    # Materialize the split into subsystems.yml (replace the `assignments: {}`
    # placeholder, preserving the curated comments around it).
    subsys_path = repo_root / ".codeconv" / "equiv-manifest" / "subsystems.yml"
    text = subsys_path.read_text(encoding="utf-8")
    block = "  assignments:\n" + assignments_block(sources)
    if "  assignments: {}\n" in text:
        text = text.replace("  assignments: {}\n", block)
        subsys_path.write_text(text, encoding="utf-8")

    breakdown: dict[str, int] = {}
    for s in sources:
        key = f"{s.suite}/{s.tier}/{s.compare_mode}"
        breakdown[key] = breakdown.get(key, 0) + 1
    return len(sources), breakdown


__all__ = [
    "CorpusSource",
    "TRACE",
    "OUTCOME",
    "STRICT",
    "DYNAMIC",
    "UNIFIED",
    "BOOK",
    "BONDS",
    "FILE",
    "PROJECT",
    "load",
    "split_bucket",
    "split_assignments",
    "glp_source_paths",
    "discover_from_suites",
    "to_yaml",
    "write_artifacts",
]

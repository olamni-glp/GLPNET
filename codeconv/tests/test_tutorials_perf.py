"""Opt-in performance bound — SC-005 (T033).

Marked ``perf`` → skipped unless ``--run-perf`` (conftest). Bounds the
full-catalog walk at <3 s. Uses the real vendored corpus when present; falls
back to the fixture so the assertion is always exercisable.
"""

from __future__ import annotations

import time
from pathlib import Path

import pytest

from codeconv.tutorials import corpus as C

REPO_ROOT = Path(__file__).resolve().parents[2]
FIXTURE = Path(__file__).resolve().parent / "fixtures" / "tutorials_corpus"


@pytest.mark.perf
def test_full_catalog_under_3s() -> None:
    real = C.default_corpus_root(REPO_ROOT)
    root = real if real.is_dir() else FIXTURE

    start = time.perf_counter()
    corpus = C.load_corpus(root, repo_root=REPO_ROOT)
    elapsed = time.perf_counter() - start

    assert corpus.chapters or corpus.warnings is not None  # produced a listing
    assert elapsed < 3.0, f"full-catalog walk took {elapsed:.2f}s (SC-005 <3s)"

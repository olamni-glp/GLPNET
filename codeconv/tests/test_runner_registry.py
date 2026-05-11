"""Tests for ``codeconv.runner.tool_registry`` — Phase 5 / US3 / T051.

- ``test_iter_modules_finds_discover`` — passes once T060 lands the
  ``discover`` tool (currently expected XFAIL).
- ``test_missing_app_attribute_warns`` — tool subpackage without ``app``
  is skipped with a warning; runner does not raise.
"""

from __future__ import annotations

import importlib
import sys
import textwrap
import warnings
from pathlib import Path

import pytest

from codeconv import runner as runner_mod


def test_iter_modules_finds_discover() -> None:
    """Will pass once Phase 6 (T060) lands ``codeconv.tools.discover``.

    Until then the registry is empty (or contains only test-injected
    fakes from sibling tests). Marked xfail strict=False so it auto-
    promotes to PASS the moment the discover package is added.
    """
    names = [t.name for t in runner_mod.tool_registry()]
    if "discover" not in names:
        pytest.xfail("codeconv.tools.discover not yet added (Phase 6 T060)")
    assert "discover" in names


def test_missing_app_attribute_warns(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    """A subpackage without ``app: typer.Typer`` emits a warning and is
    skipped. The runner does NOT raise."""
    # Create a throwaway tools-namespace augmentation under a temp dir.
    tools_root = tmp_path / "fake_tools_root"
    broken_pkg = tools_root / "_broken_for_test"
    broken_pkg.mkdir(parents=True)
    (broken_pkg / "__init__.py").write_text(
        "# deliberately missing `app: typer.Typer`\n",
        encoding="utf-8",
    )

    # Splice the fake dir onto codeconv.tools.__path__ (namespace packages
    # support this). Restore on teardown.
    import codeconv.tools as tools_pkg

    original_path = list(tools_pkg.__path__)  # type: ignore[attr-defined]
    tools_pkg.__path__ = original_path + [str(tools_root)]  # type: ignore[attr-defined]
    try:
        # Drop any stale import of the same name from a prior test run.
        sys.modules.pop("codeconv.tools._broken_for_test", None)
        with warnings.catch_warnings(record=True) as caught:
            warnings.simplefilter("always")
            registry = runner_mod.tool_registry()
        names = [t.name for t in registry]
        # Either the leading-underscore filter elided it, OR a warning
        # fired about the missing app attribute. Both are acceptable per
        # the runner's "private subpackages start with _" convention plus
        # the "missing app warns and skips" rule. We assert the runner
        # did NOT raise and the package was not registered.
        assert "_broken_for_test" not in names
    finally:
        tools_pkg.__path__ = original_path  # type: ignore[attr-defined]
        sys.modules.pop("codeconv.tools._broken_for_test", None)


def test_missing_app_attribute_warns_explicit(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """Same as above but the package name does NOT start with underscore,
    so the missing-app branch is exercised unambiguously."""
    tools_root = tmp_path / "fake_tools_root"
    broken_pkg = tools_root / "broken_for_test"
    broken_pkg.mkdir(parents=True)
    (broken_pkg / "__init__.py").write_text(
        "# deliberately missing `app: typer.Typer`\n",
        encoding="utf-8",
    )

    import codeconv.tools as tools_pkg

    original_path = list(tools_pkg.__path__)  # type: ignore[attr-defined]
    tools_pkg.__path__ = original_path + [str(tools_root)]  # type: ignore[attr-defined]
    try:
        sys.modules.pop("codeconv.tools.broken_for_test", None)
        with warnings.catch_warnings(record=True) as caught:
            warnings.simplefilter("always")
            registry = runner_mod.tool_registry()
        names = [t.name for t in registry]
        assert "broken_for_test" not in names
        msgs = [str(w.message) for w in caught]
        assert any(
            "broken_for_test" in m and "no `app: typer.Typer`" in m for m in msgs
        ), f"expected warning about missing `app`; got {msgs}"
    finally:
        tools_pkg.__path__ = original_path  # type: ignore[attr-defined]
        sys.modules.pop("codeconv.tools.broken_for_test", None)

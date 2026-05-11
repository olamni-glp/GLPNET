"""Tests for ``codeconv.tools.discover.pubspec.read_package_name``
— Feature 014 / FR-004 / FR-005.

Maps to `contracts/workflow_contract.md` § "test_pubspec.py (NEW)".
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools.discover.pubspec import read_package_name


def _write(p: Path, text: str) -> None:
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding="utf-8")


def test_happy_path(tmp_path: Path) -> None:
    sub = tmp_path / "sub"
    _write(
        sub / "pubspec.yaml",
        "name: glp_runtime\nversion: 1.0.0\n",
    )
    name, warning = read_package_name(sub)
    assert name == "glp_runtime"
    assert warning is None


def test_pubspec_absent(tmp_path: Path) -> None:
    sub = tmp_path / "sub"
    sub.mkdir()
    name, warning = read_package_name(sub)
    assert name is None
    assert warning is not None
    assert warning["kind"] == "pubspec_missing"
    assert warning["reason"] == "absent"
    assert warning["path"].endswith("pubspec.yaml")


def test_pubspec_unparseable(tmp_path: Path) -> None:
    sub = tmp_path / "sub"
    _write(
        sub / "pubspec.yaml",
        # Tab-indented mapping inside a flow scalar — yaml.YAMLError.
        "name: glp_runtime\n: [unbalanced\n",
    )
    name, warning = read_package_name(sub)
    assert name is None
    assert warning is not None
    assert warning["kind"] == "pubspec_missing"
    assert warning["reason"] == "unparseable"


def test_pubspec_no_name_field(tmp_path: Path) -> None:
    sub = tmp_path / "sub"
    _write(
        sub / "pubspec.yaml",
        "version: 1.0.0\ndescription: missing name field\n",
    )
    name, warning = read_package_name(sub)
    assert name is None
    assert warning is not None
    assert warning["kind"] == "pubspec_missing"
    assert warning["reason"] == "no_name_field"


def test_pubspec_name_empty_string(tmp_path: Path) -> None:
    sub = tmp_path / "sub"
    _write(
        sub / "pubspec.yaml",
        'name: ""\nversion: 1.0.0\n',
    )
    name, warning = read_package_name(sub)
    assert name is None
    assert warning is not None
    assert warning["reason"] == "no_name_field"


def test_pubspec_name_non_string(tmp_path: Path) -> None:
    sub = tmp_path / "sub"
    _write(
        sub / "pubspec.yaml",
        "name: 42\nversion: 1.0.0\n",
    )
    name, warning = read_package_name(sub)
    assert name is None
    assert warning is not None
    assert warning["reason"] == "no_name_field"


def test_warning_path_is_posix_relative_when_repo_root_supplied(
    tmp_path: Path,
) -> None:
    repo_root = tmp_path
    sub = repo_root / "glp_runtime_net"
    sub.mkdir()
    name, warning = read_package_name(sub, repo_root=repo_root)
    assert name is None
    assert warning is not None
    # POSIX-relative against repo_root.
    assert warning["path"] == "glp_runtime_net/pubspec.yaml"

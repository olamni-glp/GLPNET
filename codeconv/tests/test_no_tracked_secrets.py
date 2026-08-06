"""Regression guard: no private-key or credential material may be TRACKED in git.

Root cause this guard exists for (2026-08-06, feature 069):
    `glpquick-cert/glpquick.key` (plaintext PKCS8), `.pem`, `.pfx` and `.fingerprint`
    were committed on 2026-07-09 (94fbe87d, release v2026.07.09.1). The
    `.gitignore` rule `glpquick-cert/` was added LATER (b5ac6e8e, chore(049)),
    and a .gitignore rule NEVER applies to an already-tracked path. The rule was
    therefore inert from birth, `git check-ignore` matched nothing, and feature
    067's T002 ("verify glpquick-cert/ is gitignored") was unpassable by
    construction while the files stayed in the index.

Why a test and not only a .gitignore rule:
    .gitignore is advisory for untracked paths only. It cannot express "this must
    never become tracked". A pre-commit hook can be bypassed with --no-verify.
    This test runs in the normal suite, so re-introduction fails visibly and is
    attributable, which is what "durable" has to mean here.

Scope note: this asserts nothing about git HISTORY. Material already pushed to a
remote must be treated as compromised and ROTATED; untracking stops the bleeding
but does not un-publish. See specs/069-tracked-key-remediation/ for the rotation
decision record.
"""

from __future__ import annotations

import subprocess
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]

# Filename suffixes that carry private key / credential material.
FORBIDDEN_SUFFIXES = (
    ".key",
    ".pfx",
    ".p12",
    ".pem",
    ".jks",
    ".keystore",
)

# Paths that are allowed to carry a forbidden suffix because they are provably
# NOT secret material. Keep this list short, and justify every entry inline.
ALLOWLIST: frozenset[str] = frozenset()


def _tracked_files() -> list[str]:
    proc = subprocess.run(
        ["git", "ls-files", "-z"],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=True,
    )
    return [p for p in proc.stdout.split("\0") if p]


def test_no_private_key_material_is_tracked() -> None:
    """Fail loudly if any credential-bearing file is in the git index."""
    offenders = sorted(
        path
        for path in _tracked_files()
        if path.lower().endswith(FORBIDDEN_SUFFIXES) and path not in ALLOWLIST
    )
    assert not offenders, (
        "Private-key / credential material is TRACKED in git:\n  "
        + "\n  ".join(offenders)
        + "\n\nA .gitignore rule does NOT cover an already-tracked path. Untrack it:\n"
        "  git rm --cached <path>\n"
        "and confirm the ignore rule then bites:\n"
        "  git check-ignore -v <path>\n"
        "If the material was ever pushed, it must also be ROTATED — untracking "
        "does not un-publish it."
    )


def test_gitignore_rule_for_cert_dir_is_effective() -> None:
    """The glpquick-cert/ rule must actually match, not merely exist.

    This is the assertion 067's T002 asks for. It is written to fail if the
    files are re-added to the index, because `git check-ignore` reports no match
    for a tracked path — the exact silent failure that hid this for a month.
    """
    cert_dir = REPO_ROOT / "glpquick-cert"
    if not cert_dir.is_dir():
        pytest.skip("glpquick-cert/ not present on this host")

    probe = "glpquick-cert/glpquick.key"
    proc = subprocess.run(
        ["git", "check-ignore", "-v", probe],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
    )
    assert proc.returncode == 0 and ".gitignore" in proc.stdout, (
        f"{probe} is NOT ignored by any rule. Either the .gitignore entry was "
        f"removed, or the path is tracked again (a tracked path never matches "
        f"check-ignore). git check-ignore said: {proc.stdout or proc.stderr!r}"
    )

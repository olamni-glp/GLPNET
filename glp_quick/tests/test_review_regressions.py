"""Regression tests for the 036 code-review fixes (2026-07-02).

Pure-logic checks — no C# host / dotnet needed, so they always run in the unit suite.
"""

from __future__ import annotations

from glp_quick.cli import _validate_profile
from glp_quick.stacks.csharp import _EXIT_TOKEN


def test_gleam_default_profile_is_a_not_unbuilt_c() -> None:
    # Review finding #2: bare `--stack gleam` (no --profile) must default to the built,
    # documented Profile A — not Profile C, which is opt-in and may be build-blocked.
    assert _validate_profile("gleam", None) == "a"
    assert _validate_profile("gleam", "c") == "c"  # explicit C still honoured
    assert _validate_profile("csharp", None) is None


def test_exit_code_6_maps_to_quic_unsupported() -> None:
    # Review finding #4: host ExitQuicUnsupported == 6 emits `ERR quic_unsupported ...`;
    # the exit-code→token map must agree (was mislabelled alpn_version_mismatch).
    assert _EXIT_TOKEN[6] == "quic_unsupported"

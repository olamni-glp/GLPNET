"""T057 [US9 close-out] — SC coverage map (SC-013).

Asserts that every user story (US1–US8) and every success criterion (SC-001..SC-012) has at least one
asserting automated test in the suite. This is the machine-checkable half of US9's "Definition of Done";
the two-host (SC-002) and RDP/first-user (SC-001/SC-008) criteria are approximated by the loopback/mesh
harness + the on-screen ``/help`` completeness proxy, with the manual second-host/RDP pass noted as the
real acceptance (per spec Assumptions).
"""

from __future__ import annotations

import pathlib

import pytest

from glp_quick.tui import _HELP

TESTS = pathlib.Path(__file__).parent

#: Each story / criterion → test module(s) (relative to tests/) that assert it. Every key must map to
#: at least one existing module that contains real ``def test_`` functions.
COVERAGE = {
    # --- user stories ---
    "US1": ["unit/test_notty_fallback.py", "unit/test_recv_threadsafe.py",
            "unit/test_link_drop_report.py", "unit/test_at_delivery.py",
            "integration/test_us1_conversation_mesh.py"],
    "US2": ["unit/test_pages.py", "integration/test_us2_page_transmit_mesh.py"],
    "US3": ["unit/test_presentation.py"],
    "US4": ["unit/test_joint.py", "unit/test_forms.py", "integration/test_us4_joint_forms_mesh.py"],
    "US5": ["unit/test_replpage.py", "integration/test_us5_repl_page_mesh.py"],
    "US6": ["unit/test_rcopy_filter.py", "unit/test_rcopy_wizard.py",
            "integration/test_us6_rcopy_e2e_mesh.py"],
    "US7": ["unit/test_keys.py"],
    "US8": ["unit/test_rcopy_wal.py", "unit/test_rcopy_catalog.py",
            "unit/test_rcopy_responder.py", "unit/test_rcopy_commit.py"],
    # --- success criteria ---
    "SC-001": ["unit/test_notty_fallback.py", "test_sc_coverage_map.py"],   # RDP-safe typed path + /help
    "SC-002": ["integration/test_us2_page_transmit_mesh.py"],
    "SC-003": ["unit/test_notty_fallback.py"],
    "SC-004": ["unit/test_presentation.py"],
    "SC-005": ["unit/test_joint.py", "integration/test_us4_joint_forms_mesh.py"],
    "SC-006": ["unit/test_replpage.py", "integration/test_us5_repl_page_mesh.py"],
    "SC-007": ["unit/test_rcopy_wizard.py", "integration/test_us6_rcopy_e2e_mesh.py"],
    "SC-008": ["test_sc_coverage_map.py"],                                   # /help completeness proxy
    "SC-009": ["unit/test_rcopy_commit.py", "unit/test_rcopy_responder.py",
               "integration/test_us6_rcopy_e2e_mesh.py"],
    "SC-010": ["unit/test_rcopy_wal.py", "unit/test_rcopy_commit.py",
               "integration/test_us6_rcopy_e2e_mesh.py"],
    "SC-011": ["unit/test_at_delivery.py", "unit/test_routing_resolve.py"],
    "SC-012": ["unit/test_link_drop_report.py", "unit/test_recv_threadsafe.py"],
}

#: The core operator commands that ``/help`` MUST enumerate (RDP-safe surface, SC-008 proxy).
CORE_COMMANDS = [
    "/help", "/theme", "/pages", "/new", "/transmit", "/next", "/prev", "/goto", "/focus",
    "/joint", "/pin", "/undo-pin", "/mask", "/fill", "/repl", "/return", "/bind", "/rcopy",
    "/layout", "/quit", "/send",
]


def _has_tests(relpath: str) -> bool:
    p = TESTS / relpath
    return p.exists() and "def test_" in p.read_text(encoding="utf-8")


@pytest.mark.parametrize("key", sorted(COVERAGE))
def test_every_story_and_criterion_has_an_asserting_test(key):
    files = COVERAGE[key]
    covered = [f for f in files if _has_tests(f)]
    assert covered, f"{key} has no asserting test among {files}"


def test_help_enumerates_the_transmit_path_and_every_core_command():
    # SC-008 proxy: on-screen /help must show the //+Enter transmit path and every core command, so a
    # first user can reach a transmitted message from /help alone (the real acceptance is a manual pass).
    assert "'//'" in _HELP and "Enter" in _HELP        # the RDP-safe transmit path
    missing = [c for c in CORE_COMMANDS if c not in _HELP]
    assert not missing, f"/help is missing commands: {missing}"


def test_coverage_map_names_all_stories_and_criteria():
    for i in range(1, 9):
        assert f"US{i}" in COVERAGE
    for i in range(1, 13):
        assert f"SC-{i:03d}" in COVERAGE

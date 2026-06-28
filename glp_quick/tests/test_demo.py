"""Demo driver: the same-host conformance run reports honestly (T022).

Verifiable criteria (SC-001/002/005) PASS over a genuine link; criteria needing US2/US3/a second
host are reported NOT-RUN — never silently PASS. Skipped when the C# host dll is not built.
"""

from __future__ import annotations

import socket

import pytest

from glp_quick import cert as cert_mod
from glp_quick.demo import run_demo
from glp_quick.stacks.csharp import host_dll_path

pytestmark = pytest.mark.skipif(
    not host_dll_path().exists(),
    reason="glp_quick_host.dll not built (run: dotnet build csharp/glp_quick_host)",
)


def _free_udp_port() -> int:
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.bind(("127.0.0.1", 0))
    port = s.getsockname()[1]
    s.close()
    return port


def test_demo_passes_run_criteria_and_does_not_overclaim(tmp_path):
    cert_mod.generate_shared_cert(tmp_path, days=2)
    report = run_demo("127.0.0.1", _free_udp_port(), tmp_path, stack="csharp", clients=3)

    # The genuinely-exercised criteria pass over the real link.
    assert report.results["SC-001 real on-wire QUIC/HTTP-3 handshake (not loopback-sim)"] == "PASS"
    assert report.results["SC-002 full-duplex GLP-message exchange"] == "PASS"
    assert report.results["SC-005 shared self-signed cert (SPKI pin) is the only trust anchor"] == "PASS"
    assert report.ok

    # What we cannot show same-host is NOT silently claimed.
    assert report.results["SC-003 ≥3 concurrent isolated clients"].startswith("NOT-RUN")
    assert report.results["SC-006 cross-stack csharp ≡ gleam"].startswith("NOT-RUN")

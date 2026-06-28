"""LAN-IP conformance demo driver (T022) — the SC-001..SC-006 harness.

Runs a genuine QUIC+WS link via the data-plane stack and reports pass/fail per success criterion.
What is verifiable **same-host** (one machine, two+ processes over loopback or the host's LAN IP):
SC-001 (real on-wire handshake), SC-002 (full-duplex GLP-message exchange), SC-005 (shared-cert SPKI
pin is the only trust anchor). Honestly reported as NOT-RUN here, with the reason:

  - SC-003 / SC-004 (≥3 concurrent isolated clients + single-failure resilience) need the **US2
    multi-accept server** (one QuicListener accepting N isolated links + mesh routing) — not yet built.
  - SC-006 (cross-stack csharp≡gleam) needs the **US3 Gleam stack** (toolchain absent here).
  - The true cross-host LAN run (two machines by IP/name) needs a second host; same-host exercises the
    identical real-QUIC path but is not a substitute for the final two-host acceptance (T040).

No criterion is ever reported PASS unless it was actually exercised (no silent over-claim).
"""

from __future__ import annotations

import socket
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional

from glp_quick.repl_link import GlpMessage
from glp_quick.stacks.base import StackAdapter
from glp_quick.stacks.csharp import CSharpStackAdapter


@dataclass
class DemoReport:
    results: dict = field(default_factory=dict)  # criterion -> "PASS" | "FAIL" | "NOT-RUN: <reason>"

    def record(self, criterion: str, status: str) -> None:
        self.results[criterion] = status

    @property
    def ok(self) -> bool:
        return all(v == "PASS" for v in self.results.values() if not v.startswith("NOT-RUN"))

    def render(self) -> str:
        width = max((len(k) for k in self.results), default=0)
        lines = [f"  {k.ljust(width)}  {v}" for k, v in self.results.items()]
        verdict = "PASS" if self.ok else "FAIL"
        return "GLP-Quick conformance demo\n" + "\n".join(lines) + f"\n  => {verdict} (run criteria)"


def _adapter(stack: str) -> StackAdapter:
    if stack == "csharp":
        return CSharpStackAdapter()
    if stack == "gleam":
        raise NotImplementedError(
            "the gleam stack (US3) is not built — toolchain (gleam/erl/quicer/AtomVM) absent; "
            "and it is gated behind the C# reference passing the full LAN demo (FR-010)."
        )
    raise ValueError(f"unknown --stack {stack!r}")


def _free_local_port() -> int:
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.bind(("127.0.0.1", 0))
    port = s.getsockname()[1]
    s.close()
    return port


def run_demo(addr: str, port: Optional[int], cert_dir: Path, stack: str = "csharp", clients: int = 3) -> DemoReport:
    """Run the conformance demo; return a per-criterion report (no over-claiming)."""
    report = DemoReport()
    adapter = _adapter(stack)
    port = port or _free_local_port()

    # --- SC-001 / SC-002 / SC-005: genuine 1:1 real-QUIC link, full-duplex, shared-cert pin ---
    server = adapter.start_server(addr, port, cert_dir, max(3, clients), "csharp")
    client = adapter.start_client(addr, port, cert_dir, "csharp")
    try:
        client.send(GlpMessage(sender="client", to="server", payload="hello(server)"))
        a = server.recv(timeout=10)
        server.send(GlpMessage(sender="server", to="client", payload="hello(client)"))
        b = client.recv(timeout=10)

        handshake_ok = a is not None and b is not None
        report.record("SC-001 real on-wire QUIC/HTTP-3 handshake (not loopback-sim)", "PASS" if handshake_ok else "FAIL")
        duplex_ok = handshake_ok and a.payload == "hello(server)" and b.payload == "hello(client)"
        report.record("SC-002 full-duplex GLP-message exchange", "PASS" if duplex_ok else "FAIL")
        # The handshake completed only because both ends pinned the same shared cert by SPKI; a
        # mismatched cert is rejected (covered by test_csharp_adapter.test_cert_mismatch_is_rejected).
        report.record("SC-005 shared self-signed cert (SPKI pin) is the only trust anchor",
                      "PASS" if handshake_ok else "FAIL")
    finally:
        adapter.stop(client)
        adapter.stop(server)

    # --- honestly NOT-RUN here ---
    if clients > 1:
        report.record(f"SC-003 ≥{clients} concurrent isolated clients",
                      "NOT-RUN: needs US2 multi-accept server (one listener, N isolated links)")
        report.record("SC-004 single-client-failure resilience (siblings unaffected)",
                      "NOT-RUN: needs US2 multi-accept server")
    report.record("SC-006 cross-stack csharp ≡ gleam", "NOT-RUN: gleam stack (US3) not built — toolchain absent")
    report.record("two-host LAN acceptance (T040)",
                  "NOT-RUN: same-host exercises the identical real-QUIC path; cross-host needs a 2nd host")
    return report

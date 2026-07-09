"""US3 — Gleam stack (Profile A) interchangeable with the C# stack (T031-T034, SC-006).

Profile A = Gleam/BEAM channel-link logic + the C# `glp_quick_host` as a native genuine-QUIC
side-process. These tests prove the Gleam stack reaches a real QUIC+WS link and exchanges GLP
messages BOTH directions against the C# stack — the cross-stack equivalence (SC-006). Skipped when
the Gleam/Erlang toolchain or the C# host dll is unavailable.
"""

from __future__ import annotations

import socket
from pathlib import Path

import pytest

from glp_quick import cert as cert_mod
from glp_quick.repl_link import GlpMessage
from glp_quick.stacks.csharp import CSharpStackAdapter, LinkError, host_dll_path
from glp_quick.stacks.gleam import (
    GleamStackAdapter,
    _erlang_bin,
    gleam_exe,
    gleam_project_dir,
    profile_c_built,
)


def _gleam_available() -> bool:
    if not host_dll_path().exists():
        return False
    if not (gleam_project_dir() / "gleam.toml").exists():
        return False
    if _erlang_bin() is None:
        return False
    return Path(gleam_exe()).exists() or gleam_exe() == "gleam"


pytestmark = pytest.mark.skipif(not _gleam_available(), reason="Gleam/Erlang toolchain or C# host dll unavailable")


def _free_udp_port() -> int:
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.bind(("127.0.0.1", 0))
    port = s.getsockname()[1]
    s.close()
    return port


@pytest.fixture
def cert_dir(tmp_path):
    cert_mod.generate_shared_cert(tmp_path, days=2)
    return tmp_path


def test_capabilities_honest():
    gl = GleamStackAdapter(profile="a")
    assert gl.name() == "gleam"
    assert gl.profile() == "a"
    assert gl.capabilities() == {"real_quic": True, "quic_termination": "side_process"}


@pytest.mark.skipif(profile_c_built(), reason="Profile C IS built here — the not-built guard cannot fire")
def test_profile_c_not_built_is_clear():
    gl = GleamStackAdapter(profile="c")
    with pytest.raises(LinkError) as ei:
        gl.start_client("127.0.0.1", 1, Path("."), "csharp")
    assert ei.value.token == "profile_c_not_built"


@pytest.mark.skipif(not profile_c_built(), reason="Profile C not built (quicer NIF absent)")
def test_profile_c_client_in_process_to_csharp_server(cert_dir):
    """Profile C (feature 049 US2 / 036 T032): the BEAM client terminates QUIC IN-PROCESS via the
    quicer NIF (no C# side-process on the client data plane) against the C# reference server —
    connect, SPKI pin verify, and full-duplex exchange (FR-009)."""
    port = _free_udp_port()
    cs = CSharpStackAdapter()
    gl = GleamStackAdapter(profile="c")
    assert gl.capabilities() == {"real_quic": True, "quic_termination": "in_process"}
    server = cs.start_server("127.0.0.1", port, cert_dir, 3, "csharp")
    client = gl.start_client("127.0.0.1", port, cert_dir, "csharp")
    try:
        client.send(GlpMessage(sender="beam-c", to="server", payload="ping(inproc)"))
        got = server.recv(timeout=20)
        assert got is not None and got.payload == "ping(inproc)" and got.sender == "beam-c"
        server.send(GlpMessage(sender="server", to="beam-c", payload="pong(inproc)"))
        back = client.recv(timeout=20)
        assert back is not None and back.payload == "pong(inproc)"
    finally:
        gl.stop(client)
        cs.stop(server)


@pytest.mark.skipif(not profile_c_built(), reason="Profile C not built (quicer NIF absent)")
def test_profile_c_pin_mismatch_rejected(cert_dir, tmp_path_factory):
    """A wrong shared cert must fail the Profile C handshake loudly (cert_mismatch), never
    silently accept — the SPKI pin is the only trust anchor (FR-003/SC-005)."""
    other = tmp_path_factory.mktemp("othercert")
    cert_mod.generate_shared_cert(other, days=2)
    port = _free_udp_port()
    cs = CSharpStackAdapter()
    gl = GleamStackAdapter(profile="c")
    server = cs.start_server("127.0.0.1", port, cert_dir, 3, "csharp")
    try:
        with pytest.raises(LinkError) as ei:
            gl.start_client("127.0.0.1", port, other, "csharp")
        assert ei.value.token == "cert_mismatch"
    finally:
        cs.stop(server)


def test_gleam_client_to_csharp_server(cert_dir):
    port = _free_udp_port()
    cs = CSharpStackAdapter()
    gl = GleamStackAdapter(profile="a")
    server = cs.start_server("127.0.0.1", port, cert_dir, 3, "csharp")
    client = gl.start_client("127.0.0.1", port, cert_dir, "csharp")
    try:
        client.send(GlpMessage(sender="gleam-c", to="server", payload="ping(g)"))
        got = server.recv(timeout=20)
        assert got is not None and got.payload == "ping(g)" and got.sender == "gleam-c"
        server.send(GlpMessage(sender="server", to="gleam-c", payload="pong(g)"))
        back = client.recv(timeout=20)
        assert back is not None and back.payload == "pong(g)"
    finally:
        gl.stop(client)
        cs.stop(server)


def test_csharp_client_to_gleam_server(cert_dir):
    """The reverse leg: a Gleam-hosted server (C# QUIC side-process) serving a C# client."""
    port = _free_udp_port()
    gl = GleamStackAdapter(profile="a")
    cs = CSharpStackAdapter()
    server = gl.start_server("127.0.0.1", port, cert_dir, 3, "csharp")
    client = cs.start_client("127.0.0.1", port, cert_dir, "csharp")
    try:
        client.send(GlpMessage(sender="cs-c", to="server", payload="ping(c)"))
        got = server.recv(timeout=20)
        assert got is not None and got.payload == "ping(c)"
        server.send(GlpMessage(sender="server", to="cs-c", payload="pong(c)"))
        back = client.recv(timeout=20)
        assert back is not None and back.payload == "pong(c)"
    finally:
        cs.stop(client)
        gl.stop(server)

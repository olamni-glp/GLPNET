"""C# data-plane adapter — the cross-platform reference stack (FR-009/FR-010, T018).

Launches + supervises the C# QUIC+WS endpoint (``csharp/glp_quick_host``), which runs the genuine
real-QUIC + RFC 6455 WebSocket link (spec 025 `QuicTransport` leaf, FR-018) and bridges
newline-delimited L5 GLP-message envelopes over its stdio. This adapter speaks to it over that
stdio seam: data envelopes on stdout, control + FR-019 failure tokens on stderr.
"""

from __future__ import annotations

import os
import queue
import shutil
import subprocess
import threading
from pathlib import Path
from typing import Optional, Sequence

from glp_quick.repl_link import GlpMessage
from glp_quick.stacks.base import Handle, ReplKind, StackAdapter, StackName, Status

# FR-019 failure tokens the host emits on stderr (`ERR <token> ...`) → these exit codes.
_EXIT_TOKEN = {
    2: "usage",
    3: "cert_mismatch",
    4: "server_not_ready",
    5: "udp_blocked",
    6: "quic_unsupported",
    7: "bind_failed",
}


class LinkError(RuntimeError):
    """A clear, distinct link failure (FR-019) — carries the wire-contract token (e.g. ``cert_mismatch``)."""

    def __init__(self, token: str, detail: str = "") -> None:
        super().__init__(f"{token}: {detail}".strip(": "))
        self.token = token
        self.detail = detail


def _repo_root() -> Path:
    # glp_quick/src/glp_quick/stacks/csharp.py → repo root is five parents up.
    return Path(__file__).resolve().parents[4]


def host_dll_path() -> Path:
    """Resolve the built ``glp_quick_host.dll`` (override with ``GLPQUICK_HOST_DLL``).

    We launch the framework-dependent dll via ``dotnet`` rather than the apphost ``.exe`` so the
    runtime is resolved by the (possibly non-default-located) ``dotnet`` host — the apphost only
    searches default install locations and fails when .NET lives under the user profile.
    """
    override = os.environ.get("GLPQUICK_HOST_DLL")
    if override:
        return Path(override)
    base = _repo_root() / "csharp" / "glp_quick_host" / "bin"
    for config in ("Debug", "Release"):
        cand = base / config / "net10.0" / "glp_quick_host.dll"
        if cand.exists():
            return cand
    return base / "Debug" / "net10.0" / "glp_quick_host.dll"


def dotnet_path() -> str:
    """Resolve the ``dotnet`` host (override with ``GLPQUICK_DOTNET``)."""
    override = os.environ.get("GLPQUICK_DOTNET")
    if override:
        return override
    found = shutil.which("dotnet")
    if found:
        return found
    exe = "dotnet.exe" if os.name == "nt" else "dotnet"
    candidates = [
        os.environ.get("DOTNET_ROOT", ""),
        os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "dotnet"),
        r"C:\Program Files\dotnet" if os.name == "nt" else "/usr/share/dotnet",
        os.path.join(os.path.expanduser("~"), ".dotnet"),
    ]
    for root in candidates:
        if root and Path(root, exe).exists():
            return str(Path(root, exe))
    return "dotnet"  # last resort — PATH lookup at exec time


def _pump_stdout(stream, rx: "queue.Queue[Optional[bytes]]") -> None:
    """Drain a host process's stdout into ``rx`` until EOF, then signal a graceful close.

    Module-level so it can be started at process-spawn time (before a CSharpHandle exists) to avoid a
    stdout-pipe-fill deadlock while blocked on the stderr readiness signal (A5 / code-review #6).
    """
    for raw in stream:
        line = raw.rstrip(b"\r\n")
        if line:
            rx.put(line)
    rx.put(None)  # EOF -> unblock recv with a graceful close


class CSharpHandle(Handle):
    """Live link end backed by a supervised ``glp_quick_host`` process (the GLP-message I/O seam)."""

    def __init__(self, proc: subprocess.Popen, link_label: str, peer_ids: Sequence[str],
                 rx: "Optional[queue.Queue[Optional[bytes]]]" = None,
                 reader: Optional[threading.Thread] = None) -> None:
        self._proc = proc
        self._link = link_label
        self._peers = list(peer_ids)
        # A stdout reader may already be draining (started at spawn to avoid a pipe-fill deadlock
        # before readiness — see spawn_handle); adopt it rather than start a second reader.
        self._rx: "queue.Queue[Optional[bytes]]" = rx if rx is not None else queue.Queue()
        self._closed_reason: Optional[str] = None
        if reader is not None:
            self._reader = reader
        else:
            self._reader = threading.Thread(target=self._read_stdout, daemon=True)
            self._reader.start()
        self._errs = threading.Thread(target=self._drain_stderr, daemon=True)
        self._errs.start()

    @property
    def link_id(self) -> str:
        return self._link

    def send(self, message: GlpMessage) -> None:
        assert self._proc.stdin is not None
        self._proc.stdin.write(message.to_wire() + b"\n")
        self._proc.stdin.flush()

    def recv(self, timeout: Optional[float] = None) -> Optional[GlpMessage]:
        try:
            line = self._rx.get(timeout=timeout)
        except queue.Empty:
            return None
        if line is None:  # graceful link close / process exit
            return None
        return GlpMessage.from_wire(line)

    def peers(self) -> Sequence[str]:
        return tuple(self._peers)

    def _read_stdout(self) -> None:
        assert self._proc.stdout is not None
        _pump_stdout(self._proc.stdout, self._rx)

    def _drain_stderr(self) -> None:
        assert self._proc.stderr is not None
        for raw in self._proc.stderr:
            line = raw.rstrip(b"\r\n").decode("utf-8", "replace")
            if line.startswith("LINK_CLOSED") or line.startswith("FAULT"):
                self._closed_reason = line


class CSharpStackAdapter(StackAdapter):
    """The C# reference stack (cross-platform, genuine in-process QUIC)."""

    def name(self) -> StackName:
        return "csharp"

    def profile(self) -> Optional[str]:
        return None

    def capabilities(self) -> dict:
        return {"real_quic": True, "quic_termination": "in_process"}

    def start_server(self, bind: str, port: int, cert: Path, max_clients: int, repl: ReplKind,
                     *, repl_path: Optional[str] = None, self_id: Optional[str] = None) -> Handle:
        args = ["--role", "server", "--addr", bind, "--port", str(port),
                "--cert", str(cert), "--max-clients", str(max_clients)]
        if self_id:
            args += ["--id", self_id]
        if repl_path:  # 063 US1 (contract C1): the host spawns + bridges the live GLP REPL child
            args += ["--repl", repl_path]
        return self._spawn(args, await_token="READY", peer_ids=[f"server@{bind}:{port}"])

    def start_client(self, server_addr: str, port: int, cert: Path, repl: ReplKind,
                     *, repl_path: Optional[str] = None, self_id: Optional[str] = None) -> Handle:
        args = ["--role", "client", "--addr", server_addr, "--port", str(port), "--cert", str(cert)]
        if self_id:
            args += ["--id", self_id]
        if repl_path:
            args += ["--repl", repl_path]
        return self._spawn(args, await_token="LINK_UP", peer_ids=[f"server@{server_addr}:{port}"])

    def health(self, handle: Handle) -> Status:
        h = handle
        assert isinstance(h, CSharpHandle)
        alive = h._proc.poll() is None
        if h._closed_reason:
            return Status(state="closed", alive=alive, detail=h._closed_reason)
        return Status(state="linked" if alive else "failed", alive=alive)

    def stop(self, handle: Handle) -> None:
        h = handle
        assert isinstance(h, CSharpHandle)
        terminate_tree(h._proc)

    def _spawn(self, args: list[str], await_token: str, peer_ids: Sequence[str]) -> CSharpHandle:
        dll = host_dll_path()
        if not dll.exists():
            raise LinkError(
                "host_missing",
                f"{dll} not built — run: dotnet build csharp/glp_quick_host/glp_quick_host.csproj",
            )
        return spawn_handle([dotnet_path(), str(dll), *args], await_token, peer_ids)


def terminate_tree(proc: subprocess.Popen) -> None:
    """Kill a supervised endpoint process AND its children (e.g. gleam -> erl -> dotnet), so no QUIC
    host is orphaned holding its UDP port. The host now lives for the link's lifetime (not stdin),
    so terminating is the stop signal; the peer detects the drop via the aborted QUIC connection.
    """
    try:
        if proc.stdin and not proc.stdin.closed:
            proc.stdin.close()
    except OSError:
        pass
    try:
        if os.name == "nt":
            subprocess.run(["taskkill", "/PID", str(proc.pid), "/T", "/F"],
                           capture_output=True, timeout=8)
        else:
            proc.terminate()
    except Exception:
        try:
            proc.kill()
        except Exception:
            pass
    try:
        proc.wait(timeout=5)
    except Exception:
        pass


def spawn_handle(
    cmd: list[str],
    await_token: str,
    peer_ids: Sequence[str],
    env: Optional[dict] = None,
    cwd: Optional[str] = None,
) -> CSharpHandle:
    """Launch a host endpoint process and block on its stderr readiness/error signal.

    Shared by the C# and Gleam stacks — both supervise an endpoint that speaks the identical stdio
    seam (data envelopes on stdout; `READY`/`LINK_UP`/`ERR <token>` control on stderr), so the
    control plane is stack-agnostic (SC-006).
    """
    proc = subprocess.Popen(
        cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, bufsize=0,
        env=env, cwd=cwd,
    )
    assert proc.stderr is not None and proc.stdout is not None
    # A5/#6: drain stdout the instant the process exists — otherwise the host can fill its stdout pipe
    # (>~64 KB) before a reader attaches and block, never emitting READY on stderr (a startup hang).
    rx: "queue.Queue[Optional[bytes]]" = queue.Queue()
    reader = threading.Thread(target=_pump_stdout, args=(proc.stdout, rx), daemon=True)
    reader.start()
    link_label = await_token
    while True:
        raw = proc.stderr.readline()
        if not raw:  # stderr EOF before readiness → the process failed to start the link
            code = proc.wait()
            raise LinkError(_EXIT_TOKEN.get(code, "link_failed"), f"endpoint exited {code} before {await_token}")
        line = raw.rstrip(b"\r\n").decode("utf-8", "replace")
        if line.startswith("ERR "):
            parts = line.split(" ", 2)
            token = parts[1] if len(parts) > 1 else "link_failed"
            proc.wait()
            raise LinkError(token, parts[2] if len(parts) > 2 else "")
        if line.startswith(await_token):
            link_label = line.split(" ", 1)[1] if " " in line else await_token
            break
    return CSharpHandle(proc, link_label, peer_ids, rx=rx, reader=reader)

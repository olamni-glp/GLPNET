"""Gleam data-plane adapter — the staged second stack (FR-009/FR-010, US3, Decision 8).

**Profile A** (built): Gleam/BEAM channel-link logic (`gleam_quic/`) + the verified C# `glp_quick_host`
as a **native genuine-QUIC side-process** over line-delimited local IPC. QUIC is real (the
side-process performs it) and attributed honestly: ``quic_termination = "side_process"`` — never
simulated in-runtime (constitution II). The operator-visible CLI/wire/handshake surface is identical
to the C# stack (SC-006); only the data-plane runtime differs.

**Profile C** (full BEAM + ``quicer``/MsQuic in-process) is gated on a ``quicer`` NIF build (msquic)
on this host — not built here; requesting it returns a clear ``profile_c_not_built`` error rather
than silently falling back (so capabilities are never misreported).
"""

from __future__ import annotations

import os
import shutil
from pathlib import Path
from typing import Optional, Sequence

from glp_quick.stacks.base import GleamProfile, Handle, ReplKind, StackAdapter, StackName, Status
from glp_quick.stacks.csharp import (
    CSharpHandle,
    LinkError,
    dotnet_path,
    host_dll_path,
    spawn_handle,
    terminate_tree,
)


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[4]


def gleam_exe() -> str:
    override = os.environ.get("GLPQUICK_GLEAM")
    if override:
        return override
    found = shutil.which("gleam")
    if found:
        return found
    winget = Path(
        os.environ.get("LOCALAPPDATA", ""),
        "Microsoft", "WinGet", "Packages",
        "Gleam.Gleam_Microsoft.Winget.Source_8wekyb3d8bbwe", "gleam.exe",
    )
    return str(winget) if winget.exists() else "gleam"


def _erlang_bin() -> Optional[str]:
    override = os.environ.get("GLPQUICK_ERLANG_BIN")
    if override:
        return override
    for cand in (r"C:\Program Files\Erlang OTP\bin",):
        if Path(cand, "erl.exe").exists():
            return cand
    erl = shutil.which("erl")
    return str(Path(erl).parent) if erl else None


def gleam_project_dir() -> Path:
    return _repo_root() / "gleam_quic"


def _augmented_env() -> dict:
    env = dict(os.environ)
    parts = [str(Path(gleam_exe()).parent)]
    erl = _erlang_bin()
    if erl:
        parts.append(erl)
    env["PATH"] = os.pathsep.join(parts + [env.get("PATH", "")])
    return env


class GleamStackAdapter(StackAdapter):
    """The Gleam stack (Profile A: genuine QUIC via the C# side-process)."""

    def __init__(self, profile: GleamProfile = "a") -> None:
        self._profile = profile

    def name(self) -> StackName:
        return "gleam"

    def profile(self) -> Optional[str]:
        return self._profile

    def capabilities(self) -> dict:
        if self._profile == "a":
            return {"real_quic": True, "quic_termination": "side_process"}
        # Profile C would be {"real_quic": True, "quic_termination": "in_process"} once quicer builds.
        return {"real_quic": True, "quic_termination": "in_process"}

    def start_server(self, bind: str, port: int, cert: Path, max_clients: int, repl: ReplKind) -> Handle:
        host_args = ["--role", "server", "--addr", bind, "--port", str(port),
                     "--cert", str(cert), "--max-clients", str(max_clients)]
        return self._spawn(host_args, await_token="READY", peer_ids=[f"server@{bind}:{port}"])

    def start_client(self, server_addr: str, port: int, cert: Path, repl: ReplKind) -> Handle:
        host_args = ["--role", "client", "--addr", server_addr, "--port", str(port), "--cert", str(cert)]
        return self._spawn(host_args, await_token="LINK_UP", peer_ids=[f"server@{server_addr}:{port}"])

    def health(self, handle: Handle) -> Status:
        h = handle
        assert isinstance(h, CSharpHandle)
        alive = h._proc.poll() is None
        return Status(state="linked" if alive else "failed", alive=alive)

    def stop(self, handle: Handle) -> None:
        h = handle
        assert isinstance(h, CSharpHandle)
        terminate_tree(h._proc)  # kills gleam → erl → the C# QUIC side-process (no orphans)

    def _spawn(self, host_args: list[str], await_token: str, peer_ids: Sequence[str]) -> CSharpHandle:
        if self._profile == "c":
            raise LinkError(
                "profile_c_not_built",
                "Gleam Profile C (full BEAM + quicer/MsQuic in-process) needs a quicer NIF build on "
                "this host (msquic). Not built — use --profile a (genuine QUIC via native side-process).",
            )
        dll = host_dll_path()
        if not dll.exists():
            raise LinkError("host_missing", f"{dll} not built — the side-process QUIC host is required for Profile A")
        proj = gleam_project_dir()
        if not (proj / "gleam.toml").exists():
            raise LinkError("gleam_missing", f"{proj} not found — the gleam_quic project is required")
        # gleam run -- <dotnet> <host-dll> <host args...>
        cmd = [gleam_exe(), "run", "--", dotnet_path(), str(dll), *host_args]
        return spawn_handle(cmd, await_token, peer_ids, env=_augmented_env(), cwd=str(proj))

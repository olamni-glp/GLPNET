"""Cross-process unified PGLite bridge discovery + auto-spawn (Python side).

Mirrors `tools/d2net/src/D2Net.BridgeClient/BridgeClient.cs` so any
`.pgdb/`-using client (Python codeconv, .NET D2Net.*, future tools)
follows the same lifecycle protocol defined in
`specs/012-codeconv-runner/contracts/bridge_lifecycle.md`.

**Contract amendment (from Phase 5 implementation)**: the client does NOT
hold its own OS-level lock. The bridge IS the sole lock holder (it
acquires `<data-dir>.bridge.lock/` via ``proper-lockfile`` at startup
with ``retries=0``). Clients ALWAYS:

  1. Try to read a sidecar AND ping the endpoint — fast path for an
     already-running bridge.
  2. If no sidecar / sidecar dead, spawn the bridge speculatively. Watch
     for either ``BRIDGE_READY`` (we won the race; ``owned=True``) or
     exit-5 (another bridge won the race; read sidecar with one 250 ms
     retry).

This works because the bridge's ``proper-lockfile`` mkdir is the actual
mutex, and a client trying to participate in a separate file-based lock
at the same path conflicts with the bridge's mkdir on Windows (EEXIST)
and on POSIX. Empirically the original flow in bridge_lifecycle.md
("ACQUIRE_LOCK + spawn + release in finally") cannot succeed against the
shipped bridge because the bridge does not retry. See the .NET sibling
``D2Net.BridgeClient`` whose end-to-end test is gated behind
``D2NET_BRIDGECLIENT_E2E`` for the same reason.
"""

from __future__ import annotations

import json
import os
import socket
import subprocess
import threading
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Optional

import portalocker  # used only by `_try_acquire_lock` diagnostic helper


READY_TIMEOUT_DEFAULT_SECONDS = 30.0  # cover PGLite cold-init (~7s on Windows) + Node startup
SIDECAR_RETRY_DELAY_SECONDS = 0.25
SIDECAR_FALLBACK_WAIT_SECONDS = 30.0  # cover PGLite cold-init (~7s on Windows)
STALE_LOCK_WAIT_SECONDS = 2.0          # > proper-lockfile stale (1000ms) + update cycle
LOCK_HELD_EXIT_CODE = 5


class BridgeStartupTimeout(RuntimeError):
    """Raised when the bridge does not emit ``BRIDGE_READY`` in time."""


class BridgeRaceLost(RuntimeError):
    """Raised when the bridge process exited 5 (another bridge won) AND
    no sidecar appeared after the contractual retry. The caller may
    retry the whole operation."""


class DataDirFilesystemError(RuntimeError):
    """Raised when the resolved data-dir is on a filesystem PGLite cannot
    use (exFAT, FAT32). See docs/known-issues.md Issue 8."""


_SUPPORTED_WINDOWS_FILESYSTEMS = frozenset({"NTFS", "ReFS"})


def _windows_filesystem_for(path: Path) -> Optional[str]:
    """Return the filesystem-type string (e.g. ``"NTFS"``, ``"exFAT"``) of
    the volume that hosts ``path``, or ``None`` if it cannot be determined.

    Walks up from ``path`` until it finds an existing parent, then asks the
    Win32 API for that volume's filesystem name. Returns ``None`` on
    non-Windows or if any step fails — we treat "unknown" as "don't block"
    (POSIX-class filesystems on Linux/Mac don't hit this issue).
    """
    if os.name != "nt":
        return None
    try:
        import ctypes

        probe = path
        while not probe.exists() and probe.parent != probe:
            probe = probe.parent
        if not probe.exists():
            return None
        drive = Path(probe.anchor or probe.drive + os.sep)
        if str(drive) == "":
            return None
        buf = ctypes.create_unicode_buffer(256)
        ok = ctypes.windll.kernel32.GetVolumeInformationW(
            ctypes.c_wchar_p(str(drive)),
            None, 0, None, None, None,
            buf, 256,
        )
        if not ok:
            return None
        return buf.value or None
    except Exception:
        return None


def _check_data_dir_filesystem(data_dir_path: Path) -> None:
    """Hard-fail early if data_dir is on a PGLite-hostile filesystem.

    See docs/known-issues.md Issue 8: PGLite's WASM build needs POSIX-style
    atomic rename / advisory locks / mmap, which exFAT does not implement.
    Without this guard, the bridge spawns successfully and then dies
    mid-DBOS-migration with a misleading "server closed the connection
    unexpectedly" error several seconds later.
    """
    fs_type = _windows_filesystem_for(data_dir_path)
    if fs_type is None:
        return
    if fs_type in _SUPPORTED_WINDOWS_FILESYSTEMS:
        return
    raise DataDirFilesystemError(
        f"data_dir {data_dir_path} is on a {fs_type} filesystem; "
        f"PGLite requires NTFS or ReFS. "
        f"Pass --data-dir <NTFS-path> (e.g. C:/pglite/research/<project>). "
        f"See docs/known-issues.md Issue 8."
    )


@dataclass
class BridgeEndpoint:
    host: str
    port: int
    pid: int
    owned: bool
    # Heartbeat fields populated when sidecar carries them; None for older bridges.
    heartbeat_seq: Optional[int] = None
    heartbeat_at_iso: Optional[str] = None
    # Live consumer registration owned by THIS process for the lifetime of the
    # endpoint. Set when ``register_consumer=True`` (default). The lock is
    # kernel-released on process exit; explicit ``release_consumer()`` is
    # available for tests / clean shutdown but not required.
    _consumer_lock: object = None
    _consumer_path: Optional[str] = None

    def release_consumer(self) -> None:
        """Best-effort release of the consumer registration. Kernel auto-cleans
        on process exit; only call this from tests or controlled shutdown."""
        if self._consumer_lock is not None:
            try:
                self._consumer_lock.release()
            except Exception:
                pass
            self._consumer_lock = None
        if self._consumer_path is not None:
            try:
                os.unlink(self._consumer_path)
            except Exception:
                pass
            self._consumer_path = None


HEARTBEAT_FRESHNESS_SECONDS = 30.0  # sidecar heartbeat older than this = suspect

# Per-process consumer-registration singleton — Gabi's per-process granularity:
# a multi-threaded tool registers exactly once at process level. We track the
# lock + path per-data-dir so a single process talking to multiple bridges
# (unusual but legal) registers correctly with each.
_consumer_registrations_lock = threading.Lock()
_consumer_registrations: dict[str, tuple[object, str]] = {}


def acquire_or_discover(
    repo_root: str | os.PathLike[str],
    ready_timeout: float = READY_TIMEOUT_DEFAULT_SECONDS,
    *,
    bridge_script: Optional[str | os.PathLike[str]] = None,
    node_executable: Optional[str] = None,
    register_consumer: bool = True,
    data_dir: Optional[str | os.PathLike[str]] = None,
) -> BridgeEndpoint:
    """Acquire a connectable unified bridge endpoint.

    1. Read sidecar; if heartbeat is fresh AND TCP is reachable, fast-path.
    2. Otherwise spawn the bridge speculatively; bridge mkdir is the real mutex.
    3. After acquiring, register THIS process as a consumer (E register-before-connect)
       unless ``register_consumer=False``.

    ``data_dir`` overrides the default ``<repo_root>/.pgdb``. Used when the
    repo lives on a filesystem PGLite can't use (e.g. exFAT — atomic rename
    / advisory locks / mmap operations fail). Sidecar, lock, and consumers
    directory are all derived from ``data_dir`` (siblings, by convention).
    """
    repo_root = Path(repo_root).resolve()
    data_dir_path = (
        Path(data_dir).resolve() if data_dir is not None else (repo_root / ".pgdb")
    )
    _check_data_dir_filesystem(data_dir_path)
    sidecar_path = data_dir_path / "bridge.json"
    # Sibling, not inside data_dir, because PGLite refuses to init a data dir
    # that has non-PG files in it (same reasoning as the bridge lock path).
    consumers_dir = data_dir_path.parent / (data_dir_path.name + ".consumers")

    data_dir_path.mkdir(parents=True, exist_ok=True)
    consumers_dir.mkdir(parents=True, exist_ok=True)
    data_dir = data_dir_path  # used below in spawn / fast-path call sites

    # Fast path: bridge already up and reachable AND heartbeat fresh.
    fast = _try_consume_sidecar_with_heartbeat(sidecar_path, ping_timeout=0.5)
    if fast is not None:
        if register_consumer:
            _register_consumer_into(fast, consumers_dir)
        return fast

    # Slow path: spawn speculatively. The bridge enforces the lock.
    try:
        endpoint = _spawn_or_consume(
            repo_root=repo_root,
            data_dir=data_dir,
            sidecar_path=sidecar_path,
            ready_timeout=ready_timeout,
            bridge_script=bridge_script,
            node_executable=node_executable,
        )
    except BridgeRaceLost:
        # Possible stale lock from a recently-dead bridge. proper-lockfile
        # treats locks older than ~1s as stale; wait past that, retry once.
        time.sleep(STALE_LOCK_WAIT_SECONDS)
        endpoint = _spawn_or_consume(
            repo_root=repo_root,
            data_dir=data_dir,
            sidecar_path=sidecar_path,
            ready_timeout=ready_timeout,
            bridge_script=bridge_script,
            node_executable=node_executable,
        )
    if register_consumer:
        _register_consumer_into(endpoint, consumers_dir)
    return endpoint


def _register_consumer_into(endpoint: BridgeEndpoint, consumers_dir: Path) -> None:
    """C / E — register THIS process as a consumer of the bridge.

    Per-process-granularity (Gabi's decision): one registration per process,
    regardless of thread count. Creates ``<consumers_dir>/<pid>.lock``
    containing this process's pid as the file content. The bridge's orphan-
    poll uses pid-existence (`process.kill(pid, 0)`-equivalent) to determine
    liveness; on consumer crash the file persists with a dead pid and the
    bridge removes it on the next poll tick.

    This is a pid-file pattern, not a kernel-released-fd-lock pattern.
    The trade-offs vs the lock-based variant are documented in
    ``project_bridge_daemon_coordination_deferred.md`` (deferred deep
    investigation): pid-file is simpler and portable but vulnerable to pid
    reuse; kernel-fd-lock is robust but tied to a specific OS primitive.
    For this experiment we accept the pid-reuse risk because realistic
    consumer count is 2–5 and reuse within seconds of crash is unlikely.

    Idempotent within a process — repeat calls (from sibling threads / re-
    invocations of acquire_or_discover) are a no-op once the file exists.
    """
    pid = os.getpid()
    lock_path = consumers_dir / f"{pid}.lock"
    key = str(lock_path.resolve())

    with _consumer_registrations_lock:
        existing = _consumer_registrations.get(key)
        if existing is not None:
            _, existing_path = existing
            endpoint._consumer_path = existing_path
            return

        try:
            lock_path.write_text(f"{pid}\n", encoding="utf-8")
        except OSError as exc:
            raise RuntimeError(
                f"could not write consumer registration file at {lock_path}: {exc}"
            )
        # No portalocker lock — file existence + pid liveness is the signal.
        # We still record the path so release_consumer() can clean up on
        # graceful shutdown.
        _consumer_registrations[key] = (None, str(lock_path))
        endpoint._consumer_path = str(lock_path)


def _try_consume_sidecar_with_heartbeat(
    sidecar_path: Path, *, ping_timeout: float = 0.5
) -> Optional[BridgeEndpoint]:
    """Sidecar fast-path with heartbeat freshness check.

    A sidecar's TCP-reachability is necessary but not sufficient: the bridge
    may be node-alive-but-WASM-hung. Verify the sidecar's ``heartbeat_at`` is
    within ``HEARTBEAT_FRESHNESS_SECONDS``.
    """
    sidecar = _read_sidecar(sidecar_path)
    if sidecar is None:
        return None
    if not _ping_tcp(sidecar["host"], int(sidecar["port"]), ping_timeout):
        return None
    # Heartbeat is optional in older sidecar shapes; if present, enforce.
    hb_at = sidecar.get("heartbeat_at")
    if hb_at is not None:
        if not _heartbeat_fresh(hb_at, HEARTBEAT_FRESHNESS_SECONDS):
            return None
    return BridgeEndpoint(
        host=sidecar["host"],
        port=int(sidecar["port"]),
        pid=int(sidecar.get("pid", 0)),
        owned=False,
        heartbeat_seq=sidecar.get("heartbeat_seq"),
        heartbeat_at_iso=hb_at,
    )


def _heartbeat_fresh(iso: str, max_age_s: float) -> bool:
    from datetime import datetime, timezone

    try:
        # Python 3.11+ accepts trailing Z directly.
        ts = datetime.fromisoformat(iso.replace("Z", "+00:00"))
    except ValueError:
        return False
    now = datetime.now(timezone.utc)
    age = (now - ts).total_seconds()
    return -1.0 <= age <= max_age_s  # tolerate ~1s of clock skew


def request_force_shutdown(
    repo_root: str | os.PathLike[str],
    *,
    data_dir: Optional[str | os.PathLike[str]] = None,
) -> bool:
    """D — non-destructive force-shutdown. Writes ``<data_dir>/.shutdown``
    marker; bridge polls and exits gracefully on next tick. Returns True if
    the marker was written. ``data_dir`` override mirrors
    :func:`acquire_or_discover`'s — for repos on PGLite-hostile filesystems."""
    repo_root = Path(repo_root).resolve()
    data_dir_path = (
        Path(data_dir).resolve() if data_dir is not None else (repo_root / ".pgdb")
    )
    _check_data_dir_filesystem(data_dir_path)
    if not data_dir_path.is_dir():
        return False
    marker = data_dir_path / ".shutdown"
    try:
        marker.write_text("requested\n", encoding="utf-8")
    except OSError:
        return False
    return True


def _spawn_or_consume(
    *,
    repo_root: Path,
    data_dir: Path,
    sidecar_path: Path,
    ready_timeout: float,
    bridge_script: Optional[str | os.PathLike[str]],
    node_executable: Optional[str],
) -> BridgeEndpoint:
    """Spawn the bridge and watch its outcome.

    Three terminal states observed via stdout + process exit:
    - ``BRIDGE_READY`` line → we won the lock race → ``owned=True``.
    - exit code 5 → another bridge won → consume sidecar (Path B).
    - any other early exit OR no READY before deadline → error.
    """
    script = _resolve_bridge_script(repo_root, bridge_script)
    node = _resolve_node_executable(node_executable)

    args = [
        node,
        str(script),
        "--data-dir",
        str(data_dir),
        "--port",
        "0",
        "--daemon",
    ]

    creation_flags = 0
    start_new_session = False
    if os.name == "nt":
        DETACHED_PROCESS = 0x00000008
        CREATE_NEW_PROCESS_GROUP = 0x00000200
        creation_flags = DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP
    else:
        start_new_session = True

    # We deliberately route the bridge's stdio to DEVNULL rather than PIPE.
    # Node block-buffers piped stdout on Windows and only flushes on process
    # exit; trying to read BRIDGE_READY from the pipe stalls Python clients
    # for 30+ seconds. Instead, we poll the sidecar — the bridge writes it
    # synchronously after listen() AND before BRIDGE_READY, so sidecar
    # presence + TCP reachability + heartbeat freshness is a strictly
    # stronger readiness signal than the stdout token.
    proc = subprocess.Popen(
        args,
        cwd=str(repo_root),
        stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        creationflags=creation_flags if os.name == "nt" else 0,
        start_new_session=start_new_session if os.name != "nt" else False,
    )

    deadline = time.monotonic() + ready_timeout
    while time.monotonic() < deadline:
        if proc.poll() is not None:
            rc = proc.returncode
            if rc == LOCK_HELD_EXIT_CODE:
                # Another bridge won. Poll sidecar for the winner's endpoint.
                consumed = _wait_for_reachable_sidecar(
                    sidecar_path, SIDECAR_FALLBACK_WAIT_SECONDS
                )
                if consumed is not None:
                    return consumed
                raise BridgeRaceLost(
                    f"bridge race lost (exit {rc}) but no reachable sidecar "
                    f"at {sidecar_path}"
                )
            raise RuntimeError(
                f"bridge exited with code {rc} before becoming reachable"
            )

        # Check sidecar + TCP. The sidecar carries pid; if it matches our
        # spawned pid, we know we're the spawn-coordinator (Path A / owned).
        endpoint = _try_consume_sidecar_with_heartbeat(sidecar_path, ping_timeout=0.5)
        if endpoint is not None:
            owned = (endpoint.pid == proc.pid) or (endpoint.pid == 0)
            return BridgeEndpoint(
                host=endpoint.host,
                port=endpoint.port,
                pid=endpoint.pid,
                owned=owned,
                heartbeat_seq=endpoint.heartbeat_seq,
                heartbeat_at_iso=endpoint.heartbeat_at_iso,
            )
        time.sleep(0.1)

    _kill_proc_best_effort(proc)
    raise BridgeStartupTimeout(
        f"timed out waiting for bridge to become reachable after {ready_timeout:.1f}s"
    )


def _kill_proc_best_effort(proc: subprocess.Popen) -> None:
    if proc.poll() is not None:
        return
    try:
        if os.name == "nt":
            subprocess.run(
                ["taskkill", "/F", "/T", "/PID", str(proc.pid)],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                check=False,
            )
        else:
            proc.kill()
    except Exception:
        pass


def _wait_for_reachable_sidecar(
    sidecar_path: Path, max_wait_seconds: float
) -> Optional[BridgeEndpoint]:
    """Poll the sidecar until it points to a reachable bridge or timeout.

    The winner's bridge writes the sidecar AFTER ``listen()`` resolves
    AND before emitting BRIDGE_READY. PGLite cold init can take ~7 s on
    Windows, so we may have to wait several seconds for the sidecar
    to appear.
    """
    deadline = time.monotonic() + max_wait_seconds
    while time.monotonic() < deadline:
        consumed = _try_consume_sidecar(sidecar_path, ping_timeout=1.0)
        if consumed is not None:
            return consumed
        time.sleep(SIDECAR_RETRY_DELAY_SECONDS)
    return None


def _try_consume_sidecar(
    sidecar_path: Path, *, ping_timeout: float = 0.5
) -> Optional[BridgeEndpoint]:
    """Sidecar fast-path: only return Path B if the named endpoint actually
    accepts a TCP connection. A stale sidecar (kernel released the lock
    on bridge crash; sidecar lingered) is treated as absent."""
    sidecar = _read_sidecar(sidecar_path)
    if sidecar is None:
        return None
    host = sidecar["host"]
    port = int(sidecar["port"])
    if not _ping_tcp(host, port, ping_timeout):
        return None
    return BridgeEndpoint(host=host, port=port, pid=int(sidecar.get("pid", 0)), owned=False)


def _consume_sidecar_with_retry(sidecar_path: Path) -> Optional[BridgeEndpoint]:
    """Sidecar trust rule: one 250 ms retry to cover the brief window
    between the winner's ``listen()`` and ``WRITE_ATOMIC(bridge.json)``."""
    for attempt in range(2):
        sidecar = _read_sidecar(sidecar_path)
        if sidecar is not None and _ping_tcp(sidecar["host"], int(sidecar["port"]), 1.0):
            return BridgeEndpoint(
                host=sidecar["host"],
                port=int(sidecar["port"]),
                pid=int(sidecar.get("pid", 0)),
                owned=False,
            )
        if attempt == 0:
            time.sleep(SIDECAR_RETRY_DELAY_SECONDS)
    return None


def _read_sidecar(path: Path) -> Optional[dict]:
    if not path.exists():
        return None
    try:
        text = path.read_text(encoding="utf-8")
    except OSError:
        return None
    try:
        data = json.loads(text)
    except (ValueError, json.JSONDecodeError):
        return None
    if not isinstance(data, dict) or "host" not in data or "port" not in data:
        return None
    return data


def _ping_tcp(host: str, port: int, timeout_s: float) -> bool:
    try:
        with socket.create_connection((host, port), timeout=timeout_s):
            return True
    except OSError:
        return False


def _resolve_bridge_script(
    repo_root: Path, override: Optional[str | os.PathLike[str]]
) -> Path:
    if override is not None:
        candidate = Path(override)
        if not candidate.is_absolute():
            candidate = (repo_root / candidate).resolve()
        if not candidate.is_file():
            raise FileNotFoundError(f"bridge script override not found: {candidate}")
        return candidate
    candidate = repo_root / "prereq-patterns" / "pglite" / "pglite_bridge.mjs"
    if not candidate.is_file():
        raise FileNotFoundError(
            f"unified bridge script not found at {candidate}; "
            "repo root mis-detected, or feature 012 not landed."
        )
    return candidate


def _resolve_node_executable(override: Optional[str]) -> str:
    if override:
        return override
    path_env = os.environ.get("PATH", "")
    sep = os.pathsep
    exts = (".exe", ".cmd", ".bat", "") if os.name == "nt" else ("",)
    for d in path_env.split(sep):
        if not d:
            continue
        for ext in exts:
            candidate = Path(d) / ("node" + ext)
            if candidate.is_file():
                return str(candidate)
    raise RuntimeError("node not found on PATH; install Node.js >= 20")


def _try_acquire_lock(lock_path: Path) -> Optional["portalocker.Lock"]:
    """Diagnostic helper: portalocker non-blocking exclusive acquire.

    NOT used by :func:`acquire_or_discover` — kept available so the
    test suite can exercise the cross-process file-lock primitive in
    isolation, and so future code that wants a Python-only mutex at a
    distinct path can reuse the same shape.
    """
    lock = portalocker.Lock(
        str(lock_path),
        mode="ab",
        timeout=0,
        flags=portalocker.LOCK_EX | portalocker.LOCK_NB,
    )
    try:
        lock.acquire()
        return lock
    except (portalocker.exceptions.LockException, portalocker.exceptions.AlreadyLocked, OSError):
        return None


__all__ = [
    "BridgeEndpoint",
    "BridgeRaceLost",
    "BridgeStartupTimeout",
    "HEARTBEAT_FRESHNESS_SECONDS",
    "LOCK_HELD_EXIT_CODE",
    "READY_TIMEOUT_DEFAULT_SECONDS",
    "acquire_or_discover",
    "request_force_shutdown",
]

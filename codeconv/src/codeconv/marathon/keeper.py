"""Per-run keeper lifecycle over ``codeconv.bridge_client`` (US3, T028–T032).

A thin lifecycle — NOT a re-ported ``PGliteSupervisor`` (D1/D5) — that owns the
per-run isolated store at ``<store_root>/pgdb``:

- :func:`start_keeper` — ``acquire_or_discover(repo_root=toolchain_repo_root(),
  data_dir=store_root/'pgdb')``: the bridge SCRIPT comes from the toolchain
  checkout (its fixed asset), while ``data_dir`` keeps the cluster off-repo and
  per-run (decoupled — the off-repo ``store_root`` never holds a bridge script).
  Fast-paths a live, heartbeat-fresh bridge or spawns one (the ``mkdir`` of
  ``<data_dir>.bridge.lock/`` is the mutex), registers this process as a
  consumer, and surfaces the sidecar endpoint as
  ``Endpoint(host, port, pid, data_dir)`` (FR-012).
- :func:`stop_keeper` — ``request_force_shutdown`` writes the non-destructive
  ``.shutdown`` marker; the bridge flushes and exits on its next tick (the
  sidecar is unlinked on graceful exit), so the next start needs no recovery
  (FR-013). Also disposes this process's engine and releases its writer lock.
- :func:`recover_keeper` — drop a possibly-dead engine and re-acquire: stale
  residue (dead pid, lingering sidecar/lock, stale heartbeat) is cleared
  automatically by ``acquire_or_discover``'s heartbeat-freshness + TCP checks
  and stale-lock retry — no manual deletion (FR-014). A **live** bridge child
  is fast-path consumed, never killed (FR-015).
- :func:`engine_for` — engine on the per-run store, auto-starting the keeper
  when none is live; a dead cached engine is dropped and re-acquired (T030).
- :func:`doctor` — read-only diagnostics (never spawns): endpoint
  reachability, active store, last-seq per store, open escalations, budget
  headroom (T032).

Single-writer: the per-run kernel-fd writer lock lives in
``store.repository`` (``ConcurrentWriter`` — refusal message distinct from
recoverable residue, FR-015/016); the keeper re-exports the taxonomy.
"""

from __future__ import annotations

import json
import time
from typing import Any, Optional

from sqlalchemy import text
from sqlalchemy.engine import Engine

from codeconv.bridge_client import (
    BridgeEndpoint,
    _ping_tcp,
    acquire_or_discover,
    request_force_shutdown,
)
from codeconv.db import engine as db_engine
from codeconv.marathon.env import resolve_env, toolchain_repo_root
from codeconv.marathon.models import Endpoint, MarathonEnv
from codeconv.marathon.store.repository import (
    ConcurrentWriter,
    IntegrityFailure,
    Repository,
    StoreUnavailable,
    _connect,
    release_writer_lock,
)

__all__ = [
    "ConcurrentWriter",
    "IntegrityFailure",
    "StoreUnavailable",
    "doctor",
    "engine_for",
    "recover_keeper",
    "start_keeper",
    "stop_keeper",
]


def _env(run_id: str, env: Optional[MarathonEnv]) -> MarathonEnv:
    return env if env is not None else resolve_env(run_id)


def start_keeper(run_id: str, *, env: Optional[MarathonEnv] = None) -> Endpoint:
    """Acquire (spawn or discover) the per-run bridge; publish its endpoint."""
    env = _env(run_id, env)
    data_dir = env.store_root / "pgdb"
    # repo_root = the toolchain checkout (bridge-script source); data_dir = the
    # off-repo per-run cluster (decoupled — store_root holds no bridge script).
    ep = acquire_or_discover(toolchain_repo_root(), data_dir=data_dir)
    return Endpoint(host=ep.host, port=ep.port, pid=ep.pid, data_dir=str(data_dir))


def stop_keeper(run_id: str, *, env: Optional[MarathonEnv] = None) -> None:
    """Graceful shutdown: flush + exit on the bridge's next tick (FR-013)."""
    env = _env(run_id, env)
    data_dir = env.store_root / "pgdb"
    if env.engine is not None:
        try:
            env.engine.dispose()
        except Exception:
            pass
        env.engine = None
    release_writer_lock(env)
    request_force_shutdown(env.store_root, data_dir=data_dir)
    # The bridge unlinks its sidecar on graceful exit; wait so the caller can
    # rely on "the next start needs no recovery" (best-effort, bounded).
    sidecar = data_dir / "bridge.json"
    deadline = time.time() + 30.0
    while time.time() < deadline and sidecar.is_file():
        time.sleep(0.25)


def recover_keeper(run_id: str, *, env: Optional[MarathonEnv] = None) -> Endpoint:
    """Clear stale residue automatically; never kill a live bridge (FR-014/015)."""
    env = _env(run_id, env)
    if env.engine is not None:
        try:
            env.engine.dispose()
        except Exception:
            pass
        env.engine = None
    return start_keeper(run_id, env=env)


def engine_for(run_id: str, *, env: Optional[MarathonEnv] = None) -> Engine:
    """Engine on the per-run store; auto-starts the keeper if none live.

    The recovery path (T030): a cached-but-dead engine fails the liveness
    ping, is dropped, and the re-acquisition runs the same automatic
    stale-residue recovery as :func:`recover_keeper`."""
    env = _env(run_id, env)
    if env.engine is not None:
        try:
            with env.engine.connect() as conn:
                conn.execute(text("SELECT 1"))
            return env.engine
        except Exception:
            try:
                env.engine.dispose()
            except Exception:
                pass
            env.engine = None
    return _connect(env)


def doctor(run_id: str, *, env: Optional[MarathonEnv] = None) -> dict[str, Any]:
    """Read-only diagnostics — never spawns a bridge (T032).

    Reports: endpoint + reachability, active store (primary|fallback),
    last-seq per store, open escalations, budget headroom."""
    env = _env(run_id, env)
    data_dir = env.store_root / "pgdb"
    sidecar = data_dir / "bridge.json"

    endpoint: Optional[dict[str, Any]] = None
    reachable = False
    if sidecar.is_file():
        try:
            raw = json.loads(sidecar.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            raw = None
        if raw is not None:
            endpoint = {
                "host": raw.get("host"),
                "port": raw.get("port"),
                "pid": raw.get("pid"),
                "heartbeat_at": raw.get("heartbeat_at"),
            }
            reachable = _ping_tcp(raw["host"], int(raw["port"]), 0.5)

    if reachable and env.engine is None:
        # Build an engine on the live sidecar WITHOUT acquire (no spawn, no
        # consumer registration churn) — diagnostics only.
        env.engine = db_engine.build_engine(
            BridgeEndpoint(
                host=endpoint["host"],
                port=int(endpoint["port"]),
                pid=int(endpoint["pid"] or 0),
                owned=False,
            ),
            application_name="codeconv-marathon",
        )

    repo = Repository(env, autostart=False)
    primary_seq: Optional[int] = None
    run_row = None
    open_escalations: list[dict[str, Any]] = []
    if reachable:
        try:
            with repo.engine().connect() as conn:
                primary_seq = int(
                    conn.execute(
                        text(
                            "SELECT COALESCE(MAX(sequence_no), 0) "
                            "FROM marathon.checkpoint WHERE run_id = :r"
                        ),
                        {"r": run_id},
                    ).scalar_one()
                )
            run_row = repo.get_run(run_id)
            open_escalations = [
                {"id": e.id, "kind": e.kind}
                for e in repo.list_escalations(run_id, open_only=True)
            ]
        except Exception:
            reachable = False  # answered the ping but not queries

    fallback_seq = repo._fallback_max_sequence(run_id)
    if run_row is None:
        payload = repo._read_mirror("run.json")
        if payload is not None and payload.get("id") == run_id:
            from codeconv.marathon.models import MarathonRun

            run_row = MarathonRun(**payload)
    if not reachable:
        open_escalations = [
            {"id": p.get("id"), "kind": p.get("kind")}
            for p in repo._read_mirror_dir("escalations")
            if p.get("run_id") == run_id and p.get("resolved_at") is None
        ]

    budget: dict[str, Any] = {"ceiling": None, "spent": 0, "remaining": None, "unit": None}
    if run_row is not None:
        budget = {
            "ceiling": run_row.budget_ceiling,
            "spent": run_row.budget_spent,
            "remaining": (
                run_row.budget_ceiling - run_row.budget_spent
                if run_row.budget_ceiling is not None
                else None
            ),
            "unit": run_row.budget_unit,
        }

    return {
        "run": run_id,
        "store_root": str(env.store_root),
        "endpoint": endpoint,
        "endpoint_reachable": reachable,
        "active_store": "primary" if reachable else "fallback",
        "last_seq": {"primary": primary_seq, "fallback": fallback_seq},
        "open_escalations": open_escalations,
        "budget": budget,
    }

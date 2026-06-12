"""US3 keeper lifecycle — endpoint, graceful stop, stale-residue recovery (T026).

Independent Test (spec US3): start (endpoint published); kill abruptly; the
next op recovers with no manual deletion (FR-012/013/014, SC-005). Doctor
reports the endpoint + active store read-only (T032).
"""

from __future__ import annotations

import socket

from .conftest import kill_marathon_bridge, needs_bridge

RUN_ID = "us3-keeper"


def _tcp_alive(host: str, port: int) -> bool:
    try:
        with socket.create_connection((host, port), timeout=0.5):
            return True
    except OSError:
        return False


@needs_bridge
def test_keeper_start_stop_kill_recover(marathon_store):
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.keeper import doctor, recover_keeper, start_keeper, stop_keeper
    from codeconv.marathon.models import MarathonRun
    from codeconv.marathon.store import Repository

    env = resolve_env(RUN_ID, data_dir=marathon_store)
    sidecar = marathon_store / "pgdb" / "bridge.json"

    # FR-012: start publishes a reachable endpoint (sidecar under the store root).
    ep = start_keeper(RUN_ID, env=env)
    assert sidecar.is_file()
    assert _tcp_alive(ep.host, ep.port)

    # Persist something so recovery can prove durability.
    repo = Repository(env)
    repo.upsert_run(MarathonRun(id=RUN_ID, title="us3"))

    # T032 doctor: read-only health on the live store.
    report = doctor(RUN_ID, env=env)
    assert report["endpoint_reachable"] is True
    assert report["active_store"] == "primary"
    assert report["endpoint"]["pid"] == ep.pid
    assert report["last_seq"] == {"primary": 0, "fallback": 0}

    # FR-013: graceful stop flushes; the sidecar is unlinked; the next start
    # needs no recovery.
    stop_keeper(RUN_ID, env=env)
    assert not sidecar.is_file()
    ep2 = start_keeper(RUN_ID, env=env)
    assert _tcp_alive(ep2.host, ep2.port)

    # FR-014: abrupt kill leaves stale residue (sidecar/lock); the next op
    # auto-recovers — no manual deletion of anything.
    kill_marathon_bridge(marathon_store)
    env2 = resolve_env(RUN_ID, data_dir=marathon_store)
    ep3 = recover_keeper(RUN_ID, env=env2)
    assert ep3.pid != ep2.pid
    assert _tcp_alive(ep3.host, ep3.port)
    # Durable rows survived the crash.
    assert Repository(env2).get_run(RUN_ID).title == "us3"

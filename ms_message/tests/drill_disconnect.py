"""T024 — the SC-004 disconnect drill (standalone gate; also run from test/run_all_tests.sh).

Scenario (quickstart.md US2, contract guarantees 1–4 + SC-005 bounded waits):

1. Recipient OFFLINE. Originator journals N=1,000 messages for it (WAL first).
2. The ORIGINATOR RESTARTS (durability proof: a fresh process serves from replay).
3. The recipient appears: signal → resumable fetch → ALL 1,000 messages,
   exactly once, in order; gaps would be named gap_events (none expected).
4. The recipient runs AGAIN (its own restart): the durable delivery position
   dedups everything — ZERO re-observations (exactly-once floor, R7).

Every wait is bounded (--timeout / subprocess timeouts) — a hang is a FAIL,
never a stall. Prints an explicit verdict per criterion; exit 0 iff all PASS.

Usage: ``ms_message/.venv/Scripts/python ms_message/tests/drill_disconnect.py [N]``
"""

from __future__ import annotations

import socket
import subprocess
import sys
import tempfile
import time
from pathlib import Path

N = int(sys.argv[1]) if len(sys.argv) > 1 else 1000
STEP_TIMEOUT_S = 120  # generous hard bound per phase; the drill lands in seconds
REPO = Path(__file__).resolve().parents[2]
PY = sys.executable


def _free_port() -> int:
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    port = s.getsockname()[1]
    s.close()
    return port


def _run(args: list, timeout: float = STEP_TIMEOUT_S, **kw) -> subprocess.CompletedProcess:
    return subprocess.run([PY, "-m", "ms_message", *args], capture_output=True, text=True,
                          timeout=timeout, cwd=str(REPO), **kw)


def main() -> int:
    run_id = str(int(time.time()))
    alice, bob = f"alice{run_id}", f"bob{run_id}"
    mailbox = f"news{run_id}"
    port = _free_port()
    data_root = Path(tempfile.mkdtemp(prefix="msmsg-drill-"))
    verdicts: list = []

    def verdict(name: str, ok: bool, detail: str = "") -> None:
        verdicts.append(ok)
        print(f"  {name:<58} {'PASS' if ok else 'FAIL'}{(' — ' + detail) if detail else ''}",
              flush=True)

    print(f"SC-004 disconnect drill: N={N}, port={port}, data={data_root}", flush=True)

    # 1 — journal while the recipient is offline (WAL durable before ack).
    p = _run(["originator", "--station", alice, "--mailbox", mailbox, "--to", bob,
              "--count", str(N), "--data-root", str(data_root)])
    verdict("journal N while recipient offline (guarantee 1/4)",
            p.returncode == 0 and f"journalled {N}" in p.stdout,
            p.stdout.strip().splitlines()[-1] if p.stdout.strip() else p.stderr.strip()[:120])

    # 2 — RESTART: a fresh originator process serves purely from WAL replay.
    server = subprocess.Popen(
        [PY, "-m", "ms_message", "originator", "--station", alice, "--mailbox", mailbox,
         "--listen", f"127.0.0.1:{port}", "--serve-for", "90",
         "--data-root", str(data_root)],
        cwd=str(REPO), stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    time.sleep(1.5)  # listener bind
    try:
        # 3 — the recipient appears and catches up.
        r1 = _run(["recipient", "--station", bob, "--from", f"127.0.0.1:{port}",
                   "--once", "--timeout", "30", "--data-root", str(data_root)])
        lines = r1.stdout.splitlines()
        expected = [f"m{i:06d}" for i in range(1, N + 1)]
        verdict(f"all {N} delivered after originator restart", len(lines) == N,
                f"got {len(lines)} lines (rc={r1.returncode})")
        verdict("in per-sender order, no holes (guarantee 3)", lines == expected,
                "" if lines == expected else f"first divergence at {next((i for i, (a, b) in enumerate(zip(lines, expected)) if a != b), min(len(lines), len(expected)))}")
        verdict("no gap events raised", "GAP" not in r1.stderr, r1.stderr.strip()[:120])

        # 4 — recipient restart: exactly-once means ZERO re-observations.
        r2 = _run(["recipient", "--station", bob, "--from", f"127.0.0.1:{port}",
                   "--once", "--timeout", "30", "--data-root", str(data_root)])
        verdict("recipient restart re-observes nothing (guarantee 2, R7)",
                r2.returncode == 0 and r2.stdout.strip() == "",
                f"{len(r2.stdout.splitlines())} re-observed" if r2.stdout.strip() else "")
    finally:
        server.terminate()
        try:
            server.wait(timeout=10)
        except subprocess.TimeoutExpired:
            server.kill()

    ok = all(verdicts)
    print(f"=> {'PASS' if ok else 'FAIL'} ({sum(verdicts)}/{len(verdicts)} criteria)", flush=True)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())

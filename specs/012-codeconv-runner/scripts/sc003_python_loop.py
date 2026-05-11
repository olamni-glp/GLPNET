"""SC-003 Python side: 100 sequential transactions against the unified bridge.

Usage::

    python sc003_python_loop.py --port <BRIDGE_PORT> --cycles 100

The script connects via psycopg, creates a scratch table in the
``codeconv`` schema (assumed pre-migrated by ``codeconv migrate``),
and runs ``--cycles`` insert-+-commit cycles. It MUST observe zero
``lost synchronization with server`` errors AND zero
``DuplicatePreparedStatement`` errors (FR-027).

Exit codes::

    0  — all cycles succeeded
    1  — at least one cycle failed (specific error printed)
"""

from __future__ import annotations

import argparse
import sys
import time
import uuid


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, required=True)
    ap.add_argument("--cycles", type=int, default=100)
    args = ap.parse_args()

    try:
        import psycopg
    except ImportError:
        print("psycopg not available; install via `pip install 'psycopg[binary]'`", file=sys.stderr)
        return 1

    conn_str = (
        f"host={args.host} port={args.port} dbname=postgres user=postgres "
        f"password=postgres application_name=sc003-python sslmode=disable"
    )

    # Each cycle: BEGIN, INSERT, COMMIT — explicit transaction per FR-027.
    errors: list[str] = []
    t0 = time.monotonic()
    with psycopg.connect(conn_str, prepare_threshold=None, autocommit=False) as conn:
        with conn.cursor() as cur:
            cur.execute(
                """
                CREATE TABLE IF NOT EXISTS codeconv._sc003_python (
                    i INT PRIMARY KEY,
                    v TEXT NOT NULL
                )
                """
            )
            cur.execute("TRUNCATE codeconv._sc003_python")
            conn.commit()

        for i in range(args.cycles):
            try:
                with conn.cursor() as cur:
                    cur.execute(
                        "INSERT INTO codeconv._sc003_python (i, v) VALUES (%s, %s)",
                        (i, uuid.uuid4().hex),
                    )
                    cur.execute(
                        "SELECT COUNT(*) FROM codeconv._sc003_python WHERE i = %s",
                        (i,),
                    )
                    got = cur.fetchone()
                    assert got is not None and got[0] == 1
                conn.commit()
            except Exception as exc:
                msg = repr(exc)
                if "lost synchronization" in msg.lower():
                    errors.append(f"cycle {i}: LOST SYNC — {msg}")
                elif "duplicate" in msg.lower() and "prepared" in msg.lower():
                    errors.append(f"cycle {i}: DUPLICATE PREPARED STATEMENT — {msg}")
                else:
                    errors.append(f"cycle {i}: {msg}")
                conn.rollback()

    elapsed = time.monotonic() - t0
    if errors:
        print(f"sc003-python: {len(errors)} / {args.cycles} failed (elapsed {elapsed:.2f}s)")
        for e in errors[:5]:
            print(f"  - {e}")
        if len(errors) > 5:
            print(f"  - ... {len(errors) - 5} more")
        return 1
    print(
        f"sc003-python: {args.cycles}/{args.cycles} cycles OK in {elapsed:.2f}s "
        f"(zero lost-sync, zero duplicate-prepared)"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())

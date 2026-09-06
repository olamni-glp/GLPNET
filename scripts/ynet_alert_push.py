# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""
M6 clause 4 — the CLIENT-PUSHED async /btw alert (feature 093; engineer ruling R-S5-03).

WHAT WAS MISSING
    `scripts/ynet_alerts_hook.py` is a UserPromptSubmit hook. It fires only when the ENGINEER next
    speaks, so a lane doing six hours of deep work is alerted six hours late. That is agent-POLLED
    delivery wearing /btw's clothes, and this lane published it as clause 4 PARTIAL on 2026-09-06.

WHAT THIS IS
    A watcher meant to be run under the harness's background-monitor facility. It is a separate OS
    process: it does not live in the agent's turn, does not need the agent to speak, and cannot
    preempt a tool call. Every line it prints on stdout is delivered into the agent's session as an
    asynchronous notification. So:

        code client writes an alert file  ->  this process notices (<= --interval seconds)
                                          ->  one stdout line
                                          ->  the harness notifies the agent
                                          ->  THE AGENT DECIDES when to act

    Delivery is immediate; handling is scheduled by the agent. That is the /btw semantic, and the
    push half of it is now real rather than restated.

HONEST ABOUT WHAT IT IS NOT
    This polls a directory. The poll is deliberate — there is no inotifywait on SHIRAS (measured
    2026-09-06) and adding a build dependency to the alert path would make the alert path fragile.
    What matters for clause 4 is not that nothing polls; it is that THE AGENT does not poll. The
    latency this removes is turn-boundary latency (unbounded, engineer-driven) and it replaces it
    with --interval (bounded, 1s by default).

THE TWO NON-NEGOTIABLES, inherited from the hook (093 FR-017)
    1. FAIL SILENT PER FILE. A malformed or half-written alert is skipped, never fatal. The watch
       outliving one bad file matters more than reporting it.
    2. CAP THE OUTPUT. A monitor that floods is stopped by the harness, which would leave the lane
       with no push channel at all. A burst larger than --burst-cap collapses to ONE summary line.

USAGE
    python3 scripts/ynet_alert_push.py --lane shiras-glpnet
    (armed via the harness Monitor tool with persistent: true)
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import time

DEFAULT_INTERVAL = 1.0
DEFAULT_BURST_CAP = 5
PREVIEW_CHARS = 160


def _field(alert: dict, *names):
    """First present value among several spellings of one field.

    The C# client writes PascalCase; the Python records are snake_case. Reading only one spelling
    is why the sibling hook could exit 0 and surface nothing while alerts sat in the spool.
    """
    for name in names:
        if name in alert and alert[name] not in (None, ""):
            return alert[name]
    return ""


def _read(path: str):
    """Parse one alert file, or None. Never raises — see non-negotiable 1."""
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
    except (OSError, ValueError, UnicodeDecodeError):
        return None
    if not isinstance(data, dict):
        return None
    return data


def _identity(alert: dict, path: str) -> str:
    return str(_field(alert, "MessageId", "message_id") or os.path.basename(path))


def _acknowledged(alert: dict) -> bool:
    value = _field(alert, "Acknowledged", "acknowledged")
    return value is True or str(value).lower() == "true"


def _line(alert: dict, path: str) -> str:
    ident = _identity(alert, path)
    sender = _field(alert, "FromLane", "from_lane", "From", "Origin", "origin")
    signal = _field(alert, "Signal", "signal")
    body = str(_field(alert, "Body", "body", "Preview", "preview")).replace("\n", " ").strip()
    if len(body) > PREVIEW_CHARS:
        body = body[: PREVIEW_CHARS - 1] + "…"
    head = f"[YNET /btw] {ident}"
    if sender:
        head += f" from={sender}"
    if signal:
        head += f" signal={signal}"
    return f"{head} :: {body}" if body else head


def _scan(alerts_dir: str):
    """Every unacknowledged alert currently on disk, as {id: (line, path)}."""
    found = {}
    try:
        names = sorted(os.listdir(alerts_dir))
    except OSError:
        return found
    for name in names:
        if not name.endswith(".json"):
            continue
        path = os.path.join(alerts_dir, name)
        alert = _read(path)
        if alert is None or _acknowledged(alert):
            continue
        found[_identity(alert, path)] = (_line(alert, path), path)
    return found


def main() -> int:
    parser = argparse.ArgumentParser(description="Push YNET alerts into the agent's session.")
    parser.add_argument("--lane", required=True)
    parser.add_argument("--alerts", default=None)
    parser.add_argument("--interval", type=float, default=DEFAULT_INTERVAL)
    parser.add_argument("--burst-cap", type=int, default=DEFAULT_BURST_CAP)
    parser.add_argument(
        "--announce-pending",
        action="store_true",
        help="Emit one summary line for alerts already on disk at start. Off by default so "
             "arming the watcher does not replay a backlog the agent has already been shown.",
    )
    args = parser.parse_args()

    alerts_dir = args.alerts or os.path.join(".specify", "ynet", args.lane, "alerts")

    seen = set(_scan(alerts_dir))
    if args.announce_pending and seen:
        print(f"[YNET /btw] {len(seen)} alert(s) already pending for {args.lane} at watch start",
              flush=True)

    while True:
        time.sleep(args.interval)
        current = _scan(alerts_dir)
        fresh = [current[key][0] for key in current if key not in seen]
        # An id that vanished (acked, or the spool was drained) must be forgettable, or a
        # resurrected message -- which this fleet has measured happening on receiver restart --
        # would never be re-announced.
        seen = set(current)
        if not fresh:
            continue
        if len(fresh) > args.burst_cap:
            print(f"[YNET /btw] {len(fresh)} new alerts for {args.lane} "
                  f"(burst collapsed; run: ynet-client alerts --lane {args.lane})", flush=True)
            continue
        for line in fresh:
            print(line, flush=True)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        sys.exit(0)

# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""
M6 between-turn alert hook (feature 093, FR-017; engineer ruling Q-ARI093-01).

WHAT IT IS
    A UserPromptSubmit hook. It runs BETWEEN turns and prints any pending YNET alerts the
    code-based client has dropped, so the agent SEES them at a boundary and decides whether to
    act now or later. That is the /btw semantic in the harness: the agent is offered work, never
    interrupted mid-tool-call.

WHY A HOOK AND NOT A NOTIFICATION
    The client cannot reach the agent's process at all - by construction, it holds no pid, no
    pipe and no handle (093 FR-008). So something on the agent's own side has to look. A
    UserPromptSubmit hook is the only place that looks at a genuine turn boundary.

THE TWO NON-NEGOTIABLES (FR-017)
    1. FAIL SILENT. Any error at all exits 0 with no output. A broken hook must never be able to
       disrupt the prompt path - the hook is a convenience, the prompt is the product.
    2. CAP THE OUTPUT. A flooded alert directory must not flood the agent's context.

INSTALL (per lane, opt-in)
    .claude/settings.json:
      { "hooks": { "UserPromptSubmit": [ { "hooks": [ {
          "type": "command",
          "command": "python scripts/ynet_alerts_hook.py --lane <lane>" } ] } ] } }
"""

from __future__ import annotations

import argparse
import json
import os
import sys
from datetime import datetime, timezone

MAX_ALERTS_SHOWN = 10
MAX_PREVIEW_CHARS = 120


def _parse_iso(value: str) -> datetime | None:
    """Parse an ISO-8601 stamp, tolerating a trailing Z and offsets."""
    try:
        text = value.strip()
        if text.endswith("Z"):
            text = text[:-1] + "+00:00"
        parsed = datetime.fromisoformat(text)
        if parsed.tzinfo is None:
            parsed = parsed.replace(tzinfo=timezone.utc)
        return parsed
    except (ValueError, AttributeError):
        return None


def _pending(alert_dir: str, now: datetime) -> list[dict]:
    """Alerts that are neither acknowledged nor past their staleness horizon."""
    out: list[dict] = []
    try:
        names = sorted(os.listdir(alert_dir))
    except OSError:
        return out

    for name in names:
        if not name.endswith(".json"):
            continue
        try:
            with open(os.path.join(alert_dir, name), encoding="utf-8") as handle:
                alert = json.load(handle)
        except (OSError, json.JSONDecodeError):
            # One malformed or racing file must never blind the lane to the others.
            continue
        if not isinstance(alert, dict) or alert.get("acknowledged"):
            continue
        stale_after = _parse_iso(str(alert.get("stale_after_utc", "")))
        if stale_after is not None and now >= stale_after:
            continue
        out.append(alert)

    out.sort(key=lambda a: str(a.get("arrived_utc", "")))
    return out


def main() -> int:
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--lane")
    parser.add_argument("--alerts")
    args, _unknown = parser.parse_known_args()

    alert_dir = args.alerts
    if not alert_dir:
        if not args.lane:
            return 0  # nothing to look at; silence is correct
        alert_dir = os.path.join(".specify", "ynet", args.lane, "alerts")

    if not os.path.isdir(alert_dir):
        return 0

    now = datetime.now(timezone.utc)
    pending = _pending(alert_dir, now)
    if not pending:
        return 0

    shown = pending[:MAX_ALERTS_SHOWN]
    lines = [
        f"[YNET] {len(pending)} pending alert(s) for lane "
        f"{args.lane or '?'} — delivered by the code-based M6 client, not by an agent.",
        "Handle now or later; acknowledge with: ynet-client ack <message_id> --lane <lane>",
    ]
    for alert in shown:
        preview = str(alert.get("preview", ""))[:MAX_PREVIEW_CHARS]
        lines.append(
            f"  - {alert.get('message_id', '?')} from={alert.get('sender', '?')} "
            f"signal={alert.get('signal', '?')} arrived={alert.get('arrived_utc', '?')}"
        )
        if preview:
            lines.append(f"      {preview}")
    if len(pending) > len(shown):
        lines.append(f"  ... and {len(pending) - len(shown)} more (output capped).")

    print("\n".join(lines))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception:  # noqa: BLE001 - fail-silent is the requirement (FR-017)
        sys.exit(0)

#!/usr/bin/env python3
"""Lean 4 bounded tactic-loop harness — US3 spike (T014/T015, 027 / R13-R14).

Deterministic LOCAL scaffolding for the bounded Claude-over-MCP tactic loop
described in `../../reconciliation/LEAN-TACTIC-LOOP.md`. It does NOT contain a
model — it drives the *real* Lean 4 kernel and accounts the attempt budget:

  * the tactic DRIVER is **Claude** via the Agent-tool seam (this session) — it
    proposes a candidate proof body; there is NO external LM API (FR-073).
  * the kernel ORACLE is the real `lean` executable (Lean 4.30.0, tool-versions.txt).
    `lean`'s "unsolved goals" / error output IS the kernel feedback the driver
    reflects on — kernel-equivalent to Lean-LSP-MCP (which wraps the same kernel),
    but self-contained + reproducible for `run.sh` (an MCP session is not).
  * the BUDGET (start 20, FR-031/R13) is enforced across invocations via durable
    JSON state; on exhaustion the harness refuses further attempts → the `sorry`
    stays isolated and the outcome is `sorry-isolated` (owner-escalation, FR-035).

Each `attempt` splices the candidate in place of the single `sorry` in a WORKING
COPY (the committed `SRSWPreservation.lean` is never mutated), runs `lean`, and
classifies: proved | still-sorry | tactic-error (with the kernel feedback).

Subcommands:
  reset   [--budget N]            start a fresh run (attempts=0, outcome=open)
  attempt --proof <file>          one budgeted attempt; prints verdict + feedback
  verify  --proof <file>          un-budgeted single check (run.sh uses this to
                                  deterministically reproduce a found proof)
  status                          print the durable state (attempts / outcome)
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path

# The proof placeholder is the single standalone `sorry` TACTIC line (indented,
# nothing else on the line). Prose mentions of "sorry" in the docstring/comments
# are inline (backticked / mid-sentence) and never match this.
SORRY_TACTIC = re.compile(r"(?m)^[ \t]+sorry[ \t]*$")

HERE = Path(__file__).resolve().parent
SOURCE = HERE / "SRSWPreservation.lean"
DEFAULT_BUDGET = 20
STATE = Path(os.environ.get("LEAN_SPIKE_WORK", str(Path(tempfile.gettempdir()) / "lean_spike_t015"))) / "state.json"


def _load() -> dict:
    if STATE.is_file():
        return json.loads(STATE.read_text(encoding="utf-8"))
    return {"budget": DEFAULT_BUDGET, "attempts": 0, "outcome": "open", "history": []}


def _save(s: dict) -> None:
    STATE.parent.mkdir(parents=True, exist_ok=True)
    STATE.write_text(json.dumps(s, indent=2), encoding="utf-8")


def _splice(candidate: str) -> str:
    """Replace the single standalone `sorry` TACTIC line with the candidate tactics
    (indented to match), leaving docstring/comment mentions of "sorry" untouched."""
    src = SOURCE.read_text(encoding="utf-8")
    matches = SORRY_TACTIC.findall(src)
    if len(matches) != 1:
        raise SystemExit(f"expected exactly one standalone `sorry` tactic line in {SOURCE.name}, found {len(matches)}")
    indent = re.match(r"[ \t]*", matches[0]).group(0)
    body = "\n".join((indent + ln if ln.strip() else ln) for ln in candidate.strip("\n").splitlines())
    return SORRY_TACTIC.sub(lambda _m: body, src, count=1)


def _run_lean(text: str) -> tuple[int, str]:
    work = Path(tempfile.mkdtemp(prefix="lean_attempt_"))
    f = work / "SRSWPreservation.lean"
    f.write_text(text, encoding="utf-8")
    env = dict(os.environ)
    env["PATH"] = f"{Path.home()/'.elan'/'bin'}:" + env.get("PATH", "")
    proc = subprocess.run(
        ["lean", str(f)], capture_output=True, text=True, env=env, cwd=str(work)
    )
    return proc.returncode, (proc.stdout + proc.stderr).strip()


def _classify(rc: int, out: str) -> str:
    r"""proved = lean exit 0, no error, AND no `sorry` anywhere in the output. The
    only way "sorry" reaches lean's output is the `declaration uses `sorry`` warning
    (quoting style varies: straight vs backtick), so any "sorry" in the diagnostics
    means the proof is NOT closed. error always wins (it includes "error: unsolved
    goals", the kernel feedback the driver reflects on)."""
    low = out.lower()
    if "error" in low:
        return "tactic-error"
    if "sorry" in low:
        return "still-sorry"
    if rc == 0:
        return "proved"
    return "tactic-error"


def cmd_reset(args: argparse.Namespace) -> int:
    _save({"budget": args.budget, "attempts": 0, "outcome": "open", "history": []})
    print(f"reset: budget={args.budget}, attempts=0, outcome=open")
    return 0


def cmd_attempt(args: argparse.Namespace) -> int:
    s = _load()
    if s["outcome"] == "proved":
        print("already PROVED — no further attempts needed."); return 0
    if s["attempts"] >= s["budget"]:
        s["outcome"] = "sorry-isolated"
        _save(s)
        print(f"BUDGET EXHAUSTED ({s['attempts']}/{s['budget']}). outcome=sorry-isolated → ESCALATE.")
        return 2
    candidate = Path(args.proof).read_text(encoding="utf-8")
    rc, out = _run_lean(_splice(candidate))
    verdict = _classify(rc, out)
    s["attempts"] += 1
    s["history"].append({"attempt": s["attempts"], "verdict": verdict})
    if verdict == "proved":
        s["outcome"] = "proved"
    elif s["attempts"] >= s["budget"]:
        s["outcome"] = "sorry-isolated"
    _save(s)
    print(f"=== attempt {s['attempts']}/{s['budget']} → {verdict} ===")
    print(out if out else "(lean: no diagnostics)")
    if verdict == "proved":
        print(f"\nPROVED at attempt {s['attempts']} (lean exit 0, no sorry, no error).")
        return 0
    if s["outcome"] == "sorry-isolated":
        print(f"\nBUDGET EXHAUSTED → outcome=sorry-isolated → ESCALATE to owner (FR-035).")
        return 2
    return 1


def cmd_verify(args: argparse.Namespace) -> int:
    """Un-budgeted deterministic check of a candidate proof (run.sh reproduction)."""
    rc, out = _run_lean(_splice(Path(args.proof).read_text(encoding="utf-8")))
    verdict = _classify(rc, out)
    print(f"verify → {verdict}")
    print(out if out else "(lean: no diagnostics)")
    return 0 if verdict == "proved" else 1


def cmd_status(args: argparse.Namespace) -> int:
    print(json.dumps(_load(), indent=2)); return 0


def main(argv: list[str]) -> int:
    p = argparse.ArgumentParser(description="Lean bounded tactic-loop harness (T014/T015)")
    sub = p.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("reset"); r.add_argument("--budget", type=int, default=DEFAULT_BUDGET); r.set_defaults(fn=cmd_reset)
    a = sub.add_parser("attempt"); a.add_argument("--proof", required=True); a.set_defaults(fn=cmd_attempt)
    v = sub.add_parser("verify"); v.add_argument("--proof", required=True); v.set_defaults(fn=cmd_verify)
    st = sub.add_parser("status"); st.set_defaults(fn=cmd_status)
    args = p.parse_args(argv)
    return args.fn(args)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

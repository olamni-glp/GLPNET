"""Bounded remote test-control over the glp-quick mutual-pin link (feature 049, US5 / FR-017..019).

A control **agent** runs on the controlled host (announces reserved id ``ctl``); a **driver** runs on
the controlling host (sends to ``ctl``, prints the reply). Both connect to the same glp-quick server
and ride the existing mesh routing + ``GlpMessage`` envelope — no transport or server change.

SAFETY (FR-018): the agent dispatches ONLY the fixed whitelist below — ``ping``, ``status``,
``mesh_selftest``, ``echo``. There is **no** arbitrary shell / exec / eval / file-path surface: every
branch is a declared, bounded test operation. An unknown command returns
``{"ok": false, "error": "unsupported"}`` and executes nothing. The mutual-pin QUIC link is the sole
trust boundary — only holders of the shared cert can reach the agent.

Run:
  agent (on the controlled host, e.g. gavri):
    python -m glp_quick.control agent --addr 192.168.0.108 --port 8443 --cert .\\glpquick-cert
  driver (on the controlling host, e.g. Olamnit):
    python -m glp_quick.control send --addr 192.168.0.108 --port 8443 --cert .\\glpquick-cert --cmd ping
"""
from __future__ import annotations

import argparse
import json
import os
import socket
import time
import uuid
from pathlib import Path
from typing import Optional

from glp_quick.repl_link import GlpMessage
from glp_quick.stacks.csharp import CSharpStackAdapter

CTL_ID = "ctl"
DRIVER_ID = "drv"
AGENT_VERSION = "1.0"

#: The FIXED command whitelist (FR-018). Nothing outside this set executes.
WHITELIST = ("ping", "status", "mesh_selftest", "echo")


class ControlAgent:
    """Dispatches bounded control commands. Holds only the cert dir (for ``mesh_selftest``).

    ``handle`` is pure w.r.t. the host except for the declared operations; it never runs a shell,
    evaluates a string, or touches an arbitrary path (FR-018)."""

    def __init__(self, cert_dir: Path) -> None:
        self.cert_dir = cert_dir
        self.started = time.time()

    def handle(self, cmd: Optional[str], args: dict) -> dict:
        if cmd not in WHITELIST:
            return {"ok": False, "cmd": cmd, "error": "unsupported"}
        if cmd == "ping":
            return {"ok": True, "cmd": "ping",
                    "result": {"host": socket.gethostname(), "agent": AGENT_VERSION, "t": time.time()}}
        if cmd == "echo":
            return {"ok": True, "cmd": "echo", "result": {"text": str(args.get("text", ""))}}
        if cmd == "status":
            return {"ok": True, "cmd": "status",
                    "result": {"host": socket.gethostname(), "agent_id": CTL_ID,
                               "version": AGENT_VERSION, "uptime_s": round(time.time() - self.started, 1)}}
        if cmd == "mesh_selftest":
            # Bounded: run the shipped local mesh self-test on THIS host, per-criterion pass/fail.
            from glp_quick.demo import run_demo
            n = max(3, min(int(args.get("clients", 3)), 6))
            rep = run_demo("127.0.0.1", None, self.cert_dir, stack="csharp", clients=n)
            return {"ok": bool(rep.ok), "cmd": "mesh_selftest",
                    "result": {"clients": n, "criteria": dict(rep.results)}}
        return {"ok": False, "cmd": cmd, "error": "unsupported"}  # unreachable (whitelist-guarded)


def run_agent(addr: str, port: int, cert_dir: Path, agent_id: str = CTL_ID,
              idle_timeout: Optional[float] = None) -> None:
    """Connect as ``agent_id``, announce, then serve control requests until idle timeout / EOF."""
    ad = CSharpStackAdapter()
    h = ad.start_client(addr, port, cert_dir, "csharp")
    h.send(GlpMessage(sender=agent_id, to="server", payload="ctl-agent-up"))
    print(f"[ctl-agent] up as '{agent_id}' on {addr}:{port} (whitelist: {', '.join(WHITELIST)})", flush=True)
    agent = ControlAgent(cert_dir)
    try:
        while True:
            m = h.recv(timeout=idle_timeout if idle_timeout else 3600)
            if m is None:
                if idle_timeout:
                    print("[ctl-agent] idle timeout — exiting", flush=True)
                    break
                continue
            try:
                req = json.loads(m.payload)
                cmd, cargs = req.get("cmd"), req.get("args", {}) or {}
            except Exception:
                reply, cmd = {"ok": False, "error": "bad_request"}, None
            else:
                reply = agent.handle(cmd, cargs)
            h.send(GlpMessage(sender=agent_id, to=m.sender, payload=json.dumps(reply)))
            print(f"[ctl-agent] {cmd!r} from {m.sender} -> ok={reply.get('ok')}", flush=True)
    finally:
        ad.stop(h)
        print("[ctl-agent] stopped", flush=True)


def send_command(addr: str, port: int, cert_dir: Path, cmd: str, args: Optional[dict] = None,
                 driver_id: Optional[str] = None, timeout: float = 30.0) -> dict:
    """Drive one control command to the ``ctl`` agent and return its reply dict.

    A UNIQUE driver id per call (default) avoids the mesh dup-id incumbent-route rule: a fixed id
    reused across successive connections would route the reply to the previous, now-dead link (the
    server keeps first-come's route until it processes the drop), so a fresh reply-addressable id
    per call is required."""
    if driver_id is None:
        driver_id = f"{DRIVER_ID}-{os.getpid():x}-{uuid.uuid4().hex[:6]}"
    ad = CSharpStackAdapter()
    h = ad.start_client(addr, port, cert_dir, "csharp")
    try:
        try:
            h.send(GlpMessage(sender=driver_id, to="server", payload="ctl-driver-up"))
            time.sleep(1.0)  # let the server register the driver id for the reply route
            h.send(GlpMessage(sender=driver_id, to=CTL_ID,
                              payload=json.dumps({"cmd": cmd, "args": args or {}})))
        except OSError:
            # The link dropped before/at send — typically the server bounced us at capacity
            # (over_capacity) right after link. Surface it, never crash the driver.
            return {"ok": False, "error": "link_dropped_before_send"}
        m = h.recv(timeout=timeout)
        if m is None:
            return {"ok": False, "error": "no_reply"}
        try:
            return json.loads(m.payload)
        except Exception:
            return {"ok": False, "error": "bad_reply", "raw": m.payload}
    finally:
        ad.stop(h)


def _main(argv: Optional[list] = None) -> int:
    p = argparse.ArgumentParser(prog="python -m glp_quick.control", description=__doc__)
    sub = p.add_subparsers(dest="role", required=True)

    a = sub.add_parser("agent", help="run the control agent on the controlled host")
    a.add_argument("--addr", required=True)
    a.add_argument("--port", type=int, default=8443)
    a.add_argument("--cert", type=Path, required=True)
    a.add_argument("--id", default=CTL_ID)
    a.add_argument("--idle-timeout", type=float, default=None)

    s = sub.add_parser("send", help="drive one control command from the controlling host")
    s.add_argument("--addr", required=True)
    s.add_argument("--port", type=int, default=8443)
    s.add_argument("--cert", type=Path, required=True)
    s.add_argument("--cmd", required=True)
    s.add_argument("--text", default="")  # for echo
    s.add_argument("--clients", type=int, default=3)  # for mesh_selftest
    s.add_argument("--timeout", type=float, default=30.0)

    ns = p.parse_args(argv)
    if ns.role == "agent":
        run_agent(ns.addr, ns.port, ns.cert, agent_id=ns.id, idle_timeout=ns.idle_timeout)
        return 0
    args = {"text": ns.text, "clients": ns.clients, "cert": str(ns.cert)}
    reply = send_command(ns.addr, ns.port, ns.cert, ns.cmd, args=args, timeout=ns.timeout)
    print(json.dumps(reply, indent=2))
    return 0 if reply.get("ok") else 1


if __name__ == "__main__":
    raise SystemExit(_main())

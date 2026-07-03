"""GLP-message envelope (L5) + mesh routing + the GLP-REPL ↔ link bridge (FR-008/008b, FR-018).

The wire/message contract (``contracts/wire-contract.md`` §GLP-message envelope) layers a small L5
envelope on top of spec 025's reliability sublayer (L4: framing version+CRC32+fragment, seq/dedup,
reorder, epoch/fencing, backpressure window). **025 owns reliability; this module owns only the
envelope shape, the ground-relay discipline, and the ``to``/``broadcast`` routing decision** — it
does not re-implement sequencing or dedup (FR-018, constitution VIII).

Envelope (verbatim from the contract)::

    { msg_id, from, to, seq, payload }
      msg_id  : unique per message (dedup key in concert with 025 seq)
      from    : endpoint_id of the sending GLP REPL endpoint
      to      : endpoint_id | "broadcast"
      seq     : 025 per-link sequence number
      payload : a GROUND GLP term (025 ground-relay — no _w / _r placeholders cross the wire)

The bridge that actually spawns/pumps a GLP REPL process (``out/csharp/glp_repl`` default) onto a
``stacks.base.Handle`` is **behaviour** and lands in US1 (T019) / US2 mesh (T027); this module is the
shared contract those build on (FR-017 skeleton-before-behaviour).
"""

from __future__ import annotations

import json
import os
import queue
import shutil
import subprocess
import threading
import time
import uuid
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable, List, Optional, Sequence

#: The mesh fan-out sentinel for the envelope ``to`` field (FR-008b).
BROADCAST = "broadcast"

#: An endpoint identifier (a participating GLP REPL endpoint).
EndpointId = str

#: spec 025 madGLP globalize placeholders that MUST NOT cross the wire (only ground terms relay —
#: data-model.md §GLP message; typed-glp-manual.md §12 reserved constants ``_w(p,i)`` / ``_r(p,i)``).
_PLACEHOLDER_TOKENS = ("_w(", "_r(")


class GroundRelayViolation(ValueError):
    """Raised when a payload carries a non-ground spec 025 placeholder (``_w(`` / ``_r(``).

    Ground-relay is a spec 025 discipline (FR-018): only ground GLP terms relay. Sending a
    placeholder is a caller bug upstream of the link (the term should have been grounded before
    egress), not a condition to tolerate — so we STOP rather than ship a malformed term.
    """


def assert_ground_relay(payload: str) -> str:
    """Return ``payload`` unchanged iff it carries no ``_w(`` / ``_r(`` placeholder; else raise.

    A conservative, contract-level enforcement of 025 ground-relay at the envelope boundary — the
    authoritative groundness is established by 025's globalize/localize above the link; this guard
    refuses the two reserved placeholder forms the contract names explicitly.
    """
    for token in _PLACEHOLDER_TOKENS:
        if token in payload:
            raise GroundRelayViolation(
                f"payload carries a non-ground placeholder {token!r}; only ground GLP terms relay "
                f"(spec 025 ground-relay, wire-contract.md §GLP-message envelope)"
            )
    return payload


def new_msg_id() -> str:
    """A fresh per-message id (dedup key, in concert with the 025 ``seq``)."""
    return uuid.uuid4().hex


def parse_addressed(text: str, default_to: EndpointId) -> "tuple[EndpointId, str]":
    """Route a composed message to a peer (FR-006): ``@<peer> body`` -> ``(peer, body)``; otherwise
    ``(default_to, text)``.

    The single source of truth for the ``@name`` directed-send convention, shared by the plain link
    console and the ``--tui`` terminal so the two cannot drift. (The terminal previously advertised
    ``@<to>`` in its help but never parsed it -- every message silently went to the default peer.)
    """
    if text.startswith("@"):
        head, _, rest = text[1:].partition(" ")
        return head, rest
    return default_to, text


@dataclass(frozen=True)
class GlpMessage:
    """One L5 GLP-message envelope (``contracts/wire-contract.md``).

    ``sender`` serializes to the wire key ``from`` (a Python keyword). ``seq`` is assigned by the
    025 sublayer at egress; it is ``None`` until the link layer stamps it.
    """

    sender: EndpointId               # wire key: "from"
    to: EndpointId                   # an endpoint_id, or BROADCAST
    payload: str                     # a GROUND GLP term (text)
    msg_id: str = field(default_factory=new_msg_id)
    seq: Optional[int] = None        # 025 per-link sequence number (stamped by the sublayer)

    def __post_init__(self) -> None:
        if not self.sender:
            raise ValueError("GlpMessage.sender (from) must be a non-empty endpoint_id")
        if not self.to:
            raise ValueError("GlpMessage.to must be an endpoint_id or BROADCAST")
        assert_ground_relay(self.payload)

    @property
    def is_broadcast(self) -> bool:
        return self.to == BROADCAST

    def to_wire(self) -> bytes:
        """Serialize to the L5 envelope (UTF-8 JSON) carried inside the 025 frame.

        Uses the contract's exact field names (``from``, not ``sender``).
        """
        obj = {
            "msg_id": self.msg_id,
            "from": self.sender,
            "to": self.to,
            "seq": self.seq,
            "payload": self.payload,
        }
        return json.dumps(obj, separators=(",", ":"), ensure_ascii=False).encode("utf-8")

    @classmethod
    def from_wire(cls, data: bytes) -> "GlpMessage":
        """Parse an L5 envelope (UTF-8 JSON) back into a :class:`GlpMessage`.

        Re-checks ground-relay on ingress (a peer must not have shipped a placeholder).
        """
        obj = json.loads(data.decode("utf-8"))
        missing = {"msg_id", "from", "to", "payload"} - obj.keys()
        if missing:
            raise ValueError(f"malformed GLP-message envelope; missing fields {sorted(missing)}")
        return cls(
            sender=obj["from"],
            to=obj["to"],
            payload=obj["payload"],
            msg_id=obj["msg_id"],
            seq=obj.get("seq"),
        )


# --------------------------------------------------------------------------------------
# GLP-REPL ↔ link process bridge (US5 / T036 / R10)
# --------------------------------------------------------------------------------------
def _repo_root() -> Path:
    # glp_quick/src/glp_quick/repl_link.py → repo root is three parents up.
    return Path(__file__).resolve().parents[3]


def _dotnet() -> str:
    override = os.environ.get("GLPQUICK_DOTNET")
    if override:
        return override
    return shutil.which("dotnet") or ("dotnet.exe" if os.name == "nt" else "dotnet")


def default_repl_command() -> Optional[List[str]]:
    """Resolve the command that launches the GLP REPL over stdio (``out/csharp/glp_repl``; ``dart`` on
    demand), or ``None`` when none is available. Override with ``GLPQUICK_REPL_CMD`` (shell-split)."""
    override = os.environ.get("GLPQUICK_REPL_CMD")
    if override:
        import shlex

        return shlex.split(override)
    base = _repo_root() / "out" / "csharp" / "glp_repl" / "bin"
    for config in ("Release", "Debug"):
        dll = base / config / "net10.0" / "glp_repl.dll"
        if dll.exists():
            return [_dotnet(), str(dll)]
    return None


class ReplBridge:
    """A supervised GLP REPL subprocess driven over its stdio (US5 / FR-016).

    ``start`` spawns the REPL (capturing any spawn failure rather than raising); ``evaluate`` feeds one
    goal line and returns whatever the REPL renders (best-effort, bounded by a quiet-timeout). This is
    the one component that turns a local GLP REPL into a "virtual REPL running over the link": a
    receiver of ``repl_goal`` feeds the goal here and returns the rendering as ``repl_result``.
    """

    def __init__(self, command: Optional[List[str]] = None, *, quiet_timeout: float = 0.6,
                 max_wait: float = 6.0) -> None:
        self.command = command if command is not None else default_repl_command()
        self.error: Optional[str] = None
        self.quiet_timeout = quiet_timeout
        self.max_wait = max_wait
        self._proc: Optional[subprocess.Popen] = None
        self._rx: "queue.Queue[Optional[str]]" = queue.Queue()

    @property
    def started(self) -> bool:
        return self._proc is not None and self._proc.poll() is None

    def start(self) -> bool:
        """Spawn the REPL. Returns ``True`` on success; on failure records ``self.error`` and returns
        ``False`` (spawn failure is reported on the page, never crashes the terminal — FR-016)."""
        if self.command is None:
            self.error = "no GLP REPL available (out/csharp/glp_repl not built; set GLPQUICK_REPL_CMD)"
            return False
        try:
            self._proc = subprocess.Popen(
                self.command, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT, text=True, bufsize=1,
            )
        except Exception as exc:  # noqa: BLE001 - report any spawn failure, do not raise
            self.error = f"repl spawn failed: {exc}"
            self._proc = None
            return False
        threading.Thread(target=self._pump, daemon=True).start()
        self._drain(self.quiet_timeout, self.max_wait)  # swallow the startup banner
        return True

    def _pump(self) -> None:
        assert self._proc is not None and self._proc.stdout is not None
        for line in self._proc.stdout:
            self._rx.put(line.rstrip("\r\n"))
        self._rx.put(None)  # EOF

    def _drain(self, quiet: float, cap: float) -> List[str]:
        out: List[str] = []
        deadline = time.monotonic() + cap
        while True:
            try:
                item = self._rx.get(timeout=quiet)
            except queue.Empty:
                break
            if item is None:  # process EOF
                break
            out.append(item)
            if time.monotonic() >= deadline:
                break
        return out

    def evaluate(self, goal: str) -> str:
        """Feed ``goal`` to the REPL and return its rendered output (best-effort)."""
        if not self.started:
            return f"[repl error: {self.error or 'not started'}]"
        assert self._proc is not None and self._proc.stdin is not None
        try:
            self._proc.stdin.write(goal.rstrip("\n") + "\n")
            self._proc.stdin.flush()
        except Exception as exc:  # noqa: BLE001
            return f"[repl error: write failed: {exc}]"
        lines = self._drain(self.quiet_timeout, self.max_wait)
        return "\n".join(lines).strip() or "(no output)"

    def stop(self) -> None:
        if self._proc is None:
            return
        try:
            if self._proc.stdin and not self._proc.stdin.closed:
                self._proc.stdin.close()
        except OSError:
            pass
        try:
            self._proc.terminate()
            self._proc.wait(timeout=3)
        except Exception:  # noqa: BLE001
            try:
                self._proc.kill()
            except Exception:  # noqa: BLE001
                pass
        self._proc = None


def route(message: GlpMessage, endpoints: Iterable[EndpointId]) -> list[EndpointId]:
    """The mesh routing decision (FR-008b): the target endpoint_ids for ``message``.

    - ``to == BROADCAST`` → every participating endpoint **except the sender** (fan-out).
    - otherwise → ``[to]`` iff that endpoint is currently participating, else ``[]``.

    A pure function — the server's actual delivery (over per-link 025 channels) is US2 (T027).
    Self-delivery is excluded for broadcast so a sender does not receive its own fan-out.
    """
    members = list(endpoints)
    if message.is_broadcast:
        return [ep for ep in members if ep != message.sender]
    return [message.to] if message.to in members else []

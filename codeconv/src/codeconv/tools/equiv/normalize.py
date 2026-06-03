"""Heap->logical relabeling + causal-edge derivation (FR-002, T013).

PURE — no I/O beyond reading trace TEXT, no runtime, no LM
(``test_no_lm_on_production_path`` guards the import surface). Turns a raw
event stream (heap addresses + per-event read/write var sets) into the
normalized :mod:`trace` model the relation compares.

Contract: ``specs/020-trace-equivalence-fidelity/contracts/trace_normalization.md``.

Two layers:

1. **The pure core** — :func:`normalize`. Given a sequence of :class:`RawEvent`
   (payloads carrying :class:`Addr` heap-address sentinels + explicit per-event
   ``reads`` / ``writes``) it performs:
     - **first-occurrence relabeling** (the i-th distinct heap address
       encountered in capture order -> logical var ``v0, v1, ...``), so payloads
       never carry raw addresses and two runs differing ONLY in address values
       normalize to structurally identical traces (SC-005 false-divergence guard);
     - **causal-edge derivation** (writer-MGU: a read/bind depends on the event
       that last bound the writer it reads).
   This core is what the SC-005 tests exercise directly with constructed
   fixtures — no REPL needed.

2. **The text parsers** — :func:`parse_dart` / :func:`parse_csharp`. Both consume
   the SAME canonical line-oriented wire format (below) and delegate to
   :func:`normalize`, so they "produce the same model" (contract). The format is
   what the instrumented C# REPL emits (T017) and what a thin Dart trace-adapter
   targets; per research R10 the normalizer adapts to what Dart emits and we do
   NOT modify the Dart golden. Wiring these to live ``:trace`` / ``:debug`` text
   is finalized against real captures at T017/T022 (runtime-coupled, B1); the
   ``dialect`` hook is where that source-specific adaptation lands.

GLP authority (CLAUDE.md): the five event kinds are exactly the observable
three-phase / SRSW / writer-MGU / three-valued-unification semantics. We do NOT
invent events; if a needed event is absent from Dart ``:trace`` that is a spec
gap to STOP & report, not a normalizer workaround.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from typing import Any, Iterable, Optional, Sequence

from codeconv.tools.equiv.trace import Event, EventKind, Outcome, Status, Trace


@dataclass(frozen=True)
class Addr:
    """A raw heap-address sentinel, relabeled to a logical var by :func:`normalize`.

    Wrapping addresses (rather than relabeling bare ints/strings) lets the
    relabeler tell a heap address apart from a goal id, opcode, or PC — only
    ``Addr`` values are rewritten; everything else is preserved verbatim.
    """

    raw: str

    @staticmethod
    def of(value: Any) -> "Addr":
        return value if isinstance(value, Addr) else Addr(str(value))


@dataclass(frozen=True)
class GoalId:
    """A per-run goal identifier, relabeled to a logical goal token (``g0, g1,
    …``) by :func:`normalize` in a namespace SEPARATE from heap addresses
    (``v0, v1, …``).

    Like :class:`Addr`, the raw value is per-run (the C# emits ``g<RunnerContext
    id>``; the Dart golden's text names goals by display string) and must NOT be
    compared across runs — only its first-occurrence position (structural
    goal-correspondence) is. Relabeling goal ids (rather than dropping the
    ``goal`` field) keeps SUSPEND/REACTIVATE goal-identity as a fidelity signal
    while making the raw scheme-specific values irrelevant.
    """

    raw: str

    @staticmethod
    def of(value: Any) -> "GoalId":
        return value if isinstance(value, GoalId) else GoalId(str(value))


@dataclass(frozen=True)
class RawEvent:
    """A pre-relabeling event: payload may carry :class:`Addr` sentinels.

    ``reads`` / ``writes`` are the addresses this event depends on / binds, used
    for causal-edge derivation (they are NOT compared payload — they only induce
    ``causes``). ``writes`` should hold the writer(s) this event binds
    (WRITER_BIND, or a UNIFY that binds); ``reads`` the writer(s) whose bound
    value this event consumes.
    """

    seq: int
    kind: EventKind
    payload: dict[str, Any] = field(default_factory=dict)
    reads: tuple[Addr, ...] = ()
    writes: tuple[Addr, ...] = ()

    def __post_init__(self) -> None:
        object.__setattr__(self, "reads", tuple(Addr.of(a) for a in self.reads))
        object.__setattr__(self, "writes", tuple(Addr.of(a) for a in self.writes))


@dataclass(frozen=True)
class RawOutcome:
    """Pre-relabeling outcome; ``bindings`` shapes may embed :class:`Addr`."""

    status: Status
    bindings: tuple[tuple[str, Any], ...] = ()


# --------------------------------------------------------------------------- #
# The pure core: relabel + derive causes
# --------------------------------------------------------------------------- #
def _collect(value: Any, addrs: dict[str, None], goals: dict[str, None]) -> None:
    """Record first-occurrence of every :class:`Addr` / :class:`GoalId` reachable.

    ``addrs`` / ``goals`` are insertion-ordered dicts used as ordered sets (two
    SEPARATE namespaces); the first time a raw token is seen fixes its logical
    index in its namespace.
    """
    if isinstance(value, Addr):
        addrs.setdefault(value.raw, None)
    elif isinstance(value, GoalId):
        goals.setdefault(value.raw, None)
    elif isinstance(value, dict):
        for k in sorted(value):  # deterministic intra-payload scan order
            _collect(value[k], addrs, goals)
    elif isinstance(value, (list, tuple)):
        for item in value:
            _collect(item, addrs, goals)


def _relabel(value: Any, labels: dict[str, str], goal_labels: dict[str, str]) -> Any:
    """Rewrite each :class:`Addr` → ``v_i`` and each :class:`GoalId` → ``g_i``."""
    if isinstance(value, Addr):
        return labels[value.raw]
    if isinstance(value, GoalId):
        return goal_labels[value.raw]
    if isinstance(value, dict):
        return {k: _relabel(v, labels, goal_labels) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return tuple(_relabel(item, labels, goal_labels) for item in value)
    return value


def normalize(
    raw_events: Sequence[RawEvent],
    raw_outcome: RawOutcome,
) -> Trace:
    """Relabel addresses + derive causal edges -> the canonical :class:`Trace`.

    First-occurrence canonicalization is in capture order (``seq``); within a
    single event the scan order is ``reads`` then ``writes`` then payload (keys
    sorted), so two runs differing only in address *values* — not structure —
    receive identical logical labels (SC-005). ``causes`` are derived by
    writer-MGU: an event's reads depend on the event that last bound that writer.
    """
    events = sorted(raw_events, key=lambda e: e.seq)

    # Pass 1 — first-occurrence label maps over the whole run (two namespaces:
    # heap addresses -> v_i, goal ids -> g_i).
    seen: dict[str, None] = {}
    goals_seen: dict[str, None] = {}
    for ev in events:
        for a in ev.reads:
            seen.setdefault(a.raw, None)
        for a in ev.writes:
            seen.setdefault(a.raw, None)
        _collect(ev.payload, seen, goals_seen)
    _collect(
        [shape for _name, shape in raw_outcome.bindings], seen, goals_seen
    )
    labels = {raw: f"v{i}" for i, raw in enumerate(seen)}
    goal_labels = {raw: f"g{i}" for i, raw in enumerate(goals_seen)}

    # Pass 2 — relabel + derive causes (writer last-bind frontier).
    last_bind: dict[str, int] = {}
    out_events: list[Event] = []
    for ev in events:
        read_vars = [labels[a.raw] for a in ev.reads]
        causes = frozenset(last_bind[v] for v in read_vars if v in last_bind)
        out_events.append(
            Event(
                seq=ev.seq,
                kind=ev.kind,
                payload=_relabel(ev.payload, labels, goal_labels),
                causes=causes,
            )
        )
        for a in ev.writes:
            last_bind[labels[a.raw]] = ev.seq

    bindings = tuple(
        (name, _relabel(shape, labels, goal_labels))
        for name, shape in raw_outcome.bindings
    )
    return Trace(events=tuple(out_events), outcome=Outcome(raw_outcome.status, bindings))


# --------------------------------------------------------------------------- #
# Canonical wire-format parsers (shared grammar; dialect hook for R10)
# --------------------------------------------------------------------------- #
# Grammar (one record per non-blank, non-``#`` line):
#
#   EV <seq> <KIND> [reads=a,b] [writes=c] [<key>=<value> ...]
#   OUT <status> [<var>=<shape> ...]
#
# Address-valued fields (wrapped as Addr, relabeled v_i): ``reads``, ``writes``,
# UNIFY ``vars``, SUSPEND ``reader``, REACTIVATE/WRITER_BIND ``writer``.
# Goal-id fields (wrapped as GoalId, relabeled g_i in a separate namespace):
# SUSPEND/REACTIVATE ``goal``. Plain fields (kept verbatim): UNIFY ``outcome``,
# WRITER_BIND ``shape``, BYTECODE_OP ``opcode`` / ``pc``. Per-kind defaults
# supply reads/writes when not given explicitly (writer-MGU).
_ADDR_FIELDS = {
    EventKind.UNIFY: ("vars",),
    EventKind.SUSPEND: ("reader",),
    EventKind.REACTIVATE: ("writer",),
    EventKind.WRITER_BIND: ("writer",),
    EventKind.BYTECODE_OP: (),
}

_GOAL_FIELDS = {
    EventKind.SUSPEND: ("goal",),
    EventKind.REACTIVATE: ("goal",),
}


def _split_csv(value: str) -> tuple[str, ...]:
    return tuple(tok for tok in (t.strip() for t in value.split(",")) if tok)


def _default_reads_writes(
    kind: EventKind, payload: dict[str, Any]
) -> tuple[tuple[Addr, ...], tuple[Addr, ...]]:
    """Writer-MGU defaults when a line omits explicit reads/writes."""
    if kind is EventKind.WRITER_BIND:
        return (), tuple(_as_addr_list(payload.get("writer")))
    if kind is EventKind.REACTIVATE:
        return tuple(_as_addr_list(payload.get("writer"))), ()
    if kind is EventKind.SUSPEND:
        return tuple(_as_addr_list(payload.get("reader"))), ()
    if kind is EventKind.UNIFY:
        return tuple(_as_addr_list(payload.get("vars"))), ()
    return (), ()


def _as_addr_list(value: Any) -> list[Addr]:
    if value is None:
        return []
    if isinstance(value, Addr):
        return [value]
    if isinstance(value, (list, tuple)):
        return [Addr.of(v) for v in value]
    return [Addr.of(value)]


def _parse(text: str, *, dialect: str) -> Trace:
    raw_events: list[RawEvent] = []
    raw_outcome = RawOutcome(Status.FAIL)
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        head, *rest = line.split(None, 1)
        body = rest[0] if rest else ""
        if head == "OUT":
            raw_outcome = _parse_outcome(body)
        elif head == "EV":
            raw_events.append(_parse_event(body))
        # Unknown line tags are ignored — the dialect adapter (R10) is
        # responsible for emitting only EV/OUT records; stray text is benign.
    return normalize(raw_events, raw_outcome)


def _parse_fields(tokens: Iterable[str]) -> dict[str, str]:
    fields: dict[str, str] = {}
    for tok in tokens:
        if "=" in tok:
            key, _, value = tok.partition("=")
            fields[key] = value
    return fields


def _parse_event(body: str) -> RawEvent:
    parts = body.split()
    seq = int(parts[0])
    kind = EventKind(parts[1])
    fields = _parse_fields(parts[2:])

    explicit_reads = tuple(Addr(a) for a in _split_csv(fields.pop("reads", "")))
    explicit_writes = tuple(Addr(a) for a in _split_csv(fields.pop("writes", "")))

    payload: dict[str, Any] = {}
    addr_keys = _ADDR_FIELDS[kind]
    goal_keys = _GOAL_FIELDS.get(kind, ())
    for key, value in fields.items():
        if key in addr_keys:
            addrs = [Addr(a) for a in _split_csv(value)]
            payload[key] = addrs if key == "vars" else (addrs[0] if addrs else None)
        elif key in goal_keys:
            payload[key] = GoalId(value)  # relabeled g_i (separate namespace)
        elif kind is EventKind.BYTECODE_OP and key == "pc":
            payload[key] = int(value)
        else:
            payload[key] = value
    if kind is EventKind.BYTECODE_OP:
        payload.setdefault("logical_pc", payload.pop("pc", 0))

    reads = explicit_reads or _default_reads_writes(kind, payload)[0]
    writes = explicit_writes or _default_reads_writes(kind, payload)[1]
    return RawEvent(seq=seq, kind=kind, payload=payload, reads=reads, writes=writes)


def _parse_outcome(body: str) -> RawOutcome:
    parts = body.split()
    status = Status(parts[0])
    bindings = tuple(
        (key, value) for key, value in _parse_fields(parts[1:]).items()
    )
    return RawOutcome(status=status, bindings=bindings)


# --------------------------------------------------------------------------- #
# Dart :trace / :debug text adapter (R10, T022)
# --------------------------------------------------------------------------- #
# The Dart golden REPL emits human-oriented ``:trace`` + ``:debug`` text, NOT the
# canonical EV/OUT wire format. parse_dart adapts that text to the SAME canonical
# format parse_csharp consumes, then delegates to the shared ``_parse`` — so both
# front-ends "produce the same model" (trace_normalization.md). The Dart golden is
# NEVER modified (R10); all adaptation here is read-only.
#
# Mapping (verified line-by-line against tests/fixtures/equiv/append_dart.txt vs
# append_csharp.txt):
#   * ``[DEBUG] PC <pc>: <Op> …`` → ONE BYTECODE_OP per dispatch; consecutive
#     same-(pc,op) sublines collapse to one; only the 12 dispatch-loop ops the
#     Dart ``:debug`` prints unconditionally are kept (the C# ``_spineOps`` set in
#     equiv_trace.cs); GetValue is skipped (the C# excludes it).
#   * COMMIT block: the first ``COMMIT - σ̂w contains N bindings:`` line → BYTECODE_OP
#     Commit (Dart prints COMMIT only on a proceeding commit — symmetric with the
#     C# OpAt past the resolvedSi check), THEN UNIFY success (vars = the ``W#``
#     writers in listed order), THEN one WRITER_BIND per ``  W# → <shape>`` subline,
#     THEN N REACTIVATE from the ``reactivating N goal(s)`` line. The secondary
#     ``Applying …`` / ``Applied …`` COMMIT lines are ignored.
#   * ``NoMoreClauses - SUSPENDING on readers: [..]`` → UNIFY suspend (vars=readers)
#     + one SUSPEND per reader; the SUSPEND goal is the token from the FOLLOWING
#     ``<goal-display> → suspended`` line (relabeled g_i in the goal namespace).
#   * OUT: the ``Var = <value>`` lines + the terminal ``→ succeeds|suspended|failed``.
# Term displays are canonicalized to the C# ``ShapeOf`` form by ``_canonical_shape``.

_PROMPT = "GLP> "
_RE_PC_OP = re.compile(r"^\[DEBUG\]\s+PC\s+(?P<pc>\d+):\s+(?P<op>\S+)")
_RE_SUSPENDING = re.compile(r"SUSPENDING on readers:\s*\[(?P<readers>[^\]]*)\]")
_RE_WBIND = re.compile(r"^W(?P<w>\d+)\s*→\s*(?P<shape>.+?)\s*$")
_RE_COMMIT_BINDS = re.compile(r"contains\s+(?P<n>\d+)\s+bindings")
_RE_REACT = re.compile(r"reactivating\s+(?P<n>\d+)\s+goal")
_RE_GOAL_SUSP = re.compile(r"^(?P<goal>.+?)\s*→\s*suspended\s*$")
_RE_OUT_STATUS = re.compile(r"^→\s*(?P<status>succeeds|suspended|failed)\s*$")
_RE_OUT_BIND = re.compile(r"^(?P<var>[A-Za-z_]\w*)\s*=\s*(?P<val>.+?)\s*$")

# Dart `:debug` prints these op handlers' `[DEBUG] PC X: <Op>` line on EVERY
# dispatch — exactly the C# dispatch-loop `_spineOps` (equiv_trace.cs). Commit is
# emitted from the COMMIT block (conditionally observable); GetValue is excluded.
_DART_SPINE_OPS = frozenset(
    {
        "ClauseTry", "Push", "Pop", "UnifyStructure", "HeadStructure",
        "UnifyVariable", "GetVariable",
        "NoMoreClauses", "Guard", "Ground", "NoReaders", "GroundEqual",
    }
)

# Dart final-outcome word → canonical Status value (OUT uses `succeed`, distinct
# from UNIFY's `success`).
_DART_STATUS = {"succeeds": "succeed", "suspended": "suspend", "failed": "fail"}


def _looks_like_dart_repl(text: str) -> bool:
    """True for Dart ``:trace``/``:debug`` text; False for canonical EV/OUT wire
    format. The Dart REPL always emits ``[DEBUG]`` lines, ``GLP>`` prompts, and
    ``→`` outcome arrows; canonical shapes/goals never contain any of them."""
    return "[DEBUG]" in text or _PROMPT in text or "→" in text


def _strip_prompt(line: str) -> str:
    while line.startswith(_PROMPT):
        line = line[len(_PROMPT):]
    return line


def _san_goal(tok: str) -> str:
    """Sanitize a goal display to one wire token (relabeled to g_i downstream, so
    the exact value is irrelevant — only first-occurrence position matters)."""
    return re.sub(r"\s+", "_", tok.strip()).replace("=", ":")


def _split_addrs(value: str) -> list[str]:
    return [tok.strip() for tok in value.split(",") if tok.strip()]


# -- Term-display canonicalizer: Dart display → C# ShapeOf form (recursive) ---- #
_CT_DELIMS = "(),|[]"


def _canonical_shape(display: str) -> str:
    node, _ = _ct_parse(display.strip(), 0)
    return _ct_render(node)


def _ct_skip_ws(s: str, i: int) -> int:
    while i < len(s) and s[i].isspace():
        i += 1
    return i


def _ct_parse(s: str, i: int):
    i = _ct_skip_ws(s, i)
    if i >= len(s):
        return ("var",), i
    if s[i] == "[":
        return _ct_parse_list(s, i)
    j = i
    while j < len(s) and s[j] not in _CT_DELIMS and not s[j].isspace():
        j += 1
    head = s[i:j]
    k = _ct_skip_ws(s, j)
    if k < len(s) and s[k] == "(":
        if head == "Const":
            inner, nk = _ct_paren_atom(s, k)
            return ("const", inner), nk
        if head.startswith("Var"):  # defensive; clean `Var@n` has no following '('
            return ("var",), j
        args, nk = _ct_parse_args(s, k)
        return ("struct", head.split("/")[0], args), nk
    return _ct_atom_or_var(head), j


def _ct_atom_or_var(tok: str):
    tok = tok.strip()
    if not tok:
        return ("var",)
    if tok[0].isupper() or "@" in tok or tok.endswith("?"):
        return ("var",)
    return ("const", tok)


def _ct_paren_atom(s: str, k: int):
    i = k + 1
    depth = 1
    start = i
    while i < len(s) and depth > 0:
        if s[i] == "(":
            depth += 1
        elif s[i] == ")":
            depth -= 1
            if depth == 0:
                break
        i += 1
    return s[start:i].strip(), i + 1


def _ct_parse_args(s: str, k: int):
    i = _ct_skip_ws(s, k + 1)
    args: list = []
    if i < len(s) and s[i] == ")":
        return args, i + 1
    while True:
        node, i = _ct_parse(s, i)
        args.append(node)
        i = _ct_skip_ws(s, i)
        if i < len(s) and s[i] == ",":
            i += 1
            continue
        if i < len(s) and s[i] == ")":
            return args, i + 1
        return args, i


def _ct_parse_list(s: str, i: int):
    i = _ct_skip_ws(s, i + 1)  # past '['
    if i < len(s) and s[i] == "]":
        return ("const", "nil"), i + 1
    elems: list = []
    tail = ("const", "nil")
    while True:
        node, i = _ct_parse(s, i)
        elems.append(node)
        i = _ct_skip_ws(s, i)
        if i < len(s) and s[i] == ",":
            i = _ct_skip_ws(s, i + 1)
            continue
        if i < len(s) and s[i] == "|":
            tail, i = _ct_parse(s, i + 1)
            i = _ct_skip_ws(s, i)
            if i < len(s) and s[i] == "]":
                i += 1
            break
        if i < len(s) and s[i] == "]":
            i += 1
            break
        break
    result = tail
    for elem in reversed(elems):  # right-fold cons
        result = ("struct", ".", [elem, result])
    return result, i


def _ct_render(node) -> str:
    tag = node[0]
    if tag == "const":
        return f"const({node[1]})"
    if tag == "var":
        return "var"
    _, functor, args = node
    inner = ",".join(_ct_render(a) for a in args)
    return f"{functor}/{len(args)}({inner})"


def _find_suspend_goal(lines: list[str], start: int, n: int):
    """The goal token from the first ``<goal> → suspended`` line at/after ``start``
    (skipping interleaved ``[DEBUG]`` lines); ``(goal, next_index)`` or ``(None,
    start)`` if the immediately-following content line is not a goal-suspended line."""
    j = start
    while j < n:
        line = lines[j].strip()
        if not line or line.startswith("[DEBUG"):
            j += 1
            continue
        gm = _RE_GOAL_SUSP.match(line)
        if gm:
            return _san_goal(gm.group("goal")), j + 1
        return None, start
    return None, start


def _dart_to_wire(text: str) -> str:
    """Adapt Dart ``:trace``/``:debug`` text → canonical EV/OUT wire text."""
    lines = [_strip_prompt(raw) for raw in text.splitlines()]
    n = len(lines)
    wire: list[str] = []
    seq = 0

    def emit(rec: str) -> None:
        nonlocal seq
        wire.append(f"EV {seq} {rec}")
        seq += 1

    out_bindings: list[tuple[str, str]] = []
    out_status: Optional[str] = None
    last_op: Optional[tuple[int, str]] = None
    i = 0
    while i < n:
        line = lines[i].strip()
        i += 1
        if not line or line.startswith("[DEBUG _finalUnboundVar]") or " :- " in line:
            continue

        m = _RE_PC_OP.match(line)
        if m:
            pc = int(m.group("pc"))
            op = m.group("op")
            if op == "COMMIT":
                if _RE_COMMIT_BINDS.search(line):  # the commit-start line
                    emit(f"BYTECODE_OP opcode=Commit pc={pc}")
                    binds: list[tuple[str, str]] = []
                    while i < n:
                        wm = _RE_WBIND.match(lines[i].strip())
                        if not wm:
                            break
                        i += 1
                        binds.append((wm.group("w"), _canonical_shape(wm.group("shape"))))
                    emit("UNIFY outcome=success vars=" + ",".join(w for w, _ in binds))
                    for writer, shape in binds:
                        emit(f"WRITER_BIND writer={writer} shape={shape}")
                    last_op = (pc, op)
                    continue
                rm = _RE_REACT.search(line)
                if rm:
                    for _k in range(int(rm.group("n"))):
                        # Dart `reactivating N` carries no goal id; REACTIVATE
                        # goal-token fidelity for N>0 (bonds/dynamic tier) is a
                        # T022 follow-on — append's commits reactivate 0 goals.
                        emit("REACTIVATE goal=reactivated")
                    last_op = (pc, op)
                    continue
                last_op = (pc, op)  # secondary `Applying …` line — ignore
                continue
            if (pc, op) == last_op:  # collapse consecutive same-(pc,op) sublines
                continue
            last_op = (pc, op)
            if op in _DART_SPINE_OPS:
                emit(f"BYTECODE_OP opcode={op} pc={pc}")
            continue

        sm = _RE_SUSPENDING.search(line)
        if sm:
            readers = _split_addrs(sm.group("readers"))
            emit("UNIFY outcome=suspend vars=" + ",".join(readers))
            goal, ni = _find_suspend_goal(lines, i, n)
            if goal is None:
                goal = "suspended_goal"
            else:
                i = ni
            for reader in readers:
                emit(f"SUSPEND reader={reader} goal={goal}")
            continue

        om = _RE_OUT_STATUS.match(line)
        if om:
            out_status = _DART_STATUS[om.group("status")]
            continue

        if _RE_GOAL_SUSP.match(line):  # stray goal-suspended line (normally consumed)
            continue

        bm = _RE_OUT_BIND.match(line)
        if bm:
            out_bindings.append((bm.group("var"), _canonical_shape(bm.group("val"))))
            continue
        # everything else (banner, reductions, Goodbye, …) is benign noise

    out_line = f"OUT {out_status or 'fail'}"
    for var, shape in out_bindings:
        out_line += f" {var}={shape}"
    return "\n".join([*wire, out_line])


def parse_dart(text: str) -> Trace:
    """Parse the Dart golden's trace into the canonical model.

    Accepts EITHER the live Dart ``:trace``/``:debug`` text (adapted to the
    canonical wire format by :func:`_dart_to_wire`, R10/T022) OR the canonical
    EV/OUT wire format directly (the format the instrumented REPL emits and the
    pure normalize tests use). Per R10 the Dart golden text is never modified.
    """
    if _looks_like_dart_repl(text):
        text = _dart_to_wire(text)
    return _parse(text, dialect="dart")


def parse_csharp(text: str) -> Trace:
    """Parse the converted C# candidate's trace into the canonical model.

    The instrumented ``out/csharp/`` REPL (T017) emits the canonical wire
    format directly (candidate-side instrumentation is in scope, R10).
    """
    return _parse(text, dialect="csharp")

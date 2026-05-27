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

from dataclasses import dataclass, field
from typing import Any, Iterable, Sequence

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
def _collect_addrs(value: Any, into: dict[str, None]) -> None:
    """Record first-occurrence of every :class:`Addr` reachable in ``value``.

    ``into`` is an insertion-ordered dict used as an ordered set; the first time
    a raw token is seen fixes its logical index.
    """
    if isinstance(value, Addr):
        into.setdefault(value.raw, None)
    elif isinstance(value, dict):
        for k in sorted(value):  # deterministic intra-payload scan order
            _collect_addrs(value[k], into)
    elif isinstance(value, (list, tuple)):
        for item in value:
            _collect_addrs(item, into)


def _relabel(value: Any, labels: dict[str, str]) -> Any:
    """Recursively rewrite every :class:`Addr` to its logical-var string."""
    if isinstance(value, Addr):
        return labels[value.raw]
    if isinstance(value, dict):
        return {k: _relabel(v, labels) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return tuple(_relabel(item, labels) for item in value)
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

    # Pass 1 — first-occurrence label map over the whole run.
    seen: dict[str, None] = {}
    for ev in events:
        for a in ev.reads:
            seen.setdefault(a.raw, None)
        for a in ev.writes:
            seen.setdefault(a.raw, None)
        _collect_addrs(ev.payload, seen)
    _collect_addrs(
        [shape for _name, shape in raw_outcome.bindings], seen
    )
    labels = {raw: f"v{i}" for i, raw in enumerate(seen)}

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
                payload=_relabel(ev.payload, labels),
                causes=causes,
            )
        )
        for a in ev.writes:
            last_bind[labels[a.raw]] = ev.seq

    bindings = tuple(
        (name, _relabel(shape, labels)) for name, shape in raw_outcome.bindings
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
# Address-valued fields (wrapped as Addr, hence relabeled): ``reads``,
# ``writes``, UNIFY ``vars``, SUSPEND ``reader``, REACTIVATE/WRITER_BIND
# ``writer``. Plain fields (kept verbatim): UNIFY ``outcome``, ``goal``,
# WRITER_BIND ``shape``, BYTECODE_OP ``opcode`` / ``pc``. Per-kind defaults
# supply reads/writes when not given explicitly (writer-MGU).
_ADDR_FIELDS = {
    EventKind.UNIFY: ("vars",),
    EventKind.SUSPEND: ("reader",),
    EventKind.REACTIVATE: ("writer",),
    EventKind.WRITER_BIND: ("writer",),
    EventKind.BYTECODE_OP: (),
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
    for key, value in fields.items():
        if key in addr_keys:
            addrs = [Addr(a) for a in _split_csv(value)]
            payload[key] = addrs if key == "vars" else (addrs[0] if addrs else None)
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


def parse_dart(text: str) -> Trace:
    """Parse the Dart golden's trace into the canonical model.

    Currently consumes the canonical wire format (above). Adapting the live
    Dart ``:trace`` / ``:debug`` text to that format is the runtime-coupled
    step finalized at T017/T022 (B1); per R10 the Dart golden is never modified.
    """
    return _parse(text, dialect="dart")


def parse_csharp(text: str) -> Trace:
    """Parse the converted C# candidate's trace into the canonical model.

    The instrumented ``out/csharp/`` REPL (T017) emits the canonical wire
    format directly (candidate-side instrumentation is in scope, R10).
    """
    return _parse(text, dialect="csharp")

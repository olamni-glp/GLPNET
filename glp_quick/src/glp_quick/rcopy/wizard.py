"""``/rcopy`` client transfer orchestration + the responder-side wire session (feature 040, US6/US8).

The client core :func:`run_transfer` walks the exchange (offer gate → exclusion filter → manifest →
verdicts → per-``need`` chunk send → per-file outcomes), producing **exactly one** outcome per selected
file (SC-007). It runs against a :class:`TransferProxy` — either :class:`DirectResponderProxy` (a local
responder, for tests) or :class:`LinkProxy` (over the 036 link). :class:`ResponderSession` is the
responder-side counterpart: it turns inbound ``rcopy_*`` terms into reply payloads, accumulating chunks
and committing on completion. All of it rides the one codec (``terminal/protocol.py``); no second
transport (FR-026). Contract: ``contracts/rcopy-protocol.md``.
"""

from __future__ import annotations

import base64
import os
import posixpath
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Dict, List, Optional, Tuple

from glp_quick.rcopy import transfer
from glp_quick.rcopy.filter import ExclusionFilter, FileItem, apply_filter
from glp_quick.rcopy.responder import Responder
from glp_quick.terminal import protocol
from glp_quick.terminal.protocol import decode


# ======================================================================================
# Client side
# ======================================================================================
@dataclass
class FileSpec:
    files: List[FileItem]
    filter: ExclusionFilter = field(default_factory=ExclusionFilter)


@dataclass
class TransferRequest:
    root: str
    folder: str
    specs: List[FileSpec]
    mode: str = "synchronise"       # synchronise | force
    fingerprint: bool = True


@dataclass(frozen=True)
class FileOutcome:
    rel: str
    outcome: str                    # transferred | skipped_identical | filtered_out | rejected
    reason: Optional[str] = None


@dataclass
class TransferResult:
    no_service: bool
    outcomes: List[FileOutcome]

    def summary(self) -> str:
        if self.no_service:
            return "no file service available from this peer — 0 files transferred"
        counts: Dict[str, int] = {}
        for o in self.outcomes:
            counts[o.outcome] = counts.get(o.outcome, 0) + 1
        parts = [f"{k}={v}" for k, v in sorted(counts.items())]
        return "rcopy: " + ", ".join(parts) if parts else "rcopy: nothing selected"


class TransferProxy:
    """The responder operations the client needs (implemented locally or over the link)."""

    def offer(self, peer: str) -> List[Tuple[str, List[str], Optional[int]]]:  # pragma: no cover
        raise NotImplementedError

    def verdict(self, peer, root, folder, manifest, mode):  # pragma: no cover
        raise NotImplementedError

    def send_file(self, peer, root, folder, rel, data, sha):  # pragma: no cover
        raise NotImplementedError


class DirectResponderProxy(TransferProxy):
    """A proxy backed by a local :class:`Responder` (host-free tests + a same-process responder)."""

    def __init__(self, responder: Responder) -> None:
        self.r = responder

    def offer(self, peer):
        return self.r.offer(peer)

    def verdict(self, peer, root, folder, manifest, mode):
        return self.r.verdict(peer, root, folder, manifest, mode)

    def send_file(self, peer, root, folder, rel, data, sha):
        return self.r.commit(peer, root, folder, rel, data, sha)


def run_transfer(peer: str, request: TransferRequest, proxy: TransferProxy,
                 read_bytes: Callable[[FileItem], bytes]) -> TransferResult:
    """Drive one ``/rcopy`` transfer to ``peer`` and return a per-file outcome for every selected file."""
    offer = proxy.offer(peer)
    if not offer:
        return TransferResult(no_service=True, outcomes=[])  # FR-018: report, zero transfers

    outcomes: List[FileOutcome] = []
    manifest: List[Tuple[str, int, str]] = []
    prepared: Dict[str, Tuple[bytes, str]] = {}
    for spec in request.specs:
        kept, dropped = apply_filter(spec.files, spec.filter)
        for f in dropped:
            outcomes.append(FileOutcome(f.rel, "filtered_out", None))  # never sent (FR-028)
        for f in kept:
            data = read_bytes(f)
            sha = transfer.sha256_bytes(data) if request.fingerprint else ""
            manifest.append((f.rel, len(data), sha))
            prepared[f.rel] = (data, sha)

    if manifest:
        verdicts = proxy.verdict(peer, request.root, request.folder, manifest, request.mode)
        for v in verdicts:
            if v.verdict == "skip_identical":
                outcomes.append(FileOutcome(v.rel, "skipped_identical", None))
            elif v.verdict == "reject":
                outcomes.append(FileOutcome(v.rel, "rejected", v.reason))
            else:  # need
                data, sha = prepared[v.rel]
                out = proxy.send_file(peer, request.root, request.folder, v.rel, data, sha)
                outcomes.append(FileOutcome(v.rel, out.outcome, out.reason))
    return TransferResult(no_service=False, outcomes=outcomes)


# --- local file gathering ----------------------------------------------------------------
def gather_files(base: Path, pattern: str = "**/*") -> List[FileItem]:
    """Gather local files under ``base`` matching ``pattern`` as :class:`FileItem`s (POSIX rels)."""
    base = Path(base)
    out: List[FileItem] = []
    for p in sorted(base.glob(pattern)):
        if not p.is_file():
            continue
        rel = p.relative_to(base).as_posix()
        st = p.stat()
        out.append(FileItem(
            rel=rel, size=st.st_size, mtime=int(st.st_mtime),
            hidden=p.name.startswith("."),
            readonly=not os.access(p, os.W_OK),
        ))
    return out


def disk_reader(base: Path) -> Callable[[FileItem], bytes]:
    base = Path(base)
    return lambda item: (base / item.rel).read_bytes()


# ======================================================================================
# Responder side (wire session): inbound rcopy_* terms → reply payloads
# ======================================================================================
class ResponderSession:
    """Turns inbound ``rcopy_*`` messages into reply payloads, accumulating chunks then committing.

    Used by both the tui responder (via the state receive path) and a standalone serve loop. Pending
    per-file state is keyed by ``(peer, root, folder, rel)``; a file commits when its accumulated bytes
    reach the manifested size (commit-on-complete).
    """

    def __init__(self, responder: Responder, self_id: str) -> None:
        self.r = responder
        self.self_id = self_id
        # Keyed by (peer, rel): the chunk message carries only rel, so root/folder (from the manifest
        # phase) are stored in the pending value.
        self._pending: Dict[Tuple[str, str], dict] = {}

    def offer_payload(self, peer: str) -> str:
        return protocol.rcopy_offer(self.r.offer(peer))

    def manifest_verdict_payload(self, peer: str, tm) -> str:
        root, folder, mode = tm.fields[0], tm.fields[1], str(tm.fields[2])
        manifest = [(t.args[0], t.args[1], t.args[2]) for t in tm.fields[3]]
        verdicts = self.r.verdict(peer, root, folder, manifest, mode)
        wire = []
        for v, (rel, size, sha) in zip(verdicts, manifest):
            wire.append((v.rel, v.verdict, v.reason))
            if v.verdict == "need":
                self._pending[(peer, v.rel)] = {"root": root, "folder": folder,
                                                "size": size, "sha": sha, "chunks": {}}
        return protocol.rcopy_verdict(wire)

    def chunk_outcome_payload(self, peer: str, tm) -> Optional[str]:
        rel, seq, b64 = tm.fields[0], tm.fields[1], tm.fields[2]
        pend = self._pending.get((peer, rel))
        if pend is None:
            return None  # a chunk for a file we did not grant (rejected/skip/unknown) — ignore
        pend["chunks"][seq] = b64
        have = sum(len(base64.b64decode(v)) for v in pend["chunks"].values())
        if have < pend["size"]:
            return None  # more chunks expected
        data = transfer.assemble_chunks(list(pend["chunks"].items()))
        del self._pending[(peer, rel)]
        out = self.r.commit(peer, pend["root"], pend["folder"], rel, data, pend["sha"])
        return protocol.rcopy_outcome(out.rel, out.outcome, out.reason)


class LinkProxy(TransferProxy):
    """A :class:`TransferProxy` that reaches a responder over the link by sending ``rcopy_*`` messages
    and blocking on the matching reply (used by the host-gated integration test).

    ``send(to, payload)`` transmits; ``recv(timeout) -> payload|None`` returns the next inbound payload
    from the peer (the caller guarantees a single consumer of the client handle).
    """

    def __init__(self, send: Callable[[str, str], None], recv: Callable[[float], Optional[str]],
                 timeout: float = 15.0) -> None:
        self._send = send
        self._recv = recv
        self._timeout = timeout

    def _await(self, kind: str):
        while True:
            payload = self._recv(self._timeout)
            if payload is None:
                raise TimeoutError(f"no {kind} reply from responder")
            tm = decode(payload)
            if tm.kind == kind:
                return tm

    def offer(self, peer):
        self._send(peer, protocol.rcopy_offer_query())
        tm = self._await("rcopy_offer")
        out = []
        for rt in tm.fields[0]:
            name, folders, left = rt.args[0], list(rt.args[1]), rt.args[2]
            out.append((name, folders, None if left == -1 else left))
        return out

    def verdict(self, peer, root, folder, manifest, mode):
        from glp_quick.rcopy.responder import Verdict
        self._send(peer, protocol.rcopy_manifest(root, folder, mode, manifest))
        tm = self._await("rcopy_verdict")
        out = []
        for vt in tm.fields[0]:
            rel, v = vt.args[0], vt.args[1]
            if isinstance(v, protocol.Term) and v.functor == "reject":
                out.append(Verdict(rel, "reject", str(v.args[0])))
            else:
                out.append(Verdict(rel, str(v)))
        return out

    def send_file(self, peer, root, folder, rel, data, sha):
        from glp_quick.rcopy.responder import Outcome
        for seq, b64 in transfer.chunk_bytes(data):
            self._send(peer, protocol.rcopy_chunk(rel, seq, b64))
        tm = self._await("rcopy_outcome")
        reason = str(tm.fields[2])
        return Outcome(str(tm.fields[0]), str(tm.fields[1]), None if reason == "none" else reason)

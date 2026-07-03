"""``/rcopy`` responder file-service backend (feature 040, US8; FR-032/033/034/038/039).

Configured by ``/rcopy init`` (roots, permitted peers, per-root quota — ``config.json``). It offers only
the roots a requesting **authenticated PeerId** is permitted for, lands files under
``<root>/xfer/in/<peer-name-and-UID>/`` (never outside a permitted root), does the synchronise SHA-256
compare, enforces quota, and — for every file, transferred or rejected — appends the WAL (source of
truth) before updating the catalog projection and recording provenance. Contracts:
``contracts/responder-store.md`` + ``contracts/rcopy-protocol.md``.
"""

from __future__ import annotations

import json
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Tuple

from glp_quick.rcopy import transfer
from glp_quick.rcopy.catalog import PerRootCatalog
from glp_quick.rcopy.provenance import ProvenanceLog, ProvenanceRecord
from glp_quick.rcopy.wal import WalJournal, WalRecord


@dataclass(frozen=True)
class Quota:
    kind: str          # "bytes" | "count"
    limit: int


@dataclass
class UploadRoot:
    name: str
    path: Path
    permitted_peers: set
    quota: Optional[Quota] = None


@dataclass(frozen=True)
class Verdict:
    rel: str
    verdict: str                 # need | skip_identical | reject
    reason: Optional[str] = None  # quota | perm | path


@dataclass(frozen=True)
class Outcome:
    rel: str
    outcome: str                 # transferred | skipped_identical | rejected
    reason: Optional[str] = None


class Responder:
    """A configured responder over a data dir. Construct after :meth:`init` (or on an existing dir)."""

    def __init__(self, data_dir: Path) -> None:
        self.data_dir = Path(data_dir)
        self.roots: Dict[str, UploadRoot] = {}
        self._wal: Dict[str, WalJournal] = {}
        self._cat: Dict[str, PerRootCatalog] = {}
        self._prov: Dict[str, ProvenanceLog] = {}
        self._load()

    # --- configuration -----------------------------------------------------------------
    @classmethod
    def init(cls, data_dir: Path, roots: List[dict]) -> "Responder":
        """Write ``config.json`` (idempotent overwrite of config only — never touches WAL/landing)."""
        data_dir = Path(data_dir)
        data_dir.mkdir(parents=True, exist_ok=True)
        (data_dir / "config.json").write_text(
            json.dumps({"roots": roots}, ensure_ascii=False, indent=2), encoding="utf-8"
        )
        return cls(data_dir)

    def _load(self) -> None:
        cfg_path = self.data_dir / "config.json"
        if not cfg_path.exists():
            return
        cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
        for r in cfg.get("roots", []):
            q = r.get("quota")
            quota = Quota(q["kind"], q["limit"]) if q else None
            root = UploadRoot(r["name"], Path(r["path"]), set(r.get("permitted_peers", [])), quota)
            self.roots[root.name] = root
            meta = self._meta_dir(root.name)
            wal = WalJournal(meta / "wal.log")
            self._wal[root.name] = wal
            self._cat[root.name] = PerRootCatalog.from_wal(wal)  # rebuild from WAL — authoritative (SC-010)
            self._prov[root.name] = ProvenanceLog(meta / "provenance.log")

    def _meta_dir(self, root_name: str) -> Path:
        return self.data_dir / "roots" / root_name

    def _landing(self, root: UploadRoot, peer: str) -> Path:
        return root.path / "xfer" / "in" / peer

    def catalog(self, root_name: str) -> PerRootCatalog:
        return self._cat[root_name]

    def reload_catalog_from_wal(self, root_name: str) -> None:
        """Discard the in-memory catalog and rebuild from the WAL (models catalog.json loss — SC-010)."""
        self._cat[root_name] = PerRootCatalog.from_wal(self._wal[root_name])

    # --- offer (FR-018/FR-032) ---------------------------------------------------------
    def offer(self, peer: str) -> List[Tuple[str, List[str], Optional[int]]]:
        """The roots this peer is permitted for: ``(root_name, existing_folders, quota_bytes_left)``.

        Empty ⇒ "no file service available" for this requester (the wizard stops, zero transfers).
        """
        out: List[Tuple[str, List[str], Optional[int]]] = []
        for name, root in self.roots.items():
            if peer not in root.permitted_peers:
                continue
            landing = self._landing(root, peer)
            folders = sorted(p.name for p in landing.glob("*") if p.is_dir()) if landing.exists() else []
            left = self._quota_left(name)
            out.append((name, folders, left))
        return out

    def _quota_left(self, root_name: str) -> Optional[int]:
        root = self.roots[root_name]
        if root.quota is None or root.quota.kind != "bytes":
            return None
        return max(0, root.quota.limit - self._cat[root_name].total_bytes())

    # --- verdict (FR-034/FR-038, per-file) ---------------------------------------------
    def verdict(self, peer: str, root_name: str, folder: str,
                manifest: List[Tuple[str, int, str]], mode: str) -> List[Verdict]:
        """Decide each manifested file: need | skip_identical | reject(perm|quota|path).

        Records receive-provenance for every non-``need`` verdict (skip/reject) so 100% of manifested
        files are audited (SC-009); ``need`` files are audited at :meth:`commit`.
        """
        verdicts: List[Verdict] = []
        root = self.roots.get(root_name)
        pending_bytes = 0
        for rel_in_folder, size, sha in manifest:
            rel = f"{folder}/{rel_in_folder}" if folder else rel_in_folder
            if root is None or peer not in root.permitted_peers:
                verdicts.append(self._reject(peer, root_name, rel_in_folder, rel, sha, "perm"))
                continue
            if transfer.safe_target(self._landing(root, peer), rel) is None:
                verdicts.append(self._reject(peer, root_name, rel_in_folder, rel, sha, "path"))
                continue
            if mode == "synchronise" and self._cat[root_name].is_identical(peer, rel, sha):
                self._provenance(peer, root_name, rel, sha, "skipped_identical", None)
                verdicts.append(Verdict(rel_in_folder, "skip_identical"))
                continue
            if root.quota is not None and root.quota.kind == "bytes":
                existing = self._cat[root_name].get(peer, rel)
                delta = size - (existing.size if existing else 0)
                if self._cat[root_name].total_bytes() + pending_bytes + delta > root.quota.limit:
                    verdicts.append(self._reject(peer, root_name, rel_in_folder, rel, sha, "quota"))
                    continue
                pending_bytes += delta
            verdicts.append(Verdict(rel_in_folder, "need"))
        return verdicts

    def _reject(self, peer, root_name, rel_in_folder, rel, sha, reason) -> Verdict:
        self._provenance(peer, root_name, rel, sha, "rejected", reason)
        return Verdict(rel_in_folder, "reject", reason)

    # --- commit (FR-039) ---------------------------------------------------------------
    def commit(self, peer: str, root_name: str, folder: str, rel_in_folder: str,
               data: bytes, expected_sha: str, ts_start: Optional[int] = None) -> Outcome:
        """Commit a needed file all-or-nothing: verify → atomic rename → WAL → catalog → provenance."""
        root = self.roots.get(root_name)
        if root is None or peer not in root.permitted_peers:
            self._provenance(peer, root_name, rel_in_folder, expected_sha, "rejected", "perm")
            return Outcome(rel_in_folder, "rejected", "perm")
        rel = f"{folder}/{rel_in_folder}" if folder else rel_in_folder
        # Re-enforce quota against the ACTUAL received bytes: the manifest size verdict() checked is
        # peer-declared and untrusted, so a peer could under-declare size to slip an oversized payload
        # past the verdict gate. Quota MUST hold at the point of landing (FR-038).
        if root.quota is not None and root.quota.kind == "bytes":
            existing = self._cat[root_name].get(peer, rel)
            delta = len(data) - (existing.size if existing else 0)
            if self._cat[root_name].total_bytes() + delta > root.quota.limit:
                self._provenance(peer, root_name, rel, expected_sha, "rejected", "quota")
                return Outcome(rel_in_folder, "rejected", "quota")
        res = transfer.commit_file(self._landing(root, peer), rel, data, expected_sha)
        ts = int(time.time())
        if not res.ok:
            self._provenance(peer, root_name, rel, res.sha256, "rejected", res.reason)
            return Outcome(rel_in_folder, "rejected", res.reason)
        rec = WalRecord("put", rel, len(data), res.sha256, mtime=ts, peer=peer,
                        root=root_name, target_folder=folder, ts=ts)
        self._wal[root_name].append(rec)   # WAL first — the source of truth (FR-036)
        self._cat[root_name].apply(rec)    # then the projection
        self._provenance(peer, root_name, rel, res.sha256, "transferred", None,
                         ts_start=ts_start or ts, ts_commit=ts, target_path=str(res.target))
        return Outcome(rel_in_folder, "transferred")

    def _provenance(self, peer, root_name, rel, sha, outcome, reason, *,
                    ts_start=None, ts_commit=None, target_path=None) -> None:
        prov = self._prov.get(root_name)
        if prov is None:
            return
        ts = int(time.time())
        prov.record(ProvenanceRecord(
            peer=peer, root=root_name, target_path=target_path or rel,
            ts_start=ts_start or ts, ts_commit=ts_commit or ts, sha256=sha,
            outcome=outcome, reason=reason,
        ))

    def provenance(self, root_name: str) -> List[ProvenanceRecord]:
        return self._prov[root_name].all()

"""Dual checkpoint store: PGLite-primary + JSON-fallback (FR-001/020/021).

ONE logical interface (contracts/checkpoint-store.md) backed by two physical
stores reconciled by a strictly-monotonic, marathon-wide ``sequence_no``:

- **primary**  — the shared PGLite cluster, schema ``marathon`` (Alembic
  ``0010``), reached through ``codeconv.bridge_client`` +
  ``codeconv.db.engine`` (reused as libraries — FR-009/010, NOT
  re-implemented). Plain SQLAlchemy: the harness's checkpoint rows are
  ordinary ``marathon.checkpoints`` rows, not DBOS tables, so the store
  needs the engine but not a DBOS launch (DBOS is the orchestrate layer's
  reuse, T013).
- **fallback** — an on-disk JSON tree under
  ``<repo>/.codeconv/marathon/<marathon_id>/`` (data-model.md layout),
  one file per checkpoint named by ``sequence_no``.

Allocation keeps global monotonicity (I1): the next ``sequence_no`` is
``max(primary_max, fallback_max) + 1``. In the normal both-reachable case
every primary write is mirrored to fallback at the **same** seq, so the two
stores stay equal; during a primary outage only fallback advances, and
:meth:`MarathonStore.reconcile` fast-forwards the stale store by copying the
non-conflicting missing rows. Two checkpoints sharing a seq with *different*
content is a true fork — escalate, never silently pick (I7/D5).

The block-id grammar ``{marathon_id}:{stage}:{ordinal}`` (data-model.md)
lets the store derive the marathon id from a block id with no DB round-trip,
so a fallback write needs nothing from the primary.
"""

from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Optional

from .models import Checkpoint, Marathon, Position, ReconcileResult, StageBlock


def marathon_id_of_block(block_id: str) -> str:
    """Derive the marathon id from a deterministic block id
    (``{marathon_id}:{stage}:{ordinal}`` — data-model.md)."""
    return block_id.split(":", 1)[0]


def marathon_id_for(feature_slug: str) -> str:
    """Deterministic, stable, short marathon id from the feature slug, reusing
    ``codeconv.durable``'s id-derivation primitive (FR-009 — reuse, don't
    re-implement). data-model's ``mlink`` is illustrative; readability is
    secondary to determinism + uniqueness."""
    from codeconv.durable import artifact_digest as _stable_hash

    return "m" + _stable_hash(feature_slug)[:8]


def _safe_filename(name: str) -> str:
    """Block ids embed ``:`` (``{marathon_id}:{stage}:{ordinal}``), illegal in
    Windows filenames — map to ``__`` for the JSON-mirror file name (the id
    inside the file content is unchanged)."""
    return name.replace(":", "__")


def _now() -> datetime:
    return datetime.now(timezone.utc)


def _atomic_write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(payload, indent=2, sort_keys=True), encoding="utf-8")
    os.replace(tmp, path)


def _checkpoint_signature(cp: Checkpoint) -> str:
    """Content signature for fork detection — everything that identifies the
    checkpoint *except* its surrogate id and wall-clock ``created_at`` (a
    mirror carries the originator's values, so equal content → equal sig)."""
    return json.dumps(
        {
            "block_id": cp.block_id,
            "marathon_id": cp.marathon_id,
            "sequence_no": cp.sequence_no,
            "stage": cp.stage,
            "wip_unit": cp.wip_unit,
            "completed_units": cp.completed_units,
            "remaining_units": cp.remaining_units,
            "workflow_run_id": cp.workflow_run_id,
            "budget_spent": cp.budget_spent,
            "store_origin": cp.store_origin,
        },
        sort_keys=True,
    )


class MarathonStore:
    """Dual store bound to a repo (+ optional data-dir override).

    ``engine`` may be injected for tests; otherwise the primary engine is
    lazily acquired via ``codeconv.db.engine.connect`` and a failure (bridge
    unreachable) transparently degrades to fallback-only (FR-020).
    ``fallback_only=True`` forces the JSON store (used to simulate an outage).
    """

    def __init__(
        self,
        repo_root: str | os.PathLike[str],
        *,
        data_dir: Optional[str | os.PathLike[str]] = None,
        engine: Any = None,
        fallback_only: bool = False,
    ) -> None:
        self.repo_root = Path(repo_root).resolve()
        self.data_dir = Path(data_dir).resolve() if data_dir is not None else None
        self._engine = engine
        self._engine_resolved = engine is not None
        self._fallback_only = fallback_only

    # --- primary engine (lazy, bridge-backed) ------------------------------

    def _primary(self) -> Any:
        """Return a SQLAlchemy engine for the primary store, or ``None`` if
        the bridge is unreachable (→ fallback mode). Resolved once."""
        if self._fallback_only:
            return None
        if self._engine_resolved:
            return self._engine
        self._engine_resolved = True
        try:
            from codeconv.db.engine import connect

            self._engine = connect(
                self.repo_root, data_dir=self.data_dir, application_name="marathon"
            )
        except Exception:
            self._engine = None
        return self._engine

    def active_store(self, marathon_id: Optional[str] = None) -> str:
        """``"primary"`` when the bridge is reachable, else ``"fallback"``
        (contracts/checkpoint-store.md; FR-020/SC-007)."""
        return "primary" if self._primary() is not None else "fallback"

    # --- fallback (JSON) location ------------------------------------------

    def _marathon_dir(self, marathon_id: str) -> Path:
        return self.repo_root / ".codeconv" / "marathon" / marathon_id

    def _checkpoints_dir(self, marathon_id: str) -> Path:
        return self._marathon_dir(marathon_id) / "checkpoints"

    # --- marathon + stage-block rows (FK parents for checkpoints) ----------

    def upsert_marathon(self, m: Marathon) -> Marathon:
        """Create or re-attach the marathon row — idempotent (FR-014/023): a
        second ``start`` with the same id updates the mutable fields and
        never duplicates. Mirrors ``marathon.json``."""
        engine = self._primary()
        if engine is not None:
            from sqlalchemy import text

            with engine.begin() as conn:
                row = conn.execute(
                    text(
                        "INSERT INTO marathon.marathons "
                        "(id, feature_slug, feature_branch, budget_ceiling, "
                        " budget_spent, preauth_commit_push, preauth_workflow_optin, "
                        " preauth_revoked_at, auto_mode) "
                        "VALUES (:id, :slug, :branch, :ceiling, :spent, :pcp, :pwo, "
                        " :revoked, :auto) "
                        "ON CONFLICT (id) DO UPDATE SET "
                        " feature_slug = EXCLUDED.feature_slug, "
                        " feature_branch = EXCLUDED.feature_branch, "
                        " budget_ceiling = EXCLUDED.budget_ceiling, "
                        " preauth_commit_push = EXCLUDED.preauth_commit_push, "
                        " preauth_workflow_optin = EXCLUDED.preauth_workflow_optin, "
                        " preauth_revoked_at = EXCLUDED.preauth_revoked_at, "
                        " auto_mode = EXCLUDED.auto_mode "
                        "RETURNING budget_spent, created_at"
                    ),
                    {
                        "id": m.id,
                        "slug": m.feature_slug,
                        "branch": m.feature_branch,
                        "ceiling": m.budget_ceiling,
                        "spent": m.budget_spent,
                        "pcp": m.preauth_commit_push,
                        "pwo": m.preauth_workflow_optin,
                        "revoked": m.preauth_revoked_at,
                        "auto": m.auto_mode,
                    },
                ).one()
                m.budget_spent = int(row.budget_spent)
                m.created_at = row.created_at
        if m.created_at is None:
            m.created_at = _now()
        _atomic_write_json(
            self._marathon_dir(m.id) / "marathon.json", _marathon_to_dict(m)
        )
        return m

    def read_marathon(self, marathon_id: str) -> Optional[Marathon]:
        engine = self._primary()
        if engine is not None:
            from sqlalchemy import text

            with engine.connect() as conn:
                row = conn.execute(
                    text(
                        "SELECT id, feature_slug, feature_branch, budget_ceiling, "
                        "budget_spent, preauth_commit_push, preauth_workflow_optin, "
                        "preauth_revoked_at, auto_mode, created_at "
                        "FROM marathon.marathons WHERE id = :id"
                    ),
                    {"id": marathon_id},
                ).one_or_none()
            if row is not None:
                return Marathon(
                    id=row.id,
                    feature_slug=row.feature_slug,
                    feature_branch=row.feature_branch,
                    budget_ceiling=row.budget_ceiling,
                    budget_spent=int(row.budget_spent),
                    preauth_commit_push=row.preauth_commit_push,
                    preauth_workflow_optin=row.preauth_workflow_optin,
                    preauth_revoked_at=row.preauth_revoked_at,
                    auto_mode=row.auto_mode,
                    created_at=row.created_at,
                )
        path = self._marathon_dir(marathon_id) / "marathon.json"
        if path.is_file():
            try:
                return _dict_to_marathon(json.loads(path.read_text(encoding="utf-8")))
            except (OSError, ValueError):
                return None
        return None

    def update_budget_spent(self, marathon_id: str, spent: int) -> None:
        """Persist the running token spend on the marathon (primary CHECK
        guarantees ``spent <= ceiling`` — 0 overruns, SC-006). Mirrors
        ``marathon.json``."""
        engine = self._primary()
        if engine is not None:
            from sqlalchemy import text

            with engine.begin() as conn:
                conn.execute(
                    text(
                        "UPDATE marathon.marathons SET budget_spent = :s WHERE id = :id"
                    ),
                    {"s": spent, "id": marathon_id},
                )
        m = self.read_marathon(marathon_id)
        if m is not None:
            m.budget_spent = spent
            _atomic_write_json(
                self._marathon_dir(marathon_id) / "marathon.json", _marathon_to_dict(m)
            )

    def list_marathon_ids(self) -> list[str]:
        """All marathon ids known to this repo, discovered from the JSON mirror
        (always written by ``upsert_marathon``, so this is bridge-free). Used
        to resolve the sole marathon when a subcommand omits ``--feature``."""
        base = self.repo_root / ".codeconv" / "marathon"
        if not base.is_dir():
            return []
        return sorted(
            p.name
            for p in base.iterdir()
            if p.is_dir() and (p / "marathon.json").is_file()
        )

    def upsert_block(self, b: StageBlock) -> StageBlock:
        """Create or update a stage-block row (idempotent by id). Mirrors
        ``blocks/<safe block id>.json``."""
        engine = self._primary()
        if engine is not None:
            from sqlalchemy import text

            with engine.begin() as conn:
                conn.execute(
                    text(
                        "INSERT INTO marathon.stage_blocks "
                        "(id, marathon_id, stage, block_kind, ordinal, "
                        " workflow_run_id, status, last_sequence_no, started_at, "
                        " completed_at) "
                        "VALUES (:id, :mid, :stage, :kind, :ordinal, :run, :status, "
                        " :lastseq, :started, :completed) "
                        "ON CONFLICT (id) DO UPDATE SET "
                        " workflow_run_id = EXCLUDED.workflow_run_id, "
                        " status = EXCLUDED.status, "
                        " last_sequence_no = GREATEST("
                        "   marathon.stage_blocks.last_sequence_no, EXCLUDED.last_sequence_no), "
                        " started_at = COALESCE(marathon.stage_blocks.started_at, EXCLUDED.started_at), "
                        " completed_at = EXCLUDED.completed_at"
                    ),
                    {
                        "id": b.id,
                        "mid": b.marathon_id,
                        "stage": b.stage,
                        "kind": b.block_kind,
                        "ordinal": b.ordinal,
                        "run": b.workflow_run_id,
                        "status": b.status,
                        "lastseq": b.last_sequence_no,
                        "started": b.started_at,
                        "completed": b.completed_at,
                    },
                )
        _atomic_write_json(
            self._marathon_dir(b.marathon_id)
            / "blocks"
            / f"{_safe_filename(b.id)}.json",
            _block_to_dict(b),
        )
        return b

    def read_block(self, block_id: str) -> Optional[StageBlock]:
        marathon_id = marathon_id_of_block(block_id)
        engine = self._primary()
        if engine is not None:
            from sqlalchemy import text

            with engine.connect() as conn:
                row = conn.execute(
                    text(
                        "SELECT id, marathon_id, stage, block_kind, ordinal, "
                        "workflow_run_id, status, last_sequence_no, started_at, "
                        "completed_at FROM marathon.stage_blocks WHERE id = :id"
                    ),
                    {"id": block_id},
                ).one_or_none()
            if row is not None:
                return StageBlock(
                    id=row.id,
                    marathon_id=row.marathon_id,
                    stage=row.stage,
                    block_kind=row.block_kind,
                    ordinal=int(row.ordinal),
                    workflow_run_id=row.workflow_run_id,
                    status=row.status,
                    last_sequence_no=int(row.last_sequence_no),
                    started_at=row.started_at,
                    completed_at=row.completed_at,
                )
        path = (
            self._marathon_dir(marathon_id)
            / "blocks"
            / f"{_safe_filename(block_id)}.json"
        )
        if path.is_file():
            try:
                return _dict_to_block(json.loads(path.read_text(encoding="utf-8")))
            except (OSError, ValueError):
                return None
        return None

    def _fallback_block(self, block_id: str) -> Optional[StageBlock]:
        """Read a block strictly from the JSON mirror (used by reconcile to
        recover a block created during a primary outage)."""
        marathon_id = marathon_id_of_block(block_id)
        path = (
            self._marathon_dir(marathon_id)
            / "blocks"
            / f"{_safe_filename(block_id)}.json"
        )
        if not path.is_file():
            return None
        try:
            return _dict_to_block(json.loads(path.read_text(encoding="utf-8")))
        except (OSError, ValueError):
            return None

    def _ensure_block_in_primary(self, block_id: str) -> None:
        """Guarantee a checkpoint's parent marathon + block rows exist in the
        primary before the checkpoint is inserted there (reconcile fast-forward
        FK safety)."""
        if self._primary() is None:
            return
        blk = self._fallback_block(block_id)
        if blk is None:
            return
        m = self.read_marathon(blk.marathon_id)
        if m is not None:
            self.upsert_marathon(m)
        self.upsert_block(blk)

    # --- read primitives ---------------------------------------------------

    def _primary_checkpoints(self, marathon_id: str) -> list[Checkpoint]:
        engine = self._primary()
        if engine is None:
            return []
        from sqlalchemy import text

        with engine.connect() as conn:
            rows = conn.execute(
                text(
                    "SELECT id, block_id, marathon_id, sequence_no, stage, wip_unit, "
                    "completed_units, remaining_units, workflow_run_id, budget_spent, "
                    "store_origin, created_at "
                    "FROM marathon.checkpoints WHERE marathon_id = :mid "
                    "ORDER BY sequence_no"
                ),
                {"mid": marathon_id},
            ).all()
        return [self._row_to_checkpoint(r) for r in rows]

    @staticmethod
    def _row_to_checkpoint(r: Any) -> Checkpoint:
        return Checkpoint(
            id=r.id,
            block_id=r.block_id,
            marathon_id=r.marathon_id,
            sequence_no=int(r.sequence_no),
            stage=r.stage,
            wip_unit=r.wip_unit,
            completed_units=_as_list(r.completed_units),
            remaining_units=_as_list(r.remaining_units),
            workflow_run_id=r.workflow_run_id,
            budget_spent=int(r.budget_spent),
            store_origin=r.store_origin,
            created_at=r.created_at,
        )

    def _fallback_checkpoints(self, marathon_id: str) -> list[Checkpoint]:
        cdir = self._checkpoints_dir(marathon_id)
        if not cdir.is_dir():
            return []
        out: list[Checkpoint] = []
        for fp in cdir.glob("*.json"):
            try:
                d = json.loads(fp.read_text(encoding="utf-8"))
            except (OSError, ValueError):
                continue
            out.append(_dict_to_checkpoint(d))
        out.sort(key=lambda c: c.sequence_no)
        return out

    def _max_seq(self, checkpoints: list[Checkpoint]) -> int:
        return max((c.sequence_no for c in checkpoints), default=0)

    # --- write primitives --------------------------------------------------

    def _append_primary(self, cp: Checkpoint) -> Checkpoint:
        """Insert one checkpoint row with an EXPLICIT ``sequence_no`` (append-
        only; allocation happened in :meth:`write_checkpoint`). Also advances
        the block's ``last_sequence_no``. Returns the row with its id +
        ``created_at`` filled."""
        engine = self._primary()
        if engine is None:  # pragma: no cover - guarded by callers
            raise RuntimeError("primary store unavailable")
        from sqlalchemy import text

        with engine.begin() as conn:
            row = conn.execute(
                text(
                    "INSERT INTO marathon.checkpoints "
                    "(block_id, marathon_id, sequence_no, stage, wip_unit, "
                    " completed_units, remaining_units, workflow_run_id, "
                    " budget_spent, store_origin, created_at) "
                    "VALUES (:block_id, :marathon_id, :sequence_no, :stage, :wip_unit, "
                    " CAST(:completed_units AS jsonb), CAST(:remaining_units AS jsonb), "
                    " :workflow_run_id, :budget_spent, :store_origin, "
                    " COALESCE(:created_at, NOW())) "
                    "RETURNING id, created_at"
                ),
                {
                    "block_id": cp.block_id,
                    "marathon_id": cp.marathon_id,
                    "sequence_no": cp.sequence_no,
                    "stage": cp.stage,
                    "wip_unit": cp.wip_unit,
                    "completed_units": json.dumps(cp.completed_units),
                    "remaining_units": json.dumps(cp.remaining_units),
                    "workflow_run_id": cp.workflow_run_id,
                    "budget_spent": cp.budget_spent,
                    "store_origin": cp.store_origin,
                    "created_at": cp.created_at,
                },
            ).one()
            conn.execute(
                text(
                    "UPDATE marathon.stage_blocks "
                    "SET last_sequence_no = GREATEST(last_sequence_no, :seq) "
                    "WHERE id = :block_id"
                ),
                {"seq": cp.sequence_no, "block_id": cp.block_id},
            )
        cp.id = int(row.id)
        cp.created_at = row.created_at
        return cp

    def _append_fallback(self, cp: Checkpoint) -> Checkpoint:
        """Mirror/append one checkpoint as a JSON file named by its seq."""
        path = self._checkpoints_dir(cp.marathon_id) / f"{cp.sequence_no}.json"
        _atomic_write_json(path, _checkpoint_to_dict(cp))
        return cp

    # --- public interface (contracts/checkpoint-store.md) ------------------

    def write_checkpoint(
        self,
        block_id: str,
        *,
        stage: str,
        wip_unit: Optional[str],
        completed_units: list[Any],
        remaining_units: list[Any],
        workflow_run_id: Optional[str],
        budget_spent: int,
    ) -> Checkpoint:
        """Allocate the next marathon-wide ``sequence_no``, append to the
        active store, and (when the primary is reachable) mirror to fallback
        at the same seq. Append-only; never overwrites (I2)."""
        marathon_id = marathon_id_of_block(block_id)
        primary = self._primary()
        primary_cps = self._primary_checkpoints(marathon_id) if primary is not None else []
        fallback_cps = self._fallback_checkpoints(marathon_id)
        next_seq = max(self._max_seq(primary_cps), self._max_seq(fallback_cps)) + 1
        origin = "primary" if primary is not None else "fallback"
        cp = Checkpoint(
            block_id=block_id,
            marathon_id=marathon_id,
            sequence_no=next_seq,
            stage=stage,
            wip_unit=wip_unit,
            completed_units=list(completed_units),
            remaining_units=list(remaining_units),
            workflow_run_id=workflow_run_id,
            budget_spent=budget_spent,
            store_origin=origin,
            created_at=None,
        )
        if primary is not None:
            cp = self._append_primary(cp)
            self._append_fallback(cp)  # mirror at the same seq + created_at
        else:
            cp.created_at = _now()
            self._append_fallback(cp)
        return cp

    def checkpoints(self, marathon_id: str) -> list[Checkpoint]:
        """The full append-only checkpoint history (union of both stores,
        deduped by seq, ascending). Every attempt — failed or successful — is
        retained (FR-008/I2)."""
        merged: dict[int, Checkpoint] = {}
        for cp in self._fallback_checkpoints(marathon_id):
            merged[cp.sequence_no] = cp
        for cp in self._primary_checkpoints(marathon_id):
            merged.setdefault(cp.sequence_no, cp)
        return [merged[s] for s in sorted(merged)]

    def read_position(self, marathon_id: str) -> Optional[Position]:
        """The objective resume position: the max(sequence_no) checkpoint
        across whichever store(s) are reachable (I3). ``None`` = cold start."""
        all_cps = self._primary_checkpoints(marathon_id) + self._fallback_checkpoints(
            marathon_id
        )
        if not all_cps:
            return None
        cp = max(all_cps, key=lambda c: c.sequence_no)
        return Position(
            marathon_id=marathon_id,
            block_id=cp.block_id,
            stage=cp.stage,
            sequence_no=cp.sequence_no,
            completed_units=cp.completed_units,
            remaining_units=cp.remaining_units,
            wip_unit=cp.wip_unit,
            workflow_run_id=cp.workflow_run_id,
            budget_spent=cp.budget_spent,
            store_origin=cp.store_origin,
        )

    def reconcile(self, marathon_id: str) -> ReconcileResult:
        """Compare the two stores by ``sequence_no`` (D5):

        - equal contents → ``in_sync``;
        - one store missing non-conflicting rows the other has → fast-forward
          (copy them across); winner = the higher-max store;
        - the same seq present in BOTH with different content → ``fork`` →
          write a ``store_divergence`` escalation and stop (I7).
        """
        primary = self._primary()
        fallback_cps = {
            c.sequence_no: c for c in self._fallback_checkpoints(marathon_id)
        }
        if primary is None:
            # Nothing to reconcile against — you reconcile when the primary is
            # back. The JSON store is authoritative meanwhile (FR-020).
            return ReconcileResult(
                status="in_sync", primary_seq=0, fallback_seq=max(fallback_cps, default=0)
            )
        primary_cps = {c.sequence_no: c for c in self._primary_checkpoints(marathon_id)}
        pmax = max(primary_cps, default=0)
        fmax = max(fallback_cps, default=0)

        conflicts = [
            seq
            for seq in set(primary_cps) & set(fallback_cps)
            if _checkpoint_signature(primary_cps[seq])
            != _checkpoint_signature(fallback_cps[seq])
        ]
        if conflicts:
            esc_id = self._escalate_fork(marathon_id, sorted(conflicts), pmax, fmax)
            return ReconcileResult(
                status="fork",
                primary_seq=pmax,
                fallback_seq=fmax,
                escalation_id=esc_id,
            )

        missing_in_primary = set(fallback_cps) - set(primary_cps)
        missing_in_fallback = set(primary_cps) - set(fallback_cps)

        if not missing_in_primary and not missing_in_fallback:
            return ReconcileResult(status="in_sync", primary_seq=pmax, fallback_seq=fmax)

        # Fast-forward: copy each store's missing rows from the other. A row
        # copied INTO the primary needs its parent block to exist there first
        # (a block created entirely during a fallback outage lives only in
        # JSON) — ensure it, or the checkpoint FK fails.
        for seq in sorted(missing_in_primary):
            cp = fallback_cps[seq]
            self._ensure_block_in_primary(cp.block_id)
            self._append_primary(cp)
        for seq in sorted(missing_in_fallback):
            self._append_fallback(primary_cps[seq])

        winner = "fallback" if fmax > pmax else "primary"
        return ReconcileResult(
            status="fast_forward",
            primary_seq=max(pmax, fmax),
            fallback_seq=max(pmax, fmax),
            winner=winner,
        )

    def _escalate_fork(
        self, marathon_id: str, conflicts: list[int], pmax: int, fmax: int
    ) -> Optional[int]:
        """Write a ``store_divergence`` escalation row (best-effort to the
        primary; always to the JSON mirror so the divergence is durable even
        when the bridge is the thing that's flapping)."""
        from .escalation import write_escalation

        return write_escalation(
            self,
            marathon_id,
            kind="store_divergence",
            detail={
                "conflicting_sequence_nos": conflicts,
                "primary_max": pmax,
                "fallback_max": fmax,
            },
        )


# --- JSON (de)serialization for the fallback mirror -------------------------


def _iso(dt: Optional[datetime]) -> Optional[str]:
    return dt.isoformat() if dt else None


def _parse_dt(value: Any) -> Optional[datetime]:
    return datetime.fromisoformat(value) if value else None


def _marathon_to_dict(m: Marathon) -> dict[str, Any]:
    return {
        "id": m.id,
        "feature_slug": m.feature_slug,
        "feature_branch": m.feature_branch,
        "budget_ceiling": m.budget_ceiling,
        "budget_spent": m.budget_spent,
        "preauth_commit_push": m.preauth_commit_push,
        "preauth_workflow_optin": m.preauth_workflow_optin,
        "preauth_revoked_at": _iso(m.preauth_revoked_at),
        "auto_mode": m.auto_mode,
        "created_at": _iso(m.created_at),
    }


def _dict_to_marathon(d: dict[str, Any]) -> Marathon:
    return Marathon(
        id=d["id"],
        feature_slug=d["feature_slug"],
        feature_branch=d["feature_branch"],
        budget_ceiling=d.get("budget_ceiling"),
        budget_spent=int(d.get("budget_spent", 0)),
        preauth_commit_push=bool(d.get("preauth_commit_push", False)),
        preauth_workflow_optin=bool(d.get("preauth_workflow_optin", False)),
        preauth_revoked_at=_parse_dt(d.get("preauth_revoked_at")),
        auto_mode=bool(d.get("auto_mode", False)),
        created_at=_parse_dt(d.get("created_at")),
    )


def _block_to_dict(b: StageBlock) -> dict[str, Any]:
    return {
        "id": b.id,
        "marathon_id": b.marathon_id,
        "stage": b.stage,
        "block_kind": b.block_kind,
        "ordinal": b.ordinal,
        "workflow_run_id": b.workflow_run_id,
        "status": b.status,
        "last_sequence_no": b.last_sequence_no,
        "started_at": _iso(b.started_at),
        "completed_at": _iso(b.completed_at),
    }


def _dict_to_block(d: dict[str, Any]) -> StageBlock:
    return StageBlock(
        id=d["id"],
        marathon_id=d["marathon_id"],
        stage=d["stage"],
        block_kind=d["block_kind"],
        ordinal=int(d["ordinal"]),
        workflow_run_id=d.get("workflow_run_id"),
        status=d.get("status", "pending"),
        last_sequence_no=int(d.get("last_sequence_no", 0)),
        started_at=_parse_dt(d.get("started_at")),
        completed_at=_parse_dt(d.get("completed_at")),
    )


def _checkpoint_to_dict(cp: Checkpoint) -> dict[str, Any]:
    return {
        "id": cp.id,
        "block_id": cp.block_id,
        "marathon_id": cp.marathon_id,
        "sequence_no": cp.sequence_no,
        "stage": cp.stage,
        "wip_unit": cp.wip_unit,
        "completed_units": cp.completed_units,
        "remaining_units": cp.remaining_units,
        "workflow_run_id": cp.workflow_run_id,
        "budget_spent": cp.budget_spent,
        "store_origin": cp.store_origin,
        "created_at": cp.created_at.isoformat() if cp.created_at else None,
    }


def _dict_to_checkpoint(d: dict[str, Any]) -> Checkpoint:
    created = d.get("created_at")
    return Checkpoint(
        id=d.get("id"),
        block_id=d["block_id"],
        marathon_id=d["marathon_id"],
        sequence_no=int(d["sequence_no"]),
        stage=d["stage"],
        wip_unit=d.get("wip_unit"),
        completed_units=_as_list(d.get("completed_units")),
        remaining_units=_as_list(d.get("remaining_units")),
        workflow_run_id=d.get("workflow_run_id"),
        budget_spent=int(d.get("budget_spent", 0)),
        store_origin=d.get("store_origin", "fallback"),
        created_at=datetime.fromisoformat(created) if created else None,
    )


def _as_list(value: Any) -> list[Any]:
    if value is None:
        return []
    if isinstance(value, str):
        try:
            value = json.loads(value)
        except ValueError:
            return [value]
    return list(value) if isinstance(value, (list, tuple)) else [value]


__all__ = ["MarathonStore", "marathon_id_of_block"]

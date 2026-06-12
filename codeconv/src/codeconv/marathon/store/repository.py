"""Single-writer data access for the per-run store (T007–T010, T049).

Composes the per-run bridge (D1, FR-028): :func:`_connect` →
``codeconv.db.engine.connect(repo_root=store_root, data_dir=store_root/'pgdb')``
(the canonical accessor over ``bridge_client.acquire_or_discover`` — single-
writer SQLAlchemy engine, ``pool_size=1`` via ``pglite_engine_kwargs``) →
``ensure_schema`` once per env (idempotent anyway).

Owns CRUD for ``run``/``stage``/``checkpoint``/``item``/``issue`` (T008; all
writes inside ``engine.begin()`` transactions), the **JSON-mirror dual-write**
(T009: every write also serialises to ``<store_root>/json/<entity>/…`` per
``data-model.md``; reads prefer primary and fall back to the mirror when the
per-run bridge is unreachable — FR-020 carry-over, FR-027 mirror), and
**monotonic checkpoint sequencing** (T010: ``next_sequence_no =
max(primary_max, fallback_max) + 1``, UNIQUE ``(run_id, sequence_no)``,
``store_origin`` records which store wrote the row — D6, contract I1).

Fallback **writes** are supported for checkpoints only (the append-only resume
substrate that must survive a transient bridge outage); other entities raise
:class:`StoreUnavailable` so the condition surfaces instead of being masked.
``reconcile(run_id)`` (PGLite ↔ JSON mirror; fast-forward | fork | in_sync —
never silently picks) lands with US5 (T049).

NOTE — mirror layout: ``data-model.md``'s store-root tree omits an ``issues/``
dir while T009 mandates mirroring *every* write; ``issue`` rows are mirrored
to ``json/issues/<id>.json`` (the normative "every write" reading; flagged as
a doc nit on the data-model tree).
"""

from __future__ import annotations

import json
from dataclasses import asdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Mapping, Optional

import portalocker
from sqlalchemy import text
from sqlalchemy.engine import Engine

from codeconv.bridge_client import DataDirFilesystemError
from codeconv.db import engine as db_engine
from codeconv.marathon.models import (
    CheckpointRow,
    Escalation,
    Issue,
    Item,
    MarathonEnv,
    MarathonRun,
    StageRow,
    StatusReport,
)
from codeconv.marathon.store.schema import ensure_schema

__all__ = [
    "ConcurrentWriter",
    "IntegrityFailure",
    "Repository",
    "StoreUnavailable",
    "acquire_writer_lock",
    "reconcile",
    "release_writer_lock",
    "row_payload",
]


class StoreUnavailable(RuntimeError):
    """The per-run primary store (PGLite bridge) cannot be reached.

    Distinct from a recoverable stale-residue condition and from the
    filesystem guard (``DataDirFilesystemError``, exit 64) — FR-016.
    """


class IntegrityFailure(RuntimeError):
    """The store answered but its content violates an invariant (FR-016)."""


class ConcurrentWriter(RuntimeError):
    """A second concurrent marathon writer was refused (FR-015).

    Raised when another live process holds the per-run writer lock. The
    message is deliberately distinct from recoverable residue (FR-016):
    the kernel releases the lock on process exit, so a crashed writer never
    leaves this condition behind."""


# --- single-writer lock (T031; kernel-fd, per-process, per store root) ---------
# Acquired lazily on the FIRST write and held for process lifetime (released
# on process exit by the kernel, or explicitly by the keeper's graceful stop).
# Sequential short-lived writers therefore serialise naturally; concurrent
# ones are refused. Reads never take the lock (single-WRITER, not -reader).

_WRITER_LOCKS: dict[str, Any] = {}


def acquire_writer_lock(env: MarathonEnv) -> None:
    key = str(env.store_root.resolve())
    if key in _WRITER_LOCKS:
        return
    lock_path = env.store_root / "writer.lock"
    lock = portalocker.Lock(
        str(lock_path),
        mode="ab",
        timeout=0,
        flags=portalocker.LOCK_EX | portalocker.LOCK_NB,
    )
    try:
        lock.acquire()
    except (
        portalocker.exceptions.LockException,
        portalocker.exceptions.AlreadyLocked,
        OSError,
    ) as exc:
        raise ConcurrentWriter(
            f"second concurrent writer refused: another live marathon process "
            f"holds the writer lock at {lock_path} (FR-015). This is not "
            f"recoverable residue — the kernel releases the lock when the "
            f"other writer exits; stop it or wait, do not delete anything."
        ) from exc
    _WRITER_LOCKS[key] = lock


def release_writer_lock(env: MarathonEnv) -> None:
    """Explicit release for the keeper's graceful stop (tests / handover);
    otherwise the kernel releases on process exit."""
    lock = _WRITER_LOCKS.pop(str(env.store_root.resolve()), None)
    if lock is not None:
        try:
            lock.release()
        except Exception:
            pass


# --- serialisation ------------------------------------------------------------


def _jsonable(value: Any) -> Any:
    if isinstance(value, datetime):
        return value.isoformat()
    if isinstance(value, Path):
        return str(value)
    return value


def row_payload(row: Any) -> dict[str, Any]:
    """The canonical JSON-mirror payload of a row dataclass (T009/T011)."""
    return {k: _jsonable(v) for k, v in asdict(row).items()}


# --- connection (T007) ----------------------------------------------------------


def _connect(env: MarathonEnv) -> Engine:
    """Acquire the per-run bridge and return the single-writer engine.

    Caches on ``env.engine`` so ``ensure_schema`` runs once per env. The
    filesystem guard propagates unchanged (exit-64 semantics); every other
    acquisition failure surfaces as :class:`StoreUnavailable` with the
    original cause chained.
    """
    if env.engine is not None:
        return env.engine
    try:
        engine = db_engine.connect(
            env.store_root,
            data_dir=env.store_root / "pgdb",
            application_name="codeconv-marathon",
        )
        ensure_schema(engine)
    except DataDirFilesystemError:
        raise
    except Exception as exc:
        raise StoreUnavailable(
            f"per-run marathon store at {env.store_root} is unreachable "
            f"(bridge acquisition/schema failed: {exc})"
        ) from exc
    env.engine = engine
    return engine


# --- column lists (RETURNING surfaces; field order mirrors the schema) ---------

_RUN_COLS = (
    "id, title, status, head_commit, budget_ceiling, budget_spent, budget_unit, "
    "preauth_commit_push, preauth_workflow_optin, preauth_revoked_at, auto_mode, "
    "created_at"
)
_STAGE_COLS = (
    "id, run_id, stage_index, order_key, name, origin, item_id, mini_kind, "
    "status, started_at, completed_at, last_sequence_no"
)
_CHECKPOINT_COLS = (
    "id, run_id, stage_id, sequence_no, wip_unit, completed_units, "
    "remaining_units, budget_spent, committed_paths, commit_sha, pushed, "
    "push_escalation, workflow_run_id, store_origin, created_at"
)
_ITEM_COLS = (
    "id, run_id, kind, title, description, blocks_stage_id, status, "
    "artifacts_dir, created_at"
)
_ISSUE_COLS = "id, run_id, stage_id, summary, status, created_at"
_ESCALATION_COLS = "id, run_id, stage_id, kind, detail, resolved_at, created_at"
_STATUS_COLS = (
    "id, run_id, stage_id, done, issues, tokens_spent, tokens_remaining, "
    "todo, created_at"
)

_RUN_UPDATABLE = frozenset(
    {
        "title",
        "status",
        "head_commit",
        "budget_ceiling",
        "budget_spent",
        "budget_unit",
        "preauth_commit_push",
        "preauth_workflow_optin",
        "preauth_revoked_at",
        "auto_mode",
    }
)
_STAGE_UPDATABLE = frozenset(
    {"order_key", "status", "started_at", "completed_at", "last_sequence_no"}
)
_CHECKPOINT_UPDATABLE = frozenset(
    {"commit_sha", "pushed", "push_escalation", "workflow_run_id"}
)
_ITEM_UPDATABLE = frozenset(
    {"title", "description", "blocks_stage_id", "status", "artifacts_dir"}
)
_ISSUE_UPDATABLE = frozenset({"summary", "stage_id", "status"})


class Repository:
    """Single-writer CRUD over the per-run store with JSON-mirror dual-write.

    One instance per :class:`MarathonEnv`. Every write happens inside one
    ``engine.begin()`` transaction and then mirrors the *returned* row to
    ``<store_root>/json/…`` so primary and mirror always carry identical
    content (T011's parity assertion).
    """

    def __init__(self, env: MarathonEnv, *, autostart: bool = True) -> None:
        self.env = env
        # autostart=False = read-only diagnostics mode (keeper's `doctor`):
        # never spawn/acquire a bridge; use a pre-set env.engine or the mirror.
        self.autostart = autostart

    # --- connection -----------------------------------------------------------

    def engine(self) -> Engine:
        if not self.autostart:
            if self.env.engine is None:
                raise StoreUnavailable(
                    f"per-run store at {self.env.store_root}: no live engine "
                    f"and autostart is disabled (read-only diagnostics)"
                )
            return self.env.engine
        return _connect(self.env)

    def _engine_or_none(self) -> Optional[Engine]:
        try:
            return self.engine()
        except StoreUnavailable:
            return None

    def _ensure_writer(self) -> None:
        acquire_writer_lock(self.env)

    # --- JSON mirror (T009) ----------------------------------------------------

    @property
    def json_root(self) -> Path:
        return self.env.store_root / "json"

    def _write_mirror(self, rel: str, payload: Mapping[str, Any]) -> None:
        path = self.json_root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(
            json.dumps(payload, indent=2, sort_keys=True), encoding="utf-8"
        )

    def _read_mirror(self, rel: str) -> Optional[dict[str, Any]]:
        path = self.json_root / rel
        if not path.is_file():
            return None
        try:
            return json.loads(path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            return None

    def _read_mirror_dir(self, sub: str) -> list[dict[str, Any]]:
        directory = self.json_root / sub
        if not directory.is_dir():
            return []
        payloads: list[dict[str, Any]] = []
        for path in sorted(directory.glob("*.json")):
            try:
                payloads.append(json.loads(path.read_text(encoding="utf-8")))
            except (OSError, ValueError):
                continue
        return payloads

    # --- run --------------------------------------------------------------------

    def upsert_run(self, run: MarathonRun) -> MarathonRun:
        """Idempotent insert: re-attaching an existing run returns it unchanged
        (data-model: "Idempotent upsert on start"); it never clobbers durable
        state with re-register arguments."""
        self._ensure_writer()
        eng = self.engine()
        with eng.begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO marathon.run "
                    "(id, title, status, head_commit, budget_ceiling, budget_spent, "
                    " budget_unit, preauth_commit_push, preauth_workflow_optin, "
                    " preauth_revoked_at, auto_mode) "
                    "VALUES (:id, :title, :status, :head_commit, :budget_ceiling, "
                    " :budget_spent, :budget_unit, :preauth_commit_push, "
                    " :preauth_workflow_optin, :preauth_revoked_at, :auto_mode) "
                    "ON CONFLICT (id) DO NOTHING"
                ),
                {
                    "id": run.id,
                    "title": run.title,
                    "status": run.status,
                    "head_commit": run.head_commit,
                    "budget_ceiling": run.budget_ceiling,
                    "budget_spent": run.budget_spent,
                    "budget_unit": run.budget_unit,
                    "preauth_commit_push": run.preauth_commit_push,
                    "preauth_workflow_optin": run.preauth_workflow_optin,
                    "preauth_revoked_at": run.preauth_revoked_at,
                    "auto_mode": run.auto_mode,
                },
            )
            row = (
                conn.execute(
                    text(
                        f"SELECT {_RUN_COLS} FROM marathon.run WHERE id = :id"
                    ),
                    {"id": run.id},
                )
                .mappings()
                .one()
            )
        out = MarathonRun(**dict(row))
        self._write_mirror("run.json", row_payload(out))
        return out

    def get_run(self, run_id: str) -> Optional[MarathonRun]:
        eng = self._engine_or_none()
        if eng is not None:
            with eng.connect() as conn:
                row = (
                    conn.execute(
                        text(
                            f"SELECT {_RUN_COLS} FROM marathon.run WHERE id = :id"
                        ),
                        {"id": run_id},
                    )
                    .mappings()
                    .one_or_none()
                )
            return MarathonRun(**dict(row)) if row is not None else None
        payload = self._read_mirror("run.json")
        if payload is not None and payload.get("id") == run_id:
            return MarathonRun(**payload)
        return None

    def update_run(self, run_id: str, **fields: Any) -> MarathonRun:
        row = self._update(
            "run", "id", run_id, fields, _RUN_UPDATABLE, _RUN_COLS
        )
        out = MarathonRun(**dict(row))
        self._write_mirror("run.json", row_payload(out))
        return out

    # --- stage --------------------------------------------------------------------

    def insert_stage(self, stage: StageRow) -> StageRow:
        self._ensure_writer()
        eng = self.engine()
        with eng.begin() as conn:
            row = (
                conn.execute(
                    text(
                        "INSERT INTO marathon.stage "
                        "(run_id, stage_index, order_key, name, origin, item_id, "
                        " mini_kind, status, started_at, completed_at, "
                        " last_sequence_no) "
                        "VALUES (:run_id, :stage_index, :order_key, :name, :origin, "
                        " :item_id, :mini_kind, :status, :started_at, :completed_at, "
                        " :last_sequence_no) "
                        f"RETURNING {_STAGE_COLS}"
                    ),
                    {
                        "run_id": stage.run_id,
                        "stage_index": stage.stage_index,
                        "order_key": stage.order_key,
                        "name": stage.name,
                        "origin": stage.origin,
                        "item_id": stage.item_id,
                        "mini_kind": stage.mini_kind,
                        "status": stage.status,
                        "started_at": stage.started_at,
                        "completed_at": stage.completed_at,
                        "last_sequence_no": stage.last_sequence_no,
                    },
                )
                .mappings()
                .one()
            )
        out = StageRow(**dict(row))
        self._write_mirror(f"stages/{out.stage_index}.json", row_payload(out))
        return out

    def get_stage(self, run_id: str, name: str) -> Optional[StageRow]:
        for stage in self.list_stages(run_id):
            if stage.name == name:
                return stage
        return None

    def list_stages(self, run_id: str) -> list[StageRow]:
        """All stages for the run, ordered by ``(order_key, stage_index)``
        (resume-position.md rule 1)."""
        eng = self._engine_or_none()
        if eng is not None:
            with eng.connect() as conn:
                rows = (
                    conn.execute(
                        text(
                            f"SELECT {_STAGE_COLS} FROM marathon.stage "
                            "WHERE run_id = :run_id "
                            "ORDER BY order_key, stage_index"
                        ),
                        {"run_id": run_id},
                    )
                    .mappings()
                    .all()
                )
            return [StageRow(**dict(r)) for r in rows]
        payloads = [
            p for p in self._read_mirror_dir("stages") if p.get("run_id") == run_id
        ]
        stages = [StageRow(**p) for p in payloads]
        stages.sort(key=lambda s: (s.order_key, s.stage_index))
        return stages

    def update_stage(self, stage_id: int, **fields: Any) -> StageRow:
        row = self._update(
            "stage", "id", stage_id, fields, _STAGE_UPDATABLE, _STAGE_COLS
        )
        out = StageRow(**dict(row))
        self._write_mirror(f"stages/{out.stage_index}.json", row_payload(out))
        return out

    # --- checkpoint (T010 monotonic sequencing) --------------------------------------

    def next_sequence_no(self, run_id: str) -> int:
        """``max(primary_max, fallback_max) + 1`` (D6, contract I1)."""
        primary_max = 0
        eng = self._engine_or_none()
        if eng is not None:
            with eng.connect() as conn:
                primary_max = conn.execute(
                    text(
                        "SELECT COALESCE(MAX(sequence_no), 0) "
                        "FROM marathon.checkpoint WHERE run_id = :run_id"
                    ),
                    {"run_id": run_id},
                ).scalar_one()
        return max(int(primary_max), self._fallback_max_sequence(run_id)) + 1

    def _fallback_max_sequence(self, run_id: str) -> int:
        max_seq = 0
        for payload in self._read_mirror_dir("checkpoints"):
            if payload.get("run_id") != run_id:
                continue
            try:
                max_seq = max(max_seq, int(payload.get("sequence_no", 0)))
            except (TypeError, ValueError):
                continue
        return max_seq

    def insert_checkpoint(self, cp: CheckpointRow) -> CheckpointRow:
        """Append a checkpoint. Allocates ``sequence_no`` when unset; writes to
        the primary and mirrors, or — when the per-run bridge is unreachable —
        writes the mirror alone with ``store_origin='fallback'`` so progress
        survives the outage and ``reconcile`` can fast-forward later (D6)."""
        self._ensure_writer()
        if not cp.sequence_no:
            cp.sequence_no = self.next_sequence_no(cp.run_id)
        eng = self._engine_or_none()
        if eng is None:
            cp.store_origin = "fallback"
            if cp.created_at is None:
                cp.created_at = datetime.now(timezone.utc)
            self._write_mirror(
                f"checkpoints/{cp.sequence_no}.json", row_payload(cp)
            )
            return cp
        cp.store_origin = "primary"
        with eng.begin() as conn:
            row = (
                conn.execute(
                    text(
                        "INSERT INTO marathon.checkpoint "
                        "(run_id, stage_id, sequence_no, wip_unit, completed_units, "
                        " remaining_units, budget_spent, committed_paths, commit_sha, "
                        " pushed, push_escalation, workflow_run_id, store_origin) "
                        "VALUES (:run_id, :stage_id, :sequence_no, :wip_unit, "
                        " CAST(:completed_units AS jsonb), "
                        " CAST(:remaining_units AS jsonb), :budget_spent, "
                        " CAST(:committed_paths AS jsonb), :commit_sha, :pushed, "
                        " :push_escalation, :workflow_run_id, :store_origin) "
                        f"RETURNING {_CHECKPOINT_COLS}"
                    ),
                    {
                        "run_id": cp.run_id,
                        "stage_id": cp.stage_id,
                        "sequence_no": cp.sequence_no,
                        "wip_unit": cp.wip_unit,
                        "completed_units": json.dumps(cp.completed_units),
                        "remaining_units": json.dumps(cp.remaining_units),
                        "budget_spent": cp.budget_spent,
                        "committed_paths": json.dumps(cp.committed_paths),
                        "commit_sha": cp.commit_sha,
                        "pushed": cp.pushed,
                        "push_escalation": cp.push_escalation,
                        "workflow_run_id": cp.workflow_run_id,
                        "store_origin": cp.store_origin,
                    },
                )
                .mappings()
                .one()
            )
        out = CheckpointRow(**dict(row))
        self._write_mirror(f"checkpoints/{out.sequence_no}.json", row_payload(out))
        return out

    def list_checkpoints(self, run_id: str) -> list[CheckpointRow]:
        eng = self._engine_or_none()
        if eng is not None:
            with eng.connect() as conn:
                rows = (
                    conn.execute(
                        text(
                            f"SELECT {_CHECKPOINT_COLS} FROM marathon.checkpoint "
                            "WHERE run_id = :run_id ORDER BY sequence_no"
                        ),
                        {"run_id": run_id},
                    )
                    .mappings()
                    .all()
                )
            return [CheckpointRow(**dict(r)) for r in rows]
        payloads = [
            p
            for p in self._read_mirror_dir("checkpoints")
            if p.get("run_id") == run_id
        ]
        cps = [CheckpointRow(**p) for p in payloads]
        cps.sort(key=lambda c: c.sequence_no)
        return cps

    def update_checkpoint(self, checkpoint_id: int, **fields: Any) -> CheckpointRow:
        row = self._update(
            "checkpoint",
            "id",
            checkpoint_id,
            fields,
            _CHECKPOINT_UPDATABLE,
            _CHECKPOINT_COLS,
        )
        out = CheckpointRow(**dict(row))
        self._write_mirror(f"checkpoints/{out.sequence_no}.json", row_payload(out))
        return out

    # --- item --------------------------------------------------------------------

    def insert_item(self, item: Item) -> Item:
        """Insert an emergent-work item. When ``artifacts_dir`` is empty it is
        defaulted to ``<store_root>/items/<id>`` inside the same transaction
        (the id is only known post-insert; creating the directory itself is
        intake's job — T021/FR-008)."""
        self._ensure_writer()
        eng = self.engine()
        with eng.begin() as conn:
            row = (
                conn.execute(
                    text(
                        "INSERT INTO marathon.item "
                        "(run_id, kind, title, description, blocks_stage_id, "
                        " status, artifacts_dir) "
                        "VALUES (:run_id, :kind, :title, :description, "
                        " :blocks_stage_id, :status, :artifacts_dir) "
                        f"RETURNING {_ITEM_COLS}"
                    ),
                    {
                        "run_id": item.run_id,
                        "kind": item.kind,
                        "title": item.title,
                        "description": item.description,
                        "blocks_stage_id": item.blocks_stage_id,
                        "status": item.status,
                        "artifacts_dir": item.artifacts_dir or "",
                    },
                )
                .mappings()
                .one()
            )
            if not row["artifacts_dir"]:
                artifacts = str(self.env.store_root / "items" / str(row["id"]))
                row = (
                    conn.execute(
                        text(
                            "UPDATE marathon.item SET artifacts_dir = :a "
                            f"WHERE id = :i RETURNING {_ITEM_COLS}"
                        ),
                        {"a": artifacts, "i": row["id"]},
                    )
                    .mappings()
                    .one()
                )
        out = Item(**dict(row))
        self._write_mirror(f"items/{out.id}.json", row_payload(out))
        return out

    def get_item(self, item_id: int) -> Optional[Item]:
        eng = self._engine_or_none()
        if eng is not None:
            with eng.connect() as conn:
                row = (
                    conn.execute(
                        text(
                            f"SELECT {_ITEM_COLS} FROM marathon.item WHERE id = :id"
                        ),
                        {"id": item_id},
                    )
                    .mappings()
                    .one_or_none()
                )
            return Item(**dict(row)) if row is not None else None
        payload = self._read_mirror(f"items/{item_id}.json")
        return Item(**payload) if payload is not None else None

    def list_items(self, run_id: str) -> list[Item]:
        eng = self._engine_or_none()
        if eng is not None:
            with eng.connect() as conn:
                rows = (
                    conn.execute(
                        text(
                            f"SELECT {_ITEM_COLS} FROM marathon.item "
                            "WHERE run_id = :run_id ORDER BY id"
                        ),
                        {"run_id": run_id},
                    )
                    .mappings()
                    .all()
                )
            return [Item(**dict(r)) for r in rows]
        payloads = [
            p for p in self._read_mirror_dir("items") if p.get("run_id") == run_id
        ]
        items = [Item(**p) for p in payloads]
        items.sort(key=lambda i: i.id or 0)
        return items

    def update_item(self, item_id: int, **fields: Any) -> Item:
        row = self._update(
            "item", "id", item_id, fields, _ITEM_UPDATABLE, _ITEM_COLS
        )
        out = Item(**dict(row))
        self._write_mirror(f"items/{out.id}.json", row_payload(out))
        return out

    # --- issue --------------------------------------------------------------------

    def insert_issue(self, issue: Issue) -> Issue:
        self._ensure_writer()
        eng = self.engine()
        with eng.begin() as conn:
            row = (
                conn.execute(
                    text(
                        "INSERT INTO marathon.issue "
                        "(run_id, stage_id, summary, status) "
                        "VALUES (:run_id, :stage_id, :summary, :status) "
                        f"RETURNING {_ISSUE_COLS}"
                    ),
                    {
                        "run_id": issue.run_id,
                        "stage_id": issue.stage_id,
                        "summary": issue.summary,
                        "status": issue.status,
                    },
                )
                .mappings()
                .one()
            )
        out = Issue(**dict(row))
        self._write_mirror(f"issues/{out.id}.json", row_payload(out))
        return out

    def list_issues(
        self, run_id: str, *, status: Optional[str] = None
    ) -> list[Issue]:
        eng = self._engine_or_none()
        if eng is not None:
            clause = "" if status is None else " AND status = :status"
            params: dict[str, Any] = {"run_id": run_id}
            if status is not None:
                params["status"] = status
            with eng.connect() as conn:
                rows = (
                    conn.execute(
                        text(
                            f"SELECT {_ISSUE_COLS} FROM marathon.issue "
                            f"WHERE run_id = :run_id{clause} ORDER BY id"
                        ),
                        params,
                    )
                    .mappings()
                    .all()
                )
            return [Issue(**dict(r)) for r in rows]
        payloads = [
            p
            for p in self._read_mirror_dir("issues")
            if p.get("run_id") == run_id
            and (status is None or p.get("status") == status)
        ]
        issues = [Issue(**p) for p in payloads]
        issues.sort(key=lambda i: i.id or 0)
        return issues

    def update_issue(self, issue_id: int, **fields: Any) -> Issue:
        row = self._update(
            "issue", "id", issue_id, fields, _ISSUE_UPDATABLE, _ISSUE_COLS
        )
        out = Issue(**dict(row))
        self._write_mirror(f"issues/{out.id}.json", row_payload(out))
        return out

    # --- status_report (T038) ---------------------------------------------------------

    def insert_status_report(self, report: StatusReport) -> StatusReport:
        self._ensure_writer()
        eng = self.engine()
        with eng.begin() as conn:
            row = (
                conn.execute(
                    text(
                        "INSERT INTO marathon.status_report "
                        "(run_id, stage_id, done, issues, tokens_spent, "
                        " tokens_remaining, todo) "
                        "VALUES (:run_id, :stage_id, CAST(:done AS jsonb), "
                        " CAST(:issues AS jsonb), :tokens_spent, "
                        " :tokens_remaining, CAST(:todo AS jsonb)) "
                        f"RETURNING {_STATUS_COLS}"
                    ),
                    {
                        "run_id": report.run_id,
                        "stage_id": report.stage_id,
                        "done": json.dumps(report.done),
                        "issues": json.dumps(report.issues),
                        "tokens_spent": report.tokens_spent,
                        "tokens_remaining": report.tokens_remaining,
                        "todo": json.dumps(report.todo),
                    },
                )
                .mappings()
                .one()
            )
        out = StatusReport(**dict(row))
        self._write_mirror(f"status/{out.id}.json", row_payload(out))
        return out

    # --- escalation (substrate write; auto-decision policy is US5's T048) -----------

    def insert_escalation(self, esc: Escalation) -> Escalation:
        self._ensure_writer()
        eng = self.engine()
        with eng.begin() as conn:
            row = (
                conn.execute(
                    text(
                        "INSERT INTO marathon.escalation "
                        "(run_id, stage_id, kind, detail) "
                        "VALUES (:run_id, :stage_id, :kind, CAST(:detail AS jsonb)) "
                        f"RETURNING {_ESCALATION_COLS}"
                    ),
                    {
                        "run_id": esc.run_id,
                        "stage_id": esc.stage_id,
                        "kind": esc.kind,
                        "detail": json.dumps(esc.detail),
                    },
                )
                .mappings()
                .one()
            )
        out = Escalation(**dict(row))
        self._write_mirror(f"escalations/{out.id}.json", row_payload(out))
        return out

    def list_escalations(
        self, run_id: str, *, open_only: bool = False
    ) -> list[Escalation]:
        eng = self._engine_or_none()
        if eng is not None:
            clause = " AND resolved_at IS NULL" if open_only else ""
            with eng.connect() as conn:
                rows = (
                    conn.execute(
                        text(
                            f"SELECT {_ESCALATION_COLS} FROM marathon.escalation "
                            f"WHERE run_id = :run_id{clause} ORDER BY id"
                        ),
                        {"run_id": run_id},
                    )
                    .mappings()
                    .all()
                )
            return [Escalation(**dict(r)) for r in rows]
        payloads = [
            p
            for p in self._read_mirror_dir("escalations")
            if p.get("run_id") == run_id
            and (not open_only or p.get("resolved_at") is None)
        ]
        escalations = [Escalation(**p) for p in payloads]
        escalations.sort(key=lambda e: e.id or 0)
        return escalations

    # --- shared update helper -------------------------------------------------------

    def _update(
        self,
        table: str,
        key_col: str,
        key_val: Any,
        fields: Mapping[str, Any],
        allowed: frozenset[str],
        returning: str,
    ) -> Mapping[str, Any]:
        self._ensure_writer()
        if not fields:
            raise ValueError(f"update of marathon.{table} requires fields")
        unknown = set(fields) - allowed
        if unknown:
            raise ValueError(
                f"non-updatable column(s) for marathon.{table}: {sorted(unknown)}"
            )
        sets = ", ".join(f"{name} = :{name}" for name in fields)
        eng = self.engine()
        with eng.begin() as conn:
            return (
                conn.execute(
                    text(
                        f"UPDATE marathon.{table} SET {sets} "
                        f"WHERE {key_col} = :_key RETURNING {returning}"
                    ),
                    {**fields, "_key": key_val},
                )
                .mappings()
                .one()
            )


def reconcile(run_id: str, *, env: Optional[MarathonEnv] = None) -> Any:
    """PGLite ↔ JSON-mirror reconciliation (FR-024/D6) — lands with US5 (T049)."""
    raise NotImplementedError("reconcile is implemented in US5 (T049)")

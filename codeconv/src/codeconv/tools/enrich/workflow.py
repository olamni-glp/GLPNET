"""codeconv enrich workflow — fill BLANK tombstone purpose/key_idea.

Implements ``specs/035-semantic-tombstone-enrichment/contracts/enrich_cli.md``
+ ``data-model.md`` §4/§6. Entry point: :func:`run_enrich`.

For each in-scope (under ``glp_runtime_net/``), non-orphan Dart file:

- **Candidate** (tombstone ``purpose`` and/or ``key_idea`` blank): read the
  file's CURRENT source, call the injected Claude ``infer_fn`` (R-004 seam),
  and on a grounded, in-bounds result write the inferred ``purpose`` /
  ``key_idea`` + ``*_source: inferred`` to BOTH the tombstone and the
  ``dart_files`` row in one per-file transaction (FR-002/004/015). A
  ``grounded == False`` / empty / over-cap result → ``low_confidence``
  (tombstone unchanged, FR-009). Any exception → ``failed`` (tombstone
  unchanged, FR-010); other candidates still process.
- **Non-candidate** (already ``doc``/``inferred``): stamp
  ``purpose_source`` / ``key_idea_source`` derived from blank-ness +
  existing markers, WITHOUT touching the ``purpose`` / ``key_idea`` TEXT
  (research R-008; keeps markdown ⇔ DB agreement, FR-004; does not violate
  FR-006). Written only when it differs, so a no-change re-run is
  byte-identical (SC-002).

Inference runs IN CLAUDE — ``infer_fn`` MUST be injected; a ``None`` raises
:func:`_require_fn`'s ``RuntimeError`` BEFORE any bridge work (the CLI
catches it → exit 2). There is NO external-LM fallback (Constitution V).
"""

from __future__ import annotations

import fnmatch
import hashlib
import json
import logging
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Optional

from sqlalchemy import text
from sqlalchemy.engine import Engine

from codeconv.bridge_client import acquire_or_discover
from codeconv.db.engine import build_engine
from codeconv.tools.discover.tombstone import (
    read_tombstone,
    tombstone_path,
    write_tombstone,
)

from .seam import (
    InferFn,
    InferRequest,
    MAX_KEY_IDEA_CHARS,
    MAX_PURPOSE_CHARS,
    _require_fn,
)


_LOG = logging.getLogger("codeconv.enrich")


def _utc_now() -> datetime:
    return datetime.now(tz=timezone.utc)


def _in_scope(rel: str, paths: Optional[list[str]]) -> bool:
    """True if ``rel`` matches any ``--path`` filter (prefix or glob).

    ``paths is None``/empty ⇒ all candidates are in scope (FR-012/013 default).
    """
    if not paths:
        return True
    for raw in paths:
        p = str(raw).replace("\\", "/").rstrip("/")
        if not p:
            continue
        if rel == p or rel.startswith(p + "/"):
            return True
        if fnmatch.fnmatch(rel, p):
            return True
    return False


def _derive_source(value: Any, existing: Any) -> str:
    """Provenance derived from a value's blank-ness + any existing marker.

    blank ⇒ ``absent``; non-blank previously-``inferred`` ⇒ keep ``inferred``;
    otherwise non-blank ⇒ ``doc`` (the only historical non-blank source —
    research R-005). Mirrors the data-model invariant ``value == '' ⟺
    *_source == 'absent'``.
    """
    if not (value and str(value).strip()):
        return "absent"
    if existing == "inferred":
        return "inferred"
    return "doc"


def run_enrich(
    repo_root: Path,
    *,
    infer_fn: Optional[InferFn] = None,
    paths: Optional[list[str]] = None,
    dry_run: bool = False,
    quiet: bool = True,
    bridge_script: Optional[Path] = None,
    data_dir: Optional[Path] = None,
) -> dict:
    """Enrich blank tombstones via the injected Claude ``infer_fn``.

    Returns the FR-011 run summary dict (``data-model.md`` §6). Raises
    ``RuntimeError`` (no-API guard) when ``infer_fn`` is ``None`` — BEFORE
    any bridge acquisition, so a bare CLI invocation never touches the
    cluster. ``--dry-run`` computes the candidate set and mutates nothing
    (no tombstone bytes, no DB rows, no run log) and does NOT call the seam.
    """
    repo_root = Path(repo_root).resolve()
    # No-API guard FIRST (Constitution V): a bare run with no Claude seam is
    # a usage error, raised before any bridge work. The CLI catches → exit 2.
    infer = _require_fn(infer_fn, "infer_fn")

    subtree = repo_root / "glp_runtime_net"
    tombstones_root = repo_root / ".codeconv" / "tombstones"
    run_id = str(uuid.uuid4())
    started_at = _utc_now()
    t0 = time.monotonic()

    endpoint = acquire_or_discover(
        repo_root,
        ready_timeout=30.0,
        bridge_script=bridge_script,
        data_dir=data_dir,
    )
    engine = build_engine(endpoint)

    with engine.begin() as conn:
        rows = conn.execute(
            text(
                "SELECT path, purpose, key_idea, sha256, "
                "       purpose_source, key_idea_source "
                "FROM codeconv.dart_files ORDER BY path"
            )
        ).all()

    candidates = 0
    enriched = 0
    skipped = 0          # candidates skipped (stale/missing source) — no inference
    low_confidence = 0   # grounded=False / empty / over-cap → tombstone unchanged
    failed = 0           # seam raised → tombstone unchanged
    skipped_non_candidate = 0
    failures: list[dict] = []
    warnings_list: list[dict] = []
    outcomes: list[dict] = []

    for path, db_purpose, db_key_idea, db_sha, db_psrc, db_ksrc in rows:
        rel = str(path).replace("\\", "/")
        if not _in_scope(rel, paths):
            continue

        tomb_path = tombstone_path(tombstones_root, rel)
        if not tomb_path.is_file():
            warnings_list.append({"kind": "missing_tombstone", "path": rel})
            outcomes.append({"path": rel, "outcome": "missing_tombstone"})
            continue
        try:
            tomb = read_tombstone(tomb_path)
        except Exception as exc:  # unreadable tombstone — warn, do not corrupt
            warnings_list.append(
                {"kind": "unreadable_tombstone", "path": rel, "error": str(exc)}
            )
            outcomes.append(
                {"path": rel, "outcome": "unreadable_tombstone", "reason": str(exc)}
            )
            continue

        cur_purpose = str(tomb.get("purpose") or "")
        cur_key = str(tomb.get("key_idea") or "")
        is_candidate = (cur_purpose == "") or (cur_key == "")

        # ---- Non-candidate: provenance stamping only (R-008 / T011) -------
        if not is_candidate:
            desired_psrc = _derive_source(cur_purpose, tomb.get("purpose_source"))
            desired_ksrc = _derive_source(cur_key, tomb.get("key_idea_source"))
            need_md = (
                tomb.get("purpose_source") != desired_psrc
                or tomb.get("key_idea_source") != desired_ksrc
            )
            need_db = db_psrc != desired_psrc or db_ksrc != desired_ksrc
            if not dry_run and (need_md or need_db):
                if need_db:
                    with engine.begin() as conn:
                        conn.execute(
                            text(
                                "UPDATE codeconv.dart_files SET "
                                "  purpose_source = :ps, key_idea_source = :ks "
                                "WHERE path = :p"
                            ),
                            {"ps": desired_psrc, "ks": desired_ksrc, "p": rel},
                        )
                if need_md:
                    updated = dict(tomb)
                    updated["purpose_source"] = desired_psrc
                    updated["key_idea_source"] = desired_ksrc
                    write_tombstone(tombstones_root, rel, updated)
            skipped_non_candidate += 1
            outcomes.append(
                {
                    "path": rel,
                    "outcome": "non_candidate",
                    "purpose_source": desired_psrc,
                    "key_idea_source": desired_ksrc,
                }
            )
            continue

        # ---- Candidate ---------------------------------------------------
        candidates += 1

        src_path = subtree / rel
        try:
            src_bytes = src_path.read_bytes()
        except OSError as exc:
            skipped += 1
            outcomes.append(
                {"path": rel, "outcome": "skipped",
                 "reason": f"source unreadable/missing: {exc}"}
            )
            continue
        cur_hash = hashlib.sha256(src_bytes).hexdigest()
        recorded_sha = str(tomb.get("sha256") or "")
        if recorded_sha and recorded_sha != cur_hash:
            # Stale tombstone (FR-007 edge): do NOT infer from stale metadata.
            skipped += 1
            outcomes.append(
                {"path": rel, "outcome": "skipped",
                 "reason": "stale tombstone (sha256 != current source); "
                 "run discover first"}
            )
            continue

        if dry_run:
            # Would be enriched; compute nothing destructive, call no seam.
            enriched += 1
            outcomes.append({"path": rel, "outcome": "would_enrich"})
            continue

        try:
            result = infer(
                InferRequest(
                    rel_path=rel,
                    source_text=src_bytes.decode("utf-8", errors="replace"),
                )
            )
        except Exception as exc:  # FR-010 fault isolation — tombstone unchanged
            failed += 1
            failures.append({"path": rel, "reason": f"seam error: {exc}"})
            outcomes.append(
                {"path": rel, "outcome": "failed", "reason": f"seam error: {exc}"}
            )
            continue

        # Only fill the BLANK field(s); never overwrite non-blank text (FR-006).
        new_purpose = result.purpose if cur_purpose == "" else cur_purpose
        new_key = result.key_idea if cur_key == "" else cur_key

        reject = _low_confidence_reason(
            result, fill_purpose=cur_purpose == "", fill_key=cur_key == "",
            new_purpose=new_purpose, new_key=new_key,
        )
        if reject is not None:
            low_confidence += 1
            outcomes.append(
                {"path": rel, "outcome": "low_confidence", "reason": reject}
            )
            continue

        psrc = "inferred" if cur_purpose == "" else _derive_source(
            cur_purpose, tomb.get("purpose_source")
        )
        ksrc = "inferred" if cur_key == "" else _derive_source(
            cur_key, tomb.get("key_idea_source")
        )
        # DB write (transactional) FIRST, then the tombstone — so any failure
        # leaves the tombstone unchanged (SC-007); the tombstone is written
        # only after a committed DB row.
        with engine.begin() as conn:
            conn.execute(
                text(
                    "UPDATE codeconv.dart_files SET "
                    "  purpose = :p, key_idea = :k, "
                    "  purpose_source = :ps, key_idea_source = :ks "
                    "WHERE path = :path"
                ),
                {"p": new_purpose, "k": new_key,
                 "ps": psrc, "ks": ksrc, "path": rel},
            )
        updated = dict(tomb)
        updated["purpose"] = new_purpose
        updated["key_idea"] = new_key
        updated["purpose_source"] = psrc
        updated["key_idea_source"] = ksrc
        write_tombstone(tombstones_root, rel, updated)
        enriched += 1
        outcomes.append(
            {"path": rel, "outcome": "enriched",
             "purpose_source": psrc, "key_idea_source": ksrc}
        )

    scope_str = "glp_runtime_net/ (path filter: " + (
        ", ".join(paths) if paths else "none"
    ) + ")"
    summary = {
        "ok": True,
        "tool": "enrich",
        "exit_code": 0,
        "scope": scope_str,
        "dry_run": dry_run,
        "candidates": candidates,
        "enriched": enriched,
        "skipped": skipped,
        "failed": failed,
        "low_confidence": low_confidence,
        "skipped_non_candidate": skipped_non_candidate,
        "failures": failures,
        "warnings": warnings_list,
        "duration_seconds": round(time.monotonic() - t0, 2),
    }

    # Durable run log (FR-011 / C1) — a file artifact, not a DB table. Skipped
    # on --dry-run (which mutates nothing).
    if not dry_run:
        run_log_rel = f".codeconv/enrich-runs/{run_id}.json"
        log_path = repo_root / run_log_rel
        log_path.parent.mkdir(parents=True, exist_ok=True)
        log_payload = dict(summary)
        log_payload["run_id"] = run_id
        log_payload["started_at"] = started_at.isoformat()
        log_payload["outcomes"] = outcomes
        log_path.write_text(
            json.dumps(log_payload, indent=2, sort_keys=True, default=str),
            encoding="utf-8",
        )
        summary["run_log"] = run_log_rel
    else:
        summary["run_log"] = None

    return summary


def _low_confidence_reason(
    result,
    *,
    fill_purpose: bool,
    fill_key: bool,
    new_purpose: str,
    new_key: str,
) -> Optional[str]:
    """Return a rejection reason if a result is low-confidence, else None.

    Rejects (FR-009 / analyze B1): ``grounded == False``; a newly-filled
    field that is whitespace-only; a newly-filled field over its length cap
    (``MAX_PURPOSE_CHARS`` / ``MAX_KEY_IDEA_CHARS``). A rejected candidate's
    tombstone is left UNCHANGED.
    """
    if not result.grounded:
        return result.reason or "seam reported low confidence (grounded=False)"
    if fill_purpose:
        if not new_purpose.strip():
            return "inferred purpose is empty/whitespace-only"
        if len(new_purpose) > MAX_PURPOSE_CHARS:
            return f"inferred purpose exceeds {MAX_PURPOSE_CHARS} chars"
    if fill_key:
        if not new_key.strip():
            return "inferred key_idea is empty/whitespace-only"
        if len(new_key) > MAX_KEY_IDEA_CHARS:
            return f"inferred key_idea exceeds {MAX_KEY_IDEA_CHARS} chars"
    return None


__all__ = ["run_enrich"]

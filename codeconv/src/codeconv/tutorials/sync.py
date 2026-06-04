"""Corpus vendoring + drift detection (D3, FR-007).

BUILD-TIME ONLY — this is the *only* path that reads the sibling GLP repo; the
runtime list path never touches it (FR-007). ``sync`` re-vendors the sibling
tutorial tree into ``tutorials/olamni/`` and writes a provenance manifest
(``SNAPSHOT.md`` + ``.snapshot.json``); ``sync --check`` recomputes the vendored
tree's hashes against the manifest (and diffs vs the sibling when present),
reporting drift so it can gate CI later.

No bridge / DBOS / LM — stdlib only (``shutil``/``hashlib``/``subprocess`` for
the optional git ref). Pure data operations; not on the list path.
"""

from __future__ import annotations

import hashlib
import json as _json
import shutil
import subprocess
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path

# Authoritative sibling source (research ground truth); overridable via --source.
DEFAULT_SOURCE = Path("D:/bstdev/research/glp/GLP/olamni/tutorial")

VENDOR_RELPATH = ("tutorials", "olamni")
MANIFEST_NAME = ".snapshot.json"
SNAPSHOT_MD = "SNAPSHOT.md"

# Provenance files live inside the vendored tree but are not part of its content
# hash (they are written *after* the copy).
_PROVENANCE = {MANIFEST_NAME, SNAPSHOT_MD}


@dataclass
class SyncResult:
    dest: Path
    files: int
    source: Path
    source_ref: str | None


@dataclass
class CheckResult:
    ok: bool
    dest: Path
    tampered: list[str] = field(default_factory=list)  # vendored ≠ manifest
    missing: list[str] = field(default_factory=list)  # in manifest, gone on disk
    sibling_drift: list[str] = field(default_factory=list)  # vendored ≠ sibling


def default_dest(repo_root: Path) -> Path:
    return Path(repo_root).joinpath(*VENDOR_RELPATH)


def _sha256(path: Path) -> str:
    h = hashlib.sha256()
    h.update(path.read_bytes())
    return h.hexdigest()


def compute_hashes(tree_root: Path) -> dict[str, str]:
    """Sorted ``{posix-relpath: sha256}`` of every content file under ``tree_root``.

    Provenance files (``.snapshot.json``, ``SNAPSHOT.md``) are excluded.
    """
    tree_root = Path(tree_root)
    out: dict[str, str] = {}
    for path in sorted(p for p in tree_root.rglob("*") if p.is_file()):
        rel = path.relative_to(tree_root).as_posix()
        if rel in _PROVENANCE:
            continue
        out[rel] = _sha256(path)
    return dict(sorted(out.items()))


def _git_ref(source: Path) -> str | None:
    try:
        result = subprocess.run(
            ["git", "-C", str(source), "rev-parse", "HEAD"],
            capture_output=True,
            text=True,
            timeout=10,
        )
    except (OSError, subprocess.SubprocessError):
        return None
    if result.returncode != 0:
        return None
    ref = result.stdout.strip()
    return ref or None


def _write_manifest(dest: Path, source: Path, source_ref: str | None, when: str) -> int:
    hashes = compute_hashes(dest)
    manifest = {
        "source": source.as_posix(),
        "source_ref": source_ref,
        "vendored_at": when,
        "file_count": len(hashes),
        "files": hashes,
    }
    (dest / MANIFEST_NAME).write_text(
        _json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    md = (
        f"# Vendored tutorial corpus snapshot\n\n"
        f"This is a build-time **snapshot** of the GLP tutorial corpus, vendored "
        f"into glpnet so the `/glptutorial-list` lister has no runtime dependency "
        f"on the sibling repo (FR-007). Regenerate with `codeconv tutorials sync`; "
        f"verify with `codeconv tutorials sync --check`.\n\n"
        f"- **Source**: `{source.as_posix()}`\n"
        f"- **Source git ref**: `{source_ref or '(not a git checkout)'}`\n"
        f"- **Vendored at**: {when}\n"
        f"- **Files**: {len(hashes)} (content files; excludes this manifest)\n\n"
        f"Per-file SHA-256 hashes are in `{MANIFEST_NAME}`.\n"
    )
    (dest / SNAPSHOT_MD).write_text(md, encoding="utf-8")
    return len(hashes)


def sync(repo_root: Path, source: Path | None = None, dest: Path | None = None) -> SyncResult:
    """Re-vendor ``source`` → ``tutorials/olamni/`` and write the provenance manifest.

    The destination is replaced wholesale so the snapshot is a faithful mirror
    (regenerable from the source). Raises ``FileNotFoundError`` if the sibling
    source is absent.
    """
    source = Path(source) if source is not None else DEFAULT_SOURCE
    dest = Path(dest) if dest is not None else default_dest(repo_root)
    if not source.is_dir():
        raise FileNotFoundError(f"sibling tutorial corpus not found: {source}")

    if dest.exists():
        shutil.rmtree(dest)
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copytree(source, dest)
    # Drop any provenance files copied from the source so ours are authoritative.
    for name in _PROVENANCE:
        stale = dest / name
        if stale.exists():
            stale.unlink()

    when = datetime.now(timezone.utc).isoformat()
    source_ref = _git_ref(source)
    count = _write_manifest(dest, source, source_ref, when)
    return SyncResult(dest=dest, files=count, source=source, source_ref=source_ref)


def check(
    repo_root: Path, source: Path | None = None, dest: Path | None = None
) -> CheckResult:
    """Verify the vendored tree against ``.snapshot.json`` (and the sibling if present).

    Detects local tampering (vendored ≠ manifest), missing files, and — when the
    sibling source is reachable — staleness (vendored ≠ sibling). The caller maps
    ``ok=False`` to a non-zero exit so this can gate CI.
    """
    dest = Path(dest) if dest is not None else default_dest(repo_root)
    manifest_path = dest / MANIFEST_NAME
    if not manifest_path.is_file():
        return CheckResult(ok=False, dest=dest, missing=[MANIFEST_NAME])

    manifest = _json.loads(manifest_path.read_text(encoding="utf-8"))
    recorded: dict[str, str] = manifest.get("files", {})
    current = compute_hashes(dest)

    tampered = sorted(
        rel for rel, h in current.items() if rel in recorded and recorded[rel] != h
    )
    # Files added on disk but not in the manifest are also drift.
    tampered += sorted(rel for rel in current if rel not in recorded)
    missing = sorted(rel for rel in recorded if rel not in current)

    sibling_drift: list[str] = []
    source = Path(source) if source is not None else DEFAULT_SOURCE
    if source.is_dir():
        sibling = compute_hashes(source)
        keys = set(sibling) | set(current)
        sibling_drift = sorted(k for k in keys if sibling.get(k) != current.get(k))

    ok = not tampered and not missing and not sibling_drift
    return CheckResult(
        ok=ok,
        dest=dest,
        tampered=sorted(set(tampered)),
        missing=missing,
        sibling_drift=sibling_drift,
    )


__all__ = [
    "DEFAULT_SOURCE",
    "SyncResult",
    "CheckResult",
    "default_dest",
    "compute_hashes",
    "sync",
    "check",
]

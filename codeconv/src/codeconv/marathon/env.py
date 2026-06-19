"""Per-run marathon environment resolution (T005).

``MarathonEnv`` bundles the per-run ``store_root`` (the off-repo marathon-run
directory holding the isolated PGLite cluster + JSON mirror), the lazily-acquired
SQLAlchemy ``engine``, and the working ``repo_dir`` (where scoped commits land).

``resolve_env(run_id, *, data_dir=None)`` defaults the marathon root to a
guaranteed-NTFS user-level path (research F4: ``C:/pglite/marathon/<run_id>`` —
the canonical ``C:/pglite/...`` convention; ``~/.pglite/marathon/<run_id>`` on
POSIX, where the filesystem guard is a no-op) and **refuses a store root inside
any git working tree** (FR-027; keeper-lifecycle.md "Isolation invariant").
When ``data_dir`` is given it IS the per-run store root (the caller owns
per-run-ness — the test fixture and the CLI ``--data-dir`` pass it directly);
the cluster data-dir ``<store_root>/pgdb`` is derived from it (T007).

The NTFS/ReFS check reuses the existing ``bridge_client._check_data_dir_filesystem``
guard (FR-028 compose; exit 64 semantics preserved by the CLI wiring).
"""

from __future__ import annotations

import os
from pathlib import Path
from typing import Optional, Union

from codeconv.bridge_client import _check_data_dir_filesystem
from codeconv.marathon.models import MarathonEnv

__all__ = [
    "StoreRootInsideRepoError",
    "default_marathon_root",
    "resolve_env",
    "toolchain_repo_root",
]

# The unified-bridge asset path, relative to the toolchain checkout root.
_BRIDGE_SCRIPT_REL = Path("prereq-patterns") / "pglite" / "pglite_bridge.mjs"


def toolchain_repo_root() -> Path:
    """The codeconv/glpnet checkout that owns the unified PGLite **bridge
    script** (``prereq-patterns/pglite/pglite_bridge.mjs`` + its
    ``node_modules``).

    The bridge SCRIPT is a fixed asset of this toolchain — it must be located
    here, NOT under a run's off-repo ``store_root`` (FR-027: nothing repo-shaped
    lives there) and NOT under the run's ``repo_dir`` (the scoped-commit target,
    which a caller may point at a throwaway work-repo with no bridge asset).
    ``acquire_or_discover`` is therefore given this root, while ``data_dir`` keeps
    the per-run cluster off-repo (the two are decoupled — bridge_client).

    Resolved by walking up from this package to the first ancestor that actually
    holds the bridge script (self-validating; resilient to checkout layout).
    """
    here = Path(__file__).resolve()
    for candidate in here.parents:
        if (candidate / _BRIDGE_SCRIPT_REL).is_file():
            return candidate
    # Conventional layout fallback (…/codeconv/src/codeconv/marathon/env.py →
    # repo root is parents[4]); acquire_or_discover surfaces a clear error if the
    # script is genuinely absent.
    return here.parents[4]


class StoreRootInsideRepoError(RuntimeError):
    """The resolved per-run store root sits inside a git working tree.

    FR-027: the per-run isolated store MUST live outside the working
    repository (and any other repo's) git tree, so a crashed/abandoned run
    can never wedge repo state and scoped commits never sweep store files.
    """


def default_marathon_root() -> Path:
    """The user-level marathon root all runs default under (research F4).

    Windows: ``C:/pglite/marathon`` — guaranteed-NTFS, consistent with the
    canonical ``C:/pglite/research/<project>`` cluster convention. POSIX:
    ``~/.pglite/marathon`` (the filesystem guard does not apply there).
    """
    if os.name == "nt":
        return Path("C:/pglite/marathon")
    return Path.home() / ".pglite" / "marathon"


def _git_top_of(path: Path) -> Optional[Path]:
    """Return the git working-tree top containing ``path``, or None."""
    probe = path.resolve()
    for candidate in (probe, *probe.parents):
        if (candidate / ".git").exists():
            return candidate
    return None


def resolve_env(
    run_id: str,
    *,
    data_dir: Union[str, os.PathLike, None] = None,
    repo_dir: Union[str, os.PathLike, None] = None,
) -> MarathonEnv:
    """Resolve the per-run :class:`MarathonEnv` (FR-027).

    ``data_dir`` (when given) is the per-run store root itself; otherwise the
    store root is ``default_marathon_root()/<run_id>``. ``repo_dir`` defaults
    to the current working directory (the repo scoped commits land in).

    Raises :class:`StoreRootInsideRepoError` if the store root is inside a
    git working tree, and ``DataDirFilesystemError`` (bridge_client) if the
    derived cluster data-dir is on a non-NTFS/ReFS volume (fail fast, FR-016).
    """
    repo = Path(repo_dir).resolve() if repo_dir is not None else Path.cwd().resolve()
    if data_dir is not None:
        store_root = Path(data_dir).resolve()
    else:
        store_root = (default_marathon_root() / run_id).resolve()

    git_top = _git_top_of(store_root)
    if git_top is not None:
        raise StoreRootInsideRepoError(
            f"per-run marathon store root {store_root} is inside the git "
            f"working tree at {git_top}; the store must live OUTSIDE any "
            f"repo (FR-027). Pass --data-dir with an off-repo NTFS path "
            f"(default: {default_marathon_root()}/<run-id>)."
        )

    store_root.mkdir(parents=True, exist_ok=True)
    # Fail fast on a PGLite-hostile filesystem — same guard, same semantics
    # as the shared bridge (exit 64 at the CLI boundary).
    _check_data_dir_filesystem(store_root / "pgdb")

    return MarathonEnv(run_id=run_id, store_root=store_root, repo_dir=repo)

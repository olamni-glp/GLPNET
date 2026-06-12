"""Per-run marathon environment resolution (T005).

``MarathonEnv`` bundles the per-run ``store_root`` (the off-repo marathon-run
directory holding the isolated PGLite cluster + JSON mirror), the lazily-acquired
SQLAlchemy ``engine``, and the working ``repo_dir`` (where scoped commits land).
``resolve_env(run_id, *, data_dir=None)`` defaults the marathon root to a
guaranteed-NTFS user-level path (research F4) and **asserts the store root is
OUTSIDE the repo git tree** (FR-027), refusing otherwise.

Stub — implemented in Phase 2 Foundational (T005).
"""

from __future__ import annotations

__all__ = ["resolve_env"]

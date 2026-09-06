"""Bootstrap the ONE shared implementation of the adoption/override rules.

The rules live at ``<repo>/scripts/lib/adoption_gate.py`` rather than here, and this module
exists only to reach them. The reason is asymmetric reachability, not taste:

  * this package is importable only with the codeconv virtual environment on ``sys.path``;
  * ``scripts/evidence_signal_audit.py`` is deliberately stdlib-only and must run WITHOUT it.

So the shared module has to sit where the stdlib-only consumer can reach it, and this side does
the reaching. Engineer ruling ``Q-olg17-02`` (2026-09-06): extract a stdlib-only reader that both
consume, rather than let feature 109 write a second copy of the rules (feature 108 FR-006b) or
make the audit unable to run without a venv (feature 108 FR-014).

FR-013 is proven, not asserted: ``scripts/tests/test_adoption_gate_single_impl.py`` requires the
function objects reached through both paths to be **identical**. A second implementation cannot
be introduced without failing it.
"""

from __future__ import annotations

import importlib.util
import os
import sys
from pathlib import Path

_MODULE_NAME = "glpnet_adoption_gate"


def _repo_root() -> Path:
    """Walk up from this file to the repo root.

    Anchored on a file that only the repo root has. Walking up looking for ``.git`` would also
    stop inside a worktree or a submodule; anchoring on the shared module itself would beg the
    question this function is asked.
    """
    here = Path(__file__).resolve()
    for parent in here.parents:
        if (parent / "scripts" / "lib" / "adoption_gate.py").is_file():
            return parent
    raise ImportError(
        "cannot locate scripts/lib/adoption_gate.py from %s -- the shared adoption/override "
        "rules are missing. This is NOT a reason to fall back to a local copy: a second "
        "implementation of these rules is what feature 108 FR-006b forbids. Restore the file."
        % here)


def _load():
    # Already imported by the stdlib-only consumer in this process? Reuse that exact module, so
    # both callers really do share one implementation rather than two equal copies.
    if _MODULE_NAME in sys.modules:
        return sys.modules[_MODULE_NAME]
    for name, mod in list(sys.modules.items()):
        if name == "adoption_gate" and getattr(mod, "__file__", None):
            if os.path.samefile(mod.__file__, _repo_root() / "scripts" / "lib" / "adoption_gate.py"):
                sys.modules[_MODULE_NAME] = mod
                return mod
    path = _repo_root() / "scripts" / "lib" / "adoption_gate.py"
    spec = importlib.util.spec_from_file_location(_MODULE_NAME, str(path))
    if spec is None or spec.loader is None:  # pragma: no cover - defensive
        raise ImportError(f"cannot load the shared adoption gate from {path}")
    mod = importlib.util.module_from_spec(spec)
    sys.modules[_MODULE_NAME] = mod
    spec.loader.exec_module(mod)
    return mod


gate = _load()

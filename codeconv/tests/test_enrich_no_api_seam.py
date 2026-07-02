"""Feature 035 / US1 (T008) — the no-API seam is enforced (SC-004).

Two guards for Constitution V / FR-003 / SC-004:

1. A bare ``codeconv enrich run`` (CLI path, no injected Claude ``infer_fn``)
   exits 2 with the actionable "drive me through /codeconv-enrich" message —
   NEVER an external-API fallback.
2. No ``openai`` / ``litellm`` / ``OPENAI_API_KEY`` token is reachable
   anywhere in the enrich tool source (structural proof of no external LM).
"""

from __future__ import annotations

from pathlib import Path

from .conftest import run_codeconv


def test_bare_cli_exits_2_with_skill_message(tmp_path: Path) -> None:
    """No bridge needed: ``_require_fn`` raises BEFORE any bridge acquire,
    so the CLI exits 2 with the skill-drive message on any repo root."""
    proc = run_codeconv(tmp_path, "enrich", "run")
    assert proc.returncode == 2, (
        f"expected exit 2 (no seam injected), got {proc.returncode}: "
        f"stdout={proc.stdout!r} stderr={proc.stderr!r}"
    )
    combined = (proc.stdout + proc.stderr).lower()
    assert "/codeconv-enrich" in combined
    assert "no external-api" in combined or "no openai_api_key" in combined or "openai" in combined


def test_no_external_lm_tokens_on_enrich_path() -> None:
    """Static SC-004 proof: zero openai/litellm/OPENAI_API_KEY tokens in the
    enrich tool source tree."""
    import codeconv.tools.enrich as enrich_pkg

    pkg_dir = Path(enrich_pkg.__file__).parent
    forbidden = ("openai", "litellm", "OPENAI_API_KEY")
    offenders: list[str] = []
    for py in sorted(pkg_dir.rglob("*.py")):
        textsrc = py.read_text(encoding="utf-8")
        for tok in forbidden:
            if tok in textsrc:
                offenders.append(f"{py.name}: {tok}")
    assert not offenders, f"external-LM tokens on the enrich path: {offenders}"

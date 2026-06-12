"""Constitution V guard — the marathon harness has NO LM/external-API path
(T054).

The harness is deterministic scaffolding: stages, checkpoints, store,
keeper. Zero ``OPENAI_API_KEY`` / ``litellm`` / ``openai`` tokens may appear
anywhere in the marathon package source — not as an import, not as an env
lookup, not behind a flag. (``openai`` as a substring also covers
``OPENAI_API_KEY``; both are asserted per the task's explicit token list.)
"""

from __future__ import annotations

from pathlib import Path

import codeconv.marathon as marathon_pkg

FORBIDDEN_TOKENS = ("openai_api_key", "litellm", "openai")


def _marathon_sources() -> list[Path]:
    pkg_dir = Path(marathon_pkg.__file__).parent
    files = sorted(pkg_dir.rglob("*.py"))
    assert files, f"no marathon sources found under {pkg_dir}"
    return files


def test_marathon_package_has_no_lm_tokens() -> None:
    offenders: list[str] = []
    for path in _marathon_sources():
        text = path.read_text(encoding="utf-8").lower()
        for token in FORBIDDEN_TOKENS:
            if token in text:
                offenders.append(f"{path.name}: {token}")
    assert not offenders, (
        "Constitution V violation — LM/API tokens on the marathon code path: "
        f"{offenders}"
    )

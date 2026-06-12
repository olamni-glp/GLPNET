"""Polish — the marathon harness introduces no LM/API path (T054).

Constitution V: LM-backed work runs in Claude (the driving session / Agent
tool seams), never via an external API from harness code. The grep is the
test: zero ``OPENAI_API_KEY`` / ``litellm`` / ``openai`` tokens anywhere on
the marathon code path — the package itself, its CLI wiring, and the modules
it composes (``bridge_client``, ``db.engine``).
"""

from __future__ import annotations

import re
from pathlib import Path

FORBIDDEN = re.compile(r"OPENAI_API_KEY|litellm|openai", re.IGNORECASE)


def _marathon_code_path() -> list[Path]:
    import codeconv.bridge_client as bridge_client
    import codeconv.marathon as marathon_pkg
    from codeconv.db import engine as db_engine

    files = sorted(Path(marathon_pkg.__file__).parent.rglob("*.py"))
    files += [Path(bridge_client.__file__), Path(db_engine.__file__)]
    return files


def test_zero_lm_tokens_on_the_marathon_code_path() -> None:
    offenders: list[str] = []
    for src in _marathon_code_path():
        for lineno, line in enumerate(
            src.read_text(encoding="utf-8").splitlines(), start=1
        ):
            if FORBIDDEN.search(line):
                offenders.append(f"{src.name}:{lineno}: {line.strip()}")
    assert offenders == [], "\n".join(offenders)

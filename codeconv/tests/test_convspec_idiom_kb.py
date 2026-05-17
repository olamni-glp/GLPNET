"""T036/T037/T038/T035/T056 [US2] — convspec idiom KB, conflict,
research provenance, ingest step, both-bases. @needs_bridge (exercises
the 0005 tables); direct module calls — no full pipeline needed.
"""

from __future__ import annotations

from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))


def _migrate(repo_root: Path) -> None:
    assert run_codeconv(repo_root, "migrate", timeout=180.0).returncode == 0


# ---- T036: lookup-before-research, reuse, consistency (FR-012/SC-007) ----
@needs_bridge
def test_idiom_lookup_before_research_and_reuse(discover_repo: Path) -> None:
    _migrate(discover_repo)
    from codeconv.tools.convspec import idioms

    eng = _engine(discover_repo)
    key = idioms.normalise_construct("Stream<int>  async*")

    # miss → NEEDS_RESEARCH (skill would spawn research)
    assert idioms.decide(eng, key) == idioms.NEEDS_RESEARCH

    # record research + idiom; now a 2nd file with the same construct
    # REUSES verbatim — NO research, NO re-derive (SC-007).
    idioms.record_research(
        eng,
        construct_key=key,
        query="Dart Stream<int> async* → C#?",
        authoritative_source="https://learn.microsoft.com/.../IAsyncEnumerable",
        conclusion="map to IAsyncEnumerable<int>",
        is_authoritative=True,
    )
    idioms.record_idiom(
        eng,
        construct_key=key,
        source_form="Stream<int> async*",
        target_form="IAsyncEnumerable<int>",
        rationale="async stream maps to IAsyncEnumerable",
        first_seen_path="lib/a.dart",
    )
    assert idioms.decide(eng, key) == idioms.REUSE
    hit = idioms.lookup_idiom(eng, key)
    assert hit and hit.status == "active"
    assert hit.target_form == "IAsyncEnumerable<int>"


# ---- T037: idiom↔idiom & idiom↔research conflict → escalate (FR-014) ----
@needs_bridge
def test_idiom_conflict_escalates_not_overwrites(discover_repo: Path) -> None:
    _migrate(discover_repo)
    from codeconv.tools.convspec import idioms

    eng = _engine(discover_repo)
    key = idioms.normalise_construct("late final T")
    idioms.record_idiom(
        eng,
        construct_key=key,
        source_form="late final T",
        target_form="readonly T (lazy)",
        rationale="v1",
        first_seen_path="lib/a.dart",
    )
    # a DIFFERENT target for the same construct must NOT silently
    # overwrite — it flags conflicted + returns ESCALATE (FR-014).
    out = idioms.record_idiom(
        eng,
        construct_key=key,
        source_form="late final T",
        target_form="Lazy<T>",
        rationale="v2 (conflicts)",
        first_seen_path="lib/b.dart",
    )
    assert out == idioms.ESCALATE
    hit = idioms.lookup_idiom(eng, key)
    assert hit.status == "conflicted"
    assert hit.target_form == "readonly T (lazy)"  # original NOT overwritten
    # a conflicted idiom ⇒ decide() escalates (never guess)
    assert idioms.decide(eng, key) == idioms.ESCALATE


# ---- T038: official-docs-authoritative; cached never re-researched ----
@needs_bridge
def test_research_provenance_authoritative_and_cached(
    discover_repo: Path,
) -> None:
    _migrate(discover_repo)
    from codeconv.tools.convspec import idioms

    eng = _engine(discover_repo)
    key = idioms.normalise_construct("dart isolate")

    # non-authoritative-only finding ⇒ decide() ESCALATES (FR-024).
    idioms.record_research(
        eng,
        construct_key=key,
        query="dart isolate → C#?",
        authoritative_source="random-blog",
        conclusion="maybe Thread?",
        is_authoritative=False,
    )
    assert idioms.decide(eng, key) == idioms.ESCALATE

    # an authoritative finding (different construct) ⇒ CACHED_RESEARCH,
    # and the insert-once cache means it is NEVER re-researched.
    k2 = idioms.normalise_construct("dart Future<void>")
    idioms.record_research(
        eng,
        construct_key=k2,
        query="Future<void> → C#?",
        authoritative_source="https://dart.dev/.../futures + learn.microsoft.com/Task",
        conclusion="Task",
        is_authoritative=True,
    )
    assert idioms.decide(eng, k2) == idioms.CACHED_RESEARCH
    # re-record is a no-op (ON CONFLICT DO NOTHING) — offline-reproducible
    idioms.record_research(
        eng,
        construct_key=k2,
        query="DIFFERENT QUERY (must be ignored)",
        authoritative_source="x",
        conclusion="DIFFERENT (must be ignored)",
        is_authoritative=False,
    )
    f = idioms.lookup_research(eng, k2)
    assert f["conclusion"] == "Task" and f["is_authoritative"] is True


# ---- T035: convspec ingest step — deterministic, no-C#, sentinel ----
@needs_bridge
def test_convspec_step_needs_agent_then_specced(discover_repo: Path) -> None:
    _migrate(discover_repo)
    from codeconv.tools.convspec.workflow import run_convspec_step

    # Need a dart_files row (FK) + the source file.
    (discover_repo / "lib").mkdir(parents=True, exist_ok=True)
    src = discover_repo / "lib" / "x.dart"
    src.write_text("/// X.\nclass X {}\n", encoding="utf-8")
    from sqlalchemy import text

    with _engine(discover_repo).begin() as c:
        c.execute(
            text(
                "INSERT INTO codeconv.dart_files "
                "(path,name,purpose,key_idea,mtime,sha256,discovered_at) "
                "VALUES ('lib/x.dart','x.dart','','',NOW(),'x',NOW()) "
                "ON CONFLICT (path) DO NOTHING"
            )
        )

    # No artifact yet → deterministic needs_agent_work sentinel (NOT a
    # raised exception — R3).
    r = run_convspec_step(
        repo_root=discover_repo, data_dir=None, rel_path="lib/x.dart"
    )
    assert r.get("needs_agent_work") is True, r

    # Agent writes a valid spec-only artifact → ingest accepts → specced.
    art = discover_repo / ".codeconv" / "conversion-specs" / "lib" / "x.dart.md"
    art.parent.mkdir(parents=True, exist_ok=True)
    art.write_text(
        "# spec\n\n```yaml\nschema_version: 1\nsource_path: lib/x.dart\n"
        "constructs:\n  - construct_key: classx\n    trivial: true\n"
        "conversion_units: []\nescalations: []\n```\n\nRationale: trivial.\n",
        encoding="utf-8",
    )
    r2 = run_convspec_step(
        repo_root=discover_repo, data_dir=None, rel_path="lib/x.dart"
    )
    assert r2.get("outcome") == "specced", r2

    # An artifact that emits C# is REJECTED (FR-023).
    art.write_text(
        "```yaml\nschema_version: 1\n```\n```csharp\npublic class X {}\n```\n",
        encoding="utf-8",
    )
    r3 = run_convspec_step(
        repo_root=discover_repo,
        data_dir=None,
        rel_path="lib/x.dart",
        respec=True,
    )
    assert r3.get("needs_agent_work") is True, r3
    assert any("FR-023" in e for e in r3.get("errors", [])), r3


# ---- T056 (E3): both bases per non-trivial construct (SC-006) ----
@needs_bridge
def test_both_bases_required_for_nontrivial(discover_repo: Path) -> None:
    from codeconv.tools.convspec.artefact import validate_artifact

    # non-trivial construct with NEITHER idiom_id NOR (analysis+research)
    bad = (
        "```yaml\nschema_version: 1\nsource_path: a.dart\n"
        "constructs:\n  - construct_key: streamx\n    source_form: 'Stream'\n"
        "    nuance: 'Stream vs IAsyncEnumerable'\n"
        "conversion_units: []\nescalations: []\n```\n"
    )
    errs = validate_artifact(bad)
    assert any("SC-006" in e for e in errs), errs

    # with BOTH a deep-analysis basis AND a research_finding_id → OK
    good = (
        "```yaml\nschema_version: 1\nsource_path: a.dart\n"
        "constructs:\n  - construct_key: streamx\n    source_form: 'Stream<int>'\n"
        "    target_decision: 'IAsyncEnumerable<int>'\n"
        "    research_finding_id: 1\n"
        "    nuance: 'Stream vs IAsyncEnumerable'\n"
        "conversion_units: [u1]\nescalations: []\n```\n\nRationale…\n"
    )
    assert validate_artifact(good) == []

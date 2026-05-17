"""``codeconv mirror`` workflow — Feature 016 / spec Amendment 1 (US6).

Source of truth:
``specs/016-codeconv-init-scaffold-langpair/contracts/codeconv_mirror_cli.md``
behaviour 1–7 (spec FR-027..FR-041, D7), reproducing spec
``001-d2net-scaffold`` FR-002..FR-014 **generically** via the
workspace-bound language pair's mirror hooks. No spec-001 behaviour is
hard-coded in this stage tool — every language-specific value comes from
``pair.{mirror_prune_segments,preserved_source_suffix,
companion_extensions,companion_stub_comment,tracker_filename}`` and
``pair.source_extensions``.

`mirror` precedes the workspace-state stages: it makes ONE read-only
bridge/DB lookup (the workspace-bound pair + paths set by `init`), writes
no `codeconv`-schema rows, and does not touch phase tracking. It stages
its output (sibling tmp dir) and atomically moves it into place so a
failure leaves the live tree untouched (FR-037).
"""

from __future__ import annotations

import json as _json
import shutil
from pathlib import Path
from typing import Any, Optional

from sqlalchemy import text

from codeconv.bridge_client import acquire_or_discover
from codeconv.db.engine import build_engine

# Exit codes — contract § Exit codes (shared convention with the other
# tools: 0 ok / 1 generic / 2 prereq-or-refused / 5 pair).
_EXIT_OK = 0
_EXIT_GENERIC = 1
_EXIT_PREREQ = 2
_EXIT_PAIR = 5

_STAGING_SUFFIX = ".codeconv-mirror-tmp"
_OLD_SUFFIX = ".codeconv-mirror-old"


def register(dbos_app: Any) -> None:
    """No-op stub for the feature-012 tool contract.

    `mirror` makes no durable DB writes; on-disk crash-safety is the
    staged-write + atomic-move (FR-037).
    """
    return None


def _read_settings(engine: Any) -> dict[str, str]:
    with engine.connect() as conn:
        rows = conn.execute(
            text("SELECT key, value FROM codeconv.workspace_settings")
        ).all()
    return {k: v for k, v in rows}


def _is_source_file(name: str, source_exts: tuple[str, ...]) -> bool:
    return any(name.endswith(ext) for ext in source_exts)


def _strip_source_ext(name: str, source_exts: tuple[str, ...]) -> str:
    """Return the filename with the matched trailing source extension
    removed (spec-001 FR-005: replace the trailing source ext only).

    ``foo.dart`` -> ``foo``; ``foo.bar.dart`` -> ``foo.bar``.
    """
    for ext in source_exts:
        if name.endswith(ext):
            return name[: -len(ext)]
    return name


def _atomic_move(staging: Path, live: Path, *, replace: bool) -> None:
    """Atomically move ``staging`` over ``live`` (scaffold parity:
    3-step compensating rename when a live target exists)."""
    old = live.parent / (live.name + _OLD_SUFFIX)
    if old.exists():
        shutil.rmtree(old, ignore_errors=True)
    if live.exists():
        if not replace:
            raise RuntimeError(f"refusing to overwrite {live}")
        live.replace(old)
        try:
            staging.replace(live)
        except OSError:
            if not live.exists() and old.exists():
                old.replace(live)
            raise
        shutil.rmtree(old, ignore_errors=True)
    else:
        live.parent.mkdir(parents=True, exist_ok=True)
        staging.replace(live)


def _walk_sorted(root: Path, prune: frozenset[str], exclude_spec=None):
    """Deterministic recursive walk (spec-001 FR-002 + FR-043).

    Yields ``(abs_dir, rel_posix_dir, sorted_subdir_names,
    sorted_file_names)``. A subdir is pruned (not descended into, never
    yielded) when its NAME is in ``prune`` (effective standard set minus
    force-includes — FR-042) OR its output-root-relative POSIX path
    matches ``exclude_spec`` as a directory (gitignore-style — FR-043).
    File-level ``exclude_spec`` skipping is the caller's responsibility
    (it must skip files whose rel path matches ``exclude_spec``).
    """
    stack = [root]
    while stack:
        cur = stack.pop()
        try:
            entries = sorted(cur.iterdir(), key=lambda p: p.name)
        except OSError:
            continue
        subdirs: list[str] = []
        files: list[str] = []
        for e in entries:
            if e.is_dir():
                if e.name in prune:
                    continue
                if exclude_spec is not None:
                    child_rel = e.relative_to(root).as_posix()
                    if exclude_spec.match(child_rel, is_dir=True):
                        continue
                subdirs.append(e.name)
            elif e.is_file():
                files.append(e.name)
        rel = cur.relative_to(root).as_posix()
        yield cur, ("" if rel == "." else rel), subdirs, files
        # Push in reverse-sorted order so pops are ascending.
        for d in sorted(subdirs, reverse=True):
            stack.append(cur / d)


def run_mirror(
    *,
    repo_root: Path,
    refresh: bool = False,
    data_dir: Optional[Path] = None,
    quiet: bool = False,
) -> dict:
    """Produce the inventory subtree from the source-language tree
    (contract behaviour 1–7; spec-001 FR-002..FR-014 via pair hooks)."""
    repo_root = Path(repo_root).resolve()

    # 1. Acquire-or-discover the shared bridge (one read-only lookup).
    endpoint = acquire_or_discover(
        repo_root, ready_timeout=60.0, data_dir=data_dir
    )
    engine = build_engine(endpoint)

    # 2. Resolve the pair SOLELY from the workspace (FR-028 / D6 /
    #    FR-004). Pair selection is owned by `codeconv init`.
    from codeconv import langpairs

    settings = _read_settings(engine)
    src_lang = settings.get("source_lang")
    tgt_lang = settings.get("target_lang")
    mirror_source_root = settings.get("mirror_source_root")
    output_rel = settings.get("source_path")

    if not (src_lang and tgt_lang) or not mirror_source_root or not output_rel:
        return {
            "ok": False,
            "exit_code": _EXIT_PREREQ,
            "error": "no initialised workspace; run `codeconv init` first",
        }
    try:
        pair = langpairs.get(src_lang, tgt_lang)
    except langpairs.UnknownLangPair as exc:
        return {"ok": False, "exit_code": _EXIT_PAIR, "error": str(exc)}

    # 3. Resolve I/O roots (FR-029). input = mirror_source_root;
    #    output = workspace source_path (the inventory subtree).
    input_root = (repo_root / mirror_source_root).resolve()
    output_root = (repo_root / output_rel).resolve()

    if not input_root.is_dir():
        return {
            "ok": False,
            "exit_code": _EXIT_PREREQ,
            "error": (
                f"mirror source {mirror_source_root!r} does not exist or "
                f"is not a directory ({input_root})"
            ),
        }
    # Output == or nested in input → refuse (spec-001 FR-014).
    if output_root == input_root or input_root in output_root.parents:
        return {
            "ok": False,
            "exit_code": _EXIT_PREREQ,
            "error": (
                f"output {output_rel!r} is the same as or nested inside "
                f"the mirror source {mirror_source_root!r}"
            ),
        }

    # 4. Refuse an existing output unless --refresh (spec-001 FR-011).
    output_exists = output_root.exists()
    if output_exists and not refresh:
        return {
            "ok": False,
            "exit_code": _EXIT_PREREQ,
            "error": (
                f"output {output_rel!r} already exists. Re-run with "
                f"--refresh to refresh it (preserves companion files + "
                f"tracker; the /codeconv-mirror skill drives this "
                f"confirmation)."
            ),
        }

    source_exts = tuple(pair.source_extensions())
    # FR-042: effective prune = pair standard set MINUS workspace
    # force-includes (--include-pruned, recorded by init).
    force_include = {
        s.strip()
        for s in (settings.get("mirror_force_include") or "")
        .replace(",", "\n")
        .splitlines()
        if s.strip()
    }
    prune = frozenset(
        seg
        for seg in pair.mirror_prune_segments()
        if seg not in force_include
    )
    # FR-043: gitignore-style mirror exclusions (--mirror-exclude),
    # internal matcher, no new dependency.
    from .gitignore import GitignoreSpec

    exclude_spec = GitignoreSpec.from_lines(
        (settings.get("mirror_exclude_patterns") or "").splitlines()
    )
    spec = exclude_spec if exclude_spec else None

    def _excluded_file(rel_dir: str, name: str) -> bool:
        if spec is None:
            return False
        rp = f"{rel_dir}/{name}" if rel_dir else name
        return spec.match(rp, is_dir=False)

    preserved_suffix = pair.preserved_source_suffix()
    companion_exts = tuple(pair.companion_extensions())
    tracker_name = pair.tracker_filename()

    # 5. Pre-flight collision pass (spec-001 FR-012): a companion file
    #    that would collide with a pre-existing NON-source file of the
    #    same name in the same source folder. Report ALL, write nothing.
    collisions: list[str] = []
    for _abs, rel, _subdirs, files in _walk_sorted(
        input_root, prune, spec
    ):
        files = [f for f in files if not _excluded_file(rel, f)]
        nonsource = {
            f for f in files if not _is_source_file(f, source_exts)
        }
        for f in files:
            if not _is_source_file(f, source_exts):
                continue
            stem = _strip_source_ext(f, source_exts)
            for cext in companion_exts:
                cand = f"{stem}{cext}"
                if cand in nonsource:
                    where = f"{rel}/" if rel else ""
                    collisions.append(f"{where}{cand} (vs source {where}{f})")
    if collisions:
        return {
            "ok": False,
            "exit_code": _EXIT_GENERIC,
            "error": (
                "companion-file collisions with pre-existing non-source "
                "files (nothing written): " + "; ".join(sorted(collisions))
            ),
            "collisions": sorted(collisions),
        }

    # 6. Stage the produced tree under <output>.codeconv-mirror-tmp/.
    staging = output_root.parent / (output_root.name + _STAGING_SUFFIX)
    if staging.exists():
        shutil.rmtree(staging, ignore_errors=True)
    staging.mkdir(parents=True, exist_ok=True)

    dirs_created = 0
    nonsource_copied = 0
    source_preserved = 0
    companions_generated = 0
    newly_found: list[str] = []
    tracker_records: list[dict] = []

    try:
        for abs_dir, rel, _subdirs, files in _walk_sorted(
            input_root, prune, spec
        ):
            staged_dir = staging / rel if rel else staging
            staged_dir.mkdir(parents=True, exist_ok=True)
            if rel:
                dirs_created += 1
            for f in files:
                if _excluded_file(rel, f):
                    continue
                src_file = abs_dir / f
                if _is_source_file(f, source_exts):
                    # spec-001 FR-004: preserve as <name><suffix>.
                    preserved = staged_dir / f"{f}{preserved_suffix}"
                    shutil.copyfile(src_file, preserved)
                    source_preserved += 1
                    stem = _strip_source_ext(f, source_exts)
                    rel_pref = (
                        f"{rel}/{f}{preserved_suffix}"
                        if rel
                        else f"{f}{preserved_suffix}"
                    )
                    # spec-001 FR-011 (--refresh): preserve a pre-existing
                    # companion byte-identical; only newly-found source
                    # files get fresh stubs.
                    live_dir = output_root / rel if rel else output_root
                    is_new = refresh and not any(
                        (live_dir / f"{stem}{ce}").is_file()
                        for ce in companion_exts
                    )
                    if refresh and not is_new and output_exists:
                        comp_entries = []
                        for cext in companion_exts:
                            existing = live_dir / f"{stem}{cext}"
                            staged_comp = staged_dir / f"{stem}{cext}"
                            if existing.is_file():
                                shutil.copyfile(existing, staged_comp)
                            else:
                                staged_comp.write_text(
                                    pair.companion_stub_comment(cext, f)
                                    + "\n",
                                    encoding="utf-8",
                                )
                                companions_generated += 1
                            comp_entries.append(
                                {"file": f"{stem}{cext}", "status": "todo"}
                            )
                    else:
                        if is_new:
                            newly_found.append(rel_pref)
                        comp_entries = []
                        for cext in companion_exts:
                            staged_comp = staged_dir / f"{stem}{cext}"
                            staged_comp.write_text(
                                pair.companion_stub_comment(cext, f) + "\n",
                                encoding="utf-8",
                            )
                            companions_generated += 1
                            comp_entries.append(
                                {"file": f"{stem}{cext}", "status": "todo"}
                            )
                    tracker_records.append(
                        {"source": rel_pref, "companions": comp_entries}
                    )
                else:
                    # spec-001 FR-003: non-source byte-identical copy.
                    shutil.copyfile(src_file, staged_dir / f)
                    nonsource_copied += 1

        # 7. Tracker (spec-001 FR-007..FR-010). On --refresh of an
        #    existing output, the existing tracker is preserved
        #    byte-identical (FR-011 f); a fresh run writes it new.
        live_tracker = output_root / tracker_name
        if refresh and output_exists and live_tracker.is_file():
            shutil.copyfile(live_tracker, staging / tracker_name)
        else:
            (staging / tracker_name).write_text(
                _json.dumps(tracker_records, indent=2, sort_keys=True),
                encoding="utf-8",
            )

        _atomic_move(
            staging, output_root, replace=output_exists
        )
    finally:
        if staging.exists():
            shutil.rmtree(staging, ignore_errors=True)

    return {
        "ok": True,
        "exit_code": _EXIT_OK,
        "pair": list(pair.key()),
        "mirror_source_root": mirror_source_root,
        "output_path": output_rel,
        "refreshed": bool(refresh),
        "dirs_created": dirs_created,
        "nonsource_copied": nonsource_copied,
        "source_preserved": source_preserved,
        "companions_generated": companions_generated,
        "tracker_records": len(tracker_records),
        "tracker_file": tracker_name,
        "newly_found": sorted(newly_found),
    }


__all__ = ["register", "run_mirror"]

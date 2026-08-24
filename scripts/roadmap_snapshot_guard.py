#!/usr/bin/env python3
"""Signed Snapshot Integrity Guard (spec-061).

Detects, prevents, and helps repair corruption of the Ed25519-signed roadmap
export snapshots under ``.specify/roadmap-sync/exports/``. A rename/codemod sweep
(e.g. the speckit->buildkit rebrand) that rewrites text inside these signed JSON
files invalidates their signatures; this guard catches that before it reaches a
ship, keeps git from normalizing the bytes, and offers a guided repair.

Verification reuses the AUTHORITATIVE primitive shipped in
``buildkit_cli.roadmap.crdt.sign`` when it is importable (the exact code path the
roadmap ``import`` uses). Where ``buildkit_cli`` is unavailable (e.g. a minimal CI
runner), it falls back to a self-contained verifier built on the ``cryptography``
library that recomputes the SAME canonical bytes and Ed25519-verifies against the
same trust ``.pub`` files. A parity test proves the fallback is byte/verdict
identical to the authoritative module, so the fallback can never silently diverge
(spec FR-015).

CLI: ``python scripts/roadmap_snapshot_guard.py {verify|check-staged|repair|list-protected} [flags]``
Exit codes: 0 pass/override · 1 guard failure (corrupted/unverifiable) · 2 usage/env error.
"""
from __future__ import annotations

import argparse
import base64
import json
import subprocess
import sys
from pathlib import Path
from typing import Iterable, Optional

# Verdict taxonomy (spec / data-model.md; PROTECTED-CHANGE added by spec-063).
PASS = "PASS"
CORRUPTED = "CORRUPTED"
UNVERIFIABLE = "UNVERIFIABLE"
UNSIGNED = "UNSIGNED"
PROTECTED_CHANGE = "PROTECTED-CHANGE"

# Protected-artifact kinds (spec-063 D1). signed-json = Ed25519-verified snapshot;
# trust-key = byte-immutable public key (rotation needs --allow-protected-change).
KIND_SIGNED_JSON = "signed-json"
KIND_TRUST_KEY = "trust-key"
_KNOWN_KINDS = {KIND_SIGNED_JSON, KIND_TRUST_KEY}

DEFAULT_GLOBS = [".specify/roadmap-sync/exports/*.json"]
DEFAULT_PROTECTED = [
    (".specify/roadmap-sync/exports/*.json", KIND_SIGNED_JSON),
    (".specify/roadmap-sync/trust/*.pub", KIND_TRUST_KEY),
]
_TRUST_RELDIR = Path(".specify") / "roadmap-sync" / "trust"
_PROTECTED_REL = Path(".specify") / "roadmap-sync" / "protected-paths.txt"
_CONFIG_REL = Path(".specify") / "roadmap-sync" / "guard.config.json"
_QUARANTINE_REL = Path(".specify") / "roadmap-sync" / "exports" / "quarantine"

_DEFAULT_POLICY = {
    "gate": {"ship": "block", "pre_commit": "block", "ci": "block", "none": "block"},
    "unsigned": "warn",
}


# --------------------------------------------------------------------------- #
# repo root                                                                    #
# --------------------------------------------------------------------------- #
def repo_root(start: Optional[Path] = None) -> Path:
    """Resolve the repository root (git toplevel; else the nearest dir with .specify)."""
    start = Path(start or Path.cwd()).resolve()
    try:
        out = subprocess.run(
            ["git", "-C", str(start), "rev-parse", "--show-toplevel"],
            capture_output=True, text=True, check=True,
        )
        return Path(out.stdout.strip()).resolve()
    except (subprocess.CalledProcessError, FileNotFoundError, OSError):
        cur = start
        while cur != cur.parent:
            if (cur / ".specify").is_dir():
                return cur
            cur = cur.parent
        return start


# --------------------------------------------------------------------------- #
# verification: authoritative primitive with a parity-gated fallback           #
# --------------------------------------------------------------------------- #
# Canonicalisation parity with ``buildkit_cli.roadmap.crdt.sign`` (spec-065).
# The signed byte-set is VERSIONED: v2 (current) excludes only ``signature``;
# v1 (legacy, pre-spec-065) also excludes ``key_id`` and ``heads``. A fallback
# that hardcodes either one silently mis-verifies documents of the other
# version — see ``_FALLBACK_MAX_CANONICAL_VERSION`` below.
_CANONICAL_VERSION_FIELD = "canonical_version"
_CANONICAL_V1 = 1
_CANONICAL_V2 = 2
_V1_EXCLUDED = ("signature", "key_id", "heads")
#: Highest ``canonical_version`` this fallback actually implements. A document
#: declaring a HIGHER version is reported UNVERIFIABLE (never CORRUPTED): this
#: verifier cannot compute its signed bytes, which is a verifier-side gap and is
#: never evidence that the export's content was altered.
_FALLBACK_MAX_CANONICAL_VERSION = _CANONICAL_V2


def _fallback_canonical_bytes(document: dict) -> bytes:
    """Reproduce ``buildkit_cli.roadmap.crdt.sign.canonical_bytes`` exactly.

    Version-aware, per the document's own ``canonical_version``: v2 excludes only
    ``signature``; v1 (default when the field is absent) also excludes ``key_id``
    and ``heads``. Serialized sorted-keys + compact (separators ``(",", ":")``),
    ``ensure_ascii=False``, UTF-8.
    """
    version = document.get(_CANONICAL_VERSION_FIELD, _CANONICAL_V1)
    excluded = ("signature",) if version >= _CANONICAL_V2 else _V1_EXCLUDED
    payload = {k: v for k, v in document.items() if k not in excluded}
    return json.dumps(
        payload, sort_keys=True, ensure_ascii=False, separators=(",", ":")
    ).encode("utf-8")


def _fallback_public_key_for(key_id: str, root: Path) -> Optional[bytes]:
    path = root / _TRUST_RELDIR / f"{key_id}.pub"
    try:
        b64 = path.read_text(encoding="utf-8").strip()
        return base64.b64decode(b64) if b64 else None
    except FileNotFoundError:
        return None
    except (OSError, ValueError):
        return None


def _fallback_verify_document(document: dict, root: Path) -> str:
    """Self-contained verify → 'valid'/'invalid'/'unsigned'/'untrusted' (mirrors sign.py)."""
    from cryptography.exceptions import InvalidSignature
    from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PublicKey

    signature = document.get("signature")
    key_id = document.get("key_id")
    if not signature or not key_id:
        return "unsigned"
    # Refuse rather than guess: computing the wrong canonical bytes would report a
    # pristine export as CORRUPTED (content altered) when the real fault is that
    # this verifier does not implement that canonical version.
    if document.get(_CANONICAL_VERSION_FIELD, _CANONICAL_V1) > _FALLBACK_MAX_CANONICAL_VERSION:
        return "unsupported_canonical"
    public_bytes = _fallback_public_key_for(key_id, root)
    if public_bytes is None:
        return "untrusted"
    try:
        public_key = Ed25519PublicKey.from_public_bytes(public_bytes)
    except ValueError:
        # Key present but not a valid Ed25519 key — a KEY-side failure, never
        # attributable to the export's content (spec-063 D5).
        return "untrusted"
    try:
        public_key.verify(base64.b64decode(signature), _fallback_canonical_bytes(document))
        return "valid"
    except (InvalidSignature, ValueError):
        return "invalid"


def _key_suspect(key_id: Optional[str], root: Path) -> Optional[str]:
    """If the trust key for *key_id* is missing/corrupt/locally-modified, say why.

    An 'invalid' signature is cryptographically ambiguous between a rewritten export
    and a rewritten (same-length) key. Attribute to the KEY when the .pub is absent,
    does not decode to 32 raw bytes, or differs from its committed (HEAD) bytes —
    a tracked trust key must never drift in the worktree (spec-063 D5/FR-004).
    """
    if not key_id:
        return None
    rel = (_TRUST_RELDIR / f"{key_id}.pub").as_posix()
    path = root / rel
    if not path.is_file():
        return f"trust key {rel} is missing"
    try:
        decoded = base64.b64decode(path.read_text(encoding="utf-8").strip(), validate=True)
    except (OSError, ValueError):
        return f"trust key {rel} is unreadable/corrupt (not base64)"
    if len(decoded) != 32:
        return f"trust key {rel} is corrupt (decodes to {len(decoded)} bytes, expected 32)"
    head = _git_show_bytes(root, "HEAD", rel)
    if head is not None and head.decode("utf-8", "replace").strip() != \
            path.read_text(encoding="utf-8").strip():
        return f"trust key {rel} differs from its committed bytes (locally modified)"
    return None


def verification_backend():
    """Return ('buildkit', module) if the authoritative primitive imports, else ('fallback', None).

    Only an *absence* (ImportError) routes to the fallback; a genuinely broken but
    present authoritative module (e.g. a partial install raising something else on
    import) must surface, not be silently masked as a normal fallback (review F4).
    """
    try:
        from buildkit_cli.roadmap.crdt import sign  # type: ignore
        return "buildkit", sign
    except ImportError:
        return "fallback", None


_STATUS_TO_VERDICT = {
    "valid": PASS,
    "invalid": CORRUPTED,
    "untrusted": UNVERIFIABLE,
    "unsigned": UNSIGNED,
    "unsupported_canonical": UNVERIFIABLE,
}
_VERDICT_REASON = {
    PASS: None,
    CORRUPTED: "signature does not verify (content altered since signing)",
    UNVERIFIABLE: "signed by a key_id with no usable trust .pub on this host",
    UNSIGNED: "no signature/key_id (legacy unsigned snapshot)",
    PROTECTED_CHANGE: "protected artifact leaving the protected set",
}


def _raw_status(document: dict, root: Path) -> str:
    backend, sign = verification_backend()
    if backend == "buildkit":
        return sign.verify_document(document, cur=None, project_root=str(root))
    return _fallback_verify_document(document, root)


def _verdict_for_bytes(rel: str, raw: bytes, root: Path) -> dict:
    """Verify already-read bytes → {path, verdict, key_id, reason}."""
    try:
        document = json.loads(raw.decode("utf-8"))
    except (ValueError, UnicodeDecodeError) as exc:
        return {"path": rel, "verdict": CORRUPTED, "key_id": None,
                "reason": f"unreadable/invalid JSON: {exc}"}
    status = _raw_status(document, root)
    key_id = document.get("key_id")
    # Key-side attribution (spec-063 FR-004): a failing signature whose trust key is
    # itself missing/corrupt/locally-modified is the KEY's failure — report it at the
    # key as UNVERIFIABLE, never as CORRUPTED of a possibly-pristine export.
    if status in ("invalid", "untrusted"):
        suspect = _key_suspect(key_id, root)
        if suspect is not None:
            return {"path": rel, "verdict": UNVERIFIABLE, "key_id": key_id,
                    "reason": suspect}
    if status == "unsupported_canonical":
        return {"path": rel, "verdict": UNVERIFIABLE, "key_id": key_id,
                "reason": (
                    "canonical_version "
                    f"{document.get(_CANONICAL_VERSION_FIELD)} exceeds the fallback "
                    f"verifier's maximum ({_FALLBACK_MAX_CANONICAL_VERSION}); install "
                    "buildkit_cli so the authoritative signer canonicalisation is used"
                )}
    verdict = _STATUS_TO_VERDICT.get(status, UNVERIFIABLE)
    return {"path": rel, "verdict": verdict, "key_id": key_id,
            "reason": _VERDICT_REASON[verdict]}


def verify_artifact(path: Path, root: Path) -> dict:
    """Verify one on-disk artifact read-only. Returns {path, verdict, key_id, reason}."""
    rel = _relpath(path, root)
    try:
        raw = path.read_bytes()
    except OSError as exc:
        return {"path": rel, "verdict": CORRUPTED, "key_id": None,
                "reason": f"unreadable: {exc}"}
    return _verdict_for_bytes(rel, raw, root)


def verify_staged_artifact(rel: str, root: Path) -> dict:
    """Verify the STAGED (index) blob of *rel* — the bytes a commit will actually record.

    A pre-commit hook must judge the index content, not the working tree: otherwise a
    corrupt blob staged with `git add` and then reverted in the working tree (without
    re-staging) would pass the gate and be committed (review F1). Falls back to the
    working-tree file when the path has no staged blob.
    """
    try:
        out = subprocess.run(["git", "-C", str(root), "show", f":{rel}"],
                             capture_output=True, check=True)
        return _verdict_for_bytes(rel, out.stdout, root)
    except (subprocess.CalledProcessError, FileNotFoundError, OSError):
        return verify_artifact(root / rel, root)


# --------------------------------------------------------------------------- #
# enumeration                                                                  #
# --------------------------------------------------------------------------- #
def _relpath(path: Path, root: Path) -> str:
    try:
        return path.resolve().relative_to(root).as_posix()
    except ValueError:
        return path.as_posix()


def _glob_variants(pattern: str) -> list[str]:
    """Every glob spelling needed to enumerate *pattern* identically on all
    supported Pythons.

    ``pathlib`` changed ``**`` semantics in 3.13: before it matched only
    DIRECTORIES, so ``root.glob("a/**")`` yielded no files at all and the
    ``is_file()`` filter in :func:`enumerate_artifacts` discarded everything.
    A registry widened to ``.specify/roadmap-sync/**`` therefore enumerated —
    and so protected — NOTHING on 3.12, while ``_matches_any`` still claimed
    the same paths were covered. That is a silent fail-open on exactly the
    signed artifacts this guard exists to protect, and it is invisible to any
    host running 3.13+.

    Appending the explicit ``/*`` spelling makes a trailing ``**`` match files
    at any depth on every version. The results are unioned into a set, so the
    extra variant is a no-op where ``**`` already matches files."""
    variants = [pattern]
    if pattern == "**" or pattern.endswith("/**"):
        variants.append(pattern + "/*")
    return variants


def enumerate_artifacts(root: Path, globs: Optional[Iterable[str]] = None) -> list[Path]:
    """Deterministic, sorted enumeration of the declared glob set (FR-001).

    Enumeration MUST stay identical across Python versions: protection is
    checked with :func:`_matches_any` but derived from what this function
    finds, so anything it misses is silently unprotected (review F7)."""
    globs = list(globs) if globs else list(DEFAULT_GLOBS)
    found: set[Path] = set()
    for pattern in globs:
        for spelling in _glob_variants(pattern):
            for p in root.glob(spelling):
                if p.is_file():
                    found.add(p.resolve())
    return sorted(found, key=lambda p: _relpath(p, root))


# --------------------------------------------------------------------------- #
# policy                                                                        #
# --------------------------------------------------------------------------- #
_GATE_ACTIONS = {"block", "warn"}
_UNSIGNED_ACTIONS = {"warn", "block", "pass"}


class ConfigError(ValueError):
    """A guard.config.json value is invalid — fail closed rather than fail open (review F5)."""


def resolve_policy(root: Path, config_path: Optional[Path] = None) -> dict:
    """Baked-in defaults, overlaid by an optional guard.config.json.

    Config values are validated against the allowed action sets: an unknown value
    (e.g. a typo ``"blok"``) raises ConfigError instead of silently failing open —
    otherwise an unsigned/corrupt artifact could be accepted by a mistyped policy.
    """
    policy = json.loads(json.dumps(_DEFAULT_POLICY))  # deep copy
    cfg = config_path or (root / _CONFIG_REL)
    try:
        override = json.loads(Path(cfg).read_text(encoding="utf-8"))
    except FileNotFoundError:
        return policy
    except (OSError, ValueError):
        return policy  # unreadable/invalid JSON → safe baked-in defaults
    if isinstance(override.get("gate"), dict):
        for gate_name, action in override["gate"].items():
            if action not in _GATE_ACTIONS:
                raise ConfigError(
                    f"guard.config.json gate.{gate_name}={action!r} invalid; "
                    f"expected one of {sorted(_GATE_ACTIONS)}")
            policy["gate"][gate_name] = action
    if "unsigned" in override:
        if override["unsigned"] not in _UNSIGNED_ACTIONS:
            raise ConfigError(
                f"guard.config.json unsigned={override['unsigned']!r} invalid; "
                f"expected one of {sorted(_UNSIGNED_ACTIONS)}")
        policy["unsigned"] = override["unsigned"]
    return policy


# --------------------------------------------------------------------------- #
# report                                                                        #
# --------------------------------------------------------------------------- #
def build_report(root: Path, globs, gate: str, policy: dict,
                 unsigned_override: Optional[str], allow_corrupt: Optional[str],
                 only_paths: Optional[list[Path]] = None,
                 staged_results: Optional[list[dict]] = None,
                 allow_protected: Optional[str] = None) -> dict:
    globs = list(globs) if globs else list(DEFAULT_GLOBS)
    unsigned_action = unsigned_override or policy.get("unsigned", "warn")
    gate_action = policy["gate"].get(gate, "block")

    if staged_results is not None:
        # check-staged: precomputed by classify_staged — index blobs judged (review F1),
        # removals/rotations already classified PROTECTED-CHANGE (spec-063 D2/D3).
        results = staged_results
    else:
        artifacts = only_paths if only_paths is not None else enumerate_artifacts(root, globs)
        tracked = _tracked_set(root)
        results = []
        for p in artifacts:
            row = verify_artifact(p, root)
            # None = indeterminate (git failed) — omit the flag rather than guess.
            row["tracked"] = (row["path"] in tracked) if tracked is not None else None
            results.append(row)

    def _fails(verdict: str) -> bool:
        if verdict in (CORRUPTED, UNVERIFIABLE, PROTECTED_CHANGE):
            return True
        if verdict == UNSIGNED:
            return unsigned_action == "block"
        return False

    override = None
    covered: list[str] = []
    if allow_protected is not None:
        # --allow-protected-change covers ONLY protected-set changes (removal, key
        # rotation) — never signature failures (spec-063 FR-007).
        covered = [r["path"] for r in results if r["verdict"] == PROTECTED_CHANGE]
        if covered:
            override = {"kind": "protected-change", "reason": allow_protected,
                        "covered": covered}

    offending = [r["path"] for r in results
                 if _fails(r["verdict"]) and r["path"] not in covered]
    if offending and allow_corrupt is not None and gate == "ship":
        override = {"reason": allow_corrupt}

    if not offending:
        exit_code = 0
    elif override is not None and override.get("kind") != "protected-change":
        exit_code = 0
    elif gate_action == "warn":
        exit_code = 0
    else:
        exit_code = 1

    return {
        "globs": globs,
        "scanned": len(results),
        "results": results,
        "offending": offending,
        "policy": {"gate": gate, "action": gate_action, "unsigned": unsigned_action},
        "override": override,
        "exit_code": exit_code,
    }


def render_human(report: dict, out=None, err=None) -> None:
    out = out if out is not None else sys.stdout
    err = err if err is not None else sys.stderr
    globs = ", ".join(report["globs"])
    print(f"snapshot-guard: scanned {report['scanned']} artifact(s) under {globs}", file=out)
    for r in report["results"]:
        line = f"  {r['verdict']:<17} {r['path']}"
        if r.get("tracked") is False:
            line += "  [untracked]"
        if r["reason"]:
            line += f"  ({r['reason']})"
        if r.get("tracked") is False and r["verdict"] in (CORRUPTED, UNVERIFIABLE):
            line += "  — repair unavailable (no history); `quarantine` is the sanctioned path"
        stream = out if r["verdict"] == PASS else err
        print(line, file=stream)
    override = report["override"]
    if override is not None and override.get("kind") == "protected-change":
        print(f"OVERRIDE (--allow-protected-change): {override['reason']} — covering "
              f"{len(override['covered'])} protected-set change(s): "
              f"{', '.join(override['covered'])}.", file=err)
    if override is not None and override.get("kind") != "protected-change":
        print(f"OVERRIDE (--allow-corrupt): {override['reason']} — "
              f"proceeding despite {len(report['offending'])} failing artifact(s).", file=err)
    elif report["offending"]:
        verb = "WARN" if report["exit_code"] == 0 else "FAIL"
        print(f"{verb} — {len(report['offending'])} failing snapshot(s): "
              f"{', '.join(report['offending'])}. See `repair` to restore, `quarantine` "
              f"for unrepairable untracked artifacts, or investigate the sweep.",
              file=err)
    elif report["scanned"] == 0:
        print("OK — no signed snapshots in scope (scanned 0).", file=out)
    else:
        print("OK — all signed snapshots verify.", file=out)


# --------------------------------------------------------------------------- #
# protected-paths registry                                                     #
# --------------------------------------------------------------------------- #
def load_protected(root: Path) -> list[tuple[str, str]]:
    """Return the declared protected (glob, kind) pairs.

    Kind is carried in the line's comment — ``<glob>  # kind=trust-key`` — the one
    syntax a pre-063 parser degrades on SAFELY: it comment-strips to the bare glob and
    still protects it (over-block, never under-protect — spec-063 FR-003). Absent kind
    means signed-json. An unknown kind refuses (fail-closed, like guard.config values).
    """
    path = root / _PROTECTED_REL
    pairs: list[tuple[str, str]] = []
    try:
        for line in path.read_text(encoding="utf-8").splitlines():
            token, _, comment = line.partition("#")
            token = token.strip()
            if not token:
                continue
            kind = KIND_SIGNED_JSON
            for word in comment.split():
                if word.startswith("kind="):
                    kind = word[len("kind="):]
            if kind not in _KNOWN_KINDS:
                raise ConfigError(
                    f"protected-paths.txt kind={kind!r} for glob {token!r} invalid; "
                    f"expected one of {sorted(_KNOWN_KINDS)}")
            pairs.append((token, kind))
    except FileNotFoundError:
        return list(DEFAULT_PROTECTED)
    return pairs or list(DEFAULT_PROTECTED)


def _git_changed_status(root: Path) -> list[tuple[str, str, Optional[str]]]:
    """Staged + worktree changes as (status, path, new_path) rows, for check-staged.

    Uses ``--name-status -z`` so deletions (D) and BOTH sides of renames (R) are
    visible — the shipped 061 filter (`--name-only`, no D) let a protected artifact
    leave the protected set unseen (spec-063 FR-001). Status is the single-letter
    class (score stripped); new_path is set only for renames/copies.
    """
    rows: dict[tuple[str, str, Optional[str]], None] = {}
    for args in (["diff", "--cached", "--name-status", "-z", "-M", "--diff-filter=ACDMRT"],
                 ["diff", "--name-status", "-z", "-M", "--diff-filter=ACDMRT"]):
        try:
            out = subprocess.run(["git", "-C", str(root), *args],
                                 capture_output=True, text=True, check=True)
        except (subprocess.CalledProcessError, FileNotFoundError, OSError) as exc:
            # An unavailable change set must never read as an EMPTY one — that would
            # silently disable every removal/rotation protection (codex review F4).
            raise RuntimeError(f"git diff failed; cannot determine staged changes "
                               f"({exc}); refusing to pass an unknown change set")
        fields = [f for f in out.stdout.split("\0")]
        i = 0
        while i < len(fields):
            status = fields[i].strip()
            if not status:
                i += 1
                continue
            letter = status[0]
            if letter in ("R", "C") and i + 2 < len(fields):
                rows[(letter, fields[i + 1], fields[i + 2])] = None
                i += 3
            elif i + 1 < len(fields):
                rows[(letter, fields[i + 1], None)] = None
                i += 2
            else:
                break
    return sorted(rows)


def _glob_match(rel: str, pattern: str) -> bool:
    """True iff ``root.glob(pattern)`` would include ``rel`` — the SAME engine as
    enumeration, so protection matching and discovery never diverge (review F6/F7).

    Uses ``PurePosixPath.full_match`` (Python 3.13+), which is anchored and honors
    recursive ``**`` exactly like ``Path.glob``. On older interpreters it falls back
    to a translation where ``**`` crosses '/' and ``*`` does not.
    """
    from pathlib import PurePosixPath
    rp = PurePosixPath(rel)
    full_match = getattr(rp, "full_match", None)
    if full_match is not None:
        return full_match(pattern)
    # Fallback (pre-3.13): translate to a regex with recursive ** / non-crossing *.
    import re
    parts, i = [], 0
    while i < len(pattern):
        if pattern[i:i + 2] == "**":
            parts.append(".*")
            i += 2
            if i < len(pattern) and pattern[i] == "/":
                i += 1
        elif pattern[i] == "*":
            parts.append("[^/]*")
            i += 1
        elif pattern[i] == "?":
            parts.append("[^/]")
            i += 1
        else:
            parts.append(re.escape(pattern[i]))
            i += 1
    return re.fullmatch("".join(parts), rel) is not None


def _matches_any(rel: str, globs: Iterable[str]) -> bool:
    """An artifact is protected iff some registry glob would also enumerate it."""
    return any(_glob_match(rel, g) for g in globs)


def _kind_for(rel: str, protected: list[tuple[str, str]]) -> Optional[str]:
    """The kind of the first registry glob matching *rel*, or None (unprotected)."""
    for pattern, kind in protected:
        if _glob_match(rel, pattern):
            return kind
    return None


def classify_staged(root: Path, protected: list[tuple[str, str]]) -> list[dict]:
    """Classify every staged/worktree change touching the protected set (spec-063 D2/D3).

    Removal from the protected set (deletion, or rename to an unprotected path) is a
    PROTECTED-CHANGE regardless of content; changes landing inside the set verify per
    kind: signed-json by signature (index blob), trust-key by byte-immutability —
    modification/deletion is rotation and refuses without the override; a brand-new
    key is trust EXTENSION and passes with a warning line.
    """
    results: list[dict] = []
    for status, path, new_path in _git_changed_status(root):
        old_kind = _kind_for(path, protected)
        if status == "D":
            if old_kind is not None:
                results.append({
                    "path": path, "verdict": PROTECTED_CHANGE, "key_id": None,
                    "reason": ("trust key deleted (rotation requires "
                               "--allow-protected-change)" if old_kind == KIND_TRUST_KEY
                               else "protected artifact deleted from the protected set")})
            continue
        if status in ("R", "C") and new_path is not None:
            new_kind = _kind_for(new_path, protected)
            if status == "R" and old_kind == KIND_TRUST_KEY:
                # Renaming a trust key severs its key_id binding wherever it lands —
                # that is rotation, never extension (codex review F2).
                results.append({
                    "path": path, "verdict": PROTECTED_CHANGE, "key_id": None,
                    "reason": f"trust key renamed ({path} -> {new_path}) — rotation "
                              f"requires --allow-protected-change"})
                continue
            if status == "R" and old_kind is not None and new_kind is None:
                results.append({
                    "path": path, "verdict": PROTECTED_CHANGE, "key_id": None,
                    "reason": f"protected artifact renamed out of the protected set "
                              f"({path} -> {new_path})"})
                continue
            status, path, old_kind = ("A", new_path, new_kind)  # judge the landing path
        kind = old_kind
        if kind is None:
            continue
        if kind == KIND_TRUST_KEY:
            if status == "A":
                # The commit records the INDEX blob; a worktree copy differing from it
                # would make check-staged vouch for bytes that are not being committed
                # (codex review F1) — require index == worktree for a new key.
                staged = _git_show_bytes(root, "", path)  # "" rev → ":path" = index blob
                worktree = None
                try:
                    worktree = (root / path).read_bytes()
                except OSError:
                    pass
                # Byte-level compare; only CRLF→LF is normalized (the checkout smudge
                # the repo's `-text` guard exists to prevent) — anything else that
                # differs is a real divergence (codex review F1, cycle 2).
                def _norm(b: bytes) -> bytes:
                    return b.replace(b"\r\n", b"\n")
                if staged is not None and worktree is not None and \
                        _norm(staged) != _norm(worktree):
                    results.append({
                        "path": path, "verdict": PROTECTED_CHANGE, "key_id": None,
                        "reason": "new trust key's staged bytes differ from the "
                                  "worktree copy — stage the intended key"})
                    continue
                results.append({"path": path, "verdict": PASS, "key_id": None,
                                "reason": "new trust key added (trust extension — "
                                          "review that this key is intended)"})
            else:
                results.append({
                    "path": path, "verdict": PROTECTED_CHANGE, "key_id": None,
                    "reason": "trust key modified (rotation requires "
                              "--allow-protected-change)"})
            continue
        results.append(verify_staged_artifact(path, root))
    return results


def _tracked_set(root: Path) -> Optional[set[str]]:
    """Repo-relative posix paths of all git-tracked files.

    Returns None when tracking is INDETERMINATE (git failed) — callers must fail
    closed, never treat unknown as untracked (codex review F3).
    """
    try:
        out = subprocess.run(["git", "-C", str(root), "ls-files", "-z"],
                             capture_output=True, text=True, check=True)
        return {p for p in out.stdout.split("\0") if p}
    except (subprocess.CalledProcessError, FileNotFoundError, OSError):
        return None


# --------------------------------------------------------------------------- #
# repair (git-history source of truth)                                         #
# --------------------------------------------------------------------------- #
def _git_revisions(root: Path, rel: str) -> list[str]:
    try:
        out = subprocess.run(
            ["git", "-C", str(root), "log", "--follow", "--format=%H", "--", rel],
            capture_output=True, text=True, check=True,
        )
        return [l.strip() for l in out.stdout.splitlines() if l.strip()]
    except (subprocess.CalledProcessError, FileNotFoundError, OSError):
        return []


def _git_show_bytes(root: Path, rev: str, rel: str) -> Optional[bytes]:
    try:
        out = subprocess.run(["git", "-C", str(root), "show", f"{rev}:{rel}"],
                             capture_output=True, check=True)
        return out.stdout
    except (subprocess.CalledProcessError, FileNotFoundError, OSError):
        return None


def find_last_valid_revision(path: Path, root: Path,
                             from_ref: Optional[str] = None) -> Optional[tuple[str, bytes]]:
    """Newest committed revision of *path* whose signature verifies VALID (FR-012/R3)."""
    rel = _relpath(path, root)
    revs = [from_ref] if from_ref else _git_revisions(root, rel)
    for rev in revs:
        raw = _git_show_bytes(root, rev, rel)
        if raw is None:
            continue
        try:
            document = json.loads(raw.decode("utf-8"))
        except (ValueError, UnicodeDecodeError):
            continue
        if _raw_status(document, root) == "valid":
            return rev, raw
    return None


def repair(path: Path, root: Path, from_ref: Optional[str] = None,
           dry_run: bool = False, out=None, err=None) -> int:
    out = out if out is not None else sys.stdout
    err = err if err is not None else sys.stderr
    rel = _relpath(path, root)
    found = find_last_valid_revision(path, root, from_ref)
    if found is None:
        print(f"repair: REFUSED — no committed revision of {rel} verifies; "
              f"leaving the file untouched (never re-signs/fabricates).", file=err)
        return 1
    rev, raw = found
    if dry_run:
        print(f"repair (dry-run): would restore {rel} from {rev[:12]} "
              f"({len(raw)} bytes), which verifies VALID.", file=out)
        return 0
    path.write_bytes(raw)
    # Re-verify the written bytes.
    result = verify_artifact(path, root)
    if result["verdict"] != PASS:
        print(f"repair: restored {rel} from {rev[:12]} but it still does not verify "
              f"({result['verdict']}). Manual investigation needed.", file=err)
        return 1
    print(f"repair: restored {rel} from {rev[:12]} — now verifies VALID.", file=out)
    return 0


# --------------------------------------------------------------------------- #
# quarantine (byte-preserving exit path for unrepairable untracked artifacts)  #
# --------------------------------------------------------------------------- #
def quarantine(path: Path, root: Path, dry_run: bool = False,
               config_path: Optional[Path] = None, out=None, err=None) -> int:
    out = out if out is not None else sys.stdout
    err = err if err is not None else sys.stderr
    """Move a failing UNTRACKED protected artifact byte-identically into quarantine.

    Tracked artifacts are refused (they have `repair` — history exists; quarantine must
    never shrink the tracked protected set). Passing artifacts are refused (nothing to
    quarantine). Existing quarantined names are never overwritten (spec-063 FR-006).
    """
    rel = _relpath(path, root)
    protected_globs = [g for g, _ in load_protected(root)]
    if not _matches_any(rel, protected_globs):
        print(f"quarantine: REFUSED — {rel} matches no protected glob.", file=err)
        return 1
    tracked = _tracked_set(root)
    if tracked is None:
        # Indeterminate tracking must fail closed — unknown is not untracked
        # (codex review F3).
        print("quarantine: REFUSED — cannot determine git-tracked set "
              "(git unavailable/failed); refusing to move anything.", file=err)
        return 2
    if rel in tracked:
        print(f"quarantine: REFUSED — {rel} is git-tracked; use "
              f"`repair {rel}` (history exists) instead.", file=err)
        return 1
    if not path.is_file():
        print(f"quarantine: REFUSED — {rel} does not exist.", file=err)
        return 1
    result = verify_artifact(path, root)
    unsigned_action = resolve_policy(root, config_path).get("unsigned", "warn")
    failing = result["verdict"] in (CORRUPTED, UNVERIFIABLE) or \
        (result["verdict"] == UNSIGNED and unsigned_action == "block")
    if not failing:
        # Only artifacts that actually FAIL verification under the effective policy
        # may leave the protected area (codex review F7).
        print(f"quarantine: REFUSED — {rel} is not failing verification "
              f"(verdict {result['verdict']}); nothing to quarantine.", file=err)
        return 1
    dest_dir = root / _QUARANTINE_REL
    dest = dest_dir / path.name
    if dest.exists():
        print(f"quarantine: REFUSED — {_relpath(dest, root)} already exists; "
              f"will not overwrite. Move or rename the existing file first.", file=err)
        return 1
    if dry_run:
        print(f"quarantine (dry-run): would move {rel} -> {_relpath(dest, root)} "
              f"(bytes preserved).", file=out)
        return 0
    dest_dir.mkdir(parents=True, exist_ok=True)
    payload = path.read_bytes()
    try:
        # Exclusive create — the no-overwrite guarantee holds even against a
        # concurrent writer racing the exists() check above (codex review F6).
        with open(dest, "xb") as fh:
            fh.write(payload)
    except FileExistsError:
        print(f"quarantine: REFUSED — {_relpath(dest, root)} appeared concurrently; "
              f"will not overwrite.", file=err)
        return 1
    if dest.read_bytes() != payload:
        print(f"quarantine: ERROR — byte verification failed writing "
              f"{_relpath(dest, root)}; source left untouched.", file=err)
        dest.unlink(missing_ok=True)
        return 1
    # Re-verify the source is still the bytes we preserved before removing it — a
    # concurrent writer's changes must never be silently discarded (codex F6 cycle 2).
    if path.read_bytes() != payload:
        print(f"quarantine: REFUSED — {rel} changed while quarantining; preserved "
              f"copy kept at {_relpath(dest, root)}, source left in place.", file=err)
        return 1
    path.unlink()
    print(f"quarantine: moved {rel} -> {_relpath(dest, root)} (bytes preserved; "
          f"outside every protected glob).", file=out)
    return 0


# --------------------------------------------------------------------------- #
# CLI                                                                          #
# --------------------------------------------------------------------------- #
def _emit(report: dict, as_json: bool) -> int:
    if as_json:
        print(json.dumps(report, indent=2, sort_keys=True))
    else:
        render_human(report)
    return report["exit_code"]


def main(argv: Optional[list[str]] = None) -> int:
    parser = argparse.ArgumentParser(prog="roadmap_snapshot_guard",
                                     description="Signed snapshot integrity guard (spec-061).")
    parser.add_argument("--json", action="store_true", help="emit the machine report")
    parser.add_argument("--glob", action="append", dest="globs",
                        help="override enumeration glob (repeatable)")
    parser.add_argument("--config", help="path to guard.config.json")
    parser.add_argument("--root", help="repository root (default: git toplevel)")
    sub = parser.add_subparsers(dest="cmd", required=True)

    p_verify = sub.add_parser("verify", help="enumerate + verify all signed snapshots")
    p_verify.add_argument("--gate", choices=["ship", "pre_commit", "ci", "none"], default="none")
    p_verify.add_argument("--allow-corrupt", metavar="REASON",
                          help="ship-gate override: proceed despite failures (audited)")
    p_verify.add_argument("--unsigned", choices=["warn", "block", "pass"])

    p_staged = sub.add_parser("check-staged", help="verify staged/changed protected artifacts")
    p_staged.add_argument("--gate", choices=["ship", "pre_commit", "ci", "none"], default="pre_commit")
    p_staged.add_argument("--unsigned", choices=["warn", "block", "pass"])
    p_staged.add_argument("--allow-protected-change", metavar="REASON",
                          help="permit staged protected-set changes (removal, key "
                               "rotation) for this invocation; echoed in output")

    p_repair = sub.add_parser("repair", help="restore a recoverable artifact from git history")
    p_repair.add_argument("target", help="path to the artifact to repair")
    p_repair.add_argument("--from-ref", help="restore from this git revision instead of newest-valid")
    p_repair.add_argument("--dry-run", action="store_true")

    p_quar = sub.add_parser("quarantine",
                            help="move a failing UNTRACKED artifact byte-identically "
                                 "into exports/quarantine/ (tracked -> use repair)")
    p_quar.add_argument("target", help="path to the artifact to quarantine")
    p_quar.add_argument("--dry-run", action="store_true")

    sub.add_parser("list-protected", help="print the protected-paths registry")

    args = parser.parse_args(argv)
    root = Path(args.root).resolve() if args.root else repo_root()
    config = Path(args.config) if args.config else None

    try:
        if args.cmd == "verify":
            policy = resolve_policy(root, config)
            report = build_report(root, args.globs, args.gate, policy,
                                  args.unsigned, args.allow_corrupt)
            return _emit(report, args.json)

        if args.cmd == "check-staged":
            policy = resolve_policy(root, config)
            protected = load_protected(root)
            staged_results = classify_staged(root, protected)
            report = build_report(root, [g for g, _ in protected], args.gate, policy,
                                  args.unsigned, None, staged_results=staged_results,
                                  allow_protected=args.allow_protected_change)
            return _emit(report, args.json)

        if args.cmd == "repair":
            return repair(root / args.target if not Path(args.target).is_absolute()
                          else Path(args.target), root,
                          from_ref=args.from_ref, dry_run=args.dry_run)

        if args.cmd == "quarantine":
            return quarantine(root / args.target if not Path(args.target).is_absolute()
                              else Path(args.target), root, dry_run=args.dry_run,
                              config_path=config)

        if args.cmd == "list-protected":
            protected = load_protected(root)
            if args.json:
                print(json.dumps(
                    {"protected": [{"glob": g, "kind": k} for g, k in protected]},
                    indent=2))
            else:
                print("Protected snapshot globs:")
                for g, k in protected:
                    print(f"  {g}  kind={k}")
            return 0
    except Exception as exc:  # noqa: BLE001 - surface as env/usage error
        print(f"snapshot-guard: error: {exc}", file=sys.stderr)
        return 2
    return 2


if __name__ == "__main__":
    raise SystemExit(main())

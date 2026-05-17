"""Minimal internal gitignore-style matcher for ``codeconv mirror``.

Source of truth: ``specs/016-codeconv-init-scaffold-langpair/spec.md``
FR-043. NO third-party dependency (dependency authority; ``pathspec`` is
not a codeconv dep) — a small, well-scoped implementation of the
gitignore subset the feature needs:

- ``#`` / blank lines ignored.
- leading ``!`` → negation (re-include); last matching pattern wins
  (gitignore order semantics).
- trailing ``/`` → directory-only pattern.
- leading ``/`` → anchored to the subtree root; otherwise the pattern
  matches at any depth (by the gitignore "no slash ⇒ match basename at
  any level; slash ⇒ anchored" rule).
- ``**`` → any number of path segments; ``*`` / ``?`` → within-segment
  glob (never cross ``/``).

Paths are matched as output-root-relative POSIX strings (no leading
``/``). A directory match prunes its whole subtree (the caller stops
descending), which also realises gitignore's "a child cannot be
re-included once its parent dir is excluded".
"""

from __future__ import annotations

import re
from dataclasses import dataclass


@dataclass(frozen=True)
class _Rule:
    regex: re.Pattern[str]
    negated: bool
    dir_only: bool
    raw: str


def _translate(pattern: str) -> tuple[re.Pattern[str], bool]:
    """Translate a gitignore body (no ``!``/trailing-``/``) to a regex.

    Returns ``(compiled, anchored)``. Unanchored patterns match at any
    depth; anchored (had a leading or internal ``/``) match from root.
    """
    anchored = pattern.startswith("/") or (
        "/" in pattern.rstrip("/")
    )
    p = pattern.lstrip("/")

    # Tokenise into a regex, treating ** / * / ? specially and escaping
    # every other regex metacharacter.
    out: list[str] = []
    i = 0
    n = len(p)
    while i < n:
        c = p[i]
        if c == "*":
            if i + 1 < n and p[i + 1] == "*":
                # ``**`` — consume, and an optional following ``/``.
                i += 2
                if i < n and p[i] == "/":
                    i += 1
                    # ``**/`` ⇒ zero or more leading segments.
                    out.append("(?:.*/)?")
                else:
                    out.append(".*")
                continue
            out.append("[^/]*")
            i += 1
            continue
        if c == "?":
            out.append("[^/]")
            i += 1
            continue
        out.append(re.escape(c))
        i += 1
    body = "".join(out)
    if anchored:
        regex = re.compile(rf"^{body}(?:/.*)?$")
    else:
        # Match the final segment, or any directory segment, at any depth.
        regex = re.compile(rf"(?:^|.*/){body}(?:/.*)?$")
    return regex, anchored


class GitignoreSpec:
    """An ordered set of gitignore-style rules (last match wins)."""

    def __init__(self, rules: list[_Rule]) -> None:
        self._rules = rules

    @classmethod
    def from_lines(cls, lines: list[str]) -> "GitignoreSpec":
        rules: list[_Rule] = []
        for raw in lines:
            line = raw.rstrip("\n").rstrip()
            if not line or line.startswith("#"):
                continue
            negated = line.startswith("!")
            if negated:
                line = line[1:]
            dir_only = line.endswith("/")
            if dir_only:
                line = line[:-1]
            if not line:
                continue
            regex, _anchored = _translate(line)
            rules.append(
                _Rule(
                    regex=regex,
                    negated=negated,
                    dir_only=dir_only,
                    raw=raw,
                )
            )
        return cls(rules)

    def __bool__(self) -> bool:
        return bool(self._rules)

    def match(self, rel_posix: str, *, is_dir: bool) -> bool:
        """True iff ``rel_posix`` is excluded (last-match-wins).

        ``rel_posix`` is the output-root-relative POSIX path with no
        leading ``/``. ``dir_only`` rules only apply when ``is_dir``.
        """
        rel = rel_posix.strip("/")
        if not rel:
            return False
        excluded = False
        for r in self._rules:
            if r.dir_only and not is_dir:
                continue
            if r.regex.search(rel):
                excluded = not r.negated
        return excluded


__all__ = ["GitignoreSpec"]

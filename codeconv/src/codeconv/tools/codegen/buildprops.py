"""Manage ``<target_root>/Converted.props`` — the codegen-managed compile list.

Each accepted Dart→C# conversion contributes one ``<Compile Include="<rel>"
/>`` entry, scoping the library build to files that passed the build gate
(the ``glp_runtime_net.csproj`` sets ``EnableDefaultCompileItems=false``
and imports this file).

The rewrite is idempotent and sorted by ``Include`` path; an absent file
is a no-op (production checks the file in, tests opt in by creating it).

PURE: path + text only; no bridge, no LLM, no ``dotnet`` invocation.
"""

from __future__ import annotations

import os
import re
from pathlib import Path
from typing import Optional


_ANCHOR = (
    "<!-- codeconv-codegen append point — keep one entry per line, sorted. -->"
)
_INCLUDE = re.compile(r'<Compile\s+Include="([^"]+)"\s*/?\s*>')
_ITEMGROUP = re.compile(r"(<ItemGroup>)(.*?)(</ItemGroup>)", re.DOTALL)
_XML_COMMENT = re.compile(r"<!--.*?-->", re.DOTALL)


def converted_props_path(repo_root: Path, target_root: str) -> Path:
    """Path to the codegen-managed ``Converted.props`` under ``target_root``."""
    root = target_root.replace("\\", "/").strip("/")
    return Path(repo_root) / root / "Converted.props"


def include_relpath(target_cs_rel: str, target_root: str) -> str:
    """Strip ``target_root`` prefix so the path is relative to ``Converted.props``.

    e.g. ``out/csharp/lib/foo.cs`` with target_root ``out/csharp`` →
    ``lib/foo.cs``.
    """
    root = target_root.replace("\\", "/").strip("/")
    p = target_cs_rel.replace("\\", "/").strip("/")
    if root and p.startswith(root + "/"):
        p = p[len(root) + 1 :]
    return p


def update(
    props_path: Path,
    *,
    add: Optional[str] = None,
    remove: Optional[str] = None,
) -> bool:
    """Apply one add and/or one remove to the compile list; rewrite if changed.

    Returns ``True`` iff the file's include set changed. Idempotent:
    adding an existing entry is a no-op; removing an absent entry is a
    no-op. Absent ``Converted.props`` ⇒ no-op (returns ``False``).
    """
    if not props_path.is_file():
        return False
    text = props_path.read_text(encoding="utf-8")
    # Strip XML comments before scanning so example ``<Compile Include="..."/>``
    # snippets inside header/anchor commentary do not look like real entries.
    existing = set(_INCLUDE.findall(_XML_COMMENT.sub("", text)))
    new = set(existing)
    if add is not None:
        new.add(add)
    if remove is not None:
        new.discard(remove)
    if new == existing:
        return False
    body_lines = [f"    {_ANCHOR}"] + [
        f'    <Compile Include="{p}" />' for p in sorted(new)
    ]
    new_body = "\n".join(body_lines)

    def _swap(m: "re.Match[str]") -> str:
        return f"{m.group(1)}\n{new_body}\n  {m.group(3)}"

    new_text, n = _ITEMGROUP.subn(_swap, text, count=1)
    if n == 0:
        return False
    tmp = props_path.with_suffix(props_path.suffix + ".tmp")
    tmp.write_text(new_text, encoding="utf-8")
    os.replace(tmp, props_path)
    return True


__all__ = ["converted_props_path", "include_relpath", "update"]

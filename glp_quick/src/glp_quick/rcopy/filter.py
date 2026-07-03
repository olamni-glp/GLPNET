"""Pure client-side exclusion filter (feature 040, US6; FR-028; R9).

Excluded files are **never sent** (they do not appear in the manifest) and are reported to the user as
``filtered_out``. The filter is a pure function ``(files, filter) -> (kept, filtered_out)`` — host-free
and unit-tested. Rules: file size (min/max), filename glob, subdirectory glob, and file attributes
(hidden / read-only / mtime window).
"""

from __future__ import annotations

import posixpath
from dataclasses import dataclass, field
from fnmatch import fnmatch
from typing import List, Optional, Tuple


@dataclass(frozen=True)
class FileItem:
    """A local file selected for transfer. ``rel`` is a POSIX-style path relative to the spec root."""

    rel: str
    size: int
    mtime: int = 0
    hidden: bool = False
    readonly: bool = False


@dataclass(frozen=True)
class ExclusionFilter:
    """A per-spec exclusion filter (FR-028). Any matching rule drops the file."""

    min_size: Optional[int] = None
    max_size: Optional[int] = None
    name_globs: Tuple[str, ...] = field(default_factory=tuple)
    subdir_globs: Tuple[str, ...] = field(default_factory=tuple)
    exclude_hidden: bool = False
    exclude_readonly: bool = False
    mtime_before: Optional[int] = None   # drop files older than this (mtime < mtime_before)
    mtime_after: Optional[int] = None    # drop files newer than this (mtime > mtime_after)

    def excludes(self, f: FileItem) -> bool:
        if self.min_size is not None and f.size < self.min_size:
            return True
        if self.max_size is not None and f.size > self.max_size:
            return True
        name = posixpath.basename(f.rel)
        if any(fnmatch(name, g) for g in self.name_globs):
            return True
        subdir = posixpath.dirname(f.rel)
        if subdir and any(fnmatch(subdir, g) or fnmatch(subdir + "/", g) for g in self.subdir_globs):
            return True
        if self.exclude_hidden and f.hidden:
            return True
        if self.exclude_readonly and f.readonly:
            return True
        if self.mtime_before is not None and f.mtime < self.mtime_before:
            return True
        if self.mtime_after is not None and f.mtime > self.mtime_after:
            return True
        return False


def apply_filter(files: List[FileItem], filt: ExclusionFilter) -> "Tuple[List[FileItem], List[FileItem]]":
    """Partition ``files`` into ``(kept, filtered_out)`` (FR-028)."""
    kept, dropped = [], []
    for f in files:
        (dropped if filt.excludes(f) else kept).append(f)
    return kept, dropped

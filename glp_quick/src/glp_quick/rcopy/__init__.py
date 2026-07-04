"""``/rcopy`` file-transfer client + responder file-service backend (feature 040, US6/US8).

Host-free, unit-testable modules (added incrementally by the US6/US8 phases):

- ``filter``     — the pure exclusion filter ``(files, filter) -> (kept, filtered_out)`` (R9).
- ``wizard``     — the page-driven client wizard (US6).
- ``responder``  — the responder service (US8): offer / per-file verdict / commit / quota / path-safety.
- ``transfer``   — chunked receive: temp write → fsync → SHA-256 verify → atomic rename (FR-039).
- ``wal`` / ``catalog`` / ``provenance`` — the file-based WAL journal (source of truth) + rebuildable
  catalog & provenance projections (R7; no repo PGLite cluster — constitution VI-b).

All ``/rcopy`` control messages ride the one 036 ``GlpMessage`` seam via
:mod:`glp_quick.terminal.protocol` — ``/rcopy`` opens no second socket (FR-026).
"""

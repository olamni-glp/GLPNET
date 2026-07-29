"""WAL + message-file policy (research R4) — implemented by T016.

Append-only WAL records acceptance order and delivery state; content
placement by configurable target file size (small messages share a message
file, ~file-size messages get their own file, larger messages split across
files). Recovery = WAL replay; the store is reconciled to the WAL, never
the reverse (data-model.md "WAL"). A dense per-sender sequence is asserted
at recovery and on fetch; a gap is a named loss event (FR-010).
"""

"""msmesh hot-tier access (research R5) — implemented by T017/T022.

Stations, mailboxes, messages, delivery_position, dlq, gap_event rows in
the repo's ``.pgdb/`` PGlite cluster (``msmesh`` schema, additive migration
0012), reached exclusively through the shared ``codeconv.bridge_client``
bridge (constitution VI-b — never a parallel bridge stack). Also hosts the
retention sweep (ephemeral / time_windowed / permanent, FR-011b).
"""

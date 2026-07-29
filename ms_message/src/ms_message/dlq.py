"""Dead-letter queue (research R8) — implemented by T018.

A target unresolvable after direct + friend lookup parks in ``msmesh.dlq``
with a reason (contract guarantee 5); entries are listable and re-driveable
from the CLI (``ms-message dlq list|redrive``).
"""

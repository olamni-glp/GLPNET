"""Per-stage approval gate — append-only, superseded decisions retained
(FR-004/005, D6). A recorded ``approve`` short-circuits the gate on resume
(no re-ask — SC-004). Implemented in US2 (T025/T026).
"""

from __future__ import annotations

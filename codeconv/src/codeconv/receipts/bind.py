"""Contract-version binding (FR-024, research D3).

The receipt contract has exactly ONE authoritative definition, owned by
**buildkit** — the repository that distributes to every host. glpnet binds to it
**by version** and never copies the runtime artifact (copying is the divergence
FR-024 exists to stop).

``resolve_contract`` returns ``(contract_version, schema)``. It prefers the schema
shipped inside the active installed buildkit version; until that companion change
lands it falls back to a clearly-marked **pre-release DRAFT** derived from
``specs/078-verification-receipts/contracts/receipt-schema.design.md``. The draft
is a bootstrap for the MVP increment, NOT an owned competing authority — task T037
re-pins to the released buildkit version.

Every receipt records the resolved ``contract_version`` so an emitter/consumer
version skew is itself visible and reconcilable.
"""

from __future__ import annotations

from typing import Any

# Pre-release draft identity. A MAJOR mismatch between a consumer's pinned
# contract and a receipt's recorded contract is treated as UNREAD by the
# consumer (unrecognised contract), never silently accepted.
DRAFT_VERSION = "buildkit-draft-0"

# Declared bounds (FR-005). Authoritative values are contract constants owned by
# buildkit; these draft values let the MVP exercise the bounding behaviour.
MAX_ENUM = 100          # max entries in an examined/skipped enumeration
MAX_FIELD_BYTES = 4096  # byte backstop on any single string field

# Structural draft schema (documentation form). Validation is performed in pure
# Python by ``receipt.validate`` so the MVP carries no hard jsonschema dependency;
# this dict records the intended shape for the buildkit companion change.
DRAFT_SCHEMA: dict[str, Any] = {
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "title": "verification-receipt (DRAFT)",
    "type": "object",
    "required": [
        "schema_version", "contract_version", "check_id", "area",
        "resolved_target", "outcome", "examined_count", "total_count",
        "skipped", "skipped_total", "truncated", "ran_at", "verdict_pointer",
    ],
    "properties": {
        "outcome": {"enum": ["PASS", "EMPTY", "UNREAD", "UNSEARCHABLE", "FAIL"]},
    },
}


def resolve_contract() -> tuple[str, dict[str, Any]]:
    """Resolve ``(contract_version, schema)`` from the pinned buildkit version.

    Currently returns the pre-release draft (the buildkit companion change is not
    yet shipped — research D3). When it lands, this reads the schema from the
    active installed buildkit version instead; the call site does not change.
    """
    # TODO(T037): read the schema from the active installed buildkit version and
    # return its real version string here, replacing the draft fallback.
    return DRAFT_VERSION, DRAFT_SCHEMA


def major(version: str) -> str:
    """The MAJOR component of a contract version, for skew detection."""
    core = version.split("-")[-1]
    return core.split(".")[0] if core and core[0].isdigit() else version

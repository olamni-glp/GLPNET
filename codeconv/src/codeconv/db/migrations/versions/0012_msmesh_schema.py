"""Feature 063 US2 — msmesh schema (durable first-hop mesh messaging).

Per ``specs/063-wave-5-consolidated-captured-triad/data-model.md``. Creates
the ``msmesh`` schema and its six domain tables:

- ``msmesh.station``            (ground-station registry incl. friend cache)
- ``msmesh.mailbox``            (topic/mailbox + retention class)
- ``msmesh.message``            (per-message metadata; content lives in WAL files)
- ``msmesh.delivery_position``  (per-peer high-water mark + sparse seen-set — the exactly-once floor, R7)
- ``msmesh.dlq``                (dead letters with reasons, re-driveable, R8)
- ``msmesh.gap_event``          (named sequence-loss events, FR-010)

Schema isolation: a NEW schema ``msmesh``; touches neither ``public`` nor
``dbos`` nor ``codeconv`` nor ``marathon``. Message CONTENT is on disk (WAL +
message files per research R4); the store is reconciled to the WAL, never the
reverse — these tables carry metadata/sequence/delivery state only.

NOTE (tasks.md T005 deviation, recorded): the task text named this migration
``0011_msmesh_schema`` assuming head ``0010``, but feature 035
(``0011_enrich_provenance``) advanced the head to ``0011`` before this wave.
The msmesh migration is therefore ``0012`` chaining off ``0011`` — the
single-linear-head discipline (Constitution VI-a) is what the task binds, not
the stale number. Asserted by ``test_migration_0012_single_head.py``.

Additive + idempotent (``IF NOT EXISTS``), single linear head: ``0012``
chains directly off ``0011``, so ``heads`` reports exactly ``0012`` after add.

Revision ID: 0012
Revises: 0011
Create Date: 2026-07-29
"""
from __future__ import annotations

from typing import Sequence, Union

from alembic import op


revision: str = "0012"
down_revision: Union[str, None] = "0011"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.execute("CREATE SCHEMA IF NOT EXISTS msmesh;")

    # § Station — identity registry; address NULL = known-by-id only.
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS msmesh.station (
            station_id  text PRIMARY KEY,
            address     text,
            source      text NOT NULL,
            learned_at  timestamptz NOT NULL DEFAULT NOW(),
            CONSTRAINT station_source_ck CHECK (source IN
                ('config','friend-lookup','inbound'))
        );
        """
    )

    # § Mailbox — topic + retention class (contract guarantee 6).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS msmesh.mailbox (
            mailbox_id          text PRIMARY KEY,
            owner_station       text NOT NULL REFERENCES msmesh.station(station_id),
            retention_class     text NOT NULL,
            retention_window_s  integer,
            CONSTRAINT mailbox_retention_ck CHECK (retention_class IN
                ('ephemeral','time_windowed','permanent')),
            CONSTRAINT mailbox_window_ck CHECK (
                (retention_class = 'time_windowed') = (retention_window_s IS NOT NULL))
        );
        """
    )

    # § Message — metadata only; content_ref points into the WAL file set
    # (shared/own/split placement per R4). Identity = (sender, seq) — the
    # dedup key (R7). target_station is deliberately NOT an FK: an
    # unresolvable target is a legitimate row on its way to the DLQ.
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS msmesh.message (
            sender_station  text NOT NULL,
            sender_seq      bigint NOT NULL,
            mailbox_id      text NOT NULL REFERENCES msmesh.mailbox(mailbox_id),
            target_station  text NOT NULL,
            size_bytes      bigint NOT NULL,
            content_ref     text NOT NULL,
            accepted_at     timestamptz NOT NULL DEFAULT NOW(),
            state           text NOT NULL DEFAULT 'journalled',
            PRIMARY KEY (sender_station, sender_seq),
            CONSTRAINT message_state_ck CHECK (state IN
                ('journalled','signalled','fetched','expired','dead'))
        );
        """
    )
    op.execute(
        "CREATE INDEX IF NOT EXISTS message_mailbox_state_idx "
        "ON msmesh.message (mailbox_id, state);"
    )

    # § Delivery position — the exactly-once floor; survives restart (R7).
    # seen_sparse covers out-of-order fetches beyond the dense mark.
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS msmesh.delivery_position (
            peer_station    text NOT NULL,
            direction       text NOT NULL,
            high_water_seq  bigint NOT NULL DEFAULT 0,
            seen_sparse     jsonb NOT NULL DEFAULT '[]'::jsonb,
            updated_at      timestamptz NOT NULL DEFAULT NOW(),
            PRIMARY KEY (peer_station, direction),
            CONSTRAINT delivery_direction_ck CHECK (direction IN
                ('inbound','outbound'))
        );
        """
    )

    # § DLQ — park-with-reason; re-drive stamps redriven_at (guarantee 5).
    # bigserial PK keeps the history append-only (a message may park more
    # than once across re-drives); the FK binds each parking to its message.
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS msmesh.dlq (
            id              bigserial PRIMARY KEY,
            sender_station  text NOT NULL,
            sender_seq      bigint NOT NULL,
            reason          text NOT NULL,
            parked_at       timestamptz NOT NULL DEFAULT NOW(),
            redriven_at     timestamptz,
            FOREIGN KEY (sender_station, sender_seq)
                REFERENCES msmesh.message (sender_station, sender_seq)
        );
        """
    )
    op.execute(
        """
        CREATE INDEX IF NOT EXISTS dlq_parked_idx
            ON msmesh.dlq (parked_at)
            WHERE redriven_at IS NULL;
        """
    )

    # § Gap event — a named loss, never a silent skip (FR-010, guarantee 3).
    op.execute(
        """
        CREATE TABLE IF NOT EXISTS msmesh.gap_event (
            id            bigserial PRIMARY KEY,
            peer_station  text NOT NULL,
            expected_seq  bigint NOT NULL,
            got_seq       bigint NOT NULL,
            detected_at   timestamptz NOT NULL DEFAULT NOW(),
            resolution    text NOT NULL DEFAULT 'unresolved',
            CONSTRAINT gap_resolution_ck CHECK (resolution IN
                ('refetched','unresolved'))
        );
        """
    )


def downgrade() -> None:
    # Schema isolation makes downgrade a single drop.
    op.execute("DROP SCHEMA IF EXISTS msmesh CASCADE;")

# Data Model — 063 wave-5 consolidated captured triad

US2 owns the data model (US1 reuses spec-025's entities unchanged; US3 is
documentation + records). Hot tier = `msmesh` schema (PGlite, additive
migration 0011); aged tier = DuckLake parquet mirroring the same shapes.

## msmesh.station

| field | type | notes |
|---|---|---|
| station_id | text PK | ground station identity (the brief's "station ID") |
| address | text nullable | host/URL/IP when known; NULL = known-by-id only |
| source | text | how learned: config, friend-lookup, inbound |
| learned_at | timestamptz | |

## msmesh.mailbox

| field | type | notes |
|---|---|---|
| mailbox_id | text PK | topic/mailbox name |
| owner_station | text FK station | the holder |
| retention_class | text | ephemeral \| time_windowed \| permanent |
| retention_window_s | int nullable | for time_windowed |

## msmesh.message

| field | type | notes |
|---|---|---|
| sender_station | text | identity part 1 |
| sender_seq | bigint | identity part 2 — dense per-sender sequence |
| mailbox_id | text FK | |
| target_station | text | first-hop target |
| size_bytes | bigint | |
| content_ref | text | WAL file ref: file id + offset + length (or own-file id) |
| accepted_at | timestamptz | |
| state | text | journalled \| signalled \| fetched \| expired \| dead |
| PK | (sender_station, sender_seq) | the dedup identity (R7) |

## msmesh.delivery_position

| field | type | notes |
|---|---|---|
| peer_station | text | the counterparty |
| direction | text | inbound \| outbound |
| high_water_seq | bigint | dense high-water mark |
| seen_sparse | jsonb | out-of-order seen-set beyond the mark (normally empty) |
| updated_at | timestamptz | survives restart — the exactly-once floor |
| PK | (peer_station, direction) | |

## msmesh.dlq

| field | type | notes |
|---|---|---|
| sender_station, sender_seq | | FK message identity |
| reason | text | e.g. unresolvable-target-after-friend-lookup |
| parked_at | timestamptz | |
| redriven_at | timestamptz nullable | CLI re-drive stamp |

## msmesh.gap_event

| field | type | notes |
|---|---|---|
| peer_station | text | whose sequence gapped |
| expected_seq / got_seq | bigint | the named loss (FR-010) |
| detected_at | timestamptz | |
| resolution | text | refetched \| unresolved |

## WAL (on disk, not in the store)

- `wal-<n>.log`: append-only acceptance/delivery records (message identity,
  content placement, state transitions). Replayed on restart; the store is
  reconciled to the WAL, never the reverse.
- `msg-<n>.dat`: shared message files (small messages appended); `msg-own-<id>.dat`
  own-file messages; `msg-part-<id>-<k>.dat` split parts (policy per R4).

## State transitions (message)

journalled → signalled → fetched (terminal)
journalled/signalled → dead (unresolvable → DLQ) | expired (retention)
fetched is recorded via delivery_position advance; gap on fetch ⇒ gap_event.

## Aged tier (DuckLake)

Messages + gap_events older than the aging window migrate to parquet with the
same logical shapes; catch-up queries UNION hot + lake (R6 seam; loud
PGlite-only degradation).

# Contract — Snapshot Store (061)

## API (ISnapshotStore)

- `Write(SnapshotBlob, meta) → seq` — assigns the next monotonic seq for the
  engine identity; durable-then-visible: the snapshot is listable only after
  blob AND manifest are both durably written (torn-write safe, FR-013).
- `Latest() → (seq, blob)?` — highest COMPLETE seq; never a torn write.
- `BySeq(seq) → (blob)?` — exact complete snapshot or null.
- `List() → [ {seq, created_utc, size, format_version} ]` — complete only.

## Backends

- **Primary — PGLite**: additive tables on the repo's single bridge-guarded
  cluster (`<repo>/.pgdb/`), reached the way `csharp/glp_crdtmsg/store/`
  reaches it (Constitution VI-b: no second cluster, no parallel bridge stack).
- **Fallback — File**: gitignored directory; blob written to a temp name,
  fsync, atomic rename; manifest JSON updated last. Engaging the fallback is
  reported loudly to the requester and the log (US2/AS-4).
- Primary and fallback share the seq namespace: on write, seq = max(both)+1;
  restore reads Latest() = max complete across both, preferring primary on tie.

## Blob layout (format_version 1)

Versioned header `{magic 'GSNP', format_version varint, engine_identity str,
created_utc i64, seq varint}` then sections, each `{section_tag u8, length
varint, bytes}` in ByteIo conventions (LEB128 varints, LE i64, varint+UTF-8
strings — the 029/038 house style):

| tag | section | notes |
|---|---|---|
| 0x01 | heap cells | verbatim addresses (FR-011/DEF-E2) |
| 0x02 | goal queue | empty at quiescence; recorded for integrity |
| 0x03 | suspended goals + per-goal tables | |
| 0x04 | next `_goalId` | collision-free resumption (DEF-D1) |
| 0x05 | loaded IL units | |
| 0x06 | timers | remaining-duration entries (FR-015) |
| 0x07 | InfrastructureGoalIds | DEF-D1 |
| 0x08 | GlpChannels | DEF-D1 |
| 0x09 | link definitions | LinkId, role, endpoint params, cursor positions |

Unknown section tag on restore → loud fail (corrupt snapshot ⇒ taxonomy
`corrupt_latest_snapshot`, supervisor falls back to previous seq once, then
classifies unrecoverable). Trailing bytes → loud fail.

## Integrity

- `decode(encode(state)) == state` round-trip test on the snapshot corpus.
- A crash at ANY point during Write leaves Latest() at the previous seq.
- Restore MUST verify section completeness before the engine leaves
  `restoring` (FR-030).

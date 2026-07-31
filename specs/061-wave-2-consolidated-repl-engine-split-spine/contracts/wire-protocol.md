# Contract — Client↔Engine Wire Protocol (061)

Transport: `TcpTransport` loopback. Framing: `FrameCodec` TLV (big-endian
lengths, per the shipped 036/025 conventions). This contract adds new payload
types; it changes NO existing FrameCodec payload type.

## Frames

Request: `{ payload_type: REQUEST, request_id: u64, kind: u8, body: bytes }`
kinds: `0x01 LOAD_SOURCE (UTF-8)` · `0x02 RUN_GOAL (UTF-8)` · `0x03 SNAPSHOT` ·
`0x04 STATUS` · `0x05 SHUTDOWN` · `0x06 PING` (bodies empty unless noted).

Response: `{ payload_type: RESPONSE, request_id: u64, kind: u8, body: bytes }`
kinds: `0x81 RESULT` (body = 038 ResultEnvelope bytes — ground-only subset,
engine-pre-rendered bindings, length-prefixed UTF-8 output blob) ·
`0x82 ACK` (body = status string + optional seq varint) · `0x83 DEFERRED` ·
`0x84 PROTOCOL_ERROR` (body = UTF-8 reason) · `0x85 ENGINE_BUSY`.

## Rules

1. Exactly one client connection; a second accept is closed after a loud
   `PROTOCOL_ERROR`.
2. Every request gets exactly one terminal response; requests are answered in
   order (single client, single engine — no pipelining semantics promised).
3. Unknown request kind / malformed body → `PROTOCOL_ERROR`; engine keeps
   serving (FR-006). Trailing bytes in a frame → loud fail (038 convention).
4. During restore the engine answers only STATUS/PING (`ENGINE_BUSY` for the
   rest) — never serves from half-restored state.
5. SNAPSHOT on a busy engine → `DEFERRED`, then executes at next quiescence;
   the eventual completion is observable via STATUS (seq advances).
6. SHUTDOWN: engine takes a final snapshot (graceful trigger, FR-014), ACKs
   with the final seq, exits 0. When the engine is NOT quiescent (or link
   rewires are pending), the final snapshot is SKIPPED LOUDLY — the ACK body
   says so (`final_snapshot=skipped(...)`, plus `parked_snapshot=unfulfilled`
   if a DEFERRED snapshot was parked) — because an inconsistent snapshot is
   never permitted (FR-014). A taken final snapshot subsumes a parked one.
7. PING answers `ACK` within the supervisor's timeout budget whenever the
   engine event loop is alive (also during restore — rule 4).
8. Crash boundary: a request with no terminal response ⇒ transport failure at
   the client; committed = last complete snapshot + envelopes already handed
   to the transport; no replay (at-most-once — spec FR-032).

## Verification obligation (FR-040)

The full protocol above — including DEFERRED, ENGINE_BUSY, restore-window and
crash transitions — is modelled in SPIN (deadlock-freedom, no unspecified
receptions, `request_eventually_answered` under fairness); the crash/restore
committed-stream consistency in TLA+; ping/restart timing in UPPAAL. Verdicts
recorded per model under `docs/research/repl-engine-separation/models/`.

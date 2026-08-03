# Contract — distributed unification (dist_unify)

**Parity target**: the C# link's remote-binding protocol, byte-for-byte on the wire and rule-for-rule in semantics. FCP Savannah (`unify.c`) is the interpretive tie-breaker. Zero new GLP language surface (§1.14).

## Message kinds (per-link, over the reliability layer)

| Kind | Payload | Direction | Semantics |
|---|---|---|---|
| VAR_EXPORT | RemoteVarRef | owner→peer | registers a variable half remotely; a writer half exports at most once (SRSW across the link) |
| DIST_BIND | {seq, var_id, term} | writer-side→reader-side | applies the binding under writer-MGU rules |
| DIST_SUSPEND | {var_id, goal_ref} | reader-side→writer-side | records a remote suspension to reactivate on bind |
| DIST_FAULT | {var_id, reason} | either | protocol violation surfaced to the fault lattice |

## Rules (normative)

1. Only writers bind; a DIST_BIND arriving for a writer half ⇒ DIST_FAULT(writer_writer), loud, link → faulted.
2. Per-link FIFO ordering is provided by the existing sequencing/dedup layer; dist_unify MUST NOT add its own retransmission.
3. Reader suspension on a remote writer produces DIST_SUSPEND once; reactivation is local on DIST_BIND receipt.
4. Term payloads reuse the existing FrameCodec term encoding (byte parity already proven across runtimes).
5. A malformed term or unknown var_id ⇒ DIST_FAULT, never a silent drop.

## Parity matrix (acceptance)

Every scenario runs Gleam↔Gleam and Gleam↔C#, both directions, and must equal the single-instance result: shared non-ground var bound remotely; chained bindings (A binds to structure containing B's var); remote suspension then bind; writer-writer fault case; malformed payload fault case. Wired into test/parity/cross_runtime with committed .out results.

# Data Model — 064 post-wave gap closure

## Distributed binding (US1)

- **RemoteVarRef**: {link_id, origin_instance, var_id, mode: writer|reader} — identity of a variable half exported across a link. Uniqueness: (link_id, var_id). A writer half is exported at most once (SRSW preserved across the link).
- **DistBindMsg**: {seq, var_id, term_payload} — a binding travelling origin→remote. Ordering: per-link FIFO via the reliability layer (existing sequencing/dedup). Validation: receiver applies writer-MGU rules; writer-to-writer ⇒ loud protocol fault, never silent.
- **RemoteSuspension**: {var_id, goal_ref} — a local goal suspended on a remote writer; reactivated on the matching DistBindMsg.

## Quiescence (US1)

- **QuiescenceCensus**: {instance_id, running, suspended, inflight_out, inflight_acked} — per-instance snapshot exchanged over the link.
- **QuiescenceVerdict**: active | quiescent | faulted. Transition rules: quiescent iff all instances report running=0 and inflight_out==inflight_acked for every link; any fault-lattice event ⇒ faulted (terminal until re-arm).

## Client session (US2)

- **ClientSession** (C#): {session_id, transport_conn, in_channel, out_channel, state: active|draining|closed}. Lifecycle: accept → register with control program (A31 merge tree) → serve → close (discard pending replies, never wedge the merge loop).
- **RoutedReply**: {session_id, goal_id, result_envelope} — reply routing key; a reply reaches exactly its issuing session.

## IL request (US3)

- **IlRequest**: split-protocol frames LOAD_IL {envelope: CompiledIlEnvelope} and RUN_GOAL_IL {goal_ref, envelope?}. Refusal taxonomy (existing 062 hardening set): malformed | il_version_mismatch | digest_mismatch | mid_transfer_truncation → typed error response, engine keeps serving.

## FE/BE split + embed (US4)

- **BeProcess**: engine+scheduler behind the split protocol on a configured port; single-tenant per FE session initially (multi-client BE rides US2 semantics later).
- **FeSession**: thin REPL loop state {conn, pending_goal, display}.
- **EmbedHandle**: {engine_ref, loaded_project, result_sink} — the glp_embed surface handed to a host program.

## State transitions of record

- Link: established → (serving ⇄ draining) → closed | faulted (existing lattice; dist_unify and quiescence hang off `serving`).
- 059/050 task rows: open → discharged(evidence) | deferred(recorded) — tracked in tasks.md and the 059/050 files at close-out.

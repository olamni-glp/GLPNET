<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract: host-owned message log + boot replay (064 / FR-004..006)

## Append side

1. `LinkPump` gains ONE additive optional hook:
   `public Action<LinkId, Term>? OnDelivered;` (name final at implementation) —
   invoked in `TryApplyNext`'s data case immediately BEFORE the heap bind that
   extends the program's `In` stream (today `LinkPump.cs:124-132`). Null hook ⇒
   byte-identical behavior (the default). Runner-thread, once per delivered
   term, post-reassembly/ordering/dedup.
2. The REPL shim registers an appender when (and only when) an enabled
   registration exists: it encodes the delivered ground term as a crdtmsg op
   and calls `IOpWal.Append` — durably, before returning, so the program never
   acts on a message that could be lost (FR-004; US2 scenario 2).
3. WAL composition: `PgliteOpWal` primary (repo's single `.pgdb/` cluster via
   the existing `COLAB_PG_CONN` convention — Constitution VI-b), `OpWal` file
   fallback under `glpservice/wal/`, primary-then-loud-degrade (the 061
   composer discipline). Append idempotence comes from the op's dot key.
4. **Both-backends-fail policy (analyze remediation U1)**: if primary AND
   fallback appends fail, the host prints the named diagnostic
   `resume: WAL append failed on both backends: <cause>` and DELIVERS the
   message anyway — availability over durability for the chat MVP. FR-004's
   guarantee is explicitly scoped to "whenever at least one backend is
   writable"; a both-backends outage is loud, never silent, and every
   subsequent delivery repeats the diagnostic until a backend recovers.

## Replay side

1. Order: WAL `Ops` ascending (receipt order) — FR-005.
2. Timing: replay runs AFTER the registered program loads and BEFORE the goal
   arms the listener (history precedes live traffic — US2 scenario 3).
3. Mechanism: the replayed ops are presented to the service through the same
   inbound delivery shape as live traffic (the program cannot distinguish
   replay from live). Replay READS the WAL only — the delivery observer is not
   registered until replay completes, so replayed items are never re-appended
   (idempotence across restarts — US2 scenario 4, SC-002's byte-identical
   second restart).
4. A replay failure (unreadable WAL, undecodable op) is a named diagnostic
   (FR-009); the host then continues to arm the listener with whatever prefix
   replayed — it never silently drops the registration.

## Zero-language-surface guarantee (FR-006)

Everything above is host code (shim + one additive glp_link hook + existing
crdtmsg store). No new GLP predicate, guard, kernel, directive, or type; the
engine's accepted language surface is untouched (SC-004 verified by the
existing suites passing unmodified).

## Drill (SC-002)

Send ≥100 messages → restart → assert: history complete, receipt-ordered, zero
duplicates; restart again with no traffic → history byte-identical.

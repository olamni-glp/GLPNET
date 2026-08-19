<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Phase 1 Data Model — 079 (heap cell / cross-pointer entities)

Descriptive only — reads existing `heap_fcp.dart` structures; no new persisted schema.

## Heap cell (existing)
- **WrtTag cell (writer)**: `content` ∈ { `Pointer(targetAddr)` (unbound, direct→reader), `WriterContent{readerAddr, suspensions…}` (unbound with suspensions), bound-value }.
- **RoTag cell (reader)**: `content` = `Pointer(targetAddr→writer)` (bidirectional back-link).
- **Bidirectional cross-pointer (authoritative)**: writer→reader and reader→writer. `readerForWriter(writerAddr)` resolves it — but ONLY for the two unbound cases (returns `null` for a **bound** writer, Case 3).

## The gap (R-1)
- **Bound-writer reader address**: currently recoverable ONLY via the N/N+1 convention (`writerAddr+1`) — there is no cross-pointer accessor once the writer is bound. This is the entity the fix must introduce (R-1a) or explicitly report as needing a heap-format change (R-1b).

## Field (R-3)
- **`GlobalSendSpawn.readerAddr`** (`mad_helpers.dart`): typed `int`; **actually holds an onBind writer key**, not "the reader to watch". Rename target: a name that states it is a writer key (e.g. `onBindWriterAddr` / `writerKey` — final name at implement, updating all refs).

## Validation rules
- FR-004: every `pairedReaderAddr` call site classified bound-vs-unbound before change.
- SC-001: bound path resolves via cross-pointer (post R-1a) or the fallback fires only where provably safe; a genuinely-absent cross-pointer fails loud.
- SC-002: multiagent + REPL suites unchanged vs baseline.

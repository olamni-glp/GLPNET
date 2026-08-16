# Contract — pairedReaderAddr reader-resolution (079)

## Current (pre-079)
`pairedReaderAddr(writerAddr) -> int`:
1. `r = readerForWriter(writerAddr)`; if non-null return r  (unbound: Pointer or WriterContent.readerAddr)
2. else return `writerAddr + 1`  (bound: N/N+1 convention — the residual)

## Target (post-079, R-1a)
`pairedReaderAddr(writerAddr) -> int`:
1. resolve reader via the bidirectional cross-pointer for BOTH unbound AND bound writers
   (bound case gains a cross-pointer accessor equivalent to WriterContent.readerAddr)
2. if genuinely unresolvable -> raise a loud, diagnosable error naming writerAddr
   (NEVER return writerAddr+1)

Behaviour-preserving invariant: for every input the old and new resolve to the SAME reader address
whenever the cross-pointer is intact (all 11 runner.dart call sites + multiagent suite unchanged).

## Escalation (R-1b)
If step 1's bound-case accessor cannot be added without changing heap cell format / allocation
invariants / `_ClauseVar` / `_TentativeStruct`: STOP, do not implement, report to Gabi + revise FR-002.

## MVP alternative (if R-1a deferred)
Keep the fallback but ASSERT/log when it fires on an UNBOUND writer (a real cross-pointer gap) —
closes the silent-guess hazard without the heap-format work; bound-writer +1 stays until R-1a lands.

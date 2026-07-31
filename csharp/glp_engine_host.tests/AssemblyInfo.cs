// Parity tests redirect the process-global Console (both the reference REPL and
// the split client run in-process) — the suite must not interleave tests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

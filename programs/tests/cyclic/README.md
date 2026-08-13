# Cyclic-term compiler regression programs (feature 077)

These `.glp` programs exercise the guarded term-traversal utilities
(`out/csharp/lib/compiler/term_traversal.cs`). Each `cyclic_*.glp` induces a
cyclic substitution/term in the C# compiler back-end that, BEFORE feature 077,
overflowed the stack (F-069-1 / BC-2, an uncatchable `StackOverflowException`);
AFTER 077 each must compile to a catchable `CompileError` diagnostic (FR-004),
never crashing the process.

The acyclic programs (`deep_acyclic.glp`, `dag_shared.glp`) MUST still compile
cleanly (FR-006 / SC-006) — the guard distinguishes a genuine cycle from a
merely-deep or DAG-shared term.

Driven by `test/run_all_tests.sh` Section T (feature 077).

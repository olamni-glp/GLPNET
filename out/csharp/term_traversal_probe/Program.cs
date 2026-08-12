// term_traversal_probe — feature 077 structural-guard probe (SC-001/SC-003/SC-006).
//
// Exercises GlpRuntime.Compiler.StructuralGuard the same way the codegen/analyzer/
// linker walkers do (Enter at descent, Exit on return), on inputs a .glp program
// cannot express: a programmatically-constructed cyclic AST node. Asserts:
//   * cyclic Term (self-ref struct, self-ref cons tail)  -> catchable CompileError, no overflow
//   * deep-acyclic Term (depth 6000)                     -> traverses OK (not falsely rejected)
//   * DAG-shared subterm (same node at two positions)    -> traverses OK (not a false cycle)
// Prints "PROBE OK" and exits 0 on success; non-zero otherwise.

using GlpRuntime.Compiler;

int fails = 0;
void Ok(string m)  => Console.WriteLine($"  ok: {m}");
void Bad(string m) { Console.WriteLine($"  PROBE FAIL: {m}"); fails++; }

// Local DFS mirroring the structural walkers' Enter/try/finally-Exit discipline.
void Walk(Term t, StructuralGuard g)
{
    g.Enter(t);
    try
    {
        switch (t)
        {
            case StructTerm s:
                foreach (var a in s.Args) Walk(a, g);
                break;
            case ListTerm l:
                if (l.Head is not null) Walk(l.Head, g);
                if (l.Tail is not null) Walk(l.Tail, g);
                break;
        }
    }
    finally { g.Exit(t); }
}

// 1. Self-referential StructTerm  s.Args[0] == s  -> CompileError, bounded (SC-001).
{
    var cycArgs = new List<Term>();
    var cyc     = new StructTerm("s", cycArgs, 1, 1);
    cycArgs.Add(cyc);                       // close the cycle after construction
    try { Walk(cyc, new StructuralGuard("codegen")); Bad("self-ref StructTerm did NOT raise"); }
    catch (CompileError e) when (e.Message.Contains("Cyclic", StringComparison.Ordinal))
        { Ok("self-ref StructTerm -> CompileError"); }
    catch (Exception e) { Bad($"self-ref StructTerm raised {e.GetType().Name}, not CompileError"); }
}

// 2. Self-referential cons cell  '.'(a, <self>)  -> CompileError (list-tail cycle).
{
    var consArgs = new List<Term>();
    var cons     = new StructTerm(".", consArgs, 0, 0);
    consArgs.Add(new ConstTerm("a", 0, 0));
    consArgs.Add(cons);                     // tail refers back to the cons node
    try { Walk(cons, new StructuralGuard("codegen")); Bad("self-ref cons did NOT raise"); }
    catch (CompileError) { Ok("self-ref cons -> CompileError"); }
    catch (Exception e)  { Bad($"self-ref cons raised {e.GetType().Name}, not CompileError"); }
}

// 3. Deep-acyclic term (depth 6000) -> traverses OK, not falsely rejected (SC-006/FR-006).
{
    Term deep = new ConstTerm("base", 0, 0);
    for (int i = 0; i < 6000; i++) deep = new StructTerm("n", new List<Term> { deep }, 0, 0);
    try { Walk(deep, new StructuralGuard("codegen")); Ok("deep-acyclic (depth 6000) traverses OK"); }
    catch (Exception e) { Bad($"deep-acyclic falsely rejected: {e.Message}"); }
}

// 4. DAG: the SAME subterm object at two positions -> traverses OK, not a false cycle (SC-006).
{
    var shared = new StructTerm("sub", new List<Term> { new ConstTerm("x", 0, 0) }, 0, 0);
    var dag    = new StructTerm("p", new List<Term> { shared, shared }, 0, 0);
    try { Walk(dag, new StructuralGuard("codegen")); Ok("DAG-shared subterm traverses OK"); }
    catch (Exception e) { Bad($"DAG-shared falsely rejected: {e.Message}"); }
}

// 5. The REAL shared substitution/renaming/unify/resolve walkers (not just a local mirror)
//    must convert a constructed structural cycle into a catchable CompileError. This is the
//    positive direction of the 077 codexreview fix: fuel-only was insufficient (a cyclic term
//    recurses in DEPTH and blows the .NET stack long before an 8M fuel bound); identity via
//    StructuralGuard.Scope catches the cycle at its period.
StructTerm SelfRefStruct()
{
    var args = new List<Term>();
    var s = new StructTerm("s", args, 0, 0);
    args.Add(s);                            // s.Args[0] == s  (closed after construction)
    return s;
}

void ExpectCyclic(string name, Action act)
{
    try { act(); Bad($"{name} did NOT raise on a cyclic term"); }
    catch (CompileError e) when (e.Message.Contains("Cyclic", StringComparison.Ordinal))
        { Ok($"{name} -> CompileError on cyclic term"); }
    catch (Exception e) { Bad($"{name} raised {e.GetType().Name}, not CompileError"); }
}

ExpectCyclic("CollectVarNames",    () => TermTraversal.CollectVarNames(SelfRefStruct(), new HashSet<string>()));
ExpectCyclic("ApplyRenaming",      () => TermTraversal.ApplyRenaming(SelfRefStruct(), new Dictionary<string, string>()));
ExpectCyclic("ApplySubstitution",  () => TermTraversal.ApplySubstitution(SelfRefStruct(), new Dictionary<string, Term>()));
ExpectCyclic("UnifyTerms",         () => TermTraversal.UnifyTerms(SelfRefStruct(), SelfRefStruct(),
                                            new Dictionary<string, Term>(), new HashSet<string>(), _ => false));
ExpectCyclic("ResolveSubstitution", () => TermTraversal.ResolveSubstitution(
                                            new Dictionary<string, Term> { ["X"] = SelfRefStruct() }));

// 6. The REAL walkers must NOT falsely reject a deep-acyclic term (depth 6000) …
{
    Term deep = new ConstTerm("base", 0, 0);
    for (int i = 0; i < 6000; i++) deep = new StructTerm("n", new List<Term> { deep }, 0, 0);
    try { TermTraversal.CollectVarNames(deep, new HashSet<string>()); Ok("CollectVarNames deep-acyclic (6000) OK"); }
    catch (Exception e) { Bad($"CollectVarNames falsely rejected deep-acyclic: {e.Message}"); }
}

// 7. … nor a DAG-shared subterm (same node object at two positions).
{
    var shared = new StructTerm("sub", new List<Term> { new ConstTerm("x", 0, 0) }, 0, 0);
    var dag    = new StructTerm("p", new List<Term> { shared, shared }, 0, 0);
    try { TermTraversal.CollectVarNames(dag, new HashSet<string>()); Ok("CollectVarNames DAG-shared OK"); }
    catch (Exception e) { Bad($"CollectVarNames falsely rejected DAG: {e.Message}"); }
}

Console.WriteLine(fails == 0 ? "PROBE OK" : $"PROBE FAILED ({fails} failure(s))");
return fails == 0 ? 0 : 1;

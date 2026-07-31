// TermRendering — engine-side binding pre-rendering for the R6 envelope subset
// (T010). The client is thin (R7) and holds no heap, so bindings cross the wire
// as DISPLAY STRINGS rendered here, engine-side, where the heap lives.
//
// This is a faithful copy of the single-process REPL's private renderer
// (out/csharp/bin/glp_repl.cs FormatTerm/FormatDartDouble). It cannot be
// referenced from there (private members of an auto-generated, sha-stamped
// codeconv output that must not be edited), so the split host carries its own
// copy; the SC-001 parity corpus (ParityCorpusTests, T014) is the drift guard
// that keeps the two renderers observably identical.

using GlpRuntime.Engine;
using GlpRuntime.Runtime;

namespace GlpRuntime.EngineHost;

public static class TermRendering
{
    /// <summary>
    /// Render a double the way Dart's <c>double.toString()</c> does (whole-valued
    /// doubles keep a trailing ".0"; invariant culture). Mirror of the REPL's
    /// FormatDartDouble.
    /// </summary>
    public static string FormatDartDouble(double d)
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsPositiveInfinity(d)) return "Infinity";
        if (double.IsNegativeInfinity(d)) return "-Infinity";
        var s = d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (s.IndexOf('.') < 0 && s.IndexOf('E') < 0 && s.IndexOf('e') < 0)
            s += ".0";
        return s;
    }

    /// <summary>
    /// Recursive term printer with cycle detection — mirror of the REPL's
    /// FormatTerm. ConstTerm (nil → []); list-shaped StructTerm (functor '.',
    /// arity 2 → [h, …]); other StructTerm (functor(arg, …)); VarRef args
    /// dereferenced via the engine heap, unbound labelled Xn?/Xn.
    /// </summary>
    public static string FormatTerm(Term? term, GlpEngine? engine = null, HashSet<int>? path = null)
    {
        if (term is null) return "[]";

        path ??= new HashSet<int>();

        if (term is ConstTerm c)
        {
            if (c.Value is null || (c.Value is string str && str == "nil"))
                return "[]";
            if (c.Value is double dval)
                return FormatDartDouble(dval);
            return c.Value.ToString() ?? string.Empty;
        }

        if (term is StructTerm listSt && listSt.Functor == "." && listSt.Args.Count == 2)
        {
            var elements = new List<string>();
            Term? current = term;

            while (true)
            {
                if (current is not StructTerm cs || cs.Functor != ".")
                    break;

                var head = cs.Args[0];
                var tail = cs.Args[1];

                string headStr;
                if (head is VarRef headVr && engine is not null)
                {
                    var addr = headVr.Addr;
                    if (path.Contains(addr))
                    {
                        headStr = "<circular>";
                    }
                    else
                    {
                        path.Add(addr);
                        var derefHead = engine.Runtime.Heap.Dereference(headVr);
                        if (derefHead is VarRef derefVr)
                        {
                            var displayId = derefVr.Addr;
                            headStr = engine.Runtime.Heap.IsReader(derefVr.Addr)
                                ? $"X{displayId}?"
                                : $"X{displayId}";
                        }
                        else
                        {
                            headStr = FormatTerm(derefHead, engine, path);
                        }
                        path.Remove(addr);
                    }
                }
                else
                {
                    headStr = FormatTerm(head, engine, path);
                }

                elements.Add(headStr);

                if (tail is VarRef tailVr && engine is not null)
                {
                    var addr = tailVr.Addr;
                    if (path.Contains(addr))
                    {
                        var displayId = addr;
                        var label = engine.Runtime.Heap.IsReader(tailVr.Addr)
                            ? $"X{displayId}?"
                            : $"X{displayId}";
                        return $"[{string.Join(", ", elements)} | <circular {label}>]";
                    }
                    path.Add(addr);
                    var derefTail = engine.Runtime.Heap.Dereference(tailVr);
                    if (derefTail is VarRef derefTailVr)
                    {
                        path.Remove(addr);
                        var displayId = derefTailVr.Addr;
                        var label = engine.Runtime.Heap.IsReader(derefTailVr.Addr)
                            ? $"X{displayId}?"
                            : $"X{displayId}";
                        return $"[{string.Join(", ", elements)} | {label}]";
                    }
                    current = derefTail;
                    path.Remove(addr);
                    if (current is not StructTerm)
                        break;
                }
                else if (tail is ConstTerm tailCt &&
                         (tailCt.Value == null || (tailCt.Value is string ts && ts == "nil")))
                {
                    break;
                }
                else if (tail is StructTerm tailSt && tailSt.Functor == ".")
                {
                    current = tailSt;
                }
                else
                {
                    break;
                }
            }

            return $"[{string.Join(", ", elements)}]";
        }

        if (term is StructTerm s)
        {
            var currentPath = path;
            var formattedArgs = string.Join(", ", s.Args.Select(arg =>
            {
                if (arg is VarRef argVr && engine is not null)
                {
                    var addr = argVr.Addr;
                    if (currentPath.Contains(addr))
                        return "<circular>";
                    currentPath.Add(addr);
                    var deref = engine.Runtime.Heap.Dereference(argVr);
                    string result;
                    if (deref is VarRef derefVr)
                    {
                        var displayId = derefVr.Addr;
                        result = engine.Runtime.Heap.IsReader(derefVr.Addr)
                            ? $"X{displayId}?"
                            : $"X{displayId}";
                    }
                    else
                    {
                        result = FormatTerm(deref, engine, currentPath);
                    }
                    currentPath.Remove(addr);
                    return result;
                }
                return FormatTerm(arg, engine, currentPath);
            }));
            return $"{s.Functor}({formattedArgs})";
        }

        return term.ToString() ?? string.Empty;
    }
}

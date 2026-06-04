/// <summary>
/// Body kernel infrastructure for GLP arithmetic.
/// <para>
/// Body kernels are runtime-implemented predicates that:
/// - Execute inline (not spawned as separate goals)
/// - Have two-valued semantics (success or abort)
/// - Are only accessible to system predicates (assign.glp)
/// - Expect all preconditions met (guards should verify before calling)
/// </para>
/// <para>
/// Per heap-pointer-architecture-spec.md v3.0:
/// - VarRef has only addr field
/// - Use heap.IsWriter/IsReader to check cell type
/// </para>
/// </summary>

using System;
using System.Collections.Generic;
using System.Linq;

using GlpRuntime.Bytecode;
using GlpRuntime.Multiagent;

namespace GlpRuntime.Runtime;

/// <summary>Result of executing a body kernel.</summary>
public enum BodyKernelResult
{
    /// <summary>Kernel succeeded - continue execution.</summary>
    Success,

    /// <summary>Kernel aborted - fatal error (e.g., type error, unbound reader).</summary>
    Abort,
}

/// <summary>Body kernel function signature.</summary>
public delegate BodyKernelResult BodyKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args);

/// <summary>Registry of body kernels.</summary>
public class BodyKernelRegistry
{
    private readonly Dictionary<string, BodyKernel> _kernels = new Dictionary<string, BodyKernel>();

    public void Register(string name, long arity, BodyKernel kernel)
    {
        _kernels[$"{name}/{arity}"] = kernel;
    }

    public BodyKernel? Lookup(string name, long arity) =>
        _kernels.TryGetValue($"{name}/{arity}", out var k) ? k : null;

    public bool Has(string name, long arity) => _kernels.ContainsKey($"{name}/{arity}");

    public IEnumerable<string> Names => _kernels.Keys;
}

/// <summary>Register all standard body kernels.</summary>
public static class BodyKernelsModule
{
    public static void RegisterStandardBodyKernels(BodyKernelRegistry registry)
    {
        // Arithmetic operations
        registry.Register("_add", 3, AddKernel);
        registry.Register("_sub", 3, SubKernel);
        registry.Register("_mul", 3, MulKernel);
        registry.Register("_div", 3, DivKernel);
        registry.Register("_idiv", 3, IdivKernel);
        registry.Register("_mod", 3, ModKernel);
        registry.Register("_neg", 2, NegKernel);

        // Math functions
        registry.Register("_abs", 2, AbsKernel);
        registry.Register("_sqrt", 2, SqrtKernel);
        registry.Register("_sin", 2, SinKernel);
        registry.Register("_cos", 2, CosKernel);
        registry.Register("_tan", 2, TanKernel);
        registry.Register("_exp", 2, ExpKernel);
        registry.Register("_ln", 2, LnKernel);
        registry.Register("_log10", 2, Log10Kernel);
        registry.Register("_pow", 3, PowKernel);
        registry.Register("_asin", 2, AsinKernel);
        registry.Register("_acos", 2, AcosKernel);
        registry.Register("_atan", 2, AtanKernel);

        // Type conversions
        registry.Register("_integer", 2, IntegerKernel);
        registry.Register("_real", 2, RealKernel);
        registry.Register("_round", 2, RoundKernel);
        registry.Register("_floor", 2, FloorKernel);
        registry.Register("_ceil", 2, CeilKernel);

        // Structure manipulation
        registry.Register("_list_to_tuple", 2, ListToTupleKernel);
        registry.Register("_tuple_to_list", 2, TupleToListKernel);

        // Identity/copy
        registry.Register("_copy", 2, CopyKernel);

        // Time operations
        registry.Register("_now", 1, NowKernel);

        // MutualRef operations (O(1) stream append)
        registry.Register("_allocate_mutual_reference", 2, MutualRefKernel);
        registry.Register("_stream_append", 3, StreamAppendKernel);
        registry.Register("_close_mutual_reference", 1, MutualRefCloseKernel);

        // madGLP kernels
        registry.Register("_send", 3, SendKernel);

        // I/O kernels
        registry.Register("_output", 1, OutputKernel);

        // Module dispatch kernels
        registry.Register("_activate", 2, ActivateKernel);
    }

    // ============================================================================
    // PRIVATE HELPERS
    // ============================================================================

    /// <summary>Get numeric value from argument (with arithmetic evaluation).</summary>
    private static double? GetNum(GlpRuntimeEngine rt, object? arg)
    {
        if (arg is double dn) return dn;
        if (arg is long ln) return (double)ln;
        if (arg is int i32) return (double)i32;
        if (arg is ConstTerm ct && ct.Value is double cv) return cv;
        if (arg is ConstTerm ct2 && ct2.Value is long lv) return (double)lv;
        if (arg is ConstTerm ct3 && ct3.Value is int iv) return (double)iv;
        if (arg is VarRef vr)
        {
            var term = rt.Heap.GetValue(vr.Addr);
            return GetNum(rt, term);
        }
        if (arg is StructTerm st) return EvaluateArithmetic(rt, st);
        return null;
    }

    /// <summary>
    /// Numeric value preserving int-ness — boxed <c>long</c> when integral, <c>double</c>
    /// otherwise — mirroring Dart's <c>num</c>. Used by the int-preserving arithmetic
    /// kernels (+, -, *, neg, abs) so e.g. <c>3 - 1</c> stays integer <c>2</c> (matches
    /// Dart). The widening <see cref="GetNum"/> stays for /, sqrt, sin, … which are
    /// double in Dart too.
    /// </summary>
    private static object? GetNumeric(GlpRuntimeEngine rt, object? arg)
    {
        if (arg is double dn) return dn;
        if (arg is long ln) return ln;
        if (arg is int i32) return (long)i32;
        if (arg is ConstTerm ct)
        {
            if (ct.Value is double cv) return cv;
            if (ct.Value is long lv) return lv;
            if (ct.Value is int iv) return (long)iv;
        }
        if (arg is VarRef vr) return GetNumeric(rt, rt.Heap.GetValue(vr.Addr));
        if (arg is StructTerm st) return EvaluateArithmeticNum(rt, st);
        return null;
    }

    private static bool BothLong(object a, object b) => a is long && b is long;
    private static double ToD(object v) => v is double d ? d : (long)v;
    private static object NumAdd(object a, object b) => BothLong(a, b) ? (object)((long)a + (long)b) : ToD(a) + ToD(b);
    private static object NumSub(object a, object b) => BothLong(a, b) ? (object)((long)a - (long)b) : ToD(a) - ToD(b);
    private static object NumMul(object a, object b) => BothLong(a, b) ? (object)((long)a * (long)b) : ToD(a) * ToD(b);
    private static object NumMod(object a, object b) => BothLong(a, b) ? (object)((long)a % (long)b) : ToD(a) % ToD(b);
    private static object NumNeg(object a) => a is long l ? (object)(-l) : -(double)a;
    private static object NumAbs(object a) => a is long l ? (object)Math.Abs(l) : Math.Abs((double)a);

    /// <summary>
    /// Evaluate an arithmetic structure preserving int-ness (Dart <c>num</c> semantics:
    /// +,-,*,//,mod,neg keep int when both operands integral; <c>/</c> is always double).
    /// </summary>
    private static object? EvaluateArithmeticNum(GlpRuntimeEngine rt, StructTerm st)
    {
        var a = st.Args.Select(x => GetNumeric(rt, x)).ToList();
        if (a.Any(v => v == null)) return null;
        switch (st.Functor)
        {
            case "+":   return NumAdd(a[0]!, a[1]!);
            case "-":   return NumSub(a[0]!, a[1]!);
            case "*":   return NumMul(a[0]!, a[1]!);
            case "/":   return ToD(a[1]!) == 0 ? null : (object)(ToD(a[0]!) / ToD(a[1]!));
            case "//":  return ToD(a[1]!) == 0 ? null : (object)(long)Math.Truncate(ToD(a[0]!) / ToD(a[1]!));
            case "mod": return ToD(a[1]!) == 0 ? null : NumMod(a[0]!, a[1]!);
            case "neg": return NumNeg(a[0]!);
            default:    return null;
        }
    }

    /// <summary>Get integer value from argument (for int-only kernels: idiv, mod).</summary>
    private static long? GetLong(GlpRuntimeEngine rt, object? arg)
    {
        if (arg is long ln) return ln;
        if (arg is int i32) return (long)i32;
        if (arg is ConstTerm ct && ct.Value is long lv) return lv;
        if (arg is ConstTerm ct2 && ct2.Value is int iv) return (long)iv;
        if (arg is VarRef vr)
        {
            var term = rt.Heap.GetValue(vr.Addr);
            return GetLong(rt, term);
        }
        return null;
    }

    /// <summary>Evaluate arithmetic structure to numeric value.</summary>
    private static double? EvaluateArithmetic(GlpRuntimeEngine rt, StructTerm st)
    {
        var args = st.Args.Select(a => GetNum(rt, a)).ToList();
        if (args.Any(a => a == null)) return null;

        return st.Functor switch
        {
            "+" => args[0]!.Value + args[1]!.Value,
            "-" => args[0]!.Value - args[1]!.Value,
            "*" => args[0]!.Value * args[1]!.Value,
            "/" => args[1]! == 0 ? (double?)null : args[0]!.Value / args[1]!.Value,
            "//" => args[1]! == 0 ? (double?)null : Math.Truncate(args[0]!.Value / args[1]!.Value),
            "mod" => args[1]! == 0 ? (double?)null : args[0]!.Value % args[1]!.Value,
            "neg" => -args[0]!.Value,
            _ => (double?)null,
        };
    }

    /// <summary>Helper to bind result to output writer.</summary>
    private static BodyKernelResult BindResult(GlpRuntimeEngine rt, object? outputArg, object value)
    {
        if (outputArg is VarRef vr && rt.Heap.IsWriter(vr.Addr))
        {
            IReadOnlyList<GoalRef> activations;
            if (value is Term t)
            {
                activations = rt.Heap.BindVariable(vr.Addr, t);
            }
            else
            {
                activations = rt.Heap.BindVariableConst(vr.Addr, value);
            }
            foreach (var act in activations)
            {
                rt.Gq.Enqueue(act);
            }
            return BodyKernelResult.Success;
        }
        Console.WriteLine("[ABORT] Body kernel: output argument is not a writer");
        return BodyKernelResult.Abort;
    }

    /// <summary>Shallow dereference — follows top-level VarRef chain.</summary>
    private static object? Deref(GlpRuntimeEngine rt, object? term)
    {
        while (term is VarRef vr)
        {
            var val = rt.Heap.GetValue(vr.Addr);
            if (val == null) return term;
            term = val;
        }
        return term;
    }

    /// <summary>Deep dereference — recursively follows all VarRefs in structure.</summary>
    private static Term DeepDeref(GlpRuntimeEngine rt, Term term)
    {
        var current = term;
        while (current is VarRef vr)
        {
            var val = rt.Heap.GetValue(vr.Addr);
            if (val == null || val is not Term) return current;
            current = val;
        }
        if (current is StructTerm st)
        {
            var newArgs = new List<Term>();
            foreach (var arg in st.Args)
            {
                newArgs.Add(DeepDeref(rt, arg));
            }
            return new StructTerm(st.Functor, newArgs);
        }
        return current;
    }

    /// <summary>Convert a C# list to a GLP cons-cell list.</summary>
    private static Term DartListToGlpList(IReadOnlyList<object?> items)
    {
        Term result = new ConstTerm("nil");
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            Term termItem = item is Term t ? t : new ConstTerm(item);
            result = new StructTerm(".", new List<Term> { termItem, result });
        }
        return result;
    }

    /// <summary>Convert a GLP cons-cell list to a C# list. Returns null on malformed input.</summary>
    private static IReadOnlyList<object?>? GlpListToDartList(GlpRuntimeEngine rt, object? list)
    {
        var result = new List<object?>();
        var current = Deref(rt, list);

        while (current != null)
        {
            if (current is ConstTerm ct && ct.Value is "nil") return result;
            if (current is ConstTerm ct2 && ct2.Value == null) return result;
            if (current is StructTerm st && st.Functor == "." && st.Args.Count == 2)
            {
                result.Add(Deref(rt, st.Args[0]));
                current = Deref(rt, st.Args[1]);
            }
            else
            {
                return null;
            }
        }
        return result;
    }

    // ============================================================================
    // ARITHMETIC KERNELS
    // ============================================================================

    public static BodyKernelResult AddKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 3)
        {
            Console.WriteLine($"[ABORT] add/3: expected 3 arguments, got {args.Count}");
            return BodyKernelResult.Abort;
        }
        var x = GetNumeric(rt, args[0]);
        var y = GetNumeric(rt, args[1]);
        if (x == null || y == null)
        {
            Console.WriteLine("[ABORT] add/3: operands must be numbers");
            return BodyKernelResult.Abort;
        }
        return BindResult(rt, args[2], NumAdd(x, y));
    }

    public static BodyKernelResult SubKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 3)
        {
            Console.WriteLine($"[ABORT] sub/3: expected 3 arguments, got {args.Count}");
            return BodyKernelResult.Abort;
        }
        var x = GetNumeric(rt, args[0]);
        var y = GetNumeric(rt, args[1]);
        if (x == null || y == null)
        {
            Console.WriteLine("[ABORT] sub/3: operands must be numbers");
            return BodyKernelResult.Abort;
        }
        return BindResult(rt, args[2], NumSub(x, y));
    }

    public static BodyKernelResult MulKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 3)
        {
            Console.WriteLine($"[ABORT] mul/3: expected 3 arguments, got {args.Count}");
            return BodyKernelResult.Abort;
        }
        var x = GetNumeric(rt, args[0]);
        var y = GetNumeric(rt, args[1]);
        if (x == null || y == null)
        {
            Console.WriteLine("[ABORT] mul/3: operands must be numbers");
            return BodyKernelResult.Abort;
        }
        return BindResult(rt, args[2], NumMul(x, y));
    }

    public static BodyKernelResult DivKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 3)
        {
            Console.WriteLine($"[ABORT] div/3: expected 3 arguments, got {args.Count}");
            return BodyKernelResult.Abort;
        }
        var x = GetNum(rt, args[0]);
        var y = GetNum(rt, args[1]);
        if (x == null || y == null)
        {
            Console.WriteLine("[ABORT] div/3: operands must be numbers");
            return BodyKernelResult.Abort;
        }
        if (y == 0)
        {
            Console.WriteLine("[ABORT] div/3: division by zero");
            return BodyKernelResult.Abort;
        }
        return BindResult(rt, args[2], x.Value / y.Value);
    }

    public static BodyKernelResult IdivKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 3)
        {
            Console.WriteLine($"[ABORT] idiv/3: expected 3 arguments, got {args.Count}");
            return BodyKernelResult.Abort;
        }
        var x = GetLong(rt, args[0]);
        var y = GetLong(rt, args[1]);
        if (x == null || y == null)
        {
            Console.WriteLine("[ABORT] idiv/3: operands must be integers");
            return BodyKernelResult.Abort;
        }
        if (y == 0)
        {
            Console.WriteLine("[ABORT] idiv/3: division by zero");
            return BodyKernelResult.Abort;
        }
        return BindResult(rt, args[2], x.Value / y.Value);
    }

    public static BodyKernelResult ModKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 3)
        {
            Console.WriteLine($"[ABORT] mod/3: expected 3 arguments, got {args.Count}");
            return BodyKernelResult.Abort;
        }
        var x = GetLong(rt, args[0]);
        var y = GetLong(rt, args[1]);
        if (x == null || y == null)
        {
            Console.WriteLine("[ABORT] mod/3: operands must be integers");
            return BodyKernelResult.Abort;
        }
        if (y == 0)
        {
            Console.WriteLine("[ABORT] mod/3: modulo by zero");
            return BodyKernelResult.Abort;
        }
        return BindResult(rt, args[2], x.Value % y.Value);
    }

    public static BodyKernelResult NegKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2)
        {
            Console.WriteLine($"[ABORT] neg/2: expected 2 arguments, got {args.Count}");
            return BodyKernelResult.Abort;
        }
        var x = GetNumeric(rt, args[0]);
        if (x == null)
        {
            Console.WriteLine("[ABORT] neg/2: operand must be a number");
            return BodyKernelResult.Abort;
        }
        return BindResult(rt, args[1], NumNeg(x));
    }

    // ============================================================================
    // MATH FUNCTION KERNELS
    // ============================================================================

    public static BodyKernelResult AbsKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNumeric(rt, args[0]);
        if (x == null) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], NumAbs(x));
    }

    public static BodyKernelResult SqrtKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null || x.Value < 0) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], Math.Sqrt(x.Value));
    }

    public static BodyKernelResult SinKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], Math.Sin(x.Value));
    }

    public static BodyKernelResult CosKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], Math.Cos(x.Value));
    }

    public static BodyKernelResult TanKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], Math.Tan(x.Value));
    }

    public static BodyKernelResult ExpKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], Math.Exp(x.Value));
    }

    public static BodyKernelResult LnKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null || x.Value <= 0) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], Math.Log(x.Value));
    }

    public static BodyKernelResult Log10Kernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null || x.Value <= 0) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], Math.Log(x.Value) / Math.Log(10.0));
    }

    public static BodyKernelResult PowKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 3) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        var y = GetNum(rt, args[1]);
        if (x == null || y == null) return BodyKernelResult.Abort;
        return BindResult(rt, args[2], Math.Pow(x.Value, y.Value));
    }

    public static BodyKernelResult AsinKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null || x.Value < -1 || x.Value > 1) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], Math.Asin(x.Value));
    }

    public static BodyKernelResult AcosKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null || x.Value < -1 || x.Value > 1) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], Math.Acos(x.Value));
    }

    public static BodyKernelResult AtanKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], Math.Atan(x.Value));
    }

    // ============================================================================
    // TYPE CONVERSION KERNELS
    // ============================================================================

    public static BodyKernelResult IntegerKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], (long)x.Value);
    }

    public static BodyKernelResult RealKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], x.Value);
    }

    public static BodyKernelResult RoundKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], (long)Math.Round(x.Value, MidpointRounding.AwayFromZero));
    }

    public static BodyKernelResult FloorKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], (long)Math.Floor(x.Value));
    }

    public static BodyKernelResult CeilKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2) return BodyKernelResult.Abort;
        var x = GetNum(rt, args[0]);
        if (x == null) return BodyKernelResult.Abort;
        return BindResult(rt, args[1], (long)Math.Ceiling(x.Value));
    }

    // ============================================================================
    // STRUCTURE MANIPULATION KERNELS
    // ============================================================================

    public static BodyKernelResult ListToTupleKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2)
        {
            Console.WriteLine($"[ABORT] list_to_tuple/2: expected 2 arguments, got {args.Count}");
            return BodyKernelResult.Abort;
        }

        var listArg = Deref(rt, args[0]);
        var items = GlpListToDartList(rt, listArg);

        if (items == null || items.Count == 0)
        {
            Console.WriteLine("[ABORT] list_to_tuple/2: first argument must be a non-empty list");
            return BodyKernelResult.Abort;
        }

        var functorTerm = items[0];
        string? functor = null;
        if (functorTerm is ConstTerm ct && ct.Value is string sv)
        {
            functor = sv;
        }
        else if (functorTerm is string fs)
        {
            functor = fs;
        }

        if (functor == null)
        {
            Console.WriteLine("[ABORT] list_to_tuple/2: first element must be an atom (functor)");
            return BodyKernelResult.Abort;
        }

        var structArgs = new List<Term>();
        for (var i = 1; i < items.Count; i++)
        {
            var item = items[i];
            structArgs.Add(item is Term t ? t : new ConstTerm(item));
        }

        var tuple = new StructTerm(functor, structArgs);
        return BindResult(rt, args[1], tuple);
    }

    public static BodyKernelResult TupleToListKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2)
        {
            Console.WriteLine($"[ABORT] tuple_to_list/2: expected 2 arguments, got {args.Count}");
            return BodyKernelResult.Abort;
        }

        var tupleArg = Deref(rt, args[0]);

        if (tupleArg is not StructTerm st)
        {
            Console.WriteLine("[ABORT] tuple_to_list/2: first argument must be a structure");
            return BodyKernelResult.Abort;
        }

        var items = new List<object?> { new ConstTerm(st.Functor) };
        foreach (var arg in st.Args)
        {
            items.Add(Deref(rt, arg));
        }

        var list = DartListToGlpList(items);
        return BindResult(rt, args[1], list);
    }

    public static BodyKernelResult CopyKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2)
        {
            Console.WriteLine($"[ABORT] copy/2: expected 2 arguments, got {args.Count}");
            return BodyKernelResult.Abort;
        }

        var source = Deref(rt, args[0]);
        return BindResult(rt, args[1], source!);
    }

    // ============================================================================
    // TIME KERNELS
    // ============================================================================

    public static BodyKernelResult NowKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 1)
        {
            Console.WriteLine($"[ABORT] now/1: expected 1 argument, got {args.Count}");
            return BodyKernelResult.Abort;
        }
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return BindResult(rt, args[0], currentTime);
    }

    // ============================================================================
    // MUTUAL REFERENCE KERNELS (O(1) Stream Append)
    // ============================================================================

    public static BodyKernelResult MutualRefKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2)
        {
            Console.WriteLine($"[ABORT] allocate_mutual_reference/2: expected 2 arguments, got {args.Count}");
            return BodyKernelResult.Abort;
        }

        var output = Deref(rt, args[1]);
        if (output is not VarRef vr || !rt.Heap.IsWriter(vr.Addr))
        {
            Console.WriteLine("[ABORT] allocate_mutual_reference/2: second argument must be an unbound writer");
            return BodyKernelResult.Abort;
        }

        if (rt.Heap.IsFullyBound(vr.Addr))
        {
            Console.WriteLine($"[ABORT] allocate_mutual_reference/2: writer @{vr.Addr} is already bound");
            return BodyKernelResult.Abort;
        }

        var mutualRef = new MutualRefTerm(vr.Addr);
        return BindResult(rt, args[0], mutualRef);
    }

    public static BodyKernelResult StreamAppendKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 3)
        {
            Console.WriteLine($"[ABORT] kernel_stream_append/3: expected 3 arguments, got {args.Count}");
            return BodyKernelResult.Abort;
        }

        var refArg = Deref(rt, args[0]);
        if (refArg is not MutualRefTerm mr)
        {
            Console.WriteLine("[ABORT] kernel_stream_append/3: first argument must be a MutualRef");
            return BodyKernelResult.Abort;
        }

        var currentWriterAddr = mr.CurrentWriterAddr;

        if (rt.Heap.IsFullyBound(currentWriterAddr))
        {
            Console.WriteLine($"[ABORT] kernel_stream_append/3: MutualRef points to already-bound writer @{currentWriterAddr}");
            return BodyKernelResult.Abort;
        }

        var value = Deref(rt, args[1]);
        Term termValue = value is Term tv ? tv : new ConstTerm(value);

        // Allocate fresh variable for new tail
        var (newTailWriter, newTailReader) = rt.Heap.AllocateVariable();

        // Build cons cell: '.'(Value, NewTail?)
        var consCell = new StructTerm(".", new List<Term> { termValue, new VarRef(newTailReader) });

        // Bind current writer to the cons cell
        var activations = rt.Heap.BindVariable(currentWriterAddr, consCell);

        foreach (var act in activations)
        {
            rt.Gq.Enqueue(act);
        }

        // Update MutualRef to point to the new tail's writer
        mr.CurrentWriterAddr = newTailWriter;

        return BindResult(rt, args[2], mr);
    }

    public static BodyKernelResult MutualRefCloseKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 1)
        {
            Console.WriteLine($"[ABORT] kernel_close_mutual_reference/1: expected 1 argument, got {args.Count}");
            return BodyKernelResult.Abort;
        }

        var refArg = Deref(rt, args[0]);
        if (refArg is not MutualRefTerm mr)
        {
            Console.WriteLine("[ABORT] kernel_close_mutual_reference/1: argument must be a MutualRef");
            return BodyKernelResult.Abort;
        }

        var currentWriterAddr = mr.CurrentWriterAddr;

        if (rt.Heap.IsFullyBound(currentWriterAddr))
        {
            Console.WriteLine($"[ABORT] kernel_close_mutual_reference/1: MutualRef points to already-bound writer @{currentWriterAddr}");
            return BodyKernelResult.Abort;
        }

        var activations = rt.Heap.BindVariable(currentWriterAddr, new ConstTerm("nil"));

        foreach (var act in activations)
        {
            rt.Gq.Enqueue(act);
        }

        return BodyKernelResult.Success;
    }

    // ============================================================================
    // MADGLP KERNELS
    // ============================================================================

    /// <summary>
    /// Send kernel for madGLP.
    /// <para>
    /// '_send'(T, G, Q) - sends term T via global name G to agent Q.
    /// This is called by the GLP global_send/3 predicate.
    /// </para>
    /// <para>
    /// Per madGLP-spec.md Section 11.5:
    /// - Case G = _w(q, 0) (Serializer): wraps T in list [T | _w(q,0)]
    /// - Case G = _w(p, i) or _r(p, i) with i > 0: sends T directly
    /// </para>
    /// <para>Threading-model decision inherited from mad_context.dart.md — NOT re-decided here.</para>
    /// </summary>
    public static BodyKernelResult SendKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 3)
        {
            Console.WriteLine($"[ABORT] '_send'/3: expected 3 arguments, got {args.Count}");
            return BodyKernelResult.Abort;
        }

        // Get MadContext from runtime
        var ctx = rt.MadContext;
        if (ctx is not MadContext mc)
        {
            Console.WriteLine("[ABORT] '_send'/3: not in madGLP mode (no MadContext)");
            return BodyKernelResult.Abort;
        }

        // Get term T (first argument) — use DeepDeref to fully resolve nested structures
        var termArg = DeepDeref(rt, (Term)args[0]!);

        // Get global name G (second argument) — should be _w(Agent, Index) or _r(Agent, Index)
        var globalNameArg = Deref(rt, args[1]);
        if (globalNameArg is not StructTerm gnSt)
        {
            Console.WriteLine($"[ABORT] '_send'/3: second argument (G) must be a struct _w/2 or _r/2, got {globalNameArg?.GetType().Name}");
            return BodyKernelResult.Abort;
        }

        // Parse the global name structure
        var functor = gnSt.Functor;
        if (functor != "'_w'" && functor != "'_r'" && functor != "_w" && functor != "_r")
        {
            Console.WriteLine($"[ABORT] '_send'/3: global name must be _w/2 or _r/2, got {functor}");
            return BodyKernelResult.Abort;
        }
        if (gnSt.Args.Count != 2)
        {
            Console.WriteLine($"[ABORT] '_send'/3: global name must have 2 arguments, got {gnSt.Args.Count}");
            return BodyKernelResult.Abort;
        }

        // Extract agent from global name (first arg of _w/_r)
        var gnAgentArg = Deref(rt, gnSt.Args[0]);
        string? gnAgent = null;
        if (gnAgentArg is ConstTerm gct && gct.Value is string gcs)
        {
            gnAgent = gcs;
        }
        else if (gnAgentArg is string gs)
        {
            gnAgent = gs;
        }
        if (gnAgent == null)
        {
            Console.WriteLine($"[ABORT] '_send'/3: global name agent must be an atom, got {gnAgentArg}");
            return BodyKernelResult.Abort;
        }

        // Extract index from global name (second arg of _w/_r)
        var gnIndexArg = Deref(rt, gnSt.Args[1]);
        int? gnIndex = null;
        if (gnIndexArg is double gnd) gnIndex = (int)gnd;
        else if (gnIndexArg is long gnl) gnIndex = (int)gnl;
        else if (gnIndexArg is int gni) gnIndex = gni;
        else if (gnIndexArg is ConstTerm gct2 && gct2.Value is double gncd) gnIndex = (int)gncd;
        else if (gnIndexArg is ConstTerm gct3 && gct3.Value is long gncl) gnIndex = (int)gncl;
        else if (gnIndexArg is ConstTerm gct4 && gct4.Value is int gnci) gnIndex = gnci;
        if (gnIndex == null)
        {
            Console.WriteLine($"[ABORT] '_send'/3: global name index must be a number, got {gnIndexArg}");
            return BodyKernelResult.Abort;
        }

        // Get destination agent Q (third argument)
        var destArg = Deref(rt, args[2]);
        string? destAgent = null;
        if (destArg is ConstTerm dct && dct.Value is string dcs)
        {
            destAgent = dcs;
        }
        else if (destArg is string ds)
        {
            destAgent = ds;
        }
        if (destAgent == null)
        {
            Console.WriteLine($"[ABORT] '_send'/3: third argument (Q) must be an atom (destination agent), got {destArg}");
            return BodyKernelResult.Abort;
        }

        // Determine if this is a writer or reader global name
        bool isWriter = functor == "'_w'" || functor == "_w";

        // Unified send handles both serializer (index 0) and normal (index > 0) cases
        // Threading-model inherited from mad_context.dart.md — NOT re-decided here
        mc.Send(termArg, isWriter, gnAgent, gnIndex.Value, destAgent);

        return BodyKernelResult.Success;
    }

    // ============================================================================
    // I/O KERNELS
    // ============================================================================

    /// <summary>
    /// '_output'(T) - prints ground term T to stdout.
    /// <para>
    /// Called by send_to_user/1 after ui_relay has ensured the term is ground.
    /// The output callback can be overridden via GlpRuntime.OutputCallback
    /// for testing or Flutter integration.
    /// </para>
    /// </summary>
    public static BodyKernelResult OutputKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 1)
        {
            Console.WriteLine($"[ABORT] '_output'/1: expected 1 argument, got {args.Count}");
            return BodyKernelResult.Abort;
        }

        var term = DeepDeref(rt, (Term)args[0]!);
        var formatted = FormatGroundTerm(term);

        // Use callback if set (for testing/Flutter), otherwise print
        var callback = rt.OutputCallback;
        if (callback != null)
        {
            callback(formatted);
        }
        else
        {
            Console.WriteLine(formatted);
        }

        return BodyKernelResult.Success;
    }

    /// <summary>
    /// Format a ground term as readable GLP syntax.
    /// Lists are shown as [a, b, c], atoms as-is, structs as f(a, b).
    /// </summary>
    public static string FormatGroundTerm(Term term)
    {
        if (term is ConstTerm ct)
        {
            if (ct.Value is "nil" || ct.Value == null) return "[]";
            return ct.Value.ToString()!;
        }
        if (term is StructTerm st)
        {
            // List: .(H, T) -> [H, ...]
            if (st.Functor == "." && st.Args.Count == 2)
            {
                var elements = new List<string>();
                Term current = term;
                while (current is StructTerm cst && cst.Functor == "." && cst.Args.Count == 2)
                {
                    elements.Add(FormatGroundTerm(cst.Args[0]));
                    current = cst.Args[1];
                }
                if (current is ConstTerm cct && (cct.Value is "nil" || cct.Value == null))
                {
                    return $"[{string.Join(", ", elements)}]";
                }
                return $"[{string.Join(", ", elements)} | {FormatGroundTerm(current)}]";
            }
            var argsStr = string.Join(", ", st.Args.Select(FormatGroundTerm));
            return $"{st.Functor}({argsStr})";
        }
        return term.ToString()!;
    }

    // ============================================================================
    // MODULE DISPATCH KERNELS
    // ============================================================================

    /// <summary>
    /// '_activate'(Module?, Goal) — dispatch Goal directly to the exported procedure.
    /// <para>
    /// Module? is a reader referencing a ModuleTerm (wrapping a BytecodeProgram).
    /// Goal is a term representing the remote procedure call (e.g., double(5, F)).
    /// </para>
    /// <para>
    /// If the procedure is not found, the goal silently succeeds (fallback
    /// behavior matching _select/1's otherwise clause).
    /// </para>
    /// </summary>
    public static BodyKernelResult ActivateKernel(GlpRuntimeEngine rt, IReadOnlyList<object?> args)
    {
        if (args.Count != 2)
        {
            Console.WriteLine($"[ABORT] _activate/2: expected 2 arguments, got {args.Count}");
            return BodyKernelResult.Abort;
        }

        // Dereference Module? (first arg) to get the ModuleTerm
        var moduleArg = Deref(rt, args[0]);
        if (moduleArg is not ModuleTerm mt)
        {
            Console.WriteLine($"[ABORT] _activate/2: first argument must be a ModuleTerm, got {moduleArg?.GetType().Name}");
            return BodyKernelResult.Abort;
        }

        var bytecode = mt.Bytecode;
        if (bytecode is not BytecodeProgram bp)
        {
            Console.WriteLine("[ABORT] _activate/2: ModuleTerm does not contain a BytecodeProgram");
            return BodyKernelResult.Abort;
        }

        // Dereference the Goal term (second arg)
        var goalArg = Deref(rt, args[1]);
        if (goalArg is not StructTerm sg)
        {
            // Not a structured goal — silently succeed (fallback)
            return BodyKernelResult.Success;
        }

        // Extract functor/arity and look up the procedure directly
        var label = $"{sg.Functor}/{sg.Args.Count}";
        if (!bp.Labels.TryGetValue(label, out int entryPc))
        {
            // Procedure not found — silently succeed (fallback behavior)
            return BodyKernelResult.Success;
        }

        // Spawn the procedure with the goal's original arguments.
        // Each arg is stored on the heap as a VarRef. For VarRef args (e.g.,
        // unbound writers for output params), StoreTermOnHeap returns the
        // existing heap address, preserving writer/reader polarity.
        var argSlots = new Dictionary<int, Term>();
        for (int i = 0; i < sg.Args.Count; i++)
        {
            var addr = rt.Heap.StoreTermOnHeap(sg.Args[i]);
            argSlots[i] = new VarRef(addr);
        }

        var newGoalId = rt.NextGoalId++;
        var env = new CallEnv(args: argSlots);
        rt.SetGoalEnv(newGoalId, env);
        rt.SetGoalProgram(newGoalId, bp);

        // Ensure a BytecodeRunner exists for this program in rt.Runners
        if (!rt.Runners.ContainsKey(bp))
        {
            rt.Runners[bp] = new BytecodeRunner(bp);
        }

        // Enqueue the goal
        rt.Gq.Enqueue(new GoalRef(newGoalId, entryPc));

        return BodyKernelResult.Success;
    }
}

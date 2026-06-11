using Bc = GlpRuntime.Bytecode;
using V2 = GlpRuntime.Bytecode.V2;

namespace GlpRuntime.IlCodec;

/// <summary>
/// The closed discriminant registry: one entry per concrete opcode class across
/// the three families (v1 <c>IOp</c> = 0x01, v2 <c>IOpV2</c> = 0x02, <c>Label</c>
/// marker = 0x03). Each entry pairs a fixed (family, discriminant) byte pair with
/// an operand <c>Write</c>/<c>Read</c> closure, so encode (lookup by runtime type)
/// and decode (lookup by byte pair) draw from the SAME table — they cannot drift.
///
/// Completeness (F1 / T029): every concrete <c>IOp</c> and <c>IOpV2</c> subtype in
/// <c>glp_runtime_net</c> MUST appear here. A reflection test asserts this and that
/// encoding a class with no entry fails loud. The v2 <c>Unknown</c> opcode is the
/// one v2 class that carries no <c>IsReader</c> byte (the design docs' "6 v2
/// classes + isReader" undercounted it; the registry is exhaustive by rule).
/// </summary>
internal static class OpcodeDiscriminant
{
    public const byte FamilyV1 = 0x01;
    public const byte FamilyV2 = 0x02;
    public const byte FamilyLabel = 0x03;

    internal sealed record OpEntry(
        byte Family,
        byte Disc,
        Type Type,
        Action<BinaryWriter, object> Write,
        Func<BinaryReader, object> Read);

    private static readonly List<OpEntry> _entries = Build();

    private static readonly Dictionary<Type, OpEntry> _byType =
        _entries.ToDictionary(e => e.Type);

    private static readonly Dictionary<int, OpEntry> _byCode =
        _entries.ToDictionary(e => Key(e.Family, e.Disc));

    private static int Key(byte family, byte disc) => (family << 8) | disc;

    /// <summary>All opcode runtime types that have a registry entry (for the T029 completeness test).</summary>
    public static IReadOnlyCollection<Type> RegisteredTypes => _byType.Keys;

    public static bool TryGetByType(Type t, out OpEntry entry) => _byType.TryGetValue(t, out entry!);

    public static bool TryGetByCode(byte family, byte disc, out OpEntry entry) =>
        _byCode.TryGetValue(Key(family, disc), out entry!);

    private static List<OpEntry> Build()
    {
        var es = new List<OpEntry>();

        void V1(byte disc, Type t, Action<BinaryWriter, object> wr, Func<BinaryReader, object> rd) =>
            es.Add(new OpEntry(FamilyV1, disc, t, wr, rd));
        void V2e(byte disc, Type t, Action<BinaryWriter, object> wr, Func<BinaryReader, object> rd) =>
            es.Add(new OpEntry(FamilyV2, disc, t, wr, rd));

        // Operand helpers (fixed 8-byte long for operand integers / register ids).
        static long L(BinaryReader r) => r.ReadInt64();
        static bool B(BinaryReader r) => r.ReadBoolean();
        static string S(BinaryReader r) => ByteIo.ReadString(r);
        static object? C(BinaryReader r) => ConstantCodec.Read(r);

        // ── Family 0x01 — v1 IOp ─────────────────────────────────────────────
        V1(0x00, typeof(Bc.ClauseTry), (w, o) => { }, r => new Bc.ClauseTry());
        V1(0x01, typeof(Bc.GuardFail), (w, o) => { }, r => new Bc.GuardFail());
        V1(0x02, typeof(Bc.Commit), (w, o) => { }, r => new Bc.Commit());
        V1(0x03, typeof(Bc.ClauseNext),
            (w, o) => ByteIo.WriteString(w, ((Bc.ClauseNext)o).Label),
            r => new Bc.ClauseNext(S(r)));
        V1(0x04, typeof(Bc.TryNextClause), (w, o) => { }, r => new Bc.TryNextClause());
        V1(0x05, typeof(Bc.NoMoreClauses), (w, o) => { }, r => new Bc.NoMoreClauses());
#pragma warning disable CS0612 // obsolete opcodes are round-tripped exactly (A3)
        V1(0x06, typeof(Bc.UnionSiAndGoto),
            (w, o) => ByteIo.WriteString(w, ((Bc.UnionSiAndGoto)o).Label),
            r => new Bc.UnionSiAndGoto(S(r)));
        V1(0x07, typeof(Bc.ResetAndGoto),
            (w, o) => ByteIo.WriteString(w, ((Bc.ResetAndGoto)o).Label),
            r => new Bc.ResetAndGoto(S(r)));
#pragma warning restore CS0612
        V1(0x08, typeof(Bc.SuspendEnd), (w, o) => { }, r => new Bc.SuspendEnd());
        V1(0x09, typeof(Bc.Proceed), (w, o) => { }, r => new Bc.Proceed());
        V1(0x0A, typeof(Bc.BodySetConst),
            (w, o) => { var x = (Bc.BodySetConst)o; w.Write(x.WriterId); ConstantCodec.Write(w, x.Value); },
            r => { long wid = L(r); return new Bc.BodySetConst(wid, C(r)); });
        V1(0x0B, typeof(Bc.BodySetStructConstArgs),
            (w, o) =>
            {
                var x = (Bc.BodySetStructConstArgs)o;
                w.Write(x.WriterId);
                ByteIo.WriteString(w, x.Functor);
                ByteIo.WriteVarUInt(w, (ulong)x.ConstArgs.Count);
                foreach (var a in x.ConstArgs) ConstantCodec.Write(w, a);
            },
            r =>
            {
                long wid = L(r);
                string functor = S(r);
                int n = checked((int)ByteIo.ReadVarUInt(r));
                var args = new List<object?>(n);
                for (int i = 0; i < n; i++) args.Add(C(r));
                return new Bc.BodySetStructConstArgs(wid, functor, args);
            });
        V1(0x0C, typeof(Bc.PutConstant),
            (w, o) => { var x = (Bc.PutConstant)o; ConstantCodec.Write(w, x.Value); w.Write(x.ArgSlot); },
            r => { object? v = C(r); return new Bc.PutConstant(v, L(r)); });
        V1(0x0D, typeof(Bc.PutStructure),
            (w, o) => { var x = (Bc.PutStructure)o; ByteIo.WriteString(w, x.Functor); w.Write(x.Arity); w.Write(x.ArgSlot); },
            r => { string f = S(r); long ar = L(r); return new Bc.PutStructure(f, ar, L(r)); });
        V1(0x0E, typeof(Bc.SetConstant),
            (w, o) => ConstantCodec.Write(w, ((Bc.SetConstant)o).Value),
            r => new Bc.SetConstant(C(r)));
        V1(0x0F, typeof(Bc.PutNil),
            (w, o) => w.Write(((Bc.PutNil)o).ArgSlot),
            r => new Bc.PutNil(L(r)));
        V1(0x10, typeof(Bc.PutList),
            (w, o) => w.Write(((Bc.PutList)o).ArgSlot),
            r => new Bc.PutList(L(r)));
        V1(0x11, typeof(Bc.PutBoundConst),
            (w, o) => { var x = (Bc.PutBoundConst)o; ConstantCodec.Write(w, x.Value); w.Write(x.ArgSlot); },
            r => { object? v = C(r); return new Bc.PutBoundConst(v, L(r)); });
        V1(0x12, typeof(Bc.PutBoundNil),
            (w, o) => w.Write(((Bc.PutBoundNil)o).ArgSlot),
            r => new Bc.PutBoundNil(L(r)));
        V1(0x13, typeof(Bc.HeadConstant),
            (w, o) => { var x = (Bc.HeadConstant)o; ConstantCodec.Write(w, x.Value); w.Write(x.ArgSlot); },
            r => { object? v = C(r); return new Bc.HeadConstant(v, L(r)); });
        V1(0x14, typeof(Bc.HeadStructure),
            (w, o) => { var x = (Bc.HeadStructure)o; ByteIo.WriteString(w, x.Functor); w.Write(x.Arity); w.Write(x.ArgSlot); },
            r => { string f = S(r); long ar = L(r); return new Bc.HeadStructure(f, ar, L(r)); });
        V1(0x15, typeof(Bc.UnifyConstant),
            (w, o) => ConstantCodec.Write(w, ((Bc.UnifyConstant)o).Value),
            r => new Bc.UnifyConstant(C(r)));
        V1(0x16, typeof(Bc.HeadNil),
            (w, o) => w.Write(((Bc.HeadNil)o).ArgSlot),
            r => new Bc.HeadNil(L(r)));
        V1(0x17, typeof(Bc.HeadList),
            (w, o) => w.Write(((Bc.HeadList)o).ArgSlot),
            r => new Bc.HeadList(L(r)));
        V1(0x18, typeof(Bc.UnifyVoid),
            (w, o) => w.Write(((Bc.UnifyVoid)o).Count),
            r => new Bc.UnifyVoid(L(r)));
        V1(0x19, typeof(Bc.GetVariable),
            (w, o) => { var x = (Bc.GetVariable)o; w.Write(x.VarIndex); w.Write(x.ArgSlot); },
            r => { long vi = L(r); return new Bc.GetVariable(vi, L(r)); });
        V1(0x1A, typeof(Bc.GetValue),
            (w, o) => { var x = (Bc.GetValue)o; w.Write(x.VarIndex); w.Write(x.ArgSlot); },
            r => { long vi = L(r); return new Bc.GetValue(vi, L(r)); });
        V1(0x1B, typeof(Bc.Otherwise), (w, o) => { }, r => new Bc.Otherwise());
        V1(0x1C, typeof(Bc.Push),
            (w, o) => w.Write(((Bc.Push)o).RegIndex),
            r => new Bc.Push(L(r)));
        V1(0x1D, typeof(Bc.Pop),
            (w, o) => w.Write(((Bc.Pop)o).RegIndex),
            r => new Bc.Pop(L(r)));
        V1(0x1E, typeof(Bc.UnifyStructure),
            (w, o) => { var x = (Bc.UnifyStructure)o; ByteIo.WriteString(w, x.Functor); w.Write(x.Arity); },
            r => { string f = S(r); return new Bc.UnifyStructure(f, L(r)); });
        V1(0x1F, typeof(Bc.Guard),
            (w, o) => { var x = (Bc.Guard)o; ByteIo.WriteString(w, x.ProcedureLabel); w.Write(x.Arity); w.Write(x.Negated); },
            r => { string pl = S(r); long ar = L(r); return new Bc.Guard(pl, ar, B(r)); });
        V1(0x20, typeof(Bc.Ground),
            (w, o) => { var x = (Bc.Ground)o; w.Write(x.VarIndex); w.Write(x.Negated); },
            r => { long vi = L(r); return new Bc.Ground(vi, B(r)); });
        V1(0x21, typeof(Bc.Known),
            (w, o) => { var x = (Bc.Known)o; w.Write(x.VarIndex); w.Write(x.Negated); },
            r => { long vi = L(r); return new Bc.Known(vi, B(r)); });
        V1(0x22, typeof(Bc.NoReaders),
            (w, o) => { var x = (Bc.NoReaders)o; w.Write(x.VarIndex); w.Write(x.Negated); },
            r => { long vi = L(r); return new Bc.NoReaders(vi, B(r)); });
        V1(0x23, typeof(Bc.GroundEqual),
            (w, o) => { var x = (Bc.GroundEqual)o; w.Write(x.LeftVarIndex); w.Write(x.RightVarIndex); w.Write(x.Negated); },
            r => { long lv = L(r); long rv = L(r); return new Bc.GroundEqual(lv, rv, B(r)); });
        V1(0x24, typeof(Bc.HeadBindWriter),
            (w, o) => w.Write(((Bc.HeadBindWriter)o).WriterId),
            r => new Bc.HeadBindWriter(L(r)));
        V1(0x25, typeof(Bc.GuardNeedReader),
            (w, o) => w.Write(((Bc.GuardNeedReader)o).ReaderId),
            r => new Bc.GuardNeedReader(L(r)));
        V1(0x26, typeof(Bc.RequireWriterArg),
            (w, o) => { var x = (Bc.RequireWriterArg)o; w.Write(x.Slot); ByteIo.WriteString(w, x.FailLabel); },
            r => { long sl = L(r); return new Bc.RequireWriterArg(sl, S(r)); });
        V1(0x27, typeof(Bc.RequireReaderArg),
            (w, o) => { var x = (Bc.RequireReaderArg)o; w.Write(x.Slot); ByteIo.WriteString(w, x.FailLabel); },
            r => { long sl = L(r); return new Bc.RequireReaderArg(sl, S(r)); });
        V1(0x28, typeof(Bc.HeadBindWriterArg),
            (w, o) => w.Write(((Bc.HeadBindWriterArg)o).Slot),
            r => new Bc.HeadBindWriterArg(L(r)));
        V1(0x29, typeof(Bc.GuardNeedReaderArg),
            (w, o) => w.Write(((Bc.GuardNeedReaderArg)o).Slot),
            r => new Bc.GuardNeedReaderArg(L(r)));
        V1(0x2A, typeof(Bc.BodySetConstArg),
            (w, o) => { var x = (Bc.BodySetConstArg)o; w.Write(x.Slot); ConstantCodec.Write(w, x.Value); },
            r => { long sl = L(r); return new Bc.BodySetConstArg(sl, C(r)); });
        V1(0x2B, typeof(Bc.TailStep),
            (w, o) => ByteIo.WriteString(w, ((Bc.TailStep)o).Label),
            r => new Bc.TailStep(S(r)));
        V1(0x2C, typeof(Bc.Spawn),
            (w, o) => { var x = (Bc.Spawn)o; ByteIo.WriteString(w, x.ProcedureLabel); w.Write(x.Arity); },
            r => { string pl = S(r); return new Bc.Spawn(pl, L(r)); });
        V1(0x2D, typeof(Bc.Requeue),
            (w, o) => { var x = (Bc.Requeue)o; ByteIo.WriteString(w, x.ProcedureLabel); w.Write(x.Arity); },
            r => { string pl = S(r); return new Bc.Requeue(pl, L(r)); });
        V1(0x2E, typeof(Bc.Allocate),
            (w, o) => w.Write(((Bc.Allocate)o).Slots),
            r => new Bc.Allocate(L(r)));
        V1(0x2F, typeof(Bc.Deallocate), (w, o) => { }, r => new Bc.Deallocate());
        V1(0x30, typeof(Bc.Nop), (w, o) => { }, r => new Bc.Nop());
        V1(0x31, typeof(Bc.Halt), (w, o) => { }, r => new Bc.Halt());
        V1(0x32, typeof(Bc.Distribute),
            (w, o) => { var x = (Bc.Distribute)o; w.Write(x.ImportIndex); ByteIo.WriteString(w, x.Functor); w.Write(x.Arity); },
            r => { long ii = L(r); string f = S(r); return new Bc.Distribute(ii, f, L(r)); });
        V1(0x33, typeof(Bc.Transmit),
            (w, o) => { var x = (Bc.Transmit)o; w.Write(x.ModuleVarIndex); ByteIo.WriteString(w, x.Functor); w.Write(x.Arity); },
            r => { long mv = L(r); string f = S(r); return new Bc.Transmit(mv, f, L(r)); });

        // ── Family 0x02 — v2 IOpV2 (each carries IsReader, except Unknown) ────
        V2e(0x00, typeof(V2.HeadVariable),
            (w, o) => { var x = (V2.HeadVariable)o; w.Write(x.VarIndex); w.Write(x.IsReader); },
            r => { long vi = L(r); return new V2.HeadVariable(vi, B(r)); });
        V2e(0x01, typeof(V2.GetVariable),
            (w, o) => { var x = (V2.GetVariable)o; w.Write(x.VarIndex); w.Write(x.ArgSlot); w.Write(x.IsReader); },
            r => { long vi = L(r); long sl = L(r); return new V2.GetVariable(vi, sl, B(r)); });
        V2e(0x02, typeof(V2.GetValue),
            (w, o) => { var x = (V2.GetValue)o; w.Write(x.VarIndex); w.Write(x.ArgSlot); w.Write(x.IsReader); },
            r => { long vi = L(r); long sl = L(r); return new V2.GetValue(vi, sl, B(r)); });
        V2e(0x03, typeof(V2.UnifyVariable),
            (w, o) => { var x = (V2.UnifyVariable)o; w.Write(x.VarIndex); w.Write(x.IsReader); },
            r => { long vi = L(r); return new V2.UnifyVariable(vi, B(r)); });
        V2e(0x04, typeof(V2.PutVariable),
            (w, o) => { var x = (V2.PutVariable)o; w.Write(x.VarIndex); w.Write(x.ArgSlot); w.Write(x.IsReader); },
            r => { long vi = L(r); long sl = L(r); return new V2.PutVariable(vi, sl, B(r)); });
        V2e(0x05, typeof(V2.SetVariable),
            (w, o) => { var x = (V2.SetVariable)o; w.Write(x.VarIndex); w.Write(x.IsReader); },
            r => { long vi = L(r); return new V2.SetVariable(vi, B(r)); });
        V2e(0x06, typeof(V2.Unknown),
            (w, o) => w.Write(((V2.Unknown)o).VarIndex),
            r => new V2.Unknown(L(r)));

        // ── Family 0x03 — Label marker ───────────────────────────────────────
        es.Add(new OpEntry(FamilyLabel, 0x00, typeof(Bc.Label),
            (w, o) => ByteIo.WriteString(w, ((Bc.Label)o).Name),
            r => new Bc.Label(S(r))));

        return es;
    }
}

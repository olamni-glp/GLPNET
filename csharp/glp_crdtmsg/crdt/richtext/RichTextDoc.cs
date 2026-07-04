// Rich-text CRDT document (feature 041-crdtmsg-mvp) — Fugue sequence + Peritext marks over the store.
//
// This is the MANDATORY demonstrator (Gabi's clarification #2): the op-based JSON-CRDT base is only the
// substrate; the interoperable deliverable is Fugue+Peritext rich text. Op bodies are GROUND terms
// (encoded with the shipped Section-15 TermCodec) validated at apply time (TermGuards — ground + acyclic,
// FR-023/031, transport faults not GLP Fail). Both structures are order-independent CRDTs, so folding the
// store's canonical op order (Projection, T023) converges (SC-012/013).

using GlpRuntime.CrdtMsg.Crdt.RichText;
using GlpRuntime.CrdtMsg.Store;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.CrdtMsg.Crdt;

public sealed class RichTextDoc
{
    private readonly FugueTree _seq = new();
    private readonly MarkSet _marks = new();

    public string Text() => _seq.Text();
    public IReadOnlyList<FugueNode> Elements() => _seq.Visible();
    public IEnumerable<Mark> ActiveMarks() => _marks.Active;

    // --- apply (used by the projection) ---

    public void Apply(Op op)
    {
        Term body = DecodeBody(op.Payload);
        TermGuards.ValidateOpPayload(body); // ground + acyclic; transport fault on violation

        if (body is not StructTerm s)
            throw new Envelope.CrdtMsgException("rich-text op body must be a compound term");

        switch (s.Functor)
        {
            case "seq_insert":
            {
                Dot? parent = ParseParent(s.Args[0]);
                FugueSide side = Atom(s.Args[1]) == "l" ? FugueSide.Left : FugueSide.Right;
                _seq.Integrate(op.Id, parent, side, Str(s.Args[2]));
                break;
            }
            case "seq_delete":
                _seq.Delete(Dot.Parse(Str(s.Args[0])));
                break;
            case "mark_add":
                _marks.Add(new Mark(op.Id, Str(s.Args[0]), Anchor(s.Args[1]), Anchor(s.Args[2]), Str(s.Args[3])));
                break;
            case "mark_remove":
                _marks.Remove(Dot.Parse(Str(s.Args[0])));
                break;
            default:
                throw new Envelope.CrdtMsgException($"unknown rich-text op functor '{s.Functor}'");
        }
    }

    /// <summary>Projection over the store: fold ops in canonical causal order into a fresh doc.</summary>
    public static Projection<RichTextDoc> Projection() =>
        new(() => new RichTextDoc(), (doc, op) => { doc.Apply(op); return doc; });

    public static RichTextDoc Rebuild(IReadOnlyCollection<Op> ops) => Projection().Rebuild(ops);

    // --- op builders (an author computes an op from local state, then broadcasts it) ---

    /// <summary>Build a seq-insert op placing <paramref name="value"/> at visible <paramref name="index"/>.</summary>
    public Op MakeInsertAt(Dot dot, int index, string value)
    {
        var vis = _seq.Visible();
        Dot? left = index > 0 ? vis[index - 1].Id : (Dot?)null;
        Dot? right = index < vis.Count ? vis[index].Id : (Dot?)null;
        var (parent, side) = _seq.Place(left, right);
        var body = new StructTerm("seq_insert", new Term[]
        {
            parent is null ? Atomic("root") : Stringy(parent.Value.ToString()),
            Atomic(side == FugueSide.Left ? "l" : "r"),
            Stringy(value),
        });
        var deps = parent is null ? Array.Empty<Dot>() : new[] { parent.Value };
        return Op.Create(dot, deps, EncodeBody(body));
    }

    public Op MakeDelete(Dot dot, Dot targetElem)
    {
        var body = new StructTerm("seq_delete", new Term[] { Stringy(targetElem.ToString()) });
        return Op.Create(dot, new[] { targetElem }, EncodeBody(body));
    }

    public Op MakeMarkAdd(Dot dot, string type, Anchor start, Anchor end, string value)
    {
        var body = new StructTerm("mark_add", new Term[]
        {
            Stringy(type), AnchorTerm(start), AnchorTerm(end), Stringy(value),
        });
        return Op.Create(dot, new[] { start.ElemId, end.ElemId }, EncodeBody(body));
    }

    public Op MakeMarkRemove(Dot dot, Dot targetMark)
    {
        var body = new StructTerm("mark_remove", new Term[] { Stringy(targetMark.ToString()) });
        return Op.Create(dot, new[] { targetMark }, EncodeBody(body));
    }

    // --- term helpers (all ground) ---

    private static Term Stringy(string s) => new ConstTerm(new ConstString(s));
    private static Term Atomic(string a) => new ConstTerm(new ConstAtom(a));

    private static Term AnchorTerm(Anchor a) => new StructTerm("anchor", new Term[]
    {
        Stringy(a.ElemId.ToString()),
        Atomic(a.Side == AnchorSide.Before ? "before" : "after"),
    });

    private static Dot? ParseParent(Term t) =>
        t is ConstTerm { Value: ConstAtom { Name: "root" } } ? null : Dot.Parse(Str(t));

    private static Anchor Anchor(Term t)
    {
        if (t is not StructTerm { Functor: "anchor" } s)
            throw new Envelope.CrdtMsgException("malformed anchor term");
        var side = Atom(s.Args[1]) == "before" ? AnchorSide.Before : AnchorSide.After;
        return new Anchor(Dot.Parse(Str(s.Args[0])), side);
    }

    private static string Str(Term t) =>
        t is ConstTerm { Value: ConstString cs } ? cs.Value
        : throw new Envelope.CrdtMsgException("expected a string term");

    private static string Atom(Term t) =>
        t is ConstTerm { Value: ConstAtom ca } ? ca.Name
        : throw new Envelope.CrdtMsgException("expected an atom term");

    private static byte[] EncodeBody(Term t)
    {
        var w = new ByteWriter();
        TermCodec.EncodeTerm(w, t);
        return w.TakeBytes();
    }

    private static Term DecodeBody(byte[] bytes)
    {
        var r = new ByteReader(bytes);
        Term t = TermCodec.DecodeTerm(r);
        if (!r.AtEnd)
            throw new Envelope.CrdtMsgException("trailing bytes after op body term");
        return t;
    }
}

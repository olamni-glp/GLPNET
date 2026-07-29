// crdtmsg payload codec — the "quic" link's L5 wire surface (feature 050 US2, T017).
//
// Contract wire-payload-crdtmsg.md + addendum A1/A2/A3 (2026-07-10): the host-side IPayloadCodec the
// composition root installs for the "quic" scheme (research D-1 cycle resolution — the concrete codec
// lives here in glp_crdtmsg, which already references glp_link; glp_link stays codec-agnostic). It
// bridges a GLP ground message tree to a 041 crdtmsg envelope and back, LOSSLESSLY and fail-closed:
//
//   Encode(Term): a `crdtmsg/7` ground term → Message → MessageCodec.Canonical (binary/canonical surface).
//   Decode(bytes): MessageCodec.Decode(binary, bytes, understood) → rebuild the `crdtmsg/7` ground term.
//
// Option A (Gabi, 2026-07-10): the GLP program owns the envelope structure — the codec is a near-
// mechanical, lossless transcription, so "is this a valid envelope on the wire?" is a program-emitted
// property (SC-002), never link-synthesized header guesswork. Rich-text is CARRIED not APPLIED (A3):
// an edit op rides as opaque 041 OpCodec bytes in a section and survives verbatim (incl. marks the link
// never reads); the link runs no Fugue/Peritext — that is the endpoint's job (US4).
//
// Ground-term grammar (A1):
//   crdtmsg(MsgId, From, To, Seq, policy(Targets, Waypoints, Excludes), CrdtModel, Sections)
//     MsgId/From/To : atom/string        Seq : integer
//     policy lists  : list of atoms      CrdtModel : atom in {none, state_based, op_based}
//     Sections      : list of section(Type:int, Bytes:list of int 0..255)
//   GLP list = StructTerm(".", [H, T]) terminated by ConstTerm("nil") (025 convention).
//   Codec-fixed (not in the term): schema_version = EmitSchemaVersion; payload_type = CrdtMessage.
//
// Capability slot (feature 050 US3, T026/T027 — research D-2 RESOLVED): when constructed with a
// MacaroonLinkGate, the capability is codec-fixed carriage, never term-visible — Encode attaches the
// gate's presented static macaroon as the 041 CapabilitySlot TLV SECTION 0x20 (envelope v2,
// additive-optional; the binary canonical surface carries TLV sections verbatim, so NO 041 codec
// change and NO JSON stopgap is needed — Header.CapabilitySlot stays null on the wire); Decode
// verify-before-acts the inbound slot via the gate (record + strip on success; record Refused +
// CapabilityRefusedException on failure, FR-009). Without a gate the US2 behaviour is unchanged.

using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Headers;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.CrdtMsg.Route;
using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.CrdtMsg.Bridge;

/// <summary>The <c>"quic"</c> link's <see cref="IPayloadCodec"/>: <c>crdtmsg/7</c> ground term ↔ 041 envelope.</summary>
public sealed class CrdtMsgPayloadCodec : IPayloadCodec
{
    private const string MsgFunctor = "crdtmsg";
    private const int MsgArity = 7;
    private const string PolicyFunctor = "policy";
    private const string SectionFunctor = "section";
    private const string Cons = ".";
    private const string Nil = "nil";

    /// <summary>The must-understand (odd) section types the link CARRIES verbatim — carriage, not apply
    /// (addendum A3): the reserved CRDT op section (<see cref="UnifiedHeader.OpSectionType"/>), the
    /// 057 E3PC control section (<see cref="E3pcCtrlCodec.SectionType"/>), so an E3PC control frame can
    /// ride a "quic" link without the codec loud-rejecting it (codexreview 20260713T110357Z), and the
    /// 058 S4 policy request/reply sections (<see cref="Policy.PolicyWire.RequestSectionType"/> /
    /// <see cref="Policy.PolicyWire.ReplySectionType"/>) for the same reason. Any OTHER
    /// odd/must-understand section the codec does not understand still fails loud on decode (FR-005).
    /// Even/ignorable sections are always carried verbatim by the TLV codec.</summary>
    private static readonly IReadOnlySet<long> Understood = new HashSet<long>
    {
        UnifiedHeader.OpSectionType,
        E3pcCtrlCodec.SectionType,
        Policy.PolicyWire.RequestSectionType,
        Policy.PolicyWire.ReplySectionType,
    };

    private readonly MacaroonLinkGate? _gate;

    /// <summary>Ungated (US2) codec: no capability slot attached or required.</summary>
    public CrdtMsgPayloadCodec() { }

    /// <summary>Capability-gated codec (US3): outbound envelopes carry the gate's presented static
    /// macaroon in the slot section <c>0x20</c>; inbound envelopes are verify-before-act gated.</summary>
    public CrdtMsgPayloadCodec(MacaroonLinkGate? gate) => _gate = gate;

    /// <inheritdoc/>
    public byte[] Encode(Term ground)
    {
        Message msg = ToMessage(ground);
        if (_gate?.PresentedCapability is byte[] capability)
            msg = CapabilitySlot.Attach(msg, capability);   // stamps SchemaVersion = 2 (additive-optional)
        return MessageCodec.Canonical(msg);
    }

    /// <inheritdoc/>
    public Term Decode(byte[] payload)
    {
        Message msg = MessageCodec.Decode(MessageCodec.Binary, payload, Understood);
        if (_gate is not null)
            msg = _gate.VerifyInbound(msg);                 // record + strip, or record Refused + throw
        return ToTerm(msg);
    }

    // ---------------------------------------------------------------- Term → Message

    private static Message ToMessage(Term ground)
    {
        if (ground is not StructTerm s || s.Functor != MsgFunctor || s.Args.Count != MsgArity)
            throw new CrdtMsgException(
                $"a \"quic\" link payload must be a {MsgFunctor}/{MsgArity} ground term (FR-005), got {Describe(ground)}");

        var header = new Header(
            AtomOf(s.Args[0], "msg_id"),
            AtomOf(s.Args[1], "from"),
            AtomOf(s.Args[2], "to"),
            IntOf(s.Args[3], "seq"),
            PolicyOf(s.Args[4]));
        return new Message(
            VersionPolicy.EmitSchemaVersion, PayloadType.CrdtMessage, header,
            SectionsOf(s.Args[6]), CrdtModelOf(s.Args[5]));
    }

    private static RoutingPolicy PolicyOf(Term t)
    {
        if (t is not StructTerm p || p.Functor != PolicyFunctor || p.Args.Count != 3)
            throw new CrdtMsgException($"crdtmsg arg 5 must be {PolicyFunctor}/3, got {Describe(t)}");
        return new RoutingPolicy(AtomListOf(p.Args[0]), AtomListOf(p.Args[1]), AtomListOf(p.Args[2]));
    }

    private static List<Section> SectionsOf(Term t)
    {
        var sections = new List<Section>();
        foreach (var e in Elements(t, "sections"))
        {
            if (e is not StructTerm sec || sec.Functor != SectionFunctor || sec.Args.Count != 2)
                throw new CrdtMsgException($"each section must be {SectionFunctor}/2, got {Describe(e)}");
            long type = IntOf(sec.Args[0], "section type");
            // The capability slot (section 0x20) is codec-owned carriage, NEVER term-visible (review-fix,
            // contract wire-payload-crdtmsg.md): a GLP program supplying its own section(0x20) would be
            // appended BEFORE the real slot and, since the receiver extracts the FIRST 0x20, would shadow
            // the genuine capability and refuse an otherwise-valid message. Fail closed loudly (FR-005).
            if (type == CapabilitySlot.SectionType)
                throw new CrdtMsgException(
                    $"section type 0x{CapabilitySlot.SectionType:X} is the reserved capability slot — "
                    + "codec-owned carriage, never term-visible (FR-005)");
            sections.Add(new Section(type, BytesOf(sec.Args[1])));
        }
        return sections;
    }

    private static byte[] BytesOf(Term t)
    {
        var bytes = new List<byte>();
        foreach (var e in Elements(t, "section value"))
        {
            long b = IntOf(e, "section byte");
            if (b < 0 || b > 255)
                throw new CrdtMsgException($"section byte {b} is out of range 0..255");
            bytes.Add((byte)b);
        }
        return bytes.ToArray();
    }

    private static List<string> AtomListOf(Term t)
    {
        var xs = new List<string>();
        foreach (var e in Elements(t, "policy list"))
            xs.Add(AtomOf(e, "policy entry"));
        return xs;
    }

    private static CrdtModel CrdtModelOf(Term t) => AtomOf(t, "crdt_model") switch
    {
        "none" => CrdtModel.None,
        "state_based" => CrdtModel.StateBased,
        "op_based" => CrdtModel.OpBased,
        var other => throw new CrdtMsgException(
            $"crdt_model must be one of {{none, state_based, op_based}}, got '{other}'"),
    };

    // ---------------------------------------------------------------- Message → Term

    private static Term ToTerm(Message m) => new StructTerm(MsgFunctor, new Term[]
    {
        new ConstTerm(m.Header.MsgId),
        new ConstTerm(m.Header.From),
        new ConstTerm(m.Header.To),
        new ConstTerm(m.Header.Seq),
        new StructTerm(PolicyFunctor, new Term[]
        {
            AtomListTerm(m.Header.Policy.Targets),
            AtomListTerm(m.Header.Policy.Waypoints),
            AtomListTerm(m.Header.Policy.Excludes),
        }),
        new ConstTerm(ModelAtom(m.CrdtModel)),
        ListTerm(m.Sections.Select(SectionTerm)),
    });

    private static Term SectionTerm(Section s) => new StructTerm(SectionFunctor, new Term[]
    {
        new ConstTerm(s.TypeNumber),
        ListTerm(s.Value.Select(b => (Term)new ConstTerm((long)b))),
    });

    private static Term AtomListTerm(IReadOnlyList<string> xs) =>
        ListTerm(xs.Select(x => (Term)new ConstTerm(x)));

    private static string ModelAtom(CrdtModel m) => m switch
    {
        CrdtModel.None => "none",
        CrdtModel.StateBased => "state_based",
        CrdtModel.OpBased => "op_based",
        _ => throw new CrdtMsgException($"unknown crdt_model {(byte)m}"),
    };

    // ---------------------------------------------------------------- shared term helpers

    /// <summary>Walk a ground GLP list (<c>.</c>/2 spine ending in <c>nil</c>) yielding each element;
    /// loud-fail on a malformed spine (not a cons and not nil).</summary>
    private static IEnumerable<Term> Elements(Term t, string what)
    {
        Term cur = t;
        while (true)
        {
            switch (cur)
            {
                case ConstTerm c when Equals(c.Value, Nil):
                    yield break;
                case StructTerm s when s.Functor == Cons && s.Args.Count == 2:
                    yield return s.Args[0];
                    cur = s.Args[1];
                    break;
                default:
                    throw new CrdtMsgException($"malformed {what} list near {Describe(cur)}");
            }
        }
    }

    private static Term ListTerm(IEnumerable<Term> items)
    {
        Term acc = new ConstTerm(Nil);
        foreach (var item in items.Reverse())
            acc = new StructTerm(Cons, new Term[] { item, acc });
        return acc;
    }

    private static string AtomOf(Term t, string field) => t is ConstTerm c && c.Value is string s
        ? s
        : throw new CrdtMsgException($"{field} must be an atom/string, got {Describe(t)}");

    private static long IntOf(Term t, string field) => t is ConstTerm c
        ? c.Value switch
        {
            long l => l,
            int i => i,
            _ => throw new CrdtMsgException($"{field} must be an integer, got {Describe(t)}"),
        }
        : throw new CrdtMsgException($"{field} must be an integer, got {Describe(t)}");

    private static string Describe(Term t) => t switch
    {
        ConstTerm c => $"const({c.Value ?? "null"})",
        StructTerm s => $"{s.Functor}/{s.Args.Count}",
        _ => t.GetType().Name,
    };
}

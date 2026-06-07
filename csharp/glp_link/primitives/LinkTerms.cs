using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// Maps between GLP ground terms and the host link value-types. The GLP surface is
/// <c>LinkId ::= link_id(Scheme, Endpoint, Nonce)</c>,
/// <c>Endpoint ::= String ; ep(String, Integer)</c>,
/// <c>Nonce ::= Integer ; String</c>, <c>Role ∈ {listener, connector}</c>, and the
/// fault lattice <c>ok | closed(LinkId,Reason) | tempFail(LinkId,Reason) |
/// permFail(LinkId,Reason)</c> (contracts/link-primitives.md §1, rulings-log
/// "fault term closed(LinkId,Reason); graceful reason eos").
/// </summary>
/// <remarks>
/// Parsing expects already-dereferenced GROUND terms — the GLP wrappers guard
/// <c>ground/1</c> before the kernel runs, so a malformed/unbound term reaching
/// here is a caller bug (it throws) rather than something to tolerate (CLAUDE.md
/// "robustness is a workaround in disguise").
/// </remarks>
public static class LinkTerms
{
    private const string LinkIdFunctor = "link_id";
    private const string EndpointFunctor = "ep";
    private const string RequestFunctor = "request";
    public const string GracefulReason = "eos";
    public const string AbruptReason = "abrupt";

    // ---- parse: term → host value ----

    /// <summary>Parse a ground <c>link_id(Scheme, Endpoint, Nonce)</c> term.</summary>
    public static LinkId ParseLinkId(Term term)
    {
        if (term is not StructTerm st || st.Functor != LinkIdFunctor || st.Args.Count != 3)
            throw new ArgumentException($"expected link_id/3 struct, got {Describe(term)}");
        var scheme = LinkScheme.Of(ConstString(st.Args[0], "LinkId.Scheme"));
        var endpoint = ParseEndpoint(st.Args[1]);
        var nonce = ParseNonce(st.Args[2]);
        return new LinkId(scheme, endpoint, nonce);
    }

    /// <summary>Parse a ground <c>Scheme</c> string (the first component of <c>rendezvous(Scheme, Endpoint)</c>).</summary>
    public static LinkScheme ParseScheme(Term term) => LinkScheme.Of(ConstString(term, "Scheme"));

    /// <summary>Parse a ground <c>Endpoint ::= String ; ep(String, Integer)</c> term (used by both LinkId and rendezvous).</summary>
    public static LinkAddress ParseEndpoint(Term term) => term switch
    {
        StructTerm ep when ep.Functor == EndpointFunctor && ep.Args.Count == 2 =>
            LinkAddress.Endpoint(ConstString(ep.Args[0], "ep host"), checked((int)ConstLong(ep.Args[1], "ep port"))),
        _ => LinkAddress.Path(ConstString(term, "Endpoint")),
    };

    private static LinkNonce ParseNonce(Term term)
    {
        var v = Const(term, "Nonce");
        return v switch
        {
            long l => LinkNonce.Int(l),
            int i => LinkNonce.Int(i),
            string s => LinkNonce.Str(s),
            _ => throw new ArgumentException($"Nonce must be Integer or String, got {v?.GetType().Name ?? "null"}"),
        };
    }

    /// <summary>
    /// Parse the ground close <c>Reason</c> (<c>Reason ::= String</c>): the atom <c>abrupt</c>
    /// from <c>link_close/1</c>, or a user reason from <c>link_close/2</c>. The wrapper
    /// grounds it, so a non-constant reaching here is a caller bug (it throws).
    /// </summary>
    public static string ParseReason(Term term) => ConstString(term, "Reason");

    /// <summary>Parse the ground establishment role atom (<c>listener</c>/<c>connector</c>).</summary>
    public static LinkRole ParseRole(Term term)
    {
        var s = ConstString(term, "Role");
        return s switch
        {
            "listener" => LinkRole.Listener,
            "connector" => LinkRole.Connector,
            _ => throw new ArgumentException($"Role must be listener|connector, got '{s}'"),
        };
    }

    // ---- build: host value → term ----

    /// <summary>Build the GLP <c>link_id(Scheme, Endpoint, Nonce)</c> term.</summary>
    public static Term ToTerm(LinkId id) => new StructTerm(LinkIdFunctor, new Term[]
    {
        new ConstTerm(id.Scheme.Name),
        EndpointToTerm(id.Endpoint),
        NonceToTerm(id.Nonce),
    });

    private static Term EndpointToTerm(LinkAddress addr) => addr.Port is { } p
        ? new StructTerm(EndpointFunctor, new Term[] { new ConstTerm(addr.Host), new ConstTerm((long)p) })
        : new ConstTerm(addr.Host);

    private static Term NonceToTerm(LinkNonce nonce) =>
        nonce.IsInteger ? new ConstTerm(nonce.IntValue) : new ConstTerm(nonce.StringValue!);

    // ---- in-band request token (T033 path-B handshake, OQ-A3) ----

    /// <summary>Build the in-band <c>request(LinkId, FromPeer)</c> handshake token.</summary>
    public static Term RequestToken(LinkId id, Term fromPeer) =>
        new StructTerm(RequestFunctor, new Term[] { ToTerm(id), fromPeer });

    /// <summary>Parse a ground <c>request(LinkId, FromPeer)</c> token (the listener's in-band first frame).</summary>
    public static (LinkId Id, Term FromPeer) ParseRequestToken(Term term)
    {
        if (term is not StructTerm st || st.Functor != RequestFunctor || st.Args.Count != 2)
            throw new ArgumentException($"expected request/2 token, got {Describe(term)}");
        return (ParseLinkId(st.Args[0]), st.Args[1]);
    }

    // ---- fault lattice terms (FR-043/045; rulings-log fault vocab) ----

    public static Term Ok() => new ConstTerm("ok");
    public static Term Closed(LinkId id, string reason) => Fault2("closed", id, reason);
    public static Term TempFail(LinkId id, string reason) => Fault2("tempFail", id, reason);
    public static Term PermFail(LinkId id, string reason) => Fault2("permFail", id, reason);

    /// <summary>Map a seam-level <see cref="LinkFaultSignal"/> to its GLP monitor term.</summary>
    public static Term FromSignal(LinkFaultSignal s) => s.Kind switch
    {
        LinkFaultKind.Closed => Closed(s.Link, s.Reason),
        LinkFaultKind.Transient => TempFail(s.Link, s.Reason),
        LinkFaultKind.Permanent => PermFail(s.Link, s.Reason),
        _ => throw new ArgumentOutOfRangeException(nameof(s)),
    };

    private static Term Fault2(string functor, LinkId id, string reason) =>
        new StructTerm(functor, new Term[] { ToTerm(id), new ConstTerm(reason) });

    // ---- const extraction helpers ----

    private static object? Const(Term term, string what) => term switch
    {
        ConstTerm c => c.Value,
        VarRef => throw new ArgumentException($"{what}: unbound/unresolved VarRef — kernel must deep-deref before parsing"),
        _ => throw new ArgumentException($"{what}: expected an atomic constant, got {Describe(term)}"),
    };

    private static string ConstString(Term term, string what) =>
        Const(term, what) as string ?? throw new ArgumentException($"{what}: expected a String constant, got {Describe(term)}");

    private static long ConstLong(Term term, string what) => Const(term, what) switch
    {
        long l => l,
        int i => i,
        _ => throw new ArgumentException($"{what}: expected an Integer constant, got {Describe(term)}"),
    };

    private static string Describe(Term term) => term switch
    {
        ConstTerm c => $"const({c.Value ?? "null"})",
        StructTerm s => $"{s.Functor}/{s.Args.Count}",
        VarRef v => $"var@{v.Addr}",
        _ => term.GetType().Name,
    };
}

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

    /// <summary>
    /// Recursively dereference <paramref name="term"/> against the heap into a VarRef-free
    /// ground tree, so the <c>Parse*</c> helpers below see the bound constants at every depth.
    /// REQUIRED before parsing: the compiler represents a ground <c>link_id(Scheme, …)</c> with
    /// each struct ARG as a <see cref="VarRef"/> into a bound cell, so a shallow
    /// <c>heap.Dereference</c> resolves only the outer struct and leaves <c>Scheme</c>/<c>Endpoint</c>
    /// as VarRefs (the xUnit kernels passed ground <see cref="ConstTerm"/> args directly, hiding this).
    /// The GLP wrappers guard <c>ground/1</c>, so every nested cell is bound; an unbound cell at any
    /// depth is a caller bug → <see cref="ArgumentException"/> (the kernel aborts).
    /// </summary>
    public static Term GroundResolve(HeapFCP heap, Term term)
    {
        Term d = heap.Dereference(term);
        switch (d)
        {
            case ConstTerm c:
                return c;
            case StructTerm s:
                var args = new Term[s.Args.Count];
                for (int i = 0; i < s.Args.Count; i++)
                    args[i] = GroundResolve(heap, s.Args[i]);
                return new StructTerm(s.Functor, args);
            case VarRef v:
                throw new ArgumentException(
                    $"unbound cell {v.Addr} in a term expected ground (the ground/1 guard should have excluded it)");
            default:
                throw new ArgumentException($"cannot ground-resolve {d.GetType().Name}");
        }
    }

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

    /// <summary>
    /// Build the GLP <c>link_id(Scheme, Endpoint, Nonce)</c> term. String components
    /// (scheme, host, string nonce) are RE-QUOTED — the inverse of <see cref="Unquote"/> —
    /// so the reconstructed term is byte-identical to the GLP source literal
    /// <c>link_id("tcp", ep("127.0.0.1", 9100), 1)</c> the compiler produces (string
    /// constants carry their quotes). Without this, a path-B request token round-trip
    /// yields a bare <c>link_id(tcp, …)</c> that fails the <c>=?=</c> match in
    /// <c>accept_link</c> against the served (quoted) LinkId, and any program matching a
    /// <c>closed(link_id(…), …)</c> fault against a literal would likewise mismatch.
    /// </summary>
    public static Term ToTerm(LinkId id) => new StructTerm(LinkIdFunctor, new Term[]
    {
        new ConstTerm(Requote(id.Scheme.Name)),
        EndpointToTerm(id.Endpoint),
        NonceToTerm(id.Nonce),
    });

    private static Term EndpointToTerm(LinkAddress addr) => addr.Port is { } p
        ? new StructTerm(EndpointFunctor, new Term[] { new ConstTerm(Requote(addr.Host)), new ConstTerm((long)p) })
        : new ConstTerm(Requote(addr.Host));

    private static Term NonceToTerm(LinkNonce nonce) =>
        nonce.IsInteger ? new ConstTerm(nonce.IntValue) : new ConstTerm(Requote(nonce.StringValue!));

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
        ConstTerm c => c.Value is string s ? Unquote(s) : c.Value,
        VarRef => throw new ArgumentException($"{what}: unbound/unresolved VarRef — kernel must deep-deref before parsing"),
        _ => throw new ArgumentException($"{what}: expected an atomic constant, got {Describe(term)}"),
    };

    /// <summary>
    /// Strip the surrounding double-quotes a GLP STRING literal carries in its ConstTerm value.
    /// The compiler stores a string constant as <c>"value"</c> (quotes included) so the type
    /// checker can tell a String literal from a bare atom (parser.cs: "Wrap in quotes so type
    /// checker can distinguish strings from atoms"); a bare atom (e.g. <c>listener</c>,
    /// <c>abrupt</c>) carries no quotes. The kernels need the raw host value (e.g. the scheme
    /// token, the IPv4 host for <c>IPAddress.Parse</c>), so strip one surrounding pair if present.
    /// </summary>
    private static string Unquote(string s) =>
        s.Length >= 2 && s[0] == '"' && s[^1] == '"' ? s[1..^1] : s;

    /// <summary>Inverse of <see cref="Unquote"/>: wrap a raw host string value in the
    /// surrounding double-quotes a GLP STRING constant carries, so a term rebuilt from
    /// host data is structurally identical to the source literal (idempotent — a value
    /// that already carries quotes is left as-is).</summary>
    private static string Requote(string s) =>
        s.Length >= 2 && s[0] == '"' && s[^1] == '"' ? s : "\"" + s + "\"";

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

namespace GlpRuntime.Link.Seam;

/// <summary>
/// The per-establishment uniqueness nonce. Mirrors the GLP
/// <c>Nonce ::= Integer ; String</c> component
/// (contracts/link-primitives.md §1, line 56). The integer and string forms are
/// kept distinct (an integer <c>5</c> is NOT the string <c>"5"</c>) so the value
/// equality that backs idempotency-at-identity (FR-007) matches the GLP term
/// exactly rather than a lossy canonical text.
/// </summary>
public readonly record struct LinkNonce
{
    /// <summary>True when this is the <c>Integer</c> form; false for the <c>String</c> form.</summary>
    public bool IsInteger { get; }

    /// <summary>The integer value, valid only when <see cref="IsInteger"/>.</summary>
    public long IntValue { get; }

    /// <summary>The string value, valid only when <see cref="IsInteger"/> is false.</summary>
    public string? StringValue { get; }

    private LinkNonce(bool isInt, long i, string? s)
    {
        IsInteger = isInt;
        IntValue = i;
        StringValue = s;
    }

    public static LinkNonce Int(long value) => new(true, value, null);

    public static LinkNonce Str(string value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        return new LinkNonce(false, 0, value);
    }

    public override string ToString() => IsInteger ? IntValue.ToString() : StringValue!;
}

/// <summary>
/// The stable, never-reused link identity. Mirrors the GLP
/// <c>LinkId ::= link_id(Scheme, Endpoint, Nonce)</c>
/// (contracts/link-primitives.md §1, lines 51-56).
/// </summary>
/// <remarks>
/// Value equality (record) is the idempotency-at-identity basis (FR-007): a
/// re-setup with the same <see cref="LinkId"/> must return the already-established
/// link rather than open a conflicting duplicate, so <see cref="LinkId"/> keys the
/// per-instance LinkId→handle registry built in T030. The components are all
/// ground (the GLP wrappers guard <c>ground/1</c> before the host sees them), so a
/// <see cref="LinkId"/> is always fully determined.
/// </remarks>
public sealed record LinkId(LinkScheme Scheme, LinkAddress Endpoint, LinkNonce Nonce)
{
    public override string ToString() => $"link_id({Scheme}, {Endpoint}, {Nonce})";
}

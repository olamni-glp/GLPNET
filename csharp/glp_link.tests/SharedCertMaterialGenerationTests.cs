// Feature 109 — revoked trust material is refused at load.
//
// These tests exercise SharedCertMaterial.AssertPinIsTrusted DIRECTLY rather than through Load().
// That is deliberate and is the point of the extraction: driving the revoked branch through Load()
// would require gen-1's PRIVATE KEY in the tree, and reintroducing that key is the one thing this
// feature exists to prevent. The rule is pure, so it can be proven without the material.
//
// FR-009 / SC-004 require BOTH a positive and a negative control. A positive control alone passes
// against a guard that refuses everything; a negative control alone passes against a guard that
// refuses nothing. Neither is evidence by itself.

using GlpRuntime.Link.Transports;

namespace GlpRuntime.Link.Tests;

public sealed class SharedCertMaterialGenerationTests
{
    private const string RevokedGen1 = "0LOmLNM0HYv79Rkoasuu6L4MKGRyg7axgJufbZBcyTo=";
    private const string FpPath = "/fake/glpquick-cert/glpquick.fingerprint";

    // ---- POSITIVE CONTROL (FR-001/FR-003, SC-001) --------------------------------------------
    // Proves the guard FIRES. T018 mutation-checks this test by neutering the guard.
    [Fact]
    public void RevokedPin_IsRefused()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => SharedCertMaterial.AssertPinIsTrusted(RevokedGen1, FpPath));

        Assert.Contains("REVOKED", ex.Message);
    }

    // ---- NEGATIVE CONTROL (SC-002/SC-004) ----------------------------------------------------
    // Proves the guard does NOT over-fire. T019 mutation-checks this test by pointing CurrentPin
    // at the revoked value — which is also the guard against "the guard's own constant is wrong".
    [Fact]
    public void CurrentPin_IsAccepted()
    {
        // Independently specified, NOT SharedCertMaterial.CurrentPin. Passing the production
        // constant into a function that compares against that same constant is tautological:
        // codexreview 2026-09-07 [P2] found exactly that, and it was right — setting CurrentPin to
        // an arbitrary wrong non-revoked value left all nine of these tests green. Sourcing the
        // expected value independently is what makes this a control at all.
        SharedCertMaterial.AssertPinIsTrusted(QuicRegistrationTests.ExpectedGen3Pin, FpPath);
    }

    /// <summary>
    /// The production constant must equal the independently-specified expected pin. This is the
    /// test that actually catches a wrong <c>CurrentPin</c>, and unlike the real-material test it
    /// needs no provisioned certificate — so it fires on cert-less CI too, which is precisely the
    /// hole codexreview named: an outage-producing typo that no test could see.
    /// </summary>
    [Fact]
    public void CurrentPin_MatchesTheIndependentlySpecifiedGen3Pin()
    {
        Assert.Equal(QuicRegistrationTests.ExpectedGen3Pin, SharedCertMaterial.CurrentPin);
    }

    /// <summary>G-05: an operator-named directory keeps the revoked check and drops the generation one.</summary>
    [Fact]
    public void ExplicitDirectory_KeepsRevokedCheck_ButNotGenerationCheck()
    {
        const string freshlyGenerated = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB=";

        // A `glp-quick cert generate` pin is accepted when the directory was named explicitly...
        SharedCertMaterial.AssertPinIsTrusted(freshlyGenerated, FpPath, requireCurrentGeneration: false);

        // ...but the REVOKED list is unconditional, which is the property that closes the exposure.
        var ex = Assert.Throws<InvalidOperationException>(
            () => SharedCertMaterial.AssertPinIsTrusted(RevokedGen1, FpPath, requireCurrentGeneration: false));
        Assert.Contains("REVOKED", ex.Message);
    }

    // ---- SC-003: the message must be actionable without reading the source --------------------
    [Fact]
    public void RevokedPin_MessageNamesPinRuleAndRemedy()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => SharedCertMaterial.AssertPinIsTrusted(RevokedGen1, FpPath));

        Assert.Contains(RevokedGen1, ex.Message);          // the pin
        Assert.Contains("REVOKED", ex.Message);            // the rule
        Assert.Contains("REMEDY", ex.Message);             // the remedy
        Assert.Contains("do NOT", ex.Message);             // ...and the anti-remedy
        Assert.Contains("git history", ex.Message);        // named explicitly: this is the trap
    }

    // ---- FR-004: coverage for generations nobody enumerated -----------------------------------
    [Fact]
    public void UnknownGeneration_IsRefusedAsNotCurrent()
    {
        const string unknown = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

        var ex = Assert.Throws<InvalidOperationException>(
            () => SharedCertMaterial.AssertPinIsTrusted(unknown, FpPath));

        Assert.Contains("NOT THE CURRENT GENERATION", ex.Message);
        Assert.DoesNotContain("REVOKED", ex.Message);   // a different situation, a different message
    }

    // ---- FR-007: ordering is pinned so a later edit cannot merge the clauses -------------------
    [Fact]
    public void RevokedBeatsNotCurrent_OrderIsPinned()
    {
        // The revoked pin is ALSO not the current pin, so both rules match it. The more serious
        // one must win: an operator serving a public key must not be told merely that their
        // material is "unrecognised".
        Assert.NotEqual(SharedCertMaterial.CurrentPin, RevokedGen1);

        var ex = Assert.Throws<InvalidOperationException>(
            () => SharedCertMaterial.AssertPinIsTrusted(RevokedGen1, FpPath));

        Assert.Contains("REVOKED", ex.Message);
        Assert.DoesNotContain("NOT THE CURRENT GENERATION", ex.Message);
    }

    // ---- FR-008: whitespace cannot smuggle revoked material past the guard ---------------------
    [Fact]
    public void TrailingNewline_DoesNotEvadeTheGuard()
    {
        // Load() trims before calling, so this pins the CALLER's contract: an untrimmed pin must
        // not silently become "unrecognised" and thereby dodge the REVOKED message.
        var untrimmed = RevokedGen1 + "\n";

        // Untrimmed input is still refused (as not-current) — it never reaches "trusted".
        var ex = Assert.Throws<InvalidOperationException>(
            () => SharedCertMaterial.AssertPinIsTrusted(untrimmed, FpPath));
        Assert.Contains("REMEDY", ex.Message);

        // And the trimmed form — what Load() actually passes — is refused as REVOKED.
        var trimmed = Assert.Throws<InvalidOperationException>(
            () => SharedCertMaterial.AssertPinIsTrusted(untrimmed.Trim(), FpPath));
        Assert.Contains("REVOKED", trimmed.Message);
    }

    // ---- FR-007: the PRE-EXISTING checks still fire, and still fire FIRST ----------------------
    // The generation guard was inserted after them. If a later edit hoisted it above them, a
    // missing-file situation would be reported as a trust-generation problem, which is a worse
    // diagnosis of a simpler fault. These go through Load() because that is where the ordering
    // lives; they need no valid cert, because they must fail before any cert is parsed.
    [Fact]
    public void ExistingChecks_StillFireFirst_MissingPfx()
    {
        var dir = Directory.CreateTempSubdirectory("f109-nopfx-").FullName;
        try
        {
            var ex = Assert.Throws<FileNotFoundException>(() => SharedCertMaterial.Load(dir));
            Assert.Contains("shared QUIC cert missing", ex.Message);
            // NOT reported as a generation problem:
            Assert.DoesNotContain("REVOKED", ex.Message);
            Assert.DoesNotContain("NOT THE CURRENT GENERATION", ex.Message);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void ExistingChecks_StillFireFirst_MissingFingerprint()
    {
        var dir = Directory.CreateTempSubdirectory("f109-nofp-").FullName;
        try
        {
            // A pfx that EXISTS (contents irrelevant — the pin-file check precedes parsing it).
            File.WriteAllBytes(Path.Combine(dir, SharedCertMaterial.PfxFileName), [0x00]);

            var ex = Assert.Throws<FileNotFoundException>(() => SharedCertMaterial.Load(dir));
            Assert.Contains("shared QUIC SPKI pin missing", ex.Message);
            Assert.DoesNotContain("REVOKED", ex.Message);
            Assert.DoesNotContain("NOT THE CURRENT GENERATION", ex.Message);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ---- FR-002: the trust decision is compiled in, with no empty state to fail open into ------
    [Fact]
    public void TrustDecision_IsCompiledIn_NotConfigurable()
    {
        // If CurrentPin were ever read from a file or an env var, an absent source would yield
        // null/empty and the equality check would refuse everything (outage) or, worse, a
        // permissive default would admit everything. A const cannot be absent.
        Assert.False(string.IsNullOrWhiteSpace(SharedCertMaterial.CurrentPin));
        Assert.NotEqual(RevokedGen1, SharedCertMaterial.CurrentPin);
    }
}

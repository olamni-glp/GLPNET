// SPDX-License-Identifier: MIT
//
// The regression guard for the defect @ariellas-glpnet measured on 2026-09-04T17:45Z: five probe
// runs on one host produced FIVE DIFFERENT SPKI PINS, because the federation plan adopted the
// ephemeral CreateDevCert as its trust anchor. A pin table exchanged before a reboot would then be
// invalid for every host afterwards, and mTLS would refuse every peer.
//
// These tests assert the property the fleet actually needs — THE PIN SURVIVES A RESTART — rather
// than that a file exists. A test that only checks the file would pass on a keystore that rewrote
// itself with a new key every load, which is the bug.

using System.Security.Cryptography.X509Certificates;
using GlpRuntime.CrdtMsg.Route;
using Xunit;

namespace GlpRuntime.CrdtMsg.Tests;

public class FederationIdentityPersistenceTests
{
    private static string TempKeystore() =>
        Path.Combine(Path.GetTempPath(), "glpnet-fed-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void LoadOrCreate_yields_the_SAME_pin_across_process_restarts()
    {
        var dir = TempKeystore();
        try
        {
            using var first = QuicLinkTransport.LoadOrCreateDevCert("shiras", out var o1, dir);
            using var second = QuicLinkTransport.LoadOrCreateDevCert("shiras", out var o2, dir);
            using var third = QuicLinkTransport.LoadOrCreateDevCert("shiras", out var o3, dir);

            Assert.Equal("created", o1);
            Assert.Equal("loaded", o2);
            Assert.Equal("loaded", o3);

            // The claim that matters: one host, one pin, no matter how often it starts.
            var pin = QuicLinkTransport.SpkiPin(first);
            Assert.Equal(pin, QuicLinkTransport.SpkiPin(second));
            Assert.Equal(pin, QuicLinkTransport.SpkiPin(third));

            // and the private key survived the round-trip, or it cannot serve a QUIC listener
            Assert.True(second.HasPrivateKey);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void CreateDevCert_is_ephemeral_by_design_which_is_WHY_it_must_not_anchor_federation()
    {
        // The positive control. If this ever starts passing as "stable", the premise of the fix has
        // changed and the ruling above should be re-read — so the guard asserts the difference.
        using var a = QuicLinkTransport.CreateDevCert("colab-A");
        using var b = QuicLinkTransport.CreateDevCert("colab-A");
        Assert.NotEqual(QuicLinkTransport.SpkiPin(a), QuicLinkTransport.SpkiPin(b));
    }

    [Fact]
    public void Two_different_hosts_get_two_different_pins_in_one_keystore()
    {
        var dir = TempKeystore();
        try
        {
            using var shiras = QuicLinkTransport.LoadOrCreateDevCert("shiras", out _, dir);
            using var ariellas = QuicLinkTransport.LoadOrCreateDevCert("ariellas", out _, dir);
            Assert.NotEqual(QuicLinkTransport.SpkiPin(shiras), QuicLinkTransport.SpkiPin(ariellas));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void A_federation_anchor_outlives_the_30_day_CreateDevCert_window()
    {
        // A 30-day anchor is a monthly fleet-wide pin rotation nobody scheduled.
        var dir = TempKeystore();
        try
        {
            using var cert = QuicLinkTransport.LoadOrCreateDevCert("shiras", out _, dir);
            Assert.True(cert.NotAfter > DateTime.Now.AddDays(365),
                $"federation anchor expires {cert.NotAfter:O} — too soon to be a stable pin");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}

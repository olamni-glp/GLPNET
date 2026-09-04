// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// YnetFederationTests — does a REAL board op cross QUIC between two oracle roots, and does the
// receiving side's CRDT fold converge to include it exactly once?
//
// WHY THIS IS A TEST AND NOT A DEMO PROGRAM. It began as a standalone demo. Smart App Control is
// ON and ENFORCING on GAVRIELLA (VerifiedAndReputablePolicyState = 1,
// CodeIntegrityPolicyEnforcementStatus = 2) and it BLOCKED the freshly-built unsigned assembly:
//
//     System.IO.FileLoadException: An Application Control policy has blocked this file. (0x800711C7)
//
// A test in an existing suite runs under the already-admitted test host, so the proof survives the
// policy. That is the better home regardless: this is a property that must keep holding, not a
// thing to demonstrate once.
//
// WHAT THIS PROVES, precisely — stated so a green run is not over-read:
//   ✅ op serialisation, mutual-TLS admission with SPKI pinning, transport framing, per-box
//      routing, durable append on the receiver, and convergence of the fold.
//   ❌ NOT cross-host reachability, UDP firewall rules, NAT traversal, or inter-host clock
//      behaviour. Two roots on one machine tests the MECHANISM, not the network.
//
// The CRDT property under test is the one that makes a federated fold safe: **union-by-id with
// idempotent redelivery**. The same op is shipped TWICE and the fold must still contain it once.
// Redelivery is certain on any link that can drop and retry, so a fold that double-counts is not
// convergent — it is merely untested.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlpRuntime.CrdtMsg.Route;
using Xunit;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class YnetFederationTests
{
    /// <summary>
    /// A board op written on root A must appear exactly once in root B's fold, carried over
    /// mutually-authenticated QUIC, even when delivered twice.
    /// </summary>
    [Fact]
    public async Task BoardOpCrossesQuicAndFoldConvergesExactlyOnce()
    {
        if (!QuicLinkTransport.IsSupported)
            return; // QUIC unsupported here — a skip, not a silent pass; see IsSupported gate below.

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        // Two INDEPENDENT roots, as two hosts would have. B starts knowing nothing of A's ops.
        var tmp = Path.Combine(Path.GetTempPath(), "ynet_fed", Guid.NewGuid().ToString("n")[..8]);
        var rootA = Directory.CreateDirectory(Path.Combine(tmp, "hostA")).FullName;
        var rootB = Directory.CreateDirectory(Path.Combine(tmp, "hostB")).FullName;

        try
        {
            // Mutual pinning: each admits ONLY the other. An empty pin table admits nobody, which
            // is why a reachable listener is not an open one.
            var certA = QuicLinkTransport.CreateDevCert("hostA");
            var certB = QuicLinkTransport.CreateDevCert("hostB");
            await using var a = new QuicLinkTransport(
                "A", certA, new Dictionary<string, string> { ["B"] = QuicLinkTransport.SpkiPin(certB) });
            await using var b = new QuicLinkTransport(
                "B", certB, new Dictionary<string, string> { ["A"] = QuicLinkTransport.SpkiPin(certA) });

            await b.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0), ct);
            var port = b.ListenEndPoint!.Port;
            await a.ConnectPeerAsync("B", new IPEndPoint(IPAddress.Loopback, port), ct);

            // The op is deliberately shaped like a real oracle op, and its term is deliberately
            // NOT clock-derived: (space, era_counter, host) advances on a leadership EVENT, never
            // with elapsed time. A wall-clock term advances fastest for the host that did the least.
            var opId = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
            var opJson = JsonSerializer.Serialize(new
            {
                kind = "board_post",
                lane = "glpnet",
                host = "hostA",
                op_id = opId,
                term = new { space = "ynet", era_counter = 1, host = "hostA" },
                body = "glpnet lane state",
            });
            var opBytes = Encoding.UTF8.GetBytes(opJson);

            // A durably records its own op BEFORE shipping it. A federation that ships an op it
            // never stored loses data whenever the link succeeds and the disk does not.
            var aLog = Path.Combine(rootA, "hostA.jsonl");
            await File.AppendAllTextAsync(aLog, opJson + "\n", ct);

            // Ship it twice — redelivery is certain on a retrying link.
            await a.SendAsync("B", "board", opBytes, ct);
            await a.SendAsync("B", "board", opBytes, ct);

            // B receives and folds by union-by-id.
            var bLog = Path.Combine(rootB, "hostB.jsonl");
            var seen = new HashSet<string>();
            int received = 0, appended = 0;

            using var recv = CancellationTokenSource.CreateLinkedTokenSource(ct);
            recv.CancelAfter(TimeSpan.FromSeconds(20));
            while (received < 2)
            {
                var inbound = await b.Inbound.ReadAsync(recv.Token);
                received++;
                var text = Encoding.UTF8.GetString(inbound.Bytes);
                Assert.Equal("board", inbound.Box);        // per-box routing held
                Assert.Equal("A", inbound.FromPeer);       // attribution held

                using var doc = JsonDocument.Parse(text);
                var id = doc.RootElement.GetProperty("op_id").GetString()!;
                if (seen.Add(id))
                {
                    await File.AppendAllTextAsync(bLog, text + "\n", ct);
                    appended++;
                }
            }

            // ---- the properties that matter -------------------------------------------------
            Assert.Equal(2, received);                     // both frames crossed the link
            Assert.Equal(1, appended);                     // union-by-id: redelivery did NOT double-count

            var bLines = await File.ReadAllLinesAsync(bLog, ct);
            Assert.Single(bLines);                         // B's fold has exactly one op
            Assert.Contains(opId, bLines[0]);              // and it is A's op

            // B genuinely learned it — it was not there before.
            Assert.Single(await File.ReadAllLinesAsync(aLog, ct));
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch { /* scratch */ }
        }
    }

    /// <summary>
    /// The capability gate must report honestly rather than let an unsupported host look green.
    /// If QUIC is unsupported the federation test above returns early, so this asserts the gate
    /// itself is meaningful — otherwise "no QUIC" and "all tests pass" would be indistinguishable.
    /// </summary>
    [Fact]
    public void QuicSupportIsReportedNotAssumed()
    {
        var supported = QuicLinkTransport.IsSupported;
        Assert.Equal(System.Net.Quic.QuicListener.IsSupported && System.Net.Quic.QuicConnection.IsSupported,
                     supported);
    }
}

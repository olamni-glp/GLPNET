// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// glp_quic_probe — prove that THIS host can bind a real QUIC IP listener, and print the exact
// configuration the broker / guardian / oracle need to do the same.
//
// WHY THIS EXISTS. The fleet's federated golden board is blocked on one measured fact, reported by
// the yngenios oracle's own `status`:
//
//     "NOT yet one board across four hosts: no QUIC listener runs in this estate
//      (measured 2026-09-03), so there is no inter-host transport."
//
// That is true as a statement about what is RUNNING. It is false as a statement about what EXISTS:
// glpnet already ships a complete mTLS QUIC transport with a listener
// (csharp/glp_crdtmsg/route/QuicLinkTransport.cs, 491 lines, net11.0, 11/11 tests green on
// GAVRIELLA 2026-09-04). Nobody is running it. This probe closes the gap between "we have one" and
// "one is listening", and it does so by BINDING A REAL PORT rather than by asserting capability.
//
// It deliberately reports THREE separable things, because conflating them is how "no QUIC" became
// received wisdom:
//   1. Is QUIC supported by this runtime + OS at all?   (QuicListener.IsSupported)
//   2. Can we actually BIND a listener on a real endpoint?  (the thing that was never tried)
//   3. What configuration would a service need to do it?    (printed, so it is copyable)
//
// Exit: 0 listener bound and closed cleanly · 1 supported but bind FAILED · 2 QUIC unsupported here.

using System.Net;
using System.Security.Cryptography.X509Certificates;
using GlpRuntime.CrdtMsg.Route;
using Ynet.Transport.Link;
using Ynet.Transport.Capability;

// 🔴 FIRST, AND THE ORDER IS LOAD-BEARING. QuicListener.IsSupported runs MsQuic's static
// initialiser, and a DllImportResolver registered after that has NO effect. Touching
// MsQuicProvider here forces the ynet_transport assembly to load — its [ModuleInitializer]
// installs the resolver — BEFORE any System.Net.Quic type is read below.
// Without this the probe reported IsSupported=False on SHIRAS while the very same host carries a
// working libmsquic at ~/.local/lib and binds a real link. That false negative is precisely the
// "there is no QUIC in this estate" conclusion two other probes have already falsified.
var quicNative = MsQuicProvider.Instance.Probe();

var bindArg = args.Length > 0 ? args[0] : "127.0.0.1:0";
var parts = bindArg.Split(':');
var ip = IPAddress.Parse(parts[0]);
var port = parts.Length > 1 ? int.Parse(parts[1]) : 0;

Console.WriteLine("== glp_quic_probe — can this host bind a QUIC listener? ==");
Console.WriteLine($"   runtime   : .NET {Environment.Version}");
Console.WriteLine($"   os        : {Environment.OSVersion}");
Console.WriteLine($"   requested : {ip}:{port}" + (port == 0 ? "  (0 = let the OS choose)" : ""));
Console.WriteLine($"   msquic    : {(quicNative.Supported ? "resolved" : "UNRESOLVED")} — {quicNative.Detail}");
Console.WriteLine();

// ---- 1. capability -------------------------------------------------------
// Reported separately from the bind, because "supported" and "actually listening" are different
// claims and the estate has been conflating them.
Console.WriteLine($"   QuicListener.IsSupported   : {System.Net.Quic.QuicListener.IsSupported}");
Console.WriteLine($"   QuicConnection.IsSupported : {System.Net.Quic.QuicConnection.IsSupported}");
Console.WriteLine($"   QuicLinkTransport.IsSupported : {QuicLinkTransport.IsSupported}");
Console.WriteLine();

if (!QuicLinkTransport.IsSupported)
{
    Console.WriteLine("UNSUPPORTED — this host cannot perform a QUIC handshake.");
    Console.WriteLine("  On Windows this needs Win11/Server2022+ (Schannel TLS 1.3) and the");
    Console.WriteLine("  msquic native library that ships with the .NET runtime.");
    Console.WriteLine("  Reporting UNSUPPORTED rather than a bind failure: the distinction matters,");
    Console.WriteLine("  because an unsupported host is a provisioning problem, not a config error.");
    return 2;
}

// ---- 2. the actual bind --------------------------------------------------
// mTLS with SPKI pinning is not optional here: ListenAsync requires a server certificate and
// pin-checks the DIALER's cert too. A listener without pins would admit anyone who can reach the
// port, which is precisely the "mere reachability MUST NOT admit" property the transport's own
// tests assert.
// 🔴 THE PIN PRINTED HERE IS PUBLISHED TO THE FLEET AND HELD BY PEERS, so it MUST survive a reboot.
// It did not: CreateDevCert mints a fresh keypair per call, and @ariellas-glpnet measured five runs
// on one host producing FIVE DIFFERENT PINS (2026-09-04T17:45Z). Every pin published from this probe
// before that fix expired at the next restart, and mTLS would then have refused every peer — a
// failure indistinguishable from a dead transport. The probe now reports this host's PERSISTED
// federation identity. Pass --ephemeral for a pure bind test whose pin nobody will hold.
bool ephemeral = args.Contains("--ephemeral");
string certOrigin = "ephemeral";
X509Certificate2 cert = ephemeral
    ? QuicLinkTransport.CreateDevCert("glpnet-probe")
    : QuicLinkTransport.LoadOrCreateDevCert(Environment.MachineName.ToLowerInvariant(), out certOrigin);
string pin = QuicLinkTransport.SpkiPin(cert);
Console.WriteLine($"   local cert SPKI pin : {pin}");
Console.WriteLine(ephemeral
    ? "   ⚠ EPHEMERAL (--ephemeral): this pin dies with the process. DO NOT PUBLISH IT."
    : $"   identity            : PERSISTED ({certOrigin}) — stable across reboots, safe to publish");
if (certOrigin == "recreated-expired")
    Console.WriteLine("   🔴 the stored anchor had EXPIRED and was re-minted: THIS HOST'S PIN HAS CHANGED — re-publish it.");
Console.WriteLine("   (a peer must carry this pin to be admitted; reachability alone is refused)");
Console.WriteLine();

// ---- 2b. the LANE NODE ID (feature 102 / ruling Q-glpnetshiras-39) -------
// The cert pin above says "this host's TLS anchor". It does not say WHO this lane is: nodeId =
// H(pubkey) is the address-INDEPENDENT name a peer resolves, votes on, and files board ops under.
// NodeIdentity.Generate() minted a fresh keypair per call — the same defect the cert had — so the
// id changed at every process start and no pin table could survive a reboot. LoadOrMint persists it.
var laneName = Environment.GetEnvironmentVariable("YNET_LANE")
               ?? Environment.MachineName.ToLowerInvariant() + ".glpnet";
using var nodeIdentity = NodeIdentity.LoadOrMint(laneName, out var idOrigin);
Console.WriteLine("== lane node identity (feature 102) ==");
Console.WriteLine($"   lane                : {laneName}");
Console.WriteLine($"   nodeId = H(pubkey)  : {nodeIdentity.NodeId}");
Console.WriteLine($"   algorithm           : {nodeIdentity.Algorithm}");
Console.WriteLine($"   origin              : {idOrigin}");
Console.WriteLine(idOrigin switch
{
    IdentityOrigin.Loaded => "   ✅ PERSISTED — run this probe again and the id above is identical.",
    IdentityOrigin.Minted => "   first use on this host: minted and written. Re-run to see it load.",
    _ => "   🔴 the stored key was UNREADABLE and re-minted: THIS LANE'S NODE ID HAS CHANGED — re-publish it.",
});
Console.WriteLine("   Resolve(nodeId) -> address is served by INodeAddressResolver; an unbound id is");
Console.WriteLine("   refused RecordNotFound, and a refusal is a valid answer — never a fabricated address.");
Console.WriteLine();

var transport = new QuicLinkTransport("glpnet-probe", cert, new Dictionary<string, string>());
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

try
{
    await transport.ListenAsync(new IPEndPoint(ip, port), cts.Token);
    Console.WriteLine("✅ LISTENER BOUND — a real QUIC listener is up on this host.");
    Console.WriteLine();
    Console.WriteLine("   This is the component the oracle reports as absent estate-wide.");
    Console.WriteLine("   It is not absent: it is unrun. glpnet ships it and it binds here.");
}
catch (Exception ex)
{
    Console.WriteLine($"🔴 BIND FAILED — {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("   QUIC is SUPPORTED here but the bind did not succeed. That is a");
    Console.WriteLine("   configuration/permission problem, not a capability one — most likely the");
    Console.WriteLine("   UDP port is taken or blocked. Try a different port before concluding");
    Console.WriteLine("   anything about QUIC support on this host.");
    return 1;
}

// ---- 3. the configuration a service needs --------------------------------
Console.WriteLine();
Console.WriteLine("== configuration for yng-broker / yng-guardian / the oracle ==");
Console.WriteLine("   Every field below is REQUIRED by QuicLinkTransport.ListenAsync:");
Console.WriteLine();
Console.WriteLine("   bind endpoint     : IPEndPoint  — 0.0.0.0:<port> to accept from other hosts");
Console.WriteLine("                       (127.0.0.1 binds loopback ONLY and cannot federate)");
Console.WriteLine("   server certificate: X509Certificate2 with a private key");
Console.WriteLine("                       QuicLinkTransport.LoadOrCreateDevCert(<host>) — persisted, stable");
Console.WriteLine("   peer pins         : IReadOnlyDictionary<peer, spkiPin>");
Console.WriteLine("                       EMPTY DICTIONARY = admit nobody. This is the safe default");
Console.WriteLine("                       and it is why a reachable listener is not an open one.");
Console.WriteLine("   ALPN              : fixed by the transport; peers must agree");
Console.WriteLine();
Console.WriteLine("   For a 4-host federation each host needs: its own cert, plus the SPKI pin of");
Console.WriteLine("   the other three. That is a 4-entry pin table per host — the whole of the");
Console.WriteLine("   trust configuration, and it is mutual (the dialer is pin-checked too).");
Console.WriteLine();
Console.WriteLine("   🔴 UDP, not TCP. A firewall rule permitting TCP will not admit QUIC, and a");
Console.WriteLine("   port scan looking for a listening TCP socket will report 'nothing there' —");
Console.WriteLine("   which is exactly what has been measured about the broker on this host.");

await transport.DisposeAsync();
Console.WriteLine();
Console.WriteLine("listener closed cleanly.");
return 0;

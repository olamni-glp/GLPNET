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

var bindArg = args.Length > 0 ? args[0] : "127.0.0.1:0";
var parts = bindArg.Split(':');
var ip = IPAddress.Parse(parts[0]);
var port = parts.Length > 1 ? int.Parse(parts[1]) : 0;

Console.WriteLine("== glp_quic_probe — can this host bind a QUIC listener? ==");
Console.WriteLine($"   runtime   : .NET {Environment.Version}");
Console.WriteLine($"   os        : {Environment.OSVersion}");
Console.WriteLine($"   requested : {ip}:{port}" + (port == 0 ? "  (0 = let the OS choose)" : ""));
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
// The identity is PERSISTED, not minted per run (Q-GLPNETA21-01). Before this fix the probe
// called CreateDevCert and printed a DIFFERENT pin on every run of the same binary on the same
// unchanged host — measured five times on ARIELLAS 2026-09-04T17:35Z. A pin table exchanged
// between hosts is only worth exchanging if the pin outlives the process that published it, so
// the probe now prints the pin a peer can actually write down. Run it twice: the pin must not move.
var identity = QuicLinkTransport.LoadFederationIdentity("glpnet-probe");
X509Certificate2 cert = identity.Cert;
string pin = identity.Pin;
Console.WriteLine($"   local cert SPKI pin : {pin}");
// Publish all THREE. A pin and a node id are the same 32 bytes in different encodings, and an
// operator who pastes one into the other's field gets every valid peer refused by what LOOKS like
// a security event (@gavriella-glpnet, 2026-09-04T19:30Z). The SPKI is separate again: a pin is a
// hash and cannot verify a signature, so without it an admitted peer can forge ops in another
// admitted peer's name.
Console.WriteLine($"   node_id (hex)       : {identity.NodeId}");
Console.WriteLine($"   spki (base64)       : {identity.Spki}");
Console.WriteLine($"   keystore            : {identity.PfxPath}");
Console.WriteLine($"   identity            : {(identity.Created ? "MINTED (first run on this host)" : "LOADED (stable across restarts)")}");
Console.WriteLine("   (a peer must carry this pin to be admitted; reachability alone is refused)");
Console.WriteLine("   (re-run this probe — a pin that CHANGES is the Q-GLPNETA21-01 defect returning)");
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
Console.WriteLine("                       QuicLinkTransport.LoadFederationIdentity(<name>).Cert");
Console.WriteLine("                       🔴 NOT CreateDevCert — that mints a fresh keypair per call,");
Console.WriteLine("                       so its pin dies at the next restart and mTLS then refuses");
Console.WriteLine("                       EVERY peer, which looks exactly like a dead transport.");
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

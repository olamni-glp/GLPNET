// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The M6 client's control surface. Three verbs, and each one answers a question an operator or an
// agent actually asks:
//
//   run       start the receiver and keep it running, independently of any agent (M6-b)
//   serve     `run` PLUS the liveness surfaces a supervisor interrogates            (M6-d)
//   poll      one deterministic sweep of the inbox, then exit  (M6-R2, provable in a script)
//   send      deliver a frame to another lane's mailbox        (M6-R3)
//   doctor    who am I, where is my mailbox, and what is in it that is not a deliverable frame
//   pending   what is waiting for me?            - the agent's "/btw" drain queue (M6-f)
//   drain     I have handled this one            - explicit, idempotent, agent-chosen
//
// 2026-09-06 (feature 107) — TWO defects of the SAME CLASS closed, both measured in this repo on
// the same day: a capability built, tested and merged, with the consumer that would make it
// load-bearing never written.
//
//   1. QuicInbound/QuicOutbound — 400 LoC, 210 LoC of green tests, and this file contained ZERO
//      references to them. The wire plane was unreachable from every verb. `--plane wire|both` now
//      reaches it, through PlaneCatalog, which is the ONLY way a plane can be constructed — so
//      registration implies reachability by construction, and ContractReachabilityTests fails if a
//      realization is ever added without a path to it.
//
//   2. Kernel-managed hosting. The previous version of this comment said M6-d was "the next step",
//      which was true but incomplete: csharp/glp_supervisor ALREADY did child hosting, round-trip
//      liveness, zombie detection, backoff and restart — it hosted glp_engine_host and not this
//      client. The capability was here all along. `serve` answers the split-protocol Ping/Ack that
//      supervisor already sends, so it hosts this client with ZERO change to itself.
//
// 2026-09-05: "run" binds a REAL cross-lane plane (CoopFileInbound) when YNET_CLIENT_COOP and
// YNET_CLIENT_LANE are set. Before this it bound LoopbackInbound unconditionally and could only
// hear itself — a gap this lane disclosed in its own source rather than hid, and the carrier
// adapter ruled to this lane by Q-glpnetshiras-50. A wire request that cannot bind now degrades to
// the file plane, says so ON the running line, and writes a fleet-visible degraded record
// (engineer ruling Q-G34-02, 2026-09-06) — because four hosts each honestly reporting "wire
// unavailable" look, from outside, exactly like four healthy hosts.

using Ynet.Client;
using Ynet.Transport.Capability;

// 🔴 SUPERVISOR-LAUNCH DETECTION — codexreview finding P1, 2026-09-06.
//
// `Supervisor.StartChild` (csharp/glp_supervisor/Supervisor.cs:396) launches its child as
//     <binary> --listen <addr> --store "<root>"
// with NO verb. Without this branch args[0] is "--listen", the switch below falls to `default`,
// and the process exits immediately — so the supervised hosting this feature claims to deliver
// would have been reachable ONLY by a human typing `serve` by hand.
//
// That is the THIRD instance of this era's own defect class — a capability built with no consumer
// path — and it was in the fix for the second one. It was caught by the codexreview, not by me,
// and not by any of my tests, because every test constructed the responder directly. The lesson is
// the one this whole feature exists to teach: a capability's own tests take the path a real
// consumer does not.
var supervisorLaunched = args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal);
var verb = supervisorLaunched
    ? "serve"
    : args.Length > 0 ? args[0].ToLowerInvariant() : "help";

string? Opt(string name)
{
    // Scan from 0 when a supervisor launched us: there is no verb in position 0, so starting at 1
    // would skip the FIRST option — which is `--listen`, the one that matters most.
    for (var i = supervisorLaunched ? 0 : 1; i < args.Length - 1; i++)
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    return null;
}

// ONE addressing convention - the incumbent's. YNET_CLIENT_COOP names the shared root and
// YNET_CLIENT_LANE the lane's own directory under it. --coop/--self are conveniences over the same
// two values, and --self DERIVES the lane directory from a "<node>/<actor>" identity via
// PeerIdentity, so a peer can be addressed by who it IS rather than by the hashed directory name it
// happens to hash to. Two rival addressing schemes in one client is how a lane ends up addressing
// nobody, so this adds a spelling, not a second convention.
string? CoopRoot() => Opt("--coop") ?? Environment.GetEnvironmentVariable("YNET_CLIENT_COOP");

string LaneDir()
{
    var self = Opt("--self") ?? Environment.GetEnvironmentVariable("YNET_SELF");
    if (!string.IsNullOrWhiteSpace(self)) return PeerIdentity.Parse(self).DirectoryName;
    var lane = Environment.GetEnvironmentVariable("YNET_CLIENT_LANE");
    if (!string.IsNullOrWhiteSpace(lane)) return lane;
    throw new InvalidOperationException(
        "this lane's mailbox is not identified. Pass --self <node>/<actor>, or set YNET_SELF, or set " +
        "YNET_CLIENT_LANE. Refusing to invent one: an invented identity binds a mailbox no peer " +
        "addresses, and then reports 'running' forever.");
}

string RequiredRoot() => CoopRoot() ?? throw new InvalidOperationException(
    "COOP root is not set. Pass --coop <root> or set YNET_CLIENT_COOP. The carrier refuses to guess " +
    "a root: guessing one addresses nobody and reports success.");

var spool = new PendingAlertSpool(
    Environment.GetEnvironmentVariable("YNET_CLIENT_SPOOL") ?? PendingAlertSpool.DefaultDirectory);

switch (verb)
{
    case "run":
    case "serve":
    {
        // `serve` is `run` plus a liveness endpoint a SUPERVISOR can interrogate (M6-d). They share
        // one body deliberately: two code paths would let the supervised client and the operator's
        // client drift, and "the thing we supervise is not the thing we run" is its own defect.
        var supervised = verb == "serve";

        var hook = new AgentHook(Environment.GetEnvironmentVariable("YNET_CLIENT_HOOK"));

        // 🔴 PLANE SELECTION NOW GOES THROUGH PlaneCatalog — the whole point of this feature.
        // Until 2026-09-06 this verb could bind only CoopFileInbound or LoopbackInbound; QuicInbound
        // existed, was tested, and had NO PATH TO IT from any verb. See PlaneCatalog's doc comment.
        var requested = PlaneCatalog.Parse(
            Opt("--plane") ?? Environment.GetEnvironmentVariable("YNET_CLIENT_PLANE"));

        var coopRoot = CoopRoot();
        var laneDir = Environment.GetEnvironmentVariable("YNET_CLIENT_LANE")
                      ?? (Opt("--self") is { } s ? PeerIdentity.Parse(s).DirectoryName : null);

        // The signing identity is keyed by the LANE, not by the mailbox directory: `laneDir` is an
        // escaped-and-digested form (`<node>%2F<node>%2E<lane>~<hash>`) and feeding it to the
        // keystore would mint a key under a name no other tool resolves. Derived only from an
        // identity the operator actually stated — never from YNET_CLIENT_LANE, which carries the
        // directory form and would silently produce a second, divergent node id for the same lane.
        var laneName = (Opt("--self") ?? Environment.GetEnvironmentVariable("YNET_SELF")) is { } selfRaw
            ? PeerIdentity.Parse(selfRaw).Actor
            : null;

        var binding = new PlaneCatalog.Binding
        {
            CoopRoot = coopRoot,
            LaneDirectory = laneDir,
            // 🔴 2026-09-08 (olamnit.glpnet, engineer-authorised over Q-glpnetshiras-50; C-18 claim
            // CLAIM-20260908T0555Z). This read `Self = null` with the comment "supplied by
            // --identity in a later step". THAT STEP WAS NEVER WRITTEN — `--identity` has zero hits
            // across csharp/ — so `NewWire` threw on `b.Self is null` (PlaneCatalog.cs:143) for
            // EVERY run, and every host in the fleet degraded to the file plane and was then read as
            // misconfigured. Two lanes found this from opposite ends of the client within one hour
            // and both correctly deferred under the ownership rule; three lanes deferring correctly
            // still adds up to nobody fixing it.
            //
            // The keystore was already built and mints on first use, so there is no key ceremony:
            // identity is derived from the lane we were told to be, and is NEVER invented — when no
            // lane is named we leave Self null and let the existing degrade path speak, rather than
            // minting a key for a mailbox no peer addresses.
            //
            // 🔴 THIS MAKES THE WIRE ATTEMPTABLE. IT DOES NOT ASSERT THAT A HANDSHAKE SUCCEEDS.
            // Nobody in this fleet has yet observed one. The next error is the finding.
            Self = laneName is null ? null : NodeIdentity.LoadOrMint(laneName, out _),
            // Not Opt("--listen") when a supervisor launched us: there that flag is the PROBE
            // address, and binding the QUIC listener to it would make the two fight for one port.
            Listener = PlaneCatalog.ParseListen("ynet-client", supervisorLaunched ? null : Opt("--listen")),
        };

        PlaneBinding bound;
        try
        {
            bound = PlaneSelection.Bind(
                requested, binding, new DegradedNotice(coopRoot, laneDir ?? "unknown-lane"));
        }
        catch (InvalidOperationException ex)
        {
            // No plane could be bound at all. Refusing is the only honest answer — a client that
            // starts here would receive nothing while reporting that it is running.
            Console.Error.WriteLine($"ynet_client: {ex.Message}");
            return 6;
        }

        var plane = bound.Inbound;
        if (plane is CoopFileInbound direct)
        {
            direct.StrayObserved += p => Console.Error.WriteLine($"ynet_client: STRAY (not a .frame, not delivered): {p}");
            direct.PollFailed += ex => Console.Error.WriteLine($"ynet_client: poll failed: {ex.GetType().Name}: {ex.Message}");
        }

        using var machine = new YnetReceiverMachine(plane, spool, hook);

        machine.Faulted += ex => Console.Error.WriteLine($"machine fault: {ex.GetType().Name}: {ex.Message}");
        machine.Launch("ynet-client-receiver");

        // 🔴 The running line is RENDERED FROM THE BOUND VALUE, and states a degradation on the same
        // line as the word "running" (FR-004a). A fallback an operator has to go looking for in a
        // log is a silent fallback, and silent fallback is the defect this feature closes.
        Console.WriteLine($"{bound.RunningLine()}   spool={spool.Directory}");

        foreach (var inner in Enumerate(plane))
            Console.WriteLine($"ynet_client:   live plane → {inner.PlaneName}" +
                              (inner is CoopFileInbound c ? $"   inbox={c.InboxDirectory}" : string.Empty) +
                              (inner is QuicInbound q ? $"   listening={q.BoundEndPoint?.ToString() ?? "(not bound)"}" +
                                                        $"   provider={q.ProviderName ?? "(none)"}" : string.Empty));

        if (plane is LoopbackInbound)
            Console.WriteLine("ynet_client: NO CROSS-LANE PLANE — set YNET_CLIENT_COOP and YNET_CLIENT_LANE. " +
                              "This receiver can only hear messages this process makes for itself.");

        Console.WriteLine($"ynet_client: hook={(hook.IsConfigured ? "configured" : "NOT configured (durable-only)")}");
        Console.WriteLine($"ynet_client: {spool.Count} alert(s) already pending from earlier runs");

        // ---- M6-d: the liveness surfaces a supervisor interrogates ----
        //
        // TWO of them, deliberately, and they are not redundant:
        //
        //   SupervisedLiveness — speaks the split-protocol Ping/Ack that csharp/glp_supervisor
        //     ALREADY sends. This is what lets the EXISTING supervisor host this client with zero
        //     change to itself (FR-029). Its answer is two-valued because that is the protocol the
        //     supervisor has: Ack, or silence that its PingTimeout turns into a death verdict.
        //
        //   LivenessEndpoint — a three-valued answer (healthy / UNHEALTHY / gone) for watchers that
        //     can use the distinction. A supervisor that can tell "sick" from "gone" can tell a
        //     broken channel from a dead process (FR-027) and stop restarting a client that only
        //     needed its channel re-opened.
        //
        // Both read health from the SAME source at the moment of asking, so they cannot disagree.
        SupervisedLiveness? supervisedLiveness = null;
        Func<bool> isHealthy = () => !machine.IsStopped && !machine.IsDegraded;

        if (supervised)
        {
            // Under a supervisor launch, `--listen` IS the probe address: that is the flag the
            // supervisor passes and the address it will ping. When a human runs `serve`, `--listen`
            // keeps its ordinary meaning (the QUIC listener) and `--probe` names the probe, so the
            // two invocation styles cannot silently mean different things by the same flag.
            var probeSpec = supervisorLaunched
                ? (Opt("--listen") ?? "127.0.0.1:44311")
                : (Opt("--probe") ?? "127.0.0.1:44311");
            var probeAddr = PlaneCatalog.ParseListen("ynet-probe", probeSpec);
            supervisedLiveness = new SupervisedLiveness(
                probeAddr!.Value.BindAddress.ToString(), probeAddr.Value.Port, isHealthy);
            supervisedLiveness.Start();

            // Wait for a REAL bind before announcing readiness. A readiness token published on
            // intent is how a supervisor starts pinging a port nothing is listening on and
            // concludes its child is dead.
            if (!supervisedLiveness.Bound.Wait(TimeSpan.FromSeconds(5)))
            {
                Console.Error.WriteLine(
                    $"ynet_client: the supervisor probe could NOT bind {probeAddr.Value.BindAddress}:" +
                    $"{probeAddr.Value.Port} — a supervisor would read this client as dead and restart " +
                    "it forever. NOT STARTED under supervision.");
                supervisedLiveness.Dispose();
                machine.Post(new Ynet.Client.Machine.QEvt(YnetSignal.Stop));
                (plane as IDisposable)?.Dispose();
                return 9;
            }

            Console.WriteLine(
                $"ynet_client: supervisor probe={probeAddr.Value.BindAddress}:{probeAddr.Value.Port}  " +
                "(split-protocol Ping/Ack — csharp/glp_supervisor hosts this client UNMODIFIED)");
        }

        LivenessEndpoint? liveness = null;
        if (supervised)
        {
            var addr = PlaneCatalog.ParseListen("ynet-liveness", Opt("--health") ?? "127.0.0.1:0");
            liveness = new LivenessEndpoint(
                new System.Net.IPEndPoint(addr!.Value.BindAddress, addr.Value.Port),
                // 🔴 Health is read from the QHSM's OWN STATE, at the moment of asking — never from
                // a timer, never from a bare accept. A client answering "alive" from an accept loop
                // while its state machine has stopped or degraded is a zombie wearing a heartbeat,
                // and that is exactly the state a process-existence check cannot see.
                //
                // A DEGRADED machine answers UNHEALTHY rather than falling silent, so a supervisor
                // can tell "sick" from "gone" — which is also how it tells a broken channel from a
                // dead process (FR-027).
                isHealthy: isHealthy);
            liveness.Start();
            Console.WriteLine($"ynet_client: liveness={liveness.BoundEndPoint}   " +
                              "(a supervisor proves this client by ROUND-TRIP ANSWER, never by process existence)");
        }

        Console.WriteLine("ynet_client: the agent is not required; press Ctrl+C to stop.");

        var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        stop.Wait();

        machine.Post(new Ynet.Client.Machine.QEvt(YnetSignal.Stop));
        Thread.Sleep(200);
        Console.WriteLine($"ynet_client: stopped   received={machine.MessagesReceived}   pending={spool.Count}" +
                          (liveness is not null ? $"   liveness_answered={liveness.Answered}" : string.Empty) +
                          (supervisedLiveness is not null
                              ? $"   probe_acked={supervisedLiveness.Acked}   probe_refused={supervisedLiveness.Refused}"
                              : string.Empty));
        liveness?.Dispose();
        supervisedLiveness?.Dispose();
        (plane as IDisposable)?.Dispose();
        return 0;

        // Flatten a composite so status names what is ACTUALLY live rather than printing the
        // composite's own label and stopping there.
        static IEnumerable<IYnetInbound> Enumerate(IYnetInbound p) =>
            p is CompositeInbound composite ? composite.Planes : [p];
    }

    case "poll":
    {
        // One sweep, then exit. This is what makes M6-R2 PROVABLE from a script rather than
        // asserted: another PROCESS writes a frame, this process finds it, and the durable alert
        // outlives both. Deterministic - a zero poll interval starts no background pump, so nothing
        // else is sweeping the same inbox and a frame cannot be delivered twice.
        var hook = new AgentHook(Environment.GetEnvironmentVariable("YNET_CLIENT_HOOK"));
        // No Open(): this verb drives PollOnce itself, so no background pump exists to race it
        // and a frame cannot be delivered twice. The interval is left at its default rather than
        // set to Zero, which would hot-spin if anyone later added an Open() here.
        using var carrier = new CoopFileInbound(RequiredRoot(), LaneDir());
        using var machine = new YnetReceiverMachine(carrier, spool, hook);
        carrier.ConfirmDurable = m => machine.WaitForDurable(m.MessageId, TimeSpan.FromSeconds(10));
        carrier.StrayObserved += p => Console.WriteLine($"  stray (not a deliverable frame): {p}");

        // 🔴 THE CONFIDENT ZERO (olamnit-yngapp, 2026-09-05T16:12Z, corroborated here at the CLI
        // layer). "delivered=0" means two different things: an inbox READ AND FOUND EMPTY, and an
        // inbox THAT COULD NOT BE READ AT ALL - root not mounted, share dropped, ACL refused. The
        // carrier already distinguishes them and raises PollFailed; this verb subscribed only to
        // strays, so an unreachable root printed a serene "delivered=0" exactly like a quiet one.
        // Never report Quiet for a transport you could not reach.
        var unexaminable = false;
        carrier.PollFailed += ex =>
        {
            unexaminable = true;
            Console.Error.WriteLine($"ynet_client: INBOX UNEXAMINABLE — {ex.GetType().Name}: {ex.Message}");
        };

        // LAUNCH, not Start+PumpOnce. The durability gate runs INSIDE PollOnce and asks whether the
        // alert has reached the spool; the spool write happens on the machine's dispatch thread. With
        // a manually pumped machine that thread does not exist yet, so the gate could NEVER be
        // satisfied: every frame was received, durably spooled a moment later, and still bounced back
        // to the inbox as "not durable". Measured live 2026-09-05: delivered=0, received=2,
        // returned_for_retry=1. A gate that cannot be satisfied is a deadlock, not a safeguard.
        machine.Launch("ynet-client-poll");

        carrier.EnsureMailbox();

        // TWO SWEEPS, not one, and the reason is the carrier's own anti-truncation rule: a frame is
        // delivered only once its LENGTH HAS BEEN SEEN TWICE, so that a file still being written by
        // a peer is never read as a complete one. That state lives in the carrier instance, and this
        // verb is a fresh PROCESS each time - so a single sweep is always a "first sighting" and
        // could NEVER deliver anything. Measured live 2026-09-05: delivered=0 forever, with frames
        // visibly waiting in the inbox. Sweeping twice, with a gap, satisfies the rule inside one
        // invocation and keeps the verb's promise of a deterministic result.
        var delivered = carrier.PollOnce();
        Thread.Sleep(250);
        delivered += carrier.PollOnce();
        machine.WaitForNotifications(TimeSpan.FromSeconds(15));

        Console.WriteLine($"ynet_client: polled {carrier.InboxDirectory}");
        Console.WriteLine($"ynet_client: delivered={delivered}   received={machine.MessagesReceived}   " +
                          $"pending_now={spool.Count}   strays={carrier.StrayCount}   " +
                          $"returned_for_retry={carrier.UndurableReturned}");
        if (unexaminable)
        {
            Console.Error.WriteLine(
                "ynet_client: this run is UNEXAMINABLE, not quiet — the count above is not evidence " +
                "that nothing arrived.");
            return 5;
        }
        return 0;
    }

    case "send":
    {
        var to = Opt("--to");
        if (to is null)
        {
            Console.Error.WriteLine("usage: ynet_client send --to <node>/<actor> [--signal S] [--body B]");
            return 1;
        }

        var rawSelf = Opt("--self") ?? Environment.GetEnvironmentVariable("YNET_SELF");
        if (string.IsNullOrWhiteSpace(rawSelf))
        {
            Console.Error.WriteLine("ynet_client: sending requires --self <node>/<actor> or YNET_SELF");
            return 2;
        }

        var peer = PeerIdentity.Parse(to);

        // 🔴 SENDING CAN NOW USE THE WIRE. Until 2026-09-06 this verb constructed CoopFileOutbound
        // unconditionally, so QuicOutbound — written, tested and merged — was unreachable from the
        // only verb that sends. M6 requires this client to SEND as well as receive; on the file
        // plane alone it could only answer peers who share a mounted volume with it.
        var sendPlane = PlaneCatalog.Parse(
            Opt("--plane") ?? Environment.GetEnvironmentVariable("YNET_CLIENT_PLANE"));

        if (sendPlane == PlaneCatalog.Plane.Wire)
        {
            var remoteText = Opt("--peer-addr");
            if (string.IsNullOrWhiteSpace(remoteText))
            {
                // Refuse rather than quietly sending on the file plane. Silently substituting a
                // plane is the defect this feature exists to close; doing it in the SEND path would
                // deliver to a mailbox the operator did not choose and report success.
                Console.Error.WriteLine(
                    "ynet_client: sending on the wire needs the peer's address — pass " +
                    "--peer-addr <ip:port>. Refusing to fall back to the file plane silently: a " +
                    "send that lands somewhere you did not choose and reports success is worse " +
                    "than a refusal.");
                return 7;
            }
            // 🔴 2026-09-08 (olamnit.glpnet, engineer-authorised; C-18 claim CLAIM-20260908T0555Z).
            // The message this block used to print said the signing identity "is the remaining
            // step". MEASURED: THAT WAS AN UNDERCOUNT, and it is worth correcting rather than
            // quietly fixing. QuicOutbound's constructor takes FOUR things, and the identity was
            // only one of them: (NodeIdentity self, NodeId peerNode, PeerIdentity peer,
            // IPEndPoint remote). `self` is now minted exactly as the receive path mints it, and
            // `remote` is --peer-addr. But `peerNode` — the peer's NodeId — is resolved by NOTHING
            // in this fleet today. That is the Resolve half of
            // `ynet-minted-lane-identity-resolve-address-independent`, and it is unbuilt.
            //
            // So the honest surface is: let an operator who KNOWS the peer's node id state it, and
            // refuse by name when they do not — rather than either inventing one or continuing to
            // report a blanket "not supported" for a route that is otherwise complete.
            var peerNodeText = Opt("--peer-node");
            if (string.IsNullOrWhiteSpace(peerNodeText))
            {
                Console.Error.WriteLine(
                    "ynet_client: wire send needs the peer's node id — pass --peer-node <id>. " +
                    "Nothing in this fleet RESOLVES a lane name to a node id yet (that is the " +
                    "Resolve half of ynet-minted-lane-identity-resolve-address-independent, and it " +
                    "is unbuilt), so this cannot be derived and will NOT be invented: a wrong peer " +
                    "node id authenticates a handshake against the wrong key and fails in a way " +
                    "that looks like a network fault. NOT SENT.");
                return 8;
            }

            if (!System.Net.IPEndPoint.TryParse(remoteText, out var remoteEp))
            {
                Console.Error.WriteLine(
                    $"ynet_client: --peer-addr '{remoteText}' is not an <ip>:<port>. NOT SENT.");
                return 7;
            }

            var selfIdentity = NodeIdentity.LoadOrMint(PeerIdentity.Parse(rawSelf).Actor, out _);
            var wireSignal = Opt("--signal") ?? "M6_MESSAGE";
            var wireBody = Opt("--body") ?? "";

            using var wire = new QuicOutbound(
                selfIdentity, new NodeId(peerNodeText), peer, remoteEp);

            // The wire plane's contract is YnetMessage, not the file plane's (signal, body)
            // convenience overload — that overload exists only on CoopFileOutbound. Origin is
            // stated in the SAME <node>/<actor> form the file plane writes, so a frame arriving
            // over either plane is attributable by the same rule.
            var wireMessage = new YnetMessage(
                MessageId: Guid.NewGuid().ToString("N"),
                Origin: PeerIdentity.Parse(rawSelf).Identity,
                Summary: wireSignal,
                Body: System.Text.Encoding.UTF8.GetBytes(wireBody));

            if (!wire.Send(wireMessage))
            {
                Console.Error.WriteLine(
                    $"ynet_client: wire send to {peer.Identity} at {remoteText} FAILED. NOT SENT — " +
                    "a real refusal, NOT a silent fall back to the file plane.");
                return 8;
            }

            Console.WriteLine($"ynet_client: sent {wireMessage.MessageId}");
            Console.WriteLine($"ynet_client: to={peer.Identity}   remote={remoteText}   plane=quic");
            return 0;
        }

        var outbound = new CoopFileOutbound(PeerIdentity.Parse(rawSelf), peer, RequiredRoot());
        if (!outbound.Send(Opt("--signal") ?? "M6_MESSAGE", Opt("--body") ?? ""))
        {
            // Fail closed and SAY SO. A send that reports success into a directory nobody reads is
            // worse than a refusal: it retires the question without delivering anything.
            Console.Error.WriteLine(
                $"ynet_client: peer '{peer.Identity}' has no mailbox at {outbound.PeerInbox} - it has " +
                "not registered one, or you are addressing a different COOP root. NOT SENT.");
            return 4;
        }

        Console.WriteLine($"ynet_client: sent {outbound.LastFrameName}");
        Console.WriteLine($"ynet_client: to={peer.Identity}   dir={peer.DirectoryName}   plane=coop-file");
        return 0;
    }

    case "doctor":
    {
        var laneDir = LaneDir();
        using var carrier = new CoopFileInbound(RequiredRoot(), laneDir);   // read-only: no Open()
        carrier.EnsureMailbox();

        var files = Directory.Exists(carrier.InboxDirectory)
            ? Directory.GetFiles(carrier.InboxDirectory)
            : Array.Empty<string>();
        var frames = files.Count(f => f.EndsWith(".frame", StringComparison.OrdinalIgnoreCase));

        Console.WriteLine("ynet_client doctor");
        Console.WriteLine($"  lane dir   {laneDir}");
        Console.WriteLine($"  inbox      {carrier.InboxDirectory}");
        Console.WriteLine($"  spool      {spool.Directory}  ({spool.Count} pending)");
        Console.WriteLine($"  waiting    {frames} frame(s), {files.Length - frames} non-frame file(s)");
        foreach (var f in files.Where(f => !f.EndsWith(".frame", StringComparison.OrdinalIgnoreCase)))
            Console.WriteLine($"    stray: {Path.GetFileName(f)}");
        // A mis-addressed mailbox is a REPORTABLE state, not a pass.
        return files.Length - frames > 0 ? 3 : 0;
    }

    case "inject":
    {
        // Drives one message through the real machine, in a real process, with no agent running,
        // and exits. Proving M6-b/M6-f needs the alert to be found by a DIFFERENT process
        // afterwards, which a test inside one process cannot show.
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: ynet_client inject <messageId> [origin] [summary]");
            return 1;
        }

        var plane = new LoopbackInbound();
        using var machine = new YnetReceiverMachine(
            plane, spool, new AgentHook(Environment.GetEnvironmentVariable("YNET_CLIENT_HOOK")));
        machine.Start();
        machine.PumpOnce();

        var msg = new YnetMessage(
            args[1],
            args.Length > 2 ? args[2] : "unknown-origin",
            args.Length > 3 ? args[3] : "(no summary)",
            ReadOnlyMemory<byte>.Empty);

        if (!plane.Deliver(msg))
        {
            Console.Error.WriteLine("ynet_client: plane refused the message");
            return 1;
        }

        machine.PumpOnce();
        // Notification is asynchronous now, so wait for it before REPORTING its outcome —
        // printing a blank outcome would be a true-looking blank rather than a truthful result.
        machine.WaitForNotifications(TimeSpan.FromSeconds(15));
        Console.WriteLine($"ynet_client: injected {msg.MessageId}   received={machine.MessagesReceived}   " +
                          $"hook={machine.LastHookAttempt?.Outcome.ToString() ?? "not-attempted"}   pending_now={spool.Count}");
        return 0;
    }

    case "pending":
    {
        var pending = spool.Undrained();
        if (pending.Count == 0)
        {
            Console.WriteLine("ynet_client: nothing pending");
            return 0;
        }

        Console.WriteLine($"ynet_client: {pending.Count} alert(s) pending in {spool.Directory}");
        foreach (var a in pending)
            Console.WriteLine($"  {a.AlertId}  from={a.Origin}  presented={a.Presentations}x  raised={a.RaisedUtc:u}  {a.Summary}");
        return 0;
    }

    case "drain":
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: ynet_client drain <alertId>");
            return 1;
        }

        // 🔴 2026-09-08 (olamnit.glpnet). This block used to call Drain(args[1]) directly and
        // `return 0` either way, printing "was not pending (already drained, or never raised)" on
        // failure. MEASURED: given the id the ALERTS HOOK ITSELF PRINTS — the MessageId — that path
        // fired every time, because PathFor rightly refuses a message id as malformed. The result
        // was a no-op that reported success in BOTH the text and the exit code, so neither an
        // operator nor a script could tell it from a real drain, while the alert sat on disk.
        //
        // Two changes, and the exit code is the more important one: an id is now resolved against
        // what is actually pending (alert id OR message id), and a drain that removed nothing exits
        // NON-ZERO. A no-op that returns success is not a diagnostic — it is a false one.
        var resolved = spool.ResolveAlertId(args[1]);
        if (resolved is null)
        {
            Console.Error.WriteLine(
                $"ynet_client: '{args[1]}' matches no pending alert, by alert id or message id. " +
                "NOTHING DRAINED. Run `ynet_client pending` to see what is actually waiting — and " +
                "note that a previously-successful drain of this id would also land here, so this " +
                "is 'no longer pending', NOT 'was already handled by you'.");
            return 5;
        }

        if (!spool.Drain(resolved))
        {
            Console.Error.WriteLine(
                $"ynet_client: {resolved} was pending a moment ago and could not be removed — " +
                "another process almost certainly drained it between the lookup and the delete. " +
                "NOTHING DRAINED BY THIS CALL.");
            return 5;
        }

        Console.WriteLine(args[1] == resolved
            ? $"ynet_client: drained {resolved}"
            : $"ynet_client: drained {resolved} (resolved from message id {args[1]})");
        return 0;
    }

    default:
        Console.WriteLine("""
            ynet_client — the glpnet M6 QHSM YNET receiver client

              run                start the receiver; runs independently of any agent
              poll               one sweep of the inbox, then exit (provable receipt)
              send --to <node>/<actor> [--signal S] [--body B]
              doctor             lane dir, mailbox paths, and non-frame strays
              pending            list alerts the agent has not yet drained
              drain <id>         mark one alert handled; takes the ALERT id or the MESSAGE id.
                                 Exits 5 and drains NOTHING when the id matches no pending alert —
                                 never 0, so a script can tell a real drain from a no-op.

            environment:
              YNET_CLIENT_SPOOL  durable alert directory (default: %LOCALAPPDATA%/glpnet/ynet-client/alerts)
              YNET_CLIENT_HOOK   command invoked as <hook> <alertId> <messageId> <origin>
            """);
        return verb == "help" ? 0 : 1;
}

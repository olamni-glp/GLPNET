// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The M6 client's control surface. Three verbs, and each one answers a question an operator or an
// agent actually asks:
//
//   run       start the receiver and keep it running, independently of any agent (M6-b, M6-d)
//   poll      one deterministic sweep of the inbox, then exit  (M6-R2, provable in a script)
//   send      deliver a frame to another lane's mailbox        (M6-R3)
//   doctor    who am I, where is my mailbox, and what is in it that is not a deliverable frame
//   pending   what is waiting for me?            - the agent's "/btw" drain queue (M6-f)
//   drain     I have handled this one            - explicit, idempotent, agent-chosen
//
// M6-d asks for the main part to be a kernel-managed native YNGENIOS process. This executable is
// the receiver in the form that runs TODAY on a host whose kernel does not yet manage it; the
// kernel-managed hosting is the next step and is stated as not-yet-done rather than implied.
//
// 2026-09-05: "run" now binds a REAL cross-lane plane (CoopFileInbound) when YNET_CLIENT_COOP and
// YNET_CLIENT_LANE are set. Before this it bound LoopbackInbound unconditionally and could only
// hear itself — a gap this lane disclosed in its own source rather than hid, and the carrier
// adapter ruled to this lane by Q-glpnetshiras-50. With no coop root configured it still falls
// back to loopback, and says so on stdout rather than looking reachable.

using Ynet.Client;

var verb = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

string? Opt(string name)
{
    for (var i = 1; i < args.Length - 1; i++)
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
    {
        var hook = new AgentHook(Environment.GetEnvironmentVariable("YNET_CLIENT_HOOK"));

        // Plane selection. YNET_CLIENT_COOP names a shared coop root; with it, this lane receives
        // real cross-lane traffic. WITHOUT it we fall back to the in-memory plane and SAY SO — a
        // receiver that can only hear itself must never be mistaken for one that is reachable.
        var coopRoot = Environment.GetEnvironmentVariable("YNET_CLIENT_COOP");
        var laneDir = Environment.GetEnvironmentVariable("YNET_CLIENT_LANE");
        IYnetInbound plane;
        CoopFileInbound? coop = null;

        if (!string.IsNullOrWhiteSpace(coopRoot) && !string.IsNullOrWhiteSpace(laneDir))
        {
            coop = new CoopFileInbound(coopRoot, laneDir);
            coop.StrayObserved += p => Console.Error.WriteLine($"ynet_client: STRAY (not a .frame, not delivered): {p}");
            coop.PollFailed += ex => Console.Error.WriteLine($"ynet_client: poll failed: {ex.GetType().Name}: {ex.Message}");
            plane = coop;
        }
        else
        {
            plane = new LoopbackInbound();
        }

        using var machine = new YnetReceiverMachine(plane, spool, hook);

        machine.Faulted += ex => Console.Error.WriteLine($"machine fault: {ex.GetType().Name}: {ex.Message}");
        machine.Launch("ynet-client-receiver");

        Console.WriteLine($"ynet_client: receiver running   plane={plane.PlaneName}   spool={spool.Directory}");
        if (coop is not null)
            Console.WriteLine($"ynet_client: inbox={coop.InboxDirectory}");
        else
            Console.WriteLine("ynet_client: NO CROSS-LANE PLANE — set YNET_CLIENT_COOP and YNET_CLIENT_LANE. " +
                              "This receiver can only hear messages this process makes for itself.");
        Console.WriteLine($"ynet_client: hook={(hook.IsConfigured ? "configured" : "NOT configured (durable-only)")}");
        Console.WriteLine($"ynet_client: {spool.Count} alert(s) already pending from earlier runs");
        Console.WriteLine("ynet_client: the agent is not required; press Ctrl+C to stop.");

        var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        stop.Wait();

        machine.Post(new Ynet.Client.Machine.QEvt(YnetSignal.Stop));
        Thread.Sleep(200);
        Console.WriteLine($"ynet_client: stopped   received={machine.MessagesReceived}   pending={spool.Count}" +
                          (coop is not null ? $"   strays={coop.StrayCount}" : string.Empty));
        coop?.Dispose();
        return 0;
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
        machine.Start();
        machine.PumpOnce();

        carrier.EnsureMailbox();
        var delivered = carrier.PollOnce();
        machine.PumpOnce();
        machine.WaitForNotifications(TimeSpan.FromSeconds(15));

        Console.WriteLine($"ynet_client: polled {carrier.InboxDirectory}");
        Console.WriteLine($"ynet_client: delivered={delivered}   received={machine.MessagesReceived}   " +
                          $"pending_now={spool.Count}   strays={carrier.StrayCount}   " +
                          $"returned_for_retry={carrier.UndurableReturned}");
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
        Console.WriteLine($"ynet_client: to={peer.Identity}   dir={peer.DirectoryName}");
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

        var removed = spool.Drain(args[1]);
        Console.WriteLine(removed
            ? $"ynet_client: drained {args[1]}"
            : $"ynet_client: {args[1]} was not pending (already drained, or never raised)");
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
              drain <alertId>    mark one alert handled (idempotent)

            environment:
              YNET_CLIENT_SPOOL  durable alert directory (default: %LOCALAPPDATA%/glpnet/ynet-client/alerts)
              YNET_CLIENT_HOOK   command invoked as <hook> <alertId> <messageId> <origin>
            """);
        return verb == "help" ? 0 : 1;
}

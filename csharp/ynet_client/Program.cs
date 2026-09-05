// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The M6 client's control surface. Three verbs, and each one answers a question an operator or an
// agent actually asks:
//
//   run       start the receiver and keep it running, independently of any agent (M6-b, M6-d)
//   pending   what is waiting for me?            — the agent's "/btw" drain queue (M6-f)
//   drain     I have handled this one            — explicit, idempotent, agent-chosen
//
// M6-d asks for the main part to be a kernel-managed native YNGENIOS process. This executable is
// the receiver in the form that runs TODAY on a host whose kernel does not yet manage it; the
// kernel-managed hosting is the next step and is stated as not-yet-done rather than implied.

using Ynet.Client;

var verb = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
var spool = new PendingAlertSpool(
    Environment.GetEnvironmentVariable("YNET_CLIENT_SPOOL") ?? PendingAlertSpool.DefaultDirectory);

switch (verb)
{
    case "run":
    {
        var hook = new AgentHook(Environment.GetEnvironmentVariable("YNET_CLIENT_HOOK"));
        var plane = new LoopbackInbound();
        using var machine = new YnetReceiverMachine(plane, spool, hook);

        machine.Faulted += ex => Console.Error.WriteLine($"machine fault: {ex.GetType().Name}: {ex.Message}");
        machine.Launch("ynet-client-receiver");

        Console.WriteLine($"ynet_client: receiver running   plane={plane.PlaneName}   spool={spool.Directory}");
        Console.WriteLine($"ynet_client: hook={(hook.IsConfigured ? "configured" : "NOT configured (durable-only)")}");
        Console.WriteLine($"ynet_client: {spool.Count} alert(s) already pending from earlier runs");
        Console.WriteLine("ynet_client: the agent is not required; press Ctrl+C to stop.");

        var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        stop.Wait();

        machine.Post(new Ynet.Client.Machine.QEvt(YnetSignal.Stop));
        Thread.Sleep(200);
        Console.WriteLine($"ynet_client: stopped   received={machine.MessagesReceived}   pending={spool.Count}");
        return 0;
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
              pending            list alerts the agent has not yet drained
              drain <alertId>    mark one alert handled (idempotent)

            environment:
              YNET_CLIENT_SPOOL  durable alert directory (default: %LOCALAPPDATA%/glpnet/ynet-client/alerts)
              YNET_CLIENT_HOOK   command invoked as <hook> <alertId> <messageId> <origin>
            """);
        return verb == "help" ? 0 : 1;
}

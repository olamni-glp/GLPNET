// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The M6 client's control surface. Each verb answers a question an operator or an agent actually
// asks, and the two halves of M6 are now BOTH present:
//
//   run       start the receiver and keep it running, independently of any agent   (M6-R2, M6-b/d)
//   poll      one deterministic sweep of the inbox, then exit                      (M6-R2, provable)
//   send      deliver a frame to another lane's mailbox                            (M6-R3)
//   pending   what is waiting for me?          — the agent's "/btw" drain queue     (M6-f)
//   drain     I have handled this one          — explicit, idempotent, agent-chosen (M6-f)
//   doctor    who am I, where is my mailbox, and what is sitting in it that is not a frame
//
// WHAT CHANGED AND WHY IT MATTERS
//     Before CoopFileCarrier.cs, `run` bound LoopbackInbound and `inject` manufactured its own
//     message. Both halves ran green while the client could not receive a byte another process had
//     written — a suite passing against a fiction the production path does not share. This lane
//     reported that as NOT MET rather than counting it. `run --plane coop` is the real plane; the
//     loopback stays because the intra-host kernel intercom is genuinely in-memory, and because a
//     fault-injectable double must be the same code path as production.
//
// M6-d asks for the main part to be a kernel-managed native YNGENIOS process. This executable is
// the receiver in the form that runs TODAY on a host whose kernel does not yet manage it; the
// kernel-managed hosting is the next step and is stated as not-yet-done rather than implied.

using Ynet.Client;

var verb = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

string? Opt(string name)
{
    for (var i = 1; i < args.Length - 1; i++)
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    return null;
}

string? CoopRoot() => Opt("--coop") ?? Environment.GetEnvironmentVariable(CoopLayout.RootVariable);

// The lane's own address. Refused rather than defaulted: a client that invents its own identity
// binds a mailbox nobody is sending to, and then reports "running" forever.
PeerIdentity Self()
{
    var raw = Opt("--self")
              ?? Environment.GetEnvironmentVariable("YNET_SELF")
              ?? throw new InvalidOperationException(
                  "this lane's identity is not set. Pass --self <node>/<actor> or set YNET_SELF. " +
                  "Refusing to invent one: an invented identity binds a mailbox no peer addresses.");
    return PeerIdentity.Parse(raw);
}

var spool = new PendingAlertSpool(
    Environment.GetEnvironmentVariable("YNET_CLIENT_SPOOL") ?? PendingAlertSpool.DefaultDirectory);

try
{
    switch (verb)
    {
        case "run":
        {
            var hook = new AgentHook(Environment.GetEnvironmentVariable("YNET_CLIENT_HOOK"));
            var useLoopback = string.Equals(Opt("--plane"), "loopback", StringComparison.OrdinalIgnoreCase);

            IYnetInbound plane;
            CoopFileInbound? carrier = null;
            if (useLoopback)
            {
                plane = new LoopbackInbound();
            }
            else
            {
                carrier = new CoopFileInbound(Self(), CoopRoot());
                plane = carrier;
            }

            using var machine = new YnetReceiverMachine(plane, spool, hook);
            machine.Faulted += ex => Console.Error.WriteLine($"machine fault: {ex.GetType().Name}: {ex.Message}");
            machine.Launch("ynet-client-receiver");

            Console.WriteLine($"ynet_client: receiver running   plane={plane.PlaneName}   spool={spool.Directory}");
            if (carrier is not null)
            {
                Console.WriteLine($"ynet_client: identity={Self().Identity}   inbox={carrier.InboxDirectory}");
            }
            Console.WriteLine($"ynet_client: hook={(hook.IsConfigured ? "configured" : "NOT configured (durable-only)")}");
            Console.WriteLine($"ynet_client: {spool.Count} alert(s) already pending from earlier runs");
            Console.WriteLine("ynet_client: the agent is not required; press Ctrl+C to stop.");

            var stop = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
            stop.Wait();

            machine.Post(new Ynet.Client.Machine.QEvt(YnetSignal.Stop));
            Thread.Sleep(200);
            var strays = carrier?.StrayFiles ?? Array.Empty<string>();
            Console.WriteLine($"ynet_client: stopped   received={machine.MessagesReceived}   " +
                              $"pending={spool.Count}   strays={strays.Count}");
            foreach (var s in strays) Console.WriteLine($"  stray (not a frame, NOT delivered): {s}");
            carrier?.Dispose();
            return 0;
        }

        case "poll":
        {
            // One sweep, then exit. This is what makes M6-R2 PROVABLE rather than asserted: another
            // process writes a frame, this process finds it, and the alert outlives both.
            var hook = new AgentHook(Environment.GetEnvironmentVariable("YNET_CLIENT_HOOK"));
            using var carrier = CoopFileInbound.Manual(Self(), CoopRoot());
            using var machine = new YnetReceiverMachine(carrier, spool, hook);
            machine.Start();
            machine.PumpOnce();

            carrier.Open();
            var delivered = carrier.PollOnce();
            machine.PumpOnce();
            machine.WaitForNotifications(TimeSpan.FromSeconds(15));
            carrier.Close();

            Console.WriteLine($"ynet_client: polled {carrier.InboxDirectory}");
            Console.WriteLine($"ynet_client: delivered={delivered}   received={machine.MessagesReceived}   " +
                              $"pending_now={spool.Count}   strays={carrier.StrayFiles.Count}");
            foreach (var s in carrier.StrayFiles)
                Console.WriteLine($"  stray (not a frame, NOT delivered): {s}");
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

            var peer = PeerIdentity.Parse(to);
            var outbound = new CoopFileOutbound(Self(), peer, CoopRoot());
            if (!outbound.Send(Opt("--signal") ?? "M6_MESSAGE", Opt("--body") ?? ""))
            {
                // Fail closed and SAY SO. A send that reports success into a directory nobody reads
                // is worse than a refusal: it retires the question without delivering anything.
                Console.Error.WriteLine(
                    $"ynet_client: peer '{peer.Identity}' has no mailbox at {outbound.PeerInbox} — " +
                    "it has not registered one, or you are addressing a different COOP root. NOT SENT.");
                return 4;
            }

            Console.WriteLine($"ynet_client: sent {outbound.LastFrameName}");
            Console.WriteLine($"ynet_client: to={peer.Identity}   dir={peer.DirectoryName}");
            return 0;
        }

        case "doctor":
        {
            var self = Self();
            using var carrier = CoopFileInbound.Manual(self, CoopRoot());
            carrier.Open();
            // Report what is there WITHOUT consuming it: doctor must be safe to run at any time.
            var inboxFiles = Directory.Exists(carrier.InboxDirectory)
                ? Directory.EnumerateFiles(carrier.InboxDirectory).Select(Path.GetFileName).ToArray()
                : Array.Empty<string?>();
            carrier.Close();

            Console.WriteLine($"ynet_client doctor");
            Console.WriteLine($"  identity   {self.Identity}");
            Console.WriteLine($"  directory  {self.DirectoryName}");
            Console.WriteLine($"  inbox      {carrier.InboxDirectory}");
            Console.WriteLine($"  processed  {carrier.ProcessedDirectory}");
            Console.WriteLine($"  spool      {spool.Directory}  ({spool.Count} pending)");
            var frames = inboxFiles.Count(f => f is not null && f.EndsWith(".frame", StringComparison.Ordinal));
            var strays = inboxFiles.Length - frames;
            Console.WriteLine($"  waiting    {frames} frame(s), {strays} non-frame file(s)");
            foreach (var f in inboxFiles.Where(f => f is not null && !f.EndsWith(".frame", StringComparison.Ordinal)))
                Console.WriteLine($"    stray: {f}");
            return strays > 0 ? 3 : 0;      // a mis-addressed mailbox is a REPORTABLE state, not a pass
        }

        case "inject":
        {
            // Drives one message through the real machine, in a real process, with no agent running,
            // and exits. Retained for the in-memory intercom plane; it proves the machine and spool,
            // NOT the carrier — use `send` + `poll` across two processes for that.
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
                ynet_client — the glpnet M6 QHSM YNET client (sends AND receives, with no agent)

                  run [--plane coop|loopback]   start the receiver; runs independently of any agent
                  poll                          one sweep of the inbox, then exit (provable receipt)
                  send --to <node>/<actor>      deliver a frame to another lane's mailbox
                       [--signal S] [--body B]
                  doctor                        identity, mailbox paths, and non-frame strays
                  pending                       list alerts the agent has not yet drained
                  drain <alertId>               mark one alert handled (idempotent)

                addressing:
                  --self <node>/<actor>  or  YNET_SELF     this lane's address (never defaulted)
                  --coop <root>          or  YNET_COOP_ROOT the COOP root holding peer mailboxes

                environment:
                  YNET_CLIENT_SPOOL  durable alert directory (default: %LOCALAPPDATA%/glpnet/ynet-client/alerts)
                  YNET_CLIENT_HOOK   command invoked as <hook> <alertId> <messageId> <origin>
                """);
            return verb == "help" ? 0 : 1;
    }
}
catch (Exception ex) when (ex is InvalidOperationException or FormatException
                               or DirectoryNotFoundException)
{
    // These are the operator's mistakes, not crashes: say what is wrong in one line and exit 2, so
    // a script can tell "you addressed nobody" apart from "the client is broken".
    Console.Error.WriteLine($"ynet_client: {ex.Message}");
    return 2;
}

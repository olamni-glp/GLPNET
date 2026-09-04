// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// ynet-federation — the operator console (feature 102, T041/T042).
//
// Contract federation-config.md G2/G5/G6, federation-status.md S7 / FR-002, FR-019, FR-025.
//
// Every verb that CHANGES anything records its reversal in the same breath (FR-025). The reversal is
// DATA, not documentation: a runbook line saying "and to undo it, remove the rule" is a reversal
// nobody can execute six weeks later on a host they did not configure.

using System.Net;
using System.Text.Json;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Federation;
using GlpRuntime.CrdtMsg.Route;

namespace Ynet.Federation;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0) { Usage(); return 1; }

        try
        {
            return args[0] switch
            {
                "status" => Status(),
                "config" => Config(args),
                "identity" => Identity(args),
                "epoch" => Epoch(args),
                "post" => await Post(args),
                "retire" => await Retire(args),
                "serve" => await Serve(),
                "revert" => Revert(args),
                _ => Unknown(args[0]),
            };
        }
        catch (Exception ex) when (PolicyRefusal.Detect(ex) is { } refusal)
        {
            // FR-023: named, not generalised. This failure presents as a healthy build and a passing
            // test suite followed by a daemon that never runs, so a generic error costs hours.
            Console.Error.WriteLine($"REFUSED BY HOST POLICY: {refusal.Policy} (0x{refusal.HResult:X8})");
            Console.Error.WriteLine($"  {refusal.Detail}");
            Console.Error.WriteLine("  This is a host software policy refusal, NOT a build or transport failure.");
            Console.Error.WriteLine("  Run via `dotnet run` (the signed host). Durable fix: code-signing in");
            Console.Error.WriteLine("  `buildkit ship` — ruling Q-GLPNETG27-02 declined disabling the protection as one-way.");
            return 3;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
    }

    // ---- status ------------------------------------------------------------------------------

    private static int Status()
    {
        var cfg = FederationConfig.Load();
        var peers = cfg.ToPeerSet();

        var reasons = new Dictionary<string, string>();
        if (peers.AdmitsNobody) reasons["peer admitted"] = peers.WhyNotAdmitted();

        // Four INDEPENDENT measurements. Nothing here infers one state from another (FR-020).
        var status = new FederationStatus
        {
            StackSupported = FederationStatusProbe.MeasureStackSupported(),
            ListenerBound = Tri.No,          // this process is not the serving one
            PeerAdmitted = peers.AdmitsNobody ? Tri.No : Tri.Unknown,
            OpReceivedFromPeer = Tri.Unknown, // not measurable from outside the serving process
            Reasons = reasons,
        };

        Console.Write(status.Render());

        if (!cfg.Enabled)
            Console.WriteLine("\nfederation is DISABLED in configuration — local lanes are served normally (FR-004).");

        var problems = cfg.Validate();
        if (problems.Count > 0)
        {
            Console.WriteLine("\nconfiguration REFUSED:");
            foreach (var p in problems) Console.WriteLine($"  ! {p}");
            return 1;
        }
        return 0;
    }

    // ---- config ------------------------------------------------------------------------------

    private static int Config(string[] a)
    {
        if (a.Length < 2) { Console.Error.WriteLine("usage: config show|set <key> <value>|add-peer ..."); return 1; }
        var cfg = FederationConfig.Load();

        switch (a[1])
        {
            case "show":
                Console.Write(cfg.RenderEffective());
                Console.WriteLine($"path                  : {FederationConfig.DefaultPath()}");
                return cfg.IsValid ? 0 : 1;

            case "set":
            {
                if (a.Length < 4) { Console.Error.WriteLine("usage: config set <key> <value>"); return 1; }
                string prior = JsonSerializer.Serialize(cfg);
                var next = a[2] switch
                {
                    "enabled" => cfg with { Enabled = bool.Parse(a[3]) },
                    "bind_address" => cfg with { BindAddress = a[3] },
                    "bind_port" => cfg with { BindPort = int.Parse(a[3]) },
                    "space_id" => cfg with { SpaceId = a[3] },
                    "pull_interval_seconds" => cfg with { PullIntervalSeconds = int.Parse(a[3]) },
                    "push_on_append" => cfg with { PushOnAppend = bool.Parse(a[3]) },
                    _ => throw new ArgumentException($"unknown key '{a[2]}'"),
                };
                next.Save();
                Ledger().Record($"config set {a[2]}={a[3]}", "restore the recorded prior config", "enable/adjust federation", prior);

                // READ BACK, always. Write-only configuration cannot be verified and therefore
                // cannot be trusted (contract G2).
                Console.Write(FederationConfig.Load().RenderEffective());
                return 0;
            }

            case "add-peer":
            {
                string? name = Arg(a, "--name"), nodeId = Arg(a, "--node-id");
                var endpoints = Args(a, "--endpoint");
                if (name is null || nodeId is null || endpoints.Count == 0)
                {
                    Console.Error.WriteLine("usage: config add-peer --name <n> --node-id <hex> --endpoint <ip:port> [--endpoint ...]");
                    return 1;
                }
                string prior = JsonSerializer.Serialize(cfg);
                cfg.Peers.RemoveAll(p => p.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                cfg.Peers.Add(new PeerConfig { Name = name, NodeId = nodeId, Endpoints = endpoints, Pin = nodeId });
                cfg.Save();
                Ledger().Record($"config add-peer {name} [{nodeId}]", "restore the recorded prior config", "admit a federation peer", prior);

                Console.WriteLine($"peer '{name}' added with {endpoints.Count} endpoint(s) — ONE participant regardless (FR-007).");
                Console.Write(FederationConfig.Load().RenderEffective());
                return 0;
            }

            default:
                Console.Error.WriteLine($"unknown config verb '{a[1]}'");
                return 1;
        }
    }

    // ---- identity ----------------------------------------------------------------------------

    private static int Identity(string[] a)
    {
        var store = new NodeIdentityStore(NodeIdentityStore.DefaultPath());
        bool existed = store.Exists;
        var cert = store.LoadOrMint(Environment.MachineName.ToLowerInvariant());
        string nodeId = NodeIdentityStore.DeriveNodeId(cert);

        if (!existed)
            Ledger().Record("minted node.key", $"delete {NodeIdentityStore.DefaultPath()}", "a stable node id that survives restarts");

        Console.WriteLine($"node_id : {nodeId}");
        Console.WriteLine($"key     : {NodeIdentityStore.DefaultPath()}{(existed ? " (existing)" : " (minted)")}");
        Console.WriteLine();
        Console.WriteLine("Publish the node_id to peers. It is stable BECAUSE it is persisted — a pin read");
        Console.WriteLine("from a probe run is ephemeral and must never be published as stable.");
        return 0;
    }

    // ---- epoch -------------------------------------------------------------------------------

    private static int Epoch(string[] a)
    {
        if (a.Length < 2 || a[1] != "mint") { Console.Error.WriteLine("usage: epoch mint --rationale <why>"); return 1; }
        string rationale = Arg(a, "--rationale") ?? "";
        if (string.IsNullOrWhiteSpace(rationale))
        {
            Console.Error.WriteLine("epoch mint requires --rationale: minting is a RECORDED operator action (FR-026).");
            return 1;
        }

        string epochId = $"ynet-epoch-{DateTimeOffset.UtcNow:yyyy-MM}-{Guid.NewGuid().ToString("n")[..6]}";
        if (TermSpaceRegistry.LooksClockDerived(epochId))
        {
            Console.Error.WriteLine("refusing a clock-derived epoch id — that is how the fossil term was born (FR-015).");
            return 1;
        }

        var cfg = FederationConfig.Load();
        string prior = JsonSerializer.Serialize(cfg);
        (cfg with { SpaceId = epochId }).Save();
        Ledger().Record($"epoch mint {epochId}", "restore the recorded prior config", rationale, prior);

        Console.WriteLine($"space_id : {epochId}");
        Console.WriteLine("Set the SAME space_id on the peer. Different space_ids are not an error — they");
        Console.WriteLine("mean the two hosts' terms are incomparable and no leadership decision can be made.");
        Console.WriteLine("Prior-epoch operations remain readable and attributed (SC-013).");
        return 0;
    }

    // ---- post / retire / serve ----------------------------------------------------------------

    private static async Task<int> Post(string[] a)
    {
        string body = Arg(a, "--body") ?? "";
        var (cfg, svc, nodeId) = await Open();
        await using var _ = svc;

        long counter = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();   // a DOT counter, never a TERM
        var op = FederationOp.Create(new Dot(nodeId, counter), nodeId, "board_post",
            JsonSerializer.SerializeToElement(new { body, lane = Environment.MachineName }));

        await svc.AppendAndPushAsync(op);
        Console.WriteLine($"appended {op.OpId} locally, then pushed (append-then-ship, FR-030).");
        return 0;
    }

    private static async Task<int> Retire(string[] a)
    {
        string? target = Arg(a, "--op"), reason = Arg(a, "--reason");
        if (target is null || string.IsNullOrWhiteSpace(reason))
        {
            Console.Error.WriteLine("usage: retire --op <peer:counter> --reason <why>");
            Console.Error.WriteLine("  The target is NEVER deleted — a superseding op assigns it to the legacy");
            Console.Error.WriteLine("  space, where FR-014 makes it incomparable to every live term (FR-017/029).");
            return 1;
        }

        var (cfg, svc, nodeId) = await Open();
        await using var _ = svc;

        var op = RetirementOp.Create(
            new Dot(nodeId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), nodeId, Dot.Parse(target), reason);
        await svc.AppendAndPushAsync(op);

        Console.WriteLine($"retired {target} into the legacy space by appending {op.OpId}.");
        Console.WriteLine("The target is STILL PRESENT in the log — removal is indistinguishable from suppression.");
        return 0;
    }

    private static async Task<int> Serve()
    {
        var (cfg, svc, nodeId) = await Open();
        await using var _ = svc;

        if (!await svc.BindAsync())
        {
            Console.Error.WriteLine("listener did NOT bind. Run `status` — the four states are reported separately.");
            return 1;
        }

        Console.Write(svc.Status().Render());
        foreach (var p in cfg.Peers)
        {
            var outcome = await svc.DialAsync(p.Name);
            Console.WriteLine($"dial {p.Name,-12}: {outcome}");
        }

        Console.WriteLine($"\nserving. push_on_append={cfg.PushOnAppend}, pull every {cfg.PullIntervalSeconds}s. Ctrl-C to stop.");
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
        try { while (!stop.IsCancellationRequested) await svc.ReceiveOneAsync(stop.Token); }
        catch (OperationCanceledException) { }
        return 0;
    }

    // ---- revert ------------------------------------------------------------------------------

    private static int Revert(string[] a)
    {
        var plan = Ledger().ReversalPlan();
        if (plan.Count == 0) { Console.WriteLine("nothing recorded to revert."); return 0; }

        Console.WriteLine($"{plan.Count} recorded change(s), newest first — reverse order matters:\n");
        foreach (var c in plan)
        {
            Console.WriteLine($"  [{c.Utc}] {c.What}");
            Console.WriteLine($"      undo: {c.Reversal}");
            if (c.Prior is not null) Console.WriteLine($"      prior state is recorded and restorable ({c.Prior.Length} bytes)");
        }

        if (!a.Contains("--all"))
        {
            Console.WriteLine("\nDry run. Re-run with --all to apply the config reversals.");
            Console.WriteLine("The firewall rule is reversed OUT OF BAND (it needs elevation):");
            Console.WriteLine("  Remove-NetFirewallRule -DisplayName 'ynet-federation-quic-udp-47890'");
            return 0;
        }

        foreach (var c in plan)
        {
            if (c.Prior is not null)
            {
                var restored = JsonSerializer.Deserialize<FederationConfig>(c.Prior);
                restored?.Save();
                Console.WriteLine($"restored config prior to: {c.What}");
            }
            else if (c.What.StartsWith("minted node.key"))
            {
                try { File.Delete(NodeIdentityStore.DefaultPath()); Console.WriteLine("deleted node.key"); }
                catch (Exception ex) { Console.Error.WriteLine($"could not delete node.key: {ex.Message}"); }
            }
        }
        Console.WriteLine("\nConfig reversals applied. The firewall rule still needs the elevated one-liner above.");
        return 0;
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static async Task<(FederationConfig, FederationService, string)> Open()
    {
        var cfg = FederationConfig.Load();
        var store = new NodeIdentityStore(NodeIdentityStore.DefaultPath());
        var cert = store.LoadOrMint(Environment.MachineName.ToLowerInvariant());
        string nodeId = NodeIdentityStore.DeriveNodeId(cert);

        var transport = new QuicLinkTransport(nodeId, cert, cfg.ToPeerSet().ToPinTable());
        var fold = new FederationFold(new TermSpaceRegistry(
            string.IsNullOrWhiteSpace(cfg.SpaceId) ? "ynet-epoch-unset" : cfg.SpaceId));

        string logPath = Path.Combine(Path.GetDirectoryName(FederationConfig.DefaultPath())!,
                                      $"{Environment.MachineName.ToLowerInvariant()}-board-000001.jsonl");
        var log = new JsonlBoardLog(logPath);

        // Replay the local log so the fold starts where the last process left off.
        foreach (var op in await log.ReadAllAsync()) fold.Apply(op);

        return (cfg, new FederationService(cfg, new QuicFederationLink(transport), fold, log), nodeId);
    }

    private static ChangeLedger Ledger() => new(ChangeLedger.DefaultPath());

    private static string? Arg(string[] a, string flag)
    {
        int i = Array.IndexOf(a, flag);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }

    private static List<string> Args(string[] a, string flag)
    {
        var outp = new List<string>();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == flag) outp.Add(a[i + 1]);
        return outp;
    }

    private static int Unknown(string verb)
    {
        Console.Error.WriteLine($"unknown verb '{verb}'");
        Usage();
        return 1;
    }

    private static void Usage()
    {
        Console.WriteLine("""
            ynet-federation — operator console for the ynet federation transport

              status                                        four states, separately reported
              config show                                   the EFFECTIVE configuration
              config set <key> <value>                      then reads it back for verification
              config add-peer --name --node-id --endpoint    several --endpoint = ONE participant
              identity init                                 mint/load the persisted node id
              epoch mint --rationale <why>                   a recorded operator action
              serve                                         bind, dial peers, receive
              post --body <text>                            append locally, then push
              retire --op <peer:counter> --reason <why>      append a superseding op; never delete
              revert [--all]                                 replay the recorded reversals

            Run via `dotnet run` — Smart App Control blocks unsigned apphosts on this host.
            """);
    }
}

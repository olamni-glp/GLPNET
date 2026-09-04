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
//
// FOUR THINGS THE ROUND-2 REVIEW FOUND HERE, ALL OF THE SAME SHAPE — the console was internally
// consistent and matched the runbook only if you never ran two commands at once:
//
//   1. `add-peer` wrote the hex NODE ID into the pin field, which the TLS callback compares against
//      base64. Every correctly-configured peer was refused before federation could start.
//   2. `post` opened its own service, never bound or dialled it, and pushed to an empty admitted
//      set — so it wrote locally and reached nobody, while the running daemon never heard of it.
//   3. `status` read only configuration, so while `serve` was genuinely federating it reported
//      `listener bound: No` — the runbook's expected output unreachable by the runbook's procedure.
//   4. The board log was a private file under the config directory, so real lane claims never
//      entered the fold and federated ops never reached the oracle the lanes read: a second oracle.

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
        catch (BoardRootException ex)
        {
            // Named separately: attaching to the wrong root is how a second board gets created, and
            // a generic "error:" here would invite the operator to work around it.
            Console.Error.WriteLine($"BOARD ROOT REFUSED: {ex.Message}");
            return 4;
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
        var now = DateTimeOffset.UtcNow;

        // READ THE SERVING PROCESS'S OWN MEASUREMENT. This command is a separate process from
        // `serve`, so it can measure the stack and the configuration itself but NOT the listener,
        // the admitted peers or a crossing. Reporting those as No from here was reporting an
        // unmeasured state as a measured negative — the exact FR-021 violation.
        var heartbeat = StatusHeartbeat.ReadFresh(now);
        var stack = FederationStatusProbe.MeasureStackSupported();

        FederationStatus status;
        if (heartbeat is not null)
        {
            status = heartbeat.ToStatus(stack);
            Console.Write(status.Render());
            Console.WriteLine($"\nsource: serving process pid {heartbeat.Pid}, measured "
                              + $"{(int)heartbeat.AgeAt(now).TotalSeconds}s ago; fold holds {heartbeat.FoldOperations} operation(s).");
        }
        else
        {
            var reasons = new Dictionary<string, string>
            {
                ["listener bound"] = "no serving process is publishing a current measurement",
                ["op received from peer"] = "not measurable from outside the serving process",
            };
            if (peers.AdmitsNobody) reasons["peer admitted"] = peers.WhyNotAdmitted();

            status = new FederationStatus
            {
                StackSupported = stack,
                // UNKNOWN, not No. This process did not measure them, and an unmeasured state is
                // not a negative result (FR-021). Only the empty peer set is a genuine measured No:
                // it is read from configuration, which this process CAN see.
                ListenerBound = Tri.Unknown,
                PeerAdmitted = peers.AdmitsNobody ? Tri.No : Tri.Unknown,
                OpReceivedFromPeer = Tri.Unknown,
                Reasons = reasons,
            };
            Console.Write(status.Render());
            Console.WriteLine($"\nsource: configuration only — no fresh measurement at {StatusHeartbeat.DefaultPath()}.");
            Console.WriteLine($"        A record older than {(int)StatusHeartbeat.Freshness.TotalSeconds}s is treated as no");
            Console.WriteLine("        measurement at all: a file written by a process that has since been killed");
            Console.WriteLine("        is how a dead daemon reports itself healthy. Run `serve` in another terminal.");
        }

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
                    "identity_path" => cfg with { IdentityPath = a[3] },
                    "board_root" => cfg with { BoardRootPath = a[3] },
                    "board_actor" => cfg with { BoardActor = a[3] },
                    "write_into_lane_segment" => cfg with { WriteIntoLaneSegment = bool.Parse(a[3]) },
                    "require_verified_attribution" => cfg with { RequireVerifiedAttribution = bool.Parse(a[3]) },
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
                string? name = Arg(a, "--name"), nodeId = Arg(a, "--node-id"), spki = Arg(a, "--spki");
                var endpoints = Args(a, "--endpoint");
                if (name is null || nodeId is null || endpoints.Count == 0)
                {
                    Console.Error.WriteLine("usage: config add-peer --name <n> --node-id <hex> --endpoint <ip:port> [--endpoint ...] [--spki <base64>]");
                    return 1;
                }

                if (!NodeIdentityStore.IsNodeId(nodeId))
                {
                    Console.Error.WriteLine($"--node-id '{nodeId}' is not 64 hex characters.");
                    Console.Error.WriteLine("  A node id is SHA-256(SPKI) in hex — the value `identity` prints on the peer.");
                    return 1;
                }

                // DERIVE the pin; never assign the node id to it. They are the same 32 bytes in two
                // encodings (hex here, base64 in the TLS callback), and assigning one to the other
                // refuses every correct peer while presenting the refusal as a pin mismatch.
                string pin = NodeIdentityStore.PinFromNodeId(nodeId);

                if (spki is not null && !string.Equals(NodeIdentityStore.NodeIdFromSpki(spki), nodeId,
                                                      StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine("--spki does not hash to --node-id — this key does not belong to this participant.");
                    return 1;
                }

                string prior = JsonSerializer.Serialize(cfg);
                cfg.Peers.RemoveAll(p => p.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                cfg.Peers.Add(new PeerConfig
                {
                    Name = name,
                    NodeId = nodeId,
                    Endpoints = endpoints,
                    Pin = pin,
                    Spki = spki ?? "",
                });
                cfg.Save();
                Ledger().Record($"config add-peer {name} [{nodeId}]", "restore the recorded prior config", "admit a federation peer", prior);

                Console.WriteLine($"peer '{name}' added with {endpoints.Count} endpoint(s) — ONE participant regardless (FR-007).");
                Console.WriteLine($"pin derived from node id: {pin}");
                if (spki is null)
                    Console.WriteLine("no --spki given: this peer's operations will fold as UNVERIFIED ORIGIN (self-declared attribution).");
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
        var cfg = FederationConfig.Load();
        // HONOUR identity_path. Ignoring it meant a deployment that pre-provisions a key silently
        // federated under a freshly-minted one that no peer had pinned — the configured setting
        // inert while appearing effective.
        string path = cfg.EffectiveIdentityPath;
        var store = new NodeIdentityStore(path);
        bool existed = store.Exists;
        var cert = store.LoadOrMint(Environment.MachineName.ToLowerInvariant());
        string nodeId = NodeIdentityStore.DeriveNodeId(cert);

        if (!existed)
            Ledger().Record("minted node.key", $"delete {path}", "a stable node id that survives restarts");

        Console.WriteLine($"node_id : {nodeId}");
        Console.WriteLine($"pin     : {NodeIdentityStore.PinFromNodeId(nodeId)}");
        Console.WriteLine($"spki    : {NodeIdentityStore.ExportSpki(cert)}");
        Console.WriteLine($"key     : {path}{(existed ? " (existing)" : " (minted)")}"
                          + $"{(string.IsNullOrWhiteSpace(cfg.IdentityPath) ? "  [default]" : "  [from identity_path]")}");
        Console.WriteLine();
        Console.WriteLine("Publish the node_id AND the spki to peers: the node id admits you, the spki lets");
        Console.WriteLine("them VERIFY your operations rather than take your attribution on trust. Both are");
        Console.WriteLine("stable BECAUSE the key is persisted — a pin read from a probe run is ephemeral.");
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

        // NO WALL CLOCK IN THE IDENTIFIER. FR-026 forbids deriving it from time, and the previous
        // form embedded the current year and month — the fossil term was born of exactly this habit
        // (floor(unix_ts/300)). A random identifier orders nothing by magnitude, which is the point:
        // term-spaces are compared for EQUALITY, never for which is later.
        // MINT UNTIL IT CANNOT BE MISREAD. A truncated hex GUID is sometimes all digits, and this
        // command duly produced "ynet-epoch-5111282822734" — a 13-digit tail, indistinguishable
        // from a unix millisecond timestamp. The guard below now rejects that shape, so the minter
        // must not generate it: regenerating is free and makes the two consistent by construction
        // rather than by luck.
        string epochId;
        do { epochId = $"ynet-epoch-{Guid.NewGuid():n}"[..24]; }
        while (TermSpaceRegistry.LooksClockDerived(epochId));
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

    /// <summary>
    /// Append one board operation. THIS PROCESS DOES NOT PUSH — the running `serve` daemon tails the
    /// log and pushes what it finds.
    /// <para>
    /// The previous implementation opened a full service here, never bound or dialled it, and called
    /// AppendAndPushAsync against an empty admitted set: the op was written locally and reached
    /// nobody, while the running daemon had no way to learn of it. Appending durably and letting the
    /// daemon carry it is correct in both directions — with no daemon running the op is still on the
    /// board and converges at the next pull, and with one running it is pushed within a second.
    /// </para>
    /// </summary>
    private static async Task<int> Post(string[] a)
    {
        string body = Arg(a, "--body") ?? "";
        if (string.IsNullOrWhiteSpace(body)) { Console.Error.WriteLine("usage: post --body <text>"); return 1; }

        var (cfg, log, cert, nodeId) = await OpenLogAsync();

        var op = FederationOp.Create(await NextDotAsync(cfg, log, nodeId), nodeId, "board_post",
            JsonSerializer.SerializeToElement(new { body, lane = cfg.BoardActor }))
            .SignedBy(cert);

        await log.AppendAsync(op);
        Report(cfg, log, op);
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

        var (cfg, log, cert, nodeId) = await OpenLogAsync();

        var op = RetirementOp.Create(await NextDotAsync(cfg, log, nodeId), nodeId, Dot.Parse(target), reason)
                             .SignedBy(cert);
        await log.AppendAsync(op);

        Console.WriteLine($"retired {target} into the legacy space by appending {op.OpId}.");
        Console.WriteLine("The target is STILL PRESENT in the log — removal is indistinguishable from suppression.");
        Report(cfg, log, op);
        return 0;
    }

    private static void Report(FederationConfig cfg, SchedulerBoardLog log, FederationOp op)
    {
        Console.WriteLine($"appended {op.OpId} to {log.WritePath}");
        bool serving = StatusHeartbeat.ReadFresh(DateTimeOffset.UtcNow) is not null;
        Console.WriteLine(serving
            ? "a serving process is running and tails this log — it will push the operation within a second."
            : "NO serving process is publishing a measurement, so nothing will push this operation now."
              + "\n  It is durably on the board and converges at the peer's next reconciliation pull."
              + "\n  Run `serve` in another terminal to push on append.");
    }

    private static async Task<int> Serve()
    {
        var (cfg, svc, log, nodeId) = await OpenServiceAsync();
        await using var _ = svc;

        Console.WriteLine($"board root : {log.Root}");
        Console.WriteLine($"actor      : {cfg.BoardActor}   node id: {nodeId}");
        Console.WriteLine($"log        : {log.WritePath}");
        Console.WriteLine($"fold       : {svc.Fold.Count} operation(s) replayed"
                          + $" ({log.AdaptedLines} adapted from scheduler-native lines,"
                          + $" {log.UnreadableLines} unreadable)");
        if (log.UnreadableLines > 0)
        {
            Console.WriteLine("             unreadable lines are COUNTED, never silently skipped — a board that");
            Console.WriteLine("             drops a line it cannot parse converges to the wrong answer quietly.");
        }

        if (!await svc.BindAsync())
        {
            Console.Error.WriteLine("listener did NOT bind. Run `status` — the four states are reported separately.");
            return 1;
        }

        Console.Write(svc.Status().Render());
        foreach (var p in cfg.Peers)
        {
            var outcome = await svc.DialAsync(p.NodeId);
            Console.WriteLine($"dial {p.Name,-12}: {outcome}");
        }

        Console.WriteLine($"\nserving. push_on_append={cfg.PushOnAppend}, pull every {cfg.PullIntervalSeconds}s. Ctrl-C to stop.");
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };

        // Four loops, and each exists because something it does was previously claimed and not
        // done: the receive loop, the pull leg (the interval was printed while nothing read it),
        // the log tail (`post` is a separate process whose appends this daemon could not see), and
        // the status heartbeat (`status` is a separate process that could not see this one's
        // measurements — and, once it could, went stale between 60 s pull ticks).
        var pump = svc.RunPullLoopAsync(stop.Token);
        var tail = svc.RunBoardTailAsync(log.Root, log.WritePath, stop.Token);
        var beat = svc.RunStatusHeartbeatAsync(stop.Token);

        try
        {
            while (!stop.IsCancellationRequested)
            {
                try { await svc.ReceiveOneAsync(stop.Token); }
                catch (Exception ex) when (ex is System.Text.Json.JsonException
                                                 or KeyNotFoundException
                                                 or FormatException
                                                 or ArgumentException)
                {
                    // A MALFORMED FRAME IS ONE PEER'S PROBLEM, NOT THE DAEMON'S.
                    //
                    // Any admitted peer sending bad JSON in a hello, ack, pull or board frame threw
                    // out of the decoder, past this loop, into Main — terminating federation for
                    // EVERY peer. One corrupt frame could take the host off the board, which is a
                    // denial of service any admitted party could trigger by accident.
                    Console.Error.WriteLine($"REJECTED malformed frame: {ex.GetType().Name}: {ex.Message}");
                }
                catch (DotConflictException ex)
                {
                    // Two different operations claiming one dot. Refused loudly and the loop
                    // continues — the conflicting op is never folded, and never acked.
                    Console.Error.WriteLine($"REJECTED: {ex.Message}");
                }
                catch (MergeRefusedException ex)
                {
                    // A refused merge is REPORTED and the loop continues. Refusing one peer must not
                    // stop serving every other peer, and a silent swallow would hide the STOP ORDER
                    // doing its job.
                    Console.Error.WriteLine($"REFUSED: {ex.Message}");
                }
                catch (AttributionRefusedException ex)
                {
                    // Same discipline, different gate: a forged or inconsistent attribution is
                    // rejected loudly and named, never folded and never allowed to stop the daemon.
                    Console.Error.WriteLine($"REFUSED: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) { }

        try { await pump; } catch (OperationCanceledException) { }
        try { await tail; } catch (OperationCanceledException) { }
        try { await beat; } catch (OperationCanceledException) { }
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
                string keyPath = FederationConfig.Load().EffectiveIdentityPath;
                try { File.Delete(keyPath); Console.WriteLine($"deleted {keyPath}"); }
                catch (Exception ex) { Console.Error.WriteLine($"could not delete node.key: {ex.Message}"); }
            }
        }
        Console.WriteLine("\nConfig reversals applied. The firewall rule still needs the elevated one-liner above.");
        return 0;
    }

    // ---- helpers -----------------------------------------------------------------------------

    /// <summary>
    /// Open this host's identity and its log on the EXISTING board root. Used by the verbs that
    /// append but do not serve — they need durable storage and an identity, not a transport.
    /// </summary>
    private static async Task<(FederationConfig, SchedulerBoardLog, System.Security.Cryptography.X509Certificates.X509Certificate2, string)>
        OpenLogAsync()
    {
        var cfg = FederationConfig.Load();
        var problems = cfg.Validate();
        if (problems.Count > 0)
            throw new InvalidOperationException("federation config refused: " + string.Join("; ", problems));

        var store = new NodeIdentityStore(cfg.EffectiveIdentityPath);
        bool existed = store.Exists;
        var cert = store.LoadOrMint(Environment.MachineName.ToLowerInvariant());
        string nodeId = NodeIdentityStore.DeriveNodeId(cert);

        // RECORD IT HERE TOO. Only the `identity` verb logged the mint, but `post`, `retire` and
        // `serve` all mint on first use — and on a fresh host one of those is usually the first
        // command run. `revert --all` then left a minted key behind, so a change made to enable
        // federation was not reversible by the documented action (FR-025).
        if (!existed)
            Ledger().Record("minted node.key", $"delete {cfg.EffectiveIdentityPath}",
                            "a stable node id that survives restarts");

        string root = BoardRoot.Resolve(null, cfg.BoardRootPath);
        var log = new SchedulerBoardLog(root, cfg.BoardActor,
            cfg.WriteIntoLaneSegment ? BoardWriteMode.LaneSegment : BoardWriteMode.FederationKind);

        await Task.CompletedTask;
        return (cfg, log, cert, nodeId);
    }

    /// <summary>
    /// The next dot counter for this host: durable, contiguous, and safe against a second process.
    /// Seeded from the log so a lost sequence file can never re-issue a counter already on the board.
    /// </summary>
    private static async Task<Dot> NextDotAsync(FederationConfig cfg, SchedulerBoardLog log, string nodeId)
    {
        long floor = DotSequencer.HighestFor(nodeId, await log.ReadAllAsync());
        return new DotSequencer(DotSequencer.DefaultPath(), nodeId, floor).Next();
    }

    private static async Task<(FederationConfig, FederationService, SchedulerBoardLog, string)> OpenServiceAsync()
    {
        var (cfg, log, cert, nodeId) = await OpenLogAsync();

        // The pin table is keyed by NODE ID — the same string used as the dial key and the hello
        // value. Keying it by the human name made both the accept-side lookup and the dial-side
        // remote-name check reject correctly-configured peers.
        var transport = new QuicLinkTransport(nodeId, cert, cfg.ToPeerSet().ToPinTable());
        var fold = new FederationFold(new TermSpaceRegistry(
            string.IsNullOrWhiteSpace(cfg.SpaceId) ? "ynet-epoch-unset" : cfg.SpaceId));

        var svc = new FederationService(cfg, new QuicFederationLink(transport), fold, log)
        {
            StatusHeartbeatPath = StatusHeartbeat.DefaultPath(),
            RequireVerifiedAttribution = cfg.RequireVerifiedAttribution,
        };

        // ENROL THIS HOST'''S OWN KEY. The verifier table holds configured PEERS, so with strict
        // attribution on, the tail and startup replay classified THIS host'''s own operations as
        // UnverifiedOrigin and refused them — turning the security setting into a mute button.
        svc.EnrolLocalIdentity(nodeId, NodeIdentityStore.ExportSpki(cert));

        // Replay the WHOLE board — every actor's log under the root, not just this host's own
        // segment. A fold built from one host's operations is that host's corner, not the board.
        //
        // THROUGH THE SERVICE, not straight into the fold: replaying directly bypassed every
        // admission check, so require_verified_attribution was switched off by every restart and an
        // unsigned or tampered operation already on disk became visible and propagatable.
        svc.ReplayIntoFold(await log.ReadAllAsync());
        return (cfg, svc, log, nodeId);
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

              status                                        four states, read from the SERVING process
              config show                                   the EFFECTIVE configuration
              config set <key> <value>                      then reads it back for verification
              config add-peer --name --node-id --endpoint    several --endpoint = ONE participant
                              [--spki <base64>]              publish the key to VERIFY their ops
              identity                                      mint/load the persisted node id + spki
              epoch mint --rationale <why>                   a recorded operator action
              serve                                         bind, dial, receive, pull, tail, publish
              post --body <text>                            append to the board; `serve` pushes it
              retire --op <peer:counter> --reason <why>      append a superseding op; never delete
              revert [--all]                                 replay the recorded reversals

            config keys: enabled, bind_address, bind_port, space_id, identity_path, board_root,
                         board_actor, write_into_lane_segment, require_verified_attribution,
                         pull_interval_seconds, push_on_append

            `post` and `status` are SEPARATE PROCESSES from `serve` and behave correctly as such:
            post appends durably and serve tails the log; status reads serve's published measurement
            and reports UNKNOWN — never No — when no serving process is publishing one.

            Run via `dotnet run` — Smart App Control blocks unsigned apphosts on this host.
            """);
    }
}

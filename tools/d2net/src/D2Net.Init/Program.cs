using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace D2Net.Init;

public static class Program
{
    public static int Main(string[] args)
        => Run(args, Console.In, Console.Out, Console.Error, Directory.GetCurrentDirectory());

    /// <summary>Entry point for tests; takes injectable readers/writers and CWD.</summary>
    public static int Run(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr, string cwd)
    {
        try
        {
            var parsed = ArgParser.Parse(args, cwd);
            switch (parsed)
            {
                case ParsedCli.HelpRequested:
                    PrintUsage(stdout);
                    return ExitCodes.Success;
                case ParsedCli.VersionRequested:
                    stdout.WriteLine("d2net-init 0.2.0");
                    return ExitCodes.Success;
                case ParsedCli.InitMode init:
                    {
                        var prompter = new InteractivePrompter(stdin, stdout, init.Options.NonInteractive);
                        var runner = new InitRunner(prompter, stdout, stderr);
                        return runner.Run(init.Options);
                    }
                case ParsedCli.InspectMode inspect:
                    {
                        var runner = new InspectionRunner(stdout, stderr);
                        return runner.Run(inspect.Options);
                    }
                case ParsedCli.Error err:
                    stderr.WriteLine(err.Message);
                    PrintUsage(stderr);
                    return ExitCodes.ArgumentError;
                default:
                    stderr.WriteLine("internal error: unhandled CLI parse result.");
                    return ExitCodes.ArgumentError;
            }
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"d2net-init: unhandled error: {ex.Message}");
            return ExitCodes.ArgumentError;
        }
    }

    private static void PrintUsage(TextWriter w)
    {
        w.WriteLine("Usage:");
        w.WriteLine("  d2net-init [--source <name>] [--target-extension <ext>] [--target <name>]");
        w.WriteLine("             [--exclude <path> ...] [--accept-suggested-exclusions]");
        w.WriteLine("             [--non-interactive] [--bridge-port <port>]");
        w.WriteLine("             [--FORCE --DELETE-EXISTING]");
        w.WriteLine();
        w.WriteLine("Inspection (mutually exclusive):");
        w.WriteLine("  d2net-init --list           [--json] [--bridge-port <port>]");
        w.WriteLine("  d2net-init --Exclusions     [--json] [--bridge-port <port>]");
        w.WriteLine("  d2net-init --current-phase  [--json] [--bridge-port <port>]");
        w.WriteLine();
        w.WriteLine("  d2net-init --help | --version");
        w.WriteLine();
        w.WriteLine("Notes:");
        w.WriteLine("  Storage: PGLite WASM via a per-invocation Node.js bridge subprocess.");
        w.WriteLine("  Requires Node.js >= 20 on PATH for the bridge.");
        w.WriteLine("  --bridge-port default: 54400. On init, the chosen port is persisted to");
        w.WriteLine("  D2NET-Settings.json. On --list/--Exclusions/--current-phase, the persisted");
        w.WriteLine("  port is used; --bridge-port on an inspection invocation overrides only the");
        w.WriteLine("  live run and does NOT modify settings.");
        w.WriteLine();
        w.WriteLine("  --FORCE --DELETE-EXISTING also rebuilds workspaces created by the shipped");
        w.WriteLine("  002 (SQLite-backed) D2NET.Init -- no automatic data migration.");
    }
}

internal abstract record ParsedCli
{
    public sealed record HelpRequested : ParsedCli;
    public sealed record VersionRequested : ParsedCli;
    public sealed record InitMode(InitOptions Options) : ParsedCli;
    public sealed record InspectMode(InspectOptions Options) : ParsedCli;
    public sealed record Error(string Message) : ParsedCli;
}

internal static class ArgParser
{
    public static ParsedCli Parse(string[] args, string cwd)
    {
        string? source = null, ext = null, target = null;
        var manualExclusions = new List<string>();
        bool acceptSuggested = false, force = false, deleteExisting = false, nonInteractive = false;
        bool list = false, exclusions = false, currentPhase = false, json = false;
        int? userBridgePort = null;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-h":
                case "--help":
                    return new ParsedCli.HelpRequested();
                case "--version":
                    return new ParsedCli.VersionRequested();
                case "--source":
                    if (++i >= args.Length) return new ParsedCli.Error("--source requires a value.");
                    source = args[i]; break;
                case "--target-extension":
                    if (++i >= args.Length) return new ParsedCli.Error("--target-extension requires a value.");
                    ext = args[i]; break;
                case "--target":
                    if (++i >= args.Length) return new ParsedCli.Error("--target requires a value.");
                    target = args[i]; break;
                case "--exclude":
                    if (++i >= args.Length) return new ParsedCli.Error("--exclude requires a value.");
                    manualExclusions.Add(args[i]); break;
                case "--accept-suggested-exclusions":
                    acceptSuggested = true; break;
                case "--non-interactive":
                    nonInteractive = true; break;
                case "--FORCE":
                case "--force":
                    force = true; break;
                case "--DELETE-EXISTING":
                case "--delete-existing":
                    deleteExisting = true; break;
                case "--list":
                    list = true; break;
                case "--Exclusions":
                case "--exclusions":
                    exclusions = true; break;
                case "--current-phase":
                    currentPhase = true; break;
                case "--json":
                    json = true; break;
                case "--bridge-port":
                    if (++i >= args.Length) return new ParsedCli.Error("--bridge-port requires a value.");
                    if (!int.TryParse(args[i], out var bp) || bp < 1 || bp > 65535)
                        return new ParsedCli.Error($"--bridge-port must be an integer in [1,65535] (got '{args[i]}').");
                    userBridgePort = bp;
                    break;
                default:
                    return new ParsedCli.Error($"unknown argument '{a}'. Try --help.");
            }
        }

        var inspectFlagsSet = (list ? 1 : 0) + (exclusions ? 1 : 0) + (currentPhase ? 1 : 0);
        bool anyInitFlag = source is not null || ext is not null || target is not null
                            || manualExclusions.Count > 0 || acceptSuggested || force || deleteExisting;

        if (inspectFlagsSet > 1)
            return new ParsedCli.Error("--list, --Exclusions, and --current-phase are mutually exclusive.");
        if (inspectFlagsSet == 1)
        {
            if (anyInitFlag)
                return new ParsedCli.Error("inspection options cannot be combined with init flags.");
            if (nonInteractive)
                return new ParsedCli.Error("--non-interactive has no effect on inspection options.");
            var mode = list ? InspectMode.List
                     : exclusions ? InspectMode.Exclusions
                     : InspectMode.CurrentPhase;
            return new ParsedCli.InspectMode(new InspectOptions(cwd, mode, json, userBridgePort));
        }

        // Init mode
        if (json)
            return new ParsedCli.Error("--json applies only to inspection options.");
        if (force ^ deleteExisting)
            return new ParsedCli.Error(
                "--FORCE and --DELETE-EXISTING must be supplied together (or not at all).");

        return new ParsedCli.InitMode(new InitOptions(
            RepoRoot: cwd,
            SourceDir: source,
            TargetExtension: ext,
            TargetDir: target,
            ManualExclusions: manualExclusions,
            AcceptSuggestedExclusions: acceptSuggested,
            Force: force,
            DeleteExisting: deleteExisting,
            NonInteractive: nonInteractive,
            BridgePort: userBridgePort ?? BridgeOptions.DefaultPort));
    }
}

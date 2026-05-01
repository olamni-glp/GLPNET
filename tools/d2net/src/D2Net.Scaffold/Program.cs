using System.Collections.Generic;
using System.IO;

namespace D2Net.Scaffold;

public static class Program
{
    public static int Main(string[] args)
        => Run(args, System.Console.In, System.Console.Out, System.Console.Error,
            Directory.GetCurrentDirectory());

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
                    var asm = typeof(Program).Assembly;
                    var info = (System.Reflection.AssemblyInformationalVersionAttribute?)
                        System.Attribute.GetCustomAttribute(asm,
                            typeof(System.Reflection.AssemblyInformationalVersionAttribute));
                    var ver = info?.InformationalVersion ?? asm.GetName().Version?.ToString() ?? "0.0.0";
                    stdout.WriteLine($"d2net-scaffold {ver}");
                    return ExitCodes.Success;
                case ParsedCli.ScaffoldMode scaffold:
                    {
                        var runner = new ScaffoldRunner(stdin, stdout, stderr);
                        return runner.Run(scaffold.Options);
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
        catch (System.Exception ex)
        {
            stderr.WriteLine($"d2net-scaffold: unhandled error: {ex.Message}");
            return ExitCodes.ArgumentError;
        }
    }

    internal static void PrintUsage(TextWriter w)
    {
        w.WriteLine("d2net-scaffold — bootstrap a .NET conversion scaffold from a Dart source tree.");
        w.WriteLine();
        w.WriteLine("Usage:");
        w.WriteLine("  d2net-scaffold [--json] [--FORCE --DELETE-TARGET]");
        w.WriteLine("  d2net-scaffold --help | --version");
        w.WriteLine();
        w.WriteLine("The tool reads source, target, extension, and exclusions from the workspace");
        w.WriteLine("at <cwd>/.D2NET/D2NET-Settings.json (created by d2net-init). It walks the");
        w.WriteLine("on-disk source tree, skips every excluded directory and its descendants,");
        w.WriteLine("copies every other file to the target tree at <target_dir>/, and creates");
        w.WriteLine("an empty __<basename>/ working directory next to every copied .dart file.");
        w.WriteLine();
        w.WriteLine("Flags:");
        w.WriteLine("  --json                       Emit success summary / error envelope as JSON.");
        w.WriteLine("  --FORCE --DELETE-TARGET      Authorise destruction of a non-scaffold-managed");
        w.WriteLine("                               target directory. Both flags MUST be supplied");
        w.WriteLine("                               together. An interactive confirmation prompt");
        w.WriteLine("                               will name the absolute target path before any");
        w.WriteLine("                               deletion; reply 'yes' (or 'y') to proceed.");
        w.WriteLine();
        w.WriteLine("  --help                       Show this help and exit.");
        w.WriteLine("  --version                    Show the binary version and exit.");
        w.WriteLine();
        w.WriteLine("Notes:");
        w.WriteLine("  Storage: the workspace database is single-user PGLite (WASM) accessed via");
        w.WriteLine("  a per-invocation Node.js bridge subprocess. Requires Node.js >= 20 on PATH.");
        w.WriteLine();
        w.WriteLine("  Idempotent: re-running with no underlying changes produces a target tree");
        w.WriteLine("  byte-identical to the prior state. Exclusion-list changes (via");
        w.WriteLine("  d2net-init --add-exclude / --remove-exclude) are picked up on the next");
        w.WriteLine("  scaffold run automatically.");
        w.WriteLine();
        w.WriteLine("  Atomicity: scaffold writes to a sibling staging directory (<target>.d2net-tmp/)");
        w.WriteLine("  and renames it over the live target only after the workspace database");
        w.WriteLine("  transaction commits. On any failure pre-rename, the staging directory is");
        w.WriteLine("  removed and the live target is untouched.");
    }
}

internal abstract record ParsedCli
{
    public sealed record HelpRequested : ParsedCli;
    public sealed record VersionRequested : ParsedCli;
    public sealed record ScaffoldMode(ScaffoldOptions Options) : ParsedCli;
    public sealed record Error(string Message) : ParsedCli;
}

internal static class ArgParser
{
    public static ParsedCli Parse(string[] args, string cwd)
    {
        bool help = false, version = false, json = false, force = false, deleteTarget = false;
        int? bridgePort = null;
        var positional = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-h":
                case "--help":
                    help = true; break;
                case "--version":
                    version = true; break;
                case "--json":
                    json = true; break;
                case "--FORCE":
                case "--force":
                    force = true; break;
                case "--DELETE-TARGET":
                case "--delete-target":
                    deleteTarget = true; break;
                case "--bridge-port":
                    if (++i >= args.Length)
                        return new ParsedCli.Error("d2net-scaffold: --bridge-port requires a value.");
                    if (!int.TryParse(args[i], out var bp) || bp < 1 || bp > 65535)
                        return new ParsedCli.Error(
                            $"d2net-scaffold: --bridge-port must be an integer in [1,65535] (got '{args[i]}').");
                    bridgePort = bp;
                    break;
                default:
                    if (a.StartsWith("--", System.StringComparison.Ordinal)
                        || a.StartsWith("-", System.StringComparison.Ordinal))
                    {
                        return new ParsedCli.Error($"d2net-scaffold: unknown flag '{a}'. Try --help.");
                    }
                    positional.Add(a);
                    break;
            }
        }

        if (help)
        {
            if (json || force || deleteTarget || version)
                return new ParsedCli.Error(
                    "d2net-scaffold: --help is mutually exclusive with all other flags.");
            return new ParsedCli.HelpRequested();
        }
        if (version)
        {
            if (json || force || deleteTarget)
                return new ParsedCli.Error(
                    "d2net-scaffold: --version is mutually exclusive with all other flags.");
            return new ParsedCli.VersionRequested();
        }

        if (positional.Count > 0)
        {
            return new ParsedCli.Error(
                $"d2net-scaffold: positional arguments are not accepted (got '{positional[0]}'). " +
                "Source / target / exclusions are read from the workspace.");
        }

        if (force ^ deleteTarget)
        {
            return new ParsedCli.Error(
                "d2net-scaffold: --FORCE and --DELETE-TARGET must be supplied together (or not at all).");
        }

        return new ParsedCli.ScaffoldMode(new ScaffoldOptions(
            RepoRoot: cwd,
            Json: json,
            ForceDeleteTarget: force && deleteTarget,
            BridgePortOverride: bridgePort));
    }
}

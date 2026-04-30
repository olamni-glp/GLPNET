using System.CommandLine;
using D2Net.Scaffold;

// Note: System.CommandLine's RootCommand auto-registers --help and --version on the parser pipeline;
// the version is read from the assembly's InformationalVersion (set in the .csproj).

var sourceArgument = new Argument<string>(
    name: "source",
    description: "Path to the Dart source directory to scaffold from.")
{
    Arity = ArgumentArity.ZeroOrOne,
};
sourceArgument.SetDefaultValue("");

var targetArgument = new Argument<string>(
    name: "target",
    description: "Path to the target directory the tool will create (default mode) or update (with --refresh).")
{
    Arity = ArgumentArity.ZeroOrOne,
};
targetArgument.SetDefaultValue("");

var refreshOption = new Option<bool>(
    name: "--refresh",
    description: "Refresh mode: target must already exist. Refreshes non-Dart files and .dart.src files; preserves companion files and the existing d2net-tracker.json.");

var root = new RootCommand("d2net-scaffold — bootstrap a .NET conversion scaffold from a Dart source tree.")
{
    sourceArgument,
    targetArgument,
    refreshOption,
};

root.SetHandler(
    (string source, string target, bool refresh) =>
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
        {
            Console.Error.WriteLine("error: <source> and <target> are required (or pass --help / --version).");
            Environment.ExitCode = 1;
            return;
        }

        var options = new ScaffoldOptions(source, target, refresh);
        var outcome = refresh ? RefreshRunner.Run(options) : ScaffoldRunner.Run(options);

        if (outcome.ExitCode == 0 && outcome.Summary is not null)
        {
            outcome.Summary.WriteTo(Console.Out);
            Environment.ExitCode = 0;
            return;
        }

        if (outcome.ExitCode == 5 && outcome.Collisions.Count > 0)
        {
            Console.Error.WriteLine("error: companion-file collisions detected; nothing was written to the target.");
            foreach (var c in outcome.Collisions)
                Console.Error.WriteLine(
                    $"  collision: {c.DartFileRelPath} would generate .{c.CollidingExtension} stub but {c.ExistingFileRelPath} already exists");
            Environment.ExitCode = 5;
            return;
        }

        if (!string.IsNullOrEmpty(outcome.Error))
            Console.Error.WriteLine($"error: {outcome.Error}");
        Environment.ExitCode = outcome.ExitCode;
    },
    sourceArgument, targetArgument, refreshOption);

return await root.InvokeAsync(args);

using System.Diagnostics;

namespace D2Net.Scaffold.Tests;

public class HelpAndVersionTests
{
    private const string ProjectPath =
        "../../../../../src/D2Net.Scaffold/D2Net.Scaffold.csproj";

    [Fact]
    public void HelpFlag_ExitsZero_AndPrintsUsage()
    {
        var (exit, stdout, _) = RunCli("--help");
        Assert.Equal(0, exit);
        Assert.False(string.IsNullOrWhiteSpace(stdout));
        Assert.Contains("d2net-scaffold", stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VersionFlag_ExitsZero_AndPrintsNonEmptyVersion()
    {
        var (exit, stdout, _) = RunCli("--version");
        Assert.Equal(0, exit);
        var versionLine = stdout.Trim();
        Assert.False(string.IsNullOrEmpty(versionLine), "Version output must not be empty.");
        // Loose check: contains at least one digit (e.g., "0.1.0").
        Assert.Contains(versionLine, c => char.IsDigit(c));
    }

    private static (int ExitCode, string Stdout, string Stderr) RunCli(string args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { "run", "--project", ProjectPath, "--no-build", "--", },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        foreach (var a in args.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(60_000);
        return (proc.ExitCode, stdout, stderr);
    }
}

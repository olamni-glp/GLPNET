using System.IO;
using D2Net.Init;

namespace D2Net.Scaffold.Tests.Fixtures;

/// <summary>
/// Test-only helper that drives <c>D2Net.Init.Program.Run</c> through its
/// non-interactive init path so scaffold tests start from a real
/// PGLite-backed workspace. Mirrors the AddExcludeRunnerTests pattern.
/// </summary>
public static class InitHelper
{
    /// <summary>
    /// Run <c>d2net-init</c> for the supplied repo with the standard
    /// canonical flags (source = glp_runtime, target = glp_runtime_net,
    /// extension = _net). Returns the exit code, stdout text, and the
    /// bridge port used (so subsequent invocations can reuse it).
    /// </summary>
    public static (int Code, string Stdout, string Stderr, int Port) Init(
        string repoRoot, int? bridgePort = null,
        string sourceDir = "glp_runtime",
        string targetDir = "glp_runtime_net",
        string targetExtension = "_net")
    {
        var port = bridgePort ?? PortPicker.NextFreePort();
        var so = new StringWriter();
        var se = new StringWriter();
        var code = D2Net.Init.Program.Run(
            new[] {
                "--source", sourceDir,
                "--target-extension", targetExtension,
                "--target", targetDir,
                "--accept-suggested-exclusions",
                "--non-interactive",
                "--bridge-port", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            new StringReader(""),
            so, se,
            repoRoot);
        return (code, so.ToString(), se.ToString(), port);
    }

    /// <summary>
    /// Run <c>d2net-init --add-exclude</c> against an existing workspace.
    /// </summary>
    public static (int Code, string Stdout, string Stderr) AddExclude(
        string repoRoot, int port, params string[] paths)
    {
        var args = new System.Collections.Generic.List<string>();
        foreach (var p in paths) { args.Add("--add-exclude"); args.Add(p); }
        args.Add("--bridge-port"); args.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var so = new StringWriter();
        var se = new StringWriter();
        var code = D2Net.Init.Program.Run(args.ToArray(), new StringReader(""), so, se, repoRoot);
        return (code, so.ToString(), se.ToString());
    }

    public static (int Code, string Stdout, string Stderr) RemoveExclude(
        string repoRoot, int port, bool allowSystem, params string[] paths)
    {
        var args = new System.Collections.Generic.List<string>();
        foreach (var p in paths) { args.Add("--remove-exclude"); args.Add(p); }
        if (allowSystem) args.Add("--allow-system-exclusions");
        args.Add("--bridge-port"); args.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var so = new StringWriter();
        var se = new StringWriter();
        var code = D2Net.Init.Program.Run(args.ToArray(), new StringReader(""), so, se, repoRoot);
        return (code, so.ToString(), se.ToString());
    }

    public static (int Code, string Stdout, string Stderr) ListJson(string repoRoot, int port)
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = D2Net.Init.Program.Run(
            new[] { "--list", "--json", "--bridge-port", port.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            new StringReader(""), so, se, repoRoot);
        return (code, so.ToString(), se.ToString());
    }

    /// <summary>Run d2net-scaffold via Program.Run; convenience wrapper for tests.</summary>
    public static (int Code, string Stdout, string Stderr) Scaffold(
        string repoRoot, int port,
        bool json = false, bool forceDelete = false, string stdin = "")
    {
        var args = new System.Collections.Generic.List<string>();
        if (json) args.Add("--json");
        if (forceDelete) { args.Add("--FORCE"); args.Add("--DELETE-TARGET"); }
        if (port > 0)
        {
            args.Add("--bridge-port");
            args.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        var so = new StringWriter();
        var se = new StringWriter();
        var code = D2Net.Scaffold.Program.Run(args.ToArray(), new StringReader(stdin), so, se, repoRoot);
        return (code, so.ToString(), se.ToString());
    }
}

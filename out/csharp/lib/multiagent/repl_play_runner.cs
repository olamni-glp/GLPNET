// ReplPlayRunner — runs simulated dGLP plays via REPL subprocess.
//
// Spawns the REPL as a subprocess, pipes load commands and a play goal,
// parses tagged output lines, and delivers them via callbacks.
//
// Tagged output format from GLP: tagged(alice, cmd(connect(bob)))
// Parsed into: agentId="alice", kind="cmd", content="connect(bob)"
//
// Narrative kinds (play 12): friend, say, act, event
// e.g. tagged(alice, friend(bob)) → agentId="alice", kind="friend", content="bob"

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace GlpRuntime.Multiagent;

/// <summary>Parsed output line from a simulated play.</summary>
public class PlayOutput
{
    /// <summary>e.g. "alice"</summary>
    public string AgentId { get; }

    /// <summary>"cmd", "notify", "friend", "say", "act", or "event"</summary>
    public string Kind { get; }

    /// <summary>e.g. "connect(bob)"</summary>
    public string Content { get; }

    public PlayOutput(string agentId, string kind, string content)
    {
        AgentId = agentId;
        Kind = kind;
        Content = content;
    }
}

/// <summary>
/// Runs simulated dGLP plays via a REPL subprocess.
/// <para>
/// Usage:
///   var runner = new ReplPlayRunner("/Users/udi/Grassroots/GLP");
///   runner.OnOutput = (output) => { /* route to UI panel */ };
///   runner.OnLog    = (line)   => { /* trace log */ };
///   runner.OnError  = (error)  => { /* display error */ };
///   runner.OnDone   = (exitCode) => { /* play finished */ };
///   await runner.RunAsync(1); // runs fplay1
///   runner.Kill(); // abort if needed
/// </para>
/// </summary>
public class ReplPlayRunner
{
    private static readonly bool IsWindows =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    // ── Static project-file seed lists ───────────────────────────────────

    /// <summary>
    /// CSSG project (child-safe social graph, plays 1–7).
    /// Uses the modules_v2 project which loads cleanly (typed_book/cssg has
    /// an unresolved IntroChannel/FriendChannel typing issue).
    /// </summary>
    public static readonly IReadOnlyList<string> CssgFiles = new[]
    {
        "../programs/cssg_modules_v2",
    };

    /// <summary>
    /// Bonds project (grassroots bonds, plays 1–11).
    /// Uses bonds_v2 which loads cleanly (typed_book/bonds is missing
    /// procedure declarations).
    /// </summary>
    public static readonly IReadOnlyList<string> BondsFiles = new[]
    {
        "../programs/bonds_v2",
    };

    /// <summary>Bonds GLP files for play 12 — same project, fplay12 is part of the boot.</summary>
    public static readonly IReadOnlyList<string> BondsPlay12Files = new[]
    {
        "../programs/bonds_v2",
    };

    /// <summary>
    /// CSSN project (child-safe social networking, plays 1–10).
    /// Uses cssn_modules_v2 which loads cleanly (typed_book/cssn has
    /// undefined Channel and X type references).
    /// </summary>
    public static readonly IReadOnlyList<string> CssnFiles = new[]
    {
        "../programs/cssn_modules_v2",
    };

    /// <summary>
    /// CSSN v2 modules project (village scenario, fplay13).
    /// Loaded as a project directory — the REPL treats the path as a project root.
    /// </summary>
    public static readonly IReadOnlyList<string> CssnVillageFiles = new[]
    {
        "../programs/cssn_modules_v2",
    };

    // ── Precompiled regex ─────────────────────────────────────────────────

    /// <summary>
    /// Regex for parsing tagged output lines.
    /// Matches cmd, notify (protocol), and friend, say, act, event (narrative).
    /// </summary>
    private static readonly Regex TaggedRegex = new(
        @"^tagged\((\w+), (cmd|notify|friend|say|act|event)\((.+)\)\)$",
        RegexOptions.Compiled);

    // ── Instance state ────────────────────────────────────────────────────

    public string RepoRoot { get; }

    /// <summary>Which GLP files to load (relative to glp_runtime/).</summary>
    public IReadOnlyList<string> GlpFiles { get; }

    /// <summary>Called for each parsed tagged output line.</summary>
    public Action<PlayOutput>? OnOutput;

    /// <summary>Called for non-tagged REPL output (banner, load messages, etc.).</summary>
    public Action<string>? OnLog;

    /// <summary>Called for stderr lines and exceptions.</summary>
    public Action<string>? OnError;

    /// <summary>Called when the REPL process exits.</summary>
    public Action<long>? OnDone;

    private Process? _process;

    // ── Constructor ───────────────────────────────────────────────────────

    public ReplPlayRunner(string repoRoot, IReadOnlyList<string>? glpFiles = null)
    {
        RepoRoot = repoRoot;
        GlpFiles = glpFiles ?? CssgFiles;
    }

    public bool IsRunning => _process is not null;

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Run a simulated play (1, 2, or 3).</summary>
    public async Task RunAsync(long playNumber)
    {
        var runtimeDir = $"{RepoRoot}/glp_runtime";

        OnLog?.Invoke($"REPL: repoRoot={RepoRoot}");
        OnLog?.Invoke($"REPL: runtimeDir={runtimeDir}");

        if (!Directory.Exists(runtimeDir))
        {
            OnError?.Invoke($"Directory not found: {runtimeDir}");
            return;
        }

        // Prefer AOT-compiled glp_repl.exe (no Dart SDK dependency, faster startup).
        var compiledRepl = IsWindows
            ? $"{runtimeDir}/bin/glp_repl.exe"
            : $"{runtimeDir}/bin/glp_repl";
        var replScript = $"{runtimeDir}/bin/glp_repl.dart";

        string executable;
        string[] arguments;
        if (File.Exists(compiledRepl))
        {
            executable = compiledRepl;
            arguments = Array.Empty<string>();
            OnLog?.Invoke($"REPL: exe={compiledRepl} (AOT)");
        }
        else if (File.Exists(replScript))
        {
            executable = FindDart();
            arguments = new[] { "run", "bin/glp_repl.dart" };
            OnLog?.Invoke($"REPL: dart={executable} (JIT via \"dart run\")");
        }
        else
        {
            OnError?.Invoke($"No REPL found: neither {compiledRepl} nor {replScript} exists");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = runtimeDir,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var a in arguments)
                psi.ArgumentList.Add(a);

            var process = Process.Start(psi)!;
            _process = process;
            OnLog?.Invoke($"REPL: process started (pid={process.Id})");

            // Feed load commands + play goal + quit
            var commands = string.Join("\n", GlpFiles.Append($"fplay{playNumber}.").Append(":quit"));
            // WriteAsync + "\n" (NOT WriteLineAsync) to preserve LF-only byte stream the REPL parser expects.
            await process.StandardInput.WriteAsync(commands + "\n").ConfigureAwait(false);
            process.StandardInput.Close();

            // Parse stdout — fire-and-forget ReadLineAsync loop (preserves Dart .listen semantics)
            _ = Task.Run(async () =>
            {
                string? line;
                while ((line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false)) is not null)
                    ParseLine(line);
            });

            // Log stderr
            _ = Task.Run(async () =>
            {
                string? line;
                while ((line = await process.StandardError.ReadLineAsync().ConfigureAwait(false)) is not null)
                    OnError?.Invoke($"REPL stderr: {line}");
            });

            // Wait for exit
            await process.WaitForExitAsync().ConfigureAwait(false);
            long exitCode = process.ExitCode;
            // Field reset BEFORE callbacks so re-entrant IsRunning probes observe idle.
            _process = null;
            OnLog?.Invoke($"REPL: exited with code {exitCode}");
            OnDone?.Invoke(exitCode);
        }
        catch (Win32Exception e)
        {
            _process = null;
            OnError?.Invoke(
                $"REPL: Win32Exception starting [{executable} {string.Join(' ', arguments)}] in {runtimeDir}: {e.Message} (errorCode={e.NativeErrorCode})");
        }
        catch (Exception e)
        {
            _process = null;
            OnError?.Invoke(
                $"REPL: failed to start [{executable} {string.Join(' ', arguments)}] in {runtimeDir}: {e}");
        }
    }

    /// <summary>Kill the REPL subprocess if running.</summary>
    public void Kill()
    {
        _process?.Kill();
        _process = null;
    }

    // ── Private helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Parse a single stdout line from the REPL.
    /// The REPL may prefix the first output line with "GLP> ", so strip it.
    /// </summary>
    private void ParseLine(string line)
    {
        var stripped = line.StartsWith("GLP> ") ? line.Substring(5) : line;
        var match = TaggedRegex.Match(stripped);
        if (!match.Success)
        {
            OnLog?.Invoke($"REPL: {line}");
            return;
        }

        var agentId = match.Groups[1].Value;
        var kind = match.Groups[2].Value;
        var content = match.Groups[3].Value;
        OnOutput?.Invoke(new PlayOutput(agentId, kind, content));
    }

    /// <summary>
    /// Find the dart executable. Prefer the one next to the Flutter SDK,
    /// fall back to PATH.
    /// </summary>
    private string FindDart()
    {
        if (IsWindows)
        {
            // Resolve dart.exe via PATH using `where`.
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                psi.ArgumentList.Add("dart.exe");
                using var p = Process.Start(psi)!;
                // Read stdout to end BEFORE WaitForExit to avoid deadlock on full pipe buffer.
                string stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode == 0)
                {
                    var lines = stdout
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 0)
                        .ToList();
                    if (lines.Count > 0) return lines[0];
                }
            }
            catch (Exception) { /* fall through */ }

            // Common Flutter SDK locations on Windows
            var userProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? "";
            var winCandidates = new List<string>();
            if (userProfile.Length > 0)
                winCandidates.Add($"{userProfile}\\flutter\\bin\\dart.exe");
            if (userProfile.Length > 0)
                winCandidates.Add($"{userProfile}\\development\\flutter\\bin\\dart.exe");
            winCandidates.Add(@"C:\flutter\bin\dart.exe");
            winCandidates.Add(@"C:\src\flutter\bin\dart.exe");

            foreach (var path in winCandidates)
                if (File.Exists(path)) return path;

            return "dart.exe";
        }

        // Check common macOS Flutter/Dart locations
        var home = Environment.GetEnvironmentVariable("HOME") ?? "";
        var candidates = new[]
        {
            "/usr/local/bin/dart",
            $"{home}/flutter/bin/dart",
            $"{home}/development/flutter/bin/dart",
            $"{home}/.pub-cache/bin/dart",
        };
        foreach (var path in candidates)
            if (File.Exists(path)) return path;

        // Fall back to PATH (works from terminal, may not from app bundle)
        return "dart";
    }
}

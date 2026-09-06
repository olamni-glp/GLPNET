// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using GlpRuntime.ReplClient;
using GlpRuntime.SplitProtocol;
using GlpRuntime.Supervisor;

namespace Ynet.Client.Tests;

/// <summary>
/// M6-d END TO END — the REAL <see cref="Supervisor"/> hosting the REAL <c>ynet_client</c> binary
/// as its child process. FR-024/FR-026/FR-028, SC-010/SC-011.
///
/// <para>
/// 🔴 <b>Why this file exists.</b> Engineer ruling <c>Q-G35-02 → B</c>, 2026-09-06, overriding the
/// cheaper option: composition from proven parts is NOT sufficient to ship. Every part was green —
/// the supervisor's own suite at 73/73, the probe answering the supervisor's own client channel —
/// and the codexreview then found that the two could not actually be joined at all, because
/// <c>Supervisor.StartChild</c> launches its child as <c>&lt;binary&gt; --listen … --store …</c>
/// with NO verb, and the client's switch fell straight through to <c>default</c> and exited.
/// </para>
///
/// <para>
/// That is exactly the failure this repo keeps re-learning: <i>"every part is proven"</i> is the
/// sentence that preceded wave-33's suite measuring the scheduler, wave-32's review analysing
/// nothing, and wave-29's two runtimes never both being started. A composition is not proven until
/// it has been composed.
/// </para>
///
/// <para>
/// These tests spawn real processes. They are deliberately tolerant about TIMING and strict about
/// OUTCOMES: a flaky red in the supervision path is worse than no test, because it trains everyone
/// to ignore a red that might be real.
/// </para>
/// </summary>
public class SupervisedHostingEndToEndTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "ynet-e2e-" + Guid.NewGuid().ToString("N"));

    public SupervisedHostingEndToEndTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* temp cleanup */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Locate the built client binary, or return null so the test SKIPS LOUDLY rather than passing
    /// vacuously. A test that silently passes when it could not find the thing under test is the
    /// false-green this whole feature exists to remove.
    /// </summary>
    private static string? FindClientBinary()
    {
        var here = AppContext.BaseDirectory;                   // …/ynet_client.tests/bin/<cfg>/net11.0
        var csharp = new DirectoryInfo(here);
        while (csharp is not null && csharp.Name != "csharp") csharp = csharp.Parent;
        if (csharp is null) return null;

        var exe = OperatingSystem.IsWindows() ? "ynet_client.exe" : "ynet_client";
        return new DirectoryInfo(Path.Combine(csharp.FullName, "ynet_client", "bin"))
            .EnumerateFiles(exe, SearchOption.AllDirectories)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static async Task<bool> PingSucceeds(int port, TimeSpan budget)
    {
        try
        {
            await using var ch = await ClientChannel.ConnectAsync("127.0.0.1", port, budget);
            using var cts = new CancellationTokenSource(budget);
            var r = await ch.RoundTripAsync(RequestFrame.Empty(ch.NextRequestId(), RequestKind.Ping), cts.Token);
            return r.Kind == ResponseKind.Ack;
        }
        catch (Exception) { return false; }
    }

    private static async Task<bool> WaitUntil(Func<Task<bool>> cond, TimeSpan budget)
    {
        var deadline = DateTimeOffset.UtcNow + budget;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await cond().ConfigureAwait(false)) return true;
            await Task.Delay(250).ConfigureAwait(false);
        }
        return false;
    }

    /// <summary>
    /// 🔴 SC-010 — THE COMPOSITION, MEASURED.
    ///
    /// The real Supervisor starts the real client binary with its own argument shape, the client
    /// answers the supervisor's own ping, the child is KILLED, and the supervisor brings it back
    /// with no operator action.
    ///
    /// This is the test that would have caught the P1 finding: before that fix, the child exited
    /// immediately because it read <c>--listen</c> as a verb, and no amount of green unit tests
    /// said so.
    /// </summary>
    [Fact]
    public async Task The_real_supervisor_starts_the_real_client_and_restarts_it_after_a_kill()
    {
        var binary = FindClientBinary();
        Assert.True(binary is not null,
            "the ynet_client binary was not found under csharp/ynet_client/bin — build it before " +
            "running this test. Skipping silently here would be a vacuous pass, which is the exact " +
            "false-green this feature exists to remove.");

        var port = FreePort();
        var config = new SupervisorConfig
        {
            EngineBinary = binary!,
            Listen = $"127.0.0.1:{port}",
            StoreRoot = _root,
            PingInterval = TimeSpan.FromSeconds(1),
            PingTimeout = TimeSpan.FromSeconds(3),
            StartupBudget = TimeSpan.FromSeconds(30),
            BackoffInitial = TimeSpan.FromMilliseconds(500),
        };

        // The client needs a plane; the supervisor passes only --listen/--store, so the lane's
        // identity comes from the environment exactly as scripts/ynet-m6-run.sh supplies it.
        Environment.SetEnvironmentVariable("YNET_CLIENT_COOP", _root);
        Environment.SetEnvironmentVariable("YNET_CLIENT_LANE", "e2e-lane");
        Environment.SetEnvironmentVariable("YNET_CLIENT_SPOOL", Path.Combine(_root, "spool"));

        using var supervisor = new Supervisor(config);
        using var stop = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        await supervisor.StartAsync(stop.Token);
        try
        {
            // 1. The supervisor started it, and it ANSWERS — not merely exists.
            Assert.True(await WaitUntil(() => PingSucceeds(port, TimeSpan.FromSeconds(3)),
                                       TimeSpan.FromSeconds(40)),
                "the supervised client never answered the supervisor's ping. Before the 2026-09-06 " +
                "P1 fix this is exactly how it failed: the child read '--listen' as a verb and exited.");

            var firstPid = supervisor.EnginePid;
            Assert.NotNull(firstPid);

            // 2. Kill it. This is a crash, not a shutdown.
            using (var child = Process.GetProcessById(firstPid!.Value))
            {
                child.Kill(entireProcessTree: true);
                child.WaitForExit(10_000);
            }

            // 3. The supervisor brings it back, with NO operator action, and the new child answers.
            Assert.True(await WaitUntil(async () =>
                    supervisor.EnginePid is { } pid && pid != firstPid.Value
                    && await PingSucceeds(port, TimeSpan.FromSeconds(3)),
                    TimeSpan.FromSeconds(60)),
                "the supervisor did not bring the client back after a kill");

            Assert.NotEqual(firstPid.Value, supervisor.EnginePid);
        }
        finally
        {
            await supervisor.StopAsync(CancellationToken.None);
            Environment.SetEnvironmentVariable("YNET_CLIENT_COOP", null);
            Environment.SetEnvironmentVariable("YNET_CLIENT_LANE", null);
            Environment.SetEnvironmentVariable("YNET_CLIENT_SPOOL", null);
        }
    }

    /// <summary>
    /// The regression guard for the P1 finding, kept as a cheap standalone check so that a failure
    /// of the expensive test above can be told apart from a re-break of the argument contract.
    ///
    /// The supervisor launches <c>&lt;binary&gt; --listen &lt;addr&gt; --store "&lt;root&gt;"</c>
    /// (<c>Supervisor.cs:396</c>). A first argument beginning with <c>--</c> therefore MUST be
    /// understood as a supervisor launch and not as a verb.
    /// </summary>
    [Fact]
    public async Task A_supervisor_shaped_command_line_starts_the_receiver_rather_than_exiting()
    {
        var binary = FindClientBinary();
        Assert.True(binary is not null, "the ynet_client binary was not found — build it first.");

        var port = FreePort();
        var psi = new ProcessStartInfo(binary!)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // EXACTLY Supervisor.StartChild's shape — no verb.
        psi.ArgumentList.Add("--listen");
        psi.ArgumentList.Add($"127.0.0.1:{port}");
        psi.ArgumentList.Add("--store");
        psi.ArgumentList.Add(_root);
        psi.Environment["YNET_CLIENT_COOP"] = _root;
        psi.Environment["YNET_CLIENT_LANE"] = "e2e-lane";
        psi.Environment["YNET_CLIENT_SPOOL"] = Path.Combine(_root, "spool2");

        using var proc = Process.Start(psi)!;
        try
        {
            Assert.True(await WaitUntil(() => PingSucceeds(port, TimeSpan.FromSeconds(2)),
                                       TimeSpan.FromSeconds(30)),
                "a supervisor-shaped command line did not start the receiver. The client must treat " +
                "a leading '--' argument as a supervisor launch; otherwise it reads it as a verb, " +
                "falls through to default, and exits — which is precisely how M6-d was unmet while " +
                "appearing implemented.");

            Assert.False(proc.HasExited, "the client exited instead of serving");
        }
        finally
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { /* gone */ }
        }
    }
}

// glp_supervisor entry point (T025/T026; contracts/supervision.md).
//
//   glp_supervisor --engine <path\to\glp_engine_host.exe> --listen 127.0.0.1:7461
//                  --store <dir> [--ping-interval 5s] [--ping-timeout 3s]
//                  [--backoff-initial 1s] [--backoff-max 30s] [--backoff-multiplier 2]
//                  [--crash-window 2m] [--crash-threshold 3]
//
//   glp_supervisor --store <dir> --listen <hp> --status     operator liveness query (FR-024)
//   glp_supervisor --store <dir> --listen <hp> --history    crash/restart history (FR-024)
//
// One binary hosts as console (dev/test) and as a Windows service (deploy) —
// .NET generic host + AddWindowsService (FR-025; the call is a no-op off
// Windows, keeping the contract portable).

using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GlpRuntime.Supervisor;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string? engine = null, listen = null, store = null;
        bool statusQuery = false, historyQuery = false;
        var ping = TimeSpan.FromSeconds(5);
        var pingTimeout = TimeSpan.FromSeconds(3);
        var backoffInitial = TimeSpan.FromSeconds(1);
        var backoffMax = TimeSpan.FromSeconds(30);
        double backoffMultiplier = 2.0;
        var crashWindow = TimeSpan.FromMinutes(2);
        int crashThreshold = 3;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--engine" when i + 1 < args.Length: engine = args[++i]; break;
                case "--listen" when i + 1 < args.Length: listen = args[++i]; break;
                case "--store" when i + 1 < args.Length: store = args[++i]; break;
                case "--ping-interval" when i + 1 < args.Length: ping = ParseSpan(args[++i]); break;
                case "--ping-timeout" when i + 1 < args.Length: pingTimeout = ParseSpan(args[++i]); break;
                case "--backoff-initial" when i + 1 < args.Length: backoffInitial = ParseSpan(args[++i]); break;
                case "--backoff-max" when i + 1 < args.Length: backoffMax = ParseSpan(args[++i]); break;
                case "--backoff-multiplier" when i + 1 < args.Length:
                    backoffMultiplier = double.Parse(args[++i]); break;
                case "--crash-window" when i + 1 < args.Length: crashWindow = ParseSpan(args[++i]); break;
                case "--crash-threshold" when i + 1 < args.Length: crashThreshold = int.Parse(args[++i]); break;
                case "--status": statusQuery = true; break;
                case "--history": historyQuery = true; break;
                default:
                    Console.Error.WriteLine($"glp_supervisor: unknown argument '{args[i]}'");
                    return 64;
            }
        }

        if (store is null || listen is null)
        {
            Console.Error.WriteLine("glp_supervisor: --store <dir> and --listen <host:port> are required");
            return 64;
        }

        // ---- operator queries (FR-024) ----
        if (statusQuery || historyQuery)
        {
            var log = new CrashLog(store);
            if (statusQuery)
            {
                var status = log.ReadStatus();
                Console.WriteLine(status is null
                    ? "no supervisor status recorded here"
                    : JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true }));
            }
            if (historyQuery)
            {
                var history = log.History();
                if (history.Count == 0)
                    Console.WriteLine("no crash records");
                foreach (var record in history)
                    Console.WriteLine(JsonSerializer.Serialize(record));
            }
            return 0;
        }

        if (engine is null)
        {
            Console.Error.WriteLine("glp_supervisor: --engine <path> is required to supervise");
            return 64;
        }
        if (!File.Exists(engine))
        {
            Console.Error.WriteLine($"glp_supervisor: engine binary not found: {engine}");
            return 66;
        }

        var config = new SupervisorConfig
        {
            EngineBinary = Path.GetFullPath(engine),
            Listen = listen,
            StoreRoot = Path.GetFullPath(store),
            PingInterval = ping,
            PingTimeout = pingTimeout,
            BackoffInitial = backoffInitial,
            BackoffMax = backoffMax,
            BackoffMultiplier = backoffMultiplier,
            CrashWindow = crashWindow,
            CrashThreshold = crashThreshold,
        };

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddWindowsService(o => o.ServiceName = "glp_supervisor"); // no-op off Windows
        builder.Services.AddSingleton(config);
        builder.Services.AddHostedService<Supervisor>();

        Console.WriteLine($"glp_supervisor: supervising {config.EngineBinary} on {listen} " +
                          $"(ping every {ping}, timeout {pingTimeout})");
        await builder.Build().RunAsync().ConfigureAwait(false);
        return 0;
    }

    /// <summary>Parse "5s" / "300ms" / "2m" / a bare-seconds number.</summary>
    internal static TimeSpan ParseSpan(string text)
    {
        if (text.EndsWith("ms", StringComparison.Ordinal))
            return TimeSpan.FromMilliseconds(double.Parse(text[..^2]));
        if (text.EndsWith("s", StringComparison.Ordinal))
            return TimeSpan.FromSeconds(double.Parse(text[..^1]));
        if (text.EndsWith("m", StringComparison.Ordinal))
            return TimeSpan.FromMinutes(double.Parse(text[..^1]));
        return TimeSpan.FromSeconds(double.Parse(text));
    }
}

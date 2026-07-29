// glp_engine_host entry point (T011). Bootstraps the engine (root
// programs/self.glp prelude, FR-003), installs the link layer (the 025 kernels
// + tcp/loopback leaves — the engine side of the split owns all language
// context; the client has none, R7), then serves the split protocol on
// --listen until SHUTDOWN (exit 0) or a fatal startup error.
//
//   glp_engine_host --listen 127.0.0.1:7461
//
// US2 adds --store <dir> / --from-snapshot latest|<seq> (T019/T022).

using System.Net;

using GlpRuntime.Engine;
using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.EngineHost;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        IPEndPoint? listen = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--listen" when i + 1 < args.Length:
                    listen = ParseEndpoint(args[++i]);
                    break;
                default:
                    Console.Error.WriteLine($"glp_engine_host: unknown argument '{args[i]}'");
                    Console.Error.WriteLine("usage: glp_engine_host --listen <host:port>");
                    return 64;
            }
        }
        if (listen is null)
        {
            Console.Error.WriteLine("glp_engine_host: --listen <host:port> is required");
            return 64;
        }

        string rootSelfGlpPath;
        try
        {
            rootSelfGlpPath = ResolveRootSelfGlpPath();
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"glp_engine_host: {ex.Message}");
            return 66;
        }

        // FR-003: the ENGINE bootstraps the prelude; the client stays thin.
        var engine = new GlpEngine(rootSelfGlpPath);

        // Composition root (025 pattern, mirrors out/csharp/glp_repl/Program.cs):
        // install the link kernels + the MVP transport leaves. QUIC + macaroon
        // gating stay REPL-exe concerns until a snapshot carries a quic link.
        var link = LinkKernels.Install(engine.Runtime);
        link.Transports.Register(new TcpTransport());
        link.Transports.Register(new LoopbackTransport());

        var session = new EngineSession($"engine-{listen.Port}");
        session.TransitionTo(EngineState.Serving);

        var dispatcher = new RequestDispatcher(engine, session);
        var server = new EngineServer(listen, dispatcher);

        Console.WriteLine($"glp_engine_host: prelude {rootSelfGlpPath}");
        Console.WriteLine($"glp_engine_host: serving on {listen} (one client, FR-002)");

        try
        {
            await server.RunAsync().ConfigureAwait(false);
        }
        catch (EngineServerException ex)
        {
            Console.Error.WriteLine($"glp_engine_host: {ex.Message}");
            return 65;
        }

        Console.WriteLine("glp_engine_host: shutdown complete");
        return 0;
    }

    private static IPEndPoint ParseEndpoint(string text)
    {
        var idx = text.LastIndexOf(':');
        if (idx <= 0 || idx == text.Length - 1 ||
            !int.TryParse(text[(idx + 1)..], out var port) || port is < 1 or > 65535)
        {
            throw new EngineServerException($"--listen expects <host:port>, got '{text}'");
        }
        var host = text[..idx];
        var ip = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            ? IPAddress.Loopback
            : IPAddress.Parse(host);
        return new IPEndPoint(ip, port);
    }

    /// <summary>
    /// Walk up from AppContext.BaseDirectory (then the current directory) to the
    /// repo root containing programs/self.glp — the same fail-loud resolution the
    /// single-process REPL uses (out/csharp/bin/glp_repl.cs).
    /// </summary>
    internal static string ResolveRootSelfGlpPath()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "programs", "self.glp");
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }
        throw new InvalidOperationException(
            "could not locate programs/self.glp by walking up from " +
            $"{AppContext.BaseDirectory} or {Environment.CurrentDirectory}; the engine host " +
            "must run from within a checkout of the glpnet repository.");
    }
}

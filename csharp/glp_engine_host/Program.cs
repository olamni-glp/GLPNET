// glp_engine_host entry point (T011/T019/T022). Bootstraps the engine (root
// programs/self.glp prelude, FR-003), installs the link layer (the 025 kernels
// + tcp/loopback leaves — the engine side of the split owns all language
// context; the client has none, R7), then serves the split protocol on
// --listen until SHUTDOWN (exit 0) or a fatal startup error.
//
//   glp_engine_host --listen 127.0.0.1:7461
//                   [--store <dir>]                    file-fallback root
//                                                      (default <repo>/.glpsnap)
//                   [--from-snapshot latest|<seq>]     restore before serving
//
// Snapshot store (FR-012): primary = PGLite over the repo's bridge-guarded
// cluster when GLP_SNAPSHOT_PG_CONN is set (041 precedent); fallback = the
// gitignored file store under --store. No primary configured ⇒ every write
// reports the degradation loudly (US2/AS-4).
//
// Restore (FR-030) runs to completion BEFORE the listener opens: a client
// connecting mid-restore sees a connection refusal (transport-level), never an
// answer from a half-restored state. (US4/T033 revisits mid-restore STATUS.)

using System.Net;

using GlpRuntime.Engine;
using GlpRuntime.EngineHost.Snapshot;
using GlpRuntime.EngineHost.Store;
using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.EngineHost;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        IPEndPoint? listen = null;
        string? storeDir = null;
        string? fromSnapshot = null;
        var serveMode = EngineServeMode.SingleClient; // 061 FR-002 default; 064 US2 opt-in below
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--listen" when i + 1 < args.Length:
                    listen = ParseEndpoint(args[++i]);
                    break;
                case "--store" when i + 1 < args.Length:
                    storeDir = args[++i];
                    break;
                case "--from-snapshot" when i + 1 < args.Length:
                    fromSnapshot = args[++i];
                    break;
                case "--multi-client":
                    serveMode = EngineServeMode.MultiClient; // 064 US2/FR-005
                    break;
                default:
                    Console.Error.WriteLine($"glp_engine_host: unknown argument '{args[i]}'");
                    Console.Error.WriteLine(
                        "usage: glp_engine_host --listen <host:port> [--store <dir>] [--from-snapshot latest|<seq>] [--multi-client]");
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
        var rootSelfSource = File.ReadAllText(rootSelfGlpPath);

        var engineIdentity = $"engine-{listen.Port}";
        var session = new EngineSession(engineIdentity);

        // ---- snapshot store composition (T020/T021) ----
        storeDir ??= Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(rootSelfGlpPath))!, ".glpsnap");
        var fileBackend = new FileSnapshotStore(storeDir, engineIdentity);
        ISnapshotBackend? primary = null;
        var pgConn = Environment.GetEnvironmentVariable("GLP_SNAPSHOT_PG_CONN");
        if (!string.IsNullOrWhiteSpace(pgConn))
        {
            try
            {
                primary = PgliteSnapshotStore.Open(new NpgsqlSnapshotDb(pgConn), engineIdentity);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"glp_engine_host: primary snapshot store unavailable at startup ({ex.Message}) — " +
                    "continuing on the file fallback (US2/AS-4)");
            }
        }
        var store = new SnapshotStore(primary, fileBackend,
            msg => Console.Error.WriteLine($"glp_engine_host: SNAPSHOT STORE DEGRADED: {msg}"));

        // ---- engine: fresh, or restored from a snapshot (FR-030) ----
        GlpEngine engine;
        IReadOnlyList<LoadedUnit> restoredUnits = Array.Empty<LoadedUnit>();
        IReadOnlyList<RestoredLinkDefinition> restoredLinks = Array.Empty<RestoredLinkDefinition>();
        if (fromSnapshot is not null)
        {
            session.TransitionTo(EngineState.Restoring);
            byte[]? blobBytes;
            ulong? expectedSeq = null;
            if (fromSnapshot == "latest")
            {
                var latest = store.Latest();
                if (latest is null)
                {
                    Console.Error.WriteLine("glp_engine_host: --from-snapshot latest: no snapshot exists");
                    return 66;
                }
                blobBytes = latest.Value.Blob;
                expectedSeq = latest.Value.Seq;
            }
            else if (ulong.TryParse(fromSnapshot, out var seq))
            {
                blobBytes = store.BySeq(seq);
                if (blobBytes is null)
                {
                    Console.Error.WriteLine($"glp_engine_host: --from-snapshot {seq}: no such complete snapshot");
                    return 66;
                }
                expectedSeq = seq;
            }
            else
            {
                Console.Error.WriteLine(
                    $"glp_engine_host: --from-snapshot expects latest|<seq>, got '{fromSnapshot}'");
                return 64;
            }

            try
            {
                var blob = SnapshotBlob.Decode(blobBytes);
                // Header-context checks (codexreview 20260730T070051Z): a store
                // mis-index (blob under the wrong seq) is corruption — refuse; a
                // foreign engine identity is a legitimate operator migration but
                // never silent.
                if (expectedSeq is ulong want && blob.Seq != want)
                    throw new SnapshotException(
                        $"store returned blob seq={blob.Seq} for requested seq={want} (store mis-index)");
                if (!string.Equals(blob.EngineIdentity, engineIdentity, StringComparison.Ordinal))
                    Console.Error.WriteLine(
                        $"glp_engine_host: WARNING restoring snapshot of '{blob.EngineIdentity}' " +
                        $"into '{engineIdentity}' (cross-identity restore — operator intent assumed)");
                var restored = SnapshotRestore.Restore(blob, rootSelfGlpPath);
                engine = restored.Engine;
                restoredUnits = restored.Units;
                restoredLinks = restored.Links;
                Console.WriteLine(
                    $"glp_engine_host: restored snapshot seq={blob.Seq} " +
                    $"({restoredUnits.Count} unit(s), heap={engine.Runtime.Heap.Hp} cells, " +
                    $"{restoredLinks.Count} link definition(s))");
            }
            catch (Exception ex)
            {
                // ANY decode/restore failure is a restore failure — the supervisor's
                // DEF-F2 previous-seq fallback gates on this exact "RESTORE FAILED"
                // marker (Supervisor.TryStartRestoringAsync), so a corrupt blob whose
                // damage surfaces as an OverflowException / codec exception deep
                // inside a section must take the SAME loud path as a SnapshotException
                // — otherwise the supervisor re-drives the corrupt seq into
                // repeated_immediate_crash instead of falling back (codexreview
                // 20260730T070051Z corrupt-section-exceptions-bypass-taxonomy).
                Console.Error.WriteLine(
                    $"glp_engine_host: RESTORE FAILED: {ex.GetType().Name}: {ex.Message}");
                return 65;
            }
            session.TransitionTo(EngineState.Serving);
        }
        else
        {
            // FR-003: the ENGINE bootstraps the prelude; the client stays thin.
            engine = new GlpEngine(rootSelfGlpPath);
            session.TransitionTo(EngineState.Serving);
        }

        // Composition root (025 pattern, mirrors out/csharp/glp_repl/Program.cs):
        // install the link kernels + the MVP transport leaves. QUIC + macaroon
        // gating stay REPL-exe concerns until a snapshot carries a quic link.
        var link = LinkKernels.Install(engine.Runtime);
        link.Transports.Register(new TcpTransport());
        link.Transports.Register(new LoopbackTransport());

        // Host policy: with a live link, the engine's pump loop camps on
        // TryApplyNext for InboundPumpWait per idle break (default 30 s — a REPL
        // camping for frames is fine; a request/response server is not). Keep
        // link-goal requests prompt; frames arriving between requests are applied
        // by the next request's drain (US4/T034 relies on this cadence).
        engine.InboundPumpWait = TimeSpan.FromMilliseconds(100);

        // US4/T032 restore-order gating: with the link layer installed, re-establish
        // every restored link definition per its recorded role in the background
        // (peer-unreachable ⇒ local work proceeds); adoption lands on the request
        // thread via RequestDispatcher → LinkRewirer.ApplyReady.
        LinkRewirer? rewirer = null;
        if (restoredLinks.Count > 0)
        {
            rewirer = new LinkRewirer(engine.Runtime, link);
            rewirer.Begin(restoredLinks);
            Console.WriteLine(
                $"glp_engine_host: re-establishing {restoredLinks.Count} link(s) from snapshot definitions");
        }

        var quiescence = new Quiescence(engine);
        var dispatcher = new RequestDispatcher(
            engine, session, quiescence, store, link, rootSelfSource, restoredUnits, rewirer);
        var server = new EngineServer(listen, dispatcher, serveMode);

        // The wire is unauthenticated (LOAD_SOURCE executes arbitrary GLP;
        // SHUTDOWN stops the engine) — loopback is the MVP contract. A
        // non-loopback bind is an explicit operator choice; warn loudly.
        if (!IPAddress.IsLoopback(listen.Address))
        {
            Console.Error.WriteLine(
                $"glp_engine_host: WARNING --listen {listen} is NOT loopback: the split protocol is " +
                "unauthenticated (any connecting peer can load and run code). TCP loopback is the MVP " +
                "trust boundary (spec FR-001/plan constraint); non-loopback exposure is at your own risk.");
        }

        Console.WriteLine($"glp_engine_host: prelude {rootSelfGlpPath}");
        Console.WriteLine($"glp_engine_host: snapshot store {storeDir}" +
                          (primary is null ? " (file fallback only — no PGLite primary configured)" : " + PGLite primary"));
        Console.WriteLine(serveMode == EngineServeMode.MultiClient
            ? $"glp_engine_host: serving on {listen} (multi-client, 064 FR-005)"
            : $"glp_engine_host: serving on {listen} (one client, FR-002)");

        try
        {
            await server.RunAsync().ConfigureAwait(false);
        }
        catch (EngineServerException ex)
        {
            Console.Error.WriteLine($"glp_engine_host: {ex.Message}");
            return 65;
        }
        finally
        {
            rewirer?.Dispose(); // established-but-never-adopted endpoints
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

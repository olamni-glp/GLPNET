// Shared fixtures for the 064 US3 suites: repo-root resolution (the same
// walk-up the engine host and IL client use) and an in-process
// RequestDispatcher fixture (no TCP — the wire framing is covered by the
// round-trip suite; the dispatcher IS the request/response semantics).

using GlpRuntime.Engine;
using GlpRuntime.EngineHost;
using GlpRuntime.EngineHost.Store;

namespace GlpRuntime.SplitProtocol.Tests;

internal static class TestRepo
{
    public static string Root { get; } = FindRoot();

    public static string RootSelfGlp => Path.Combine(Root, "programs", "self.glp");

    public static string Typed(string rel) =>
        Path.Combine(Root, "programs", "tests", "typed", rel);

    public static string Csharp(string rel) => Path.Combine(Root, "csharp", rel);

    private static string FindRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "programs", "self.glp")))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        throw new InvalidOperationException(
            "could not locate the glpnet repo root (programs/self.glp) from " +
            AppContext.BaseDirectory);
    }
}

/// <summary>One in-process engine-host dispatcher (fresh engine, file store).</summary>
internal sealed class DispatcherFixture : IDisposable
{
    public RequestDispatcher Dispatcher { get; }

    private readonly string _storeDir;

    public DispatcherFixture()
    {
        var engine = new GlpEngine(TestRepo.RootSelfGlp);
        var session = new EngineSession("split-protocol-test");
        session.TransitionTo(EngineState.Serving);
        _storeDir = Path.Combine(Path.GetTempPath(), $"glpsplit-test-{Guid.NewGuid():N}");
        var store = new SnapshotStore(
            primary: null,
            fallback: new FileSnapshotStore(_storeDir, "split-protocol-test"),
            report: _ => { });
        Dispatcher = new RequestDispatcher(
            engine, session, new Quiescence(engine), store,
            linkRuntime: null, rootSelfSource: File.ReadAllText(TestRepo.RootSelfGlp));
    }

    public Task<ResponseFrame> SendAsync(RequestFrame request) =>
        Dispatcher.DispatchAsync(request);

    public void Dispose()
    {
        try { Directory.Delete(_storeDir, recursive: true); } catch (IOException) { }
    }
}

// Shared support for the US2 store tests (feature 041-crdtmsg-mvp): a concrete convergent CRDT (an
// LWW-register map keyed on the op's dot) so convergence/rebuild are testable against real state. The
// store itself is CRDT-agnostic; US3 plugs in Fugue/Peritext op bodies.

using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Store;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.CrdtMsg.Tests;

internal sealed class TempDir : IDisposable
{
    public string Path { get; }
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "crdtmsg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }
    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { /* best-effort cleanup */ }
    }
}

/// <summary>LWW-register map: op payload = set(key, value); the winner per key is the max dot.</summary>
internal static class LwwMap
{
    public static byte[] SetPayload(string key, string value)
    {
        var w = new ByteWriter();
        w.WriteString(key);
        w.WriteString(value);
        return w.TakeBytes();
    }

    public static Op Set(string peer, long counter, string key, string value, IReadOnlyList<Dot>? deps = null) =>
        Op.Create(new Dot(peer, counter), deps ?? Array.Empty<Dot>(), SetPayload(key, value));

    public static Projection<Dictionary<string, (string Val, Dot Dot)>> Projection() =>
        new(
            seed: () => new Dictionary<string, (string, Dot)>(),
            apply: (state, op) =>
            {
                var r = new ByteReader(op.Payload);
                string key = r.ReadString();
                string val = r.ReadString();
                if (!state.TryGetValue(key, out var cur) || op.Id.CompareTo(cur.Dot) > 0)
                    state[key] = (val, op.Id);
                return state;
            });

    /// <summary>Canonical text of the projected state (sorted) — for order-independent equality asserts.</summary>
    public static string Canonicalize(Dictionary<string, (string Val, Dot Dot)> state) =>
        string.Join("|", state.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                              .Select(kv => $"{kv.Key}={kv.Value.Val}@{kv.Value.Dot}"));

    /// <summary>Rebuild + canonicalize the projected state from a store (any IOpWal backend, 048 T012).</summary>
    public static string StateOf(IOpWal wal) => Canonicalize(Projection().Rebuild(wal.Ops));

    /// <summary>A deterministic op pool: two peers, overlapping keys incl. concurrent writes.</summary>
    public static List<Op> Pool()
    {
        var ops = new List<Op>();
        for (long i = 1; i <= 15; i++)
        {
            ops.Add(Set("alice", i, $"k{i % 5}", $"a{i}"));
            ops.Add(Set("bob", i, $"k{i % 5}", $"b{i}")); // concurrent same-key writes across peers
        }
        return ops;
    }
}

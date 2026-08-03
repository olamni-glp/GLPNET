using System.Collections.Concurrent;
using System.Net.Quic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;

namespace GlpQuick.Host;

/// <summary>
/// The C# QUIC+WS endpoint the glp_quick control plane launches (FR-007). One process = one role:
/// <c>--role client</c> runs one link bridged to stdio; <c>--role server</c> is a multi-accept
/// <b>mesh router</b> (US2) — one <see cref="QuicTransport.QuicListenerHandle"/> accepting up to
/// <c>--max-clients</c> isolated links, routing L5 envelopes by <c>to</c>/<c>broadcast</c> among the
/// clients and the server's own stdio endpoint. Real QUIC only (FR-001).
/// </summary>
internal static class Program
{
    // FR-019 failure tokens → distinct non-zero exit codes.
    private const int ExitOk = 0, ExitUsage = 2, ExitCertMismatch = 3, ExitServerNotReady = 4,
        ExitUdpBlocked = 5, ExitQuicUnsupported = 6, ExitBindFailed = 7;

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false); // no BOM
        var stdout = Console.Out;
        stdout.NewLine = "\n";

        Opts opts;
        try { opts = Opts.Parse(args); }
        catch (Exception ex) { Console.Error.WriteLine($"ERR usage {ex.Message}"); return ExitUsage; }

        if (!QuicTransport.IsSupported)
        {
            Console.Error.WriteLine("ERR quic_unsupported QuicListener/QuicConnection.IsSupported=false (msquic missing); real QUIC only (FR-001)");
            return ExitQuicUnsupported;
        }

        X509Certificate2 cert;
        string pin;
        try
        {
            cert = X509CertificateLoader.LoadPkcs12(File.ReadAllBytes(Path.Combine(opts.CertDir, "glpquick.pfx")), null,
                X509KeyStorageFlags.Exportable);
            pin = File.ReadAllText(Path.Combine(opts.CertDir, "glpquick.fingerprint")).Trim();
        }
        catch (Exception ex) { Console.Error.WriteLine($"ERR cert_load {ex.Message}"); return ExitBindFailed; }

        var transport = new QuicTransport(cert, pin);
        var addr = LinkAddress.Endpoint(opts.Addr, opts.Port);
        using var life = new CancellationTokenSource();

        try
        {
            return opts.Role == "server"
                ? await RunMeshServerAsync(transport, addr, opts, stdout, life)
                : await RunClientAsync(transport, addr, opts, stdout, life);
        }
        catch (AuthenticationException ex) { Console.Error.WriteLine($"ERR cert_mismatch {ex.Message}"); return ExitCertMismatch; }
        catch (QuicException ex) when (ex.QuicError is QuicError.ConnectionTimeout or QuicError.ConnectionAborted)
        { Console.Error.WriteLine($"ERR server_not_ready {ex.QuicError}: {ex.Message}"); return ExitServerNotReady; }
        catch (QuicException ex) { Console.Error.WriteLine($"ERR udp_blocked {ex.QuicError}: {ex.Message}"); return ExitUdpBlocked; }
        catch (System.Net.Sockets.SocketException ex) { Console.Error.WriteLine($"ERR bind_failed {ex.SocketErrorCode}: {ex.Message}"); return ExitBindFailed; }
    }

    // ---------------------------------------------------------------- client role (US1)
    private static async Task<int> RunClientAsync(QuicTransport transport, LinkAddress addr, Opts opts, TextWriter stdout, CancellationTokenSource life)
    {
        var endpoint = await ConnectWithReadinessAsync(transport, addr, opts.Retry, life.Token).ConfigureAwait(false);
        Console.Error.WriteLine($"LINK_UP {endpoint.Id}");
        endpoint.OnFault += f => Console.Error.WriteLine($"FAULT {f.Kind} {f.Reason}");

        // 063 US1 T011 (contract C1): with --repl, a live GLP REPL child is bridged onto this link —
        // a directed inbound envelope's payload is one goal line to the child; each of the child's
        // rendered result blocks returns as ONE envelope to the goal's sender.
        ReplChild? repl = null;
        if (opts.ReplPath is not null)
            repl = ReplChild.Spawn(opts.ReplPath,
                (requester, page, block) => SendSafeAsync(endpoint,
                    ComposeEnvelope(opts.SelfId, requester, Tmsg.ReplResult(page, block)), life.Token),
                exit => _ = SendSafeAsync(endpoint,
                    ComposeEnvelope(opts.SelfId, Broadcast, Tmsg.LinkStatus("repl_down", $"repl child exited ({exit})")), life.Token));

        // The client lives for the LINK's lifetime, NOT stdin's: stdin EOF (a one-shot / piped /
        // non-interactive shell) must NOT tear the link down — it only stops accepting new sends.
        // We exit when the peer closes the link (recv returns) or the process is killed.
        _ = StdinToLinkAsync(endpoint, life.Token);
        await LinkToStdoutAsync(endpoint, stdout, life, repl, opts.SelfId).ConfigureAwait(false);
        life.Cancel();
        if (repl is not null) await repl.DisposeAsync().ConfigureAwait(false);
        await endpoint.DisposeAsync().ConfigureAwait(false);
        return ExitOk;
    }

    private const string Broadcast = "broadcast";

    /// <summary>One L5 envelope (wire-contract.md shape) composed host-side for REPL replies/notices.</summary>
    private static byte[] ComposeEnvelope(string from, string to, string payload)
    {
        var obj = new Dictionary<string, object?>
        {
            ["msg_id"] = Guid.NewGuid().ToString("N"),
            ["from"] = from,
            ["to"] = to,
            ["seq"] = null,
            ["payload"] = payload,
        };
        return JsonSerializer.SerializeToUtf8Bytes(obj);
    }

    /// <summary>Send one frame, degrading a transport failure to an explicit DROP (never a crash).</summary>
    private static async Task SendSafeAsync(ILinkEndpoint link, byte[] frame, CancellationToken ct)
    {
        try { await link.SendBytesAsync(frame, ct).ConfigureAwait(false); }
        catch (Exception ex) when (ex is QuicException or IOException or ObjectDisposedException)
        { Console.Error.WriteLine($"DROP send-failed {link.Id}: {ex.Message}"); }
    }

    /// <summary>Feed one directed inbound <c>tmsg(repl_goal, "Page", "goal.")</c> to the REPL child
    /// (the one 040 terminal codec — chat/page/etc. payloads are NOT goals and never feed the REPL);
    /// a dead child answers with an explicit repl_result refusal, never a silent stall (C1).</summary>
    private static async Task FeedReplAsync(ReplChild repl, string selfId, string from, string payload,
        Func<byte[], Task> sendBack)
    {
        if (string.IsNullOrEmpty(from) || !Tmsg.TryParseReplGoal(payload, out var page, out var goal)) return;
        if (repl.Alive) repl.Feed(from, page, goal);
        else await sendBack(ComposeEnvelope(selfId, from, Tmsg.ReplResult(page, "[repl error: repl_down]"))).ConfigureAwait(false);
    }

    private static async Task<ILinkEndpoint> ConnectWithReadinessAsync(QuicTransport transport, LinkAddress addr, bool retry, CancellationToken ct)
    {
        Console.Error.WriteLine($"READY client {addr}");
        var deadline = DateTime.UtcNow + (retry ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(8));
        while (true)
        {
            try { return await transport.ConnectAsync(LinkScheme.Quic, addr, LinkOptions.Default, ct).ConfigureAwait(false); }
            catch (QuicException ex) when (retry
                && (ex.QuicError is QuicError.ConnectionTimeout or QuicError.ConnectionRefused) && DateTime.UtcNow < deadline)
            {
                Console.Error.WriteLine($"WAIT server_not_ready {ex.QuicError} — retrying");
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }
    }

    private static async Task StdinToLinkAsync(ILinkEndpoint endpoint, CancellationToken ct)
    {
        using var stdin = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        string? line;
        while (!ct.IsCancellationRequested && (line = await stdin.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            if (line.Length > 0)
                await endpoint.SendBytesAsync(Encoding.UTF8.GetBytes(line), ct).ConfigureAwait(false);
    }

    private static async Task LinkToStdoutAsync(ILinkEndpoint endpoint, TextWriter stdout, CancellationTokenSource life,
        ReplChild? repl = null, string selfId = "server")
    {
        while (!life.IsCancellationRequested)
        {
            byte[]? frame = await endpoint.RecvBytesAsync(life.Token).ConfigureAwait(false);
            if (frame is null) { Console.Error.WriteLine("LINK_CLOSED"); life.Cancel(); return; }
            await stdout.WriteLineAsync(Encoding.UTF8.GetString(frame)).ConfigureAwait(false);
            await stdout.FlushAsync().ConfigureAwait(false);
            // T011: a directed envelope addressed to THIS endpoint is a goal for the bridged REPL
            // (broadcasts are chat; third-party-directed envelopes are never fed — E2 finding host-15).
            if (repl is not null && TryParseEnvelope(frame, out var from, out var to, out var payload) && to == selfId)
                await FeedReplAsync(repl, selfId, from, payload,
                    bytes => SendSafeAsync(endpoint, bytes, life.Token)).ConfigureAwait(false);
        }
    }

    private static bool TryParseEnvelope(byte[] frame, out string from, out string to, out string payload)
    {
        from = ""; to = ""; payload = "";
        try
        {
            using var doc = JsonDocument.Parse(frame);
            var r = doc.RootElement;
            from = r.GetProperty("from").GetString() ?? "";
            to = r.GetProperty("to").GetString() ?? "";
            payload = r.GetProperty("payload").GetString() ?? "";
            return true;
        }
        catch { return false; }
    }

    // ---------------------------------------------------------------- server role: mesh router (US2)
    private static async Task<int> RunMeshServerAsync(QuicTransport transport, LinkAddress addr, Opts opts, TextWriter stdout, CancellationTokenSource life)
    {
        await using var listener = await transport.CreateListenerAsync(addr, LinkOptions.Default, life.Token).ConfigureAwait(false);
        Console.Error.WriteLine($"READY server {addr}"); // listener bound — clients may connect

        // 063 US1 T011 (contract C1): with --repl, envelopes directed to SelfId also feed the live
        // REPL child; each rendered block routes back to the goal's sender through the mesh.
        // The mesh is constructed BEFORE the child spawns so the death callback can never observe
        // an unassigned mesh, even for a child that exits immediately (E2 finding host-06); the
        // self-directed hook reads the repl local through the closure and no-ops until assignment.
        Mesh mesh = null!;
        ReplChild? repl = null;
        mesh = new Mesh(opts.SelfId, stdout, opts.ReplPath is null
            ? null
            : (from, payload) =>
            {
                var r = repl;
                return r is null
                    ? Task.CompletedTask
                    : FeedReplAsync(r, opts.SelfId, from, payload,
                        bytes => mesh.RouteAsync(bytes, fromSelf: true, srcLink: null, life.Token));
            });
        if (opts.ReplPath is not null)
            repl = ReplChild.Spawn(opts.ReplPath,
                (requester, page, block) => mesh.RouteAsync(
                    ComposeEnvelope(opts.SelfId, requester, Tmsg.ReplResult(page, block)), fromSelf: true, srcLink: null, life.Token),
                exit => _ = mesh.RouteAsync(ComposeEnvelope(opts.SelfId, Broadcast,
                    Tmsg.LinkStatus("repl_down", $"repl child exited ({exit})")), fromSelf: true, srcLink: null, life.Token));

        // The server's own stdio endpoint participates as `SelfId` (preserves US1: client→server).
        var selfPump = SelfStdioPumpAsync(mesh, life.Token);

        // 064 US1 T012 (research D3, FR-004): with --bridge-port, a Gleam-facing plain-TCP
        // acceptor joins Gleam peers to this mesh as ordinary clients — same pump, same routing,
        // same capacity budget (the bridge is a relay, not a protocol translator).
        var capacity = new ClientCapacity(opts.MaxClients);
        Task bridge = opts.BridgePort is int bridgePort
            ? BridgeAcceptor.RunAsync(mesh, LinkAddress.Endpoint(opts.BridgeAddr, bridgePort), capacity, life.Token)
            : Task.CompletedTask;

        while (!life.IsCancellationRequested)
        {
            ILinkEndpoint link;
            try { link = await listener.AcceptAsync(life.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (QuicException ex) { Console.Error.WriteLine($"ACCEPT_FAULT {ex.QuicError}: {ex.Message}"); continue; }

            if (!capacity.TryAdmit())
            {
                Console.Error.WriteLine($"REJECT over_capacity {link.Id}"); // T026: clear over-capacity reject
                _ = RejectOverCapacityAsync(link);
                continue;
            }
            Console.Error.WriteLine($"CLIENT_UP {link.Id} ({capacity.Active}/{capacity.Max})");
            _ = ClientPumpAsync(mesh, link, capacity.Release, life.Token);
        }
        await selfPump.ConfigureAwait(false);
        await bridge.ConfigureAwait(false);
        if (repl is not null) await repl.DisposeAsync().ConfigureAwait(false);
        return ExitOk;
    }

    /// <summary>Pump the server's own stdio endpoint into the mesh (envelopes from/to <c>SelfId</c>).</summary>
    private static async Task SelfStdioPumpAsync(Mesh mesh, CancellationToken ct)
    {
        using var stdin = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        string? line;
        while (!ct.IsCancellationRequested && (line = await stdin.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            if (line.Length > 0)
                await mesh.RouteAsync(Encoding.UTF8.GetBytes(line), fromSelf: true, srcLink: null, ct).ConfigureAwait(false);
    }

    /// <summary>One isolated client link: register it, route its envelopes, and clean up on drop
    /// (FR-006/SC-004). Internal: the T012 bridge acceptor pumps its TCP-accepted Gleam peers
    /// through this SAME path, so a bridge peer is indistinguishable from a QUIC client here.</summary>
    internal static async Task ClientPumpAsync(Mesh mesh, ILinkEndpoint link, Action onGone, CancellationToken ct)
    {
        link.OnFault += f => Console.Error.WriteLine($"FAULT {f.Kind} {f.Reason}");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                byte[]? frame = await link.RecvBytesAsync(ct).ConfigureAwait(false);
                if (frame is null) break; // client gone → leave siblings untouched
                await mesh.RouteAsync(frame, fromSelf: false, srcLink: link, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        finally
        {
            mesh.Remove(link);
            onGone();
            Console.Error.WriteLine($"CLIENT_DOWN {link.Id}");
            await link.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal static async Task RejectOverCapacityAsync(ILinkEndpoint link)
    {
        try
        {
            var env = "{\"msg_id\":\"capacity\",\"from\":\"server\",\"to\":\"_overflow\",\"seq\":null,\"payload\":\"over_capacity\"}";
            await link.SendBytesAsync(Encoding.UTF8.GetBytes(env)).ConfigureAwait(false);
        }
        catch { /* best-effort notice */ }
        await link.DisposeAsync().ConfigureAwait(false);
    }

    private sealed record Opts(string Role, string Addr, int Port, string CertDir, int MaxClients, bool Retry, string SelfId, string? ReplPath,
        int? BridgePort, string BridgeAddr)
    {
        public static Opts Parse(string[] args)
        {
            string? role = null, addr = null, cert = null, selfId = "server", replPath = null;
            int port = 0, maxClients = 3;
            int? bridgePort = null;
            string bridgeAddr = "127.0.0.1"; // Gleam peers dial loopback by default (bridge on the mesh host)
            bool retry = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--role": role = Req(args, ++i); break;
                    case "--addr": addr = Req(args, ++i); break;
                    case "--port": port = int.Parse(Req(args, ++i)); break;
                    case "--cert": cert = Req(args, ++i); break;
                    case "--max-clients": maxClients = int.Parse(Req(args, ++i)); break;
                    case "--id": selfId = Req(args, ++i); break;
                    case "--repl": replPath = Req(args, ++i); break;
                    case "--bridge-port": bridgePort = int.Parse(Req(args, ++i)); break; // T012: Gleam-facing TCP acceptor
                    case "--bridge-addr": bridgeAddr = Req(args, ++i); break;
                    case "--retry": retry = true; break;
                    default: throw new ArgumentException($"unknown arg '{args[i]}'");
                }
            }
            if (role is not ("server" or "client")) throw new ArgumentException("--role must be server|client");
            if (string.IsNullOrWhiteSpace(addr)) throw new ArgumentException("--addr required");
            if (port is < 1 or > 65535) throw new ArgumentException("--port in [1,65535] required");
            if (string.IsNullOrWhiteSpace(cert)) throw new ArgumentException("--cert <dir> required");
            if (bridgePort is < 1 or > 65535) throw new ArgumentException("--bridge-port in [1,65535] required");
            if (bridgePort is not null && role != "server") throw new ArgumentException("--bridge-port requires --role server (the bridge is a mesh-server leg)");
            return new Opts(role, addr, port, cert, maxClients, retry, selfId!, replPath, bridgePort, bridgeAddr);
        }

        private static string Req(string[] args, int i) =>
            i < args.Length ? args[i] : throw new ArgumentException("missing value for last flag");
    }
}

/// <summary>
/// The server-side mesh router (US2 T027): keeps the server's own stdio endpoint plus a registry of
/// connected client links keyed by their announced endpoint_id, and routes each L5 envelope by its
/// <c>to</c> field (a specific endpoint or <c>broadcast</c>). A client's id is learned from the
/// <c>from</c> of its first envelope. Routing reads only <c>from</c>/<c>to</c>; the original frame
/// bytes are forwarded unchanged (msg_id/seq/payload preserved).
/// </summary>
internal sealed class Mesh
{
    private const string Broadcast = "broadcast";
    private readonly string _selfId;
    private readonly TextWriter _stdout;
    private readonly SemaphoreSlim _stdoutLock = new(1, 1);
    private readonly ConcurrentDictionary<string, ILinkEndpoint> _byId = new();
    private readonly ConcurrentDictionary<ILinkEndpoint, string> _idOf = new();
    private readonly Func<string, string, Task>? _onSelfDirected;

    /// <param name="onSelfDirected">Optional (from, payload) hook invoked for envelopes directed to
    /// <paramref name="selfId"/> — the T011 REPL-bridge seam. Broadcasts never hit it (chat, not goals).</param>
    public Mesh(string selfId, TextWriter stdout, Func<string, string, Task>? onSelfDirected = null)
    {
        _selfId = selfId;
        _stdout = stdout;
        _onSelfDirected = onSelfDirected;
    }

    public void Remove(ILinkEndpoint link)
    {
        if (_idOf.TryRemove(link, out var id))
            // Only evict the id->link mapping if it still points at THIS link — never drop a live
            // sibling that has since taken over the same announced id (routing-loss / data-loss guard).
            _byId.TryRemove(new KeyValuePair<string, ILinkEndpoint>(id, link));
    }

    /// <summary>
    /// Learn/refresh a link's announced id. NEVER evicts or hijacks a live incumbent already holding
    /// <paramref name="from"/>: first-come owns the routable id; a duplicate id from a different live
    /// link is tracked (so its cleanup is clean) but not made addressable under the taken name — so one
    /// client cannot silently steal another's route nor evict it on drop (FR-006/SC-004).
    /// </summary>
    private void Register(ILinkEndpoint srcLink, string from)
    {
        if (_idOf.TryGetValue(srcLink, out var known))
        {
            if (known == from) return;  // already registered under this id
            // this link changed its announced id: release the old mapping only if it still points at us
            _byId.TryRemove(new KeyValuePair<string, ILinkEndpoint>(known, srcLink));
        }
        _idOf[srcLink] = from;  // always track the declared id (clean Remove; avoids re-processing)
        if (!_byId.TryGetValue(from, out var holder) || ReferenceEquals(holder, srcLink))
            _byId[from] = srcLink;  // claim the routable id only if free or already ours
        else
            Console.Error.WriteLine($"WARN dup-id from={from}; incumbent keeps the route (newcomer not addressable under {from})");
    }

    public async Task RouteAsync(byte[] frame, bool fromSelf, ILinkEndpoint? srcLink, CancellationToken ct)
    {
        if (!TryRoute(frame, out string from, out string to, out string payload))
        {
            Console.Error.WriteLine("DROP malformed-envelope");
            return;
        }
        // Register the client's announced id on first sight (so `to:<id>` can reach it).
        if (!fromSelf && srcLink is not null && !string.IsNullOrEmpty(from))
            Register(srcLink, from);

        if (to == _selfId)
        {
            await WriteSelfAsync(frame, ct).ConfigureAwait(false);
            if (!fromSelf && _onSelfDirected is not null)
                await _onSelfDirected(from, payload).ConfigureAwait(false);
            return;
        }

        if (to == Broadcast)
        {
            foreach (var kv in _byId)
                if (!ReferenceEquals(kv.Value, srcLink))
                    await SafeSendAsync(kv.Value, frame, ct).ConfigureAwait(false);
            if (!fromSelf) await WriteSelfAsync(frame, ct).ConfigureAwait(false); // server also receives broadcasts
            return;
        }

        if (_byId.TryGetValue(to, out var dest)) await SafeSendAsync(dest, frame, ct).ConfigureAwait(false);
        else Console.Error.WriteLine($"DROP no-route to={to}");
    }

    private static bool TryRoute(byte[] frame, out string from, out string to, out string payload)
    {
        from = ""; to = ""; payload = "";
        try
        {
            using var doc = JsonDocument.Parse(frame);
            var r = doc.RootElement;
            from = r.GetProperty("from").GetString() ?? "";
            to = r.GetProperty("to").GetString() ?? "";
            payload = r.TryGetProperty("payload", out var p) ? p.GetString() ?? "" : "";
            return true;
        }
        catch { return false; }
    }

    private async Task WriteSelfAsync(byte[] frame, CancellationToken ct)
    {
        await _stdoutLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _stdout.WriteLineAsync(Encoding.UTF8.GetString(frame)).ConfigureAwait(false);
            await _stdout.FlushAsync(ct).ConfigureAwait(false);
        }
        finally { _stdoutLock.Release(); }
    }

    private static async Task SafeSendAsync(ILinkEndpoint link, byte[] frame, CancellationToken ct)
    {
        try { await link.SendBytesAsync(frame, ct).ConfigureAwait(false); }
        catch (Exception ex) when (ex is QuicException or IOException or ObjectDisposedException)
        { Console.Error.WriteLine($"DROP send-failed {link.Id}: {ex.Message}"); }
    }
}

/// <summary>
/// The C# half of the 040 terminal ``tmsg(...)`` ground-term codec, restricted to the three shapes
/// the T011 REPL bridge speaks: parse <c>tmsg(repl_goal, "Page", "goal.")</c>, emit
/// <c>tmsg(repl_result, "Page", "Rendered")</c> and <c>tmsg(link_status, State, "Detail")</c>.
/// The escaping mirrors <c>glp_quick/terminal/protocol.py</c> exactly (backslash, quote,
/// \n/\r/\t, and the ground-relay neutralization of the <c>_w(</c> / <c>_r(</c> heads) so the two
/// codec halves cannot drift on these shapes (constitution VIII: protocol.py stays authoritative).
/// </summary>
internal static class Tmsg
{
    private static string Esc(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (var c in s)
            sb.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => c.ToString(),
            });
        // Neutralize the ground-relay placeholder heads inside string content (protocol.py _esc).
        return sb.ToString().Replace("_w(", "_w\\(").Replace("_r(", "_r\\(");
    }

    public static string ReplResult(string page, string rendered) =>
        $"tmsg(repl_result,\"{Esc(page)}\",\"{Esc(rendered)}\")";

    public static string LinkStatus(string state, string detail)
    {
        // E2 host-08: `state` sits at an unquoted atom position — constrain it to atom-safe
        // characters so no caller can ever forge term structure through it (injection-proof
        // by construction, not by call-site discipline).
        var atom = new string(state.Where(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_').ToArray());
        if (atom.Length == 0) atom = "unknown";
        return $"tmsg(link_status,{atom},\"{Esc(detail)}\")";
    }

    public static bool TryParseReplGoal(string payload, out string page, out string goal)
    {
        page = ""; goal = "";
        var s = payload.TrimStart();
        const string head = "tmsg(";
        if (!s.StartsWith(head, StringComparison.Ordinal)) return false;
        int i = head.Length;
        SkipWs(s, ref i);
        const string kind = "repl_goal";
        if (i + kind.Length > s.Length || s.AsSpan(i, kind.Length).ToString() != kind) return false;
        i += kind.Length;
        SkipWs(s, ref i);
        if (i >= s.Length || s[i] != ',') return false;
        i++;
        if (!TryQuoted(s, ref i, out page)) return false;
        SkipWs(s, ref i);
        if (i >= s.Length || s[i] != ',') return false;
        i++;
        if (!TryQuoted(s, ref i, out goal)) return false;
        SkipWs(s, ref i);
        if (i >= s.Length || s[i] != ')') return false;
        i++;
        SkipWs(s, ref i);
        return i == s.Length; // E2 host-09: trailing garbage after ')' is a malformed payload, refused
    }

    private static void SkipWs(string s, ref int i)
    {
        while (i < s.Length && (s[i] is ' ' or '\t' or '\r' or '\n')) i++;
    }

    private static bool TryQuoted(string s, ref int i, out string value)
    {
        value = "";
        SkipWs(s, ref i);
        if (i >= s.Length || s[i] != '"') return false;
        i++;
        var sb = new StringBuilder();
        while (i < s.Length)
        {
            var c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                var n = s[i + 1];
                sb.Append(n switch { 'n' => '\n', 'r' => '\r', 't' => '\t', _ => n }); // \\ \" \( \) fall through
                i += 2;
                continue;
            }
            if (c == '"') { i++; value = sb.ToString(); return true; }
            sb.Append(c);
            i++;
        }
        return false; // unterminated string
    }
}

/// <summary>
/// 063 US1 T011 (contract C1, research R2) — a supervised GLP REPL child bridged onto the link's
/// message envelopes: one directed envelope payload = one goal line to the child's stdin; one
/// rendered result BLOCK (stdout lines until a quiet window) = one envelope back to the goal's
/// sender. Child death surfaces as an explicit <c>FAULT repl_down</c> on the host's fault stream
/// plus a <c>repl_down</c> notice envelope — never a silent stall; later goals to a dead child are
/// answered with an explicit <c>repl_down</c> refusal (bounded silence). Mirrors the quiet-drain
/// discipline of the Python-side <c>ReplBridge</c> (repl_link.py): quiet 600 ms, block cap 6 s.
/// </summary>
internal sealed class ReplChild : IAsyncDisposable
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan BlockCap = TimeSpan.FromSeconds(6);

    /// <summary>One in-flight goal: requester, page, and its generation (E2 host-04 —
    /// blocks are attributed to the goal CAPTURED at emit time, never re-read fields).</summary>
    private sealed record Goal(string Requester, string Page, int Gen);

    private readonly System.Diagnostics.Process _proc;
    private readonly Func<string, string, string, Task> _reply;   // (requester, page, block)
    private readonly CancellationTokenSource _own = new();
    private readonly object _stdinLock = new();                    // E2 host-05: child stdin is not thread-safe
    private volatile Goal? _current;                               // the latest goal (atomically swapped)
    private volatile bool _sawGoal;                                // pre-goal output is banner, not a result
    private int _goalGen;                                          // generation of the latest goal
    private int _answeredGen;                                      // highest generation answered (CAS-raised only)

    public bool Alive => !_proc.HasExited;

    /// <summary>Raise <paramref name="target"/> to at least <paramref name="value"/> atomically;
    /// returns true iff THIS call performed the raise (E2 host-02: the answered transition is a
    /// single winner — the '(no output)' timeout never fires once a real block answered, and a
    /// late real block after a timeout is still delivered as an explicit correction, never lost).</summary>
    private static bool RaiseTo(ref int target, int value)
    {
        while (true)
        {
            int seen = Volatile.Read(ref target);
            if (seen >= value) return false;
            if (Interlocked.CompareExchange(ref target, value, seen) == seen) return true;
        }
    }

    private ReplChild(System.Diagnostics.Process proc, Func<string, string, string, Task> reply, Action<int> onDeath)
    {
        _proc = proc;
        _reply = reply;
        _ = PumpBlocksAsync();
        _ = PumpStderrAsync();
        _ = Task.Run(async () =>
        {
            await _proc.WaitForExitAsync().ConfigureAwait(false);
            Console.Error.WriteLine($"FAULT repl_down exit={_proc.ExitCode}");
            onDeath(_proc.ExitCode);
        });
    }

    /// <summary>Spawn the REPL (a .dll runs via the current dotnet host; anything else executes
    /// directly). A spawn failure is the same explicit fault as a death — never a silent absence.</summary>
    public static ReplChild? Spawn(string path, Func<string, string, string, Task> reply, Action<int> onDeath)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var host = Environment.ProcessPath;
            psi.FileName = host is not null && Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                ? host : "dotnet";
            psi.ArgumentList.Add(path);
        }
        else
        {
            psi.FileName = path;
        }
        try
        {
            var proc = System.Diagnostics.Process.Start(psi)
                ?? throw new InvalidOperationException("Process.Start returned null");
            return new ReplChild(proc, reply, onDeath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAULT repl_down spawn: {ex.Message}");
            onDeath(-1);
            return null;
        }
    }

    /// <summary>One goal line in (C1: one line ⇒ one message consumed). A goal whose evaluation
    /// renders nothing within the wait cap is answered <c>(no output)</c> — mirroring the Python
    /// <c>ReplBridge.evaluate</c> contract — so the requester never waits unbounded (C1).</summary>
    public void Feed(string requester, string page, string goalLine)
    {
        var gen = Interlocked.Increment(ref _goalGen);
        _current = new Goal(requester, page, gen);
        _sawGoal = true;
        try
        {
            lock (_stdinLock) // E2 host-05: concurrent mesh clients must not interleave goal bytes
            {
                _proc.StandardInput.WriteLine(goalLine);
                _proc.StandardInput.Flush();
            }
        }
        catch (Exception ex)
        {
            // E2 host-01: a failed write still ANSWERS the requester — never a silent stall (C1).
            Console.Error.WriteLine($"FAULT repl_down write: {ex.Message}");
            if (RaiseTo(ref _answeredGen, gen))
                _ = _reply(requester, page, "[repl error: repl_down]");
            return;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(BlockCap + Quiet + Quiet, _own.Token).ConfigureAwait(false);
                if (Volatile.Read(ref _goalGen) == gen && RaiseTo(ref _answeredGen, gen))
                    await _reply(requester, page, "(no output)").ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* disposed */ }
        });
    }

    /// <summary>Assemble stdout lines into result blocks by quiet-window drain; each block replies
    /// as ONE envelope to the current requester (pre-goal banner lines log to stderr only).</summary>
    private async Task PumpBlocksAsync()
    {
        var reader = _proc.StandardOutput;
        var lines = System.Threading.Channels.Channel.CreateUnbounded<string>();
        _ = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await reader.ReadLineAsync(_own.Token).ConfigureAwait(false)) is not null)
                    lines.Writer.TryWrite(line);
            }
            catch (OperationCanceledException) { /* disposed */ }
            finally { lines.Writer.Complete(); }
        });

        var block = new List<string>();
        try
        {
            await PumpBlocksCoreAsync(lines.Reader, block).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* disposed */ }
    }

    private async Task PumpBlocksCoreAsync(System.Threading.Channels.ChannelReader<string> lines, List<string> block)
    {
        while (await lines.WaitToReadAsync(_own.Token).ConfigureAwait(false))
        {
            var blockStart = DateTime.UtcNow;
            while (lines.TryRead(out var l)) block.Add(l);
            // Extend the block until the child goes quiet (or the cap forces the flush).
            while (DateTime.UtcNow - blockStart < BlockCap)
            {
                using var quiet = CancellationTokenSource.CreateLinkedTokenSource(_own.Token);
                quiet.CancelAfter(Quiet);
                try
                {
                    if (!await lines.WaitToReadAsync(quiet.Token).ConfigureAwait(false)) break;
                }
                catch (OperationCanceledException) when (!_own.IsCancellationRequested) { break; }
                while (lines.TryRead(out var l)) block.Add(l);
            }
            var current = _current;
            var text = string.Join("\n", block).Trim();
            block.Clear();
            if (text.Length == 0) continue;
            if (!_sawGoal || current is null)
            {
                Console.Error.WriteLine($"REPL_BANNER {text.Replace('\n', ' ')}");
                continue;
            }
            // E2 host-04: attribute the block to the goal captured HERE (requester+page+gen travel
            // together); the answered mark raises only to THAT goal's generation, so a stale block
            // can never suppress a newer goal's timeout reply. Real blocks are always delivered.
            RaiseTo(ref _answeredGen, current.Gen);
            await _reply(current.Requester, current.Page, text).ConfigureAwait(false);
        }
    }

    private async Task PumpStderrAsync()
    {
        try
        {
            string? line;
            while ((line = await _proc.StandardError.ReadLineAsync(_own.Token).ConfigureAwait(false)) is not null)
                Console.Error.WriteLine($"REPL_ERR {line}");
        }
        catch (OperationCanceledException) { /* disposed */ }
    }

    public async ValueTask DisposeAsync()
    {
        _own.Cancel();
        try
        {
            if (!_proc.HasExited)
            {
                try { _proc.StandardInput.Close(); } catch (IOException) { }
                if (!_proc.WaitForExit(2000)) _proc.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException) { /* already gone */ }
        _proc.Dispose();
        await Task.CompletedTask;
    }
}

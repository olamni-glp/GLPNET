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
            return opts.Role switch
            {
                // `--role server --binary` is a POINT-TO-POINT opaque listener, not the mesh
                // router: it accepts ONE link and runs the same stdio relay the client does, so
                // both ends of an 025 link can carry opaque frames (glpnet 050 T055).
                "server" when opts.Binary => await RunBinaryServerAsync(transport, addr, stdout, life),
                "server" => await RunMeshServerAsync(transport, addr, opts, stdout, life),
                _ => await RunClientAsync(transport, addr, opts, stdout, life),
            };
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

        // The client lives for the LINK's lifetime, NOT stdin's: stdin EOF (a one-shot / piped /
        // non-interactive shell) must NOT tear the link down — it only stops accepting new sends.
        // We exit when the peer closes the link (recv returns) or the process is killed.
        _ = StdinToLinkAsync(endpoint, life.Token, opts.Binary);
        await LinkToStdoutAsync(endpoint, stdout, life, opts.Binary).ConfigureAwait(false);
        life.Cancel();
        await endpoint.DisposeAsync().ConfigureAwait(false);
        return ExitOk;
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

    /// <summary>
    /// stdin → link. In the default (L5) mode a line IS the payload, UTF-8 encoded. In
    /// <c>--binary</c> mode (glpnet feature 050 T055) the line is BASE64 of an opaque binary frame:
    /// it is decoded here, so what reaches the wire is the RAW frame — byte-identical to what
    /// <c>glp_link</c>'s own <c>QuicTransport</c> sends, preserving cross-runtime parity (US5).
    /// The base64 exists ONLY on the stdio IPC leg, which is line-delimited UTF-8 and therefore
    /// cannot carry arbitrary binary (CRC bytes, length prefixes, embedded newlines) intact.
    /// A malformed base64 line is reported and skipped — never a crash (FR-019).
    /// </summary>
    private static async Task StdinToLinkAsync(ILinkEndpoint endpoint, CancellationToken ct, bool binary)
    {
        using var stdin = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        string? line;
        while (!ct.IsCancellationRequested && (line = await stdin.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            if (line.Length == 0) continue;
            byte[] payload;
            if (binary)
            {
                try { payload = Convert.FromBase64String(line); }
                catch (FormatException)
                {
                    Console.Error.WriteLine("ERR bad_base64 stdin line is not valid base64 (--binary mode); skipped");
                    continue;
                }
            }
            else payload = Encoding.UTF8.GetBytes(line);
            await endpoint.SendBytesAsync(payload, ct).ConfigureAwait(false);
        }
    }

    /// <summary>link → stdout; the exact inverse of <see cref="StdinToLinkAsync"/> per mode.</summary>
    private static async Task LinkToStdoutAsync(ILinkEndpoint endpoint, TextWriter stdout, CancellationTokenSource life, bool binary)
    {
        while (!life.IsCancellationRequested)
        {
            byte[]? frame = await endpoint.RecvBytesAsync(life.Token).ConfigureAwait(false);
            if (frame is null) { Console.Error.WriteLine("LINK_CLOSED"); life.Cancel(); return; }
            await stdout.WriteLineAsync(binary ? Convert.ToBase64String(frame) : Encoding.UTF8.GetString(frame)).ConfigureAwait(false);
            await stdout.FlushAsync().ConfigureAwait(false);
        }
    }

    // ------------------------------------------------- server role: opaque point-to-point (050 T055)
    /// <summary>
    /// `--role server --binary`: bind, accept ONE link, and relay opaque frames over stdio exactly
    /// as the client role does (base64 on the stdio leg, RAW bytes on the wire). This is the
    /// LISTENING half of an 025 link — the mesh router is deliberately not involved, because that
    /// router parses L5 JSON envelopes and an 025 frame is opaque binary by contract.
    /// One link per process mirrors the 025 base MVP (one-link-per-listen, as in the TCP leaf).
    /// </summary>
    private static async Task<int> RunBinaryServerAsync(QuicTransport transport, LinkAddress addr, TextWriter stdout, CancellationTokenSource life)
    {
        await using var listener = await transport.CreateListenerAsync(addr, LinkOptions.Default, life.Token).ConfigureAwait(false);
        Console.Error.WriteLine($"READY server {addr}");

        ILinkEndpoint link;
        try { link = await listener.AcceptAsync(life.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { return ExitOk; }

        Console.Error.WriteLine($"LINK_UP {link.Id}");
        link.OnFault += f => Console.Error.WriteLine($"FAULT {f.Kind} {f.Reason}");

        _ = StdinToLinkAsync(link, life.Token, binary: true);
        await LinkToStdoutAsync(link, stdout, life, binary: true).ConfigureAwait(false);
        life.Cancel();
        await link.DisposeAsync().ConfigureAwait(false);
        return ExitOk;
    }

    // ---------------------------------------------------------------- server role: mesh router (US2)
    private static async Task<int> RunMeshServerAsync(QuicTransport transport, LinkAddress addr, Opts opts, TextWriter stdout, CancellationTokenSource life)
    {
        await using var listener = await transport.CreateListenerAsync(addr, LinkOptions.Default, life.Token).ConfigureAwait(false);
        Console.Error.WriteLine($"READY server {addr}"); // listener bound — clients may connect
        var mesh = new Mesh(opts.SelfId, stdout);

        // The server's own stdio endpoint participates as `SelfId` (preserves US1: client→server).
        var selfPump = SelfStdioPumpAsync(mesh, life.Token);

        int active = 0;
        while (!life.IsCancellationRequested)
        {
            ILinkEndpoint link;
            try { link = await listener.AcceptAsync(life.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (QuicException ex) { Console.Error.WriteLine($"ACCEPT_FAULT {ex.QuicError}: {ex.Message}"); continue; }

            if (Interlocked.Increment(ref active) > opts.MaxClients)
            {
                Interlocked.Decrement(ref active);
                Console.Error.WriteLine($"REJECT over_capacity {link.Id}"); // T026: clear over-capacity reject
                _ = RejectOverCapacityAsync(link);
                continue;
            }
            Console.Error.WriteLine($"CLIENT_UP {link.Id} ({active}/{opts.MaxClients})");
            _ = ClientPumpAsync(mesh, link, () => Interlocked.Decrement(ref active), life.Token);
        }
        await selfPump.ConfigureAwait(false);
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

    /// <summary>One isolated client link: register it, route its envelopes, and clean up on drop (FR-006/SC-004).</summary>
    private static async Task ClientPumpAsync(Mesh mesh, ILinkEndpoint link, Action onGone, CancellationToken ct)
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

    private static async Task RejectOverCapacityAsync(ILinkEndpoint link)
    {
        try
        {
            var env = "{\"msg_id\":\"capacity\",\"from\":\"server\",\"to\":\"_overflow\",\"seq\":null,\"payload\":\"over_capacity\"}";
            await link.SendBytesAsync(Encoding.UTF8.GetBytes(env)).ConfigureAwait(false);
        }
        catch { /* best-effort notice */ }
        await link.DisposeAsync().ConfigureAwait(false);
    }

    private sealed record Opts(string Role, string Addr, int Port, string CertDir, int MaxClients, bool Retry, string SelfId, bool Binary)
    {
        public static Opts Parse(string[] args)
        {
            string? role = null, addr = null, cert = null, selfId = "server";
            int port = 0, maxClients = 3;
            bool retry = false, binary = false;
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
                    case "--retry": retry = true; break;
                    // glpnet 050 T055: opaque-binary stdio mode — stdin/stdout lines are BASE64 of
                    // the frame; the wire still carries the RAW bytes (see StdinToLinkAsync).
                    case "--binary": binary = true; break;
                    default: throw new ArgumentException($"unknown arg '{args[i]}'");
                }
            }
            if (role is not ("server" or "client")) throw new ArgumentException("--role must be server|client");
            if (string.IsNullOrWhiteSpace(addr)) throw new ArgumentException("--addr required");
            if (port is < 1 or > 65535) throw new ArgumentException("--port in [1,65535] required");
            if (string.IsNullOrWhiteSpace(cert)) throw new ArgumentException("--cert <dir> required");
            // --binary is valid for BOTH roles: client dials, server binds+accepts-one — each an
            // opaque point-to-point end of one 025 link. It only excludes the MESH router, which
            // needs L5 envelopes it can route by `to`/`from`.
            return new Opts(role, addr, port, cert, maxClients, retry, selfId!, binary);
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

    public Mesh(string selfId, TextWriter stdout)
    {
        _selfId = selfId;
        _stdout = stdout;
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
        if (!TryRoute(frame, out string from, out string to))
        {
            Console.Error.WriteLine("DROP malformed-envelope");
            return;
        }
        // Register the client's announced id on first sight (so `to:<id>` can reach it).
        if (!fromSelf && srcLink is not null && !string.IsNullOrEmpty(from))
            Register(srcLink, from);

        if (to == _selfId) { await WriteSelfAsync(frame, ct).ConfigureAwait(false); return; }

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

    private static bool TryRoute(byte[] frame, out string from, out string to)
    {
        from = ""; to = "";
        try
        {
            using var doc = JsonDocument.Parse(frame);
            var r = doc.RootElement;
            from = r.GetProperty("from").GetString() ?? "";
            to = r.GetProperty("to").GetString() ?? "";
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

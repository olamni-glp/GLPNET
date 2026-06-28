using System.Security.Authentication;
using System.Net.Quic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;

namespace GlpQuick.Host;

/// <summary>
/// The C# QUIC+WS endpoint the glp_quick control plane launches (FR-007). One process = one role
/// (<c>--role server|client</c>) running one genuine real-QUIC + RFC 6455 WebSocket link
/// (<see cref="QuicTransport"/>), bridging newline-delimited L5 GLP-message envelopes between
/// stdio and the link. stdout carries data frames; stderr carries control + the FR-019 failure
/// tokens. Real QUIC only — gated on <see cref="QuicTransport.IsSupported"/> (FR-001).
/// </summary>
internal static class Program
{
    // FR-019 failure tokens (wire-contract.md §Failure contract) → distinct non-zero exit codes.
    private const int ExitOk = 0;
    private const int ExitUsage = 2;
    private const int ExitCertMismatch = 3;    // cert_mismatch
    private const int ExitServerNotReady = 4;  // server_not_ready
    private const int ExitUdpBlocked = 5;      // udp_blocked
    private const int ExitQuicUnsupported = 6; // alpn_version_mismatch / unsupported stack
    private const int ExitBindFailed = 7;

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false); // no BOM on stdout
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
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERR cert_load {ex.Message}");
            return ExitBindFailed;
        }

        var transport = new QuicTransport(cert, pin);
        var addr = LinkAddress.Endpoint(opts.Addr, opts.Port);
        using var life = new CancellationTokenSource();

        // Readiness signal so the control plane can start the server before the client. For the
        // server this precedes the UDP bind by microseconds; QUIC handshake retransmission absorbs
        // the tiny race if a client Initial arrives first.
        Console.Error.WriteLine($"READY {opts.Role} {opts.Addr}:{opts.Port}");

        ILinkEndpoint endpoint;
        try
        {
            endpoint = opts.Role == "server"
                ? await transport.ListenAsync(LinkScheme.Quic, addr, LinkOptions.Default, life.Token)
                : await ConnectWithReadinessAsync(transport, addr, opts.Retry, life.Token);
        }
        catch (AuthenticationException ex)
        {
            Console.Error.WriteLine($"ERR cert_mismatch {ex.Message}"); // SPKI pin rejected — no half-open link
            return ExitCertMismatch;
        }
        catch (QuicException ex) when (ex.QuicError == QuicError.ConnectionTimeout || ex.QuicError == QuicError.ConnectionAborted)
        {
            Console.Error.WriteLine($"ERR server_not_ready {ex.QuicError}: {ex.Message}");
            return ExitServerNotReady;
        }
        catch (QuicException ex)
        {
            Console.Error.WriteLine($"ERR udp_blocked {ex.QuicError}: {ex.Message}"); // unreachable/datagrams dropped
            return ExitUdpBlocked;
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            Console.Error.WriteLine($"ERR bind_failed {ex.SocketErrorCode}: {ex.Message}");
            return ExitBindFailed;
        }

        Console.Error.WriteLine($"LINK_UP {endpoint.Id}");
        endpoint.OnFault += f => Console.Error.WriteLine($"FAULT {f.Kind} {f.Reason}");

        // Full-duplex bridge (FR-008a): stdin→link and link→stdout run concurrently.
        var sendLoop = SendLoopAsync(endpoint, life.Token);
        var recvLoop = RecvLoopAsync(endpoint, stdout, life);
        await Task.WhenAny(sendLoop, recvLoop).ConfigureAwait(false);
        life.Cancel();
        await endpoint.DisposeAsync().ConfigureAwait(false);
        return ExitOk;
    }

    /// <summary>Client connect with server-not-ready readiness retry (FR-019); cert mismatch is NOT retried.</summary>
    private static async Task<ILinkEndpoint> ConnectWithReadinessAsync(QuicTransport transport, LinkAddress addr, bool retry, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + (retry ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(8));
        while (true)
        {
            try
            {
                return await transport.ConnectAsync(LinkScheme.Quic, addr, LinkOptions.Default, ct).ConfigureAwait(false);
            }
            catch (QuicException ex) when (retry
                && (ex.QuicError == QuicError.ConnectionTimeout || ex.QuicError == QuicError.ConnectionRefused)
                && DateTime.UtcNow < deadline)
            {
                Console.Error.WriteLine($"WAIT server_not_ready {ex.QuicError} — retrying");
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Read newline-delimited L5 envelopes from stdin; ship each as one link frame.</summary>
    private static async Task SendLoopAsync(ILinkEndpoint endpoint, CancellationToken ct)
    {
        using var stdin = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        string? line;
        while (!ct.IsCancellationRequested && (line = await stdin.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            if (line.Length == 0) continue;
            await endpoint.SendBytesAsync(Encoding.UTF8.GetBytes(line), ct).ConfigureAwait(false);
        }
    }

    /// <summary>Emit each received link frame as one newline-delimited L5 envelope on stdout.</summary>
    private static async Task RecvLoopAsync(ILinkEndpoint endpoint, TextWriter stdout, CancellationTokenSource life)
    {
        while (!life.IsCancellationRequested)
        {
            byte[]? frame = await endpoint.RecvBytesAsync(life.Token).ConfigureAwait(false);
            if (frame is null) // graceful close / eos
            {
                Console.Error.WriteLine("LINK_CLOSED");
                life.Cancel();
                return;
            }
            await stdout.WriteLineAsync(Encoding.UTF8.GetString(frame)).ConfigureAwait(false);
            await stdout.FlushAsync().ConfigureAwait(false);
        }
    }

    private sealed record Opts(string Role, string Addr, int Port, string CertDir, int MaxClients, bool Retry)
    {
        public static Opts Parse(string[] args)
        {
            string? role = null, addr = null, cert = null;
            int port = 0, maxClients = 3;
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
                    case "--retry": retry = true; break;
                    default: throw new ArgumentException($"unknown arg '{args[i]}'");
                }
            }
            if (role is not ("server" or "client")) throw new ArgumentException("--role must be server|client");
            if (string.IsNullOrWhiteSpace(addr)) throw new ArgumentException("--addr required");
            if (port is < 1 or > 65535) throw new ArgumentException("--port in [1,65535] required");
            if (string.IsNullOrWhiteSpace(cert)) throw new ArgumentException("--cert <dir> required");
            return new Opts(role, addr, port, cert, maxClients, retry);
        }

        private static string Req(string[] args, int i) =>
            i < args.Length ? args[i] : throw new ArgumentException("missing value for last flag");
    }
}

using System.Net;
using System.Runtime.InteropServices;

namespace Ynet.Transport.Link;

/// <summary>
/// Tier 2 — the ULTIMATE QUIC fallback on Linux: <b>ngtcp2</b> with the <b>OpenSSL</b> crypto backend.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why ngtcp2 is the equivalent chosen.</b> The gap it fills is not "no QUIC exists for Linux" —
/// libmsquic builds and runs on Linux. The gap is that libmsquic is distributed only from
/// <c>packages.microsoft.com</c>: it is absent from Ubuntu apt and absent from the .NET install, so
/// every new Linux host reintroduces a manual, elevated provisioning step. ngtcp2 is in the Ubuntu
/// archive itself (<c>libngtcp2-16</c>, <c>libngtcp2-crypto-ossl0</c>, universe), so it is a prebuilt
/// native library from the distribution, needs no third-party feed, and needs no Rust toolchain.
/// </para>
/// <para>
/// That last point is why it sits <i>beneath</i> iroh rather than beside it. Ruling
/// <c>Q-glpnetshiras-38</c> records the hazard directly: if iroh is vendored as Rust rather than
/// consumed as a prebuilt native library it creates a new per-host, per-platform system prerequisite
/// — <b>the same class as the libmsquic gap it was meant to remove</b>. A fallback that shares the
/// primary's failure mode is not a fallback. ngtcp2 from apt shares neither iroh's toolchain
/// dependency nor msquic's feed dependency, so all three tiers fail independently.
/// </para>
/// <para>
/// Rejected alternatives, and why: <b>quiche</b> (Cloudflare) has the cleanest C ABI of the three but
/// ships no distro package and requires cargo to build — iroh's problem again. <b>lsquic</b>
/// (LiteSpeed) is mature but likewise unpackaged, and its engine API is larger than ngtcp2's.
/// <b>picoquic</b> is research-grade. <b>Vendoring libmsquic.so.2 beside the binary</b> is worth doing
/// and is done — see <see cref="MsQuicNativeResolver"/> — but it keeps the Microsoft feed in the
/// provisioning path, so it strengthens tier 1 rather than replacing it.
/// </para>
/// <para>
/// <b>Measured on shiras 2026-09-04</b> (Ubuntu 26.04.1, OpenSSL 3.5.5): all three libraries fetch
/// with <c>apt-get download</c> <i>without root</i>, load under <c>dlopen</c>, and export the required
/// symbols; <c>ngtcp2_version</c> reports 1.16.0. <c>ngtcp2_crypto_ossl</c> needs OpenSSL's own QUIC
/// TLS API, which 3.5.x provides.
/// </para>
/// <para>
/// <b>Current honest state — read this before relying on the tier.</b> The native engine is
/// provisionable and probed; the managed interop that drives it is NOT yet implemented. ngtcp2 is a
/// protocol engine only — it owns neither sockets nor TLS, so the binding is a substantial piece of
/// work (callback table, versioned settings/transport-params structs, the OpenSSL QUIC handshake, a
/// UDP pump, timers and path validation) rather than a thin P/Invoke. Accordingly
/// <see cref="Probe"/> reports <b>unavailable</b> while that is true, and says which of the two
/// reasons applies. It deliberately does not report the tier healthy on the strength of the native
/// library being present: a provider that probes green and then refuses at bind is precisely the
/// "green check, deaf service" failure this chain exists to prevent.
/// </para>
/// </remarks>
public sealed class Ngtcp2Provider : IQuicProvider
{
    public static readonly Ngtcp2Provider Instance = new();

    /// <summary>The distribution packages that provision this tier (Debian/Ubuntu names).</summary>
    public static readonly IReadOnlyList<string> AptPackages =
        new[] { "libngtcp2-16", "libngtcp2-crypto-ossl0", "libnghttp3-9" };

    private static readonly string[] EngineSonames = { "libngtcp2.so.16", "libngtcp2.so" };
    private static readonly string[] CryptoSonames = { "libngtcp2_crypto_ossl.so.0", "libngtcp2_crypto_ossl.so" };

    // Versioned entry points: ngtcp2 suffixes its ABI-versioned functions, so their presence is a
    // sharper ABI check than the soname alone.
    private static readonly string[] EngineExports =
    {
        "ngtcp2_version",
        "ngtcp2_conn_client_new_versioned",
        "ngtcp2_conn_server_new_versioned",
        "ngtcp2_conn_read_pkt_versioned",
        "ngtcp2_conn_writev_stream_versioned",
    };

    private static readonly string[] CryptoExports =
    {
        "ngtcp2_crypto_ossl_init",
        "ngtcp2_crypto_ossl_ctx_new",
        "ngtcp2_crypto_ossl_configure_client_session",
    };

    public string Name => "ngtcp2";
    public QuicProviderTier Tier => QuicProviderTier.Ngtcp2;

    /// <summary>The measured state of the native engine, independent of the managed interop.</summary>
    /// <param name="Present">Both libraries loaded and exported every required symbol.</param>
    /// <param name="Version">ngtcp2's self-reported version string, when it could be read.</param>
    /// <param name="Detail">Where each library was found, or exactly what failed.</param>
    public readonly record struct NativeEngineState(bool Present, string? Version, string Detail);

    /// <summary>
    /// Probe the native engine only. Provisioning checks use this; link admission must not — a present
    /// engine is a necessary and not a sufficient condition for carrying a link. See <see cref="Probe"/>.
    /// </summary>
    private static readonly object NativeGate = new();
    private static NativeEngineState? _nativeOk;   // memoised only on success; a loaded .so stays loaded

    public static NativeEngineState ProbeNative()
    {
        // Success is cached, failure is not: provisioning can land between two probes (a package
        // installed, a library staged beside the binary) and a cached "missing" would outlive the fix.
        lock (NativeGate)
        {
            if (_nativeOk is { } cached) return cached;
            var state = ProbeNativeUncached();
            if (state.Present) _nativeOk = state;
            return state;
        }
    }

    private static NativeEngineState ProbeNativeUncached()
    {
        if (!OperatingSystem.IsLinux())
            return new NativeEngineState(false, null,
                $"ngtcp2 is provisioned as the Linux fallback; this host is {RuntimeInformation.OSDescription}");

        // ORDER IS LOAD-BEARING. libngtcp2_crypto_ossl.so.0 carries DT_NEEDED libngtcp2.so.16, and the
        // directory we load from is not on the loader search path. Loading the engine FIRST puts it in
        // the process link map, which then satisfies the crypto library's dependency by soname.
        // Probing them in the other order — or in separate processes — reports a false "crypto
        // missing" (verified on shiras 2026-09-04). The engine handle is deliberately not freed.
        if (!QuicNativeLoader.TryLoad(EngineSonames, EngineExports, "YNET_NGTCP2_PATH",
                out var engine, out var engineDetail))
            return new NativeEngineState(false, null,
                $"libngtcp2 {engineDetail}; install: sudo apt install {string.Join(" ", AptPackages)}");

        if (!QuicNativeLoader.TryLoad(CryptoSonames, CryptoExports, "YNET_NGTCP2_CRYPTO_PATH",
                out _, out var cryptoDetail))
            return new NativeEngineState(false, ReadVersion(engine.Handle),
                $"libngtcp2 present ({engine.Path}) but the crypto backend is missing: {cryptoDetail}; "
              + "install: sudo apt install libngtcp2-crypto-ossl0 (needs OpenSSL >= 3.5 for its QUIC TLS API)");

        return new NativeEngineState(true, ReadVersion(engine.Handle),
            $"libngtcp2 {engineDetail}; crypto backend {cryptoDetail}");
    }

    /// <summary>
    /// Whether this provider can carry a link here. Reports unavailable while the managed interop is
    /// unbuilt, even when the native engine is present, and names which of the two is the reason.
    /// </summary>
    public QuicAvailability Probe()
    {
        var native = ProbeNative();

        if (!native.Present)
            return QuicAvailability.No($"native engine not provisioned — {native.Detail}");

        return QuicAvailability.No(
            $"native engine PRESENT (ngtcp2 {native.Version}; {native.Detail}) but the managed ngtcp2 "
          + "interop is not implemented yet, so this tier cannot carry a link. Provisioning is done; "
          + "the binding is the outstanding work.");
    }

    public Task<IQuicListenerHandle> BindListenerAsync(IPEndPoint local, CancellationToken ct = default)
        => throw Unavailable("listen");

    public Task<IWireChannel> ConnectAsync(IPEndPoint remote, CancellationToken ct = default)
        => throw Unavailable("connect");

    private QuicUnavailableException Unavailable(string op)
        => new(op, new[] { (Name, Tier, Probe()) });

    private static string? ReadVersion(IntPtr engineHandle)
    {
        try
        {
            if (!NativeLibrary.TryGetExport(engineHandle, "ngtcp2_version", out var fn)) return null;
            var version = Marshal.GetDelegateForFunctionPointer<Ngtcp2VersionDelegate>(fn)(0);
            if (version == IntPtr.Zero) return null;
            // struct ngtcp2_info { int age; int version_num; const char *version_str; }
            var info = Marshal.PtrToStructure<Ngtcp2Info>(version);
            return info.VersionStr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(info.VersionStr);
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or MarshalDirectiveException or AccessViolationException)
        {
            return null;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr Ngtcp2VersionDelegate(int leastVersion);

    [StructLayout(LayoutKind.Sequential)]
    private struct Ngtcp2Info
    {
        public int Age;
        public int VersionNum;
        public IntPtr VersionStr;
    }
}

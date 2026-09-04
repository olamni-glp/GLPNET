using System.Net;
using System.Net.Quic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// System.Net.Quic is [SupportedOSPlatform] windows/linux/macOS; every entry gates on IsSupported at
// runtime (FR-001), the sanctioned guard — so the CA1416 advisory does not apply here.
#pragma warning disable CA1416

namespace Ynet.Transport.Link;

/// <summary>
/// Tier 1: QUIC via <c>System.Net.Quic</c>, i.e. MsQuic. Bundled inside the .NET runtime on Windows;
/// on Linux it is a separate <c>libmsquic.so.2</c> that ships in neither Ubuntu apt nor the .NET
/// install, which is why <see cref="Ngtcp2Provider"/> exists beneath it.
/// </summary>
public sealed class MsQuicProvider : IQuicProvider
{
    public static readonly MsQuicProvider Instance = new();

    public string Name => "msquic";
    public QuicProviderTier Tier => QuicProviderTier.MsQuic;

    public QuicAvailability Probe()
    {
        MsQuicNativeResolver.EnsureRegistered();

        if (QuicListener.IsSupported && QuicConnection.IsSupported)
            return QuicAvailability.Yes(
                $"System.Net.Quic supported ({MsQuicNativeResolver.ResolutionDetail})");

        return QuicAvailability.No(
            $"QuicListener.IsSupported={QuicListener.IsSupported}, "
          + $"QuicConnection.IsSupported={QuicConnection.IsSupported}; "
          + $"native msquic {MsQuicNativeResolver.ResolutionDetail}. "
          + "On Linux install libmsquic (packages.microsoft.com) or place libmsquic.so.2 beside the "
          + "service binary; do NOT rely on LD_LIBRARY_PATH — a systemd unit does not inherit it.");
    }

    public async Task<IQuicListenerHandle> BindListenerAsync(IPEndPoint local, CancellationToken ct = default)
    {
        Require("listen");
        var listener = await QuicWireChannel.BindListenerAsync(local, ct).ConfigureAwait(false);
        return new MsQuicListenerHandle(listener, Name);
    }

    public async Task<IWireChannel> ConnectAsync(IPEndPoint remote, CancellationToken ct = default)
    {
        Require("connect");
        return await QuicWireChannel.ConnectAsync(remote, ct).ConfigureAwait(false);
    }

    private void Require(string op)
    {
        var a = Probe();
        if (!a.Supported)
            throw new QuicUnavailableException(op, new[] { (Name, Tier, a) });
    }

    private sealed class MsQuicListenerHandle(QuicListener listener, string providerName) : IQuicListenerHandle
    {
        public IPEndPoint LocalEndPoint => listener.LocalEndPoint;
        public string ProviderName => providerName;

        public async Task<IWireChannel> AcceptAsync(CancellationToken ct = default)
            => await QuicWireChannel.AcceptAsync(listener, ct).ConfigureAwait(false);

        public ValueTask DisposeAsync() => listener.DisposeAsync();
    }
}

/// <summary>
/// Resolves the native msquic library from paths that travel with the build output, so a service
/// finds it without any environment variable.
/// </summary>
/// <remarks>
/// <para>
/// This closes the measured loader-path gap on shiras (2026-09-04): the same binary reported
/// <c>IsSupported=False</c> on the default loader path and <c>True</c> only under
/// <c>LD_LIBRARY_PATH=$HOME/.local/lib</c>. Since a systemd unit inherits no interactive
/// <c>LD_LIBRARY_PATH</c>, that env var produces a green test and a deaf service — and a deaf oracle
/// takes the PBFT margin from f=1 to f=0 with no signal.
/// </para>
/// <para>
/// <b>Ordering is load-bearing.</b> <c>QuicConnection.IsSupported</c> runs MsQuic's static
/// initialiser, and a resolver registered after that point has no effect. The
/// <see cref="ModuleInitializerAttribute"/> below therefore registers at assembly load, before any
/// caller can touch a QUIC type.
/// </para>
/// </remarks>
internal static class MsQuicNativeResolver
{
    private static readonly object Gate = new();
    private static bool _registered;
    private static IntPtr _handle;

    /// <summary>Human-readable outcome of the last resolution attempt — for honest probe detail.</summary>
    internal static string ResolutionDetail { get; private set; } = "not yet resolved";

    // CA2255 warns that ModuleInitializer belongs in application code. Here it is the point:
    // QuicConnection.IsSupported runs MsQuic's static initialiser, and a DllImportResolver
    // registered after that has no effect. Assembly-load is the only moment early enough, and a
    // caller cannot be relied on to reach a registration call before touching a QUIC type.
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Initialize() => EnsureRegistered();
#pragma warning restore CA2255

    internal static void EnsureRegistered()
    {
        lock (Gate)
        {
            if (_registered) return;
            _registered = true;

            // Windows resolves msquic from inside the runtime; nothing to redirect.
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                ResolutionDetail = "resolved by the .NET runtime (bundled on this platform)";
                return;
            }

            var sonames = OperatingSystem.IsMacOS()
                ? new[] { "libmsquic.dylib", "libmsquic.2.dylib" }
                : new[] { "libmsquic.so.2", "libmsquic.so" };

            // MsQuicOpenVersion is the single entry point System.Net.Quic needs; a libmsquic that
            // loads without it is an ABI mismatch, which otherwise surfaces as a runtime crash.
            if (!QuicNativeLoader.TryLoad(sonames, new[] { "MsQuicOpenVersion" },
                    overrideEnvVar: "YNET_MSQUIC_PATH", out var found, out var detail))
            {
                ResolutionDetail = detail;
                return;
            }

            _handle = found.Handle;
            ResolutionDetail = detail;

            try
            {
                NativeLibrary.SetDllImportResolver(typeof(QuicConnection).Assembly, Resolve);
            }
            catch (InvalidOperationException)
            {
                // A resolver is already installed for System.Net.Quic (another component got there
                // first). Ours is redundant, not wrong — the library is loaded either way.
                ResolutionDetail += " (a DllImportResolver was already registered by another component)";
            }
        }
    }

    private static IntPtr Resolve(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
        => libraryName.Contains("msquic", StringComparison.OrdinalIgnoreCase) ? _handle : IntPtr.Zero;
}

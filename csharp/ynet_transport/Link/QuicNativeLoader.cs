using System.Runtime.InteropServices;

namespace Ynet.Transport.Link;

/// <summary>
/// Finds a QUIC native library the way a <b>service</b> will find it, not the way an interactive shell
/// will.
/// </summary>
/// <remarks>
/// <para>
/// Measured on shiras 2026-09-04, twice, same binary: <c>QuicListener.IsSupported</c> was
/// <c>False</c> on the default loader path and <c>True</c> only under
/// <c>LD_LIBRARY_PATH=$HOME/.local/lib</c>. A systemd unit does not inherit an interactive shell's
/// <c>LD_LIBRARY_PATH</c>, so that env var greens the tests and leaves every service deaf — the
/// broker, guardian and oracle would register and then refuse at first link.
/// </para>
/// <para>
/// This loader therefore searches, in order: an explicit override, the application directory and its
/// RID-native subdirectory (which travel with the build output and need no environment at all), the
/// per-user lib dir, then the system loader. The <b>application-directory</b> hit is the one that
/// makes a service work; the rest are conveniences.
/// </para>
/// </remarks>
internal static class QuicNativeLoader
{
    /// <summary>Where a candidate was found, for the honest detail string in a probe result.</summary>
    internal readonly record struct Found(IntPtr Handle, string Path, string Origin);

    /// <summary>
    /// Try each <paramref name="sonames"/> across every search origin and return the first that loads
    /// AND exports every name in <paramref name="requiredExports"/>. A library that loads but is
    /// missing an export is NOT a hit — that is an ABI mismatch, which otherwise surfaces later as a
    /// runtime crash rather than an install error.
    /// </summary>
    internal static bool TryLoad(
        IReadOnlyList<string> sonames,
        IReadOnlyList<string> requiredExports,
        string? overrideEnvVar,
        out Found found,
        out string detail)
    {
        var attempts = new List<string>();

        foreach (var (dir, origin) in SearchOrigins(overrideEnvVar))
        {
            foreach (var soname in sonames)
            {
                var candidate = dir is null ? soname : System.IO.Path.Combine(dir, soname);

                // dir==null means "ask the system loader by soname" (ldconfig cache, DT_RUNPATH, ld.so.conf).
                if (dir is not null && !File.Exists(candidate)) continue;

                if (!NativeLibrary.TryLoad(candidate, out var handle))
                {
                    attempts.Add($"{origin}:{candidate} (load failed)");
                    continue;
                }

                var missing = requiredExports
                    .Where(sym => !NativeLibrary.TryGetExport(handle, sym, out _))
                    .ToList();

                if (missing.Count > 0)
                {
                    NativeLibrary.Free(handle);
                    attempts.Add($"{origin}:{candidate} (loaded, missing exports: {string.Join(", ", missing)})");
                    continue;
                }

                found = new Found(handle, candidate, origin);
                detail = $"{candidate} via {origin}";
                return true;
            }
        }

        found = default;
        detail = attempts.Count == 0
            ? $"not found; searched sonames [{string.Join(", ", sonames)}] and no candidate path existed"
            : "not found; tried " + string.Join("; ", attempts);
        return false;
    }

    /// <summary>
    /// Search origins in preference order. The first two travel with the build output — a service
    /// started by systemd resolves them with no environment at all.
    /// </summary>
    private static IEnumerable<(string? Dir, string Origin)> SearchOrigins(string? overrideEnvVar)
    {
        if (overrideEnvVar is not null)
        {
            var explicitPath = Environment.GetEnvironmentVariable(overrideEnvVar);
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                // Accept either a directory or a full path to the .so itself.
                if (Directory.Exists(explicitPath)) yield return (explicitPath, overrideEnvVar);
                else yield return (System.IO.Path.GetDirectoryName(explicitPath), overrideEnvVar);
            }
        }

        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            yield return (System.IO.Path.Combine(baseDir, "runtimes", RuntimeIdentifier(), "native"), "app-rid-native");
            yield return (baseDir, "app-dir");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            yield return (System.IO.Path.Combine(home, ".local", "lib", "ynet-quic"), "user-ynet-lib");
            yield return (System.IO.Path.Combine(home, ".local", "lib"), "user-lib");
        }

        yield return (null, "system-loader");
    }

    private static string RuntimeIdentifier()
    {
        var os = OperatingSystem.IsLinux() ? "linux"
               : OperatingSystem.IsWindows() ? "win"
               : OperatingSystem.IsMacOS() ? "osx"
               : "unknown";
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            var other => other.ToString().ToLowerInvariant(),
        };
        return $"{os}-{arch}";
    }
}

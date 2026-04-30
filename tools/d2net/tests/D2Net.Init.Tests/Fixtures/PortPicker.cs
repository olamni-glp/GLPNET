using System.Net;
using System.Net.Sockets;

namespace D2Net.Init.Tests.Fixtures;

/// <summary>
/// Free-port selector for parallel-safe test runs. Binds a TcpListener on
/// 127.0.0.1:0, captures the OS-assigned port, releases the listener, and
/// returns the port. Subject to a tiny TOCTOU window (the OS may reuse the
/// port between Stop and the test's Start), but acceptable in practice.
/// </summary>
public static class PortPicker
{
    public static int NextFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}

using System.Net;
using System.Net.Sockets;

namespace D2Net.Scaffold.Tests.Fixtures;

/// <summary>
/// Free-port selector for parallel-safe test runs. Mirrors the D2Net.Init.Tests fixture.
/// Subject to a tiny TOCTOU window between Stop and the test's Start, but acceptable in practice.
/// </summary>
public static class PortPicker
{
    public static int NextFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }
}

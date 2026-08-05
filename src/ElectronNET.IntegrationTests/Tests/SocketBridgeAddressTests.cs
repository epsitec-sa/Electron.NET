using ElectronNET.Runtime.Services.SocketBridge;

namespace ElectronNET.IntegrationTests.Tests;

/// <summary>
/// Unit tests pinning the loopback address used by the Electron &lt;-&gt; .NET socket
/// bridge - no Electron runtime required.
///
/// Both ends of the bridge must use the IPv4 loopback literal, unconditionally.
/// When one end binds 127.0.0.1 while the other connects to "localhost", Windows
/// resolves "localhost" to ::1 first; on a machine where the IPv6 loopback drops
/// the SYN instead of refusing it, every connection attempt pays a full TCP
/// retransmit timeout (~2s) before falling back to IPv4. Socket.IO opens two
/// connections per handshake (polling, then the WebSocket upgrade), so the cost
/// is paid twice and startup loses ~4s.
///
/// The literal is IPv4 and not ::1 because the IPv6 loopback can be disabled on
/// locked-down machines, which would turn a slow start into a failed one.
/// </summary>
public class SocketBridgeAddressTests
{
    // -----------------------------------------------------------------------
    // .NET side: the client must never rely on name resolution
    // -----------------------------------------------------------------------

    // 0 stands for the dynamic-port path (packaged, Electron-first), the other
    // for the forced-port path used by ElectronProcessActive when .NET starts
    // Electron. Neither may resolve a name.
    [Theory]
    [InlineData(0)]
    [InlineData(60911)]
    public void SocketBridgeService_ShouldTargetIPv4LoopbackLiteral(int port)
    {
        var service = new SocketBridgeService(port, authorization: null);

        service.SocketUrl.Should().Be(
            $"http://127.0.0.1:{port}",
            "because resolving \"localhost\" yields ::1 first on Windows, where nothing " +
            "is listening, costing a TCP retransmit timeout per connection attempt.");
    }

    // -----------------------------------------------------------------------
    // Electron side: the host must bind the same literal, unconditionally
    // -----------------------------------------------------------------------

    [Fact]
    public void MainJs_ShouldBindIPv4LoopbackLiteralUnconditionally()
    {
        var content = File.ReadAllText(MainJsPath);

        content.Should().Contain(
            "const host = '127.0.0.1';",
            "because the .NET client connects to the IPv4 loopback literal, and the two " +
            "ends of the bridge must agree.");

        // The previous form picked the bind address from how the port was chosen,
        // which is arbitrary and is what let the two ends drift apart: a dynamic
        // port bound IPv4-only while a forced port bound ::1.
        content.Should().NotContain(
            "'127.0.0.1' : 'localhost'",
            "because the bind address must not depend on whether the port was forced.");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static readonly string MainJsPath = FindMainJs();

    /// <summary>
    /// Walks up the directory tree from <see cref="AppContext.BaseDirectory"/> until it
    /// finds the Electron host's main.js. Robust against varying output paths (with or
    /// without RID subfolder, debug/release, etc.).
    /// </summary>
    private static string FindMainJs()
    {
        const string RelativeFromRepoRoot = "src/ElectronNET.Host/main.js";
        const string RelativeFromSrc = "ElectronNET.Host/main.js";

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var fromRepoRoot = Path.Combine(dir.FullName, RelativeFromRepoRoot);
            if (File.Exists(fromRepoRoot))
            {
                return Path.GetFullPath(fromRepoRoot);
            }

            var fromSrc = Path.Combine(dir.FullName, RelativeFromSrc);
            if (File.Exists(fromSrc))
            {
                return Path.GetFullPath(fromSrc);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate main.js by walking up from '{AppContext.BaseDirectory}'.");
    }
}

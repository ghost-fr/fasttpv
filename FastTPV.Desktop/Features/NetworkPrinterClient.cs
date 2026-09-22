using System.Net.Sockets;
using System.Threading;

namespace FastTPV.Desktop.Features;

/// <summary>
/// Sends raw bytes to a network (Ethernet/WiFi) thermal printer over the "raw
/// printing" TCP port used by virtually all ESC/POS network printers. Port 9100 is
/// the near-universal default for this (sometimes called "JetDirect" or "AppSocket"
/// printing) — it's a plain socket, not HTTP/IPP, so no request/response protocol,
/// just write the bytes and the printer prints them.
/// </summary>
public sealed class NetworkPrinterClient
{
    public async Task<bool> SendAsync(string ipAddress, int port, byte[] data, int timeoutMs)
    {
        if (string.IsNullOrWhiteSpace(ipAddress)) return false;

        using var client = new TcpClient();
        using var cts = new CancellationTokenSource(timeoutMs);

        try
        {
            await client.ConnectAsync(ipAddress, port, cts.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync(data, cts.Token);
            await stream.FlushAsync(cts.Token);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

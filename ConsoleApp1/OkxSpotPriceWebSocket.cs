using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

public static class OkxSpotPriceWebSocket
{
    /// <summary>
    /// �����ֵ䣬�洢ÿ��instId�������ּۣ�last��
    /// </summary>
    public static ConcurrentDictionary<string, string> LastPrices { get; } = new();

    /// <summary>
    /// ����WebSocket��������������LastPrices�ֵ�
    /// </summary>
    /// <param name="instIds">�Ҷ�����</param>
    /// <param name="proxyUrl">��ѡ�����ַ��Ĭ��127.0.0.1:29290</param>
    /// <returns></returns>
    public static async Task StartSpotPriceListenerAsync(string[] instIds, string? proxyUrl = "http://127.0.0.1:29290")
    {
        var proxy = new WebProxy(proxyUrl)
        {
            BypassProxyOnLocal = false
        };

        var wsUri = new Uri("wss://ws.okx.com:8443/ws/v5/public");
        var cws = new ClientWebSocket();

        cws.Options.Proxy = proxy;
        cws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;

        try
        {
            await cws.ConnectAsync(wsUri, CancellationToken.None);

            // ���Ķ���ֻ�ticker
            var subMsg = new
            {
                op = "subscribe",
                args = instIds.Select(id => new { channel = "tickers", instId = id }).ToArray()
            };
            var subJson = JsonSerializer.Serialize(subMsg);
            var subBuffer = Encoding.UTF8.GetBytes(subJson);
            await cws.SendAsync(new ArraySegment<byte>(subBuffer), WebSocketMessageType.Text, true, CancellationToken.None);

            var buffer = new byte[4096];
            while (cws.State == WebSocketState.Open)
            {
                var result = await cws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await cws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);

                try
                {
                    using var doc = JsonDocument.Parse(msg);
                    if (doc.RootElement.TryGetProperty("data", out var dataElem) && dataElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var data in dataElem.EnumerateArray())
                        {
                            if (data.TryGetProperty("instId", out var instIdElem) &&
                                data.TryGetProperty("last", out var lastElem))
                            {
                                var instId = instIdElem.GetString();
                                var last = lastElem.GetString();
                                if (!string.IsNullOrEmpty(instId) && !string.IsNullOrEmpty(last))
                                {
                                    LastPrices[instId] = last;
                                    Console.WriteLine($"�Ҷ�: {instId}, �����ּ�: {last}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"������Ϣ�쳣: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket����: {ex.Message}");
        }
    }

    /// <summary>
    /// ��ȡĳ���ҶԵ������ּۣ���δ��ȡ���򷵻�null��
    /// </summary>
    public static string? GetLastPrice(string instId)
    {
        LastPrices.TryGetValue(instId, out var price);
        return price;
    }
}
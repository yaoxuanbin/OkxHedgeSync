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
    /// 公共字典，存储每个instId的最新现价（last）
    /// </summary>
    public static ConcurrentDictionary<string, string> LastPrices { get; } = new();

    /// <summary>
    /// 启动WebSocket监听，持续更新LastPrices字典
    /// </summary>
    /// <param name="instIds">币对数组</param>
    /// <param name="proxyUrl">可选代理地址，默认127.0.0.1:29290</param>
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

            // 订阅多个现货ticker
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
                                    Console.WriteLine($"币对: {instId}, 最新现价: {last}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"解析消息异常: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取某个币对的最新现价（如未获取到则返回null）
    /// </summary>
    public static string? GetLastPrice(string instId)
    {
        LastPrices.TryGetValue(instId, out var price);
        return price;
    }
}
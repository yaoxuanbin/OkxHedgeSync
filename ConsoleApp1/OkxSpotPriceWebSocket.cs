using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class OkxSpotPriceWebSocket : BaseWebSocket<string>
{
    public OkxSpotPriceWebSocket(
        bool isSimulated = false,
        string? proxyUrl = null,
        bool enableLog = false,
        bool logToFile = false,
        string? logFilePath = null)
        : base(isSimulated, proxyUrl, enableLog, logToFile, logFilePath) { }


    /// <summary>
    /// 启动WebSocket监听，持续更新SharedDict字典
    /// </summary>
    /// <param name="instIds">币对数组</param>
    public async Task StartSpotPriceListenerAsync(string[] instIds)
    {
        var wsUri = new Uri("wss://ws.okx.com:8443/ws/v5/public");
        using var cws = CreateWebSocket();

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
                                    SharedDict[instId] = last;
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
        SharedDict.TryGetValue(instId, out var price);
        return price;
    }
}
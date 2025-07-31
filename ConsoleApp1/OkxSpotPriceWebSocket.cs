using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

public class OkxSpotPriceWebSocket : BaseWebSocket<string>
{
    private readonly bool _throttleEnable;
    private readonly int _throttleInterval;
    private readonly Dictionary<string, DateTime> _lastLogTime = new();

    public OkxSpotPriceWebSocket(
        bool isSimulated = false,
        string? proxyUrl = null,
        bool enableLog = false,
        bool logToFile = false,
        string? logFilePath = null,
        LogLevel minLogLevel = LogLevel.Info,
        LogLevel maxLogLevel = LogLevel.Error,
        bool throttleEnable = true,
        int throttleInterval = 1)
        : base(isSimulated, proxyUrl, enableLog, logToFile, logFilePath, minLogLevel, maxLogLevel)
    {
        _throttleEnable = throttleEnable;
        _throttleInterval = throttleInterval;
    }

    public async Task StartSpotPriceListenerAsync(string[] instIds)
    {
        while (true)
        {
            var wsUri = new Uri("wss://ws.okx.com:8443/ws/v5/public");
            using var cws = CreateWebSocket();
            try
            {
                await cws.ConnectAsync(wsUri, CancellationToken.None);

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
                        Log("WebSocket连接被服务器关闭，3秒后自动重连...", LogLevel.Warn);
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
                                        if (!_throttleEnable || ShouldLog(instId))
                                            Log($"币对: {instId}, 最新现价: {last}", LogLevel.Info);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"解析消息异常: {ex.Message}", LogLevel.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"WebSocket错误: {ex.Message}", LogLevel.Error);
                Log("WebSocket连接异常，3秒后自动重连...", LogLevel.Warn);
            }
            await Task.Delay(3000); // 3秒后重连
        }
    }

    private bool ShouldLog(string instId)
    {
        var now = DateTime.UtcNow;
        if (!_lastLogTime.TryGetValue(instId, out var last) || (now - last).TotalSeconds >= _throttleInterval)
        {
            _lastLogTime[instId] = now;
            return true;
        }
        return false;
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
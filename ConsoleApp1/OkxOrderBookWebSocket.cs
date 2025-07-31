using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class OkxOrderBookWebSocket : BaseWebSocket<OrderBookLevel>
{
    private readonly bool _throttleEnable;
    private readonly int _throttleInterval;
    private readonly Dictionary<string, DateTime> _lastLogTime = new();

    public OkxOrderBookWebSocket(
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

    public async Task StartOrderBookListenerAsync(
        string[] instIds,
        Dictionary<string, int> levels)
    {
        while (true)
        {
            foreach (var level in levels.Values)
            {
                if (level < 1 || level > 5)
                    throw new ArgumentException("level参数必须为1~5");
            }

            var wsUri = new Uri("wss://ws.okx.com:8443/ws/v5/public");
            using var cws = CreateWebSocket();

            try
            {
                await cws.ConnectAsync(wsUri, CancellationToken.None);

                var subMsg = new
                {
                    op = "subscribe",
                    args = instIds.Select(id => new { channel = "books5", instId = id }).ToArray()
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
                            foreach (var book in dataElem.EnumerateArray())
                            {
                                if (!book.TryGetProperty("instId", out var instIdElem)) continue;
                                var instId = instIdElem.GetString() ?? "";

                                var ob = new OrderBookLevel();

                                if (levels.TryGetValue("buy", out int buyLevel) &&
                                    book.TryGetProperty("bids", out var bidsElem) &&
                                    bidsElem.ValueKind == JsonValueKind.Array &&
                                    bidsElem.GetArrayLength() >= buyLevel)
                                {
                                    var bid = bidsElem[buyLevel - 1];
                                    ob.BuyPrice = bid[0].GetString();
                                    ob.BuySize = bid[1].GetString();
                                }
                                if (levels.TryGetValue("sell", out int sellLevel) &&
                                    book.TryGetProperty("asks", out var asksElem) &&
                                    asksElem.ValueKind == JsonValueKind.Array &&
                                    asksElem.GetArrayLength() >= sellLevel)
                                {
                                    var ask = asksElem[sellLevel - 1];
                                    ob.SellPrice = ask[0].GetString();
                                    ob.SellSize = ask[1].GetString();
                                }

                                if (!string.IsNullOrEmpty(ob.BuyPrice) || !string.IsNullOrEmpty(ob.SellPrice))
                                {
                                    SharedDict[instId] = ob;
                                    if (!_throttleEnable || ShouldLog(instId))
                                        Log($"[{DateTime.Now:HH:mm:ss}] {instId} 买{levels.GetValueOrDefault("buy", 0)}: {ob.BuyPrice} × {ob.BuySize} 卖{levels.GetValueOrDefault("sell", 0)}: {ob.SellPrice} × {ob.SellSize}", LogLevel.Info);
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
}

public class OrderBookLevel
{
    public string? BuyPrice { get; set; }
    public string? BuySize { get; set; }
    public string? SellPrice { get; set; }
    public string? SellSize { get; set; }
}
using System;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

public class OkxPositionAccountWebSocket : BaseWebSocket<decimal>
{
    public OkxPositionAccountWebSocket(
        bool isSimulated = false,
        string? proxyUrl = null,
        bool enableLog = false,
        bool logToFile = false,
        string? logFilePath = null,
        LogLevel minLogLevel = LogLevel.Info,
        LogLevel maxLogLevel = LogLevel.Error)
        : base(isSimulated, proxyUrl, enableLog, logToFile, logFilePath, minLogLevel, maxLogLevel) { }

    private static string Sign(string secret, string prehash)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var msg = Encoding.UTF8.GetBytes(prehash);
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(msg);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// 启动账户和持仓WebSocket监听，自动鉴权并持续更新SharedDict
    /// </summary>
    public async Task CheckAccountPositionsWebSocket(
        string apiKey,
        string secretKey,
        string passphrase,
        IEnumerable<string> instIds)
    {
        var wsUri = new Uri("wss://wspap.okx.com:8443/ws/v5/private?brokerId=9999");
        using var cws = CreateWebSocket();

        // 用于筛选合约/现货币对
        var instIdSet = new HashSet<string>(instIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        // 用于筛选现货币种
        var ccySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in instIdSet)
        {
            var ccy = id.Split('-')[0];
            ccySet.Add(ccy);
        }

        try
        {
            await cws.ConnectAsync(wsUri, CancellationToken.None);

            // 1. 鉴权
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signStr = ts + "GET" + "/users/self/verify";
            var sign = Sign(secretKey, signStr);

            var loginMsg = new
            {
                op = "login",
                args = new[]
                {
                    new Dictionary<string, object>
                    {
                        { "apiKey", apiKey },
                        { "passphrase", passphrase },
                        { "timestamp", ts },
                        { "sign", sign }
                    }
                }
            };
            var loginJson = JsonSerializer.Serialize(loginMsg);
            await cws.SendAsync(Encoding.UTF8.GetBytes(loginJson), WebSocketMessageType.Text, true, CancellationToken.None);

            var buffer = new byte[4096];
            bool authed = false;

            while (cws.State == WebSocketState.Open)
            {
                try
                {
                    var result = await cws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    // 登录成功后订阅频道
                    if (!authed && msg.Contains("\"event\":\"login\"") && msg.Contains("\"code\":\"0\""))
                    {
                        authed = true;
                        var subMsg = new
                        {
                            op = "subscribe",
                            args = new object[]
                            {
                                new { channel = "positions", instType = "SWAP" },
                                new { channel = "positions", instType = "SPOT" },
                                new { channel = "account" }
                            }
                        };
                        var subJson = JsonSerializer.Serialize(subMsg);
                        await cws.SendAsync(Encoding.UTF8.GetBytes(subJson), WebSocketMessageType.Text, true, CancellationToken.None);
                        Log("已鉴权，已订阅合约持仓和资产频道。", LogLevel.Info);
                    }

                    // 合约持仓数据
                    if (msg.Contains("\"channel\":\"positions\""))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(msg);
                            if (doc.RootElement.TryGetProperty("data", out var dataElem) && dataElem.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var pos in dataElem.EnumerateArray())
                                {
                                    var instId = pos.GetProperty("instId").GetString();
                                    if (instIdSet.Count == 0 || instIdSet.Contains(instId))
                                    {
                                        var posSide = pos.GetProperty("posSide").GetString();
                                        var posSz = pos.GetProperty("pos").GetString();
                                        Log($"合约持仓: {instId} {posSide} 数量: {posSz}", LogLevel.Info);

                                        if (decimal.TryParse(posSz, out var posValue))
                                        {
                                            SharedDict[$"{instId}_{posSide}"] = posValue;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"解析持仓消息异常: {ex.Message}", LogLevel.Error);
                        }
                    }

                    // 现货资产数据
                    if (msg.Contains("\"channel\":\"account\""))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(msg);
                            if (doc.RootElement.TryGetProperty("data", out var dataElem) && dataElem.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var acc in dataElem.EnumerateArray())
                                {
                                    if (acc.TryGetProperty("details", out var detailsElem) && detailsElem.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var detail in detailsElem.EnumerateArray())
                                        {
                                            var ccy = detail.GetProperty("ccy").GetString();
                                            if (ccySet.Count == 0 || ccySet.Contains(ccy))
                                            {
                                                var availBal = detail.GetProperty("availBal").GetString();
                                                Log($"现货资产: {ccy} 可用余额: {availBal}", LogLevel.Info);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"解析资产消息异常: {ex.Message}", LogLevel.Error);
                        }
                    }

                    // 如果收到关闭消息，退出循环
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Log("WebSocket被服务器关闭。", LogLevel.Warn);
                        await cws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Log($"WebSocket循环异常: {ex}", LogLevel.Error);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"WebSocket错误: {ex.Message}", LogLevel.Error);
        }
    }
}
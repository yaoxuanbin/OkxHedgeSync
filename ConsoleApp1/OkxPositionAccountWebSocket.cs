using System;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent; 

class OkxPositionAccountWebSocket
{
    // 公开静态字典用于存储持仓数据
    public static ConcurrentDictionary<string, decimal> PositionDict { get; } = new();

    // 生成OKX签名
    static string Sign(string secret, string prehash)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var msg = Encoding.UTF8.GetBytes(prehash);
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(msg);
        return Convert.ToBase64String(hash);
    }

    public static async Task CheckAccountPositionsWebSocket(
        string apiKey,
        string secretKey,
        string passphrase,
        IEnumerable<string> instIds,
        string proxyUrl = "http://127.0.0.1:29290")
    {
        var wsUri = new Uri("wss://wspap.okx.com:8443/ws/v5/private?brokerId=9999");
        var proxy = new WebProxy(proxyUrl) { BypassProxyOnLocal = false };
        var cws = new ClientWebSocket();
        cws.Options.Proxy = proxy;
        cws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;

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
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(); // 秒级
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
                        Console.WriteLine("已鉴权，已订阅合约持仓和资产频道。");
                    }

                    // 打印合约持仓数据
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
                                        Console.WriteLine($"合约持仓: {instId} {posSide} 数量: {posSz}");

// 存储到字典
                                        if (decimal.TryParse(posSz, out var posValue))
                                        {
                                            // key可以自定义，比如用instId+posSide区分多空
                                            PositionDict[$"{instId}_{posSide}"] = posValue;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"解析持仓消息异常: {ex.Message}");
                        }
                    }

                    // 打印现货资产数据
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
                                                Console.WriteLine($"现货资产: {ccy} 可用余额: {availBal}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"解析资产消息异常: {ex.Message}");
                        }
                    }

                    // 如果收到关闭消息，退出循环
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("WebSocket被服务器关闭。");
                        await cws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WebSocket循环异常: {ex}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket错误: {ex.Message}");
        }
    }
}
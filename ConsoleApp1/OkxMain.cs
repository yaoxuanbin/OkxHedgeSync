using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

class OKxMain
{
    static async Task Main()
    {
        // 读取配置文件
        var configText = await File.ReadAllTextAsync("OkxSettings.json");
        var settings = JsonSerializer.Deserialize<OkxSettings>(configText)!;

        Console.WriteLine("启动OKX交易API（支持模拟盘/实盘，强制代理 127.0.0.1:29290）...");

        // 1. 行情WebSocket
        var spotPriceWs = new OkxSpotPriceWebSocket(settings.IsSimulated, settings.ProxyUrl);
        var priceTask = spotPriceWs.StartSpotPriceListenerAsync(new[] { "DOGE-USDT", "DOGE-USDT-SWAP" });

        // 2. 账户持仓WebSocket
        var positionWs = new OkxPositionAccountWebSocket(settings.IsSimulated, settings.ProxyUrl);
        var positionTask = positionWs.CheckAccountPositionsWebSocket(
            settings.ApiKey,
            settings.SecretKey,
            settings.Passphrase,
            new[] { "DOGE-USDT", "DOGE-USDT-SWAP" }
        );

        // 3. 盘口WebSocket
        var orderBookWs = new OkxOrderBookWebSocket(settings.IsSimulated, settings.ProxyUrl);
        var orderBookTask = orderBookWs.StartOrderBookListenerAsync(
            new[] { "DOGE-USDT", "BTC-USDT" },
            new Dictionary<string, int> { { "buy", 2 }, { "sell", 2 } }
        );

        await Task.WhenAll(priceTask, positionTask, orderBookTask);
    }
}
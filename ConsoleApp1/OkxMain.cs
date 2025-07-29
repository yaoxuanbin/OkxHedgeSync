using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

class OKxMain
{
    static LogLevel ParseLogLevel(string level) =>
        Enum.TryParse<LogLevel>(level, true, out var l) ? l : LogLevel.Info;

    static async Task Main()
    {
        // 读取配置文件
        var configText = await File.ReadAllTextAsync("OkxSettings.json");
        var settings = JsonSerializer.Deserialize<OkxSettings>(configText)!;

        Console.WriteLine("启动OKX交易API（支持模拟盘/实盘，强制代理 127.0.0.1:29290）...");

        // 读取每个WebSocket的日志配置
        var spotLog = settings.WebSocketLogs.SpotPrice;
        var positionLog = settings.WebSocketLogs.PositionAccount;
        var orderBookLog = settings.WebSocketLogs.OrderBook;

        // 1. 行情WebSocket
        var spotPriceWs = new OkxSpotPriceWebSocket(
                   settings.IsSimulated,
                   settings.ProxyUrl,
                   spotLog.EnableLog,
                   spotLog.LogToFile,
                   spotLog.LogFilePath,
                   ParseLogLevel(spotLog.MinLevel), // 使用配置文件中的MinLevel
                   ParseLogLevel(spotLog.MaxLevel)  // 使用配置文件中的MaxLevel
        );
        var priceTask = spotPriceWs.StartSpotPriceListenerAsync(new[] { "DOGE-USDT", "DOGE-USDT-SWAP" });

        var positionWs = new OkxPositionAccountWebSocket(
             settings.IsSimulated,
             settings.ProxyUrl,
             positionLog.EnableLog,
             positionLog.LogToFile,
             positionLog.LogFilePath,
             ParseLogLevel(positionLog.MinLevel), // 使用配置文件中的MinLevel
             ParseLogLevel(positionLog.MaxLevel)  // 使用配置文件中的MaxLevel
         );
        var positionTask = positionWs.CheckAccountPositionsWebSocket(
                    settings.ApiKey,
                    settings.SecretKey,
                    settings.Passphrase,
                    new[] { "DOGE-USDT", "DOGE-USDT-SWAP" }
        );
        // 3. 盘口WebSocket
        var orderBookWs = new OkxOrderBookWebSocket(
             settings.IsSimulated,
             settings.ProxyUrl,
             orderBookLog.EnableLog,
             orderBookLog.LogToFile,
             orderBookLog.LogFilePath,
             ParseLogLevel(orderBookLog.MinLevel), // 使用配置文件中的MinLevel
             ParseLogLevel(orderBookLog.MaxLevel)  // 使用配置文件中的MaxLevel
         );
        var orderBookTask = orderBookWs.StartOrderBookListenerAsync(
            new[] { "DOGE-USDT", "BTC-USDT" },
            new Dictionary<string, int> { { "buy", 2 }, { "sell", 2 } }
        );

        await Task.WhenAll(priceTask, positionTask, orderBookTask);
    }
}
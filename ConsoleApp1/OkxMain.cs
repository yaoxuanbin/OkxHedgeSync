using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

class OKxMain
{
    static LogLevel ParseLogLevel(string level) =>
        Enum.TryParse<LogLevel>(level, true, out var l) ? l : LogLevel.Info;

    static async Task Main()
    {
        // 读取主配置
        var configText = await File.ReadAllTextAsync("OkxSettings.json");
        var settings = JsonSerializer.Deserialize<OkxSettings>(configText)!;

        // 读取交易对配置
        var tradingPairsJson = await File.ReadAllTextAsync("TradingPairs.json");
        var tradingPairs = JsonSerializer.Deserialize<List<TradingPairConfig>>(tradingPairsJson)!;

        Console.WriteLine("启动OKX交易API（支持模拟盘/实盘，强制代理 127.0.0.1:29290）...");

        // 读取WebSocket日志配置
        var spotLog = settings.WebSocketLogs.SpotPrice;
        var positionLog = settings.WebSocketLogs.PositionAccount;
        var orderBookLog = settings.WebSocketLogs.OrderBook;

        // 整理所有币对（现货和合约）去重
        var allInstIds = tradingPairs
            .SelectMany(p => new[] { p.Spot, p.Swap })
            .Distinct()
            .ToArray();

        // 盘口档位（取所有交易对最大SellLevel，保证盘口WebSocket能满足所有对的需求）
        int maxSellLevel = tradingPairs.Max(p => p.SellLevel);
        int maxBuyLevel = maxSellLevel; // 假设买档和卖档一致

        var orderBookLevels = new Dictionary<string, int>
        {
            { "buy", maxBuyLevel },
            { "sell", maxSellLevel }
        };

        // 1. 行情WebSocket
        var spotPriceWs = new OkxSpotPriceWebSocket(
            settings.IsSimulated,
            settings.ProxyUrl,
            spotLog.EnableLog,
            spotLog.LogToFile,
            spotLog.LogFilePath,
            ParseLogLevel(spotLog.MinLevel),
            ParseLogLevel(spotLog.MaxLevel),
            settings.LogThrottle.Enable,
            settings.LogThrottle.IntervalSeconds
        );
        var priceTask = spotPriceWs.StartSpotPriceListenerAsync(allInstIds);

        // 2. 盘口WebSocket
        var orderBookWs = new OkxOrderBookWebSocket(
            settings.IsSimulated,
            settings.ProxyUrl,
            orderBookLog.EnableLog,
            orderBookLog.LogToFile,
            orderBookLog.LogFilePath,
            ParseLogLevel(orderBookLog.MinLevel),
            ParseLogLevel(orderBookLog.MaxLevel),
            settings.LogThrottle.Enable,
            settings.LogThrottle.IntervalSeconds
        );
        var orderBookTask = orderBookWs.StartOrderBookListenerAsync(
            allInstIds,
            orderBookLevels
        );

        // 3. 账户/持仓WebSocket
        var positionWs = new OkxPositionAccountWebSocket(
            settings.IsSimulated,
            settings.ProxyUrl,
            positionLog.EnableLog,
            positionLog.LogToFile,
            positionLog.LogFilePath,
            ParseLogLevel(positionLog.MinLevel),
            ParseLogLevel(positionLog.MaxLevel)
        );
        var positionTask = positionWs.CheckAccountPositionsWebSocket(
            settings.ApiKey,
            settings.SecretKey,
            settings.Passphrase,
            allInstIds
        );

        // 4. 主交易逻辑
        ITradeClient tradeClient = new OkxTradeClient(
            settings.ApiKey,
            settings.SecretKey,
            settings.Passphrase,
            settings.IsSimulated,
            settings.ProxyUrl
        );

        IPositionClient positionClient = positionWs;

        // 交易记录日志委托
        Action<string> tradeRecordLog = msg => {
            if (settings.TradeRecord.Enable && settings.TradeRecord.LogToFile && !string.IsNullOrEmpty(settings.TradeRecord.LogFilePath))
            {
                File.AppendAllText(settings.TradeRecord.LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}{Environment.NewLine}");
            }
        };

        // 传递日志委托给OkxMainTrader
        var trader = new OkxMainTrader(tradingPairs, tradeClient, positionClient, ((msg, level) => ((OkxTradeClient)tradeClient).Log(msg, level)), tradeRecordLog);
        var tradeTask = trader.RunAsync();

        await Task.WhenAll(priceTask, orderBookTask, positionTask, tradeTask);
    }
}
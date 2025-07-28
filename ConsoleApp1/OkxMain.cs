using System;
using System.Threading.Tasks;

class OKxMain
{
    static async Task Main()
    {
        var apiKey = "770a5580-588e-4605-a2ca-57224ccba876";
        var secretKey = "B4734E2885D1B71136E4732B394A3C3D";
        var passphrase = "Ww@123456";

        Console.WriteLine("启动OKX交易API（支持模拟盘/实盘，强制代理 127.0.0.1:29290）...");

        // 设置为模拟盘（true）或实盘（false）
        bool isSimulated = true;
        // 如需代理，填写代理地址，否则为 null
        string? proxyUrl = "http://127.0.0.1:29290";

        //启动行情WebSocket
        var priceTask = OkxSpotPriceWebSocket.StartSpotPriceListenerAsync(new[] { "DOGE-USDT", "DOGE-USDT-SWAP" });

        //await priceTask;
        ////启动账户持仓WebSocket
        var positionTask = OkxPositionAccountWebSocket.CheckAccountPositionsWebSocket(
            apiKey,
            secretKey,
            passphrase,
            new[] { "DOGE-USDT", "DOGE-USDT-SWAP" }
        );

        await Task.WhenAll(priceTask, positionTask);

        //var client = new OkxTradeClient(apiKey, secretKey, passphrase, isSimulated, proxyUrl);

        //try
        //{
        //    // 买入小币种（如DOGE-USDT），数量建议大于最小下单金额
        //    string spotBuyResult = await client.PlaceSpotMarketBuyOrderAsync("DOGE-USDT", 20m);
        //    Console.WriteLine("现货买入结果: " + spotBuyResult);

        //    // 卖出小币种（如DOGE-USDT）
        //    string spotSellResult = await client.PlaceSpotMarketSellOrderAsync("DOGE-USDT", 20m);
        //    Console.WriteLine("现货卖出结果: " + spotSellResult);

        //    // 开空
        //    string openShortResult = await client.PlaceSwapMarketSellOrderAsync("DOGE-USDT-SWAP", 0.1m, "short");
        //    Console.WriteLine("DOGE永续合约开空结果: " + openShortResult);

        //    // 平空
        //    string closeShortResult = await client.PlaceSwapMarketBuyOrderAsync("DOGE-USDT-SWAP", 0.1m, "short");
        //    Console.WriteLine("DOGE永续合约平空结果: " + closeShortResult);
        //}
        //catch (Exception ex)
        //{
        //    Console.WriteLine("交易异常: " + ex.Message);
        //}

        // 启动监听，获取DOGE-USDT和BTC-USDT的买一卖二行情（模拟盘，带代理）
        //_= OkxOrderBookWebSocket.StartOrderBookListenerAsync(
        //    new[] { "DOGE-USDT", "BTC-USDT" },
        //    new Dictionary<string, int> { { "buy", 1 }, { "sell", 1 } },
        //    isSimulated: true,
        //    proxyUrl: "http://127.0.0.1:29290"
        //);

        //await Task.Delay(5000); // 等待5秒钟，确保WebSocket连接已建立

        // 随时读取最新盘口
        //var dogeBook = OkxOrderBookWebSocket.OrderBookLevels.GetValueOrDefault("DOGE-USDT");
        //if (dogeBook != null)
        //{
        //    Console.WriteLine($"DOGE买一: {dogeBook.BuyPrice} 卖二: {dogeBook.SellPrice}");
        //}


    }
}
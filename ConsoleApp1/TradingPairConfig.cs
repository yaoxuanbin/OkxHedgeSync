using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class TradingPairConfig
{
    public string Spot { get; set; } = "";
    public string Swap { get; set; } = "";
    public double OpenThreshold { get; set; }
    public double CloseThreshold { get; set; }
    public int SellLevel { get; set; } = 2; // 卖几档，默认2
    public double SpotQuantity { get; set; } = 1; // 现货下单数量
    public double SwapQuantity { get; set; } = 1; // 合约下单数量
}

public class OkxMainTrader
{
    private readonly List<TradingPairConfig> _pairs;
    private readonly ITradeClient _tradeClient;
    private readonly IPositionClient _positionClient;

    public OkxMainTrader(List<TradingPairConfig> pairs, ITradeClient tradeClient, IPositionClient positionClient)
    {
        _pairs = pairs;
        _tradeClient = tradeClient;
        _positionClient = positionClient;
    }

    public async Task RunAsync()
    {
        while (true)
        {
            foreach (var pair in _pairs)
            {
                // 获取现货和合约的 last price
                var spotLastStr = OkxSpotPriceWebSocket.GetLastPrice(pair.Spot);
                var swapLastStr = OkxSpotPriceWebSocket.GetLastPrice(pair.Swap);
                if (!double.TryParse(spotLastStr, out var spotLast) ||
                    !double.TryParse(swapLastStr, out var swapLast))
                    continue;

                // 获取现货盘口的卖N价
                if (!OkxOrderBookWebSocket.SharedDict.TryGetValue(pair.Spot, out var spotBook) ||
                    spotBook == null)
                    continue;

                // 卖N价（N=SellLevel，1为卖一，2为卖二...）
                var sellLevel = pair.SellLevel;
                var sellPrice = spotBook.SellPrice; // 你的OrderBookLevel.SellPrice就是配置的档
                if (!double.TryParse(sellPrice, out var spotSellN))
                    continue;

                // 1. 一般价差判断
                var diff = (swapLast - spotLast) / spotLast;

                // 2. 开仓判断（合约last - 现货卖N价）
                var openDiff = (swapLast - spotSellN) / spotSellN;

                // 3. 持仓检查
                var spotPos = await _positionClient.GetSpotPositionAsync(pair.Spot);
                var swapPos = await _positionClient.GetSwapPositionAsync(pair.Swap);

                if (openDiff > pair.OpenThreshold && spotPos== 0 && swapPos == 0)
                {
                    // 买入现货（按卖N价），做空合约
                    await _tradeClient.BuySpotAsync(pair.Spot, pair.SpotQuantity, spotSellN);
                    await _tradeClient.SellSwapAsync(pair.Swap, pair.SwapQuantity, swapLast);
                }

                // 4. 平仓判断
                if (spotPos > 0 && swapPos > 0)
                {
                    var closeDiff = (swapLast - spotLast) / spotLast;
                    if (closeDiff < pair.CloseThreshold)
                    {
                        await _tradeClient.SellSpotAsync(pair.Spot, spotPos, spotLast);
                        await _tradeClient.BuySwapAsync(pair.Swap, swapPos, swapLast);
                    }
                }
            }
            await Task.Delay(1000); // 每0.1秒轮询一次
        }
    }
}
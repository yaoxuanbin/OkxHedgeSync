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
    private readonly Action<string, LogLevel> _log;
    private readonly Action<string> _tradeRecordLog;

    // 新增：每个币对的开仓/平仓冻结时间戳
    private readonly Dictionary<string, DateTime> _openFreezeUntil = new();
    private readonly Dictionary<string, DateTime> _closeFreezeUntil = new();
    private readonly TimeSpan _freezeDuration = TimeSpan.FromSeconds(5);

    public OkxMainTrader(List<TradingPairConfig> pairs, ITradeClient tradeClient, IPositionClient positionClient, Action<string, LogLevel> log, Action<string> tradeRecordLog)
    {
        _pairs = pairs;
        _tradeClient = tradeClient;
        _positionClient = positionClient;
        _log = log;
        _tradeRecordLog = tradeRecordLog;
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

                // 冻结key用币对名区分
                string openKey = pair.Spot + "/" + pair.Swap + "/open";
                string closeKey = pair.Spot + "/" + pair.Swap + "/close";

                // 检查开仓冻结
                if (!_openFreezeUntil.TryGetValue(openKey, out var openFreeze) || openFreeze < DateTime.UtcNow)
                {
                    if (openDiff > pair.OpenThreshold && spotPos == 0 && swapPos == 0)
                    {
                        await _tradeClient.BuySpotAsync(pair.Spot, pair.SpotQuantity, spotSellN);
                        _log($"开仓 买现货 {pair.Spot} 数量:{pair.SpotQuantity} 价格:{spotSellN}", LogLevel.Info);
                        await _tradeClient.SellSwapAsync(pair.Swap, pair.SwapQuantity, swapLast);
                        _log($"开仓 卖合约 {pair.Swap} 数量:{pair.SwapQuantity} 价格:{swapLast}", LogLevel.Info);
                        _openFreezeUntil[openKey] = DateTime.UtcNow + _freezeDuration;
                        // 记录详细交易信息
                        _tradeRecordLog($"[开仓] {pair.Spot}/{pair.Swap} openDiff={openDiff:F6} spotLast={spotLast} swapLast={swapLast} spotSellN={spotSellN} qty={pair.SpotQuantity}/{pair.SwapQuantity}");
                    }
                }

                // 检查平仓冻结
                if (!_closeFreezeUntil.TryGetValue(closeKey, out var closeFreeze) || closeFreeze < DateTime.UtcNow)
                {
                    if (spotPos > 1 && swapPos > 0)
                    {
                        var closeDiff = (swapLast - spotLast) / spotLast;
                        if (closeDiff < pair.CloseThreshold)
                        {
                            await _tradeClient.SellSpotAsync(pair.Spot, spotPos, spotLast);
                            _log($"平仓 卖现货 {pair.Spot} 数量:{spotPos} 价格:{spotLast}", LogLevel.Info);
                            await _tradeClient.CloseShortSwapAsync(pair.Swap, swapPos, swapLast); // 修改为平空仓
                            _log($"平仓 买合约(平空) {pair.Swap} 数量:{swapPos} 价格:{swapLast}", LogLevel.Info);
                            _closeFreezeUntil[closeKey] = DateTime.UtcNow + _freezeDuration;
                            // 记录详细交易信息
                            _tradeRecordLog($"[平仓] {pair.Spot}/{pair.Swap} closeDiff={closeDiff:F6} spotLast={spotLast} swapLast={swapLast} spotSellN={spotSellN} qty={spotPos}/{swapPos}");
                        }
                    }
                }
            }
            await Task.Delay(1000); // 每1秒轮询一次
        }
    }
}
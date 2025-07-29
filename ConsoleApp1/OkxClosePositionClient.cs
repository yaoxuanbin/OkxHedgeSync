using System;
using System.Threading.Tasks;

/// <summary>
/// OKX平仓专用客户端，支持平多/平空、卖出现货
/// </summary>
public class OkxClosePositionClient
{
    private readonly OkxTradeClient _tradeClient;

    public OkxClosePositionClient(OkxTradeClient tradeClient)
    {
        _tradeClient = tradeClient;
    }

    /// <summary>
    /// 卖出现货（如 DOGE-USDT）
    /// </summary>
    public async Task<string> SellSpotAsync(string instId, decimal size)
    {
        return await _tradeClient.PlaceSpotMarketSellOrderAsync(instId, size);
    }

    /// <summary>
    /// 平空仓（即买入平掉合约空头持仓）
    /// </summary>
    public async Task<string> CloseShortAsync(string instId, decimal size)
    {
        // 平空 = 买入 short
        return await _tradeClient.PlaceSwapMarketBuyOrderAsync(instId, size, "short");
    }
}
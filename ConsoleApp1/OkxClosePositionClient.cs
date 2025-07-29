using System;
using System.Threading.Tasks;

/// <summary>
/// OKXƽ��ר�ÿͻ��ˣ�֧��ƽ��/ƽ�ա������ֻ�
/// </summary>
public class OkxClosePositionClient
{
    private readonly OkxTradeClient _tradeClient;

    public OkxClosePositionClient(OkxTradeClient tradeClient)
    {
        _tradeClient = tradeClient;
    }

    /// <summary>
    /// �����ֻ����� DOGE-USDT��
    /// </summary>
    public async Task<string> SellSpotAsync(string instId, decimal size)
    {
        return await _tradeClient.PlaceSpotMarketSellOrderAsync(instId, size);
    }

    /// <summary>
    /// ƽ�ղ֣�������ƽ����Լ��ͷ�ֲ֣�
    /// </summary>
    public async Task<string> CloseShortAsync(string instId, decimal size)
    {
        // ƽ�� = ���� short
        return await _tradeClient.PlaceSwapMarketBuyOrderAsync(instId, size, "short");
    }
}
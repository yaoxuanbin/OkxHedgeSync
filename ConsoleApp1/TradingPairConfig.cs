using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class TradingPairConfig
{
    public string Spot { get; set; } = "";
    public string Swap { get; set; } = "";
    public double OpenThreshold { get; set; }
    public double CloseThreshold { get; set; }
    public int SellLevel { get; set; } = 2; // ��������Ĭ��2
    public double SpotQuantity { get; set; } = 1; // �ֻ��µ�����
    public double SwapQuantity { get; set; } = 1; // ��Լ�µ�����
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
                // ��ȡ�ֻ��ͺ�Լ�� last price
                var spotLastStr = OkxSpotPriceWebSocket.GetLastPrice(pair.Spot);
                var swapLastStr = OkxSpotPriceWebSocket.GetLastPrice(pair.Swap);
                if (!double.TryParse(spotLastStr, out var spotLast) ||
                    !double.TryParse(swapLastStr, out var swapLast))
                    continue;

                // ��ȡ�ֻ��̿ڵ���N��
                if (!OkxOrderBookWebSocket.SharedDict.TryGetValue(pair.Spot, out var spotBook) ||
                    spotBook == null)
                    continue;

                // ��N�ۣ�N=SellLevel��1Ϊ��һ��2Ϊ����...��
                var sellLevel = pair.SellLevel;
                var sellPrice = spotBook.SellPrice; // ���OrderBookLevel.SellPrice�������õĵ�
                if (!double.TryParse(sellPrice, out var spotSellN))
                    continue;

                // 1. һ��۲��ж�
                var diff = (swapLast - spotLast) / spotLast;

                // 2. �����жϣ���Լlast - �ֻ���N�ۣ�
                var openDiff = (swapLast - spotSellN) / spotSellN;

                // 3. �ֲּ��
                var spotPos = await _positionClient.GetSpotPositionAsync(pair.Spot);
                var swapPos = await _positionClient.GetSwapPositionAsync(pair.Swap);

                if (openDiff > pair.OpenThreshold && spotPos== 0 && swapPos == 0)
                {
                    // �����ֻ�������N�ۣ������պ�Լ
                    await _tradeClient.BuySpotAsync(pair.Spot, pair.SpotQuantity, spotSellN);
                    await _tradeClient.SellSwapAsync(pair.Swap, pair.SwapQuantity, swapLast);
                }

                // 4. ƽ���ж�
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
            await Task.Delay(1000); // ÿ0.1����ѯһ��
        }
    }
}
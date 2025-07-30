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
    private readonly Action<string, LogLevel> _log;
    private readonly Action<string> _tradeRecordLog;

    // ������ÿ���ҶԵĿ���/ƽ�ֶ���ʱ���
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

                // ����key�ñҶ�������
                string openKey = pair.Spot + "/" + pair.Swap + "/open";
                string closeKey = pair.Spot + "/" + pair.Swap + "/close";

                // ��鿪�ֶ���
                if (!_openFreezeUntil.TryGetValue(openKey, out var openFreeze) || openFreeze < DateTime.UtcNow)
                {
                    if (openDiff > pair.OpenThreshold && spotPos == 0 && swapPos == 0)
                    {
                        await _tradeClient.BuySpotAsync(pair.Spot, pair.SpotQuantity, spotSellN);
                        _log($"���� ���ֻ� {pair.Spot} ����:{pair.SpotQuantity} �۸�:{spotSellN}", LogLevel.Info);
                        await _tradeClient.SellSwapAsync(pair.Swap, pair.SwapQuantity, swapLast);
                        _log($"���� ����Լ {pair.Swap} ����:{pair.SwapQuantity} �۸�:{swapLast}", LogLevel.Info);
                        _openFreezeUntil[openKey] = DateTime.UtcNow + _freezeDuration;
                        // ��¼��ϸ������Ϣ
                        _tradeRecordLog($"[����] {pair.Spot}/{pair.Swap} openDiff={openDiff:F6} spotLast={spotLast} swapLast={swapLast} spotSellN={spotSellN} qty={pair.SpotQuantity}/{pair.SwapQuantity}");
                    }
                }

                // ���ƽ�ֶ���
                if (!_closeFreezeUntil.TryGetValue(closeKey, out var closeFreeze) || closeFreeze < DateTime.UtcNow)
                {
                    if (spotPos > 1 && swapPos > 0)
                    {
                        var closeDiff = (swapLast - spotLast) / spotLast;
                        if (closeDiff < pair.CloseThreshold)
                        {
                            await _tradeClient.SellSpotAsync(pair.Spot, spotPos, spotLast);
                            _log($"ƽ�� ���ֻ� {pair.Spot} ����:{spotPos} �۸�:{spotLast}", LogLevel.Info);
                            await _tradeClient.CloseShortSwapAsync(pair.Swap, swapPos, swapLast); // �޸�Ϊƽ�ղ�
                            _log($"ƽ�� ���Լ(ƽ��) {pair.Swap} ����:{swapPos} �۸�:{swapLast}", LogLevel.Info);
                            _closeFreezeUntil[closeKey] = DateTime.UtcNow + _freezeDuration;
                            // ��¼��ϸ������Ϣ
                            _tradeRecordLog($"[ƽ��] {pair.Spot}/{pair.Swap} closeDiff={closeDiff:F6} spotLast={spotLast} swapLast={swapLast} spotSellN={spotSellN} qty={spotPos}/{swapPos}");
                        }
                    }
                }
            }
            await Task.Delay(1000); // ÿ1����ѯһ��
        }
    }
}
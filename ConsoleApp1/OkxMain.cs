using System;
using System.Threading.Tasks;

class OKxMain
{
    static async Task Main()
    {
        Console.WriteLine("启动OKX WebSocket 价格/账户监控（强制代理 127.0.0.1:29290）...");

        // 启动行情WebSocket
        var priceTask = OkxSpotPriceWebSocket.GetMultiSpotPriceWebSocket(new[] { "DOGE-USDT", "DOGE-USDT-SWAP" });

        var positionTask = OkxPositionAccountWebSocket.CheckAccountPositionsWebSocket(
            "770a5580-588e-4605-a2ca-57224ccba876",
            "B4734E2885D1B71136E4732B394A3C3D",
            "Ww@123456", 
            new[] { "DOGE-USDT", "DOGE-USDT-SWAP" }
        );
        //await positionTask;

        await Task.WhenAll(priceTask, positionTask);
    }
}
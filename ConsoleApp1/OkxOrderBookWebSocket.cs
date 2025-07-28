using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// ͨ��WebSocket����OKX�̿��嵵���ݣ�֧��ָ��������λ��ģ���̲��������instId�����������µ������ֵ�
/// </summary>
public class OkxOrderBookWebSocket
{
    /// <summary>
    /// �����ֵ䣬�洢ÿ��instId����������������
    /// Key: instId
    /// Value: OrderBookLevel { BuyPrice, BuySize, SellPrice, SellSize }
    /// </summary>
    public static ConcurrentDictionary<string, OrderBookLevel> OrderBookLevels { get; } = new();

    /// <summary>
    /// �������Ĳ�ʵʱ����ָ���ҶԵ��̿�ָ��������λ����
    /// </summary>
    /// <param name="instIds">�Ҷ����飬�� ["DOGE-USDT", "BTC-USDT"]</param>
    /// <param name="levels">������λ�ֵ䣬�� { "buy": 1, "sell": 2 } ��ʾ��һ����</param>
    /// <param name="isSimulated">�Ƿ�ģ����</param>
    /// <param name="proxyUrl">��ѡ�����ַ</param>
    /// <returns></returns>
    public static async Task StartOrderBookListenerAsync(
        string[] instIds,
        Dictionary<string, int> levels,
        bool isSimulated = false,
        string? proxyUrl = null)
    {
        foreach (var level in levels.Values)
        {
            if (level < 1 || level > 5)
                throw new ArgumentException("level��������Ϊ1~5");
        }

        var wsUri = new Uri("wss://ws.okx.com:8443/ws/v5/public");
        var cws = new ClientWebSocket();

        if (!string.IsNullOrEmpty(proxyUrl))
        {
            cws.Options.Proxy = new WebProxy(proxyUrl);
        }
        cws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;

        // ģ����Header
        if (isSimulated)
        {
            cws.Options.SetRequestHeader("x-simulated-trading", "1");
        }

        try
        {
            await cws.ConnectAsync(wsUri, CancellationToken.None);

            // ��������instId��books5
            var subMsg = new
            {
                op = "subscribe",
                args = instIds.Select(id => new { channel = "books5", instId = id }).ToArray()
            };
            var subJson = JsonSerializer.Serialize(subMsg);
            var subBuffer = Encoding.UTF8.GetBytes(subJson);
            await cws.SendAsync(new ArraySegment<byte>(subBuffer), WebSocketMessageType.Text, true, CancellationToken.None);

            var buffer = new byte[4096];
            while (cws.State == WebSocketState.Open)
            {
                var result = await cws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await cws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                try
                {
                    using var doc = JsonDocument.Parse(msg);
                    if (doc.RootElement.TryGetProperty("data", out var dataElem) && dataElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var book in dataElem.EnumerateArray())
                        {
                            if (!book.TryGetProperty("instId", out var instIdElem)) continue;
                            var instId = instIdElem.GetString() ?? "";

                            var ob = new OrderBookLevel();

                            // ��
                            if (levels.TryGetValue("buy", out int buyLevel) &&
                                book.TryGetProperty("bids", out var bidsElem) &&
                                bidsElem.ValueKind == JsonValueKind.Array &&
                                bidsElem.GetArrayLength() >= buyLevel)
                            {
                                var bid = bidsElem[buyLevel - 1];
                                ob.BuyPrice = bid[0].GetString();
                                ob.BuySize = bid[1].GetString();
                            }
                            // ����
                            if (levels.TryGetValue("sell", out int sellLevel) &&
                                book.TryGetProperty("asks", out var asksElem) &&
                                asksElem.ValueKind == JsonValueKind.Array &&
                                asksElem.GetArrayLength() >= sellLevel)
                            {
                                var ask = asksElem[sellLevel - 1];
                                ob.SellPrice = ask[0].GetString();
                                ob.SellSize = ask[1].GetString();
                            }

                            if (!string.IsNullOrEmpty(ob.BuyPrice) || !string.IsNullOrEmpty(ob.SellPrice))
                            {
                                OrderBookLevels[instId] = ob;
                                // ��ѡ�������������
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {instId} ��{levels.GetValueOrDefault("buy", 0)}: {ob.BuyPrice} �� {ob.BuySize} ��{levels.GetValueOrDefault("sell", 0)}: {ob.SellPrice} �� {ob.SellSize}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"������Ϣ�쳣: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket����: {ex.Message}");
        }
    }
}

/// <summary>
/// �洢�̿�ָ������������
/// </summary>
public class OrderBookLevel
{
    public string? BuyPrice { get; set; }
    public string? BuySize { get; set; }
    public string? SellPrice { get; set; }
    public string? SellSize { get; set; }
}
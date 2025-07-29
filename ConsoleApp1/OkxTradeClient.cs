using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// OKX交易客户端，支持模拟盘和实盘下单（现货/合约）
/// </summary>
public class OkxTradeClient : ApiBase
{
    public OkxTradeClient(string apiKey, string secretKey, string passphrase, bool isSimulated = false, string? proxyUrl = null)
        : base(apiKey, secretKey, passphrase, isSimulated, proxyUrl) { }

    /// <summary>
    /// 下现货市价买单
    /// </summary>
    public async Task<string> PlaceSpotMarketBuyOrderAsync(string instId, decimal size)
    {
        var order = new
        {
            instId,
            tdMode = "cash",
            side = "buy",
            ordType = "market",
            sz = size.ToString()
        };
        return await SendOrderAsync(order);
    }

    /// <summary>
    /// 下现货市价卖单
    /// </summary>
    public async Task<string> PlaceSpotMarketSellOrderAsync(string instId, decimal size)
    {
        var order = new
        {
            instId,
            tdMode = "cash",
            side = "sell",
            ordType = "market",
            sz = size.ToString()
        };
        return await SendOrderAsync(order);
    }

    /// <summary>
    /// 下合约市价买单
    /// </summary>
    public async Task<string> PlaceSwapMarketBuyOrderAsync(string instId, decimal size, string posSide = "long")
    {
        var order = new
        {
            instId,
            tdMode = "cross",
            side = "buy",
            ordType = "market",
            posSide,
            sz = size.ToString()
        };
        return await SendOrderAsync(order);
    }

    /// <summary>
    /// 下合约市价卖单
    /// </summary>
    public async Task<string> PlaceSwapMarketSellOrderAsync(string instId, decimal size, string posSide = "short")
    {
        var order = new
        {
            instId,
            tdMode = "cross",
            side = "sell",
            ordType = "market",
            posSide,
            sz = size.ToString()
        };
        return await SendOrderAsync(order);
    }

    private async Task<string> SendOrderAsync(object order)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var method = "POST";
        var requestPath = "/api/v5/trade/order";
        var body = JsonSerializer.Serialize(order);
        var sign = Sign(SecretKey, timestamp, method, requestPath, body);

        var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + requestPath);
        request.Headers.Add("OK-ACCESS-KEY", ApiKey);
        request.Headers.Add("OK-ACCESS-SIGN", sign);
        request.Headers.Add("OK-ACCESS-TIMESTAMP", timestamp);
        request.Headers.Add("OK-ACCESS-PASSPHRASE", Passphrase);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await HttpClient.SendAsync(request);
        var respContent = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"请求失败: {response.StatusCode} 内容: {respContent}");
        }
        return respContent;
    }

    private static string Sign(string secret, string timestamp, string method, string requestPath, string body)
    {
        var prehash = timestamp + method.ToUpper() + requestPath + body;
        var key = Encoding.UTF8.GetBytes(secret);
        var msg = Encoding.UTF8.GetBytes(prehash);
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(msg);
        return Convert.ToBase64String(hash);
    }
}
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net;
using System.IO;

/// <summary>
/// OKX交易客户端，支持模拟盘和实盘下单（现货/合约）
/// </summary>
public class OkxTradeClient : BaseAPI, ITradeClient
{
    public OkxTradeClient(string apiKey, string secretKey, string passphrase, bool isSimulated = false, string? proxyUrl = null)
        : base(apiKey, secretKey, passphrase, isSimulated, proxyUrl) { }

    // 实现 ITradeClient 接口方法
    public async Task BuySpotAsync(string instId, double quantity, double price)
    {
        // price 参数未用，接口要求保留
        await PlaceSpotMarketBuyOrderAsync(instId, (decimal)quantity);
    }

    public async Task SellSpotAsync(string instId, double quantity, double price)
    {
        await PlaceSpotMarketSellOrderAsync(instId, (decimal)quantity);
    }

    public async Task BuySwapAsync(string instId, double quantity, double price)
    {
        await PlaceSwapMarketBuyOrderAsync(instId, (decimal)quantity, "long");
    }

    public async Task SellSwapAsync(string instId, double quantity, double price)
    {
        await PlaceSwapMarketSellOrderAsync(instId, (decimal)quantity, "short");
    }

    // 买入平空
    public async Task CloseShortSwapAsync(string instId, double quantity, double price)
    {
        await PlaceSwapMarketBuyOrderAsync(instId, (decimal)quantity, "short");
    }

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
            tgtCcy = "base_ccy",
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
            tgtCcy = "base_ccy",
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

public abstract class BaseAPI
{
    protected readonly string ApiKey;
    protected readonly string SecretKey;
    protected readonly string Passphrase;
    protected readonly HttpClient HttpClient;
    protected readonly string BaseUrl = "https://www.okx.com";
    protected bool IsSimulated { get; }

    // 日志相关
    protected bool EnableLog { get; }
    protected bool LogToFile { get; }
    protected string? LogFilePath { get; }

    protected BaseAPI(
        string apiKey,
        string secretKey,
        string passphrase,
        bool isSimulated = false,
        string? proxyUrl = null,
        bool enableLog = false,
        bool logToFile = false,
        string? logFilePath = null)
    {
        ApiKey = apiKey;
        SecretKey = secretKey;
        Passphrase = passphrase;
        IsSimulated = isSimulated;
        EnableLog = enableLog;
        LogToFile = logToFile;
        LogFilePath = logFilePath;

        var handler = new HttpClientHandler();
        if (!string.IsNullOrEmpty(proxyUrl))
        {
            handler.Proxy = new WebProxy(proxyUrl);
            handler.UseProxy = true;
        }
        HttpClient = new HttpClient(handler);

        if (isSimulated)
        {
            HttpClient.DefaultRequestHeaders.Add("x-simulated-trading", "1");
        }
    }

    public virtual void Log(string message, LogLevel level = LogLevel.Info)
    {
        if (EnableLog)
        {
            Console.WriteLine($"[{level}] {message}");
            if (LogToFile && !string.IsNullOrEmpty(LogFilePath))
            {
                File.AppendAllText(LogFilePath, $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}][{level}] {message}{System.Environment.NewLine}");
            }
        }
    }
}
using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// OKX���׿ͻ��ˣ�֧��ģ���̺�ʵ���µ����ֻ�/��Լ��
/// </summary>
public class OkxTradeClient
{
    private readonly string _apiKey;
    private readonly string _secretKey;
    private readonly string _passphrase;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    /// <summary>
    /// ��ʼ�����׿ͻ���
    /// </summary>
    /// <param name="apiKey">API Key</param>
    /// <param name="secretKey">Secret Key</param>
    /// <param name="passphrase">Passphrase</param>
    /// <param name="isSimulated">�Ƿ�Ϊģ���̣�true=ģ���̣�false=ʵ�̣�</param>
    /// <param name="proxyUrl">��ѡ�������ַ���� http://127.0.0.1:29290��</param>
    public OkxTradeClient(string apiKey, string secretKey, string passphrase, bool isSimulated = false, string? proxyUrl = null)
    {
        _apiKey = apiKey;
        _secretKey = secretKey;
        _passphrase = passphrase;
        _baseUrl = "https://www.okx.com";

        var handler = new HttpClientHandler();
        if (!string.IsNullOrEmpty(proxyUrl))
        {
            handler.Proxy = new WebProxy(proxyUrl);
            handler.UseProxy = true;
        }
        _httpClient = new HttpClient(handler);

        // ģ�����µ���Ӵ�Header
        if (isSimulated)
        {
            _httpClient.DefaultRequestHeaders.Add("x-simulated-trading", "1");
        }
    }

    /// <summary>
    /// ���ֻ��м���
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
    /// ���ֻ��м�����
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
    /// �º�Լ�м���
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
    /// �º�Լ�м�����
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
        var sign = Sign(_secretKey, timestamp, method, requestPath, body);

        var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + requestPath);
        request.Headers.Add("OK-ACCESS-KEY", _apiKey);
        request.Headers.Add("OK-ACCESS-SIGN", sign);
        request.Headers.Add("OK-ACCESS-TIMESTAMP", timestamp);
        request.Headers.Add("OK-ACCESS-PASSPHRASE", _passphrase);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var respContent = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"����ʧ��: {response.StatusCode} ����: {respContent}");
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
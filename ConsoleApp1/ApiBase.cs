using System.Net;
using System.Net.Http;

public abstract class ApiBase
{
    protected readonly string ApiKey;
    protected readonly string SecretKey;
    protected readonly string Passphrase;
    protected readonly HttpClient HttpClient;
    protected readonly string BaseUrl = "https://www.okx.com";

    /// <summary>
    /// ÊÇ·ñÎªÄ£ÄâÅÌ
    /// </summary>
    protected bool IsSimulated { get; }

    protected ApiBase(string apiKey, string secretKey, string passphrase, bool isSimulated = false, string? proxyUrl = null)
    {
        ApiKey = apiKey;
        SecretKey = secretKey;
        Passphrase = passphrase;
        IsSimulated = isSimulated;

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
}
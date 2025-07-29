using System.Net;
using System.Net.Http;

public abstract class BaseApi
{
    protected readonly string ApiKey;
    protected readonly string SecretKey;
    protected readonly string Passphrase;
    protected readonly HttpClient HttpClient;
    protected readonly string BaseUrl = "https://www.okx.com";

    /// <summary>
    /// ??¡¤?????????
    /// </summary>
    protected bool IsSimulated { get; }

    protected BaseApi(string apiKey, string secretKey, string passphrase, bool isSimulated = false, string? proxyUrl = null)
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
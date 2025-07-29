using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;

public abstract class WebSocketBase<TValue>
{
    /// <summary>
    /// 公共字典，供外部读写
    /// </summary>
    public static ConcurrentDictionary<string, TValue> SharedDict { get; } = new();

    protected bool IsSimulated { get; }
    protected string? ProxyUrl { get; }

    protected WebSocketBase(bool isSimulated = false, string? proxyUrl = null)
    {
        IsSimulated = isSimulated;
        ProxyUrl = proxyUrl;
    }

    /// <summary>
    /// 创建已配置的 WebSocket 客户端
    /// </summary>
    protected ClientWebSocket CreateWebSocket()
    {
        var ws = new ClientWebSocket();
        if (!string.IsNullOrEmpty(ProxyUrl))
            ws.Options.Proxy = new WebProxy(ProxyUrl);
        ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        if (IsSimulated)
            ws.Options.SetRequestHeader("x-simulated-trading", "1");
        return ws;
    }
}   
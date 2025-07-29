using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.WebSockets;

public abstract class BaseWebSocket<TValue>
{
    /// <summary>
    /// 公共字典，供外部读写
    /// </summary>
    public static ConcurrentDictionary<string, TValue> SharedDict { get; } = new();

    protected bool IsSimulated { get; }
    protected string? ProxyUrl { get; }
    protected bool EnableLog { get; }
    protected bool LogToFile { get; }
    protected string? LogFilePath { get; }

    protected BaseWebSocket(bool isSimulated = false, string? proxyUrl = null, bool enableLog = false, bool logToFile = false, string? logFilePath = null)
    {
        IsSimulated = isSimulated;
        ProxyUrl = proxyUrl;
        EnableLog = enableLog;
        LogToFile = logToFile;
        LogFilePath = logFilePath;
    }

    /// <summary>
    /// 日志输出，子类可重写
    /// </summary>
    protected virtual void Log(string message)
    {
        if (EnableLog)
        {
            Console.WriteLine(message);
            if (LogToFile && !string.IsNullOrEmpty(LogFilePath))
            {
                File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
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
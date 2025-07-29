using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.WebSockets;

public abstract class BaseWebSocket<TValue>
{
    /// <summary>
    /// �����ֵ䣬���ⲿ��д
    /// </summary>
    public static ConcurrentDictionary<string, TValue> SharedDict { get; } = new();

    protected bool IsSimulated { get; }
    protected string? ProxyUrl { get; }
    protected bool EnableLog { get; }
    protected bool LogToFile { get; }
    protected string? LogFilePath { get; }
    protected LogLevel MinLogLevel { get; }
    protected LogLevel MaxLogLevel { get; }

    protected BaseWebSocket(
        bool isSimulated = false,
        string? proxyUrl = null,
        bool enableLog = false,
        bool logToFile = false,
        string? logFilePath = null,
        LogLevel minLogLevel = LogLevel.Info,
        LogLevel maxLogLevel = LogLevel.Error)
    {
        IsSimulated = isSimulated;
        ProxyUrl = proxyUrl;
        EnableLog = enableLog;
        LogToFile = logToFile;
        LogFilePath = logFilePath;
        MinLogLevel = minLogLevel;
        MaxLogLevel = maxLogLevel;
    }

    /// <summary>
    /// ��־������������д
    /// </summary>
    protected virtual void Log(string message, LogLevel level = LogLevel.Info)
    {
        if (!EnableLog) return;
        if (level < MinLogLevel || level > MaxLogLevel) return;

        Console.WriteLine($"[{level}] {message}");
        if (LogToFile && !string.IsNullOrEmpty(LogFilePath))
        {
            File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][{level}] {message}{Environment.NewLine}");
        }
    }

    /// <summary>
    /// ���������õ� WebSocket �ͻ���
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
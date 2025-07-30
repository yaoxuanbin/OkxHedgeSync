public class TradeRecordConfig
{
    public bool Enable { get; set; } = true;
    public bool LogToFile { get; set; } = true;
    public string LogFilePath { get; set; } = "trade_record.log";
}

public class OkxSettings
{
    public string ApiKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Passphrase { get; set; } = "";
    public bool IsSimulated { get; set; }
    public string? ProxyUrl { get; set; }
    public WebSocketLogGroup WebSocketLogs { get; set; } = new();
    public LogThrottleConfig LogThrottle { get; set; } = new();
    public TradeRecordConfig TradeRecord { get; set; } = new();
}

public class LogThrottleConfig
{
    public bool Enable { get; set; } = true;
    public int IntervalSeconds { get; set; } = 1;
}

public class WebSocketLogGroup
{
    public WebSocketLogSettings SpotPrice { get; set; } = new();
    public WebSocketLogSettings PositionAccount { get; set; } = new();
    public WebSocketLogSettings OrderBook { get; set; } = new();
}

public class WebSocketLogSettings
{
    public bool EnableLog { get; set; }
    public bool LogToFile { get; set; }
    public string? LogFilePath { get; set; }
    public string MinLevel { get; set; }
    public string MaxLevel { get; set; }
}
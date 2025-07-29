public class OkxSettings
{
    public string ApiKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Passphrase { get; set; } = "";
    public bool IsSimulated { get; set; }
    public string? ProxyUrl { get; set; }
    public WebSocketLogGroup WebSocketLogs { get; set; } = new();
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
}
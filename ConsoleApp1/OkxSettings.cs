public class OkxSettings
{
    public string ApiKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Passphrase { get; set; } = "";
    public bool IsSimulated { get; set; }
    public string? ProxyUrl { get; set; }
}
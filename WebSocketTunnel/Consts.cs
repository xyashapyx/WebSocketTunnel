namespace WebSocketTunnel;

public class Consts
{
    public const string CloseCommand = "Close";
    public const string NewConnection = "New";
    public const string ResponseToStream = "Response";
    public const int CommandSizeBytes = 32;
    public const int TcpPackageSize = 524288;
    
    public const string Http = "http";
    public const string Https = "https";

    public const string Http11 = "1.1";

    public const string ConfigFileName = "Config.json";
}
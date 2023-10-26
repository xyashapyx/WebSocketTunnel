namespace WebSocketTunnel;

public class Consts
{
    public const byte CloseCommand = 22; //"Close";
    public const byte NewConnection = 23; //"New";
    public const byte ResponseToStream = 24;// "Response";
    public const byte CommandSplitter = 21;
    
    public const int CommandSizeBytes = 32;
    public const int TcpPackageSize = 81920;
    
    public const string Http = "http";
    public const string Https = "https";

    public const string Http11 = "1.1";

    public const string ConfigFileName = "Config.json";
}
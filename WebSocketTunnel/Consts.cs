using System.Text;

namespace WebSocketTunnel;

public static class Consts
{
    public const string CloseCommand = "Close";
    public static readonly Memory<byte> CloseCommandBytes = new Memory<byte>(Encoding.ASCII.GetBytes(CloseCommand));

    public const string NewConnection = "New";
    public static readonly Memory<byte> NewConnectionBytes = new Memory<byte>(Encoding.ASCII.GetBytes(NewConnection));

    public const string ResponseToStream = "Response";
    public static readonly Memory<byte> ResponseToStreamBytes = new Memory<byte>(Encoding.ASCII.GetBytes(ResponseToStream));

    public const int CommandSizeBytes = 32;
    public const int TcpPackageSize = 524288;
    
    public const string Http = "http";
    public const string Https = "https";

    public const string Http11 = "1.1";

    public const string ConfigFileName = "Config.json";
}
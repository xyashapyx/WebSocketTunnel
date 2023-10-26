using System.ComponentModel;

namespace WebSocketTunnel.DTO;

public class Config
{
    public string LocalVmIp { get; set; }
    public WsConfig WsConfig { get; set; }
    public TcpConfig TcpConfig { get; set; }
}

public class WsConfig
{
    public WsMode Mode { get; set; }
    public string WsSeccurity { get; set; }
    public int WsPort { get; set; }
    public string TcpVersion { get; set; }
}

public class TcpConfig
{
    public string TargetVmIp { get; set; }
    public int[] ListeningPorts { get; set; }
}

public enum WsMode
{
    [Description("Server")]
    Server,
    [Description("Client")]
    Client
}
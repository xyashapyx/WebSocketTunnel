// See https://aka.ms/new-console-template for more information

using NLog;
using WebSocketTunnel;
using WebSocketTunnel.DTO;

var logger = LogManager.GetCurrentClassLogger();

logger.Info("Started new instance");

var configServer = new Config()
{
    LocalVmIp = "127.0.0.1",
    TcpConfig = new TcpConfig()
    {
        ListeningPorts = new int[] { 443 },
        TargetVmIp = "10.171.81.29",
    },
    WsConfig = new WsConfig()
    {
        Mode = WsMode.Server,
        TcpVersion = "1.1",
        WsPort = 6666,
        WsSeccurity = "http",
        WsServerIp = "127.0.0.1",
    }
};
var configClient = new Config()
{
    LocalVmIp = "127.0.0.1",
    TcpConfig = new TcpConfig()
    {
        ListeningPorts = new int[] { 8081 },
        TargetVmIp = "10.171.81.29",
    },
    WsConfig = new WsConfig()
    {
        Mode = WsMode.Client,
        TcpVersion = "1.1",
        WsPort = 6666,
        WsSeccurity = "http",
        WsServerIp = "127.0.0.1",
    }
};

StartServer(configServer);
StartClient(configClient);

//if (config.WsConfig.Mode == WsMode.Server)
//{
//    StartServer(config);
//}
//else
//{
//    StartClient(config);
//}

Console.ReadLine();

void StartServer(Config config)
{
    var tcpMSP = new TcpConnector(config.TcpConfig.TargetVmIp, config.TcpConfig.ListeningPorts, config.LocalVmIp);
    var wsMsp = new WsServer(tcpMSP, Consts.TcpPackageSize);
    Task.Run(()=> wsMsp.Start(config.WsConfig.WsPort, config.LocalVmIp, config.WsConfig.WsSeccurity));
}

void StartClient(Config config)
{
    var tcpTenant = new TcpConnector(config.TcpConfig.TargetVmIp, config.TcpConfig.ListeningPorts, config.LocalVmIp);
    var wsTenant = new WsClient(tcpTenant, Consts.TcpPackageSize);
    Task.Run(()=> wsTenant.Start(config.WsConfig.WsPort, config.WsConfig.WsServerIp, config.WsConfig.WsSeccurity, config.WsConfig.TcpVersion));
}
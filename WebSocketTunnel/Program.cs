// See https://aka.ms/new-console-template for more information

using NLog;
using WebSocketTunnel;
using WebSocketTunnel.DTO;

var logger = LogManager.GetCurrentClassLogger();

logger.Info("Started new instance");


//StartBoth();
var config = ConfigService.ReadConfig();

if (config.WsConfig.Mode == WsMode.Server)
{
    StartServer(config);
}
else
{
    StartClient(config);
}

Console.ReadLine();

void StartServer(Config config)
{
    var tcpMSP = new TcpConnector(config.TcpConfig.TargetVmIp, config.TcpConfig.ListeningPorts, config.LocalVmIp);
    var wsMsp = new WsServer(tcpMSP, Consts.TcpPackageSize);
    Task.Run(()=> wsMsp.Start(config.WsConfig.WsPort, config.WsConfig.WsServerIp, config.WsConfig.WsSeccurity));
}

void StartClient(Config config)
{
    var tcpTenant = new TcpConnector(config.TcpConfig.TargetVmIp, config.TcpConfig.ListeningPorts, config.LocalVmIp);
    var wsTenant = new WsClient(tcpTenant, Consts.TcpPackageSize);
    Task.Run(()=> wsTenant.Start(config.WsConfig.WsPort, config.WsConfig.WsServerIp, config.WsConfig.WsSeccurity, config.WsConfig.TcpVersion));
}

void StartBoth()
{
    var serverConfig = new Config
    {
        LocalVmIp = "127.0.0.1",
        WsConfig = new WsConfig
        {
            Mode = WsMode.Server,
            TcpVersion = "1.1",
            WsPort = 6666,
            WsSeccurity = "http",
            WsServerIp = "localhost"
        },
        TcpConfig = new TcpConfig
        {
            ListeningPorts = new []{9779},
            TargetVmIp = "192.168.111.20"
        }
    };
    var clientConfig = new Config
    {
        LocalVmIp = "127.0.0.1",
        WsConfig = new WsConfig{
            Mode = WsMode.Client,
            TcpVersion = "1.1",
            WsPort = 6666,
            WsSeccurity = "http",
            WsServerIp = "localhost"
        },
        TcpConfig = new TcpConfig
        {
            ListeningPorts = new []{9669},
            TargetVmIp = "192.168.111.20"
        }
    };
    StartServer(serverConfig);
    StartClient(clientConfig);
}
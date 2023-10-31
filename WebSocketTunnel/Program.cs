// See https://aka.ms/new-console-template for more information

using NLog;
using WebSocketTunnel;
using WebSocketTunnel.DTO;

var logger = LogManager.GetCurrentClassLogger();

logger.Info("Started new instance");

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
    Task.Run(() => wsMsp.Start(config.WsConfig.WsPort, config.LocalVmIp, config.WsConfig.WsSeccurity));
}

void StartClient(Config config)
{
    var tcpTenant = new TcpConnector(config.TcpConfig.TargetVmIp, config.TcpConfig.ListeningPorts, config.LocalVmIp);
    var wsTenant = new WsClient(tcpTenant, Consts.TcpPackageSize);
    Task.Run(() => wsTenant.Start(config.WsConfig.WsPort, config.WsConfig.WsServerIp, config.WsConfig.WsSeccurity, config.WsConfig.TcpVersion));
}
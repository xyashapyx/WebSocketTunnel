// See https://aka.ms/new-console-template for more information

using NLog;
using WebSocketTunnel;

var logger = LogManager.GetCurrentClassLogger();

logger.Info("Started new");
string localhostIp = "127.0.0.1";

var tcpMSP = new TcpConnector("92.168.111.20", new List<int>{9669}, localhostIp);
var tcpTenant = new TcpConnector("192.168.111.20", new List<int>{5201}, localhostIp);

var wsMsp = new WsServer(tcpMSP, Consts.TcpPackageSize);
var wsTenant = new WsClient(tcpTenant, Consts.TcpPackageSize);

Task.Run(()=> wsMsp.Start(6666, localhostIp, "https"));
Task.Run(()=> wsTenant.Start(6666, "localhost", "https", "2.0"));

Console.ReadLine();
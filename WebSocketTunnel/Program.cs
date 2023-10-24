// See https://aka.ms/new-console-template for more information

using System.Text;
using WebSocketTunnel;

string localhostIp = "127.0.0.1";

var tcpMSP = new TcpConnector("192.168.111.20", new List<int>{80}, localhostIp);
var tcpTenant = new TcpConnector("192.168.111.20", new List<int>{9669}, localhostIp);

var wsMsp = new WsServer(tcpMSP, Consts.TcpPackageSize);
var wsTenant = new WsClient(tcpTenant, Consts.TcpPackageSize);

Task.Run(()=> wsMsp.Start(6666, localhostIp));
Task.Run(()=> wsTenant.Start(6666, localhostIp));

Console.ReadLine();
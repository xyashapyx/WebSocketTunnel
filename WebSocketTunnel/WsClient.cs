using System.Buffers;
using System.Net;
using System.Net.WebSockets;

namespace WebSocketTunnel;

public class WsClient: WsBase
{
    public WsClient(TcpConnector tcpConnector, int packageSize) : base(tcpConnector, packageSize)
    {
    }
    
    public async Task Start(int wsPort, string serverIp, string httpPrefix = Consts.Http, string httpVersion = Consts.Http11)
    {
        //TODO:Add reconnect
        var ws = new ClientWebSocket();
        WebSocket = ws;

        using SocketsHttpHandler handler = new();
        ws.Options.HttpVersion = httpVersion == Consts.Http11? HttpVersion.Version11: HttpVersion.Version20;
        ws.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;

        string wsType = httpPrefix == Consts.Http ? "ws" : "wss";
        string url = $"{wsType}://{serverIp}:{wsPort}/ws";
        Console.WriteLine($"Connecting to {url}");
        await ws.ConnectAsync(new Uri(url), new HttpMessageInvoker(handler), CancellationToken.None);
    }
}
using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using NLog;

namespace WebSocketTunnel;

public class WsServer: WsBase
{
    private static Logger _logger = LogManager.GetCurrentClassLogger();

    public WsServer(TcpConnector tcpConnector, int packageSize)
        :base(tcpConnector, packageSize, _logger)
    {
    }
    
    public async Task Start(int wsPort, string serverIp, string httpPrefix = "http")
    {
        var builder = WebApplication.CreateBuilder();
        string wsUrl = $"{httpPrefix}://{serverIp}:{wsPort}";
        builder.WebHost.UseUrls(wsUrl);
        var app = builder.Build();
        var wsOptions = new WebSocketOptions();
        wsOptions.AllowedOrigins.Add($"{httpPrefix}://{serverIp}");
        app.UseWebSockets(wsOptions);
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/ws")//192.168.111.10:5555/ws
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    _logger.Info($"Wating for connection on {wsUrl}");
                    WebSocket = await context.WebSockets.AcceptWebSocketAsync();
                    _logger.Info("Connected");
                    while (WebSocket.State == WebSocketState.Open)
                        await Task.Delay(1000);
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
            }
            else
            {
                await next(context);
            }
        });
        await app.RunAsync();
    }
}
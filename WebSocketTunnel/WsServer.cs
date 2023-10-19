using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace WebSocketTunnel;

public class WsServer: WsBase
{
    public WsServer(TcpConnector tcpConnector, int packageSize)
        :base(tcpConnector, packageSize)
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
            if (context.Request.Path == "/ws")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    Console.WriteLine($"Wating for connection on {wsUrl}");
                    WebSocket = await context.WebSockets.AcceptWebSocketAsync();
                    Console.WriteLine("Connected");
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
    }
}
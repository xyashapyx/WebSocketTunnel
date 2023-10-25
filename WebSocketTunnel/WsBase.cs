using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using NLog;

namespace WebSocketTunnel;

public abstract class WsBase
{
    private WebSocket _webSocket;
    protected int PackageSize = 32768;
    private readonly Logger _logger;
    protected TcpConnector TcpConnector;

    public WsBase(TcpConnector tcpConnector, int packageSize, Logger logger)
    {
        TcpConnector = tcpConnector;
        PackageSize = packageSize;
        _logger = logger;
    }

    protected WebSocket WebSocket
    {
        get => _webSocket;
        set
        {
            _webSocket = value;
            TcpConnector.InnitWs(this);
            Task.Run(() => ReceiveMessage());
        }
    }

    public bool IsConnected => WebSocket != null && WebSocket.State == WebSocketState.Open;

    public async Task SendCloseCommandAsync(int remoteStreamId)
    {
        if (WebSocket == null || WebSocket.State != WebSocketState.Open)
        {
            _logger.Warn($"Cannot close stream {remoteStreamId}, WS disconnected");
        }
        string command = $"{Consts.CloseCommand}:{remoteStreamId}:";
        await WebSocket.SendAsync(Encoding.ASCII.GetBytes(command), WebSocketMessageType.Text, true,
            CancellationToken.None);
    }

    public async Task RespondToMessageAsync(int localStreamId, int remoteStreamId, Memory<byte> data)
    {
        string command = $"{Consts.ResponseToStream}:{remoteStreamId}:{localStreamId}:";
        await SendBytesAsync(command, data);
    }
    
    public async Task InnitConnectionAsync(int localStreamId, int localPort, Memory<byte> data)
    {
        string command = $"{Consts.NewConnection}:{localPort}:{localStreamId}:";
        await SendBytesAsync(command, data);
    }

    //TODO: I expect that first Consts.CommandSizeBytes is free and can hold commad.
    //TODO: Can we do better?
    private async Task SendBytesAsync(string command, Memory<byte> data)
    {
        _logger.Info(command);
        var encodedCommand = Encoding.ASCII.GetBytes(command);
        encodedCommand.CopyTo(data);
        await WebSocket.SendAsync(data, WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    protected async Task ReceiveMessage()
    {
        //TODO:Work in multithreading
        while (true)
        {
            try
            {
                if (WebSocket == null || WebSocket.State != WebSocketState.Open)
                {
                    await Task.Delay(1000);
                    continue;
                }
                //TODO:remove
                await Task.Delay(1000);
                Memory<byte> buffer = ArrayPool<byte>.Shared.Rent(PackageSize);

                var message = await WebSocket.ReceiveAsync(buffer, CancellationToken.None);
                if (message.MessageType == WebSocketMessageType.Close)
                {
                    await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    WebSocket = null;
                    break;
                }
                if (message.MessageType == WebSocketMessageType.Text)
                {
                    await ProcessCommand(buffer);
                    continue;
                }

                int packageSize = message.Count;
                await ProcessData(buffer, packageSize);
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
        }
    }

    private async Task ProcessCommand(Memory<byte> buffer)
    {
        int streamId = 0;
        var command = Encoding.ASCII.GetString(buffer[..Consts.CommandSizeBytes].Span);
        _logger.Info($"Got command {command}");
        if (command.StartsWith(Consts.CloseCommand))
        {
            //TODO: can we use bytes instead?
            streamId = int.Parse(command.Split(':')[1]);
            TcpConnector.CloseStream(streamId);
            return;
        }
        
        throw new NotImplementedException();
    }

    private async Task ProcessData(Memory<byte> buffer, int size)
    {
        string command = Encoding.ASCII.GetString(buffer[..Consts.CommandSizeBytes].ToArray());
        _logger.Info($"Got command {command}");
        if (command.StartsWith(Consts.NewConnection))
        {
            var splited = command.Split(':');
            int remotePort = int.Parse(splited[1]);
            int remoteStreamId = int.Parse(splited[2]);
            await TcpConnector.EstablishConnectionAsync(remotePort, remoteStreamId, buffer[Consts.CommandSizeBytes..size]);
        }
        else
        if (command.StartsWith(Consts.ResponseToStream))
        {
            var splited = command.Split(':');
            int remoteStreamId = int.Parse(splited[2]);
            int localStreamId = int.Parse(splited[1]);
            await TcpConnector.HandleRespondToStreamAsync(remoteStreamId, localStreamId, buffer[Consts.CommandSizeBytes..size]);
        }
        else
            _logger.Warn($"Wrong Command {command}");
    }
}
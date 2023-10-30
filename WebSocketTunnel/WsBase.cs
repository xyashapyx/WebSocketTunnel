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

    public Task SendCloseCommandAsync(int remoteStreamId)
    {
        if (WebSocket == null || WebSocket.State != WebSocketState.Open)
        {
            _logger.Warn($"Cannot close stream {remoteStreamId}, WS disconnected");
        }
        string command = $"{Consts.CloseCommand}:{remoteStreamId}:";
        return WebSocket.SendAsync(Encoding.ASCII.GetBytes(command), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public ValueTask RespondToMessageAsync(int localStreamId, int remoteStreamId, Memory<byte> data)
    {
        string command = $"{Consts.ResponseToStream}:{remoteStreamId}:{localStreamId}:";
        return SendBytesAsync(command, data);
    }

    public ValueTask InnitConnectionAsync(int localStreamId, int localPort, Memory<byte> data)
    {
        string command = $"{Consts.NewConnection}:{localPort}:{localStreamId}:";
        return SendBytesAsync(command, data);
    }

    //TODO: I expect that first Consts.CommandSizeBytes is free and can hold commad.
    //TODO: Can we do better?
    private ValueTask SendBytesAsync(string command, Memory<byte> data)
    {
        _logger.Info(command);
        var encodedCommand = Encoding.ASCII.GetBytes(command);
        encodedCommand.CopyTo(data);
        return WebSocket.SendAsync(data, WebSocketMessageType.Binary, true, CancellationToken.None);
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
                    await Task.Delay(1000).ConfigureAwait(false);
                    continue;
                }
                //TODO:remove
                Memory<byte> buffer = ArrayPool<byte>.Shared.Rent(PackageSize);

                var message = await WebSocket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                if (message.MessageType == WebSocketMessageType.Close)
                {
                    await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
                    WebSocket = null;
                    break;
                }
                if (message.MessageType == WebSocketMessageType.Text)
                {
                    await ProcessCommand(buffer).ConfigureAwait(false);
                    continue;
                }

                int packageSize = message.Count;
                _logger.Info($"Package size {packageSize}");
                await ProcessData(buffer, packageSize).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
        }
    }

    private async Task ProcessCommand(Memory<byte> buffer)
    {
        var command = Encoding.ASCII.GetString(buffer[..Consts.CommandSizeBytes].Span);
        _logger.Info($"Got command {command}");
        if (command.StartsWith(Consts.CloseCommand))
        {
            //TODO: can we use bytes instead?
            int streamId = int.Parse(command.Split(':')[1]);
            TcpConnector.CloseStream(streamId);
            return;
        }

        throw new NotImplementedException();
    }

    private async Task ProcessData(Memory<byte> buffer, int size)
    {
        string command = Encoding.ASCII.GetString(buffer[..Consts.CommandSizeBytes].Span);
        _logger.Info($"Got command {command}");
        if (command.StartsWith(Consts.NewConnection))
        {
            var splited = command.Split(':');
            int remotePort = int.Parse(splited[1]);
            int remoteStreamId = int.Parse(splited[2]);
            await TcpConnector.EstablishConnectionAsync(remotePort, remoteStreamId, buffer[Consts.CommandSizeBytes..size]).ConfigureAwait(false);
        }
        else
        if (command.StartsWith(Consts.ResponseToStream))
        {
            var splited = command.Split(':');
            int remoteStreamId = int.Parse(splited[2]);
            int localStreamId = int.Parse(splited[1]);
            await TcpConnector.HandleRespondToStreamAsync(remoteStreamId, localStreamId, buffer[Consts.CommandSizeBytes..size]).ConfigureAwait(false);
        }
        else
            _logger.Warn($"Wrong Command {command}");
    }
}
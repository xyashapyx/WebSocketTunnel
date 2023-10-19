using System.Buffers;
using System.Net.WebSockets;
using System.Text;

namespace WebSocketTunnel;

public abstract class WsBase
{
    protected WebSocket WebSocket;
    protected int PackageSize = 32768;
    protected TcpConnector TcpConnector;

    public WsBase(TcpConnector tcpConnector, int packageSize)
    {
        TcpConnector = tcpConnector;
        PackageSize = packageSize;
    }

    public async Task SendCloseCommand(int remoteStreamId)
    {
        if (WebSocket == null || WebSocket.State != WebSocketState.Open)
        {
            Console.WriteLine($"Cannot close stream {remoteStreamId}, WS disconnected");
        }
        string command = $"{Consts.CloseCommand}:{remoteStreamId}";
        await WebSocket.SendAsync(Encoding.ASCII.GetBytes(command), WebSocketMessageType.Text, true,
            CancellationToken.None);
    }

    public async Task RespondToMessage(int localStreamId, int remoteStreamId, Memory<byte> data)
    {
        string command = $"{Consts.ResponseToStream}:{remoteStreamId}:{localStreamId}";
        await SendBytes(command, data);
    }
    
    public async Task InnitConnection(int localStreamId, int localPort, Memory<byte> data)
    {
        string command = $"{Consts.ResponseToStream}:{localPort}:{localStreamId}";
        await SendBytes(command, data);
    }

    //TODO: I expect that first Consts.CommandSizeBytes is free and can hold commad.
    //TODO: Can we do better?
    private async Task SendBytes(string command, Memory<byte> data)
    {
        var encodedCommand = Encoding.ASCII.GetBytes(command);
        encodedCommand.CopyTo(data);
        await WebSocket.SendAsync(data, WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    protected async Task ReceiveMessage()
    {
        //TODO:Work in multithreading
        while (true)
        {
            if (WebSocket == null || WebSocket.State != WebSocketState.Open)
            {
                await Task.Delay(1000);
                continue;
            }
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
    }

    private async Task ProcessCommand(Memory<byte> buffer)
    {
        int streamId = 0;
        var command = Encoding.ASCII.GetString(buffer.Span);
        if (command.StartsWith(Consts.CloseCommand))
        {
            //TODO: can we use bytes instead?
            streamId = int.Parse(command.Split(':').Last());
            TcpConnector.CloseStream(streamId);
            return;
        }
        
        throw new NotImplementedException();
    }

    private async Task ProcessData(Memory<byte> buffer, int size)
    {
        string command = Encoding.ASCII.GetString(buffer[..Consts.CommandSizeBytes].ToArray());
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
            await TcpConnector.RespondToStreamAsync(remoteStreamId, localStreamId, buffer[Consts.CommandSizeBytes..size]);
        }

        Console.WriteLine($"Wrong Command {command}");
    }
}
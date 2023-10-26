using System.Buffers;
using System.Net.WebSockets;
using System.Text;
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
        Memory<byte> command = SerializeCommand(Consts.CloseCommand, remoteStreamId);
        await WebSocket.SendAsync(command, WebSocketMessageType.Text, true,
            CancellationToken.None);
    }

    public async Task RespondToMessageAsync(int localStreamId, int remoteStreamId, Memory<byte> data)
    {
        Memory<byte> command = SerializeCommand(Consts.ResponseToStream, remoteStreamId, localStreamId);
        await SendBytesAsync(command, data);
    }
    
    public async Task InnitConnectionAsync(int localStreamId, int localPort, Memory<byte> data)
    {
        Memory<byte> command = SerializeCommand(Consts.NewConnection, localPort, localStreamId);
        await SendBytesAsync(command, data);
    }

    //TODO: I expect that first Consts.CommandSizeBytes is free and can hold commad.
    //TODO: Can we do better?
    private async Task SendBytesAsync(Memory<byte> command, Memory<byte> data)
    {
        command.CopyTo(data);
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
                //_logger.Info($"Package size {packageSize}");
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
        var command = DeserializeCommand(buffer[..Consts.CommandSizeBytes].Span);
        //_logger.Info($"Got command {command.Item1}");
        if (command.Item1 == Consts.CloseCommand)
        {
            //TODO: can we use bytes instead?
            streamId = command.Item2[0];
            TcpConnector.CloseStream(streamId);
            return;
        }
        
        throw new NotImplementedException();
    }

    private async Task ProcessData(Memory<byte> buffer, int size)
    {
        var command = DeserializeCommand(buffer[..Consts.CommandSizeBytes].Span);
        //_logger.Info($"Got command {command.Item1}");
        if (command.Item1 == Consts.NewConnection)
        {
            int remotePort = command.Item2[0];
            int remoteStreamId = command.Item2[1];
            await TcpConnector.EstablishConnectionAsync(remotePort, remoteStreamId, buffer[Consts.CommandSizeBytes..size]);
        }
        else
        if (command.Item1 == Consts.ResponseToStream)
        {
            int remoteStreamId = command.Item2[1];
            int localStreamId = command.Item2[0];
            await TcpConnector.HandleRespondToStreamAsync(remoteStreamId, localStreamId, buffer[Consts.CommandSizeBytes..size]);
        }
        else
            _logger.Warn($"Wrong Command {command}");
    }
    
    //TODO: move to helper class
    public static Memory<byte> SerializeCommand(byte command, params int[] parameters)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Consts.CommandSizeBytes);
        int position = 0;
        buffer[position++] = command;
        InjectCommandSplitter();

        foreach (var parameter in parameters)
        {
            byte[] parameterBytes = IntArrayConvertor.IntToArr(parameter);
            foreach (var parameterByte in parameterBytes)
            {
                buffer[position++] = parameterByte;
            }
            InjectCommandSplitter();
        }
        
        return buffer;

        void InjectCommandSplitter()
        {
            buffer[position++] = Consts.CommandSplitter;
        }
    }

    public static (byte, int[]) DeserializeCommand(Span<byte> data)
    {
        int position = 0;
        int parameterId = 0;
        byte command = data[position];
        int[] commandParams = new int[2];
        position += 2;
        int startPosition = position;

        while (position < data.Length)
        {
            if (data[position++] == Consts.CommandSplitter)
            {
                commandParams[parameterId++] = IntArrayConvertor.ArrToInt(data[startPosition..(position - 1)]);
                startPosition = position;
                if (parameterId == 2)
                 break;
            }
        }
        
        return new(command, commandParams);
    }
}
using NLog;
using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;

namespace WebSocketTunnel;

public abstract class WsBase
{
    // TODO rewrite with Ascii.FromUtf16 on net8.0 for 10x performance
    private static readonly Encoder s_asciiEncoder = Encoding.ASCII.GetEncoder();

    // TODO rewrite with Ascii.FromUtf16 on net8.0 for beter performance
    private static readonly Decoder m_asciiDecoder = Encoding.ASCII.GetDecoder();

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
    private ValueTask SendBytesAsync(string command, Memory<byte> data)
    {
        _logger.Info(command);

        // TODO rewrite with Ascii.FromUtf16 on net8.0 for 10x performance
        s_asciiEncoder.GetBytes(command, data.Span, false);

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
                    ProcessCommand(buffer);
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

    private void ProcessCommand(Memory<byte> buffer)
    {
        Span<byte> commandSpan = buffer[..Consts.CommandSizeBytes].Span;
        //_logger.Info($"Got command {command}");
        if (commandSpan.StartsWith(Consts.CloseCommandBytes.Span))
        {
            Span<byte> span = commandSpan[(Consts.CloseCommandBytes.Length + 1)..];
            int firstDelimiterIndex = span.IndexOf(Consts.DelimiterByte);

            span = span[..firstDelimiterIndex];
            int streamId = GetInt(in span);

            TcpConnector.CloseStream(streamId);
            return;
        }

        throw new NotImplementedException();
    }

    private async Task ProcessData(Memory<byte> buffer, int size)
    {
        Memory<byte> commandBuffer = buffer[..Consts.CommandSizeBytes];
        //_logger.Info($"Got command {command}");
        if (commandBuffer.Span.StartsWith(Consts.NewConnectionBytes.Span))
        {
            ParseStringBytes(commandBuffer, out int remotePort, out int remoteStreamId);
            await TcpConnector.EstablishConnectionAsync(remotePort, remoteStreamId, buffer[Consts.CommandSizeBytes..size]).ConfigureAwait(false);
        }
        else if (commandBuffer.Span.StartsWith(Consts.ResponseToStreamBytes.Span))
        {
            ParseStringBytes(commandBuffer, out int localStreamId, out int remoteStreamId);
            await TcpConnector.HandleRespondToStreamAsync(remoteStreamId, localStreamId, buffer[Consts.CommandSizeBytes..size]).ConfigureAwait(false);
        }
        else
        {
            string command = Encoding.ASCII.GetString(commandBuffer.Span);
            _logger.Warn($"Wrong Command {command}");
        }


        static void ParseStringBytes(Memory<byte> commandBuffer, out int firstInteger, out int secondInteger)
        {
            Span<byte> span = commandBuffer.Span[(Consts.CloseCommandBytes.Length + 1)..];

            int delimiterIndex = span.IndexOf(Consts.DelimiterByte);
            span = span[..delimiterIndex];

            firstInteger = GetInt(in span);

            delimiterIndex = span.IndexOf(Consts.DelimiterByte) + 1;
            span = span[..delimiterIndex];

            secondInteger = GetInt(in span);
        }
    }


    private static int GetInt(in Span<byte> bytes)
    {
        char[] buffer = ArrayPool<char>.Shared.Rent(bytes.Length);
        try
        {
            Span<char> firstChars = buffer;
            m_asciiDecoder.GetChars(bytes, firstChars, true);

            return int.Parse(firstChars);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }
}
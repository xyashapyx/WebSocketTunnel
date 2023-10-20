using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace WebSocketTunnel;

public class TcpConnector
{
    private readonly string _targetIp;
    private ConcurrentDictionary<int, int> _localToRemoteStreamsMapping = new ConcurrentDictionary<int, int>();
    private ConcurrentDictionary<int, int> _remoteToLocalStreamsMapping = new ConcurrentDictionary<int, int>();
    private ConcurrentDictionary<int, NetworkStream> _streams = new();

    //TODO: innit this
    private WsBase _wsBase;

    public TcpConnector(string targetIp)
    {
        _targetIp = targetIp;
    }
    
    public void CloseStream(int streamId)
    {
        if (_streams.ContainsKey(streamId))
        {
            _streams[streamId]?.Close();
            _streams.TryRemove(streamId, out _);
            _remoteToLocalStreamsMapping.TryRemove(_localToRemoteStreamsMapping[streamId], out _);
            _localToRemoteStreamsMapping.TryRemove(streamId, out _);
        }
        else
        {
            Console.WriteLine($"Did not find Stream {streamId}");
        }
    }

    public async Task EstablishConnectionAsync(int remotePort, int remoteStreamId, Memory<byte> memory)
    {
        //TODO: I assume that source port is same as target port here
        //Need to change for prod
        var forwardClient = new TcpClient();
        await forwardClient.ConnectAsync(IPAddress.Parse(_targetIp), remotePort).ConfigureAwait(false);
        var clientStream = forwardClient.GetStream();
        _streams.TryAdd(clientStream.GetHashCode(), clientStream);
        _localToRemoteStreamsMapping.TryAdd(clientStream.GetHashCode(), remoteStreamId);
        _remoteToLocalStreamsMapping.TryAdd(remoteStreamId, clientStream.GetHashCode());
        await clientStream.WriteAsync(memory);

        Task.Run(() => StartReadingFromStream(clientStream));
        //TODO: read response somehow
    }

    public async Task RespondToStreamAsync(int remoteStreamId, int localStreamId, Memory<byte> memory)
    {
        if(!_streams.TryGetValue(localStreamId, out  var stream))
            Console.WriteLine($"Cannot find stream {localStreamId}");
        _localToRemoteStreamsMapping.TryAdd(localStreamId, remoteStreamId);
        _remoteToLocalStreamsMapping.TryAdd(remoteStreamId, localStreamId);
        await stream.WriteAsync(memory);
    }

    private async Task StartListeningForConnections(int listeningPort, string localIp)
    {
        var localServer = new TcpListener(new IPEndPoint(IPAddress.Parse(localIp), listeningPort));
        localServer.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.AcceptConnection, false);
        localServer.Start();
        var localServerConnection = await localServer.AcceptTcpClientAsync().ConfigureAwait(false);
        var clientStream = localServerConnection.GetStream();
        _streams.TryAdd(clientStream.GetHashCode(), clientStream);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Consts.TcpPackageSize);
        int bytesRead = await clientStream.ReadAsync(buffer, Consts.CommandSizeBytes, Consts.TcpPackageSize-Consts.CommandSizeBytes).ConfigureAwait(false);
        await WaitToWsReadyAsync();

        await _wsBase.InnitConnectionAsync(clientStream.GetHashCode(), listeningPort, buffer[..(bytesRead + Consts.CommandSizeBytes)]);
        
        Task.Run(() => StartReadingFromStream(clientStream));
    }

    private async Task StartReadingFromStream(NetworkStream networkStream)
    {
        int localStreamHashCode = networkStream.GetHashCode();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Consts.TcpPackageSize);
        while (_streams.ContainsKey(localStreamHashCode))
        {
            int bytesRead = await networkStream.ReadAsync(buffer, Consts.CommandSizeBytes, Consts.TcpPackageSize-Consts.CommandSizeBytes).ConfigureAwait(false);
            await WaitToWsReadyAsync();
            
            if (bytesRead == 0)
            {
                await _wsBase.SendCloseCommandAsync(_localToRemoteStreamsMapping[localStreamHashCode]);
                Console.WriteLine($"Sending close stream {_localToRemoteStreamsMapping[localStreamHashCode]}");
                return;
            }

            await _wsBase.RespondToMessageAsync(localStreamHashCode, _localToRemoteStreamsMapping[localStreamHashCode],
                buffer[..(bytesRead + Consts.CommandSizeBytes)]);
        }
    }

    private async Task WaitToWsReadyAsync()
    {
        if (_wsBase == null || !_wsBase.IsConnected)
        {
            Console.WriteLine("Ws is not connected, waiting");
            //TODO: wait in while cycle
            await Task.Delay(1000);
        }
    }
}
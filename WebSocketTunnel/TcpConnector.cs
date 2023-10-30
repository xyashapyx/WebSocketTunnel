using NLog;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace WebSocketTunnel;

public class TcpConnector
{
    private readonly string _targetIp;
    private ConcurrentDictionary<int, int> _localToRemoteStreamsMapping = new();
    private ConcurrentDictionary<int, int> _remoteToLocalStreamsMapping = new();
    private ConcurrentDictionary<int, NetworkStream> _streams = new();
    private static Logger _logger = LogManager.GetCurrentClassLogger();

    //TODO: innit this
    private WsBase _wsBase;

    public TcpConnector(string targetIp, IEnumerable<int> listeningPort, string localIp)
    {
        _targetIp = targetIp;
        //TODO: extract method
        foreach (var localPort in listeningPort)
        {
            Task.Run(() => StartListeningForConnections(localPort, localIp));
        }
    }

    public void CloseStream(int remoteStreamId)
    {
        //TODO: wait for remote stream
        if (_remoteToLocalStreamsMapping.TryGetValue(remoteStreamId, out int localStreamId))
        {
            _streams[localStreamId]?.Close();
            _streams.TryRemove(localStreamId, out _);
            _remoteToLocalStreamsMapping.TryRemove(remoteStreamId, out _);
            _localToRemoteStreamsMapping.TryRemove(localStreamId, out _);
        }
        else
        {
            _logger.Warn($"Did not find remote Stream {remoteStreamId}");
        }
    }

    public async Task EstablishConnectionAsync(int remotePort, int remoteStreamId, Memory<byte> memory)
    {
        //TODO: I assume that source port is same as target port here
        //Need to change for prod
        var forwardClient = new TcpClient(); // dispose?
        await forwardClient.ConnectAsync(IPAddress.Parse(_targetIp), remotePort).ConfigureAwait(false);
        var clientStream = forwardClient.GetStream();
        int localStreamHashCode = clientStream.GetHashCode();
        _streams.TryAdd(localStreamHashCode, clientStream);
        _localToRemoteStreamsMapping.TryAdd(localStreamHashCode, remoteStreamId);
        _remoteToLocalStreamsMapping.TryAdd(remoteStreamId, localStreamHashCode);
        await clientStream.WriteAsync(memory).ConfigureAwait(false);

        Task.Run(() => StartReadingFromStream(clientStream));
        //TODO: read response somehow
    }

    public async Task HandleRespondToStreamAsync(int remoteStreamId, int localStreamHashCode, Memory<byte> memory)
    {
        //TODO:wait for innit local stream
        if (!_remoteToLocalStreamsMapping.TryGetValue(remoteStreamId, out int localStreamId))
        {
            if (localStreamHashCode != default)
            {
                _localToRemoteStreamsMapping.TryAdd(localStreamHashCode, remoteStreamId);
                _remoteToLocalStreamsMapping.TryAdd(remoteStreamId, localStreamHashCode);
                localStreamId = localStreamHashCode;
            }
            else
            {
                _logger.Warn($"Cannot find remote stream {remoteStreamId}");
                return;
            }
        }

        if(!_streams.TryGetValue(localStreamId, out  var stream))
            _logger.Warn($"Cannot find local stream {localStreamId}");
        else
            await stream.WriteAsync(memory).ConfigureAwait(false);
    }

    private async Task StartListeningForConnections(int listeningPort, string localIp)
    {
        try
        {
            var localServer = new TcpListener(new IPEndPoint(IPAddress.Parse(localIp), listeningPort));
            localServer.Start();
        
            //This is new connection
            //TODO: we need to have task of this
            while (true)
            {
                _logger.Info($"Waiting for TCP connection {localIp}:{listeningPort}");
                var localServerConnection = await localServer.AcceptTcpClientAsync().ConfigureAwait(false);
                _logger.Info($"Got TCP connection on {localIp}:{listeningPort}");
                var clientStream = localServerConnection.GetStream();
                int localStreamId = clientStream.GetHashCode();
                _streams.TryAdd(localStreamId, clientStream);

                Memory<byte> buffer = ArrayPool<byte>.Shared.Rent(Consts.TcpPackageSize);
                int bytesRead = await clientStream
                    .ReadAsync(buffer[Consts.CommandSizeBytes..Consts.TcpPackageSize])
                    .ConfigureAwait(false);
                await WaitToWsReadyAsync().ConfigureAwait(false);

                await _wsBase.InnitConnectionAsync(localStreamId, listeningPort, buffer[..(bytesRead + Consts.CommandSizeBytes)]).ConfigureAwait(false);

                Task.Run(() => StartReadingFromStream(clientStream));
            }
        }
        catch (Exception e)
        {
            _logger.Error(e);
        }
    }

    private async Task StartReadingFromStream(NetworkStream networkStream)
    {
        int localStreamHashCode = networkStream.GetHashCode();
        Memory<byte> buffer = ArrayPool<byte>.Shared.Rent(Consts.TcpPackageSize);
        while (_streams.ContainsKey(localStreamHashCode))
        {
            int bytesRead = await networkStream.ReadAsync(buffer[Consts.CommandSizeBytes..Consts.TcpPackageSize]).ConfigureAwait(false);
            await WaitToWsReadyAsync().ConfigureAwait(false);

            if (bytesRead == 0)
            {
                //TODO:wait for stream to be ready
                await _wsBase.SendCloseCommandAsync(localStreamHashCode).ConfigureAwait(false);
                _logger.Info($"Sending close stream {localStreamHashCode}");
                return;
            }

            var remoteStreamKnown = _localToRemoteStreamsMapping.TryGetValue(localStreamHashCode, out var remoteStreamId);
            
            await _wsBase.RespondToMessageAsync(localStreamHashCode,
                remoteStreamKnown? remoteStreamId: default, buffer[..(bytesRead + Consts.CommandSizeBytes)]).ConfigureAwait(false);
        }
    }

    private async Task WaitToWsReadyAsync()
    {
        if (_wsBase == null || !_wsBase.IsConnected)
        {
            _logger.Info("Ws is not connected, waiting");
            //TODO: wait in while cycle
            await Task.Delay(1000).ConfigureAwait(false);
        }
    }

    public void InnitWs(WsBase webSocket)
    {
        _wsBase = webSocket;
    }
}
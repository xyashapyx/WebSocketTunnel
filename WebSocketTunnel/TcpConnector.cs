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
            _remoteToLocalStreamsMapping.Remove(_localToRemoteStreamsMapping[streamId], out _);
            _localToRemoteStreamsMapping.Remove(streamId, out _);
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
        
        //TODO: read response somehow
    }

    public async Task RespondToStreamAsync(int remoteStreamId, int localStreamId, Memory<byte> memory)
    {
        if(!_streams.TryGetValue(localStreamId, out  var stream))
            Console.WriteLine($"Cannot find stream {localStreamId}");
        await stream.WriteAsync(memory);
        
        //TODO: read response somehow
    }
}
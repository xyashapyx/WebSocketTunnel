using System.Collections.Concurrent;
using System.Net.Sockets;

namespace WebSocketTunnel;

public class TcpConnector
{
    private ConcurrentDictionary<int, NetworkStream> _streams = new();

    public void CloseStream(int streamId)
    {
        if (_streams.ContainsKey(streamId))
        {
            _streams[streamId].Close();
            _streams.TryRemove(streamId, out _);
        }
        else
        {
            Console.WriteLine($"Did not find Stream {streamId}");
        }
    }

    public async Task EstablishConnectionAsync(int remotePort, int remoteStreamId, Memory<byte> memory)
    {
        throw new NotImplementedException();
    }

    public async Task RespondToStreamAsync(int remoteStreamId, int localStreamId, Memory<byte> memory)
    {
        throw new NotImplementedException();
    }
}
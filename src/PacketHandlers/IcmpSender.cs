using System.Net;
using System.Net.Sockets;
using System.Text;
using NetSonar.Packets;

namespace NetSonar.PacketHandlers;

public class IcmpSender
{
    /// <summary>
    /// The socket to send request <see cref="IcmpPacket"/>s though
    /// </summary>
    private readonly Socket _icmpSocket;
    
    /// <summary>
    /// The range of IPs that are to be pinged by this <see cref="IcmpSender"/>
    /// </summary>
    private readonly IpRange _range;
    /// <summary>
    /// The amount of IPs that could not be pinged
    /// </summary>
    private uint _failCount;
    
    /// <summary>
    /// The last time the status bar was updated, relative to <see cref="StatusManager.Timer"/>
    /// </summary>
    private TimeSpan _statusUpdateTimeOffset;
    
    /// <summary>
    /// Whether this <see cref="IcmpSender"/> has shut down
    /// </summary>
    public bool IsShutDown { get; private set; }
    
    public IcmpSender(Socket icmpSocket, IpRange range, int id)
    {
        _icmpSocket = icmpSocket;
        _range = range;
        
        var t = new Thread(Run)
        {
            Name = $"IcmpSender #{id}"
        };
        t.Start();
    }
    
    private void Run()
    {
        var dataLen = (int)Math.Ceiling(Constants.EchoRequestData.Length / 16d) * 16;
        
        var echo = new IcmpPacket(
            new IpHeader(),
            IcmpType.EchoRequest,
            0x0,
            Constants.Identifier,
            Constants.SequenceNumber,
            Encoding.UTF8.GetBytes(Constants.EchoRequestData.PadRight(dataLen, ' ')));
        
        var data = echo.GetIcmpBytes();
        
        var ep = new IPEndPoint(new IPAddress(0), 0);

        for (uint i = 0; i < _range.Size; i++)
        {
            if ((StatusManager.Timer.Elapsed - _statusUpdateTimeOffset).TotalMilliseconds >= Constants.StatusBarRefreshRateMs)
            {
                UpdateStatusBar(i);
                _statusUpdateTimeOffset = StatusManager.Timer.Elapsed;
            }
            
            var ip = _range.Get(i);
            ep.Address = ip;
            
            try
            {
                _icmpSocket.SendTo(data, ep);
            }
            catch (SocketException e)
            {
                var shouldExit = false;
                switch (e.SocketErrorCode)
                {
                    case SocketError.WouldBlock:
                    {
                        // retry the current IP
                        i--;
                        continue;
                    }
                    case SocketError.NetworkUnreachable:
                    { 
                        _failCount++;
                        continue;
                    }
                    case SocketError.Shutdown:
                    {
                        shouldExit = true;
                        break;
                    }
                }
                if (shouldExit) break;
            }
        }
        
        UpdateStatusBar(_range.Size);
        IsShutDown = true;
    }

    private void UpdateStatusBar(uint progress)
    {
        StatusBar.SetField("upload-prog", $"{progress / (double)_range.Size:P}");
        StatusBar.SetField("fail-count", $"{_failCount}");
    }
}

using System.Net;
using System.Net.Sockets;
using System.Text;
using NetSonar.Packets;

namespace NetSonar;

public class IcmpSender
{
    private readonly Socket _icmpSocket;
    private readonly DataProcessor _processor;
    
    public bool IsShutDown { get; private set; }
    
    private readonly IpRange _range;
    
    public IcmpSender(Socket icmpSocket, DataProcessor processor, IpRange range, int id)
    {
        _icmpSocket = icmpSocket;
        _processor = processor;
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

        var failCount = 0u;
        var ipUint = _range.First.GetUint();

        for (uint i = 0; i < _range.Size; i++)
        {
            var ip = _range.Get(i);
            ep.Address = ip;
            
            try
            {
                _icmpSocket.SendTo(data, ep);
                _processor.SetSendTime(ip, StatusManager.Timer.Elapsed);
            }
            catch (Exception e)
            {
                if (e is SocketException s)
                {
                    var shouldExit = false;
                    switch (s.SocketErrorCode)
                    {
                        case SocketError.WouldBlock:
                        case SocketError.TimedOut:
                        {
                            // retry the current IP
                            i--;
                            continue;
                        }
                        case SocketError.NetworkUnreachable:
                        {
                            failCount++;
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
                
                Console.WriteLine(e);
            }
            
            if (ipUint % Constants.SenderStatusRefreshInterval == 0)
            {
                StatusBar.SetField("upload-prog", $"{(ipUint - _range.First.GetUint()) / (double)_range.Size:P}");
                StatusBar.SetField("fail-count", $"{failCount}");
            }
            
            ipUint++;
        }
        
        IsShutDown = true;
    }
}

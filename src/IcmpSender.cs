using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetSonar;

public class IcmpSender
{
    private readonly Socket _icmpSocket;
    private readonly IpRange _range;
    
    private readonly Stopwatch _stopwatchTotal = new();
    private readonly Stopwatch _stopwatchWait = new();
    
    public IcmpSender(Socket icmpSocket, IpRange range)
    {
        _icmpSocket = icmpSocket;
        _range = range;
        
        var thread = new Task(Run);
        thread.Start();
    }
    
    private void Run()
    {
        _stopwatchTotal.Start();
        var echo = new IcmpPacket(
            new IpHeader(),
            0x8,
            0x0,
            0x1111,
            0x2222,
            Encoding.UTF8.GetBytes("".PadRight(16, ' ')));
        
        foreach (var ip in _range)
        {
            echo.Data = Encoding.UTF8.GetBytes(ip.ToString().PadRight(16, ' '));

            var data = echo.GetIcmpBytes();
            var ep = new IPEndPoint(ip, 0);
            try
            {
                _stopwatchWait.Start();
                _icmpSocket.SendTo(data, ep);
                _stopwatchWait.Stop();
            }
            catch (Exception e)
            {
                if (e is SocketException s)
                    Console.WriteLine($"{e.GetType().Name} ({s.NativeErrorCode}): {e.Message}");
                else
                    Console.WriteLine($"{e.GetType()}: {e.Message}");
            }
            
            if (ip.GetUint() % 256 == 0)
                Console.WriteLine($"{ip} in {_range} ({(ip.GetUint() - _range.First.GetUint()) / (double)_range.Size:P})");
        }
        
        _stopwatchTotal.Stop();
        _stopwatchWait.Stop();
        var totalMs = _stopwatchTotal.Elapsed.TotalMicroseconds / 1000.0;
        var waitMs = _stopwatchWait.Elapsed.TotalMicroseconds / 1000.0;
        Console.WriteLine($"Sent All {_range.Size} ({_range}) ICMP Echo Requests in {totalMs:F4}ms ({waitMs:F4}ms spent waiting ({waitMs/totalMs:P}))");
    }
}

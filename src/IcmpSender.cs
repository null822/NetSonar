using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetSonar;

public class IcmpSender
{
    private readonly Socket _icmpSocket;
    private readonly DataProcessor _processor;
    
    private readonly IpRange _range;
    
    private readonly Stopwatch _stopwatchTotal = new();
    private readonly Stopwatch _stopwatchWait = new();
    
    public IcmpSender(Socket icmpSocket, DataProcessor processor, IpRange range)
    {
        _icmpSocket = icmpSocket;
        _processor = processor;
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
            Encoding.UTF8.GetBytes("null822/NetSonar Ping".PadRight(32, ' ')));
        
        var data = echo.GetIcmpBytes();
        
        var ep = new IPEndPoint(new IPAddress(0), 0);
        
        var ipUint = _range.First.GetUint();
        foreach (var ip in _range)
        {
            ep.Address = ip;
            try
            {
                _stopwatchWait.Start();
                
                _icmpSocket.SendTo(data, ep);
                _processor.SetSendTime(ip, StatusManager.Timer.Elapsed);
                
                _stopwatchWait.Stop();

            }
            catch (Exception e)
            {
                if (e is not SocketException)
                    Console.WriteLine(e);
            }
            
            if (ipUint % Constants.SenderStatusRefreshInterval == 0)
            {
                StatusBar.SetField("upload-prog", $"{(ip.GetUint() - _range.First.GetUint()) / (double)_range.Size:P}");
                StatusBar.SetField("current-upload", ip.ToString());
                
                var totalMs = _stopwatchTotal.Elapsed.TotalMilliseconds;
                var waitMs = _stopwatchWait.Elapsed.TotalMilliseconds;
                StatusBar.SetField("upload-wait", $"{waitMs/totalMs:P4}");
            }

            ipUint++;
        }
        
        _stopwatchTotal.Stop();
        _stopwatchWait.Stop();
        // var totalMs = _stopwatchTotal.Elapsed.TotalMicroseconds / 1000.0;
        // var waitMs = _stopwatchWait.Elapsed.TotalMicroseconds / 1000.0;
        // Console.WriteLine($"Sent All {_range.Size} ({_range}) ICMP Echo Requests in {totalMs:F4}ms ({waitMs:F4}ms spent waiting for packets to send ({waitMs/totalMs:P} of total))");
    }
}

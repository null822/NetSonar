using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetSonar.Packets;

namespace NetSonar;

public class IcmpSender
{
    private readonly Socket _icmpSocket;
    private readonly DataProcessor _processor;
    
    private readonly IpRange _range;
    
    private readonly Stopwatch _stopwatchTotal = new();
    private readonly Stopwatch _stopwatchWait = new();
    
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
        _stopwatchTotal.Start();

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
                Console.WriteLine(e);
            }
            
            if (ipUint % Constants.SenderStatusRefreshInterval == 0)
            {
                StatusBar.SetField("upload-prog", $"{(ipUint - _range.First.GetUint()) / (double)_range.Size:P}");
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

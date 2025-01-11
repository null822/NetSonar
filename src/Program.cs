using System.Net.Sockets;
using NetSonar.PacketHandlers;
using static NetSonar.Constants;

namespace NetSonar;

public static class Program
{
    private static IpRange _ipRange;
    private static ScanType _scanType;
    
    private static DataProcessor _dataProcessor = null!;
    
    private static readonly IcmpSender[] Senders = new IcmpSender[SenderCount];
    private static readonly IcmpReceiver[] Receivers = new IcmpReceiver[ReceiverCount];
    
    private static readonly Socket IcmpSocket = new(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp) 
    {
        ReceiveBufferSize = ReceiveBufferSize,
        SendBufferSize = ReceiveBufferSize,
        Blocking = false
    };
    
    public static int Main(string[] args)
    {
        // args = ["-range 127.0.0.0/16", "-type 0"];
        
        _ipRange = new IpRange(GetArgument(args, "range", "IP Range"));
        var typeStr = GetArgument(args, "type", "Scan Type");
        if (!Enum.TryParse(typeStr, out _scanType))
        {
            throw new Exception("Invalid Scan Type");
        }
        
        _dataProcessor = new DataProcessor(_ipRange);
        
        Console.Clear();
        
        CreateStatusBar();
        
        Task.Run(StatusBar.Run);
        Task.Run(StatusManager.Run);
        
        // create receivers
        for (var i = 0; i < ReceiverCount; i++)
        {
            Receivers[i] = new IcmpReceiver(IcmpSocket, _dataProcessor, i);
        }
        
        Thread.Sleep(1000);
        
        // create senders
        for (uint i = 0; i < SenderCount; i++)
        {
            Senders[i] = new IcmpSender(
                IcmpSocket,
                _ipRange.Split(i, SenderCount),
                (int)i
                );
        }
        
        // wait for all senders to get shut down
        while (true)
        {
            if (Senders.All(r => r.IsShutDown))
                break;
            
            Thread.Sleep(250);
        }
        
        // allow all receivers to shut down
        foreach (var receiver in Receivers)
        {
            receiver.EnableShutdown();
        }
        
        // wait for all receivers to get shut down
        while (true)
        {
            if (Receivers.All(r => r.IsShutDown))
                break;
            
            Thread.Sleep(250);
        }
        
        // shut down
        
        StatusManager.Shutdown();
        StatusBar.Shutdown();
        _dataProcessor.Shutdown();
        IcmpSocket.Shutdown(SocketShutdown.Both);
        
        while (!_dataProcessor.IsShutDown)
        {
            Thread.Sleep(100);
        }
        while (!StatusBar.IsShutDown)
        {
            Thread.Sleep(100);
        }
        
        return 0;
    }

    private static string GetArgument(string[] args, string name, string displayName)
    {
        var arg = args.FirstOrDefault(a => a.StartsWith($"-{name} "), "");
        if (arg != "")
            return arg[(name.Length + 2)..];
        
        Console.Write($"{displayName}: ");
        return Console.ReadLine() ?? "";
    }

    private static void CreateStatusBar()
    {
        StatusBar.CreateField("ip-range", _ipRange.ToString());
        StatusBar.CreateField("ip-cidr", _ipRange.Cidr);
        var (x, y) = DataProcessor.Deinterleave(_ipRange.First.GetUint());
        StatusBar.CreateField("ip-coords", $"({x}, {y})");
        StatusBar.CreateField("uptime-h", "0");
        StatusBar.CreateField("uptime-m", "0");
        StatusBar.CreateField("uptime-s", "0");
        StatusBar.CreateField("uptime-ms", "0");
        StatusBar.SetLine(0, "Scanning: %18C / %35C / %14C         | Uptime: %2Rh %2Rm %2Rs %3Rms",
            "ip-cidr", "ip-range", "ip-coords", "uptime-h", "uptime-m", "uptime-s", "uptime-ms");
        
        StatusBar.SetLine(1, "--#-> |  Send   | %8R Mbps | Progress: %7R | Fails: %10R |",
            "upload-speed", "upload-prog", "fail-count");
        
        StatusBar.SetLine(2, "  #<- | Receive | %8R Mbps | Fill:     %7R | Max Fill: %7R | Batches: %8R |",
            "download-speed", "receiver-fill", "max-receiver-fill", "batch-count");
        
        StatusBar.SetLine(3, "<-#   | Process | Load: %7R | Total: %10R |",
            "processor-load", "processed-count");
    }

    private enum ScanType
    {
        Ping,
        Port
    }
}

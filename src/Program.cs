using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using static NetSonar.Constants;

namespace NetSonar;

public static class Program
{
    private static readonly DataProcessor DataProcessor = new();
    
    private static readonly IcmpSender[] Senders = new IcmpSender[SenderCount];
    private static readonly IcmpReceiver[] Receivers = new IcmpReceiver[ReceiverCount];

    private static readonly Socket IcmpSocket = new(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp) 
    {
        ReceiveTimeout = 1,
        ReceiveBufferSize = ReceiveBufferSize,
        SendBufferSize = ReceiveBufferSize,
        Blocking = false
    };
    
    public static int Main(string[] args)
    {
        if (args.Length >= 1)
        {
            Config.Range = new IpRange(args[0]);
        }
        else
        {
            // Config.Range = new IpRange("1.0.0.0/16");
            Console.Write("IP Range: ");
            Config.Range = new IpRange(Console.ReadLine() ?? "");
        }
        
        Console.Clear();
        
        CreateStatusBar();
        
        Task.Run(StatusBar.Run);
        Task.Run(StatusManager.Run);
        // Console.CursorTop = 0;
        
        
        // create receivers
        for (var i = 0; i < ReceiverCount; i++)
        {
            Receivers[i] = new IcmpReceiver(IcmpSocket, DataProcessor, i);
        }
        
        Thread.Sleep(1000);
        
        // create senders
        for (uint i = 0; i < SenderCount; i++)
        {
            Senders[i] = new IcmpSender(
                IcmpSocket,
                DataProcessor,
                Config.Range.Split(i, SenderCount),
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
        DataProcessor.Shutdown();
        IcmpSocket.Shutdown(SocketShutdown.Both);
        
        while (!DataProcessor.IsShutDown)
        {
            Thread.Sleep(100);
        }
        
        return 0;
    }

    private static void CreateStatusBar()
    {
        StatusBar.CreateField("ip-range", Config.Range.ToString());
        StatusBar.CreateField("ip-cidr", Config.Range.Cidr);
        StatusBar.CreateField("uptime-h", "0");
        StatusBar.CreateField("uptime-m", "0");
        StatusBar.CreateField("uptime-s", "0");
        StatusBar.CreateField("uptime-ms", "0");
        StatusBar.SetLine(0, "Scanning: %18C (%35C) \\% | Uptime: %2Rh %2Rm %2Rs %3Rms",
            "ip-cidr", "ip-range", "uptime-h", "uptime-m", "uptime-s", "uptime-ms");
        
        StatusBar.SetLine(1, "#-> |  Send   | %8R Mbps | Load: %8R | Progress: %6R | Fails: %10R",
            "upload-speed", "sender-load", "upload-prog", "fail-count");
        
        StatusBar.SetLine(2, "#<- | Receive | %8R Mbps | Fill: %8R | Max Fill: %6R | Batches: %8R",
            "download-speed", "receiver-fill", "max-receiver-fill", "batch-count");
        
        StatusBar.SetLine(3, "<-# | Process | Total: %10R",
            "processed-count");
    }

    #region Extension Methods
    
    public static ushort ComputeIpChecksum(this byte[] data)
    {
        if (data.Length % 2 != 0) throw new ArgumentException($"Data length must be divisible by 2: found {data.Length}");
        
        uint checksum32 = 0;
        for (var i = 0; i < data.Length; i += 2)
        {
            checksum32 += BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan()[i..(i + 2)]);
        }
        
        return (ushort)~((checksum32 & 0xFFFF) + (checksum32 >> 16));
    }
    
    public static byte[] GetBytes(this ushort v)
    {
        return [(byte)(v >> 8), (byte)(v & 0xFF)];
    }
    
    public static ushort SwapEndian(this ushort v)
    {
        var b = v.GetBytes();
        return (ushort)((b[1] << 8) | b[0]);
    }
    
    
    public static byte[] GetBytes(this uint v)
    {
        return [
            (byte)((v >> 24) & 0xFF),
            (byte)((v >> 16) & 0xFF),
            (byte)((v >>  8) & 0xFF),
            (byte)((v >>  0) & 0xFF)
        ];
    }
    
    public static uint SwapEndian(this uint v)
    {
        // var b = v.GetBytes();
        return (((v >>  0) & 0xFF) << 24) |
               (((v >>  8) & 0xFF) << 16) |
               (((v >> 16) & 0xFF) <<  8) |
               (((v >> 24) & 0xFF) <<  0);
    }
    
    public static uint GetUint(this IPAddress v)
    {
        var b = v.GetAddressBytes();
        return (uint)(
            (b[0] << 24) |
            (b[1] << 16) |
            (b[2] <<  8) |
             b[3]);
    }
    
    #endregion
}
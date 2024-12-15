using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using static NetSonar.Constants;

namespace NetSonar;

public static class Program
{
    private static readonly DataProcessor DataProcessor = new();
    
    private static readonly IcmpSender[] Senders = new IcmpSender[SenderCount];
    private static readonly IcmpReceiver[] Receivers = new IcmpReceiver[ReceiverCount];
    private static readonly byte[] Buffer = new byte[ReceiverCount * BufferSize];
    
    private static readonly Socket IcmpSocket = new(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)
    {
        ReceiveTimeout = 1,
        ReceiveBufferSize = BufferSize,
        SendBufferSize = BufferSize,
    };
    
    /*
     * 1.0.*.*
     * 
     * synchronous = ~27.5s / 65536 IPs @ ~1.1Mbps
     * 16 senders = ~27.5s / 65536 IPs @ ~1.1Mbps
     * synchronous = ~0.75s / 4096 IPs @ ~1.1Mbps
     * synchronous + invalid checksum = ~11s / 65536 IPs @ ~2.9Mbps
     *
     * ~ 2166 IPs/s/[Mbps]
     * 
     */
    
    public static void Main(string[] args)
    {
        if (args.Length >= 1)
        {
            Config.Range = new IpRange(args[0]);
        }
        else
        {
            // Config.Range = new IpRange("220.232.100.55/16");
            Console.Write("IP Range: ");
            Config.Range = new IpRange(Console.ReadLine() ?? "");
        }
        
        Console.Clear();
        
        StatusBar.SetField("ip-range", Config.Range.ToString());
        StatusBar.SetField("ip-cidr", Config.Range.Cidr);
        StatusBar.CreateField("uptime");
        StatusBar.SetLine(0, fields => 
            $"Scanning: {fields["ip-cidr"]} ({fields["ip-range"]}) | Uptime: {fields["uptime"]}");
        
        StatusBar.CreateField("upload-speed");
        StatusBar.CreateField("upload-wait");
        StatusBar.CreateField("upload-prog");
        StatusBar.SetLine(1, fields => 
            $"#-> |  Send   | {fields["upload-speed"],8} Mbps | Progress: {fields["upload-prog"],6} | Network Wait: {fields["upload-wait"],8}");
        
        StatusBar.CreateField("download-speed");
        StatusBar.SetField("response-count", "0");
        StatusBar.SetLine(2, fields => 
            $"#<- | Receive | {fields["download-speed"],8} Mbps | Total: {fields["response-count"]}");
        
        StatusBar.SetField("processed-count", "0");
        StatusBar.SetLine(3, fields => 
            $"<-# | Process | Total: {fields["processed-count"]}");
        
        Task.Run(StatusBar.Run);
        Task.Run(StatusManager.Run);
        
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
        
        var receiveStopwatch = new Stopwatch();
        receiveStopwatch.Start();
        
        // create receivers
        for (var i = 0; i < ReceiverCount; i++)
        {
            Receivers[i] = new IcmpReceiver(
                IcmpSocket,
                DataProcessor,
                new ArraySegment<byte>(Buffer, i * BufferSize, BufferSize),
                i);
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
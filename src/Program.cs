using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetSonar;

public static class Program
{
    private static readonly Socket[] IcmpSockets = new Socket[SenderCount];
    private static readonly IcmpSender[] Senders = new IcmpSender[SenderCount];
    private static readonly List<Task<int>> Receivers = new(SenderCount);
    private static readonly byte[] Buffers = new byte[SenderCount * BufferSize];
    
    public static readonly Stopwatch Timer = new();

    public static readonly IPAddress LocalHostAddress = Dns.GetHostAddresses(Dns.GetHostName())
        .First(a => a.AddressFamily == AddressFamily.InterNetwork);
    
    /*
     * 1.0.*.*
     * 
     * synchronous = ~27.5s / 65536 IPs @ ~1.1Mb/s
     * 16 senders = ~27.5s / 65536 IPs @ ~1.1Mb/s
     * synchronous = ~0.75s / 4096 IPs @ ~1.1Mb/s
     * synchronous + invalid checksum = ~11s / 65536 IPs @ ~2.9Mb/s
     * 
     * 
     */
    private const int SenderCount = 16;
    private const int BufferSize = 128;
    private static readonly IpRange Range = new("1.0.0.0/16");
    
    public static void Main()
    {
        Timer.Start();
        
        var lEp = new IPEndPoint(LocalHostAddress, 0);
        for (uint i = 0; i < SenderCount; i++)
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)
            {
                ReceiveBufferSize = BufferSize,
                SendBufferSize = BufferSize
            };
            
            IcmpSockets[i] = s;
            
            Senders[i] = new IcmpSender(s, Range.Split(i, SenderCount));
        }
        
        var receiveStopwatch = new Stopwatch();
        receiveStopwatch.Start();
        
        for (var i = 0; i < SenderCount; i++)
        {
            var t = new Task<int>(() => -1);
            t.Start();
            Receivers.Add(t);
        }
        
        while (true)
        {
            var i = Receivers.FindIndex(r => r is { IsCompleted: true });
            if (i == -1)
            {
                Thread.Sleep(10);
                continue;
            }
            
            if (Receivers[i].Result != -1)
            {
                var packet = new IcmpPacket(Buffers, i * BufferSize);
                
                Console.WriteLine($"[{IcmpSockets[i].Available / packet.Header.TotalLength}] {Encoding.UTF8.GetString(packet.Data)} " + 
                                  $"@ {Timer.Elapsed.TotalMicroseconds / 1000.0:F4}ms " + 
                                  $"({receiveStopwatch.Elapsed.TotalMicroseconds / 1000.0:F4}ms since last)");
            }
            
            Receive(i);
            
            receiveStopwatch.Restart();
        }
        
        Timer.Stop();
        
        for (var i = 0; i < SenderCount; i++)
        {
            IcmpSockets[i].Shutdown(SocketShutdown.Both);
            IcmpSockets[i].Close();
        }
        
    }
    
    public static void Receive(int i)
    {
        var seg = new ArraySegment<byte>(Buffers, i * BufferSize, BufferSize);
        Receivers[i] = IcmpSockets[i].ReceiveAsync(seg);
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
    
    public static BinaryReader ToBinaryReader(this byte[] data)
    {
        var s = new MemoryStream();
        s.Write(data);
        s.Position = 0;
        return new BinaryReader(s, Encoding.Default, false);
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
        var b = v.GetBytes();
        return (uint)(
            (b[3] << 24) |
            (b[2] << 16) |
            (b[1] <<  8) |
            (b[0] <<  0));
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
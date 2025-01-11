using System.Buffers.Binary;
using System.Net;

namespace NetSonar;

public static class Extensions
{
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
}
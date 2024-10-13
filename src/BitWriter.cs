using System.Text;

namespace NetSonar;

public class BitWriter(Stream stream)
{
    private byte _incompleteByte;
    private int _incompleteBitCount;
    
    public byte Incomplete => _incompleteByte;
    
    public void Write(ulong value, int bitCount)
    {
        if (bitCount > 64) throw new Exception("Cannot write more than 64 bits at a time");
        
        // handle values that don't fill up the _incompleteByte
        if (_incompleteBitCount + bitCount < 8)
        {
            _incompleteByte |= (byte)((value << (8 - bitCount)) >> _incompleteBitCount);
            _incompleteBitCount += bitCount;
            return;
        }
        
        var incompleteMask = (byte)~(~0x0u >> (_incompleteBitCount + 24));
        var valueMask = bitCount == 64 ? ~0uL : ~(~0x0uL << bitCount);
        
        var firstByteWriteBits = 8 - _incompleteBitCount;
        
        var firstByte = (byte)(((ulong)_incompleteByte & incompleteMask) | ((value & valueMask) >> (bitCount - firstByteWriteBits)));
        
        stream.WriteByte(firstByte);

        var remainingBitCount = bitCount - firstByteWriteBits;
        var (writeByteCount, incompleteBitCount) = int.DivRem(remainingBitCount, 8);

        _incompleteByte = (byte)((value >> (bitCount - incompleteBitCount) << (8 - incompleteBitCount)) & 0xFF);
        _incompleteBitCount = incompleteBitCount;
        
        for (var i = 1; i < writeByteCount + 1; i++)
        {
            stream.WriteByte((byte)((value >> (remainingBitCount - 8 * i)) & 0xFF));
        }
        
    }
    
    public void Flush()
    {
        stream.WriteByte(_incompleteByte);
        _incompleteByte = 0;
        _incompleteBitCount = 0;
    }
    
    public long Position
    {
        get => stream.Position;
        set => stream.Position = value;
    }

    public override string ToString()
    {
        if (stream.Length == 0) return "";
        
        var buffer = new Span<byte>(new byte[stream.Length]);
        var p = stream.Position;
        stream.Position = 0;
        var l = stream.Read(buffer);
        stream.Position = p;
        
        var s = new StringBuilder();
        s.EnsureCapacity((int)(stream.Length * 9));
        foreach (var b in buffer)
        {
            s.Append($"{b:b8} ");
        }
        
        return s.Remove(s.Length - 1, 1).ToString();
    }
}

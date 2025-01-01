using System.Text;

namespace NetSonar.Packets;

public struct IcmpPacket
{
    public IpHeader Header;
    
    private byte _type;
    private byte _code;
    private ushort _checksum;
    private ushort _identifier;
    private ushort _sequenceNumber;
    private byte[] _data;
    
    public IcmpType Type
    {
        get => (IcmpType)_type;
        set { _type = (byte)value; ComputeChecksum(); }
    }
    public byte Code
    {
        get => _code;
        set { _code = value; ComputeChecksum(); }
    }
    public ushort Checksum => _checksum;
    public ushort Identifier
    {
        get => _identifier;
        set { _identifier = value; ComputeChecksum(); }
    }
    public ushort SequenceNumber
    {
        get => _sequenceNumber;
        set { _sequenceNumber = value; ComputeChecksum(); }
    }
    public byte[] Data
    {
        get => _data;
        set { _data = value; ComputeChecksum(); }
    }
    
    public IcmpPacket(IpHeader header, IcmpType type, byte code, ushort identifier, ushort sequenceNumber, byte[] data)
    {
        Header = header;
        
        _type = (byte)type;
        _code = code;
        _identifier = identifier;
        _sequenceNumber = sequenceNumber;
        _data = data;
        
        ComputeChecksum();
    }
    
    public IcmpPacket(Span<byte> bytes)
    {
        var s = new MemoryStream();
        s.Write(bytes);
        s.Position = 0;
        var packet = new BinaryReader(s, Encoding.Default, false);
        
        Header = new IpHeader(s);
        
        _type = packet.ReadByte();
        _code = packet.ReadByte();
        _checksum = packet.ReadUInt16();
        _identifier = packet.ReadUInt16();
        _sequenceNumber = packet.ReadUInt16();
        
        var data = new byte[Header.TotalLength - 20 - 8];
        var dataLen = packet.Read(data);
        _data = data[..(dataLen-1)];
        
        packet.Dispose();
    }
    
    public IcmpPacket(Stream s)
    {
        var buffer = new BinaryReader(s, Encoding.Default, true);
        
        Header = new IpHeader(s);
        
        _type = buffer.ReadByte();
        _code = buffer.ReadByte();
        _checksum = buffer.ReadUInt16();
        _identifier = buffer.ReadUInt16();
        _sequenceNumber = buffer.ReadUInt16();
        
        var data = new byte[Header.TotalLength - 20 - 8];
        var dataLen = buffer.Read(data);
        _data = data[..(dataLen-1)];
        
        buffer.Dispose();
    }
    
    /// <summary>
    /// Returns all bytes of the ICMP Packet, including the IP Header
    /// </summary>
    public byte[] GetBytes()
    {
        return
        [
            ..Header.GetBytes(),
            ..GetIcmpBytes()
        ];
    }
    /// <summary>
    /// Returns all bytes of the ICMP Packet, excluding the IP Header
    /// </summary>
    public byte[] GetIcmpBytes()
    {
        return
        [
            _type, _code,
            ..BitConverter.GetBytes(_checksum),
            ..BitConverter.GetBytes(_identifier),
            ..BitConverter.GetBytes(_sequenceNumber),
            .._data
        ];
    }
    /// <summary>
    /// Returns all bytes of the ICMP Packet, excluding the IP Header
    /// </summary>
    public byte[] GetIcmpBytesNoChecksum()
    {
        return
        [
            _type, _code,
            ..BitConverter.GetBytes(_identifier),
            ..BitConverter.GetBytes(_sequenceNumber),
            .._data
        ];
    }
    
    private void ComputeChecksum()
    {
        _checksum = GetIcmpBytesNoChecksum().ComputeIpChecksum();
    }
}

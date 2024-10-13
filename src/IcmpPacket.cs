using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetSonar;

public struct IcmpPacket
{
    public IpHeader Header;
    
    private byte _type;
    private byte _code;
    private ushort _checksum;
    private ushort _identifier;
    private ushort _sequenceNumber;
    private byte[] _data;
    
    public byte Type
    {
        get => _type;
        set { _type = value; ComputeChecksum(); }
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
    
    public IcmpPacket(IpHeader header, byte type, byte code, ushort identifier, ushort sequenceNumber, byte[] data)
    {
        Header = header;
        
        _type = type;
        _code = code;
        _identifier = identifier;
        _sequenceNumber = sequenceNumber;
        _data = data;
        
        ComputeChecksum();
    }

    public IcmpPacket(byte[] bytes, int offset)
    {
        var s = new MemoryStream();
        s.Write(bytes, offset, bytes.Length - offset);
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
        var packet = new BinaryReader(s, Encoding.Default, true);
        
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

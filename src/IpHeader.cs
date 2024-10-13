using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetSonar;

// TODO: compute header checksum
public struct IpHeader
{
    /// <summary>
    /// Contains the Version and Header Length fields.
    /// <code>
    /// Version = 1111 ....
    /// Header  = .... 1111
    /// </code>
    /// </summary>
    private byte _versionAndHeaderLength;
    public byte Version
    {
        get => (byte)(_versionAndHeaderLength >> 4);
        set => _versionAndHeaderLength = (byte)((_versionAndHeaderLength & 0b0000_1111) | ((value & 0b0000_1111) << 4));
    }
    public int HeaderLength
    {
        get => (byte)(_versionAndHeaderLength & 0b0000_1111) * 4;
        set => _versionAndHeaderLength = (byte)((_versionAndHeaderLength & 0b1111_0000) | ((byte)(value / 4) & 0b0000_1111));
    }
    
    /// <summary>
    /// Contains the DSCP and ECN fields.
    /// <code>
    /// DSCP = 1111 11..
    /// ECN  = .... ..11
    /// </code>
    /// </summary>
    private byte _differentiatedServices;
    public byte Dscp
    {
        get => (byte)(_differentiatedServices >> 2);
        set => _differentiatedServices = (byte)((_differentiatedServices & 0b0000_0011) | ((value & 0b0000_0011) << 2));
    }
    public byte Ecn
    {
        get => (byte)(_differentiatedServices & 0b0000_0011);
        set => _differentiatedServices = (byte)((_differentiatedServices & 0b1111_1100) | (value & 0b0000_0011));
    }

    private ushort _totalLength;
    public ushort TotalLength
    {
        get => _totalLength.SwapEndian();
        set => _totalLength = value.SwapEndian();
    }

    private ushort _identification;
    public ushort Identification
    {
        get => _identification.SwapEndian();
        set => _identification = value.SwapEndian();
    }
    /// <summary>
    /// Contains the Flags and Fragment Offset fields.
    /// <code>
    /// Flags           = 111. .... .... ....
    /// Fragment Offset = ...1 1111 1111 1111
    /// </code>
    /// </summary>
    private ushort _flagsAndFragmentOffset;
    public FragmentFlags FragmentFlags
    {
        get => (FragmentFlags)(_flagsAndFragmentOffset >> 13);
        set => _flagsAndFragmentOffset = (byte)((_flagsAndFragmentOffset & 0b1111_1100) | ((byte)value & 0b1111_1100));
    }
    public ushort FragmentOffset
    {
        get => (byte)(_flagsAndFragmentOffset & 0b0001_1111_1111_1111);
        set => _flagsAndFragmentOffset = (byte)((_flagsAndFragmentOffset & 0b1110_0000_0000_0000) | (value & 0b0001_1111_1111_1111));
    }
    
    public byte TimeToLive { get; set; }
    
    private byte _protocol;
    public ProtocolType Protocol
    {
        get => (ProtocolType)_protocol;
        set => _protocol = (byte)value;
    }

    private ushort _headerChecksum;
    public ushort HeaderChecksum
    {
        get => _headerChecksum.SwapEndian();
        set => _headerChecksum = value.SwapEndian();
    }
    
    private uint _sourceAddress;
    public IPAddress SourceAddress
    {
        get => new(_sourceAddress);
        set => _sourceAddress = value.GetUint();
    }
    
    private uint _destinationAddress;
    public IPAddress DestinationAddress
    {
        get => new(_destinationAddress);
        set => _destinationAddress = value.GetUint();
    }
    
    public IpHeader(byte version, byte headerLength, byte dscp, byte ecn, ushort totalLength, ushort identification,
        FragmentFlags fragmentFlags, ushort fragmentOffset, byte ttl, ProtocolType protocol, ushort headerChecksum,
        IPAddress sourceAddress, IPAddress destinationAddress)
    {
        Version = version;
        HeaderLength = headerLength;
        Dscp = dscp;
        Ecn = ecn;
        _totalLength = totalLength;
        _identification = identification;
        FragmentFlags = fragmentFlags;
        FragmentOffset = fragmentOffset;
        TimeToLive = ttl;
        Protocol = protocol;
        HeaderChecksum = headerChecksum;
        SourceAddress = sourceAddress;
        DestinationAddress = destinationAddress;
    }
    
    public IpHeader(byte[] bytes, int offset)
    {
        var s = new MemoryStream();
        s.Write(bytes, offset, bytes.Length - offset);
        s.Position = 0;
        var header = new BinaryReader(s, Encoding.Default, false);
        
        _versionAndHeaderLength = header.ReadByte();
        _differentiatedServices = header.ReadByte();
        _totalLength = header.ReadUInt16();
        _identification = header.ReadUInt16();
        _flagsAndFragmentOffset = header.ReadUInt16();
        TimeToLive = header.ReadByte();
        _protocol = header.ReadByte();
        HeaderChecksum = header.ReadUInt16();
        _sourceAddress = header.ReadUInt32();
        _destinationAddress = header.ReadUInt32();
        
        header.Dispose();
    }
    
    public IpHeader(Stream s)
    {
        var header = new BinaryReader(s, Encoding.Default, true);
        
        _versionAndHeaderLength = header.ReadByte();
        _differentiatedServices = header.ReadByte();
        _totalLength = header.ReadUInt16();
        _identification = header.ReadUInt16();
        _flagsAndFragmentOffset = header.ReadUInt16();
        TimeToLive = header.ReadByte();
        _protocol = header.ReadByte();
        HeaderChecksum = header.ReadUInt16();
        _sourceAddress = header.ReadUInt32();
        _destinationAddress = header.ReadUInt32();
        
        header.Dispose();
    }
    
    public byte[] GetBytes()
    {
        return
        [
            _versionAndHeaderLength, _differentiatedServices,
            ..BitConverter.GetBytes(_totalLength),
            ..BitConverter.GetBytes(_identification),
            ..BitConverter.GetBytes(_flagsAndFragmentOffset),
            TimeToLive, _protocol,
            ..BitConverter.GetBytes(_headerChecksum),
            ..BitConverter.GetBytes(_sourceAddress),
            ..BitConverter.GetBytes(_destinationAddress)
        ];
    }
}

[Flags]
public enum FragmentFlags : byte
{
    Reserved = 0,
    DontFragment = 1,
    MoreFragments = 2,
}
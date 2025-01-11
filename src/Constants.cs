using NetSonar.PacketHandlers;
using NetSonar.Packets;

namespace NetSonar;

public static class Constants
{
    /// <summary>
    /// The amount of sender threads
    /// </summary>
    public const int SenderCount = 1;
    /// <summary>
    /// The amount of receiver threads
    /// </summary>
    public const int ReceiverCount = 1;
    
    /// <summary>
    /// The size of the receiving buffer and batch of <see cref="IcmpPacket"/> responses, in bytes
    /// </summary>
    public const int ReceiveBufferSize = 8192;
    /// <summary>
    /// The amount of receiving buffers for each <see cref="IcmpReceiver"/>
    /// </summary>
    public const int ReceiveBufferCount = 8;
    
    /// <summary>
    /// How long to wait for submitting a batch of response <see cref="IcmpPacket"/>s to the <see cref="DataProcessor"/>
    /// before accepting a new batch into the <see cref="BatchSubmitter"/> and trying again
    /// </summary>
    public const int BatchSubmitTimeout = 50;
    
    /// <summary>
    /// How long a receiver should wait before shutting down due to a stale connection, in milliseconds
    /// </summary>
    public const int ReceiverShutdownWait = 1_000;

    
    
    /// <summary>
    /// The value of the <see cref="IcmpPacket.Identifier"/> field of all outgoing ICMP packets
    /// </summary>
    public const ushort Identifier = 'N' + ('S' << 8);
    /// <summary>
    /// The value of the <see cref="IcmpPacket.SequenceNumber"/> field of all outgoing ICMP packets
    /// </summary>
    public const ushort SequenceNumber = 0x2222;
    
    /// <summary>
    /// A string containing the data to put in the <see cref="IcmpPacket.Data"/> field
    /// </summary>
    public const string EchoRequestData = "null822/NetSonar";
    
    
    /// <summary>
    /// The delay between status bar refreshes
    /// </summary>
    public const int StatusBarRefreshRateMs = 500;
    
    /// <summary>
    /// The time, in milliseconds, between each update of the map file
    /// </summary>
    public const int MapRefreshRate = 200;
}

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
    /// The size of the receiving buffer for a batch of <see cref="IcmpPacket"/> responses
    /// </summary>
    public const int ReceiveBufferSize = 8192;
    /// <summary>
    /// The amount of receiving buffers for each <see cref="IcmpReceiver"/>
    /// </summary>
    public const int ReceiveBufferCount = 8;
    
    
    
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
    /// How long a receiver should wait in between failed receiving attempts
    /// </summary>
    public const int SenderIpsPer50MsWait = 16;
    /// <summary>
    /// How long a receiver should wait before shutting down due to a stale connection
    /// </summary>
    public const int ReceiverShutdownWaitMs = 1_000;
    
    /// <summary>
    /// The amount of IPs to ping before refreshing the status bar
    /// </summary>
    public const int SenderStatusRefreshInterval = 256;
    
    /// <summary>
    /// The amount of times to try to receive before refreshing the status bar
    /// </summary>
    public const int ReceiverStatusRefreshRateMs = 500;

    /// <summary>
    /// The delay between status bar refreshes
    /// </summary>
    public const int StatusBarRefreshRateMs = 100;
    
    /// <summary>
    /// The amount of IP responses to process before updating the map file
    /// </summary>
    public const int MapRefreshRateMs = 5000;
}

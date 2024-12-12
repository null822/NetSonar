namespace NetSonar;

public static class Constants
{
    // TODO: allow for non-SenderCount-multiple lengths
    // TODO: crash on odd subnet mask?
    /// <summary>
    /// The range of IP addresses to ping. Total number of IP addresses must be a multiple of <see cref="SenderCount"/>.
    /// For an even IP map, use an even subnet mask.
    /// </summary>
    public static readonly IpRange Range = new("13.224.0.0/15");
    
    /// <summary>
    /// The amount of sender threads
    /// </summary>
    public const int SenderCount = 16;
    /// <summary>
    /// The amount of receiver threads
    /// </summary>
    public const int ReceiverCount = 16;
    
    /// <summary>
    /// The size of the receiving buffer of each ICMP ping response
    /// </summary>
    public const int BufferSize = 128;
    /// <summary>
    /// The size of a batch of ping responses, submitted to the <see cref="DataProcessor"/>
    /// </summary>
    public const int PingDataBatchSize = 16;
    
    /// <summary>
    /// How long a receiver should wait in between failed receiving attempts
    /// </summary>
    public const int ReceiverWait = 10;
    /// <summary>
    /// How long a receiver should wait before shutting down due to a stale connection
    /// </summary>
    public const int ReceiverShutdownWaitMs = 1_000;
}

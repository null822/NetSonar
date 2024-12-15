namespace NetSonar;

public static class Config
{
    // TODO: allow for non-SenderCount-multiple lengths
    /// <summary>
    /// The range of IP addresses to ping. Total number of IP addresses must be a multiple of <see cref="SenderCount"/>.
    /// For an even IP map, use an even subnet mask.
    /// </summary>
    public static IpRange Range;

}
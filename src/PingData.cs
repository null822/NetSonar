using System.Net;

namespace NetSonar;

public record PingData(IPAddress Address, TimeSpan ReceiveTime);
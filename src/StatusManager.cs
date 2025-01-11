using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NetSonar;

public static class StatusManager
{
    public static readonly Stopwatch Timer = new();
    public static double TimerMs => Timer.Elapsed.TotalMilliseconds;
    
    private static long _prevUp;
    private static long _prevDown;

    private static bool _isShuttingDown;
    
    public static void Run()
    {
        Timer.Start();

        var nic = GetActiveNetworkInterface();
        const double refreshMultiplier = 1000d / Constants.StatusBarRefreshRateMs;

        var statistics = nic.GetIPStatistics();
        _prevUp = statistics.BytesSent;
        _prevDown = statistics.BytesReceived;
        
        do
        {
            statistics = nic.GetIPStatistics();
            var up = statistics.BytesSent;
            var down = statistics.BytesReceived;
            
            var upSpeed = (up - _prevUp) * 8 / 1_000_000d * refreshMultiplier;
            var downSpeed = (down - _prevDown) * 8 / 1_000_000d * refreshMultiplier;
            
            StatusBar.SetField("upload-speed", $"{upSpeed:N4}");
            StatusBar.SetField("download-speed", $"{downSpeed:N4}");
            
            StatusBar.SetField("uptime-h", Timer.Elapsed.Hours.ToString());
            StatusBar.SetField("uptime-m", Timer.Elapsed.Minutes.ToString());
            StatusBar.SetField("uptime-s", Timer.Elapsed.Seconds.ToString());
            StatusBar.SetField("uptime-ms", Timer.Elapsed.Milliseconds.ToString());
            
            _prevUp = up;
            _prevDown = down;

            Thread.Sleep(Constants.StatusBarRefreshRateMs);
        } while (!_isShuttingDown);
        
        Timer.Stop();
    }
    
    //TODO: make this handle high calling rates
    public static bool ShouldUpdateStatusBar(ref TimeSpan prevUpdate)
    {
        var result = Timer.Elapsed.TotalMilliseconds - prevUpdate.TotalMilliseconds >= Constants.StatusBarRefreshRateMs;
        if (result) prevUpdate = Timer.Elapsed;
        return result;
    }

    public static void Shutdown()
    {
        _isShuttingDown = true;
    }
    
    private static NetworkInterface GetActiveNetworkInterface()
    {
        var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        s.Connect(IPAddress.Parse("8.8.8.8"), 80);
        var lIp = (s.LocalEndPoint as IPEndPoint)?.Address;
        s.Dispose();
        
        return NetworkInterface.GetAllNetworkInterfaces()
                   .FirstOrDefault(nic => nic.GetIPProperties().UnicastAddresses
                       .Any(a => a.Address.Equals(lIp))) 
               ?? throw new Exception("No Active Network Interface Found");
    }
}

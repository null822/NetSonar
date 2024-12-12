using System.Net;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NetSonar;

public class DataProcessor
{
    private readonly TimeSpan[] _sendTimes = new TimeSpan[Constants.Range.Size];
    private readonly uint _globalFirstIpValue = Constants.Range.First.GetUint();

    private readonly Image<Rgb24> _image;
    
    private ArraySegment<PingData> _data = [];
    private bool _isDataNew;
    private readonly Mutex _mutex = new();
    
    private bool _isShuttingDown;
    public bool IsShutDown { get; private set; }
    
    public DataProcessor()
    {
        var size = (int)Math.Ceiling(Math.Sqrt(Constants.Range.Size));
        Console.WriteLine(size);
        _image = new Image<Rgb24>(size, size);
        Task.Run(Run);
    }
    
    public void SetBatch(ArraySegment<PingData> data)
    {
        _mutex.WaitOne();
        
        _data = data;
        _isDataNew = true;
        
        _mutex.ReleaseMutex();
    }
    
    public void SetSendTime(IPAddress address, TimeSpan sendTime)
    {
        _sendTimes[address.GetUint() - _globalFirstIpValue] = sendTime;
    }

    public void Shutdown()
    {
        _isShuttingDown = true;
    }
    
    private void Run()
    {
        do
        {
            _mutex.WaitOne();
            
            if (!_isDataNew)
            {
                _mutex.ReleaseMutex();
                Thread.Sleep(100);
                continue;
            }
            
            foreach (var (ip, receiveTime) in _data)
            {
                var index = ip.GetUint() - _globalFirstIpValue;
                var (x, y) = Deinterleave(index);
                
                var time = receiveTime - _sendTimes[ip.GetUint() - _globalFirstIpValue];
                var scaledTime = 32768 / (time.TotalMilliseconds + 128);
                var brightness = (int)Math.Clamp(scaledTime, 0, 255);
                
                _image[x, y] = new Rgb24((byte)brightness, (byte)brightness, (byte)brightness);
            }
            
            _isDataNew = false;
            _mutex.ReleaseMutex();
            
        } while (!_isShuttingDown);
        
        _image.Save($"map_{Constants.Range}.png");

        IsShutDown = true;
    }
    
    private static (ushort X, ushort Y) Deinterleave(uint zValue)
    {
        var x = zValue & 0b01010101010101010101010101010101;
        var y = (zValue >> 1) & 0b01010101010101010101010101010101;
        
        x = (x | (x >> 1)) & 0b00110011001100110011001100110011;
        x = (x | (x >> 2)) & 0b00001111000011110000111100001111;
        x = (x | (x >> 4)) & 0b00000000111111110000000011111111;
        x = (x | (x >> 8)) & 0b00000000000000001111111111111111;
        
        y = (y | (y >> 1)) & 0b00110011001100110011001100110011;
        y = (y | (y >> 2)) & 0b00001111000011110000111100001111;
        y = (y | (y >> 4)) & 0b00000000111111110000000011111111;
        y = (y | (y >> 8)) & 0b00000000000000001111111111111111;
        
        return ((ushort)x, (ushort)y);
    }
    
}
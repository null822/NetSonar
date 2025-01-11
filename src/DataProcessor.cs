using System.Diagnostics;
using System.Net;
using NetSonar.Packets;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NetSonar;

public class DataProcessor
{
    private readonly IpRange _ipRange;
    private readonly uint _globalFirstIpValue;
    
    private readonly Image<L8> _image;
    private readonly Stopwatch _imageRefreshTimer = new();
    private uint _responseCount;
    
    private byte[] _data = new byte[Constants.ReceiveBufferSize];
    private int _dataLength;
    private readonly MemoryStream _dataStream = new(Constants.ReceiveBufferSize);
    
    private bool _unprocessedData;
    private readonly Mutex _dataMutex = new();
    
    private bool _isShuttingDown;
    public bool IsShutDown { get; private set; }
    
    public DataProcessor(IpRange range)
    {
        _ipRange = range;
        _globalFirstIpValue = _ipRange.First.GetUint();
        
        var sqrt = Math.Sqrt(_ipRange.Size);

        if (sqrt % 1 == 0)
        {
            var size = (int)sqrt;
            _image = new Image<L8>(size, size);
        }
        else
        {
            var width = (int)Math.Sqrt(_ipRange.Size * 2d);
            var height = (int)Math.Sqrt(_ipRange.Size / 2d);
            
            _image = new Image<L8>(width, height);
        }
        
        var t = new Thread(Run)
        {
            Name = "DataProcessor"
        };
        t.Start();
    }
    
    private void Run()
    {
        var targetName = $"[{_ipRange.First}]~{_ipRange.SubnetMaskLength}";
        var counter = 0;
        string name;
        do
        {
            name = $"{targetName}-{counter}";
            counter++;
        } 
        while (File.Exists($"maps/{name}.png"));
        var imagePath = $"maps/{name}.png";
        
        _image.Save(imagePath);
        _imageRefreshTimer.Start();
        do
        {
            if (!_unprocessedData)
            {
                Thread.Sleep(0);
                continue;
            }
            
            _dataMutex.WaitOne();
            
            _dataStream.Position = 0;
            _dataStream.Write(_data);
            _dataStream.Position = 0;
            var length = _dataLength;
            _unprocessedData = false;
            
            _dataMutex.ReleaseMutex();
            
            
            while (_dataStream.Position < length)
            {
                var packet = new IcmpPacket(_dataStream);
                
                var ip = packet.Header.SourceAddress;
                var index = ip.GetUint() - _globalFirstIpValue;
                var (x, y) = Deinterleave(index);
                
                _image[x, y] = new L8(255);
                
                _responseCount++;
            }
            
            if (_imageRefreshTimer.Elapsed.TotalMilliseconds > Constants.MapRefreshRate)
            {
                try
                {
                    _image.Save(imagePath);
                }
                catch (IOException) { }
                
                _imageRefreshTimer.Restart();
            }
            
            StatusBar.SetField("processed-count", $"{_responseCount}");
        } while (!_isShuttingDown || _unprocessedData);

        try
        {
            _image.Save(imagePath);
        }
        catch (IOException) { }
        
        IsShutDown = true;
    }
    
    public bool Submit(ref byte[] data, int dataLength, int timeoutMs = -1)
    {
        if (dataLength == 0)
            return true;

        var endTime =StatusManager.TimerMs + timeoutMs;

        while (_unprocessedData)
        {
            Thread.Sleep(0);
            if (timeoutMs != -1 && StatusManager.TimerMs > endTime)
                break;
        }
        
        if (_unprocessedData)
            return false;
        
        if (!_dataMutex.WaitOne(timeoutMs == -1 ? -1 : (int)(endTime - StatusManager.TimerMs)))
            return false;
        
        (data, _data) = (_data, data);
        _dataLength = dataLength;
        _unprocessedData = true;
        
        _dataMutex.ReleaseMutex();

        return true;
    }
    
    public void Shutdown()
    {
        _isShuttingDown = true;
    }
    
    public static (ushort X, ushort Y) Deinterleave(uint zValue)
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
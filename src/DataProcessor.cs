using System.Diagnostics;
using System.Net;
using NetSonar.Packets;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NetSonar;

public class DataProcessor
{
    // private readonly TimeSpan[] _sendTimes = new TimeSpan[Config.Range.Size];
    private readonly uint _globalFirstIpValue = Config.Range.First.GetUint();
    
    private readonly Image<Rgb24> _image;
    private readonly Stopwatch _imageRefreshTimer = new();
    private uint _responseCount;
    
    // private TimeSpan[] _receiveTimes = new TimeSpan[Constants.PingDataBatchSize];
    
    private byte[] _data = new byte[Constants.ReceiveBufferSize];
    private int _dataLength;
    private readonly MemoryStream _dataStream = new(Constants.ReceiveBufferSize);
    
    
    private bool _unprocessedData;
    private readonly Mutex _mutex = new();
    
    private bool _isShuttingDown;
    public bool IsShutDown { get; private set; }
    
    public DataProcessor()
    {
        var sqrt = Math.Sqrt(Config.Range.Size);

        if (sqrt % 1 == 0)
        {
            var size = (int)sqrt;
            _image = new Image<Rgb24>(size, size);
        }
        else
        {
            var width = (int)Math.Sqrt(Config.Range.Size * 2d);
            var height = (int)Math.Sqrt(Config.Range.Size / 2d);
            
            _image = new Image<Rgb24>(width, height);
        }
        
        var t = new Thread(Run)
        {
            Name = "DataProcessor"
        };
        t.Start();
    }
    
    private void Run()
    {
        var targetName = $"[{Config.Range.First}]~{Config.Range.SubnetMaskLength}";
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
            try
            {
                if (!_unprocessedData)
                {
                    Thread.Sleep(0);
                    continue;
                }

                _mutex.WaitOne();
                
                _dataStream.Position = 0;
                _dataStream.Write(_data);
                _dataStream.Position = 0;

                var length = _dataLength;
                
                _mutex.ReleaseMutex();
                
                // Console.WriteLine($"New Batch ({_dataLength})");
                while (_dataStream.Position < length)
                {
                    var packet = new IcmpPacket(_dataStream);
                    
                    var ip = packet.Header.SourceAddress;
                    // var receiveTime = _receiveTimes[i];
                    
                    // Console.WriteLine(_dataStream.Position);
                    
                    var index = ip.GetUint() - _globalFirstIpValue;
                    var (x, y) = Deinterleave(index);
                    
                    // var time = receiveTime - _sendTimes[ip.GetUint() - _globalFirstIpValue];
                    // var scaledTime = 32768 / (time.TotalMilliseconds + 128);
                    // var brightness = (int)Math.Clamp(scaledTime, 0, 255);
                    var brightness = 255;
                    
                    _image[x, y] = new Rgb24((byte)brightness, (byte)brightness, (byte)brightness);
                    
                    _responseCount++;
                }
                
                if (_imageRefreshTimer.Elapsed.TotalMilliseconds > Constants.MapRefreshRateMs)
                {
                    _image.Save(imagePath);
                    _imageRefreshTimer.Restart();
                }
                
                _unprocessedData = false;
                
                StatusBar.SetField("processed-count", $"{_responseCount}");
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        } while (!_isShuttingDown || _unprocessedData);
        
        _image.Save(imagePath);
        
        IsShutDown = true;
    }
    
    public void Submit(ref byte[] data, int dataLength)
    {
        if (data.Length == 0)
            return;
        
        while (true)
        {
            while (_unprocessedData)
            {
                Thread.Sleep(1);
            }
            
            _mutex.WaitOne();
            
            if (_unprocessedData)
            {
                _mutex.ReleaseMutex();
                continue;
            }
            
            (data, _data) = (_data, data);
            // (receiveTimes, _receiveTimes) = (_receiveTimes, receiveTimes);
            _dataLength = dataLength;
            
            _unprocessedData = true;
            
            _mutex.ReleaseMutex();

            break;
        }
    }
    
    public void SetSendTime(IPAddress address, TimeSpan sendTime)
    {
        // _sendTimes[address.GetUint() - _globalFirstIpValue] = sendTime;
    }
    
    public void Shutdown()
    {
        _isShuttingDown = true;
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
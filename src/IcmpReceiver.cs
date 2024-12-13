using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetSonar;

public class IcmpReceiver
{
    private readonly Socket _icmpSocket;
    private readonly DataProcessor _processor;
    
    private readonly Stopwatch _stopwatch = new();
    private int _staleCounter;
    
    private readonly ArraySegment<byte> _buffer;
    private readonly PingData[] _responses = new PingData[Constants.PingDataBatchSize];
    private int _responseIndex;

    private bool _isShutDown;
    
    public IcmpReceiver(Socket icmpSocket, DataProcessor processor, ArraySegment<byte> buffer)
    {
        _icmpSocket = icmpSocket;
        _processor = processor;
        _buffer = buffer;
        
        var thread = new Task(Run);
        thread.Start();
    }
    
    private void Run()
    {
        _stopwatch.Start();
        
        while (true)
        {
            if (_staleCounter > Constants.ReceiverShutdownWaitMs / Constants.ReceiverWait)
            {
                _isShutDown = true;
                break;
            }
            
            try
            {
                _icmpSocket.Receive(_buffer);

                var packet = new IcmpPacket(_buffer);
                
                // Console.WriteLine(
                    // $"[{_icmpSocket.Available / packet.Header.TotalLength}] {Encoding.UTF8.GetString(packet.Data)} " +
                    // $"@ {Program.Timer.Elapsed.TotalMicroseconds / 1000.0:F4}ms " +
                    // $"({_stopwatch.Elapsed.TotalMicroseconds / 1000.0:F4}ms since last)");
                
                _responses[_responseIndex] = new PingData(
                    packet.Header.SourceAddress,
                    StatusManager.Timer.Elapsed);
                
                _responseIndex++;
                
                if (_responseIndex >= Constants.PingDataBatchSize)
                {
                    _processor.SetBatch(_responses);
                    _responseIndex = 0;
                }

                _staleCounter = 0;
            }
            catch
            {
                _staleCounter++;
                Thread.Sleep(Constants.ReceiverWait);
                continue;
            }

            _stopwatch.Restart();
        }
        
        // process the remaining responses before shutting down
        _processor.SetBatch(_responses[.._responseIndex]);
        
    }
    
    public bool IsShutDown()
    {
        return _isShutDown;
    }
    
}

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetSonar.Packets;

namespace NetSonar;

public class IcmpReceiver
{
    private readonly Socket _icmpSocket;
    private readonly DataProcessor _processor;
    private readonly int _id;
    
    private readonly Stopwatch _stopwatch = new();
    private int _staleCounter;
    
    private readonly ArraySegment<byte> _buffer;
    private readonly PingData[] _responses = new PingData[Constants.PingDataBatchSize];
    private int _responseIndex;

    public bool IsShutDown { get; private set; }
    
    public IcmpReceiver(Socket icmpSocket, DataProcessor processor, ArraySegment<byte> buffer, int id)
    {
        _icmpSocket = icmpSocket;
        _processor = processor;
        _buffer = buffer;
        _id = id;
        
        var t = new Thread(Run)
        {
            Name = $"IcmpReceiver #{id}"
        };
        t.Start();
    }
    
    private void Run()
    {
        var count = 0;
        
        _stopwatch.Start();
        
        while (true)
        {
            if (_staleCounter > Constants.ReceiverShutdownWaitMs / Constants.ReceiverWait)
            {
                break;
            }
            
            try
            {
                _icmpSocket.Receive(_buffer);
                
                var packet = new IcmpPacket(_buffer);
                
                // if (packet.SequenceNumber != Constants.SequenceNumber || packet.Identifier != Constants.Identifier)
                    // continue;
                
                _responses[_responseIndex] = new PingData(
                    packet.Header.SourceAddress,
                    StatusManager.Timer.Elapsed);
                
                _responseIndex++;
                
                if (_responseIndex >= Constants.PingDataBatchSize)
                {
                    _responseIndex = 0;
                    _processor.SetBatch(_responses);
                }
                
                _staleCounter = 0;
            }
            catch (Exception e)
            {
                if (e is SocketException s)
                {
                    var shouldExit = false;
                    switch (s.SocketErrorCode)
                    {
                        case SocketError.TimedOut:
                        {
                            _staleCounter++;
                            Thread.Sleep(Constants.ReceiverWait);
                            continue;
                        }
                        case SocketError.Shutdown:
                        {
                            shouldExit = true;
                            break;
                        }
                    }
                    
                    if (shouldExit) break;
                }
                
                Console.WriteLine(e);
            }

            _stopwatch.Restart();
        }
        
        // process the remaining responses before shutting down
        _processor.SetBatch(_responses[.._responseIndex]);
        
        IsShutDown = true;
    }
    
}

public record PingData(IPAddress Address, TimeSpan ReceiveTime);

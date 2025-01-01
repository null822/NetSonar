using System.Diagnostics;
using System.Net.Sockets;

namespace NetSonar;

public class IcmpReceiver
{
    private readonly Socket _icmpSocket;
    private readonly DataProcessor _processor;
    private readonly BatchSubmitter _submitter;
    private readonly int _id;
    
    private readonly byte[][] _buffers = new byte[Constants.ReceiveBufferCount][];
    private int _currentBufferIndex;
    
    // private TimeSpan[] _receiveTimes = new TimeSpan[Constants.PingDataBatchSize];
    
    private int _responseIndex;
    
    private int _batchCount;
    
    private bool _isStale;
    private TimeSpan _shutdownTime;
    
    private int _prevBatchCount;
    private int _prevResponseIndex;

    private int _maxReceiverFill;
    private int _receiverFill;
    
    private TimeSpan _statusUpdateTimeOffset;

    private bool _canShutDown;
    public bool IsShutDown { get; private set; }
    
    public IcmpReceiver(Socket icmpSocket, DataProcessor processor, int id)
    {
        _icmpSocket = icmpSocket;
        _processor = processor;
        _id = id;

        for (var i = 0; i < Constants.ReceiveBufferCount; i++)
        {
            _buffers[i] = new byte[Constants.ReceiveBufferSize];
        }
        
        _submitter = new BatchSubmitter(processor, _buffers, id);
        
        var t = new Thread(Run)
        {
            Name = $"IcmpReceiver #{id}"
        };
        t.Start();
    }
    
    private void Run()
    {
        // var outValue = new byte[4];
        int read;
        
        while (true)
        {
            try
            {
                if ((StatusManager.Timer.Elapsed - _statusUpdateTimeOffset).TotalMilliseconds
                    >= Constants.ReceiverStatusRefreshRateMs)
                {
                    UpdateStatusBar();
                    
                    if (_canShutDown && _isStale && _shutdownTime < StatusManager.Timer.Elapsed)
                    {
                        break;
                    }
                }
                
                // Check how many bytes have been received.
                // _icmpSocket.IOControl(0x4004667F, null, outValue);
                // var available = (int)BitConverter.ToUInt32(outValue);
                
                
                
                if (Constants.ReceiveBufferSize * 0.75 < _responseIndex)
                {
                    // submit the buffer for submitting, and switch over to the next one
                    _submitter.Submit(_currentBufferIndex, _responseIndex);
                    _currentBufferIndex = (_currentBufferIndex + 1) % Constants.ReceiveBufferCount;
                    _responseIndex = 0;
                    
                    _batchCount++;
                }

                try
                {
                    read = _icmpSocket.Receive(
                        _buffers[_currentBufferIndex],
                        _responseIndex,
                        Constants.ReceiveBufferSize - _responseIndex,
                        SocketFlags.None);
                }
                catch
                {
                    if (!_isStale)
                    {
                        _shutdownTime = StatusManager.Timer.Elapsed + TimeSpan.FromMilliseconds(Constants.ReceiverShutdownWaitMs);
                        _isStale = true;
                    }
                    continue;
                }
                
                _responseIndex += read;
                
                _receiverFill = Math.Max(_receiverFill, _responseIndex);
                
                if (read > Constants.ReceiveBufferSize - _responseIndex)
                {
                    throw new ReceiverBufferOverflowException("Primary");
                }

                
                _isStale = false;
            }
            catch (Exception e)
            {
                if (e is SocketException s)
                {
                    var shouldExit = false;
                    switch (s.SocketErrorCode)
                    {
                        case SocketError.WouldBlock:
                        case SocketError.TimedOut:
                        {
                            if (_canShutDown && !_isStale)
                            {
                                _isStale = true;
                                _shutdownTime = StatusManager.Timer.Elapsed + TimeSpan.FromMilliseconds(Constants.ReceiverShutdownWaitMs);
                            }
                            continue;
                        }
                        case SocketError.Shutdown:
                        {
                            shouldExit = true;
                            break;
                        }
                        default:
                        {
                            Console.WriteLine(e);
                            break;
                        }
                    }
                    
                    if (shouldExit) break;
                }

                if (e is ReceiverBufferOverflowException b)
                {
                    Console.WriteLine(b);
                    break;
                }
                
                Console.WriteLine(e);
            }
            
        }
        
        // process the remaining responses before shutting down
        _processor.Submit(ref _buffers[_currentBufferIndex], _responseIndex);
        _batchCount++;
        
        _submitter.Shutdown();
        IsShutDown = true;
    }

    private void UpdateStatusBar()
    {
        _maxReceiverFill = Math.Max(_maxReceiverFill, _receiverFill);
                    
        StatusBar.SetField("batch-count", $"{_batchCount}");
        StatusBar.SetField("receiver-fill", $"{_receiverFill / (float)Constants.ReceiveBufferSize:P0}");
        StatusBar.SetField("max-receiver-fill", $"{_maxReceiverFill / (float)Constants.ReceiveBufferSize:P0}");
        _receiverFill = 0;
        
        _prevBatchCount = _batchCount;
        _prevResponseIndex = _responseIndex;
                    
        _statusUpdateTimeOffset = StatusManager.Timer.Elapsed;
    }
    
    public void EnableShutdown()
    {
        _canShutDown = true;
    }
}

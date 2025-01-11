using System.Net.Sockets;

namespace NetSonar.PacketHandlers;

public class IcmpReceiver
{
    /// <summary>
    /// The socket to receive response <see cref="Packets.IcmpPacket"/>s from
    /// </summary>
    private readonly Socket _icmpSocket;
    /// <summary>
    /// The <see cref="BatchSubmitter"/> used to submit received <see cref="Packets.IcmpPacket"/>s
    /// </summary>
    private readonly BatchSubmitter _submitter;
    
    /// <summary>
    /// The receiving buffer
    /// </summary>
    private byte[] _buffer;
    /// <summary>
    /// The current index within the current receiving buffer
    /// </summary>
    private int _responseIndex;
    
    
    private int _batchCount;
    
    private bool _isStale;
    private TimeSpan _shutdownTime;
    
    private int _maxReceiverFill;
    private int _receiverFill;

    private bool _canShutDown;
    public bool IsShutDown { get; private set; }
    private TimeSpan _prevStatusUpdate;
    
    public IcmpReceiver(Socket icmpSocket, DataProcessor processor, int id)
    {
        _icmpSocket = icmpSocket;
        _submitter = new BatchSubmitter(processor, id);
        _buffer = new byte[Constants.ReceiveBufferSize];
        
        var t = new Thread(Run)
        {
            Name = $"IcmpReceiver #{id}"
        };
        t.Start();
    }
    
    private void Run()
    {
        while (true)
        {
            
            if (StatusManager.ShouldUpdateStatusBar(ref _prevStatusUpdate))
            {
                UpdateStatusBar();

                if (_canShutDown && _isStale && _shutdownTime < StatusManager.Timer.Elapsed)
                {
                    break;
                }
            }
            
            if (Constants.ReceiveBufferSize * 0.80 < _responseIndex)
            {
                _receiverFill = Math.Max(_receiverFill, _responseIndex);
                
                // swap in a new receiving buffer, sending the old one off for processing
                _submitter.SwapBuffers(ref _buffer, _responseIndex);
                _responseIndex = 0;
                
                if (_icmpSocket.Available == Constants.ReceiveBufferSize)
                {
                    StatusBar.SyncShutdown();
                    throw new ReceiverBufferOverflowException("Primary");
                }
                
                _batchCount++;
            }
            
            var read = _icmpSocket.Receive(
                _buffer,
                _responseIndex,
                Constants.ReceiveBufferSize - _responseIndex,
                SocketFlags.None,
                out var error);

            switch (error)
            {
                case SocketError.WouldBlock:
                {
                    if (!_isStale)
                    {
                        _shutdownTime = StatusManager.Timer.Elapsed +
                                        TimeSpan.FromMilliseconds(Constants.ReceiverShutdownWait);
                        _isStale = true;
                    }
                    continue;
                }
            }
            
            _responseIndex += read;
            
            if (read > Constants.ReceiveBufferSize - _responseIndex)
            {
                StatusBar.SyncShutdown();
                throw new ReceiverBufferOverflowException("Primary");
            }
            
            _isStale = false;
        }
        
        // process the remaining responses before shutting down
        _submitter.SwapBuffers(ref _buffer, _responseIndex);
        _batchCount++;
        
        UpdateStatusBar();
        
        _submitter.Shutdown();
        IsShutDown = true;
    }

    private void UpdateStatusBar()
    {
        _maxReceiverFill = Math.Max(_maxReceiverFill, _receiverFill);
        
        StatusBar.SetField("batch-count", $"{_batchCount}");
        StatusBar.SetField("receiver-fill", $"{_receiverFill / (float)Constants.ReceiveBufferSize:P}");
        StatusBar.SetField("max-receiver-fill", $"{_maxReceiverFill / (float)Constants.ReceiveBufferSize:P}");
        _receiverFill = 0;
    }
    
    public void EnableShutdown()
    {
        _canShutDown = true;
    }
}

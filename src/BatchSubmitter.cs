
using System.Diagnostics;

namespace NetSonar;

public class BatchSubmitter
{
    private readonly DataProcessor _processor;
    
    /// <summary>
    /// The receiving buffers
    /// </summary>
    private readonly byte[][] _buffers = new byte[Constants.ReceiveBufferCount][];
    
    private readonly int[] _bufferLengths = new int[Constants.ReceiveBufferCount];
    /// <summary>
    /// Which receiving buffer is currently in use
    /// </summary>
    private int _currentBufferIndex;

    /// <summary>
    /// The index of the next buffer to be processed
    /// </summary>
    private int _nextProcessBuffer;
    
    private readonly Mutex _mutex = new();
    private volatile bool _isSubmitting;
    
    private bool _isShuttingDown;
    public bool IsShutDown { get; private set; }
    private TimeSpan _prevStatusUpdate;
    
    public BatchSubmitter(DataProcessor processor, int id)
    {
        for (var i = 0; i < Constants.ReceiveBufferCount; i++)
        {
            _buffers[i] = new byte[Constants.ReceiveBufferSize];
        }
        
        _processor = processor;
        
        var t = new Thread(Run)
        {
            Name = $"BatchSubmitter #{id}"
        };
        t.Start();
    }

    private void Run()
    {
        _mutex.WaitOne();
        while (!_isShuttingDown)
        {
            if (StatusManager.ShouldUpdateStatusBar(ref _prevStatusUpdate))
            {
                StatusBar.SetField("processor-load", $"{_bufferLengths.Count(v => v != 0) / (float)_bufferLengths.Length:P}");
            }
            
            if (_isSubmitting)
            {
                _mutex.ReleaseMutex();
                // interrupt to let receiver thread run and get the mutex
                Thread.Sleep(0);
                _mutex.WaitOne();
            }
            
            var index = -1;
            for (var o = 0; o < Constants.ReceiveBufferCount; o++)
            {
                var i = (_nextProcessBuffer + o) % Constants.ReceiveBufferCount;
                if (_bufferLengths[i] > 0)
                {
                    index = i;
                    _nextProcessBuffer = (_nextProcessBuffer + o + 1) % Constants.ReceiveBufferCount;
                    break;
                }
            }
            if (index == -1)
                continue;
            
            if (_processor.Submit(ref _buffers[index], _bufferLengths[index], Constants.BatchSubmitTimeout))
                _bufferLengths[index] = 0;
            
            Thread.Sleep(0);
        }
        IsShutDown = true;
    }
    
    public void SwapBuffers(ref byte[] buffer, int length)
    {
        _isSubmitting = true;
        _mutex.WaitOne();
        
        // find the next free buffer
        var index = -1;
        for (var o = 0; o < Constants.ReceiveBufferCount; o++)
        {
            var i = (_currentBufferIndex + o) % Constants.ReceiveBufferCount;
            if (_bufferLengths[i] == 0)
            {
                index = i;
                break;
            }
        }
        if (index == -1)
        {
            StatusBar.SyncShutdown();
            throw new ReceiverBufferOverflowException("Secondary");
        }
        
        // mark the current buffer as full
        _bufferLengths[index] = length;
        
        // use that buffer
        _currentBufferIndex = index;
        (buffer, _buffers[index]) = (_buffers[index], buffer);

        _isSubmitting = false;
        _mutex.ReleaseMutex();
    }
    
    public void Shutdown()
    {
        _isShuttingDown = true;
    }
}

public class ReceiverBufferOverflowException(string name) : Exception($"{name} Receiver Buffers Overflowed");
using System.Collections;

namespace NetSonar;

public class BatchSubmitter
{
    private readonly DataProcessor _processor;
    private readonly byte[][] _buffers;
    private readonly int[] _bufferLengths = new int[Constants.ReceiveBufferCount];
    
    private readonly Mutex _mutex = new();
    private bool _isSubmitting;
    
    private bool _isShuttingDown;
    public bool IsShutDown { get; private set; }

    public BatchSubmitter(DataProcessor processor, byte[][] buffers, int id)
    {
        _processor = processor;
        _buffers = buffers;
        
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
            var index = -1;

            if (_isSubmitting)
            {
                _mutex.ReleaseMutex();
                Thread.Sleep(0);
                _mutex.WaitOne();
            }

            for (var i = 0; i < Constants.ReceiveBufferCount; i++)
            {
                if (_bufferLengths[i] > 0)
                {
                    index = i;
                    break;
                }
            }
            if (index == -1)
                continue;
            
            _processor.Submit(ref _buffers[index], _bufferLengths[index]);
            _bufferLengths[index] = 0;
        }

        IsShutDown = true;
    }
    
    public void Submit(int index, int length)
    {
        if (_bufferLengths[index] != 0)
            throw new ReceiverBufferOverflowException("Secondary");

        _isSubmitting = true;
        _mutex.WaitOne();
        _bufferLengths[index] = length;
        _mutex.ReleaseMutex();
    }
    
    public void Shutdown()
    {
        _isShuttingDown = true;
    }
}

public class ReceiverBufferOverflowException(string name) : Exception($"{name} Receiver Buffers Overflowed");
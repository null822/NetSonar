﻿using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetSonar;

public class IcmpReceiver
{
    private readonly Socket _icmpSocket;
    private readonly Stopwatch _stopwatch = new();
    
    public IcmpReceiver(Socket icmpSocket)
    {
        _icmpSocket = icmpSocket;
        
        var thread = new Task(Run);
        thread.Start();
    }
    
    private void Run()
    {
        _stopwatch.Start();
        
        var buffer = new byte[128];
        
        while (true)
        {
            _icmpSocket.Receive(buffer);
            var packet = new IcmpPacket(buffer, 0);
            
            // Console.WriteLine($"[{_icmpSocket.Available / packet.Header.TotalLength}] {Encoding.UTF8.GetString(packet.Data)} " + 
                              // $"@ {Program.Timer.Elapsed.TotalMicroseconds / 1000.0:F4}ms " +
                              // $"({_stopwatch.Elapsed.TotalMicroseconds / 1000.0:F4}ms since last)");
            
            _stopwatch.Restart();
        }
        
    }
    
}
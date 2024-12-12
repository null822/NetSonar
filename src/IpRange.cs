using System.Collections;
using System.Net;

namespace NetSonar;

public readonly struct IpRange : IEnumerable<IPAddress>
{
    private readonly uint _first;
    private readonly uint _last;
    
    public uint Size => _last - _first + 1;
    public IPAddress First => Get(0);
    public IPAddress Last => Get(Size - 1);
    
    public IpRange(uint first, uint last)
    {
        _first = first;
        _last = last;
    }
    public IpRange(IPAddress start, IPAddress end) : this(start.GetUint(), end.GetUint()) { }
    public IpRange(string start, string end) : this(IPAddress.Parse(start), IPAddress.Parse(end)) { }

    public IpRange(string cidr)
    {
        var slash = cidr.IndexOf('/');
        if (slash == -1)
        {
            _first = _last = IPAddress.Parse(cidr).GetUint();
            return;
        }
        _first = IPAddress.Parse(cidr[..slash]).GetUint();
        
        var subnetMaskNumber = int.Parse(cidr[(slash + 1)..]);
        ArgumentOutOfRangeException.ThrowIfNegative(subnetMaskNumber, nameof(subnetMaskNumber));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(subnetMaskNumber, 32, nameof(subnetMaskNumber));
        
        var size = 0x1u << 32 - subnetMaskNumber;
        _last = _first + size - 1;
    }
    
    public IPAddress Get(uint index)
    {
        var v = _first + index;
        return new IPAddress(v.SwapEndian());
    }

    public IpRange Split(uint numerator, uint denominator)
    {
        var size = Size / denominator;
        var first = _first + numerator * size;
        return new IpRange(first, first + size - 1);
    }

    public string GetCidr()
    {
        var subnetMask = 32-(int)Math.Round(Math.Log2(_last - _first));
        return $"{new IPAddress(_first.GetBytes())}/{subnetMask}";
    }
    
    public IEnumerator<IPAddress> GetEnumerator()
    {
        return new Enumerator(this);
    }
    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public override string ToString()
    {
        return $"[{First}]-[{Last}]";
    }
    
    public class Enumerator(IpRange range) : IEnumerator<IPAddress>
    {
        private long _index = -1;
        
        public IPAddress Current => _index >= 0 ? range.Get((uint)_index) : throw new Exception("asdf");
        object? IEnumerator.Current => Current;
        
        public bool MoveNext()
        {
            if (_index >= range.Size - 1)
            {
                // _index = range.Size - 1;
                // Console.WriteLine(_index);
                return false;
            }
            
            _index++;
            return true;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose()
        {
            
        }
    }
}
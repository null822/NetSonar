using System.Text;

namespace NetSonar;

public static class StatusBar
{
    private static readonly Dictionary<string, string> Fields = [];
    private static readonly List<StatusBarLine> Lines = [];
    private static int _maxLinePos;
    
    public static void SetField(string name, string value)
    {
        Fields[name] = value;
    }
    
    public static void CreateField(string field, string initialValue = "")
    {
        Fields.Add(field, initialValue);
    }
    
    public static void SetLine(int index, string format, params string[] fields)
    {
        foreach (var field in fields)
        {
            if (!Fields.ContainsKey(field))
                CreateField(field);
        }
        
        _maxLinePos = Math.Max(_maxLinePos, index);
        Lines.Add(new StatusBarLine(index, format, fields));
        
        var pos = Console.CursorTop;
        Console.SetCursorPosition(0, Math.Max(pos, _maxLinePos) + 1);
    }
    
    public static void Run()
    {
        while (true)
        {
            foreach (var line in Lines)
            {
                var prevPos = Console.GetCursorPosition();
                Console.SetCursorPosition(0, line.Index);
                Console.WriteLine(line.GetText());
                Console.SetCursorPosition(prevPos.Left, prevPos.Top);
            }
            
            Thread.Sleep(Constants.StatusBarRefreshRateMs);
        }
    }
    
    private readonly struct StatusBarLine
    {
        public readonly int Index;
        private readonly StringBuilder _base = new();
        private readonly FieldReference[] _fieldReferences;
        
        public StatusBarLine(int index, string template, params string[] values)
        {
            Index = index;
            var fieldReferences = new List<FieldReference>();
            
            var isEscaped = false;
            var isFormat = false;
            var formatIndex = -1;
            var formatStart = 0;
            var format = new StringBuilder();
            
            foreach (var c in template)
            {
                if (c == '\\')
                {
                    if (!isEscaped) isEscaped = true;
                    else _base.Append(c);
                    
                    continue;
                }
                
                if (!isEscaped && c == '%')
                {
                    isFormat = true;
                    
                    formatIndex++;
                    
                    if (formatIndex >= values.Length)
                        throw new Exception($"Not enough field references for all formats in {nameof(StatusBar)} line");
                    
                    formatStart = _base.Length;
                    
                    continue;
                }
                
                if (isFormat)
                {
                    format.Append(c);
                    
                    if (c is 'L' or 'R' or 'C')
                    {
                        var length = int.Parse(format.ToString()[..^1]);
                        
                        _base.Capacity += length;
                        for (var i = 0; i < length; i++)
                        {
                            _base.Append(' ');
                        }
                        
                        var align = format[^1] switch
                        {
                            'L' => TextAlign.Left,
                            'R' => TextAlign.Right,
                            'C' => TextAlign.Center,
                            _ => TextAlign.Left
                        };
                        
                        fieldReferences.Add(new FieldReference(formatStart, length, values[formatIndex], align));
                        
                        format.Clear();
                        
                        isFormat = false;
                    }
                    
                    continue;
                }
                
                
                
                _base.Append(c);
                isEscaped = false;
            }
            
            _fieldReferences = fieldReferences.ToArray();
        }

        public string GetText()
        {
            foreach (var fieldReference in _fieldReferences)
            {
                var value = Fields[fieldReference.FieldName];

                var offset = fieldReference.Align switch
                {
                    TextAlign.Left => 0,
                    TextAlign.Right => value.Length - fieldReference.Length,
                    TextAlign.Center => (value.Length - fieldReference.Length) / 2,
                    _ => 0
                };
                
                for (var i = 0; i < fieldReference.Length; i++)
                {
                    var baseIndex = fieldReference.Index + i;
                    var valueIndex = i + offset;
                    
                    if (valueIndex >= 0 && valueIndex < value.Length)
                        _base[baseIndex] = value[valueIndex];
                    else
                        _base[baseIndex] = ' ';
                    
                }
            }
            
            return _base.ToString();
        }
        
        private record FieldReference(int Index, int Length, string FieldName, TextAlign Align);

        private enum TextAlign : byte
        {
            Left,
            Right,
            Center
        }
    }
}

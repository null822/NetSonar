namespace NetSonar;

public static class StatusBar
{
    private static readonly Dictionary<string, string> Fields = [];
    private static readonly Dictionary<int, StatusBarLine> Lines = [];
    private static int _maxLinePos;
    
    public static void SetField(string field, string value)
    {
        if (!Fields.TryAdd(field, value))
            Fields[field] = value;
    }
    
    public static void CreateField(string field)
    {
        Fields.Add(field, "");
    }
    
    public static void SetLine(int index, StatusBarLine line)
    {
        _maxLinePos = Math.Max(_maxLinePos, index);
        Lines.Add(index, line);
        
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
                Console.SetCursorPosition(0, line.Key);
                Console.WriteLine(line.Value.Invoke(Fields).PadRight(Console.WindowWidth));
                Console.SetCursorPosition(prevPos.Left, prevPos.Top);
            }
            
            Thread.Sleep(Constants.StatusBarRefreshRateMs);
        }
    }
    
    public delegate string StatusBarLine(Dictionary<string, string> fields);
}
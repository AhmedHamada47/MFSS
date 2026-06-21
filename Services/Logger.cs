namespace MFSS.Services;

public class Logger
{
    private readonly string _logPath;
    private readonly object _lock = new();

    public Logger(string name)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        _logPath = Path.Combine(desktop, $"mfss-{name.Replace(" ", "-")}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    }

    public string LogPath => _logPath;

    public void Info(string m) => Log(m, ConsoleColor.White);
    public void Success(string m) => Log(m, ConsoleColor.Green);
    public void Error(string m) => Log(m, ConsoleColor.Red);
    public void Warning(string m) => Log(m, ConsoleColor.Yellow);
    public void Header(string m) => Log(m, ConsoleColor.Cyan);

    private void Log(string msg, ConsoleColor color)
    {
        lock (_lock)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(msg);
            Console.ForegroundColor = old;
            try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] {SecretMasker.MaskConnectionString(msg)}\n"); } catch { }
        }
    }
}

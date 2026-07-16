using MFSS.Abstractions;

namespace MFSS.Services;

public class Logger : ILogger
{
    private readonly string _migrationName;
    private readonly string _logPath;
    private readonly StreamWriter _writer;

    public string LogPath => _logPath;

    public Logger(string migrationName)
    {
        _migrationName = migrationName;
        var logDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        Directory.CreateDirectory(logDir);
        _logPath = Path.Combine(logDir, $"{migrationName}_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        _writer = new StreamWriter(_logPath, append: true) { AutoFlush = true };
    }

    public void Header(string msg) => Write(msg, ConsoleColor.Cyan);
    public void Info(string msg) => Write(msg, ConsoleColor.White);
    public void Success(string msg) => Write(msg, ConsoleColor.Green);
    public void Warning(string msg) => Write(msg, ConsoleColor.Yellow);
    public void Error(string msg) => Write(msg, ConsoleColor.Red);

    public void Progress(string msg)
    {
        int width;
        try { width = Console.WindowWidth; } catch { width = 80; }
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"\r{msg}{new string(' ', Math.Max(0, width - msg.Length - 1))}\r");
        Console.ForegroundColor = prev;
    }

    private void Write(string msg, ConsoleColor color)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(msg);
        Console.ForegroundColor = prev;
        _writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}

namespace MFSS.Abstractions;

public interface ILogger : IDisposable
{
    string LogPath { get; }
    void Header(string msg);
    void Info(string msg);
    void Success(string msg);
    void Warning(string msg);
    void Error(string msg);
    void Progress(string msg);
}

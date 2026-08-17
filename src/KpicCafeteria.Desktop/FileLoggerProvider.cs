using System.IO;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop;

/// <summary>
/// 간단한 파일 로깅 프로바이더. 앱 크래시 원인 파악용.
/// </summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly Lock _lock = new();

    public FileLoggerProvider(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_path, _lock, categoryName);

    public void Dispose() { }

    private sealed class FileLogger(string path, Lock lockObj, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            try
            {
                var msg = $"[{DateTime.Now:HH:mm:ss}] [{logLevel}] [{category}] {formatter(state, exception)}";
                if (exception is not null)
                    msg += $"\n{exception}";
                lock (lockObj)
                {
                    File.AppendAllText(path, msg + "\n");
                }
            }
            catch { }
        }
    }
}

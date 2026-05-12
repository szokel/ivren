using System.Collections.Concurrent;
using System.Text;

namespace Ivren.Service;

public sealed class DailyFileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, DailyFileLogger> _loggers = new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();
    private readonly string _logFolderPath;

    public DailyFileLoggerProvider(string logFolderPath)
    {
        _logFolderPath = logFolderPath;
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new DailyFileLogger(name, this));

    public void Dispose()
    {
        _loggers.Clear();
    }

    private void Write(
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        if (string.IsNullOrWhiteSpace(_logFolderPath))
        {
            return;
        }

        try
        {
            if (!Directory.Exists(_logFolderPath))
            {
                return;
            }

            var logFilePath = Path.Combine(_logFolderPath, $"ivren-service-{DateTime.Now:yyyy-MM-dd}.log");
            var line = FormatLine(categoryName, logLevel, eventId, message, exception);

            lock (_syncRoot)
            {
                File.AppendAllText(logFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // File logging must never bring down the worker. Console logging remains active.
        }
    }

    private static string FormatLine(
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        var builder = new StringBuilder();
        builder.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
        builder.Append(" [");
        builder.Append(logLevel);
        builder.Append("] ");
        builder.Append(categoryName);
        if (eventId.Id != 0)
        {
            builder.Append(" (");
            builder.Append(eventId.Id);
            builder.Append(')');
        }

        builder.Append(": ");
        builder.Append(message.ReplaceLineEndings(" "));

        if (exception is not null)
        {
            builder.Append(" Exception=");
            builder.Append(exception.ToString().ReplaceLineEndings(" "));
        }

        return builder.ToString();
    }

    private sealed class DailyFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly DailyFileLoggerProvider _provider;

        public DailyFileLogger(string categoryName, DailyFileLoggerProvider provider)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.Write(_categoryName, logLevel, eventId, formatter(state, exception), exception);
        }
    }
}

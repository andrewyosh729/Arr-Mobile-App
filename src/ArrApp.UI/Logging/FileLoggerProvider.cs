using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;

namespace ArrApp.UI.Logging;

/// <summary>
/// Minimal daily-rolling file logger. Writes synchronously so fatal errors reach disk before the process dies.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider {
    private const int RetainDays = 7;

    private readonly string _directory;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string directory) {
        _directory = directory;
        Directory.CreateDirectory(directory);
        Prune();
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    public void Dispose() { }

    private void Write(string line) {
        var path = Path.Combine(_directory, $"arr-{DateTime.Now:yyyyMMdd}.log");
        try {
            lock (_gate)
                File.AppendAllText(path, line + Environment.NewLine);
        } catch (IOException) {
            // Logging must never take the app down.
        } catch (UnauthorizedAccessException) {
        }
    }

    private void Prune() {
        try {
            foreach (var file in Directory.EnumerateFiles(_directory, "arr-*.log"))
                if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-RetainDays))
                    File.Delete(file);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    private sealed class FileLogger(string category, FileLoggerProvider owner) : ILogger {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) {
            if (!IsEnabled(logLevel))
                return;

            var line = $"{DateTime.Now:HH:mm:ss.fff} [{logLevel}] {category}: {formatter(state, exception)}";
            if (exception is not null)
                line += Environment.NewLine + exception;
            owner.Write(line);
        }
    }
}

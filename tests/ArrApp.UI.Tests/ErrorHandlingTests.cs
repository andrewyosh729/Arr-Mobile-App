using ArrApp.UI.Errors;
using ArrApp.UI.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArrApp.UI.Tests;

public class ErrorHandlingTests {
    [Fact]
    public void Toast_Shows_Then_Dismisses_Itself() {
        var toasts = new ToastService();

        toasts.Show("Saved", ToastKind.Success, TimeSpan.FromMilliseconds(50));
        Assert.Equal("Saved", toasts.Current?.Message);
        Assert.True(toasts.Current!.IsSuccess);

        SpinWait.SpinUntil(() => toasts.Current is null, TimeSpan.FromSeconds(2));
        Assert.Null(toasts.Current);
    }

    [Fact]
    public void Newer_Toast_Is_Not_Dismissed_By_Older_Timer() {
        var toasts = new ToastService();

        toasts.Show("first", duration: TimeSpan.FromMilliseconds(50));
        toasts.Show("second", duration: TimeSpan.FromSeconds(30));
        Thread.Sleep(300);

        Assert.Equal("second", toasts.Current?.Message);
    }

    [Fact]
    public void Dismiss_Clears_Current_Toast() {
        var toasts = new ToastService();
        toasts.Show("x", duration: TimeSpan.FromSeconds(30));

        toasts.Dismiss();

        Assert.Null(toasts.Current);
    }

    [Fact]
    public void ErrorReporter_Shows_Friendly_Toast_And_Logs_Exception() {
        var toasts = new ToastService();
        var logs = new CapturingLogger<ErrorReporter>();
        var reporter = new ErrorReporter(logs, toasts);
        var boom = new InvalidOperationException("boom");

        reporter.Report(boom);

        Assert.True(toasts.Current!.IsError);
        Assert.Equal(ErrorReporter.DefaultMessage, toasts.Current.Message);
        Assert.Same(boom, logs.LastException);
    }

    [Fact]
    public void ErrorReporter_Uses_Caller_Message_When_Given() {
        var toasts = new ToastService();
        var reporter = new ErrorReporter(NullLogger<ErrorReporter>.Instance, toasts);

        reporter.Report(new Exception("x"), "Couldn't reach the server");

        Assert.Equal("Couldn't reach the server", toasts.Current!.Message);
    }

    [Fact]
    public void FileLogger_Writes_Messages_And_Exceptions_To_Daily_File() {
        var dir = Path.Combine(Path.GetTempPath(), "arrapp-log-test-" + Guid.NewGuid().ToString("N"));
        try {
            var logger = new FileLoggerProvider(dir).CreateLogger("Test");

            logger.LogInformation("hello {Name}", "world");
            logger.LogError(new InvalidOperationException("kaboom"), "failed");

            var text = File.ReadAllText(Directory.GetFiles(dir, "arr-*.log").Single());
            Assert.Contains("[Information] Test: hello world", text);
            Assert.Contains("[Error] Test: failed", text);
            Assert.Contains("kaboom", text);
        } finally {
            Directory.Delete(dir, true);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T> {
        public Exception? LastException { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => LastException = exception;
    }
}

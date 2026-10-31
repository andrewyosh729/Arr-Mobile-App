using System;
using Microsoft.Extensions.Logging;

namespace ArrApp.UI.Errors;

/// <summary>Single place to turn an exception into a log entry plus a friendly on-screen message.</summary>
public interface IErrorReporter {
    void Report(Exception exception, string? userMessage = null);
}

public sealed class ErrorReporter(ILogger<ErrorReporter> logger, IToastService toasts) : IErrorReporter {
    public const string DefaultMessage = "Something went wrong. Details were saved to the log.";

    public void Report(Exception exception, string? userMessage = null) {
        logger.LogError(exception, "Reported error: {Message}", userMessage ?? exception.Message);
        toasts.Show(userMessage ?? DefaultMessage, ToastKind.Error);
    }
}

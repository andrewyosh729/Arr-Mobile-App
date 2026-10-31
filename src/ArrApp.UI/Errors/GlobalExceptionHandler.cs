using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace ArrApp.UI.Errors;

public static class GlobalExceptionHandler {
    public static void Install(ILogger logger, IErrorReporter reporter) {
        // Last-chance: the process is going down, so just make sure it is on disk.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.LogCritical(e.ExceptionObject as Exception, "Unhandled exception (terminating: {Terminating})", e.IsTerminating);

        // Forgotten fire-and-forget tasks: report and carry on.
        TaskScheduler.UnobservedTaskException += (_, e) => {
            reporter.Report(e.Exception);
            e.SetObserved();
        };

        // Exceptions thrown from UI-thread callbacks (commands, event handlers): report instead of crashing.
        Dispatcher.UIThread.UnhandledException += (_, e) => {
            reporter.Report(e.Exception);
            e.Handled = true;
        };
    }
}

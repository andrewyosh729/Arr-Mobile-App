using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArrApp.UI.Errors;

public enum ToastKind {
    Info,
    Success,
    Error,
}

public sealed record Toast(string Message, ToastKind Kind) {
    public bool IsError => Kind == ToastKind.Error;
    public bool IsSuccess => Kind == ToastKind.Success;
}

/// <summary>One transient message at a time (snackbar). Safe to call from any thread.</summary>
public interface IToastService {
    void Show(string message, ToastKind kind = ToastKind.Info, TimeSpan? duration = null);
    void Dismiss();
}

public sealed class ToastService : ObservableObject, IToastService {
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(4);

    // Captured at construction (the UI thread in the app) so property changes always happen there.
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private CancellationTokenSource? _cts;
    private Toast? _current;

    public Toast? Current {
        get => _current;
        private set => SetProperty(ref _current, value);
    }

    public void Show(string message, ToastKind kind = ToastKind.Info, TimeSpan? duration = null) {
        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _cts, cts)?.Cancel();
        var toast = new Toast(message, kind);
        Post(() => Current = toast);
        _ = DismissAfterAsync(toast, duration ?? DefaultDuration, cts.Token);
    }

    public void Dismiss() {
        Interlocked.Exchange(ref _cts, null)?.Cancel();
        Post(() => Current = null);
    }

    private async Task DismissAfterAsync(Toast toast, TimeSpan delay, CancellationToken token) {
        try {
            await Task.Delay(delay, token).ConfigureAwait(false);
        } catch (OperationCanceledException) {
            return;
        }

        Post(() => {
            if (ReferenceEquals(Current, toast))
                Current = null;
        });
    }

    private void Post(Action action) {
        if (_ui is null || SynchronizationContext.Current == _ui)
            action();
        else
            _ui.Post(_ => action(), null);
    }
}

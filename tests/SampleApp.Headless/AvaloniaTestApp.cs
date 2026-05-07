using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;

namespace SampleApp.Headless;

/// <summary>
/// Bootstraps the Avalonia headless platform once for the test process and
/// exposes a helper to invoke arbitrary code on the UI thread synchronously.
/// </summary>
internal static class AvaloniaTestApp
{
    private static readonly object _lock = new();
    private static bool _started;

    public static void Ensure()
    {
        lock (_lock)
        {
            if (_started) return;
            AppBuilder.Configure<global::SampleApp.App>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
                .SetupWithoutStarting();
            _started = true;
        }
    }

    public static T RunOnUi<T>(Func<T> action)
    {
        Ensure();
        return Dispatcher.UIThread.Invoke(action);
    }

    public static void RunOnUi(Action action)
    {
        Ensure();
        Dispatcher.UIThread.Invoke(action);
    }
}

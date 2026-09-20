using System;
using System.Diagnostics;
using System.Threading;
using Avalonia;
using Avalonia.Threading;
using EveOPreview.UI;

namespace EveOPreview.Tests.Infrastructure;

/// <summary>Runs the real Windows Avalonia platform on a worker's isolated STA desktop.</summary>
internal static class TestAvalonia
{
    public static void Initialize()
    {
        if (Application.Current != null) return;
        AppBuilder.Configure<WorkspaceApp>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
    }

    public static void Pump()
    {
        // Forms is used only for legacy/native source fixtures. Its message drain
        // also dispatches Windows messages for Avalonia; all application work uses
        // the Avalonia dispatcher, including its timers and synchronization context.
        System.Windows.Forms.Application.DoEvents();
        Dispatcher.UIThread.RunJobs();
    }

    public static void PumpUntil(Func<bool> completed, int timeoutMilliseconds = 5000)
    {
        var watch = Stopwatch.StartNew();
        while (!completed())
        {
            if (watch.ElapsedMilliseconds >= timeoutMilliseconds)
                throw new TimeoutException("Avalonia worker did not reach the expected state.");
            Pump();
            Thread.Sleep(1);
        }
        Pump();
    }
}

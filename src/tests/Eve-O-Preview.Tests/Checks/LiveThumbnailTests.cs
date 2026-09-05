using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.View;
using Gma.System.MouseKeyHook;
using MediatR;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class LiveThumbnailTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("HealthyRefresh")]
    [InlineData("BorderAndZOrder")]
    [InlineData("FailedRefreshRecovers")]
    public Task PreservesLiveImageUntilRecoveryIsNeeded(string scenario) =>
        PrivateDesktopRunner.RunAsync("live-" + scenario, output);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnregisteredDwmThumbnailReportsFailedUpdate(bool compositionEnabled)
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        var windowManager = Stub.Create<IWindowManager>((method, args) =>
            method.Name == "get_IsCompositionEnabled" ? compositionEnabled : Stub.Default(method.ReturnType));
        var thumbnail = (IDwmThumbnail)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType(
            "EveOPreview.Services.Implementation.DwmThumbnail"), windowManager, logger);
        Assert.False(thumbnail.Update());
    }

    internal static void RunScenario(string scenario)
    {
        var assembly = typeof(ThumbnailView).Assembly;
        var config = (IThumbnailConfiguration)Activator.CreateInstance(
            assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
        using var logger = new LoggerConfiguration().CreateLogger();
        int registrations = 0, unregistrations = 0, updates = 0, failedThumbnail = 0;
        var calls = new List<string>();
        var windowManager = Stub.Create<IWindowManager>((method, args) =>
        {
            if (method.Name != "GetLiveThumbnail") return Stub.Default(method.ReturnType);
            int id = ++registrations;
            calls.Add("Register:" + id);
            return Stub.Create<IDwmThumbnail>((operation, values) =>
            {
                calls.Add(operation.Name + ":" + id);
                if (operation.Name == "Unregister") unregistrations++;
                if (operation.Name == "Update") { updates++; return id != failedThumbnail; }
                return Stub.Default(operation.ReturnType);
            });
        });
        using var view = (ThumbnailView)Activator.CreateInstance(assembly.GetType("EveOPreview.View.LiveThumbnailView"),
            windowManager, config, Stub.Create<IThumbnailManager>(), Stub.Create<IMediator>(),
            Stub.Create<IKeyboardMouseEvents>(), logger);
        view.Id = new IntPtr(101);
        view.Title = "EVE - Live image test";
        view.ThumbnailSize = new Size(320, 180);
        view.Show();
        view.SetTopMost(true);
        Assert.Equal(1, registrations);

        switch (scenario)
        {
            case "HealthyRefresh":
                int previousUpdates = updates;
                for (int i = 0; i < 10; i++) view.Refresh(true);
                Assert.Equal(previousUpdates + 10, updates);
                Assert.Equal(1, registrations);
                Assert.Equal(0, unregistrations);
                break;

            case "BorderAndZOrder":
                for (int i = 0; i < 10; i++)
                {
                    view.SetHighlight(true, 3);
                    view.Refresh(false);
                    view.ClearBorder();
                    int beforeRaise = updates;
                    Assert.True(view.RestoreAndBringToFront());
                    Assert.Equal(beforeRaise, updates); // Reordering does not redraw or replace the image.
                }
                Assert.Equal(1, registrations);
                Assert.Equal(0, unregistrations);
                break;

            case "FailedRefreshRecovers":
                failedThumbnail = 1;
                calls.Clear();
                view.Refresh(true);
                Assert.Equal(2, registrations);
                Assert.Equal(1, unregistrations);
                Assert.True(calls.IndexOf("Update:2") < calls.IndexOf("Unregister:1"),
                    "The replacement must be populated before releasing the old image.");
                view.Refresh(true);
                Assert.Equal(2, registrations); // The healthy replacement stays registered.
                Assert.Equal(1, unregistrations);
                break;

            default:
                throw new ArgumentException("Unknown live thumbnail scenario: " + scenario);
        }
        view.Close();
    }
}

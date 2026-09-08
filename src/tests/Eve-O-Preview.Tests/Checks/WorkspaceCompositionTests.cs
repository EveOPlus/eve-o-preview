using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autofac;
using Avalonia;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Configuration.Interface;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.UI;
using EveOPreview.View;
using Gma.System.MouseKeyHook;
using MediatR;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class WorkspaceCompositionTests(ITestOutputHelper output)
{
    [Fact]
    public Task ProductionContainerConstructsWorkspaceWithRealConfiguration() =>
        PrivateDesktopRunner.RunAsync("workspace-composition", output);

    internal static void CheckComposition()
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        AppBuilder.Configure<WorkspaceApp>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
        using var logger = new LoggerConfiguration().CreateLogger();
        using var context = new ApplicationContext();
        var root = Path.Combine(Path.GetTempPath(), "EveOPreviewComposition-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var program = typeof(WorkspaceForm).Assembly.GetType("EveOPreview.Program");
            var factory = program.GetMethod("CreateApplicationContainerBuilder", BindingFlags.NonPublic | BindingFlags.Static);
            var builder = (ContainerBuilder)factory.Invoke(null, [logger, Stub.Create<IKeyboardMouseEvents>(), context]);
            builder.Register(ctx => (IProfileManager)Activator.CreateInstance(typeof(ProfileManager),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                [logger, ctx.Resolve<IMediator>(), Path.Combine(root, "Profiles")], null))
                .As<IProfileManager>().SingleInstance();
            builder.RegisterInstance(Stub.Create<IWorkspacePortraitProvider>((_, _) => Task.FromResult<byte[]>(null)))
                .As<IWorkspacePortraitProvider>();
            using var container = builder.Build();
            Assert.IsType<WindowsWorkspacePreviewCapture>(container.Resolve<IWorkspacePreviewCapture>());
            Assert.IsType<EveOPreview.Services.Implementation.CharacterPortraitCache>(container.Resolve<EveOPreview.Services.Implementation.CharacterPortraitCache>());
            Assert.Same(container.Resolve<EveOPreview.Services.Implementation.CharacterIdentityCache>(), container.Resolve<IWorkspaceCharacterProvider>());
            Assert.NotNull(container.Resolve<EveOPreview.Services.IProcessMonitor>());
            using var view = Assert.IsType<WorkspaceForm>(container.Resolve<IMainFormView>());
            Assert.Equal("Default", view.Backend.Read().ProfileName);
            Assert.NotNull(view.Backend.Read().Settings);
            Console.WriteLine("PASS: production Autofac registrations resolve the real workspace and configuration without running native services.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}

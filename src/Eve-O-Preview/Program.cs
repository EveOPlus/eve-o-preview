//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <https://www.gnu.org/licenses/>.

using Autofac;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Configuration.Interface;
using EveOPreview.Presenters;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interface;
using EveOPreview.View;
using Gma.System.MouseKeyHook;
using MediatR;
using Serilog;
using Serilog.Events;
using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Autofac.Extensions.DependencyInjection;
using MediatR.Extensions.Autofac.DependencyInjection;
using Avalonia;
using EveOPreview.UI;
using Application = System.Windows.Forms.Application;

namespace EveOPreview
{
    static class Program
    {
        private static string MUTEX_NAME = "EVE-O Preview Single Instance Mutex";

        private static Mutex _singleInstanceMutex;

        /// <summary>The main entry point for the application.</summary>
        [STAThread]
        static void Main(params string[] args)
        {
            if (args.Contains("--validate-workspace"))
            {
                ValidateWorkspace();
                return;
            }

            if (args?.Any(x => x == "--attach-debug-sidecar") == true)
            {
                DebuggerSidecar.RunAsTheSideCar(args);
            }
            else
            {
                SetupLogger(args);
                
                Log.Information("Starting new instance of Eve-O Preview");
                
                // The very usual Mutex-based single-instance screening
                // 'token' variable is used to store reference to the instance Mutex
                // during the app lifetime
                Program._singleInstanceMutex = Program.GetInstanceToken();

                // If it was not possible to acquire the app token then another app instance is already running
                // Nothing to do here
                if (Program._singleInstanceMutex == null)
                {
                    Log.Warning("An existing instance of Eve-O Preview is already running. Exiting.");
                    return;
                }

                
                ExceptionHandler handler = new ExceptionHandler();
                handler.SetupExceptionHandlers();

                Program.InitializeWinForms();
                AppBuilder.Configure<WorkspaceApp>()
                    .UsePlatformDetect()
                    .WithInterFont()
                    .SetupWithoutStarting();

                IApplicationController controller = Program.InitializeApplicationController();
                
                DebuggerSidecar.LaunchTheSideCar();

                controller.Run<MainFormPresenter>();
            }
        }

        // Run in the actual executable, so the SDK bundle and Costura resolvers are
        // exercised too. No user profiles, single-instance mutex, discovery, input
        // subscriptions, injection, message boxes or debugger sidecar are involved.
        private static void ValidateWorkspace()
        {
            var root = Path.Combine(Path.GetTempPath(), "EveOPreviewStartup-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                InitializeWinForms();
                AppBuilder.Configure<WorkspaceApp>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
                using var logger = new LoggerConfiguration().CreateLogger();
                using var inputEvents = Hook.GlobalEvents();
                using var context = new ApplicationContext();
                var builder = CreateApplicationContainerBuilder(logger, inputEvents, context);
                builder.Register(ctx => new ProfileManager(logger, ctx.Resolve<IMediator>(), Path.Combine(root, "Profiles")))
                    .As<IProfileManager>().SingleInstance();
                // Network availability is separate from executable/resource validation.
                builder.RegisterInstance(new ValidationPortraitProvider()).As<IWorkspacePortraitProvider>();
                using var container = builder.Build();
                var form = (WorkspaceForm)container.Resolve<IMainFormView>();
                var host = (Avalonia.Win32.Interoperability.WinFormsAvaloniaControlHost)form.Controls[0];
                var workspace = (WorkspaceView)host.Content;
                workspace.Measure(new Avalonia.Size(1180, 800));
                workspace.Arrange(new Avalonia.Rect(0, 0, 1180, 800));
                using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(1180, 800));
                bitmap.Render(workspace);
                Console.WriteLine("Workspace startup validation passed (configuration, UI resources and native rendering).");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Environment.ExitCode = 1;
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        private sealed class ValidationPortraitProvider : IWorkspacePortraitProvider
        {
            public System.Threading.Tasks.Task<byte[]> GetCharacterPortraitAsync(long characterId) =>
                System.Threading.Tasks.Task.FromResult<byte[]>(null);
        }

        private static void SetupLogger(params string[] args)
        {
            var isVerbose = args.Contains("--verbose") || args.Contains("-v");
            var minimumLevel = isVerbose ? LogEventLevel.Verbose : LogEventLevel.Information;

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(minimumLevel)
                .Enrich.FromLogContext()
                .WriteTo.File("logs/EVE-O Preview Log-.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    fileSizeLimitBytes: 10 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    restrictedToMinimumLevel: minimumLevel,
                    outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Properties:j} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Logger.Information("Logger initialized. Application arguments: {Arguments}", string.Join(", ", args ?? Array.Empty<string>()));
        }

        private static Mutex GetInstanceToken()
        {
            // The code might look overcomplicated here for a single Mutex operation
            // Yet we had already experienced a Windows-level issue
            // where .NET finalizer thread was literally paralyzed by
            // a failed Mutex operation. That did lead to weird OutOfMemory
            // exceptions later
            try
            {
                Mutex.OpenExisting(Program.MUTEX_NAME);
                // if that didn't fail then another instance is already running
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (Exception)
            {
                Mutex token = new Mutex(true, Program.MUTEX_NAME, out var result);
                return result ? token : null;
            }
        }

        private static void InitializeWinForms()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }

        private static IApplicationController InitializeApplicationController()
        {
            var builder = CreateApplicationContainerBuilder(Log.Logger, Hook.GlobalEvents(), new ApplicationContext());
            var container = builder.Build();
            return container.Resolve<IApplicationController>();
        }

        // Keep the production registrations available for isolated startup checks without
        // installing global hooks or selecting the user's profile directory.
        internal static ContainerBuilder CreateApplicationContainerBuilder(ILogger logger, IKeyboardMouseEvents inputEvents, ApplicationContext context)
        {
            var builder = new ContainerBuilder();
            var assembly = typeof(Program).Assembly;
            
            builder.RegisterInstance(logger).AsImplementedInterfaces().SingleInstance();
            builder.RegisterInstance(inputEvents).AsImplementedInterfaces().SingleInstance();

            // Singleton registration is used for services
            // Low-level services
            builder.RegisterType<WindowManager>().As<IWindowManager>().SingleInstance();
            builder.RegisterType<HookService>().As<IHookService>().SingleInstance();
            builder.RegisterType<ProcessMonitor>().As<IProcessMonitor>().SingleInstance();
            builder.RegisterType<CpuAffinityService>().As<ICpuAffinityService>().SingleInstance();

            // MediatR
            MediatR.Mediator.LicenseKey = "Community";
            
            builder.Register(ctx => new AutofacServiceProvider(ctx.Resolve<ILifetimeScope>()))
                .As<IServiceProvider>()
                .InstancePerLifetimeScope();

            var mediatrConfig = MediatR.Extensions.Autofac.DependencyInjection.Builder.MediatRConfigurationBuilder
                .Create("Community", typeof(Program).Assembly)
                .WithAllOpenGenericHandlerTypesRegistered()
                .Build();

            builder.RegisterMediatR(mediatrConfig);

            // Configuration services
            builder.RegisterType<ProfileManager>().As<IProfileManager>().SingleInstance();
            builder.RegisterType<ConfigurationStorage>().As<IConfigurationStorage>().SingleInstance();
            builder.RegisterType<AppConfig>().As<IAppConfig>().SingleInstance();
            builder.RegisterType<ThumbnailConfiguration>().As<IThumbnailConfiguration>().SingleInstance();
            builder.RegisterType<GlobalEvents>().As<IGlobalEvents>().SingleInstance();
            builder.RegisterType<ApplicationPreferences>().AsSelf().SingleInstance();
            builder.RegisterType<CharacterPortraitCache>().AsSelf().As<IWorkspacePortraitProvider>().SingleInstance();
            builder.RegisterType<EveClientUserIdReader>().AsSelf().SingleInstance();
            builder.RegisterType<CharacterIdentityCache>().AsSelf().As<IWorkspaceCharacterProvider>().SingleInstance();
            builder.RegisterType<WindowsWorkspacePreviewCapture>().As<IWorkspacePreviewCapture>().SingleInstance();


            // Application services
            builder.RegisterType<ThumbnailManager>().As<IThumbnailManager>().SingleInstance();
            builder.RegisterType<ThumbnailViewFactory>().As<IThumbnailViewFactory>().SingleInstance();
            builder.RegisterType<ThumbnailDescription>().As<IThumbnailDescription>().SingleInstance();

            // Views
            builder.RegisterType<StaticThumbnailView>().AsSelf().InstancePerDependency();
            builder.RegisterType<LiveThumbnailView>().AsSelf().InstancePerDependency();
            builder.RegisterType<WorkspaceForm>().As<IMainFormView>().InstancePerDependency();
            
            // Main presenter and controller
            builder.RegisterInstance(context).AsSelf().SingleInstance();
            builder.RegisterType<MainFormPresenter>().AsSelf().SingleInstance();
            builder.RegisterType<ApplicationController>().As<IApplicationController>().SingleInstance();

            return builder;
        }
    }
}

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

using EveOPreview.Configuration;
using EveOPreview.Configuration.Interface;
using EveOPreview.Presenters;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interface;
using EveOPreview.View;
using Gma.System.MouseKeyHook;
using LightInject;
using MediatR;
using Serilog;
using Serilog.Events;
using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

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

                IApplicationController controller = Program.InitializeApplicationController();

                Program.InitializeWinForms();
                
                DebuggerSidecar.LaunchTheSideCar();

                controller.Run<MainFormPresenter>();
            }
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
                    restrictedToMinimumLevel: minimumLevel,
                    outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Properties:j} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
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
            IIocContainer container = new LightInjectContainer();

            container.RegisterInstance<ILogger>(Log.Logger);
            container.RegisterInstance<IKeyboardMouseEvents>(Hook.GlobalEvents());

            // Singleton registration is used for services
            // Low-level services
            container.Register<IWindowManager>();
            container.Register<IHookService>();
            container.Register<IProcessMonitor>();
            container.Register<IPremiumService>();

            // MediatR
            container.Register<IMediator, MediatR.Mediator>();
            container.RegisterInstance<ServiceFactory>(t => container.Resolve(t));
            container.Register(typeof(INotificationHandler<>), typeof(Program).Assembly);
            container.Register(typeof(IRequestHandler<>), typeof(Program).Assembly);
            container.Register(typeof(IRequestHandler<,>), typeof(Program).Assembly);

            // Configuration services
            container.Register<IProfileManager>();
            container.Register<IConfigurationStorage>();
            container.Register<IAppConfig>();
            container.Register<IThumbnailConfiguration>();
            container.Register<IGlobalEvents>();

            // Application services
            container.Register<IThumbnailManager>();
            container.Register<IThumbnailViewFactory>();
            container.Register<IThumbnailDescription>();

            IApplicationController controller = new ApplicationController(container);

            // UI classes
            controller.RegisterView<StaticThumbnailView, StaticThumbnailView>();
            controller.RegisterView<LiveThumbnailView, LiveThumbnailView>();

            controller.RegisterView<IMainFormView, MainForm>();
            controller.RegisterInstance(new ApplicationContext());

            return controller;
        }
    }
}
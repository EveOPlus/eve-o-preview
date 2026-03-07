using Cake.Common.Diagnostics;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Common.Tools.MSBuild;
using Cake.Frosting;

namespace Build.Tasks
{
	[IsDependentOn(typeof(Restore))]
	public sealed class Build : FrostingTask<Context>
	{
		public override void Run(Context context)
		{
			context.Information("Build started...");

			context.MSBuild(Configuration.SolutionName, settings =>
			{
				settings.Configuration = Configuration.BuildConfiguration;
				settings.ToolVersion = MSBuildToolVersion.Default;

				if (!string.IsNullOrEmpty(Configuration.BuildToolPath))
				{
					settings.ToolPath = Configuration.BuildToolPath;
				}
			});

            //context.DotNetPublish("./src/Eve-O-Hooks/Eve-O-Hooks.csproj", new DotNetPublishSettings
            //{
            //    Configuration = Configuration.BuildConfiguration,
            //    Runtime = "win-x64",
            //    SelfContained = true,
            //    OutputDirectory = Configuration.BinFolder,
            //    MSBuildSettings = new DotNetMSBuildSettings()
            //        .WithProperty("PublishAot", "true")
            //});
        }
	}
}

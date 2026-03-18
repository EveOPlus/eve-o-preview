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

using Cake.Common.Diagnostics;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Frosting;

namespace Build.Tasks
{
	[IsDependentOn(typeof(Documentation))]
	public sealed class Build : FrostingTask<Context>
	{
		public override void Run(Context context)
		{
            context.Information("Build started for .NET 10...");

            context.DotNetPublish(Configuration.MainSolutionName, new DotNetPublishSettings
            {
				Configuration = Configuration.BuildConfiguration,
                Runtime = "win-x64",
                OutputDirectory = Configuration.BinFolder,
                MSBuildSettings = new DotNetMSBuildSettings()
                    .WithProperty("PublishSingleFile", "true")
                    .WithProperty("SelfContained", "false")
                    .WithProperty("IncludeNativeLibrariesForSelfExtract", "true")
                    .WithProperty("IncludeAllContentForSelfExtract", "true")
            });

			context.DotNetPublish(Configuration.RobinSolutionName, new DotNetPublishSettings
			{
				Configuration = Configuration.BuildConfiguration,
				Runtime = "win-x64",
				SelfContained = true,
				OutputDirectory = Configuration.BinFolder,
				MSBuildSettings = new DotNetMSBuildSettings()
					.WithProperty("PublishAot", "true")
			});
		}
	}
}

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
using Cake.Common.IO;
using Cake.Common.Net;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build
{
	public sealed class Lifetime : FrostingLifetime<Context>
	{
		private const string NuGetUrl = @"https://dist.nuget.org/win-x86-commandline/latest/nuget.exe";

		private void DeleteDirectory(Context context, string directoryName)
		{
			if (!context.DirectoryExists(directoryName))
			{
				return;
			}

			context.DeleteDirectory(directoryName, new DeleteDirectorySettings { Force = true, Recursive = true });
		}

		private void DownloadNuGet(Context context)
		{
			if (context.FileExists(Configuration.ToolsFolder + "/nuget.exe"))
			{
				return;
			}

			if (!context.DirectoryExists(Configuration.ToolsFolder))
			{
				context.CreateDirectory(Configuration.ToolsFolder);
			}

			var tempFile = context.DownloadFile(NuGetUrl);
			context.CopyFile(tempFile, new FilePath(Configuration.ToolsFolder + "/nuget.exe"));
		}

		public override void Setup(Context context, ISetupContext setup)
		{
			context.Information("Setting things up...");

			context.Information("Delete bin and publish folders");
			this.DeleteDirectory(context, Configuration.BinFolder);
			this.DeleteDirectory(context, Configuration.PublishFolder);

			context.Information("Download NuGet");
			this.DownloadNuGet(context);

		}

		public override void Teardown(Context context, ITeardownContext info)
		{
			context.Information("Tearing things down...");
			//this.DeleteDirectory(context, ToolsDirectoryName);
		}
	}
}
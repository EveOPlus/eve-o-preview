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

using Cake.Common.IO;
using Cake.Frosting;

namespace Build.Tasks
{
	[IsDependentOn(typeof(Build))]
	public sealed class Zip : FrostingTask<Context>
	{
		public override void Run(Context context)
		{
			if (!context.DirectoryExists(Configuration.PublishFolder))
			{
				context.CreateDirectory(Configuration.PublishFolder);
			}

			context.Zip(Configuration.BinFolder, Configuration.PublishFolder + "/EVE-O Preview.zip",
			new[]
            {
                Configuration.BinFolder + "/EVE-O Preview.exe", 
                Configuration.BinFolder + "/LICENSE.txt", 
                Configuration.BinFolder + "/readme.pdf", 
                Configuration.BinFolder + "/FPSLimiter.Hook.dll"
            });
		}
	}
}
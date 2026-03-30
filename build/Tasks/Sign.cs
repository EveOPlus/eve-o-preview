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
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Common.Tools.SignTool;
using Cake.Frosting;
using System;

namespace Build.Tasks
{
	[IsDependentOn(typeof(Build))]
	public sealed class Sign : FrostingTask<Context>
	{
		public override void Run(Context context)
		{
            if (string.IsNullOrWhiteSpace(Configuration.CodeSigningPath))
            {
                context.Information("No code signing certificate, skipping...");
                return;
            }

            context.Information("Code signing started...");

            context.Information("Please enter the code signing password:");
            var password = Console.ReadLine(); // We can move this to pull it from CICD pipeline later if we move to github actions or similar. For now just prompt the user to type it each time.

            var files = context.GetFiles($"{Configuration.BinFolder}/**/*.{{exe,dll}}");

            var settings = new SignToolSignSettings
            {
                CertPath = Configuration.CodeSigningPath,
                Password = password,
                TimeStampUri = new Uri("http://timestamp.digicert.com"),
                DigestAlgorithm = SignToolDigestAlgorithm.Sha256
            };
    
            context.Sign(files, settings);
    
            context.Information("Code signing completed.");
        }
    }
}

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
using Cake.Frosting;
using Cake.MarkdownToPdf;
using Markdig;

namespace Build.Tasks
{
	public sealed class Documentation : FrostingTask<Context>
	{
		public override void Run(Context context)
		{
			context.Information("Convert README.MD");

			context.MarkdownFileToPdf("readme.md", Configuration.BinFolder + "/readme.pdf", settings =>
			{
				settings.Theme = Themes.Github;
				settings.UseAdvancedMarkdownTables();
				settings.MarkdownPipeline.UseGridTables();
			});
		}
	}
}
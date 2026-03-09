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

namespace Build
{
	static class Configuration
	{
		public const string SolutionName = @"./src/EVE-O-Preview.sln";

		public const string BinFolder = @"./bin";
		public const string ToolsFolder = @"./tools";
		public const string PublishFolder = @"./publish";
		public const string BuildConfiguration = @"Release";

		public const string BuildToolPath = @"C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe"; // Set to NULL to let Cake to try to use the default MSBuild instance
	}
}

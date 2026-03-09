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

using Cake.Frosting;
using Microsoft.Extensions.DependencyInjection;

namespace Build
{
	public class Program : IFrostingStartup
	{
		public static int Main(string[] args)
			=> new CakeHost()
				.UseStartup<Program>()
				.Run(args);

		public void Configure(IServiceCollection services)
		{
			services.UseContext<Context>();
			services.UseLifetime<Lifetime>();

			//move up from build directory and searching for sln or csproj files
			services.UseWorkingDirectory("..");
		}
	}
}
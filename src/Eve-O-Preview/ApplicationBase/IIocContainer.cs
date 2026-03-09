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

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace EveOPreview
{
    /// <summary>
    /// Generic interface for an Inversion Of Control container
    /// </summary>
    public interface IIocContainer
    {
        void Register<TService, TImplementation>()
            where TImplementation : TService;
        void RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService;
        void Register(Type serviceType, Assembly container);
        void Register<TService>();
        void Register<TService>(Expression<Func<TService>> factory);
        void Register<TService, TArgument>(Expression<Func<TArgument, TService>> factory);
        void RegisterInstance<TService>(TService instance);
        TService Resolve<TService>();
        IEnumerable<TService> ResolveAll<TService>();
        object Resolve(Type serviceType);
        IEnumerable<object> ResolveAll(Type serviceType);
        bool IsRegistered<TService>();
    }
}
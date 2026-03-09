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
using LightInject;

namespace EveOPreview
{
    // Adapts LighInject to the generic IoC interface
    sealed class LightInjectContainer : IIocContainer
    {
        private readonly ServiceContainer _container;

        public LightInjectContainer()
        {
            this._container = new ServiceContainer(ContainerOptions.Default);
        }

        public bool IsRegistered<TService>()
        {
            return this._container.CanGetInstance(typeof(TService), "");
        }

        public void Register(Type serviceType, Assembly container)
        {
            if (!serviceType.IsInterface)
            {
                this._container.Register(serviceType, new PerContainerLifetime());
                return;
            }

            if (serviceType.IsInterface && serviceType.IsGenericType)
            {
                this._container.RegisterAssembly(container, (st, it) => st.IsConstructedGenericType && st.GetGenericTypeDefinition() == serviceType);
            }
            else
            {
                foreach (TypeInfo implementationType in container.DefinedTypes)
                {
                    if (!implementationType.IsClass || implementationType.IsAbstract)
                    {
                        continue;
                    }

                    if (serviceType.IsAssignableFrom(implementationType))
                    {
                        this._container.Register(serviceType, implementationType, new PerContainerLifetime());
                    }
                }
            }
        }
        
        public void Register<TService>()
        {
            this.Register(typeof(TService), typeof(TService).Assembly);
        }
        public void RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService
        {
            this._container.RegisterSingleton<TService, TImplementation>();
        }

        public void Register<TService, TImplementation>()
                    where TImplementation : TService
        {
            this._container.Register<TService, TImplementation>();
        }

        public void Register<TService>(Expression<Func<TService>> factory)
        {
            this._container.Register(f => factory);
        }

        public void Register<TService, TArgument>(Expression<Func<TArgument, TService>> factory)
        {
            this._container.Register(f => factory);
        }

        public void RegisterInstance<TService>(TService instance)
        {
            this._container.RegisterInstance(instance);
        }

        public TService Resolve<TService>()
        {
            return this._container.GetInstance<TService>();
        }

        public IEnumerable<TService> ResolveAll<TService>()
        {
            return this._container.GetAllInstances<TService>();
        }

        public object Resolve(Type serviceType)
        {
            return this._container.GetInstance(serviceType);
        }

        public IEnumerable<object> ResolveAll(Type serviceType)
        {
            return this._container.GetAllInstances(serviceType);
        }
    }
}
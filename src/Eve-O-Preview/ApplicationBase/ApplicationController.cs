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

namespace EveOPreview
{
    public class ApplicationController : IApplicationController
    {
        private readonly IIocContainer _container;

        public ApplicationController(IIocContainer container)
        {
            this._container = container;
            this._container.RegisterInstance<IApplicationController>(this);
        }

        public IApplicationController RegisterView<TView, TImplementation>()
            where TView : IView
            where TImplementation : class, TView
        {
            this._container.Register<TView, TImplementation>();
            return this;
        }

        public IApplicationController RegisterInstance<TArgument>(TArgument instance)
        {
            this._container.RegisterInstance(instance);
            return this;
        }

        public IApplicationController RegisterService<TService, TImplementation>()
            where TImplementation : class, TService
        {
            this._container.Register<TService, TImplementation>();
            return this;
        }

        public void Run<TPresenter>()
            where TPresenter : class, IPresenter
        {
            if (!this._container.IsRegistered<TPresenter>())
            {
                this._container.Register<TPresenter>();
            }

            TPresenter presenter = this._container.Resolve<TPresenter>();
            presenter.Run();
        }

        public void Run<TPresenter, TParameter>(TParameter args)
            where TPresenter : class, IPresenter<TParameter>
        {
            if (!this._container.IsRegistered<TPresenter>())
            {
                this._container.Register<TPresenter>();
            }

            TPresenter presenter = this._container.Resolve<TPresenter>();
            presenter.Run(args);
        }

        public TService Create<TService>()
            where TService : class
        {
            if (!this._container.IsRegistered<TService>())
            {
                this._container.Register<TService>();
            }

            return this._container.Resolve<TService>();
        }
    }
}
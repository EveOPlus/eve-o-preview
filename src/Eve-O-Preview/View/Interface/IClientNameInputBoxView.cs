using System;
using System.Collections.Generic;
using System.Drawing;

namespace EveOPreview.View
{
	/// <summary>
	/// Main view interface
	/// Presenter uses it to access GUI properties
	/// </summary>
	public interface IClientNameInputBoxView : IView
	{
		string SelectedClientName { get; set; }

		void LoadKnownClients(List<string> clientNames);
	}
}
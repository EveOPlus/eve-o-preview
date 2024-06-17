namespace EveOPreview.Configuration.Implementation
{
	class AppConfig : IAppConfig
	{
		public AppConfig()
		{
			// Default values
			this.ActiveProfileName = null;
		}

		public string ActiveProfileName { get; set; }
	}
}

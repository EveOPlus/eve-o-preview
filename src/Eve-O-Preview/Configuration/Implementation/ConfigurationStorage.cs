using System.IO;
using Newtonsoft.Json;

namespace EveOPreview.Configuration.Implementation
{
	class ConfigurationStorage : IConfigurationStorage
	{
		private const string DEFAULT_CONFIGURATION_FILE_NAME = "Default Profile";
		private const string APP_CONFIG_FILENAME = "config.json";
		private const string APP_CONFIG_BASE_DIR = @"./config/";
		private const string PROFILES_BASE_DIR = @"./profiles/";
		private const string EXTENSION = ".json";

		private readonly IAppConfig _appConfig;
		private readonly IThumbnailConfiguration _thumbnailConfiguration;

		public ConfigurationStorage(IAppConfig appConfig, IThumbnailConfiguration thumbnailConfiguration)
		{
			this._appConfig = appConfig;
			this._thumbnailConfiguration = thumbnailConfiguration;
		}

		public void Load()
		{

			initializeDirectories();

			string filename;

			 if(File.Exists(GetAppConfigPath()) && this._appConfig.ActiveProfileName == null)
            {
				string appConfigRawData = File.ReadAllText(this.GetAppConfigPath());
				JsonConvert.PopulateObject(appConfigRawData, this._appConfig);
				filename = this.GetProfileName();
			} else
            {
				filename = this.GetProfileName();
				this.UpdateActiveProfileName(filename);
            }

			

			if (!File.Exists(this.GetProfilePath()))
			{
				return;
			}

			string rawData = File.ReadAllText(this.GetProfilePath());

			JsonConvert.PopulateObject(rawData, this._thumbnailConfiguration);

			// Validate data after loading it
			this._thumbnailConfiguration.ApplyRestrictions();
		}

		public void Save()
		{
			string rawData = JsonConvert.SerializeObject(this._thumbnailConfiguration, Formatting.Indented);
			string appConfigRawData = JsonConvert.SerializeObject(this._appConfig, Formatting.Indented);

			try
			{
				File.WriteAllText(this.GetAppConfigPath(), appConfigRawData);
				File.WriteAllText(this.GetProfilePath(), rawData);
			}
			catch (IOException)
			{
				// Ignore error if for some reason the updated config cannot be written down
			}
		}

		public void UpdateActiveProfileName(string filename)
        {
			this._appConfig.ActiveProfileName = filename;
        }

		public string GetProfilesBaseDir()
        {
			return PROFILES_BASE_DIR;
        }

		public bool CreateNewProfile(string profileName, IThumbnailConfiguration config)
        {
			string rawData = JsonConvert.SerializeObject(config, Formatting.Indented);
			try
			{
				string filename = string.Concat(PROFILES_BASE_DIR, profileName, EXTENSION);
				if(File.Exists(filename))
                {
					return false;
                } else
                {
					File.WriteAllText(string.Concat(filename), rawData);
					return true;
                }

			}
			catch (IOException)
			{
				return false;
			}
		}

		private string GetAppConfigPath()
        {
			return APP_CONFIG_BASE_DIR + APP_CONFIG_FILENAME;
        }

		private string GetProfileName()
		{
			return (string.IsNullOrEmpty(this._appConfig.ActiveProfileName) ? ConfigurationStorage.DEFAULT_CONFIGURATION_FILE_NAME : this._appConfig.ActiveProfileName);
		}

		private string GetProfilePath()
        {
			return PROFILES_BASE_DIR + GetProfileFileName();
        }

		private void initializeDirectories()
        {
			if (!Directory.Exists(APP_CONFIG_BASE_DIR))
			{
				Directory.CreateDirectory(APP_CONFIG_BASE_DIR);
			}
			if (!Directory.Exists(PROFILES_BASE_DIR))
			{
				Directory.CreateDirectory(PROFILES_BASE_DIR);
			}
		}

		private string GetProfileFileName()
        {
			return string.Concat(GetProfileName(), EXTENSION);
        }
	}
}
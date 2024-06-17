namespace EveOPreview.Configuration
{
	public interface IConfigurationStorage
	{
		void Load();
		void Save();
		void UpdateActiveProfileName(string filename);
		string GetProfilesBaseDir();
		bool CreateNewProfile(string profileName, IThumbnailConfiguration config);
	}
}
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

using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Helper;
using EveOPreview.Mediator.Messages;
using MediatR;

namespace EveOPreview.Configuration.Implementation;

public class ProfileManager : IProfileManager
{
    private const string BASE_FILENAME = "EVE-O Preview.json";
    private const string PROFILES_DIR = "Profiles";
    private const string DEFAULT_PROFILE_DIR = "Default";
    private const string APP_FOLDER_NAME = "Eve-O Preview";

    private readonly ILogger _logger;
    private readonly IMediator _mediator;

    public string ProfileRootDirectory { get; }
    public List<ProfileLocation> ProfileLocations { get; }

    public ProfileManager(ILogger logger, IMediator _mediator)
    {
        _logger = logger;
        this._mediator = _mediator;
        ProfileRootDirectory = FindOrCreateProfileRootDirectory();
        _logger.WithCallerInfo().Information($"Profiles Root Directory located at {ProfileRootDirectory}");

        MigrateLegacySingleProfile();

        ProfileLocations = RefreshProfileLocations();
    }

    private string FindOrCreateProfileRootDirectory()
    {
        string exePath = System.IO.Path.GetDirectoryName(System.Environment.ProcessPath);
        string localProfilesPath = Path.Combine(exePath, PROFILES_DIR);

        // First use any existing profiles that live in the same folder.
        if (Directory.Exists(localProfilesPath))
        {
            return localProfilesPath;
        }

        // Second check if any profiles live in %localappdata%
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appDataPath = Path.Combine(localAppData, APP_FOLDER_NAME);
        string appDataProfilesPath = Path.Combine(appDataPath, PROFILES_DIR);

        if (Directory.Exists(appDataProfilesPath))
        {
            return appDataProfilesPath;
        }

        // nothing exists so lets try and create the profiles in our current folder.
        try
        {
            Directory.CreateDirectory(localProfilesPath);
            Directory.CreateDirectory(Path.Combine(localProfilesPath, DEFAULT_PROFILE_DIR));

            return localProfilesPath;
        }
        catch (UnauthorizedAccessException)
        {
            // If we can't write to that folder for any reasons, fallback to using AppData.
            Directory.CreateDirectory(appDataProfilesPath);
            Directory.CreateDirectory(Path.Combine(appDataProfilesPath, DEFAULT_PROFILE_DIR));

            return appDataProfilesPath;
        }
    }

    private void MigrateLegacySingleProfile()
    {
        // The location we would expect to find EVE-O Preview.json before we started supporting multiple profiles.
        string exePath = System.IO.Path.GetDirectoryName(System.Environment.ProcessPath);
        string sourceFile = Path.Combine(exePath, BASE_FILENAME);

        string destDir = Path.Combine(ProfileRootDirectory, DEFAULT_PROFILE_DIR);
        string destFile = Path.Combine(destDir, BASE_FILENAME);

        if (!File.Exists(sourceFile) || File.Exists(destFile))
        {
            // Either the old profile doesn't exist, or we've already migrated it (destination exists), so we can skip migration.
            return;
        }

        _logger.WithCallerInfo().Information($"Located a legacy profile to be migrated");

        try
        {
            _logger.WithCallerInfo().Information($"Copying profile from {sourceFile} to {destFile}");
            File.Copy(sourceFile, destFile, overwrite: false);

            // rename the old file so it's left as a backup.
            string backupPath = sourceFile + ".bak";
            int counter = 1;

            while (File.Exists(backupPath))
            {
                backupPath = $"{sourceFile}.bak({counter})";
                counter++;
            }

            _logger.WithCallerInfo().Information($"Moving old profile from {sourceFile} to {backupPath}");
            File.Move(sourceFile, backupPath);
        }
        catch (Exception ex)
        {
            _logger.WithCallerInfo().Error($"Error while moving the old profile", ex);
        }
    }

    public List<ProfileLocation> RefreshProfileLocations()
    {
        var locations = new List<ProfileLocation>();

        if (!Directory.Exists(this.ProfileRootDirectory))
        {
            _logger.WithCallerInfo().Error($"{nameof(ProfileRootDirectory)} does not exist!");
            return locations;
        }

        // Get all subdirectories in the Profiles folder
        string[] profileDirs = Directory.GetDirectories(this.ProfileRootDirectory);

        foreach (string dirPath in profileDirs)
        {
            string baseJsonPath = Path.Combine(dirPath, BASE_FILENAME);
            string profileName = Path.GetFileName(dirPath);

            // If this folder has a profile, add it to the list.
            if (File.Exists(baseJsonPath) || profileName == DEFAULT_PROFILE_DIR)
            {
                // Use the folder name as the friendly name.
                locations.Add(new ProfileLocation
                {
                    FriendlyName = profileName,
                    FolderPath = dirPath,
                    FullPath = baseJsonPath
                });
            }
        }

        _mediator.Publish(new ProfileListChangedNotification(locations));

        return locations;
    }

    public ProfileLocation GetDefaultProfileLocation()
    {
        var defaultProfile = ProfileLocations.FirstOrDefault(x => x.FriendlyName == DEFAULT_PROFILE_DIR);

        if (string.IsNullOrWhiteSpace(defaultProfile?.FullPath))
        {
            _logger.WithCallerInfo()
                .Information($"Unable to locate any profiles at default location {defaultProfile?.FullPath}");
            return null;
        }

        return defaultProfile;
    }

    public void CloneCurrentProfile()
    {
        var currentProfile = _mediator.Send(new GetCurrentProfileLocation()).Result;
        string newProfileName = GenerateNextProfileName(currentProfile.FriendlyName);
        string destDir = Path.Combine(ProfileRootDirectory, newProfileName);

        try
        {
            CopyDirectory(currentProfile.FolderPath, destDir);

            RefreshProfileLocations();
        }
        catch (Exception ex)
        {
            _logger.WithCallerInfo().Error(ex, $"Failed to clone profile {currentProfile.FriendlyName}");
        }
    }

    public void DeleteCurrentProfile()
    {
        var currentProfile = _mediator.Send(new GetCurrentProfileLocation()).Result;
        if (currentProfile.FriendlyName == DEFAULT_PROFILE_DIR)
        {
            return;
        }

        if (!Directory.Exists(currentProfile.FolderPath))
        {
            _logger.WithCallerInfo().Warning($"Failed to delete non existing path {currentProfile.FolderPath}");
            return;
        }

        try
        {
            Directory.Delete(currentProfile.FolderPath, true);
            _logger.WithCallerInfo().Information($"Deleted profile {currentProfile.FriendlyName} directory: {currentProfile.FolderPath}");

            this._mediator.Send(new ChangeSelectedProfile(GetDefaultProfileLocation()));
            RefreshProfileLocations();
        }
        catch (Exception ex)
        {
            _logger.WithCallerInfo().Error(ex, $"Failed to delete profile {currentProfile.FriendlyName}");
        }
    }

    public void RenameCurrentProfile(RenameCurrentProfile request)
    {
        var currentProfile = _mediator.Send(new GetCurrentProfileLocation()).Result;

        if (currentProfile.FriendlyName == request.NewProfileName)
        {
            return;
        }

        string newProfileName = GenerateNextProfileName(request.NewProfileName);
        string destDir = Path.Combine(ProfileRootDirectory, newProfileName);

        try
        {
            MoveDirectory(currentProfile.FolderPath, destDir);

            RefreshProfileLocations();
        }
        catch (Exception ex)
        {
            _logger.WithCallerInfo().Error(ex, $"Failed to rename profile {currentProfile.FriendlyName}");
        }
    }

    private string GenerateNextProfileName(string baseName)
    {
        int i = 1;
        string candidateName = baseName;

        while (Directory.Exists(Path.Combine(ProfileRootDirectory, candidateName)))
        {
            i++;
            candidateName = $"{baseName} ({i})";
        }

        return candidateName;
    }

    private void CopyDirectory(string sourceDir, string destDir)
    { 
        bool sourceExists = Directory.Exists(sourceDir);
        bool destDirExists = Directory.Exists(destDir);
        if (!sourceExists || destDirExists)
        {
            _logger.WithCallerInfo().Warning($"Unable to copy folder. Source [{sourceDir}] Exists = {sourceExists}.  Destination [{destDir}] Exists = {destDirExists}.");
            return;
        }

        Directory.CreateDirectory(destDir);

        foreach (string filePath in Directory.GetFiles(sourceDir))
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                string destPath = Path.Combine(destDir, fileName);
                File.Copy(filePath, destPath, true);
            }
            catch (Exception ex)
            {
                _logger.WithCallerInfo().Error(ex, $"Failed to copy file {filePath}");
            }
        }
    }

    private void MoveDirectory(string sourceDir, string destDir)
    {
        bool sourceExists = Directory.Exists(sourceDir);
        bool destExists = Directory.Exists(destDir);

        if (!sourceExists || destExists)
        {
            _logger.WithCallerInfo().Warning($"Unable to move folder. Source [{sourceDir}] Exists = {sourceExists}. Destination [{destDir}] Exists = {destExists}.");
            return;
        }

        try
        {
            Directory.Move(sourceDir, destDir);
            _logger.WithCallerInfo().Information($"Successfully renamed directory from {sourceDir} to {destDir}");
        }
        catch (Exception ex)
        {
            _logger.WithCallerInfo().Error(ex, $"Failed to move directory {sourceDir}");
        }
    }
}
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
using EveOPreview.Mediator.Messages;
using EveOPreview.Services.Interface;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Helper;
using Serilog;

namespace EveOPreview.Configuration.Implementation
{
    class ConfigurationStorage : IConfigurationStorage
    {
        private readonly IAppConfig _appConfig;
        private readonly IThumbnailConfiguration _thumbnailConfiguration;
        private readonly IMediator _mediator;
        private readonly ILogger _logger;
        private readonly IGlobalEvents _globalEvents;
        private readonly object _storageLock = new object();

        public ProfileLocation CurrentProfile { get; set; }

        public ConfigurationStorage(IAppConfig appConfig, IThumbnailConfiguration thumbnailConfiguration, IMediator mediator, IProfileManager profileManager, ILogger logger, IGlobalEvents globalEvents)
        {
            this._appConfig = appConfig;
            this._thumbnailConfiguration = thumbnailConfiguration;
            _mediator = mediator;
            _logger = logger;
            _globalEvents = globalEvents;

            CurrentProfile = profileManager.GetDefaultProfileLocation();

        }

        public bool Load()
        {
            lock (_storageLock)
            {
                try
                {
                    _logger.WithCallerInfo().Information($"Loading configuration profile: {CurrentProfile.FriendlyName} at {CurrentProfile.FullPath}");
                    string rawData = File.Exists(CurrentProfile.FullPath) ? File.ReadAllText(CurrentProfile.FullPath) : "{}";

                    JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
                    {
                        ObjectCreationHandling = ObjectCreationHandling.Replace,
                        NullValueHandling = NullValueHandling.Ignore
                    };
                    // Build a complete candidate first: omitted fields belong to this profile's defaults,
                    // and malformed input must never partially overwrite the active singleton.
                    var candidate = new ThumbnailConfiguration();
                    JsonConvert.PopulateObject(rawData, candidate, jsonSerializerSettings);
                    candidate.ApplyRestrictions();

                    AutoMigrateVersion1Config(rawData, candidate);
                    AutoMigrateVersion2Config(rawData, candidate);

                    // Validate data after loading it
                    candidate.ApplyRestrictions();
                    JsonConvert.PopulateObject(JsonConvert.SerializeObject(candidate), _thumbnailConfiguration, jsonSerializerSettings);
                    // The candidate is committed. A subscriber failure must not report a
                    // failed load and roll the selected path back while retaining these settings.
                    try { _mediator.Send(new RefreshHotkeys()).GetAwaiter().GetResult(); }
                    catch (Exception ex) { _logger.Error(ex, "Profile loaded, but refreshing hotkeys failed"); }
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.WithCallerInfo().Error(ex, "Unhandled exception while loading config.");
                    return false;
                }
            }
        }

        private void AutoMigrateVersion2Config(string rawData, IThumbnailConfiguration candidate)
        {
            
            var dynamicConfig = JsonConvert.DeserializeObject<dynamic>(rawData);
            if (dynamicConfig.ConfigVersion < 3)
            {
                _logger.Information($"Auto migrating version 2 config for profile: {CurrentProfile.FriendlyName}");
                Dictionary<string, string> oldClientHotkeys = new Dictionary<string, string>();
                if (dynamicConfig.ClientHotkey is JObject)
                {
                    oldClientHotkeys = dynamicConfig.ClientHotkey.ToObject<Dictionary<string, string>>();

                    var grouped = oldClientHotkeys.GroupBy(x => x.Value);
                    foreach (var individualGroup in grouped)
                    {
                        var newCycleGroup = new CycleGroup();
                        newCycleGroup.ForwardHotkeys = new List<string> { individualGroup.Key };

                        int i = 1;
                        foreach (var client in individualGroup)
                        {
                            newCycleGroup.ClientsOrder.Add(i++, client.Key);
                        }

                        var toonNames = individualGroup.Select(x => x.Key.Replace("EVE - ", "")).ToList();

                        newCycleGroup.Description = $"ClientHk - {string.Join(", ", toonNames)}";

                        if (candidate.CycleGroups.All(x => x.Description != newCycleGroup.Description))
                        {

                            candidate.CycleGroups.Add(newCycleGroup);
                        }
                    }
                }

                candidate.ConfigVersion = 3;
            }
        }

        private void AutoMigrateVersion1Config(string rawData, IThumbnailConfiguration candidate)
        {
            var dynamicConfig = JsonConvert.DeserializeObject<dynamic>(rawData);
            if (dynamicConfig.ConfigVersion == 1 && !candidate.CycleGroups.Any())
            {
                _logger.Information($"Auto migrating version 1 config for profile: {CurrentProfile.FriendlyName}");
                var cycleGroup1 = new CycleGroup();
                cycleGroup1.Description = "Cycle Group 1 Migrated";
                if (dynamicConfig.CycleGroup1ForwardHotkeys is JArray)
                {
                    foreach (var item in (JArray)dynamicConfig.CycleGroup1ForwardHotkeys)
                    {
                        cycleGroup1.ForwardHotkeys.Add(item.Value<string>());
                    }
                }

                if (dynamicConfig.CycleGroup1BackwardHotkeys is JArray)
                {
                    foreach (var item in (JArray)dynamicConfig.CycleGroup1BackwardHotkeys)
                    {
                        cycleGroup1.BackwardHotkeys.Add(item.Value<string>());
                    }
                }

                if (dynamicConfig.CycleGroup1ClientsOrder is JObject)
                {
                    foreach (JProperty property in ((JObject)dynamicConfig.CycleGroup1ClientsOrder).Properties().OrderBy(p => (int)p.Value))
                    {
                        string clientName = property.Name;      // e.g., "EVE - Example Toon 1"

                        cycleGroup1.ClientsOrder.Add(cycleGroup1.ClientsOrder.Count + 1, clientName);
                    }
                }

                var cycleGroup2 = new CycleGroup();
                cycleGroup2.Description = "Cycle Group 2 Migrated";
                if (dynamicConfig.CycleGroup2ForwardHotkeys is JArray)
                {
                    foreach (var item in (JArray)dynamicConfig.CycleGroup2ForwardHotkeys)
                    {
                        cycleGroup2.ForwardHotkeys.Add(item.Value<string>());
                    }
                }

                if (dynamicConfig.CycleGroup2BackwardHotkeys is JArray)
                {
                    foreach (var item in (JArray)dynamicConfig.CycleGroup2BackwardHotkeys)
                    {
                        cycleGroup2.BackwardHotkeys.Add(item.Value<string>());
                    }
                }

                if (dynamicConfig.CycleGroup2ClientsOrder is JObject)
                {
                    foreach (JProperty property in ((JObject)dynamicConfig.CycleGroup2ClientsOrder).Properties().OrderBy(p => (int)p.Value))
                    {
                        string clientName = property.Name;      // e.g., "EVE - Example Toon 1"

                        cycleGroup2.ClientsOrder.Add(cycleGroup2.ClientsOrder.Count + 1, clientName);
                    }
                }

                candidate.CycleGroups.Add(cycleGroup1);
                candidate.CycleGroups.Add(cycleGroup2);
                candidate.ConfigVersion = 2;
            }
        }

        public void Save()
        {
            lock (_storageLock)
            {
                _logger.Information($"Saving configuration profile: {CurrentProfile.FriendlyName} at {CurrentProfile.FullPath}");
                var options = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                string rawData = JsonConvert.SerializeObject(this._thumbnailConfiguration, Formatting.Indented, options);

                string tempPath = CurrentProfile.FullPath + ".tmp";
                try
                {
                    File.WriteAllText(tempPath, rawData);
                    File.Move(tempPath, CurrentProfile.FullPath, overwrite: true);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    _logger.Error(ex, "Failed to save profile {Path}", CurrentProfile.FullPath);
                }
                finally
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    { _logger.Warning(ex, "Could not remove temporary profile file {Path}", tempPath); }
                }
            }
        }
        
    }
}
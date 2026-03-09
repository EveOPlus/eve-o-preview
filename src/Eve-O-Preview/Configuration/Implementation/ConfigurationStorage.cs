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

using EveOPreview.Mediator.Messages;
using EveOPreview.Services.Interface;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EveOPreview.Configuration.Implementation
{
    class ConfigurationStorage : IConfigurationStorage
    {
        private const string CONFIGURATION_FILE_NAME = "EVE-O Preview.json";

        private readonly IAppConfig _appConfig;
        private readonly IThumbnailConfiguration _thumbnailConfiguration;
        private readonly IPremiumService _premiumService;
        private readonly IMediator _mediator;

        public ConfigurationStorage(IAppConfig appConfig, IThumbnailConfiguration thumbnailConfiguration, IPremiumService premiumService, IMediator mediator)
        {
            this._appConfig = appConfig;
            this._thumbnailConfiguration = thumbnailConfiguration;
            _premiumService = premiumService;
            _mediator = mediator;
        }

        public void Load()
        {
            string filename = this.GetConfigFileName();

            if (!File.Exists(filename))
            {
                return;
            }

            string rawData = File.ReadAllText(filename);

            AutoMigrateVersion1Config(rawData);
            var cycleGroupsToAdd = AutoMigrateVersion2Config(rawData);

            JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
            
            JsonConvert.PopulateObject(rawData, this._thumbnailConfiguration, jsonSerializerSettings);
            this._thumbnailConfiguration.CycleGroups.AddRange(cycleGroupsToAdd);
            this._thumbnailConfiguration.IsPremium = _premiumService.IsLicenseValidAndCurrent(this._thumbnailConfiguration.PremiumLicenseKey);

            // Validate data after loading it
            this._thumbnailConfiguration.ApplyRestrictions();
            this._mediator.Send(new RefreshHotkeys()).GetAwaiter().GetResult();
        }

        private List<CycleGroup> AutoMigrateVersion2Config(string rawData)
        {
            var newCycleGroups = new List<CycleGroup>();
            
            var dynamicConfig = JsonConvert.DeserializeObject<dynamic>(rawData);
            if (dynamicConfig.ConfigVersion < 3)
            {
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
                            newCycleGroup.ClientsOrder.Add(i, client.Key);
                        }

                        var toonNames = individualGroup.Select(x => x.Key.Replace("EVE - ", "")).ToList();

                        newCycleGroup.Description = $"ClientHk - {string.Join(", ", toonNames)}";

                        newCycleGroups.Add(newCycleGroup);
                    }
                }

                dynamicConfig.ConfigVersion = 3;
            }
            
            return newCycleGroups;
        }

        private void AutoMigrateVersion1Config(string rawData)
        {
            var dynamicConfig = JsonConvert.DeserializeObject<dynamic>(rawData);
            if (dynamicConfig.ConfigVersion == 1)
            {
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

                int numberOfDuplicateOrders = 0;
                if (dynamicConfig.CycleGroup1ClientsOrder is JObject)
                {
                    foreach (JProperty property in dynamicConfig.CycleGroup1ClientsOrder.Properties())
                    {
                        string clientName = property.Name;      // e.g., "EVE - Example Toon 1"
                        int orderNumber = (int)property.Value;  // e.g., 1

                        if (cycleGroup1.ClientsOrder.ContainsKey(orderNumber))
                        {
                            numberOfDuplicateOrders++;
                        }

                        orderNumber += numberOfDuplicateOrders;

                        cycleGroup1.ClientsOrder.Add(orderNumber, clientName);
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

                numberOfDuplicateOrders = 0;
                if (dynamicConfig.CycleGroup2ClientsOrder is JObject)
                {
                    foreach (JProperty property in dynamicConfig.CycleGroup2ClientsOrder.Properties())
                    {
                        string clientName = property.Name;      // e.g., "EVE - Example Toon 1"
                        int orderNumber = (int)property.Value;  // e.g., 1

                        if (cycleGroup2.ClientsOrder.ContainsKey(orderNumber))
                        {
                            numberOfDuplicateOrders++;
                        }

                        orderNumber += numberOfDuplicateOrders;

                        cycleGroup2.ClientsOrder.Add(orderNumber, clientName);
                    }
                }

                this._thumbnailConfiguration.CycleGroups.Add(cycleGroup1);
                this._thumbnailConfiguration.CycleGroups.Add(cycleGroup2);
                this._thumbnailConfiguration.ConfigVersion = 2;
            }
        }

        public void Save()
        {
            var options = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            string rawData = JsonConvert.SerializeObject(this._thumbnailConfiguration, Formatting.Indented, options);
            string filename = this.GetConfigFileName();

            try
            {
                File.WriteAllText(filename, rawData);
            }
            catch (IOException)
            {
                // Ignore error if for some reason the updated config cannot be written down
            }
        }

        private string GetConfigFileName()
        {
            return string.IsNullOrEmpty(this._appConfig.ConfigFileName) ? ConfigurationStorage.CONFIGURATION_FILE_NAME : this._appConfig.ConfigFileName;
        }
    }
}
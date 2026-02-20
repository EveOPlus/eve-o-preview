using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace EveOPreview.Configuration.Implementation
{
    class ConfigurationStorage : IConfigurationStorage
    {
        private const string CONFIGURATION_FILE_NAME = "EVE-O Preview.json";

        private readonly IAppConfig _appConfig;
        private readonly IThumbnailConfiguration _thumbnailConfiguration;

        public ConfigurationStorage(IAppConfig appConfig, IThumbnailConfiguration thumbnailConfiguration)
        {
            this._appConfig = appConfig;
            this._thumbnailConfiguration = thumbnailConfiguration;
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

            JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };

            // StageHotkeyArraysToAvoidDuplicates(rawData);

            JsonConvert.PopulateObject(rawData, this._thumbnailConfiguration, jsonSerializerSettings);

            // Validate data after loading it
            this._thumbnailConfiguration.ApplyRestrictions();
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
            }
        }

        public void Save()
        {
            string rawData = JsonConvert.SerializeObject(this._thumbnailConfiguration, Formatting.Indented);
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
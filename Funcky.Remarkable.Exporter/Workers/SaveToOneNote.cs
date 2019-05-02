using System;
using System.Collections.Generic;
using System.Text;

namespace Funcky.Remarkable.Exporter.Workers
{
    using System.IO;
    using System.Threading.Tasks;

    using Funcky.Remarkable.Exporter.Model;

    using Newtonsoft.Json.Linq;

    using NLog;

    public static class SaveToOneNote
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public static async Task Execute()
        {
            Logger.Info("Start Exporting to Evernote");

            var config = Configuration.Read();

            if (config?.Devices == null)
            {
                Logger.Warn("No configuration found, an empty one is created");
                Configuration.CreateEmptyConfiguration();
                return;
            }

            foreach (var device in config.Devices)
            {
                Logger.Info($"Processing device {device.Name}");

                // Check if Evernote is enabled
                /*if (!IsEnabled(device))
                {
                    Logger.Info($"Skipping step for device {device.Name} because it's not enabled");
                    continue;
                }*/

                // Check if smtp is present
                /*if (config.Smtp == null)
                {
                    Logger.Error("Cannot continue with this processor because no smtp info are configured");
                    continue;
                }*/

                // Build mail and process send, with a delay    
                var baseDirectory = new DirectoryInfo(device.LocalPath);

                foreach (var file in baseDirectory.GetFiles("content.json", SearchOption.AllDirectories))
                {
                    var onenoteflag = file.FullName.Replace(".json", ".onenote");

                    if (File.Exists(onenoteflag))
                    {
                        continue;
                    }

                    var content = JObject.Parse(File.ReadAllText(file.FullName));


                }
            }
        }

        private static bool IsEnabled(DeviceRegistration device)
        {
            if (device == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(device.EvernoteDestinationEmail)
                   && !string.IsNullOrWhiteSpace(device.EvernoteNotebook)
                   && !string.IsNullOrWhiteSpace(device.EvernoteSourceEmail);
        }

    }
}

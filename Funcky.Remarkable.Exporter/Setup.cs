using System;
using System.Collections.Generic;
using System.Text;

namespace Funcky.Remarkable.Exporter
{
    using System.IO;

    using Funcky.Remarkable.Exporter.Model;
    using Funcky.Remarkable.Exporter.Workers;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    using NLog;

    public static class ConfigurationManager
    {
        static ConfigurationManager()
        {
            // Set up configuration sources.
            var builder = new ConfigurationBuilder().SetBasePath(Path.Combine(AppContext.BaseDirectory))
                .AddJsonFile("appsettings.json", optional: true);

            AppSettings = builder.Build();
        }

        public static IConfiguration AppSettings { get; private set; }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="DrawNotes.cs" company="Prism">
//  Copyright (c) Prism. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

namespace Funcky.Remarkable.Exporter.Workers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Funcky.Remarkable.Exporter.Drawer;
    using Funcky.Remarkable.Exporter.Model;

    using NLog;

    public class DrawNotes
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public void Execute()
        {
            Logger.Info("Start Exporting to PNG");

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
                
                var baseDirectory = new DirectoryInfo(device.LocalPath);

                var v3 = from file in baseDirectory.GetFiles("*.rm", SearchOption.AllDirectories)
                         group file by file.Directory.FullName
                         into d
                         select new
                                    {
                                        PageSources =
                                            d.OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f.Name)))
                                                .Select(f => f.FullName)
                                                .ToArray(),
                                        TemplateFile = d.First().Directory.FullName + ".pagedata",
                                        RenderDir = d.First().Directory.FullName + ".png"
                                    };


                var v2 = from file in baseDirectory.GetFiles("*.lines", SearchOption.AllDirectories)
                         select new
                                    {
                                        PageSources = new[] { file.FullName },
                                        TemplateFile = file.FullName.Replace(".lines", ".pagedata"),
                                        RenderDir = Path.Combine(file.DirectoryName, ".png")
                                    };

                foreach (var source in v2.Concat(v3))
                {
                    /*if (Directory.Exists(source.RenderDir))
                    {
                        continue;
                    }*/

                    var templates = new List<string>();
                    if (File.Exists(source.TemplateFile))
                    {
                        templates.AddRange(File.ReadAllLines(source.TemplateFile));
                    }

                    Directory.CreateDirectory(source.RenderDir);

                    var templateIndex = 0;
                    var pageNumber = 0;
                    foreach (var file in source.PageSources)
                    {
                        var parser = new LinesParserMultiVersion(File.ReadAllBytes(file), file);
                        var pages = parser.Parse();

                        var drawer = new LinesDrawer(pages, templates, templateIndex);
                        var images = drawer.Draw();

                        foreach (var imageBinary in images)
                        {
                            pageNumber++;

                            var outputFile = Path.Combine(source.RenderDir, $"{pageNumber:000}.png");
                            File.WriteAllBytes(outputFile, imageBinary);
                        }

                        templateIndex += pages.Count;
                    }
                }
            }

            Logger.Info("End Exporting to PNG");
        }
    }
}
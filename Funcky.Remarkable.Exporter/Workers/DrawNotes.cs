// -----------------------------------------------------------------------
//  <copyright file="DrawNotes.cs" company="Prism">
//  Copyright (c) Prism. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

namespace Funcky.Remarkable.Exporter.Workers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    using Funcky.Remarkable.Exporter.Drawer;
    using Funcky.Remarkable.Exporter.Model;

    using NLog;

    public class DrawNotes
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public static void Execute(bool force)
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
                                        RenderDir = d.First().Directory.FullName,
                                        PagesToFileName = (Func<List<Page>, string, string[]>)UseFileNameAsPageName
                                    };


                var v2 = from file in baseDirectory.GetFiles("*.lines", SearchOption.AllDirectories)
                         select new
                                    {
                                        PageSources = new[] { file.FullName },
                                        TemplateFile = file.FullName.Replace(".lines", ".pagedata"),
                                        RenderDir = file.Directory.FullName,
                                        PagesToFileName = (Func<List<Page>, string, string[]>)UsePageIndexAsFileName
                                    };

                foreach (var source in v2.Concat(v3))
                {
                    if (Directory.Exists(source.RenderDir + ".png")
                        && Directory.Exists(source.RenderDir + ".inkml")
                        && !force)
                    {
                        continue;
                    }

                    var templates = new List<string>();
                    if (File.Exists(source.TemplateFile))
                    {
                        templates.AddRange(File.ReadAllLines(source.TemplateFile));
                    }

                    Directory.CreateDirectory(source.RenderDir + ".png");
                    Directory.CreateDirectory(source.RenderDir + ".inkml");

                    var pageOffset = 0;
                    foreach (var file in source.PageSources)
                    {
                        var parser = new LinesParserMultiVersion(File.ReadAllBytes(file), file);
                        var pages = parser.Parse();

                        var drawer = new LinesDrawer(pages, templates, pageOffset);
                        var images = drawer.Draw();

                        var fileNames = source.PagesToFileName(pages, file);

                        for (var i = 0; i < images.Count; i++)
                        {
                            var imageBinary = images[i];                            
                            var outputFile = Path.Combine(source.RenderDir + ".png", fileNames[i] + ".png");
                            File.WriteAllBytes(outputFile, imageBinary);
                        }

                        var inkMLGenerator = new InkMLGenerator(pages, templates, pageOffset);
                        var inkMLPages = inkMLGenerator.GenerateInkML();

                        for (int i = 0; i < inkMLPages.Count; i++)
                        {
                            var inkML = inkMLPages[i];

                            var xmlFile = Path.Combine(source.RenderDir + ".inkml", fileNames[i] + ".xml");
                            using (var fs = File.Create(xmlFile))
                            {
                                inkML.Save(fs);
                            }
                        }

                        pageOffset += pages.Count;
                    }
                }
            }

            Logger.Info("End Exporting to PNG");
        }

        private static string[] UsePageIndexAsFileName(List<Page> pages, string filename)
        {
            return pages.Select((p, i) => i.ToString("000")).ToArray();
        }

        private static string[] UseFileNameAsPageName(List<Page> pages, string filename)
        {
            if (pages.Count > 1)
            {
                throw new ArgumentException("Does not support .rm files with more than one page", "pages");
            }

            return new[] { Path.GetFileNameWithoutExtension(filename) };
        }
    }
}
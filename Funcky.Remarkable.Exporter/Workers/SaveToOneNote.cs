using System;
using System.Collections.Generic;
using System.Text;

namespace Funky.Remarkable.Exporter.OneNote
{
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Threading.Tasks;

    using console_csharp_connect_sample.Helpers;

    using Funcky.Remarkable.Exporter.Model;
    using Funcky.Remarkable.Exporter.OneNote;

    using Funky.Remarkable.Exporter.OneNote.Model;

    using global::OneNote.Net;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Graph;
    using Microsoft.Identity.Client;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using NLog;

    using File = System.IO.File;

    public class SaveToOneNote
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public async static Task Execute()
        {
            Logger.Info("Start synchronising to OneNote");

            var config = Configuration.Read();

            if (config?.Devices == null)
            {
                Logger.Warn("No configuration found, an empty one is created");
                Configuration.CreateEmptyConfiguration();
                return;
            }

            OneNoteConfiguration oneNoteConfiguration = config.OneNote;

            var tokenCachePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Configuration.ConfigurationPath), "token.cache");

            // Authenticate
            var clientApplication = PublicClientApplicationBuilder.Create(oneNoteConfiguration.ClientId)
                .WithAuthority(AadAuthorityAudience.PersonalMicrosoftAccount)
                .WithRedirectUri(oneNoteConfiguration.RedirectUri)
                //.WithTenantId("appds.onmicrosoft.com")
                .Build();

            var graphAuthenticationProvider = new MsalAuthenticationProvider(
                clientApplication,
                tokenCachePath,
                new[] { "Notes.Create", "Notes.ReadWrite" });

            var oneNoteAuthenticationProvider = new MsalAuthenticationProvider(
                clientApplication,
                tokenCachePath,
                new[] { "office.onenote_update" });

            var graphClient = new GraphServiceClient(graphAuthenticationProvider);

            var oneNoteClient = ClientFactory.CreateClient(oneNoteAuthenticationProvider);

            foreach (var device in config.Devices)
            {
                Logger.Info($"Processing device {device.Name}");

                
                var notebook = (await graphClient.Me.Onenote.Notebooks.Request()
                                    .Expand(n => n.Sections)
                                    .Filter($"displayName eq '{device.Name}'").GetAsync()).FirstOrDefault();
                
                if (notebook == null)
                {
                    continue;
                }

                //var sections = await oneNoteClient.GetNotebookSectionsAsync(notebook.Id);

                var baseDirectory = new DirectoryInfo(device.LocalPath);

                foreach (var file in baseDirectory.GetFiles("content.json", SearchOption.AllDirectories))
                {
                    var extractedPath = Path.Combine(file.DirectoryName ?? throw new ArgumentNullException(nameof(file.DirectoryName)), "content");

                    var contentJson = JObject.Parse(System.IO.File.ReadAllText(file.FullName));

                    var sectionName = contentJson.Value<string>("VissibleName");
                    if (sectionName.Length > 50)
                    {
                        sectionName = sectionName.Substring(0, 50);
                    }

                    var version = contentJson.Value<int>("Version");
                    var contentType = contentJson.Value<string>("Type");

                    if (contentType != "DocumentType")
                    {
                        Logger.Trace($"Ignoring content type {contentType} for {file.FullName}");
                        continue;
                    }

                    var oneNoteMarkerFile = Path.Combine(file.DirectoryName, "onenote.json");
                    var oneNoteMarker = File.Exists(oneNoteMarkerFile)
                                            ? JsonConvert.DeserializeObject<OneNoteMarker>(
                                                File.ReadAllText(oneNoteMarkerFile))
                                            : new OneNoteMarker();

                    if (oneNoteMarker.LastSavedVersion >= version)
                    {
                        Logger.Trace($"Already saved pages for version {version} of  {file.FullName}");
                        continue;
                    }

                    Logger.Info($"Uploading pages for version {version} of {file.FullName}");

                    var extractedDirectory = new DirectoryInfo(extractedPath);
                    var inkMLDirectory = extractedDirectory.GetDirectories("*.inkml").FirstOrDefault();
                    if (inkMLDirectory == null)
                    {
                        Logger.Warn($"Could not find .inkml directory in {extractedPath}");
                        continue;
                    }

                    var section = notebook.Sections.FirstOrDefault(s => s.DisplayName == sectionName);
                    if (section == null)

                    {
                        section = await graphClient.Me.Onenote.Notebooks[notebook.Id].Sections.Request()
                                      .AddAsync(new OnenoteSection { DisplayName = sectionName });
                        notebook.Sections.Add(section);
                    }



                    var oldPages = await graphClient.Me.Onenote.Sections[section.Id].Pages.Request().GetAsync();

                    foreach (var fileInfo in inkMLDirectory.GetFiles("*.xml").OrderBy(f => f.Name))
                    {
                        var baseFileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                        if (!System.Text.RegularExpressions.Regex.IsMatch(baseFileName, "^[0-9]+$"))
                        {
                            continue;
                        }

                        var pageIndex = int.Parse(baseFileName);

                        /*
                        var pageToDelete = pageIndex < oneNoteMarker.PageIds.Count
                                               ? oneNoteMarker.PageIds[pageIndex]
                                               : null;
                                               */

                        var pageHTML = $"<html><head><title>Page {pageIndex}</title></head></html>";
                        var htmlPart = new ByteArrayPartWithDisposition(
                            Encoding.UTF8.GetBytes(pageHTML),
                            "page.html",
                            "text/html",
                            "form-data; name=presentation");

                        var inkPart = new FileInfoPartWithDisposition(
                            fileInfo,
                            fileInfo.Name,
                            "application/inkml+xml",
                            "form-data; name=presentation-onenote-inkml");

                        Logger.Info("Uploading file: " + fileInfo.FullName);
                        var pageResponse = await oneNoteClient.CreatePageInSection(section.Id, htmlPart, inkPart);
                        var pageLocation = pageResponse.Headers.Location;
                        var newPage = pageResponse.Content;
                        Logger.Info($"Created new page at {pageLocation}");
                    }

                    // Now delete old pages
                    foreach (var existingPage in oldPages)
                    {
                        try
                        {
                            Logger.Info($"Deleting old page at {existingPage.Links.OneNoteWebUrl}");

                            //var pageResponse = await oneNoteClient.GetPageHtmlContent(existingPage.Id);
                            //File.WriteAllText(@"c:\temp\out.html",pageResponse);
                            

                            await graphClient.Me.Onenote.Pages[existingPage.Id].Request().DeleteAsync();
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, $"Failed deleting old page {existingPage.Links.OneNoteWebUrl}");
                        }
                    }

                    oneNoteMarker.LastSavedVersion = version;
                    File.WriteAllText(oneNoteMarkerFile, JsonConvert.SerializeObject(oneNoteMarker));
                }
            }

            Logger.Info("End Uploading files");        
        }

        public class OneNoteMarker
        {
            public int LastSavedVersion { get; set; }
        }
    }
}

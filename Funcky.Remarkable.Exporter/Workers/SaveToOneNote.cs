using System;
using System.Collections.Generic;
using System.Text;

namespace Funky.Remarkable.Exporter.OneNote
{
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text.RegularExpressions;
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

                    // Find the latest oneNote marker file for this notebook (may be a previous version).
                    var versionDirectories = from searchDir in file.Directory.Parent.GetDirectories()
                                             where Regex.IsMatch(searchDir.Name, "^[0-9]+$")
                                             orderby int.Parse(searchDir.Name) descending
                                             from fileInfo in searchDir.GetFiles("onenote.json")
                                             select fileInfo.FullName;

                    // Pick the onenote file for this specific version if it exists, otherwise get the latest
                    // onenote json file we have.
                    var oneNoteMarkerFile = 
                        File.Exists(Path.Combine(file.DirectoryName, "onenote.json")) 
                            ? Path.Combine(file.DirectoryName, "onenote.json")
                            : versionDirectories.FirstOrDefault();

                    var oneNoteMarker = oneNoteMarkerFile != null 
                                            ? JsonConvert.DeserializeObject<OneNoteMarker>(
                                                File.ReadAllText(oneNoteMarkerFile))
                                            : new OneNoteMarker();

                    if (oneNoteMarker.LastSavedVersion >= version)
                    {
                        Logger.Trace($"Already saved pages for version {version} of  {file.FullName}");
                        continue;
                    }

                    if (oneNoteMarker == null)
                    {
                        // Find the latest
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



                    var oldPages = await graphClient
                                       .Me
                                       .Onenote
                                       .Sections[section.Id]
                                       .Pages
                                       .Request(new[] { new QueryOption("pagelevel", "true") })
                                       .OrderBy("level,order")
                                       .GetAsync();


                    var filesInOrder = from inkMLFile in inkMLDirectory.GetFiles("*.xml")
                                       let baseName = Path.GetFileNameWithoutExtension(inkMLFile.Name)
                                       where Regex.IsMatch(baseName, "^[0-9]+$")
                                       let pageIndex = int.Parse(baseName)
                                       orderby pageIndex
                                       select (pageIndex, file: inkMLFile);

                    List<OnenotePage> pagesToDelete = new List<OnenotePage>();

                    int oneNoteIndex = -1;
                    foreach (var (pageIndex, fileInfo) in filesInOrder)
                    {
                        oneNoteIndex++;

                        var pageHTML = $"<html><head><title>Page {pageIndex + 1}</title></head></html>";
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

                        var htmlHash = GetHash(pageHTML);
                        var inkHash = GetHash(File.ReadAllText(fileInfo.FullName));

                        // Delete any pages in onenote at this index that do not match (may be an older version of this page, or 
                        // other pages in-between)
                        bool isMatch = false;
                        while (oldPages.Count > oneNoteIndex)
                        {
                            if (oneNoteMarker.PageHashes.TryGetValue(oldPages[oneNoteIndex].Id, out var pageHash))
                            {
                                if (pageHash.PageHtmlHash == htmlHash && pageHash.PageInkMLHash == inkHash)
                                {
                                    // Page matches and is in the right place.
                                    isMatch = true;
                                    break;
                                }

                                oneNoteMarker.PageHashes.Remove(oldPages[oneNoteIndex].Id);
                            }

                            // This page needs to be deleted
                            pagesToDelete.Add(oldPages[oneNoteIndex]);
                            oldPages.RemoveAt(oneNoteIndex);
                        }

                        if (isMatch)
                        {
                            Logger.Trace($"Page {pageIndex} already exists and matches, skipping.");
                            continue;
                        }

                        Logger.Info("Uploading file: " + fileInfo.FullName);
                        var pageResponse = await oneNoteClient.CreatePageInSection(section.Id, htmlPart, inkPart);
                        var pageLocation = pageResponse.Headers.Location;
                        var newPage = pageResponse.Content;
                        Logger.Info($"Created new page at {pageLocation}");

                        // Save marker file as we are going
                        oneNoteMarker.PageHashes.Add(newPage.Id, new OneNoteMarker.PageHash { PageHtmlHash = htmlHash, PageInkMLHash = inkHash });
                        File.WriteAllText(oneNoteMarkerFile, JsonConvert.SerializeObject(oneNoteMarker));
                    }

                    // Any remaining pages in the old pages collection past this index need to also be deleted
                    oneNoteIndex++;
                    while (oldPages.Count > oneNoteIndex)
                    {
                        pagesToDelete.Add(oldPages[oneNoteIndex]);
                        oldPages.RemoveAt(oneNoteIndex);
                    }


                    // Now delete old pages
                    foreach (var existingPage in pagesToDelete)
                    {
                        try
                        {
                            Logger.Info($"Deleting old page at {existingPage.ParentNotebook?.DisplayName} - {existingPage.ParentSection?.DisplayName} - {existingPage.Title}");

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

        private static string GetHash(string txt)
        {
            return Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(txt)));
        }

        public class OneNoteMarker
        {
            public int LastSavedVersion { get; set; }

            public Dictionary<string, PageHash> PageHashes { get; set; } = new Dictionary<string, PageHash>();

            public class PageHash
            {
                public string PageHtmlHash { get; set; }
                public string PageInkMLHash { get; set; }
            }
        }
    }
}

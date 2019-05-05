// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Prism">
//  Copyright (c) Prism. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

namespace Funcky.Remarkable.Exporter
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using Funcky.Remarkable.Exporter.Workers;

    using Funky.Remarkable.Exporter.OneNote;

    using Microsoft.Extensions.Configuration;

    using NLog;

    internal static class Program
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public static async Task<int> Main()
        {
            try
            {
                await SynchronizeNotes.Execute();

                ExtractNotes.Execute();

                DrawNotes.Execute(false);

                //SaveToEvernote.Execute();
                await SaveToOneNote.Execute();

                return 0;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return 1;
            }
            finally
            {
                LogManager.Shutdown();
            }
        }
    }
}
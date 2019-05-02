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

    using Microsoft.Extensions.Configuration;

    using NLog;

    internal static class Program
    {
        public static async Task Main()
        {
            await SynchronizeNotes.Execute();

            ExtractNotes.Execute();

            DrawNotes.Execute(false);

            SaveToEvernote.Execute();

            LogManager.Shutdown();
        }
    }
}
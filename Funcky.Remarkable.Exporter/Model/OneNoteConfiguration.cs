using System;
using System.Collections.Generic;
using System.Text;

namespace Funky.Remarkable.Exporter.OneNote.Model
{
    public class OneNoteConfiguration
    {
        /// <summary>
        /// Gets or sets the client identifier registered in Azure Portal.
        /// </summary>
        /// <value>
        /// The client identifier.
        /// </value>
        public string ClientId { get; set; }

        /// <summary>
        /// Gets or sets the redirect URI defined in the Authentication section
        /// of the Application in the Azure Portal
        /// </summary>
        /// <value>
        /// The redirect URI.
        /// </value>
        public string RedirectUri { get; set; }
    }
}

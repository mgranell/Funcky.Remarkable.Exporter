using System;
using System.Collections.Generic;
using System.Text;

namespace Funcky.Remarkable.Exporter.OneNote
{
    using System.IO;
    using System.Net.Http;

    using Refit;

    public class ByteArrayPartWithDisposition : ByteArrayPart
    {
        private readonly string disposition;

        public ByteArrayPartWithDisposition(byte[] value, string fileName, string contentType = null, string disposition = null)
            : base(value, fileName, contentType)
        {
            this.disposition = disposition;
        }

        protected override HttpContent CreateContent()
        {
            var content = base.CreateContent();
            content.Headers.Add("Content-Disposition", this.disposition);

            return content;
        }
    }
}

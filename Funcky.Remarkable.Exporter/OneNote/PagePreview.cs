using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace OneNote.Net
{
   public class PagePreviewResponse
   {
      [JsonProperty("previewText")]
      public string PreviewText { get; set; }

      [JsonProperty("links")]
      public PagePreviewLinks Links { get; set; }
   }

   public class PagePreviewLinks
   {
      [JsonProperty("previewImageUrl")]
      public PagePreviewLink ImageUrl { get; set; }
   }

   public class PagePreviewLink
   {
      [JsonProperty("href")]
      public string Href { get; set; }
   }
}

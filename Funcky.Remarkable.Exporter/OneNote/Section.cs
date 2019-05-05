using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace OneNote.Net
{
   public class SectionsResponse
   {
      [JsonProperty("@odata.context")]
      public string Context { get; set; }

      [JsonProperty("value")]
      public Section[] Sections { get; set; }
   }

   public class Section : OneNoteEntity
   {
      [JsonProperty("createdTime")]
      public DateTime CreatedTime { get; set; }
   }
}

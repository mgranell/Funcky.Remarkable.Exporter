using Newtonsoft.Json;
using System;

namespace OneNote.Net
{
   public class NotebooksResponse
   {
      [JsonProperty("@odata.context")]
      public string Context { get; set; }

      [JsonProperty("value")]
      public Notebook[] Notebooks { get; set; }
   }

   public class Notebook : OneNoteEntity
   {
      [JsonProperty("isDefault")]
      public bool IsDefault { get; set; }

      [JsonProperty("userRole")]
      public string UserRole { get; set; }

      [JsonProperty("isShared")]
      public bool IsShared { get; set; }
   }
}

using Refit;
using System;
using System.IO;
using System.Threading.Tasks;

namespace OneNote.Net
{
   [Headers("Authorization: Bearer")]
   public interface IOneNoteClient
   {
      [Get("/v1.0/me/notes/notebooks")]
      Task<NotebooksResponse> GetNotebooksAsync();

      [Get("/v1.0/me/notes/notebooks/{notebookId}/sections")]
      Task<SectionsResponse> GetNotebookSectionsAsync(string notebookId);

      [Get("/v1.0/me/notes/sections/{sectionId}/pages")]
      Task<PagesResponse> GetSectionPagesAsync(string sectionId);

      [Get("/beta/me/notes/pages/{pageId}/content?includeInkML=true")]
      Task<string> GetPageHtmlContent(string pageId);

      [Get("/v1.0/me/notes/pages/{pageId}/preview")]
      Task<PagePreviewResponse> GetPagePreview(string pageId);

      //https://www.onenote.com/api/v1.0/me/notes/resources/1-9f2f281b6f3143339e2407cd68b3e41f!1-a3887b9d-f776-406c-84d8-09917f1b7dca/$value
      [Get("/v1.0/me/notes/resources/{id}/$value")]
      Task<Stream> DownloadResource(string id);

      [Post("/beta/me/notes/pages")]
      [Multipart]
      Task<ApiResponse<PageCreateResponse>> CreatePage(MultipartItem htmlPart, MultipartItem inkML);

      [Post("/beta/me/notes/sections/{sectionId}/pages")]
      [Multipart]
      Task<ApiResponse<PageCreateResponse>> CreatePageInSection(string sectionId, MultipartItem htmlPart, MultipartItem inkML);
   }
}

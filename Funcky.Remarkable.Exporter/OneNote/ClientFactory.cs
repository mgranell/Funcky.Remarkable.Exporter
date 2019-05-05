using Refit;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OneNote.Net
{
    using Microsoft.Graph;

    public static class ClientFactory
   {
      public static IOneNoteClient CreateClient(IAuthenticationProvider authValueGetter)
      {
         var http = new HttpClient(new AuthenticatedHttpClientHandler(authValueGetter))
         {
            BaseAddress = new Uri("https://www.onenote.com/api")
         };

         return RestService.For<IOneNoteClient>(http);
      }

      private class AuthenticatedHttpClientHandler : HttpClientHandler
      {
         private readonly IAuthenticationProvider authProvider;

         public AuthenticatedHttpClientHandler(IAuthenticationProvider getToken)
         {
            this.authProvider = getToken ?? throw new ArgumentNullException("getToken");
         }

         protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
         {
            // See if the request has an authorize header
            AuthenticationHeaderValue auth = request.Headers.Authorization;
            if (auth != null)
            {
               await this.authProvider.AuthenticateRequestAsync(request);
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
         }

      }
   }
}

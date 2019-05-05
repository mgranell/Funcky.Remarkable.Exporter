/*
	Copyright (c) 2019 Microsoft Corporation. All rights reserved. Licensed under the MIT license.
	See LICENSE in the project root for license information.
*/

using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Graph;
using System.Linq;

namespace console_csharp_connect_sample.Helpers
{
    using System;
    using System.Collections.Generic;

    using NLog;

    /// <summary>
	/// This class encapsulates the details of getting a token from MSAL and exposes it via the 
	/// IAuthenticationProvider interface so that GraphServiceClient or AuthHandler can use it.
	/// </summary>
	/// A significantly enhanced version of this class will in the future be available from
	/// the GraphSDK team. It will support all the types of Client Application as defined by MSAL.
	public class MsalAuthenticationProvider : IAuthenticationProvider
	{		
		private IPublicClientApplication _clientApplication;
		private  string[] _scopes;

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public MsalAuthenticationProvider(
            IPublicClientApplication clientApplication, 
            string tokenCachePath,
            string[] scopes)
		{
			_clientApplication = clientApplication;
			_scopes = scopes;

            clientApplication.UserTokenCache.SetAfterAccess(args =>
                    {
                        System.IO.File.WriteAllBytes(
                            tokenCachePath,
                            clientApplication.UserTokenCache.SerializeMsalV3());
                    }
            );
            clientApplication.UserTokenCache.SetBeforeAccess(
                args =>
                    {
                        if (System.IO.File.Exists(tokenCachePath))
                        {
                            clientApplication.UserTokenCache.DeserializeMsalV3(System.IO.File.ReadAllBytes(tokenCachePath));
                        }
                    });
        }



        /// <summary>
        /// Update HttpRequestMessage with credentials
        /// </summary>
        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
		{
			var authentication = await GetAuthenticationAsync();
			request.Headers.Authorization = AuthenticationHeaderValue.Parse(authentication.CreateAuthorizationHeader());
		}

		/// <summary>
		/// Acquire Token for user
		/// </summary>
		public async Task<AuthenticationResult> GetAuthenticationAsync()
		{
			AuthenticationResult authResult = null;
			var accounts = await _clientApplication.GetAccountsAsync();

			try
			{
				authResult = await _clientApplication.AcquireTokenSilent(_scopes, accounts.FirstOrDefault()).ExecuteAsync();
			}
			catch (MsalUiRequiredException)
			{
				try
				{
                    var authResultTask = _clientApplication.AcquireTokenInteractive(_scopes).ExecuteAsync();

                    /*var authResultTask = _clientApplication.AcquireTokenWithDeviceCode(_scopes, result =>
                        {
                            Logger.Info($"Visit {result.VerificationUrl} with {result.UserCode}");
                            return Task.CompletedTask;
                        }).ExecuteAsync();*/
                    
                    authResult = await authResultTask;

                    Logger.Info("Successfully logged in with device code");
                }
				catch (MsalException)
				{
					throw;
				}
			}

			return authResult;
		}

	}
}

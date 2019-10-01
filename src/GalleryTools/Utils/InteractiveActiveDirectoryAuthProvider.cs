// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace GalleryTools.Utils
{
    public class InteractiveActiveDirectoryAuthProvider : SqlAuthenticationProvider
    {
        /// <summary>
        /// This is a special redirect URL that tells AAD to return the token directly and not redirect.
        /// </summary>
        private static readonly Uri EmptyRedirectUrl = new Uri("urn:ietf:wg:oauth:2.0:oob");

        public string ClientId { get; set; }

        public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            var authContext = new AuthenticationContext(parameters.Authority)
            {
                CorrelationId = parameters.ConnectionId
            };

            AuthenticationResult result;
            switch (parameters.AuthenticationMethod)
            {
                case SqlAuthenticationMethod.ActiveDirectoryInteractive:
                    Console.WriteLine("Authenticating with AAD interactively...");

                    result = await authContext.AcquireTokenAsync(
                        parameters.Resource,
                        ClientId,
                        EmptyRedirectUrl,
                        new PlatformParameters(PromptBehavior.Auto),
                        new UserIdentifier(
                            parameters.UserId,
                            UserIdentifierType.RequiredDisplayableId));
                    break;

                default: throw new InvalidOperationException();
            }

            Console.WriteLine("Authenticated with AAD interactively successfully.");
            return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
        }

        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
        {
            return authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryInteractive;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Moq;
using Newtonsoft.Json;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public static class TestExtensionMethods
    {
        public static void SetOwinContextCurrentUser(this AppController self, User user, IEnumerable<Scope> scopes = null)
        {
            var scopesString = JsonConvert.SerializeObject(scopes, Formatting.None);
            self.SetOwinContextCurrentUser(user, scopesString);
        }

        /// <summary>
        /// Should only be used in the rare cases where you are testing an action that
        /// does NOT use AppController.GetCurrentUser()! In those cases, use
        /// TestExtensionMethods.SetCurrentUser(AppController, User) instead.
        /// </summary>
        public static void SetOwinContextCurrentUser(this AppController self, User user, string scopes = null)
        {
            ClaimsIdentity identity = null;

            if (scopes != null)
            {
                identity = AuthenticationService.CreateIdentity(
                    user,
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty),
                    new Claim(NuGetClaims.Scope, scopes));
            }
            else
            {
                identity = new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, string.IsNullOrEmpty(user.Username) ? "theUserName" : user.Username) });
            }

            var principal = new ClaimsPrincipal(identity);

            var mock = Mock.Get(self.HttpContext);
            mock.Setup(c => c.Request.IsAuthenticated).Returns(true);
            mock.Setup(c => c.User).Returns(principal);

            self.OwinContext.Request.User = principal;
        }

        public static void SetCurrentUser(this AppController self, User user, IEnumerable<Scope> scopes)
        {
            self.SetOwinContextCurrentUser(user, scopes);
            self.SetCurrentUserOwinEnvironmentKey(user);
        }

        public static void SetCurrentUser(this AppController self, User user, string scopes = null)
        {
            self.SetOwinContextCurrentUser(user, scopes);
            self.SetCurrentUserOwinEnvironmentKey(user);
        }

        private static void SetCurrentUserOwinEnvironmentKey(this AppController self, User user)
        {
            self.OwinContext.Environment[Constants.CurrentUserOwinEnvironmentKey] = user;
        }

        public static async Task<byte[]> CaptureBody(this IOwinResponse self, Func<Task> captureWithin)
        {
            var strm = new MemoryStream();
            self.Body = strm;
            await captureWithin();
            return strm.ToArray();
        }

        public static async Task<T> CaptureBody<T>(this IOwinResponse self, Func<Task> captureWithin, Func<byte[], Task<T>> converter)
        {
            var data = await CaptureBody(self, captureWithin);
            return await converter(data);
        }

        public static Task<string> CaptureBodyAsString(this IOwinResponse self, Func<Task> captureWithin)
        {
            return CaptureBody<string>(self, captureWithin, bytes => Task.FromResult(Encoding.UTF8.GetString(bytes)));
        }
    }
}

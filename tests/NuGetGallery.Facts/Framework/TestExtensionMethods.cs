// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Moq;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public static class TestExtensionMethods
    {
        /// <summary>
        /// Should only be used in the rare cases where you are testing an action that
        /// does NOT use AppController.GetCurrentUser()! In those cases, use 
        /// TestExtensionMethods.SetCurrentUser(AppController, User) instead.
        /// </summary>
        /// <param name="name"></param>
        public static void SetCurrentUser(this AppController self, string name)
        {
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new [] { new Claim(ClaimTypes.Name, String.IsNullOrEmpty(name) ? "theUserName" : name) }));

            var mock = Mock.Get(self.HttpContext);
            mock.Setup(c => c.Request.IsAuthenticated).Returns(true);
            mock.Setup(c => c.User).Returns(principal);

            self.OwinContext.Request.User = principal;
        }

        public static void SetCurrentUser(this AppController self, User user)
        {
            SetCurrentUser(self, user.Username);
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


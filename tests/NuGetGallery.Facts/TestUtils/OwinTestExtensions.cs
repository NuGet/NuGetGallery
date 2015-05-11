// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Xunit;

namespace NuGetGallery
{
    public static class OwinTestExtensions
    {
        public static IOwinRequest SetUrl(this IOwinRequest self, string url)
        {
            Uri uri = new Uri(url);
            self.Scheme = uri.Scheme;
            self.Host = HostString.FromUriComponent(uri);
            self.PathBase = new PathString("");
            self.QueryString = QueryString.FromUriComponent(uri);
            self.Path = PathString.FromUriComponent(uri);
            return self;
        }

        public static IOwinRequest SetCookie(this IOwinRequest self, string name, string value)
        {
            // Force the cookies collection to be initialized.
            var _ = self.Cookies;

            // Grab the internal dictionary
            var dict = self.Get<IDictionary<string, string>>("Microsoft.Owin.Cookies#dictionary");

            // Set the cookie
            dict[name] = value;
            return self;
        }
    }
}

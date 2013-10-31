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
            self.Host = uri.Host;
            self.PathBase = "";
            self.QueryString = uri.Query.TrimStart('?');
            self.Path = uri.AbsolutePath;
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

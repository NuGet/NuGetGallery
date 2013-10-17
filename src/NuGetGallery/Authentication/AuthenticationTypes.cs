using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Authentication
{
    public static class AuthenticationTypes
    {
        public static readonly string Cookie = "cookie";
        public static readonly string ApiKey = "apikey";
        public static readonly string ResolvedUser = "resolveduser";
    }
}
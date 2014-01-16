using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Authentication
{
    public static class AuthenticationTypes
    {
        public static readonly string External = "External";
        public static readonly string LocalUser = "LocalUser";
        public static readonly string ApiKey = "ApiKey";
    }
}
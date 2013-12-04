using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Authentication
{
    public static class NuGetClaims
    {
        // Normally public consts are bad, but here we can't change the claim URL without messing
        // things up, so we should encourage that by using a const.
        public const string ApiKey = "https://claims.nuget.org/apikey";
    }
}
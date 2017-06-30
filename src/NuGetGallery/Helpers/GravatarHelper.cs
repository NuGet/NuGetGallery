// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Web;
using Microsoft.Web.Helpers;

namespace NuGetGallery.Helpers
{
    public static class GravatarHelper
    {
        private const string UnsecureGravatarUrl = "http://www.gravatar.com/";
        private const string SecureGravatarUrl = "https://secure.gravatar.com/";

        public static string Url(string email, int size)
        {
            var url = Gravatar.GetUrl(email, size, "retro", GravatarRating.G);

            if (url != null && ShouldUseSecureGravatar())
            {
                url = url.Replace(UnsecureGravatarUrl, SecureGravatarUrl);
            }

            return HttpUtility.HtmlDecode(url);
        }

        public static HtmlString Image(string email, int size, object attributes = null)
        {
            var gravatarHtml = Gravatar.GetHtml(email, size, "retro", GravatarRating.G, attributes: attributes);

            if (gravatarHtml != null && ShouldUseSecureGravatar())
            {
                gravatarHtml = new HtmlString(gravatarHtml.ToHtmlString().Replace(UnsecureGravatarUrl, SecureGravatarUrl));
            }

            return gravatarHtml;
        }

        private static bool ShouldUseSecureGravatar()
        {
            return (HttpContext.Current == null || HttpContext.Current.Request.IsSecureConnection);
        }
    }
}
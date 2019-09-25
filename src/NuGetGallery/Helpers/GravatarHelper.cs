// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Web;
using System.Web.Mvc;
using Microsoft.Web.Helpers;

namespace NuGetGallery.Helpers
{
    public static class GravatarHelper
    {
        private const string UnsecureGravatarUrl = "http://www.gravatar.com/";
        private const string SecureGravatarUrl = "https://secure.gravatar.com/";

        public static string Url(string email, int size)
        {
            // The maximum allowed Gravatar size is 512 pixels.
            if (size > 512)
            {
                size = 512;
            }

            var url = email == null ? null : Gravatar.GetUrl(email, size, "retro", GravatarRating.G);

            if (url != null && ShouldUseSecureGravatar())
            {
                url = url.Replace(UnsecureGravatarUrl, SecureGravatarUrl);
            }

            return HttpUtility.HtmlDecode(url);
        }

        public static HtmlString Image(UrlHelper url, string email, string username, int size, bool responsive)
        {
            var classAttribute = responsive ? "owner-image img-responsive" : "owner-image";

            // Load a higher resolution image than the element requires, to improve high DPI display.
            // However, the maximum allowed Gravatar size is 512 pixels.
            var imageSize = (size * 2 > 512) ? 512 : size * 2;

            var features = DependencyResolver.Current.GetService<IFeatureFlagService>();

            if (features.IsGravatarProxyEnabled())
            {
                var html = $@"<img src=""{url.Avatar(username, imageSize)}""
                                class=""{classAttribute}""
                                height=""{size}""
                                width=""{size}""
                                title=""{username}""
                                alt=""gravatar"" />";

                return new HtmlString(html);
            }

            if (email == null)
            {
                return new HtmlString(value: null);
            }

            var gravatarHtml = Gravatar.GetHtml(
                email,
                imageSize,
                "retro",
                GravatarRating.G,
                attributes: new
                {
                    width = size,
                    height = size,
                    title = username,
                    @class = classAttribute
                });

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
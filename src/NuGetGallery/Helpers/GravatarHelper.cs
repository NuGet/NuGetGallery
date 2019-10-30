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
        private const string SecureEnGravatarUrl = "https://en.gravatar.com/";

        /// <summary>
        /// Generates a URL to Gravatar that isn't proxied.
        /// </summary>
        /// <param name="email">The user's email address.</param>
        /// <param name="size">The size of the gravatar image.</param>
        /// <returns>The image URL, direct to Gravatar without proxying.</returns>
        public static string RawUrl(string email, int size)
        {
            return RawUrl(email, size, useEnSubdomain: false);
        }

        public static string RawUrl(string email, int size, bool useEnSubdomain)
        {
            // The maximum allowed Gravatar size is 512 pixels.
            if (size > 512)
            {
                size = 512;
            }

            var url = email == null ? null : Gravatar.GetUrl(email, size, "retro", GravatarRating.G);

            if (url != null && ShouldUseSecureGravatar())
            {
                var secureDomain = useEnSubdomain ? SecureEnGravatarUrl : SecureGravatarUrl;

                url = url.Replace(UnsecureGravatarUrl, secureDomain);
            }

            return HttpUtility.HtmlDecode(url);
        }

        /// <summary>
        /// Generates a URL to Gravatar that is proxied.
        /// </summary>
        /// <param name="email">The user's email address.</param>
        /// <param name="size">The size of the gravatar image.</param>
        /// <returns>The image URL, proxied.</returns>
        public static string Url(UrlHelper url, string email, string username, int imageSize)
        {
            var features = DependencyResolver.Current.GetService<IFeatureFlagService>();

            return (features.IsGravatarProxyEnabled())
                ? url.Avatar(username, imageSize)
                : RawUrl(email, imageSize);
        }

        /// <summary>
        /// Generate the HTML to a Gravatar image.
        /// </summary>
        /// <param name="url">The URL builder.</param>
        /// <param name="email">The user's email address.</param>
        /// <param name="username">The user's username.</param>
        /// <param name="size">The size of the gravatar image.</param>
        /// <param name="responsive">If true, the image will scale with its parent element.</param>
        /// <returns>The HTML for a Gravatar image.</returns>
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
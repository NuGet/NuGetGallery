// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public static class PackageHelper
    {
        public static string ParseTags(string tags)
        {
            if (tags == null)
            {
                return null;
            }
            return tags.Replace(',', ' ').Replace(';', ' ').Replace('\t', ' ').Replace("  ", " ");
        }

        public static bool ShouldRenderUrl(string url, bool secureOnly = false)
        {
            if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                if (secureOnly)
                {
                    return uri.Scheme == Uri.UriSchemeHttps;
                }

                return uri.Scheme == Uri.UriSchemeHttps
                    || uri.Scheme == Uri.UriSchemeHttp;
            }

            return false;
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Cookies
{
    public class CookieConsentMessage
    {
        public string Message { get; set; }

        public string MoreInfoUrl { get; set; }

        public string MoreInfoText { get; set; }

        public string MoreInfoAriaLabel { get; set; }

        public string[] Javascripts { get; set; }
    }
}
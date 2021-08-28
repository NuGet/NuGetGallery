// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class UrlHelperWrapper : IUrlHelper
    {
        private readonly UrlHelper _urlHelper;

        public UrlHelperWrapper(UrlHelper urlHelper)
        {
            _urlHelper = urlHelper ?? throw new ArgumentNullException(nameof(urlHelper));
        }

        public string Package(string id, string version, bool relativeUrl)
        {
            return _urlHelper.Package(id, version, relativeUrl);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Mvc;

namespace NuGetGallery
{
    public class AddPackageOwnerViewModel
    {
        public string Id { get; set; }
        public string Username { get; set; }

        [AllowHtml]
        public string Message { get; set; }
    }
}
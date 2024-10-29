// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Web.Mvc;

namespace NuGetGallery.RequestModels
{
    public class DeprecatePackageRequest
    {
        public string Id { get; set; }
        public IEnumerable<string> Versions { get; set; }
        public bool IsLegacy { get; set; }
        public bool HasCriticalBugs { get; set; }
        public bool IsOther { get; set; }
        public string AlternatePackageId { get; set; }
        public string AlternatePackageVersion { get; set; }

        [AllowHtml]
        public string CustomMessage { get; set; }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class ApiKeysViewModel
    {
        public string SiteRoot { get; set; }
        public IList<ApiKeyViewModel> ApiKeys { get; set; }
        public int ExpirationInDaysForApiKeyV1 { get; set; }
        public IList<string> PackageIds { get; set; }
    }
}
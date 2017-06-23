// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class ApiKeysViewModel
    {
        public string SiteRoot { get; set; }
        public IList<ApiKeyViewModel> ApiKeys { get; set; }
    }

    public class ApiKeyViewModel
    {
        public bool IsNonScopedV1ApiKey { get; set; }
        public bool HasExpired { get; set; }
        public string Expires { get; set; }
        public IList<string> ShortScopeList { get; set; }
        public IList<string> Scopes { get; set; }
        public IList<string> ShortPackageList { get; set; }
        public IList<IList<string>> PackageBatches { get; set; }
        public string GlobPattern { get; set; }
        public string Value { get; set; }
        public string Type { get; set; }
        public int Key { get; set; }
        public string Description { get; set; }
    }
}
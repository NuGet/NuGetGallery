// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ApiKeyListViewModel
    {
        public IList<ApiKeyViewModel> ApiKeys { get; set; }
        public int ExpirationInDaysForApiKeyV1 { get; set; }
        public IList<ApiKeyOwnerViewModel> PackageOwners { get; set; }
        public IList<GitHubFederatedToken> GitHubFederatedTokens { get; set; }
    }
}
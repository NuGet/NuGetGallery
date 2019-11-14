// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class ApiKeyRevokeViewModel
    {
        public ApiKeyViewModel ApiKeyViewModel { get; }
        public string ApiKey { get; }
        public string LeakedUrl { get; }
        public string RevocationSource { get; }
        public bool IsRevocable { get; }

        public ApiKeyRevokeViewModel(ApiKeyViewModel apiKeyViewModel, string apiKey, string leakedUrl, string revocationSource, bool isRevocable)
        {
            ApiKeyViewModel = apiKeyViewModel;
            IsRevocable = isRevocable;
            RevocationSource = revocationSource;
            LeakedUrl = leakedUrl;
            ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKeyViewModel));
        }
    }
}
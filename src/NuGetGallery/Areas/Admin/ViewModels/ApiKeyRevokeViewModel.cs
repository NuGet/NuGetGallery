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
        public bool IsRevocable { get; }

        public ApiKeyRevokeViewModel(ApiKeyViewModel apiKeyViewModel, string apiKey, string leakedUrl, bool isRevocable)
        {
            ApiKeyViewModel = apiKeyViewModel;
            IsRevocable = isRevocable;
            ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKeyViewModel));
            LeakedUrl = leakedUrl ?? throw new ArgumentNullException(nameof(leakedUrl));
        }
    }
}
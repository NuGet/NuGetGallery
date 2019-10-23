// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class ApiKeyRevokeViewModel
    {
        public ApiKeyViewModel ApiKeyViewModel { get; }
        public string ApiKey { get; }
        public string LeakedURL { get; }

        public ApiKeyRevokeViewModel(ApiKeyViewModel apiKeyViewModel, string apiKey, string leakedURL)
        {
            ApiKeyViewModel = apiKeyViewModel;
            ApiKey = apiKey;
            LeakedURL = leakedURL;
        }
    }
}
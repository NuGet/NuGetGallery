// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class DeleteAccountSearchResult
    {
        public string AccountName { get; }

        public DeleteAccountSearchResult(string accountName)
        {
            AccountName = accountName;
        }
    }
}
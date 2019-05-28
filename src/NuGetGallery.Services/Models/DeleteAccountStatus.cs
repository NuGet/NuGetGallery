// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services.Models
{
    public class DeleteAccountStatus
    {
        public bool Success { get; set; }

        public string Description { get; set; }

        public string AccountName { get; set; }
    }
}
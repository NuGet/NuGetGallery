// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class UserCredential 
    { 
        public string TenantId { get; set; }

        public string Type { get; set; }

        public string Value { get; set; }
    }
}
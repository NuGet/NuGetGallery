// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.Models
{
    public class ValidateUsernameResult
    {
        public bool IsFormatValid { get; set; }
        public bool IsAvailable { get; set; }
        public IEnumerable<string> OwnedPackageIds { get; set; } = new List<string>();
    }
}